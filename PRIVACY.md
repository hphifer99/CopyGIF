# CopyGIF Privacy Notice

This notice describes the data handled by the current CopyGIF Windows app. The GIF providers and GitHub operate their own services and policies.

## Network activity

CopyGIF sends searches and requests for GIF results to the provider you select, KLIPY or GIPHY. It also downloads images from URLs supplied by the provider. Provider API requests include the API key you enter. The provider and media hosts may receive your public IP address, search terms, and request information. Review the privacy terms for your selected provider before using its API.

Microsoft Store installations use the Store's update channel. Intentionally unsigned MSI installations from GitHub do not use CopyGIF's automatic updater. A signed MSI build can use GitHub release information and download a verified MSI when update checking is enabled.

## Local data

CopyGIF stores settings, favorites, recents, optional search history, cache files, update state, and diagnostic information in `%LOCALAPPDATA%\CopyGIF`. If you choose a custom library storage folder, the relevant GIF files are stored there. Clipboard GIF files are kept separately from Recents and older copies are cleaned up after later successful copies. Provider keys are saved separately under the `Secrets` folder using Windows Data Protection API for the current Windows user. They are not stored as plain text in `settings.json`. After a successful import from an older version, CopyGIF removes the old key field from its archived settings file.

CopyGIF does not include a CopyGIF account, its own advertising service, or a first-party analytics upload service.

## Removing your data

Uninstall CopyGIF through Windows Installed apps. To remove local app data afterward, delete `%LOCALAPPDATA%\CopyGIF` and any custom library folder you chose. This also deletes protected keys and local GIF copies. CopyGIF cannot remove copies that you have already pasted or saved in other applications.

For security issues, use the private reporting route in [SECURITY.md](SECURITY.md).
