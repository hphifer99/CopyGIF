# CopyGIF Privacy Policy

Last updated: September 20, 2026

This Privacy Policy explains how CopyGIF handles information when you use the CopyGIF Windows application. CopyGIF is a desktop utility that searches supported GIF providers and copies selected GIF files to the Windows clipboard.

## Summary

CopyGIF does not require a CopyGIF account and does not operate a server that collects app usage data. CopyGIF does not include first-party analytics, advertising, or telemetry uploads.

To provide its core features, CopyGIF sends requests to the GIF provider you select, currently KLIPY or GIPHY, and downloads GIF files from provider-supplied media locations. Those third-party services receive information needed to process the requests, as described below.

## Information handled by CopyGIF

### GIF searches and downloads

When you search for, view, or copy a GIF, CopyGIF may transmit the following information to the selected GIF provider or its media hosts:

- The search terms you enter.
- The provider API key you supplied.
- The requested content identifier or media URL.
- Your public IP address and standard network request information, which are necessarily available to the service receiving the request.

The provider may also associate requests with information it already maintains about your API key or provider account. CopyGIF uses these requests only to obtain search results, retrieve GIF metadata, validate provider credentials, and download the GIF you select.

CopyGIF does not send your provider API key, search terms, or selected GIFs to the CopyGIF developer.

### Information stored locally

CopyGIF may store the following information on your Windows device:

- Application settings and preferences.
- Favorites and recent GIF information.
- Search history, if search-history saving is enabled.
- Cached images and downloaded GIF files.
- Update state and locally generated diagnostic information.
- Provider API keys that you choose to save.

Most CopyGIF data is stored under `%LOCALAPPDATA%\CopyGIF`. If you select a custom library folder, relevant GIF files are stored in that folder. GIF files prepared for clipboard use are kept separately from Recents, and older clipboard copies are cleaned up after later successful copy operations.

Provider API keys are stored separately under the CopyGIF `Secrets` folder and protected for the current Windows user with the Windows Data Protection API. They are not stored as plain text in `settings.json`. When CopyGIF successfully imports settings from an older version, it removes the old API-key field from the archived settings file.

CopyGIF does not automatically upload local settings, favorites, recents, search history, cached files, diagnostic information, or provider keys to the CopyGIF developer.

### Application updates

Microsoft Store installations use the Microsoft Store update channel. Intentionally unsigned MSI installations distributed through GitHub do not use CopyGIF's automatic updater. If a separately signed MSI build supports update checking, it may request release information from GitHub and download an update from GitHub Releases.

Microsoft and GitHub may receive standard network and service information when their services are used. Their handling of that information is governed by their respective privacy policies.

## Third-party services

CopyGIF does not control how third-party services retain, use, or disclose information they receive. Review the policy for any service you use with CopyGIF:

- [KLIPY Privacy Policy](https://klipy.com/support/privacy-policy)
- [GIPHY Privacy Policy](https://support.giphy.com/hc/en-us/articles/360032872931-GIPHY-Privacy-Policy)
- [GitHub General Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement)
- [Microsoft Privacy Statement](https://privacy.microsoft.com/en-us/privacystatement)

## Your choices and controls

You can control the information handled by CopyGIF in the following ways:

- Choose which supported GIF provider to use.
- Remove a saved provider API key in CopyGIF Settings.
- Disable search-history saving and clear saved search history.
- Remove individual favorites or clear Favorites and Recents.
- Choose or change the folder used for locally stored library files.
- Stop provider requests by not performing searches or by removing the applicable API key. Core search and copy features will not work without a valid provider key.
- Contact the applicable provider regarding information retained by that provider.

## Retention and deletion

Local information remains on your device until it is cleared by you, removed by CopyGIF's normal cleanup behavior, replaced by newer data, or manually deleted.

To remove CopyGIF and its local information:

1. Uninstall CopyGIF through Windows Installed apps.
2. Delete `%LOCALAPPDATA%\CopyGIF` if it remains after uninstalling.
3. Delete any custom library folder you selected, if you no longer want its contents.

These steps remove locally protected provider keys and locally stored GIF copies. CopyGIF cannot remove GIFs that you already pasted into or saved through another application. Deleting local CopyGIF data also does not delete information retained independently by KLIPY, GIPHY, Microsoft, GitHub, or a media host.

## Security

CopyGIF uses HTTPS for supported provider and update requests. Saved provider API keys are protected locally with Windows Data Protection API for the current Windows user. No storage or transmission method can guarantee absolute security, so you should protect your Windows account and treat provider API keys as credentials.

CopyGIF does not automatically send diagnostic logs to the developer. If you voluntarily provide logs or other information when requesting support or reporting a security issue, the information you choose to provide will be processed through the service you use to submit the report.

## Children's privacy

CopyGIF is a general-audience productivity utility and is not directed to children under 13. CopyGIF does not knowingly collect personal information from children. The third-party GIF providers may have their own age requirements and policies.

## Changes to this policy

This policy may be updated when CopyGIF's features, supported services, or data-handling practices change. The revision date at the top of this document identifies the latest version.

## Contact

For general privacy questions that do not contain sensitive information, use the public [CopyGIF GitHub issue tracker](https://github.com/hphifer99/CopyGIF/issues). Do not post API keys, logs containing sensitive information, or other private data in a public issue.

For a sensitive privacy or security report, use the private reporting instructions in [SECURITY.md](SECURITY.md).
