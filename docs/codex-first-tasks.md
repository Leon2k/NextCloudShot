# Tasks for Codex after initial import

The scaffold is meant to be pushed to Git first, then hardened in small verifiable changes.

## Milestone 1 — build and capture on Windows

- Restore/build `CloudShot.sln` on Windows with .NET 8 SDK.
- Fix any Avalonia/package API mismatches without collapsing the project boundaries.
- Launch desktop shell and ensure `PrintScreen` opens the region overlay.
- Ensure `Alt + PrintScreen` captures the foreground window.
- Test scaling at 100%, 125% and 150% DPI with one and two monitors.

Acceptance: both hotkeys reliably produce an editable source image.

## Milestone 2 — editor parity baseline

- Verify and repair pointer mapping in `AnnotationCanvasControl`.
- Complete crop, rectangle, arrow, freehand, text and pixelation interactions.
- Add undo/redo command stack and keyboard shortcuts.
- Add save-to-file and copy-image actions alongside upload.

Acceptance: the encoded PNG contains exactly the visible crop and annotations.

## Milestone 3 — real Nextcloud upload

- Move credentials into Windows Credential Manager/DPAPI implementation.
- Add login/connect test UI and error states.
- Verify WebDAV folder creation/upload and OCS link creation on the target instance.
- Decide share expiry default and expose it in settings.

Acceptance: uploaded file opens from the copied public URL.

## Milestone 4 — server companion

- Start the provided Nextcloud Docker environment.
- Build Vue assets and enable `cloudshot` app.
- Verify configured folder listing and image preview/gallery actions.
- Implement copy-link action using server-side OCS services or client endpoints.

Acceptance: screenshots uploaded by the desktop client appear in the web gallery.
