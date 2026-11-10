# WinGet Market

A native WinUI 3 app store interface for Windows Package Manager.

![WinGet Market](WinGetMarket/Assets/WinGetMarketLogo.png)

## Features

- First-launch WinGet verification and installation or repair
- Curated collections and the full WinGet community catalog
- Search, app details, and install buttons on app cards
- Installed app library, individual updates, and Update All
- Confirmation before installation, update, and removal
- Self-contained single executable publishing with a custom app icon

## Build

Requires Windows 10 version 2004 or newer (x64), the .NET 10 SDK, and NuGet network access. Visual Studio with WinUI development tooling is recommended.

```powershell
./WinGetMarket/build.ps1 -SingleFile
./WinGetMarket/single-exe/WinGetMarket.exe
```

Distribute the resulting EXE alone. Bundled .NET and Windows App SDK files extract to a per-user cache at launch. WinGet Market offers to install or repair WinGet when needed.

For a folder-based release, use `./WinGetMarket/build.ps1 -Publish` and distribute the entire `WinGetMarket/dist` folder.

## Tests

```powershell
dotnet run --project WinGetMarket.Tests
# Optional read-only checks against an installed WinGet:
dotnet run --project WinGetMarket.Tests -- --live-catalog --live-setup
```

The installation/repair and actual package modification flows require verification on a suitable Windows test machine. Automated checks do not install, update, or remove applications.

## Notes

The app invokes the installed WinGet client and parses its command output. Source agreements may need to be accepted in a terminal before catalog queries succeed. Installers may show UI and Windows permission prompts. Microsoft Store purchases are not supported.

This is an independent interface, not a Microsoft product. Microsoft's winget source and third-party dependencies are not bundled in this source repository. See [project documentation](WinGetMarket/README.md) for more details.
