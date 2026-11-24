# K-073 result

Completed archer asset delivery using D-081 (the stale NEEDS SIZE DECISION label is resolved): 96x128 native cells, approximately 73-pixel human figure, 3x nearest-neighbor game cells at 288x384, foot boundary (48,120)/(144,360).

## Work and files

- Created only `Docs/References/codex_k073_v1/`, `_HTML/4_그림_캐릭터/codex_K-073_궁수동작_v1.html`, and this result file.
- Delivered `archer_static.png` (1 frame), `archer_run.png` (4), `archer_fire.png` (6), `archer_fall.png` (4), with matching `*_native.png` and `*_4x.png` versions. Preserved replacement basenames and chronological frame counts/order; changed cell size as explicitly required by D-081.
- Static reuses K-070's approved-size back idle. Built-in ImageGen generated run, fire, and fall originals from K-070 archer references. Technical postprocessing extracts characters, normalizes scale/anchors, quantizes motion palettes, removes tiny disconnected noise, and packs sheets. Originals and prompts are retained.
- Included Korean README, manifest with pivots/frame order/source paths, reproducible build and browser verification scripts, contact sheet, validation JSON, source hashes, and review screenshots.
- Korean self-contained HTML embeds current Game sheets and new sheets, with animation, pause, step, flip, and speed controls. Existing sheets are aligned by their old foot pivot for comparison.

## Verification actually performed

- Validated all 15 native frames: RGBA, binary alpha, bottom-exclusive ground boundary y=120, fitting inside 96x128 cells, no disconnected components of 1-2 pixels. Every final frame has one connected visible component.
- Checked existing sheet dimensions/counts: 256x384 cells, 1/4/6/4 frames. Verified every delivered game sheet is exactly its native sheet scaled 3x with nearest-neighbor interpolation.
- Inspected generated images, final contact sheet, and desktop review screenshot visually.
- Headless installed Chrome opened the HTML through file://; all embedded images loaded, eight animated image sources loaded, eight canvases rendered, frame stepping/continuous playback/flip/speed selection worked. No script errors. No page overflow at 390px width. Evidence: `review_validation.json`, `review_desktop.png`, `review_mobile.png`.
- SHA-256 verification confirmed all 1,065 pre-existing Game files unchanged. No Unity launch, Play, in-game capture, Git writes, collaboration-log edits, or Game edits performed.

## Handoff / remaining work

- User visual review is still required; this delivery does not imply final art approval.
- Claude must copy the selected sheets into Game and adapt cell parsing and pivot handling to 288x384. Use normalized bottom-left Unity pivot (0.5,0.0625); manifest coordinates are top-left pixel coordinates. Preserve existing gameplay timing/event mapping.
- Run faces right; flip for left travel. Fire stays back-facing. Fall retains the same ground boundary while the figure progressively collapses; final ground contact is the prone body, not upright boots.
- Native motion normalization targets approximately 73px; a raised bow can extend above the head. Slight pose-specific silhouette changes remain intentional. No outstanding size question.
- Normal maps were outside the named card deliverables and were not generated. Claude/user should handle any normal-map integration separately if needed.
- Unity/in-game visual validation remains for the user/Claude after integration. No external communication sent.
