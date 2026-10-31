# Decode measurements

Measured locally on the user's files on 2026-11-01 with `tools/Pikvew.Bench`.
These are loader/decoder measurements with warm filesystem caches, not end-to-end
window startup or monitor-rendering timings. The original files were read only.

| Input | Operation | Time |
| --- | --- | --- |
| `K:\ScanImage001.png` (14032 × 10208, 342 MB) | WIC full-resolution decode | 2.136 s |
| Same PNG | Pikvew load plus 4096-pixel fit preview | 2.337 s |
| `K:\4685896-bdsmlr-10356-r8vcfiol2b.gif` (1280 × 720, 119 frames) | Initial Pikvew load | 0.089 s |
| Same GIF, 4.957-second animation | Decode and cache all frames, first pass | 1.876 s |
| Same GIF | Second pass through cached frames | < 1 ms |

The old GIF path supplied no prior frame whenever the timer skipped past the
immediate dependency. That caused Skia to decode an increasingly long prefix again.
The new path advances through skipped frames incrementally and caches snapshots
within a 512 MiB budget per document. Backwards seeking and later loops reuse them.

Ordinary PNG avoids animated-format probing and intermediate Magick pixel copies.
WIC keeps the complete source; fit draws a reduced image to avoid uploading the
entire large bitmap just to show it at screen size. Zoom uses the original source.

Neighbour caching retains the document just left and preloads both adjacent files.
Document versions are tied to their original file metadata so an externally
replaced file cannot accidentally reuse its older decoded pixels.
