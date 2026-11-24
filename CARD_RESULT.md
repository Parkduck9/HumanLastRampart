# K-099 Result

## Delivered
- Four source motions covered: goblin walk/attack, ladder goblin walk with ladder, ladder goblin carry/run, ogre walk.
- Eight transparent RGBA sheets: exact half-resolution derivatives preserving all source frames and row order; quarter-resolution two-frame walk derivatives with simplified material colours.
- Goblin half sheet preserves both original rows (4 walk + 4 attack). Other half sheets have 4 frames. Quarter sheets have 2 frames.
- Half cells: goblin/ladder 144x192, pivot (72,180); ogre 128x192, pivot (64,176). Quarter cells: 72x96 / (36,90), ogre 64x96 / (32,88). Pivots are top-left pixel-edge coordinates.
- Packed 1024x1024 atlas with at least 4px transparent outer padding and 8px between packed sheets; manifest includes PNG and Unity-origin rectangles, frame grids, order and pivots.
- Eight exact nearest-neighbour 4x previews, two comparison boards, Korean README, machine-readable verification report, and a self-contained Korean animated review HTML.

## Files changed
- New files only under `Docs/References/codex_k099_v1/` (22 files).
- `_HTML/6_그림_화면/codex_K-099_원거리저비용그림_v1.html`.
- This `CARD_RESULT.md`.
- No Game/, collaboration logs, other docs, or Git state modified. Pre-existing untracked CARD_CODEX_LOG.jsonl was left untouched.

## Verified
- All 28 delivered frames: dimensions, binary alpha, final opaque row ending exactly at the scaled foot baseline, fixed pivot and original spatial alignment.
- 8 sheet sizes/grids, exact 4x preview pixels, non-overlapping/in-bounds atlas rectangles, and byte-identical atlas crops versus standalone sheets.
- Removed isolated 1-2 pixel components; remaining connected component sizes are recorded per frame in verification.json. Intentional source silhouette parts are retained.
- Four source SHA-256 hashes unchanged. Review page's 18 embedded PNGs decode correctly.
- Headless Edge: all 16 animation/background images and both comparison boards load, 24 canvases render, animation changes frames, pause freezes frames, night and attack selectors work, no JavaScript errors, no horizontal overflow at 480px.
- Visually inspected source sheets, night reference, packed atlas and comparison artwork. Night previews use cropped reference backgrounds and simulated 24/14/9px cell heights; these are not engine captures.
- Unity was not opened/run. No runtime performance measurements or Git writes performed.

## Integration / leftovers
- Claude wires atlas/frame UVs and LOD selection. Use point sampling, mipmaps disabled, and clamp sampling within each frame. The existing full-sheet dimension autodetection must not be reused without adapting it to these smaller cells.
- K-099's explicit exact half/quarter dimensions take precedence over COMMON's full-size 96x128 / 3x contract; these are new LOD assets, not replacements for close-up art.
- Quarter tier is walk only; retain normal/half assets for close-range attacks, deployment and climbing. Suggested 60-110m half / >110m quarter thresholds require engine confirmation.
- Carry/run source itself has no visible ladder prop; the separate ladder_goblin_walk derivative retains its original visible ladder. No new equipment was invented.
- Actual distance projection, night lighting, LOD transition appearance and 4000+ enemy frame times remain to be tested by Claude/user. Smaller textures alone do not establish a performance gain or reduce overdraw.
