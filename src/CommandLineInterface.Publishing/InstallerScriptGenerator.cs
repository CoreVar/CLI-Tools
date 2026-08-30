namespace CoreVar.CommandLineInterface.Publishing;

public static class InstallerScriptGenerator
{
    public static string PowerShell(string product, Uri catalog, string channel = "stable") => $$"""
        # CoreVar CLI bootstrap installer for {{product}}
        [CmdletBinding()] param([string]$Version, [string]$Channel = '{{channel}}', [string]$InstallDir = "$env:LOCALAPPDATA\{{product}}", [switch]$NoPath)
        $ErrorActionPreference = 'Stop'
        $catalog = Invoke-RestMethod '{{catalog}}'
        if (-not $Version) { $Version = $catalog.channels.$Channel }
        $release = $catalog.releases | Where-Object version -eq $Version | Select-Object -First 1
        $rid = if ([Environment]::Is64BitOperatingSystem) { 'win-x64' } else { 'win-x86' }
        $artifact = $release.artifacts | Where-Object runtimeIdentifier -eq $rid | Select-Object -First 1
        if (-not $artifact) { throw "No artifact for $rid" }
        $temp = Join-Path ([IO.Path]::GetTempPath()) "{{product}}-$Version.zip"
        Invoke-WebRequest $artifact.uri -OutFile $temp
        $hash = (Get-FileHash $temp -Algorithm SHA256).Hash
        if ($hash -ne $artifact.sha256.Replace('sha256:','')) { throw 'SHA-256 verification failed' }
        $versionDir = Join-Path $InstallDir "versions\$Version"
        New-Item -ItemType Directory -Force $versionDir | Out-Null
        Expand-Archive $temp $versionDir -Force
        $launcher = Join-Path $versionDir '.corevar\launcher.exe'
        if (-not (Test-Path $launcher)) { throw 'The artifact does not contain the CoreVar stable launcher. Package it with cli-tools package --launcher.' }
        $bin = Join-Path $InstallDir 'bin'; New-Item -ItemType Directory -Force $bin | Out-Null
        Copy-Item $launcher (Join-Path $bin '{{product}}.exe') -Force
        @{ schemaVersion='1.0'; product='{{product}}'; version=$Version; channel=$Channel; catalog='{{catalog}}'; provider='direct'; entrypoint='{{product}}' } |
          ConvertTo-Json | Set-Content (Join-Path $InstallDir 'state.json') -Encoding utf8
        if (-not $NoPath) {
          $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
          if (($userPath -split ';') -notcontains $bin) { [Environment]::SetEnvironmentVariable('Path', (($userPath.TrimEnd(';') + ';' + $bin).TrimStart(';')), 'User') }
        }
        Remove-Item $temp -Force
        Write-Host "Installed {{product}} $Version to $InstallDir"
        """;

    public static string Shell(string product, Uri catalog, string channel = "stable") => $$"""
        #!/usr/bin/env sh
        set -eu
        VERSION="${VERSION:-}"
        CHANNEL="${CHANNEL:-{{channel}}}"
        INSTALL_DIR="${INSTALL_DIR:-$HOME/.local/share/{{product}}}"
        CATALOG='{{catalog}}'
        command -v curl >/dev/null || { echo 'curl is required' >&2; exit 69; }
        command -v python3 >/dev/null || { echo 'python3 is required by this bootstrap script' >&2; exit 69; }
        command -v unzip >/dev/null || { echo 'unzip is required' >&2; exit 69; }
        JSON="$(curl -fsSL "$CATALOG")"
        eval "$(COREVAR_CATALOG_JSON="$JSON" python3 - "$CHANNEL" "$VERSION" <<'PY'
        import json,os,platform,sys
        d=json.loads(os.environ['COREVAR_CATALOG_JSON']); channel,version=sys.argv[1:]
        version=version or d['channels'][channel]
        rid=('osx' if platform.system()=='Darwin' else 'linux')+'-'+({'x86_64':'x64','aarch64':'arm64'}.get(platform.machine(),platform.machine()))
        r=next(x for x in d['releases'] if x['version']==version)
        a=next(x for x in r['artifacts'] if x['runtimeIdentifier']==rid)
        print("VERSION='{}'".format(version)); print("URI='{}'".format(a['uri'])); print("SHA='{}'".format(a['sha256'].replace('sha256:','').lower()))
        PY
        )"
        TMP="${TMPDIR:-/tmp}/{{product}}-$VERSION.zip"
        curl -fsSL "$URI" -o "$TMP"
        ACTUAL="$(sha256sum "$TMP" 2>/dev/null | cut -d' ' -f1 || shasum -a 256 "$TMP" | cut -d' ' -f1)"
        [ "$ACTUAL" = "$SHA" ] || { echo 'SHA-256 verification failed' >&2; exit 65; }
        mkdir -p "$INSTALL_DIR/versions/$VERSION"
        unzip -q -o "$TMP" -d "$INSTALL_DIR/versions/$VERSION"
        LAUNCHER="$INSTALL_DIR/versions/$VERSION/.corevar/launcher"
        [ -f "$LAUNCHER" ] || { echo 'Artifact is missing the CoreVar stable launcher; package with cli-tools package --launcher' >&2; exit 65; }
        mkdir -p "$INSTALL_DIR/bin" "$HOME/.local/bin"
        cp "$LAUNCHER" "$INSTALL_DIR/bin/{{product}}"; chmod 0755 "$INSTALL_DIR/bin/{{product}}"
        ln -sf "$INSTALL_DIR/bin/{{product}}" "$HOME/.local/bin/{{product}}"
        printf '{"schemaVersion":"1.0","product":"{{product}}","version":"%s","channel":"%s","catalog":"%s","provider":"direct","entrypoint":"{{product}}"}\n' "$VERSION" "$CHANNEL" "$CATALOG" > "$INSTALL_DIR/state.json"
        rm -f "$TMP"
        echo "Installed {{product}} $VERSION to $INSTALL_DIR"
        """;
}
