# NextCloudShot

NextCloudShot is a self-hosted screenshot workflow for Nextcloud: a desktop capture/editor client that uploads directly into the user's files.

> Current status: **development scaffold / alpha foundation**. Windows is the first supported runtime for native capture and global hotkeys; the client architecture keeps capture, hotkeys and credential storage replaceable for Linux and macOS.

## Target workflow

`PrintScreen` -> select a region -> crop/annotate/censor -> upload to Nextcloud -> public link copied to clipboard.

`Alt + PrintScreen` -> capture the foreground window -> edit -> upload -> copy link.

## Included projects

| Path | Purpose |
| --- | --- |
| `NextCloudShot.Core` | Platform-neutral models and service contracts. |
| `NextCloudShot.Infrastructure.Nextcloud` | WebDAV upload and OCS public-share client. |
| `NextCloudShot.Platform.Windows` | Windows capture and native global hotkeys. |
| `NextCloudShot.Desktop` | Avalonia desktop application and screenshot editor. |

## What is already scaffolded

### Desktop client

- Avalonia desktop application shell, deliberately separated from OS-specific services.
- Native Windows hotkey service for `PrintScreen` and `Alt + PrintScreen`.
- Windows screen capture: virtual desktop and foreground window.
- Selection overlay for region capture.
- Editor window with crop, rectangle, arrow, freehand pen, text and pixelation tools.
- Skia-based renderer that produces the annotated PNG intended for upload.
- Nextcloud client for WebDAV `PUT` upload and OCS public link creation.
- Configuration model and clipboard integration points.

## Important boundaries

- The desktop client uploads through official WebDAV and OCS APIs. No custom Nextcloud app is required on the server.
- Screenshots remain ordinary files in the configured Nextcloud folder. The default follows the Nextcloud user language, for example `/Скриншоты` for Russian.
- The editor implementation is a strong starting foundation, not yet a production replacement for Yandex Disk Screenshots: multi-monitor DPI testing, undo/redo UX, installer, auto-update and end-to-end tests remain in the backlog.

## Desktop development

Prerequisites:

- .NET SDK 8 or newer
- Windows 10/11 for native capture/hotkey testing

```powershell
# From repository root, Visual Studio can open NextCloudShot.sln directly.
dotnet restore .\NextCloudShot.sln
dotnet build .\NextCloudShot.sln
dotnet run --project .\NextCloudShot.Desktop\NextCloudShot.Desktop.csproj
```

In the desktop settings screen provide:

- Server URL, for example `https://cloud.example.ru`
- Username
- Nextcloud app password, stored with Windows DPAPI after saving or a successful connection test
- Upload folder, localized from the Nextcloud user language by default

## Windows release packaging

```powershell
.\tools\publish-desktop-win-x64.ps1
```

The first installer target is Inno Setup. After installing Inno Setup locally, build the setup from `installer\windows\NextCloudShot.iss`; it consumes the published files in `artifacts\desktop-win-x64`.

If NuGet runtime-pack restore is slow or blocked locally, use `.\tools\publish-desktop-win-x64.ps1 -FrameworkDependent` for a quick installer smoke test; public release builds should stay self-contained in CI.

## First development priorities

See `docs/codex-first-tasks.md`. The first useful milestone is: run the Windows client, capture with both hotkeys, finish an annotated PNG, upload it to a real Nextcloud account and copy the public link.

## License

The intended project license is **AGPL-3.0-or-later**. The repository includes the GNU AGPL v3 license text; confirm copyright holder metadata before public publication.
