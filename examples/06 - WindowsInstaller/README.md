# CoreVar reference installer

From the repository root on Windows:

```powershell
dotnet tool install wix --version 4.0.6 --tool-path .tools
.\.tools\wix.exe extension add WixToolset.Bal.wixext/4.0.6
dotnet publish src/CoreVar.CliTools/CoreVar.CliTools.csproj -c Release -r win-x64 --self-contained true -o .artifacts/corevar-win-x64
dotnet .artifacts/corevar-win-x64/cli-tools.dll installer build --recipe "examples/06 - WindowsInstaller/corevar.windows.json" --wix .tools/wix.exe
```

This creates an unsigned MSI and a native **CoreVar CLI Tools** setup EXE for development. No certificate or account is required. For an official release, maintainers supply a certificate-store signing configuration and set `requireSigning: true` as described in [the installer guide](../../docs/installers.md).

Copy the recipe for your own CLI and change its name, publisher, source directory, executable, version, and both upgrade GUIDs. Do not reuse CoreVar's identities. Add `logoFile`, `iconFile`, and optionally a WiX `themeFile` for custom presentation. Add `downloadUrl` to produce a downloader for the built MSI; omit it for an offline bundle.

The Windows Installer/Burn engine supports the same install, repair, upgrade and uninstall behavior through graphical, passive, and quiet modes. `tests/windows_installer.py` builds the sources locally. Its `--install` switch is restricted to explicitly opted-in isolated CI runners so ordinary contributor tests do not register a product on their machine.
