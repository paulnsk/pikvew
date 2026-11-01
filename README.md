# Pikvew

A Windows 10/11 image browser and viewer with GIF/WebP playback, seeking, zoom and pan. Portable, self-contained EXE; settings live in the registry.

| Shortcut | Action |
| --- | --- |
| Enter / double-click | Open folder/image; switch image view back to browser |
| Esc | Exit for externally opened images; otherwise return to browser |
| Ctrl+Enter / middle click | Toggle fullscreen |
| Wheel / PgUp / PgDn | Previous / next image |
| Home / End | First / last image |
| Ctrl+wheel | Zoom at cursor; resize thumbnails in browser |
| + / - | Zoom at center |
| / / * | 100% / fit (fit preference carries to following images) |
| Left press on reduced image | 100%; hold and drag to pan |
| Click enlarged image | Fit, unless dragged |
| Arrows / drag | Pan |
| Ctrl+Left / Right | Rotate 90 degrees |
| Ctrl+arrows in browser | Navigate folder tree without moving focus |
| Alt+Up / Left / Right | Parent folder / back / forward |
| I | Show / hide metadata; Pin keeps the panel open |
| Space / , / . | Play-pause / previous frame / next frame |
| Del / Ctrl+Del | Recycle / permanent deletion, with confirmation |
| Ctrl+C | Copy files; copy selected rows inside metadata panel |

Move to the bottom/right edge for controls/metadata. Run without arguments for the browser, or pass an image path. `publish.bat` builds the EXE; `test.bat` runs tests; `release.bat` publishes a GitHub release.