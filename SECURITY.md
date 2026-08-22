# Security Policy

## Supported versions

Security fixes are provided for the latest published CopyGIF release. Pre-release builds and older releases may not receive separate patches.

## Report a vulnerability

Please use this repository's **Security** tab to open a private GitHub security advisory.

Include, when possible:

- the affected CopyGIF version or commit;
- Windows and .NET Framework versions;
- clear reproduction steps;
- the expected and observed behavior;
- the likely security impact; and
- a minimal proof of concept that does not expose third-party data or credentials.

Do not open a public issue for an unpatched vulnerability. Do not include a real KLIPY API key, personal data, or another person's content in a report.

## Scope notes

CopyGIF is a local Windows client that contacts KLIPY over HTTPS and downloads untrusted media. Relevant reports include, but are not limited to:

- unsafe file handling or path traversal;
- credential disclosure;
- arbitrary code execution;
- insecure update or installer behavior;
- unintended outbound data transmission; and
- security boundary failures involving the clipboard, cache, registry, or local library.

Availability failures in KLIPY itself, content-policy disputes, and vulnerabilities that exist only in an unsupported Windows version should normally be reported to the responsible vendor.
