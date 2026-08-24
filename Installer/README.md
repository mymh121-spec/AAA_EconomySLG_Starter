# Windows installer

The installer packages the complete Unity Windows player from
`Builds/Windows` into a single setup executable. It installs per-user by
default, registers an uninstaller, creates a Start menu shortcut, and offers
an optional desktop shortcut.

## Build

1. Build the Windows player with Unity's `Game.Editor.StandaloneBuild` method.
2. Install Inno Setup 6.
3. Run:

```powershell
powershell -ExecutionPolicy Bypass -File Installer/BuildInstaller.ps1 -Version 1.0.0
```

Generated files are written to `Builds/Installer` and remain outside Git.
