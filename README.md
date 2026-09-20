<p align="center">
  <img src="src\CopyGIF.App\Assets\CopyGIF-Logo-Lockup.png" alt="CopyGIF app icon">
</p>

# CopyGIF

CopyGIF is a Windows GIF picker. Press a global keyboard shortcut, search with KLIPY or GIPHY, and copy a GIF file to the clipboard.

## Features

- Open the search window with `Alt+G` by default, or use the tray icon.
- Search and browse GIFs with the provider you configure.
- Copy a real GIF file to paste into applications that accept file pastes.
- Keep Favorites, Recents, and optional search history on your device.
- Choose one of five animated GIF copy sizes and a GIPHY content rating in Settings.
- Clear saved search terms separately from your GIF Recents.
- Adjust the hotkey, window placement, appearance, startup, and update settings.
- Protect your provider API keys for your Windows user with Windows Data Protection API.

## Requirements

- Windows 10 or Windows 11, x64.
- A KLIPY or GIPHY API key for the provider you choose.

## Install

The public GitHub release build is a signed MSI installer. When a CopyGIF 2.x release is published, download its MSI from [GitHub Releases](https://github.com/hphifer99/CopyGIF/releases). You can verify its SHA-256 hash against the accompanying `CopyGIF-win-x64.sha256` file. The MSI installs for the current user only (into `%LOCALAPPDATA%\Programs\CopyGIF`) and never asks for administrator rights. The Microsoft Store package is a separate distribution channel.

To remove an MSI installation, use Windows **Installed apps**. Uninstalling the app does not remove your data in `%LOCALAPPDATA%\CopyGIF`.

## First run

1. Start CopyGIF and finish provider setup.
2. Add the API key for KLIPY or GIPHY when prompted.
3. Press `Alt+G` to open search, then choose a GIF to copy it.
4. Paste it into an application that accepts file pastes.

The app lives in the Windows notification area. Settings let you choose what happens when you close the search window or open it from the tray.
The content rating setting filters GIPHY search and trending requests. KLIPY uses the rating associated with your KLIPY API key. Both providers support local Favorites and Recents.

## Local data and privacy

CopyGIF stores settings, favorites, recents, search history (if enabled), caches, and protected API keys under `%LOCALAPPDATA%\CopyGIF`. Searches and GIF downloads contact your selected provider. Optional update checks contact GitHub Releases for the MSI distribution. See [PRIVACY.md](PRIVACY.md) for details.

## Build from source

Install Visual Studio with .NET desktop development, Windows App SDK/WinUI support, and the .NET SDK version specified in [global.json](global.json).

1. Clone the repository and open `CopyGIF.slnx`.
2. Select `x64` and build the solution.
3. Run the solution's test projects to check your build.

The GitHub Actions build checks the x64 solution and tests. GitHub Releases are created separately by the release workflow, which requires a signing certificate configured through GitHub Secrets.

## Security

Report vulnerabilities privately as described in [SECURITY.md](SECURITY.md). Do not post API keys, local data, or credentials in issues or logs.

## License and attribution

CopyGIF source code is licensed under the [MIT License](LICENSE.txt). The selected GIF provider and its media have separate terms. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the components installed with CopyGIF, provider information and the GIPHY attribution mark.
