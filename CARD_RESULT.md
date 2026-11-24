# K-098 — Completed (assets only)

## Work performed

- Audited all color PNGs in BattleArt/Cannon, the fallback cannon_aim3 sheet, and all 35 BattleArt/Props PNGs: 43 sheets / 62 frames.
- Created 39 specular masks and reused 4 current-source-compatible K-092 masks (haul body, chassis, wheel, smoke). Rebuilt recoil and both crew masks because the K-092 entries did not match the current source hash and/or alpha. Existing files were preserved.
- Created 41 tangent-space normal maps. Reused the two existing current crew normal maps after verifying size, alpha and lossless native-grid reconstruction. New maps follow K-081: +X right, +Y up, +Z toward viewer; silhouette/weak-luminance height approximation, not a 3D bake.
- Created five pure-black contact shadows with continuous elliptical alpha falloff at 2x resolution: soldier, ogre, cannon, ladder pair, small prop. Recommended world dimensions and opacity are in the manifest.
- Preserved current source dimensions, frame order, pixel grids, alpha and attachment points. Native assets use lossless integer-grid recovery only; the manifest mask/normal paths are the game-resolution 1:1 assets. Did not force props into character cells.
- Added a self-contained Korean review page with synchronized source/mask/normal/lit frame previews, light rotation, gloss toggle, frame stepping, playback controls and all five shadows.

## Files changed

Only these authorized output locations were written:

- `Docs/References/codex_k098_v1/`
  - `masks/`: 39 new RGBA specular sheets.
  - `normals/`: 41 new RGBA normal sheets.
  - `native/`: 43 mask + 43 normal native-grid sheets, including native copies of reused assets.
  - `previews/`: 86 native-grid map previews at exact 4x nearest neighbor + 5 shadow previews at 4x delivered resolution.
  - `shadows/`: 5 contact-shadow PNGs at 2x their stated base resolution.
  - `manifest.json`: repository-relative source -> mask -> normal paths, hashes, sizes, cells, frames, attachment points, alpha bounds/bottoms, import settings, shadow dimensions and per-sheet validation table.
  - `README.md` (Korean), `build_assets.cjs`, `verify_assets.cjs`, `review_template.html`.
  - `validation.json`, `browser_validation.json`, `review_desktop.png`, `source_contact.png`.
- `_HTML/6_그림_화면/codex_K-098_접촉그림자광택_v1.html`.
- Root `CARD_RESULT.md` (this English handoff).

Reused assets remain at the exact existing repository paths listed in the manifest. The output folder is not a standalone import bundle; resolve those paths against the repository root.

## Verification performed

- `node Docs/References/codex_k098_v1/build_assets.cjs`: passed.
- `node Docs/References/codex_k098_v1/verify_assets.cjs`: passed for 43 sheets / 62 frames / 5 shadows.
- Reopened stored PNGs and checked exact source/mask/normal sizes, byte-identical alpha, B=0 for masks, transparent RGB, approximately unit normals with positive Z, unchanged source SHA-256, native round trips and exact 4x previews.
- Checked every frame's alpha bounds and cutout bottom against the current source. No alpha pixels were added; existing source specks were intentionally preserved.
- Checked shadow black RGB, exact 2x size, transparent edges, X/Y symmetry, monotonic falloff and single connected alpha support.
- Chrome headless loaded all 134 embedded images, rendered all 62 frames and passed next-frame/pause/resume checks. Five multi-frame sheets were exercised. Results: `browser_validation.json`.
- Opened and visually inspected the source contact board and the desktop review screenshot. Original/mask/normal/lit panels and shadow samples displayed correctly.
- No Unity execution, Game/ writes, collaboration-log writes or Git writes were performed. Unity lighting, actual shader decoding, flip behavior and final in-game appearance were not tested.

## Integration notes / leftovers

1. Current `crew_pull6.png` and `crew_push6.png` are 1728x384 with six 288x384 cells and foot anchor (144,360). `CannonArt.cs` and K-092 metadata still describe obsolete 384px-wide cells. Claude must reconcile the runtime layout when importing; no Game code was changed here.
2. Read masks as linear Default textures (R strength, G smoothness, B unused, A source alpha). Normal PNGs are green-up. Match Unity texture import type to PerfLabSprite's actual normal decoding; the review uses raw RGB decoding.
3. Shadow widths are starting recommendations, based on the current small unit scale. Tune placement, opacity and dimensions after integration. Use alpha-aware black blending; multiplying black RGB alone produces a rectangle.
4. Optional wall albedo masks were omitted. No required sprite is left without a mask/normal mapping.
5. Final visual approval and in-game validation remain with the user/Claude. No approval question is blocking this asset handoff.
