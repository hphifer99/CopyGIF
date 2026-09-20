# Third-Party Notices

CopyGIF includes or communicates with the third-party components and services listed below.

This file has two parts. The first part covers the software that is installed together with CopyGIF. The second part covers the GIF services and the attribution material.

## Software that is installed with CopyGIF

CopyGIF is published as a self-contained application, so the installer includes the .NET runtime and the Windows App SDK. The exact package versions are the ones pinned in `Directory.Packages.props`. That file is the source of truth, so this notice does not repeat version numbers.

### .NET runtime, base class libraries and Windows Desktop runtime (including Windows Forms)

- License: MIT License. Copyright (c) .NET Foundation and Contributors.
- Source and license text: [github.com/dotnet/runtime](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT), [github.com/dotnet/winforms](https://github.com/dotnet/winforms/blob/main/LICENSE.TXT) and [github.com/dotnet/wpf](https://github.com/dotnet/wpf/blob/main/LICENSE.TXT).
- The .NET runtime itself uses third-party libraries that are under other licenses. Their notices are kept upstream in [THIRD-PARTY-NOTICES.TXT](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT).
- CopyGIF uses Windows Forms for the tray icon, the global hotkey window and the folder picker. The Windows Desktop runtime files can include other Windows Desktop components, which use the same license.

### Windows App SDK (includes WinUI 3)

- License: Microsoft Software License Terms for Microsoft Windows App SDK, which ships with the `Microsoft.WindowsAppSDK` NuGet package. It is not the MIT License. The text is at [nuget.org/packages/Microsoft.WindowsAppSDK](https://www.nuget.org/packages/Microsoft.WindowsAppSDK) under "License Info".
- Project: [github.com/microsoft/WindowsAppSDK](https://github.com/microsoft/WindowsAppSDK).
- The files that this package places next to CopyGIF are redistributed under the distributable code terms of that license.

### CommunityToolkit.Mvvm

- License: MIT License. Copyright (c) .NET Foundation and Contributors.
- Project and license text: [github.com/CommunityToolkit/dotnet](https://github.com/CommunityToolkit/dotnet).

### Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.DependencyInjection.Abstractions and Microsoft.Extensions.Http

- License: MIT License. Copyright (c) .NET Foundation and Contributors.
- Source and license text: [github.com/dotnet/runtime](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT).

### Build and test tools

`Microsoft.Windows.SDK.BuildTools`, MSTest, the WiX Toolset and the .NET SDK are used only while CopyGIF is built and tested. They are not part of the installed application.

### MIT License text

The following text applies to the MIT-licensed components above. The copyright holder for each component is named in its own entry.

```
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## GIF services and attribution

### KLIPY

- Developer site: [klipy.com/developers](https://klipy.com/developers)
- API documentation: [docs.klipy.com](https://docs.klipy.com/)
- Privacy policy: [klipy.com/support/privacy-policy](https://klipy.com/support/privacy-policy)
- Terms: [klipy.com/support/terms-services](https://klipy.com/support/terms-services)

CopyGIF uses KLIPY's hosted API and media delivery services. KLIPY software and media are not relicensed under CopyGIF's MIT License. The application displays `Powered by KLIPY` attribution.

CopyGIF is an independent project and is not affiliated with, endorsed by, or sponsored by KLIPY or Kikliko, Inc. KLIPY and related marks belong to their respective owners.


### GIPHY attribution mark

`src/CopyGIF.App/Assets/PoweredByGiphy.png` is an unaltered static attribution mark from GIPHY's official attribution archive, linked by https://developers.giphy.com/docs/api/. GIPHY and the GIPHY marks belong to their respective owner and are included for required API attribution. The image is not an original CopyGIF asset and is not relicensed under the application's source-code license. The application displays it on a black background for legibility.

The CopyGIF app icon and derived package assets use the project's official logo.
