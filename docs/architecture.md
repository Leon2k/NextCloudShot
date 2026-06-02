# Architecture

## Repository split

NextCloudShot is a desktop product. It owns native user interaction: global hotkeys, screen capture, region overlay, editor, uploading and clipboard.

There is no custom server application. Screenshots remain ordinary files and are browsed through the standard Nextcloud Files interface.

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

The first production platform is Windows. Linux/macOS implementations must provide only the OS-adapter layer; editor and uploading workflows remain shared.

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

The target folder default follows the Nextcloud user language, for example `/Скриншоты` for Russian. Users browse uploaded images in Nextcloud Files without installing a custom server component.
