# Pikvew

A Windows image viewer for Windows 10/11 x64, built with WPF and .NET 10.

## Run

Launch `Pikvew.exe` without arguments for the folder browser, or pass an image path
to open it directly. A second launch forwards the new path to the existing window.
Files and directories can also be dropped onto the window. UNC shares are supported.

The browser provides a lazy folder tree, a virtualized thumbnail grid, multiple
selection, thumbnail sizing, natural/name/date sorting and a supported-format filter.
The viewer provides zoom/pan, animation seeking, frame stepping and fullscreen.
UI colors follow the Windows application theme; image margins stay black.

Common raster formats are decoded with Magick.NET. GIF/WebP animation uses Skia's
native frame compositor with a single decoded canvas; APNG has a managed fallback.
SVG uses the bundled RSVG renderer. Multi-page TIFF shows its first page with a notice.
An extension being recognized does not guarantee every variant of that format is decodable.

## Controls

| Key / action | Result |
| --- | --- |
| Enter / double-click | Toggle browser and image view |
| Esc | Exit after external image opening; return to browser after opening from the browser |
| Ctrl+Enter / middle click | Toggle fullscreen and windowed view |
| Wheel / PgUp / PgDn | Previous / next supported image; stop at folder boundaries |
| Home / End | First / last supported image |
| Ctrl+wheel | Zoom around the cursor |
| + / - | Zoom around the viewport center |
| / | 100%: one image pixel per physical screen pixel |
| * | Fit, including enlargement of small images |
| Drag / arrow keys | Pan an enlarged image |
| Space | Play / pause animation |
| , / . | Previous / next frame, paused |
| Del / Ctrl+Del | Recycle / permanently delete |
| Ctrl+C | Copy selected file(s) |

Animations always loop. Use the playback slider to seek. Fullscreen controls appear
on mouse movement and hide after two seconds. Esc never merely exits fullscreen.

Context menus provide image pixels, file/path copying, copy/move to a remembered
destination, deletion and the native Windows Open With dialog. Copy image places
bitmap and PNG data on the clipboard; for animation it copies the displayed frame.
Copy file preserves the encoded animation.

Copy/move never silently overwrites an existing file: rename or skip, with an
available name suggested. Copy and move remember different destination folders.
Deletion prompts only for multiple files. A share without a recycle bin can delete
permanently, including with Del, without an extra prompt, as explicitly requested.

## Layout

- `src/Pikvew.App`: WPF views, view models, imaging, folder catalog, registry,
  Windows shell/window placement, and Generic Host/DI composition root.
- `src/Pikvew.Core`: UI-independent sorting, navigation, viewport geometry,
  animation timeline, collision names and file transfer.
- `tests/Pikvew.Tests`: xUnit unit, imaging, corpus and WPF smoke tests.
- `PROJECT_STATE.md`: agreed requirements, current stage and remaining work.

Open `Pikvew.slnx` in Rider with .NET 10 support. Rider MCP is used for navigation
and inspections; ordinary tools are authorized for edits and build/test.

## Publish

Double-click `publish.bat` with a .NET 10 SDK installed. It publishes a Release,
win-x64, self-contained single-file `Pikvew.exe` into `artifacts/release/win-x64`.
Trimming is disabled for WPF. Bundled native/content files can extract at runtime.

If `C:\tools` exists, the script also copies the executable there, replacing the
previous copy. The whole console changes to green on success or red on failure
(including a failed copy), and waits for a keypress. An existing running executable
may need to be closed before replacing it.

## Settings

User preferences are stored under `HKEY_CURRENT_USER\Software\Pikvew`.
No administrator rights or adjacent settings JSON files are required.
Window placement, viewer mode, folder, selection, scroll, sorting, thumbnail size,
transparency background, and separate copy/move destinations are remembered.

## Verification

Run `dotnet test Pikvew.slnx` for the default suite. Double-click `test.bat` outside
the agent sandbox to also enable registry and named-pipe integration tests. Those
tests use isolated test keys/pipe names and do not overwrite application preferences.

Optional internet samples and approved reference frames can be downloaded with
`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/fetch-samples.ps1`.
They are pinned to revision `fb3714b6cfdbdae734e33b523e80c303bdfdae52` of
[whistyun/AnimatedImage](https://github.com/whistyun/AnimatedImage).
Downloaded files live in ignored `artifacts/samples`; corpus tests skip when absent.
Generated fixtures test decoding, orientation, timing and a two-minute GIF without downloads.
WPF smoke tests also exercise virtualization and browser/viewer/fullscreen transitions.

Manual acceptance remains necessary for actual mixed-DPI monitors, real UNC shares,
Open With, clipboard consumers, recycle-bin behavior and native integration outside
the sandbox. External file changes are detected by asynchronous metadata polling
every three seconds; unchanged listings keep their existing controls and selection.
