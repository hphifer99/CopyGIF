# Releasing CopyGIF

This checklist is the release gate for a public CopyGIF build.

## Go or no-go items

Complete these before announcing the first public release:

- Perform formal name and trademark clearance for `CopyGIF`. A basic search is not legal clearance.
- Confirm the integration in the KLIPY Partner Panel, request production access if required, and verify the current branding and attribution guidelines.
- Decide whether the first binaries will remain unsigned. Unsigned downloads can trigger Windows SmartScreen.
- Test on clean, supported Windows 10 and Windows 11 x64 systems.
- Make the repository public before advertising the PowerShell install command.

## Versioning

CopyGIF uses semantic release tags such as `v1.0.0`. Before tagging, update these attributes in `CopyGIF/Properties/AssemblyInfo.cs` to the same version:

```csharp
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
```

Keep the fourth assembly version component at `0`.

## Local build

Use a Visual Studio Developer PowerShell with `nuget.exe`, MSBuild, and the .NET Framework 4.8 targeting pack available:

```powershell
.\scripts\Build-Release.ps1 -Version 1.0.0
```

The script restores packages, rebuilds the project, stages the required executable and notices, creates `artifacts\CopyGIF-win-x64.zip`, and writes `artifacts\CopyGIF-win-x64.sha256`.

## Required Windows test matrix

Run every check on a clean standard-user profile, not only a development machine.

| Area | Checks |
| --- | --- |
| Install | Manual ZIP install and PowerShell installer complete without elevation |
| First launch | Welcome window appears, KLIPY link opens, blank and invalid keys cannot silently complete setup |
| Authentication | Valid key saves, survives restart, and is not present in plaintext in `settings.json` |
| Search | Typing debounce, Enter, and Search button work; network and rate-limit failures show useful messages |
| Animation | Tiles are static until hover, hovered GIF animates, leaving the tile stops animation, repeated use does not cause runaway memory growth |
| Clipboard | Selected item places a valid `.gif` file on the clipboard and pastes correctly into at least two target applications |
| Library | Favorites, Recents, limits, local-storage toggles, and Clear Recents work after restart |
| Window | `Alt+G`, alternate hotkey, Escape, focus-loss behavior, multi-monitor placement, DPI scaling, and tray actions work |
| Startup | Default startup value is created under HKCU, launches the installed executable after sign-in, and is removed when disabled |
| Update | Installing a newer release replaces program files without deleting `%LOCALAPPDATA%\CopyGIF` |
| Uninstall | Installed apps entry, shortcut, and startup value are removed; data is preserved by default and removed only with `-RemoveUserData` |
| Security | Release checksum matches, package contains all notices, no secrets are committed, and binaries are scanned before publication |

## Publish through GitHub Actions

1. Merge the release-preparation commit into `main` and confirm the `Build` workflow passes.
2. Create and push an annotated tag that matches `AssemblyInformationalVersion`:

   ```powershell
   git tag -a v1.0.0 -m "CopyGIF 1.0.0"
   git push origin v1.0.0
   ```

3. The `Release` workflow builds from that tag and creates or updates the GitHub Release with the ZIP and SHA-256 file.
4. Download both assets from GitHub and verify the checksum independently.
5. Test the published PowerShell installer from a clean Windows user profile.
6. Review the generated release notes, then publish the announcement.

## Rollback

If a published build is unsafe or materially broken, mark the release as a pre-release or remove its public availability, document the reason, fix forward on a new patch version, and do not reuse the affected tag or checksum.
