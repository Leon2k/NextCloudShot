# NextCloudShot

NextCloudShot is a self-hosted screenshot workflow for Nextcloud: a desktop capture/editor client plus an optional Nextcloud web gallery.

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
| `NextCloudShot.NextcloudApp` | Nextcloud app: gallery, personal settings and API. The Nextcloud app id is `nextcloudshot`. |
| `tools/dev-nextcloud` | Local Nextcloud 33 Docker environment. |

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

### Nextcloud web app

- Installable app id: `nextcloudshot`. Public App Store naming still needs review because Nextcloud guidelines disallow `Nextcloud` in app names.
- Target compatibility in `info.xml`: Nextcloud **32-33**.
- Navigation entry and Vue-based gallery view.
- API controllers for listing screenshots and storing each user's upload-folder setting.
- Files are not duplicated into an app database; the app works over the user's Nextcloud files folder.

## Important boundaries

- The web app is an optional companion. The desktop client uploads through official client APIs and remains usable without the server app.
- Publishing in the Nextcloud App Store requires completing security review, screenshots, signed releases and a complete AGPL-3.0-or-later licensing pass.
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
- Upload folder, default `/Screenshots`

## Windows release packaging

```powershell
.\tools\publish-desktop-win-x64.ps1
```

The first installer target is Inno Setup. After installing Inno Setup locally, build the setup from `installer\windows\NextCloudShot.iss`; it consumes the published files in `artifacts\desktop-win-x64`.

If NuGet runtime-pack restore is slow or blocked locally, use `.\tools\publish-desktop-win-x64.ps1 -FrameworkDependent` for a quick installer smoke test; public release builds should stay self-contained in CI.

## Nextcloud app development

The server app is a classic PHP/Vue Nextcloud app. For local startup:

```powershell
cd tools\dev-nextcloud
docker compose up -d
```

Then copy or symlink `NextCloudShot.NextcloudApp` into the development server custom apps folder as `nextcloudshot`, as described in `docs/nextcloud-app.md`.

For frontend assets:

```powershell
cd NextCloudShot.NextcloudApp
npm install
npm run build
```

## First development priorities

See `docs/codex-first-tasks.md`. The first useful milestone is: run the Windows client, capture with both hotkeys, finish an annotated PNG, upload it to a real Nextcloud account and copy the public link.

## License

The intended project license is **AGPL-3.0-or-later**, matching the distribution requirements for a public Nextcloud app. The repository includes the GNU AGPL v3 license text; confirm copyright holder metadata before public publication.
