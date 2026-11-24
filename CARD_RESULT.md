# K-080 result

Created hero animation assets matching the selected v6 original (`generated.png`), using D-081's decided 73-pixel figure scale. Preserved white hair, blue eyes, dark officer coat, gold epaulettes/trim and brown boots.

## Files changed

Only new files in `Docs/References/codex_k080_v1/`, the requested `_HTML/4_그림_캐릭터/codex_K-080_주인공동작_v1.html`, and this result file were written.

- Three game sheets: `hero_back_idle.png` (1 frame), `hero_walk_run_grid.png` (4x4), `hero_command.png` (3 frames).
- Corresponding `_native.png` sheets and `_4x.png` previews.
- Two generated source PNGs, full prompts, manifest, Korean README, reproducible build/verification scripts, validation JSON and browser screenshots.
- Self-contained Korean review page with all six motions animated beside the current game sheets, pause/speed/step controls and full-sheet previews.

No Game files, collaboration documents/logs or existing review pages were modified. No Unity execution or git writes were performed.

## Verified

- Read current sheets: grid rows are back walk, left side walk, left side run, front walk. Command order is back idle, hand raised beside head, extended command gesture.
- All 20 delivered cells: RGBA binary alpha, transparent RGB zero, figure height 73, baseline bottom-exclusive y=120, head/body registration axis within half a native pixel of x=48, no cell clipping, one connected opaque component (no disconnected stray pixels).
- Native cells 96x128; game cells 288x384, pivot (144,360). Game PNGs are pixel-exact nearest-neighbor 3x; previews are pixel-exact 4x.
- Visually inspected final movement/command sheets and desktop review screenshot.
- Opened the actual local review HTML in Chrome: all images decoded, 12 comparison canvases loaded, animation advanced, pause and step worked, mobile had no horizontal overflow, no page errors.
- SHA-256 of all three original Game PNGs still matches values recorded before generation.
- Detailed evidence: `Docs/References/codex_k080_v1/validation.json`.

## Decisions and remaining work

- The stale card title's size hold is resolved by user-provided D-081. The phrase “keep size” was interpreted according to the common contract: retain names/count/order, replace old 256x384 cells with 288x384 cells. Game sheet totals are 288x384, 1152x1536 and 864x384.
- Idle is exactly command frame 0 to avoid a separate idle/command-start size change.
- Asset generation used the built-in image tool; deterministic packing/alpha cleanup/palette normalization used Sharp. Full prompts are saved in `prompts.json`.
- Head size is approximate, not a certified per-frame anatomical measurement; every local outline width is not automatically certified as exactly one native pixel.
- Claude must copy/connect the three game sheets, update cell dimensions and pivots, and inspect animation transitions. The existing `hero_walk_run_grid_normal.png` was not modified and must not be assumed to match the new sheet.
- User visual acceptance and Unity Play/real captures remain pending. No runtime or actual-camera validation is claimed.
