# CopyGIF Privacy Notice

Last updated: August 21, 2026

This notice describes the data behavior of the open-source CopyGIF Windows application. It does not replace KLIPY's own privacy policy or terms.

## Data CopyGIF sends

CopyGIF connects to KLIPY and KLIPY-hosted media endpoints over HTTPS in order to:

- validate the API key you provide;
- send GIF search terms;
- receive search results and media URLs; and
- download static thumbnails, animated previews, and selected GIF files.

The KLIPY API key is included in authenticated HTTPS request URLs as required by the KLIPY API. CopyGIF does not send the key anywhere else and does not write it to application logs.

As with any network service, KLIPY and its delivery providers receive connection metadata such as your public IP address. KLIPY's published privacy policy states that KLIPY may collect information including IP addresses, search terms, content viewed, and information about service usage. Review [KLIPY's Privacy Policy](https://klipy.com/support/privacy-policy) and [Terms of Service](https://klipy.com/support/terms-services) for KLIPY's practices.

## Data CopyGIF stores locally

CopyGIF stores the following data on the current Windows device:

| Location | Contents |
| --- | --- |
| `%LOCALAPPDATA%\CopyGIF\settings.json` | Settings, including the KLIPY API key encrypted with Windows Data Protection API for the current Windows user |
| `%LOCALAPPDATA%\CopyGIF\library.json` | Favorites and Recents metadata |
| `%LOCALAPPDATA%\CopyGIF\Favorites` | Favorite GIF files, when local favorite storage is enabled |
| `%LOCALAPPDATA%\CopyGIF\Recents` | Recent GIF files, when local recent storage is enabled |
| `%LOCALAPPDATA%\CopyGIF\Cache\Previews` | Cached animated previews, normally removed after seven days of inactivity |
| `%TEMP%\CopyGIF` | Temporary GIF files used for clipboard operations, normally removed after 24 hours |

If the normal preview cache cannot be created, CopyGIF may use `%TEMP%\CopyGIF\Cache\Previews` instead.

CopyGIF also creates a per-user Windows startup value named `CopyGIF` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` when **Start CopyGIF with Windows** is enabled.

## Data CopyGIF does not collect

CopyGIF does not include its own:

- telemetry or analytics service;
- advertising system;
- crash-report upload service;
- user account system; or
- remote CopyGIF database.

## Delete your local data

Exit CopyGIF, then delete `%LOCALAPPDATA%\CopyGIF` and `%TEMP%\CopyGIF`. The supplied uninstaller preserves user data by default. Run it with `-RemoveUserData` only if you also want these local files removed.

Disabling **Start CopyGIF with Windows** removes the startup value. The supplied uninstaller also removes that value.

## Questions and changes

This notice applies to CopyGIF itself. KLIPY controls the external API and media service and may change its practices independently. Review this notice and KLIPY's current policies when updating CopyGIF or before a public release.
