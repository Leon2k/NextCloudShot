# Architecture

## Repository split

NextCloudShot is deliberately one repository with two products:

1. **NextCloudShot Desktop** - owns native user interaction: global hotkeys, screen capture, region overlay, editor, uploading and clipboard.
2. **nextcloudshot Nextcloud App** - owns web browsing of screenshot files and server-side user settings; it is not required for desktop uploads.

The division prevents the desktop product from being blocked by Nextcloud app deployment or App Store review.

## Desktop boundaries

```text
NextCloudShot.Desktop (Avalonia UI)
      |
      +-- NextCloudShot.Core (contracts/models/workflows)
      +-- NextCloudShot.Infrastructure.Nextcloud (HTTP APIs)
      +-- NextCloudShot.Platform.Windows (capture/hotkeys)
```

Cross-platform preparation is made through interfaces in `NextCloudShot.Core`:

- `IGlobalHotkeyService`
- `IScreenCaptureService`
- `ICredentialVault`
- `IClipboardService`
- `IAnnotationRenderer`
- `INextCloudShotStorageClient`

The first production platform is Windows. Linux/macOS implementations must provide only the OS-adapter layer; editor, uploading and gallery workflows remain shared.

## Desktop capture workflows

### Region capture - PrintScreen

1. Native hotkey service receives `PrintScreen`.
2. Capture service takes a full virtual-desktop PNG.
3. Transparent selection overlay displays the captured surface and accepts a rectangular crop.
4. Editor opens with that crop as the active document bounds.
5. User draws annotations and clicks **Upload & copy link**.
6. Renderer composes final PNG; WebDAV uploads it; OCS Share API obtains/creates a public URL; clipboard receives URL.

### Active window - Alt + PrintScreen

1. Native hotkey service receives `Alt + PrintScreen`.
2. Windows capture adapter gets foreground window bounds and captures them.
3. Editor opens without mandatory region selection.
4. Upload/share behavior is identical.

## Annotation model

The editor does not burn pixels into the source during interaction. It maintains a `ScreenshotDocument` containing:

- immutable source PNG;
- optional crop rectangle;
- ordered annotation list.

Supported model types already represented in the scaffold:

- rectangle;
- arrow;
- freehand stroke;
- text;
- pixelation/censor region.

A later undo/redo stack should operate on annotation commands, not on repeated image encodes.

## Nextcloud integration

### Desktop API usage

The desktop client uses APIs exposed to third-party clients:

- `MKCOL` / `PUT` through WebDAV under `/remote.php/dav/files/{user}/...`;
- `POST` through OCS Share API at `/ocs/v2.php/apps/files_sharing/api/v1/shares` with `shareType=3` for a public link.

Credentials should ultimately be stored in an OS vault. The scaffold exposes `ICredentialVault`; the production Windows implementation should use DPAPI/Credential Manager and never persist an app password in JSON.

### Server application

The web app reads from a configured per-user folder, default `/Screenshots`, using Nextcloud's file APIs. It does not need to accept desktop uploads itself and does not duplicate screenshot metadata until there is a real need for extended link history/tagging.

## Version assumptions

As of 2026-05-26, supported Nextcloud server major versions are 32 and 33 and the latest maintenance version is 33.0.3. `NextCloudShot.NextcloudApp/appinfo/info.xml` is intentionally set to `min-version="32" max-version="33"`; adjust only after testing on the instance you deploy to.
