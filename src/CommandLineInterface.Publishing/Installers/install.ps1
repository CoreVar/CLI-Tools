#requires -Version 7.4
[CmdletBinding()]
param([string]$Version, [string]$Channel, [string]$InstallDir, [switch]$NoPath,
    [switch]$Quiet, [switch]$RequireSignature, [string]$TrustedKeys, [string]$Log)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$config = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__CONFIG__')) | ConvertFrom-Json -AsHashtable
if (-not $Channel) { $Channel = $config.channel }
if (-not $InstallDir) { $InstallDir = Join-Path $env:LOCALAPPDATA $config.product }
$InstallDir = [IO.Path]::GetFullPath($InstallDir)
$operation = $null; $temporary = $null; $transcribing = $false
function Assert-Segment([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value) -or $value -in '.', '..' -or $value.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or $value.Contains('/') -or $value.Contains('\')) { throw 'Invalid version or channel path segment' }
}
function Get-Artifact([string]$uri, [string]$path) {
    $source = [Uri]$uri
    if ($source.IsFile) { [IO.File]::Copy($source.LocalPath, $path, $true) }
    else { Invoke-WebRequest $source -OutFile $path -TimeoutSec 60 }
}
function Get-Digest([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function Normalize-Digest([string]$hash) { $hash.ToUpperInvariant().Replace('SHA256:', '').Replace('-', '') }
function Write-Atomic([string]$path, [byte[]]$bytes) {
    $next = $path + '.new-' + [Guid]::NewGuid().ToString('N')
    try { [IO.File]::WriteAllBytes($next, $bytes); [IO.File]::Move($next, $path, $true) }
    finally { if ([IO.File]::Exists($next)) { [IO.File]::Delete($next) } }
}
try {
    if ($Log) { Start-Transcript -LiteralPath $Log | Out-Null; $transcribing = $true }
    Assert-Segment $Channel
    [IO.Directory]::CreateDirectory($InstallDir) | Out-Null
    try { $operation = [IO.File]::Open((Join-Path $InstallDir '.operation.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
    catch { throw 'Another installation operation is running' }
    $temporary = Join-Path $InstallDir ('.setup-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($temporary) | Out-Null
    $catalogPath = Join-Path $temporary 'catalog.json'
    Get-Artifact $config.catalog $catalogPath
    $catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json -AsHashtable
    if ($catalog.product -cne $config.product) { throw 'Catalog product mismatch' }
    if (-not $Version) { $Version = $catalog.channels[$Channel] }
    Assert-Segment $Version
    $release = @($catalog.releases | Where-Object { $_.version -ceq $Version }) | Select-Object -First 1
    $rid = 'win-' + [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    $artifact = @($release.artifacts | Where-Object { $_.runtimeIdentifier -eq $rid }) | Select-Object -First 1
    if (-not $artifact) { throw "No release artifact for $rid" }
    foreach ($revocation in $catalog.revocations) {
        if ($revocation.version -eq $Version -or ($revocation.sha256 -and (Normalize-Digest $revocation.sha256) -eq (Normalize-Digest $artifact.sha256))) { throw 'The selected release has been revoked' }
    }
    $archive = Join-Path $temporary 'artifact.zip'
    Get-Artifact $artifact.uri $archive
    $hash = Get-Digest $archive
    if ($hash -ne (Normalize-Digest $artifact.sha256)) { throw 'SHA-256 verification failed' }
    $keys = $config.trustedPublicKeys
    if ($TrustedKeys) { $extra = Get-Content -LiteralPath $TrustedKeys -Raw | ConvertFrom-Json -AsHashtable; foreach ($key in $extra.Keys) { $keys[$key] = $extra[$key] } }
    if ($artifact.signature) {
        if (-not $artifact.signingKeyId -or -not $keys.ContainsKey($artifact.signingKeyId)) { throw 'Artifact signing key is not trusted' }
        $rsa = [Security.Cryptography.RSA]::Create()
        try {
            $rsa.ImportFromPem($keys[$artifact.signingKeyId])
            if (-not $rsa.VerifyData([Text.Encoding]::ASCII.GetBytes($hash), [Convert]::FromBase64String($artifact.signature), [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pss)) { throw 'Artifact signature is invalid' }
        } finally { $rsa.Dispose() }
    } elseif ($RequireSignature -or $config.requireSignature -or $artifact.signingKeyId) { throw 'A trusted signature is required' }
    $staging = Join-Path $temporary 'payload'
    [IO.Directory]::CreateDirectory($staging) | Out-Null
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $total = 0L; $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        if ($zip.Entries.Count -gt 100000) { throw 'Archive entry limit exceeded' }
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            if ($name.StartsWith('/') -or '..' -in $name.Split('/') -or $name.Contains(':')) { throw 'Archive path traversal was blocked' }
            $targetPath = [IO.Path]::GetFullPath((Join-Path $staging $name))
            if (-not $targetPath.StartsWith($staging + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not $names.Add($targetPath)) { throw 'Duplicate or invalid archive path' }
            $kind = ($entry.ExternalAttributes -shr 16) -band 0xF000
            if ($kind -notin 0, 0x8000, 0x4000) { throw 'Archive links are not supported' }
            $total += $entry.Length; if ($total -gt 4GB) { throw 'Archive extraction limit exceeded' }
            if ($name.EndsWith('/')) { [IO.Directory]::CreateDirectory($targetPath) | Out-Null; continue }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($targetPath)) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $targetPath, $false)
        }
    } finally { $zip.Dispose() }
    $hostName = $config.product + '.exe'
    if (-not [IO.File]::Exists((Join-Path $staging $hostName)) -or -not [IO.File]::Exists((Join-Path $staging '.corevar/launcher.exe'))) { throw 'Artifact is missing its host or stable launcher' }
    if ($release.bundle) {
        $bundlePath = Join-Path $staging '.corevar/module-bundle.json'
        Get-Artifact $release.bundle.manifest $bundlePath
        if ((Get-Digest $bundlePath) -ne (Normalize-Digest $release.bundle.sha256)) { throw 'Module bundle SHA-256 verification failed' }
    }
    $target = Join-Path $InstallDir "versions/$Version"
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    if ([IO.Directory]::Exists($target)) {
        $marker = Join-Path $target '.corevar-artifact.sha256'
        if (-not [IO.File]::Exists($marker) -or (Normalize-Digest ([IO.File]::ReadAllText($marker))) -ne $hash) { throw 'Existing version has different or unknown contents' }
    } else {
        [IO.File]::WriteAllText((Join-Path $staging '.corevar-artifact.sha256'), $hash)
        [IO.Directory]::Move($staging, $target)
    }
    $statePath = Join-Path $InstallDir 'state.json'
    $previous = if ([IO.File]::Exists($statePath)) { Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
    $pointers = @{}
    $modules = Join-Path $InstallDir 'modules'
    if (Test-Path -LiteralPath $modules) { Get-ChildItem -LiteralPath $modules -Directory | ForEach-Object { $p = Join-Path $_.FullName 'current.json'; if ([IO.File]::Exists($p)) { $pointers[$p] = [IO.File]::ReadAllBytes($p) } } }
    $bin = Join-Path $InstallDir 'bin'; [IO.Directory]::CreateDirectory($bin) | Out-Null
    $installed = Join-Path $bin $hostName
    $backup = if ([IO.File]::Exists($installed)) { [IO.File]::ReadAllBytes($installed) } else { $null }
    $committed = $false
    try {
        if ($release.postInstallArguments.Count -gt 0) {
            $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $target $hostName)); $start.UseShellExecute = $false
            $start.Environment['COREVAR_CLI_HOME'] = $InstallDir
            $start.Environment['COREVAR_CLI_ACTIVE_VERSION'] = $Version
            foreach ($name in 'COREVAR_MODULE_BUNDLE', 'COREVAR_MODULE_BUNDLE_SHA256', 'COREVAR_MODULE_BUNDLE_SNAPSHOT') { $start.Environment.Remove($name) | Out-Null }
            if ($release.bundle) {
                $start.Environment['COREVAR_MODULE_BUNDLE'] = Join-Path $target '.corevar/module-bundle.json'
                $start.Environment['COREVAR_MODULE_BUNDLE_SHA256'] = $release.bundle.sha256
                $start.Environment['COREVAR_MODULE_BUNDLE_SNAPSHOT'] = $release.bundle.snapshot
            }
            foreach ($argument in $release.postInstallArguments) { $start.ArgumentList.Add($argument) }
            $start.RedirectStandardOutput = [bool]$Quiet
            $start.RedirectStandardInput = [bool]$Quiet
            $child = [Diagnostics.Process]::Start($start)
            if ($Quiet) { $child.StandardInput.Close() }
            try { if ($Quiet) { $child.StandardOutput.ReadToEnd() | Out-Null }; $child.WaitForExit(); if ($child.ExitCode -ne 0) { throw "Post-install setup failed with exit code $($child.ExitCode)" } }
            finally { if (-not $child.HasExited) { $child.Kill($true) }; $child.Dispose() }
        }
        Write-Atomic $installed ([IO.File]::ReadAllBytes((Join-Path $target '.corevar/launcher.exe')))
        $state = @{ schemaVersion='1.0'; product=$config.product; version=$Version; channel=$Channel; catalog=$config.catalog; provider='direct'; entrypoint=$config.product;
            installationId=$(if ($previous.installationId) { $previous.installationId } else { [Guid]::NewGuid().ToString('N') });
            previousVersion=$(if ($previous.version -eq $Version) { $previous.previousVersion } else { $previous.version }) }
        Write-Atomic $statePath ([Text.Encoding]::UTF8.GetBytes(($state | ConvertTo-Json)))
        $committed = $true
    } finally {
        if (-not $committed) {
            if (Test-Path -LiteralPath $modules) { Get-ChildItem -LiteralPath $modules -Directory | ForEach-Object { $p = Join-Path $_.FullName 'current.json'; if ([IO.File]::Exists($p) -and -not $pointers.ContainsKey($p)) { [IO.File]::Delete($p) } } }
            foreach ($pointer in $pointers.Keys) { Write-Atomic $pointer $pointers[$pointer] }
            if ($null -ne $backup) { Write-Atomic $installed $backup } elseif ([IO.File]::Exists($installed)) { [IO.File]::Delete($installed) }
        }
    }
    if (-not $NoPath) {
        try { $userPath = [Environment]::GetEnvironmentVariable('Path', 'User'); if (($userPath -split ';') -notcontains $bin) { [Environment]::SetEnvironmentVariable('Path', (($userPath + ';' + $bin).Trim(';')), 'User') } }
        catch { Write-Warning "Installed successfully; add $bin to PATH manually." }
    }
    if (-not $Quiet) { Write-Host "Installed $($config.product) $Version to $InstallDir" }
} catch { [Console]::Error.WriteLine("Installation failed: $($_.Exception.Message)"); exit 1 }
finally {
    if ($temporary -and [IO.Directory]::Exists($temporary)) { [IO.Directory]::Delete($temporary, $true) }
    if ($operation) { $operation.Dispose() }
    if ($transcribing) { Stop-Transcript | Out-Null }
}
