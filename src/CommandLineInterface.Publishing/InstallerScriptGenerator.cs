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
        $arch = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
        $rid = 'win-' + $(switch ($arch) { 'x64' {'x64'} 'x86' {'x86'} 'arm64' {'arm64'} 'arm' {'arm'} default {$arch} })
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
        if ($release.bundle) {
          $bundleDir = Join-Path $versionDir '.corevar'; New-Item -ItemType Directory -Force $bundleDir | Out-Null
          $bundlePath = Join-Path $bundleDir 'module-bundle.json'
          Invoke-WebRequest $release.bundle.manifest -OutFile $bundlePath
          $bundleHash = (Get-FileHash $bundlePath -Algorithm SHA256).Hash
          if ($bundleHash -ne $release.bundle.sha256.Replace('sha256:','')) { throw 'Module bundle SHA-256 verification failed' }
          $env:COREVAR_MODULE_BUNDLE = $bundlePath
          $env:COREVAR_MODULE_BUNDLE_SHA256 = $release.bundle.sha256
          $env:COREVAR_MODULE_BUNDLE_SNAPSHOT = $release.bundle.snapshot
          if ($release.bundle.root) { $env:COREVAR_CLI_HOME = [Environment]::ExpandEnvironmentVariables($release.bundle.root) }
        }
        if ($release.postInstallArguments -and $release.postInstallArguments.Count -gt 0) {
          & (Join-Path $bin '{{product}}.exe') @($release.postInstallArguments)
          if ($LASTEXITCODE -ne 0) { throw "Post-install bootstrap failed with exit code $LASTEXITCODE" }
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
        import json,os,platform,shlex,sys
        d=json.loads(os.environ['COREVAR_CATALOG_JSON']); channel,version=sys.argv[1:]
        version=version or d['channels'][channel]
        rid=('osx' if platform.system()=='Darwin' else 'linux')+'-'+({'x86_64':'x64','aarch64':'arm64'}.get(platform.machine(),platform.machine()))
        r=next(x for x in d['releases'] if x['version']==version)
        a=next(x for x in r['artifacts'] if x['runtimeIdentifier']==rid)
        b=r.get('bundle') or {}; args=r.get('postInstallArguments') or []
        print('VERSION={}'.format(shlex.quote(version))); print('URI={}'.format(shlex.quote(a['uri']))); print('SHA={}'.format(shlex.quote(a['sha256'].replace('sha256:','').lower())))
        print('BUNDLE_URI={}'.format(shlex.quote(b.get('manifest','')))); print('BUNDLE_SHA={}'.format(shlex.quote(b.get('sha256','').replace('sha256:','').lower())))
        print('BUNDLE_SNAPSHOT={}'.format(shlex.quote(b.get('snapshot','')))); print('BUNDLE_ROOT={}'.format(shlex.quote(b.get('root',''))))
        print('POST_INSTALL_ARGS={}'.format(shlex.quote(json.dumps(args))))
        PY
        )"
        TMP="${TMPDIR:-/tmp}/{{product}}-$VERSION.zip"
        curl -fsSL "$URI" -o "$TMP"
        if command -v sha256sum >/dev/null 2>&1; then
          ACTUAL="$(sha256sum "$TMP" | cut -d' ' -f1)"
        else
          ACTUAL="$(shasum -a 256 "$TMP" | cut -d' ' -f1)"
        fi
        [ "$ACTUAL" = "$SHA" ] || { echo 'SHA-256 verification failed' >&2; exit 65; }
        mkdir -p "$INSTALL_DIR/versions/$VERSION"
        unzip -q -o "$TMP" -d "$INSTALL_DIR/versions/$VERSION"
        HOST_ENTRYPOINT="$INSTALL_DIR/versions/$VERSION/{{product}}"
        [ ! -f "$HOST_ENTRYPOINT" ] || chmod 0755 "$HOST_ENTRYPOINT"
        LAUNCHER="$INSTALL_DIR/versions/$VERSION/.corevar/launcher"
        [ -f "$LAUNCHER" ] || { echo 'Artifact is missing the CoreVar stable launcher; package with cli-tools package --launcher' >&2; exit 65; }
        mkdir -p "$INSTALL_DIR/bin" "$HOME/.local/bin"
        cp "$LAUNCHER" "$INSTALL_DIR/bin/{{product}}"; chmod 0755 "$INSTALL_DIR/bin/{{product}}"
        ln -sf "$INSTALL_DIR/bin/{{product}}" "$HOME/.local/bin/{{product}}"
        printf '{"schemaVersion":"1.0","product":"{{product}}","version":"%s","channel":"%s","catalog":"%s","provider":"direct","entrypoint":"{{product}}"}\n' "$VERSION" "$CHANNEL" "$CATALOG" > "$INSTALL_DIR/state.json"
        if [ -n "$BUNDLE_URI" ]; then
          BUNDLE_PATH="$INSTALL_DIR/versions/$VERSION/.corevar/module-bundle.json"
          curl -fsSL "$BUNDLE_URI" -o "$BUNDLE_PATH"
          if command -v sha256sum >/dev/null 2>&1; then
            BUNDLE_ACTUAL="$(sha256sum "$BUNDLE_PATH" | cut -d' ' -f1)"
          else
            BUNDLE_ACTUAL="$(shasum -a 256 "$BUNDLE_PATH" | cut -d' ' -f1)"
          fi
          [ "$BUNDLE_ACTUAL" = "$BUNDLE_SHA" ] || { echo 'Module bundle SHA-256 verification failed' >&2; exit 65; }
          export COREVAR_MODULE_BUNDLE="$BUNDLE_PATH" COREVAR_MODULE_BUNDLE_SHA256="$BUNDLE_SHA" COREVAR_MODULE_BUNDLE_SNAPSHOT="$BUNDLE_SNAPSHOT"
          [ -z "$BUNDLE_ROOT" ] || export COREVAR_CLI_HOME="$BUNDLE_ROOT"
        fi
        if [ "$POST_INSTALL_ARGS" != '[]' ]; then
          COREVAR_POST_INSTALL_ARGS="$POST_INSTALL_ARGS" python3 - "$INSTALL_DIR/bin/{{product}}" <<'PY'
        import json,os,subprocess,sys
        raise SystemExit(subprocess.call([sys.argv[1], *json.loads(os.environ['COREVAR_POST_INSTALL_ARGS'])]))
        PY
        fi
        rm -f "$TMP"
        echo "Installed {{product}} $VERSION to $INSTALL_DIR"
        """;
}
