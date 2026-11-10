# WinGet Market

A separate native WinUI 3 storefront for Windows Package Manager. Includes curated collections, live catalog search, installed packages, available upgrades, package information, and confirmed install/update/uninstall actions.

## Build

Windows 10 2004 or newer, x64, .NET 10 SDK, and network access to NuGet are required. Visual Studio with WinUI development tools is recommended. Install Microsoft App Installer to provide winget.exe.

```powershell
.\build.ps1 -Publish
.\dist\WinGetMarket.exe
```

The app publishes unpackaged with a self-contained Windows App SDK and .NET runtime. Distribute the entire dist folder. This is a storefront interface, not a Microsoft Store submission package.

## Behavior

Discover collections are curated package IDs, not live popularity rankings. Search, library, updates, and detail views invoke the installed winget.exe against the winget community source. Microsoft Store purchases are not supported. Version shown as Latest on curated cards is a placeholder until WinGet retrieves package details.

Package changes require a review dialog and explicit confirmation. The app does not silently accept package or source agreements. When WinGet requires source agreement acceptance, review and accept it in a terminal using `winget search --source winget`, then retry. Installers may display UI and UAC prompts. The application does not install or update anything on startup.

CLI tables are parsed using column positions. Truncated package IDs are omitted to avoid selecting the wrong package. Changes to winget's table format may require parser adjustments. Nonzero exit codes and command output appear in the status bar. Operations time out after 30 minutes; check installer state before retrying.

## Source provenance

Created in response to the supplied winget-cli-master.zip. The archive's README and search command documentation were inspected as reference material. This UI invokes the existing client; it does not compile, bundle, or modify Microsoft's C++ winget source. Microsoft retains ownership of the Windows Package Manager project. This app is an independent interface.

## Branding

The sidebar header uses Assets/WinGetMarketLogo.png, a custom WinGet Market logo. The asset is included in both build and publish output, and the header exposes an accessible application name.


The executable embeds Assets/WinGetMarket.ico and uses the same icon for its window. To regenerate the nine icon sizes from Assets/WinGetMarketIcon.png, run create-icon.ps1 before publishing.


Updates includes an Update All button for eligible upgrades in the winget community source. The button requires confirmation, displays progress and errors, and refreshes the list afterward. It follows WinGet defaults for pinned packages and unknown versions.


## Single executable

Run `./build.ps1 -SingleFile` to create `single-exe/WinGetMarket.exe`. This is a self-contained x64 app including .NET, Windows App SDK, the sidebar logo, and the application icon. Distribute just that EXE. At launch, .NET extracts bundled runtime and content files into its per-user cache; first launch can take longer. The installed Windows Package Manager (App Installer) is still required for catalog and package operations.

## First-launch setup

On first launch, a setup screen verifies `winget --version`. Existing working installations can continue immediately. The Install or repair button asks for confirmation, installs Microsoft's Microsoft.WinGet.Client PowerShell module from PSGallery in CurrentUser scope, and runs Repair-WinGetPackageManager. Microsoft Store is offered as an alternative. The app verifies WinGet again after installation; failed checks show retry and diagnostic output. Setup completion is saved in `%LOCALAPPDATA%/WinGetMarket/setup-v1.complete` only when the user continues after verification. Each later launch checks WinGet again and returns to setup if needed. No catalog packages are installed by setup.
