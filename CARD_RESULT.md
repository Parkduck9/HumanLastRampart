# K-096 — Repair map UI kit

Completed assets only. No Game/ edits, Unity launch, Git writes, or collaboration log changes.

## Deliverables

- `Docs/References/codex_k096_v1/`: 29 individual transparent PNG assets: eight directional hover/selected wall outlines, five HP bar layers, twelve button backgrounds, and four coin/hammer icons.
- Seven sprite sheets and seven exact nearest-neighbor 4x previews.
- `repair_screen_mockup_1920x1080.png`: HP-colored castle, selected north wall, before/after HP, and the three requested Korean action labels.
- `manifest.json`, Korean `README.md` and `PROMPTS.md`, generation source, minimal read-only reference snapshots, validation reports, and two browser screenshots.
- `_HTML/6_그림_화면/codex_K-096_수리지도UI_v1.html`: standalone Korean review with 49 embedded images, comparison against the current game castle, directional selection, automatic outline animation, HP controls, and button stretching.

## Implementation details

K-091 v2 was absent from this worktree, so it was read from `C:/Unity/1/HumanLastRampart/Docs/References/codex_k091_v2` without modification. Relevant reference images and its manifest were copied into this card's output folder. Outlines retain its 256x256 canvas and (128,128) pivot. Damage patch composition clears replace_rect before drawing; outlines are then alpha-overlaid without clearing.

Built-in imagegen supplied the brass button and icon source atlas. Final assets use component extraction, nearest-neighbor normalization, fixed padding and 9-slice packing. Fixed-coordinate outlines and HP bars use deterministic UI geometry. No runtime code is delivered.

9-slice borders use Unity L/B/R/T order: buttons [28,16,28,16], HP frame/fills [8,8,8,8]. Character cell sizes, foot baselines, and 3x character sheets are not applicable to this UI kit.

## Verified

- All 29 individual assets: expected dimensions, transparency, transparent canvas edges, and one connected alpha component each; no isolated stray pixels.
- All eight outlines: no overlap with the opaque normal-wall silhouette; unchanged full-canvas alignment.
- All seven 4x previews: every pixel matches exact 4x source replication.
- The reference castle matches every decoded pixel of the current in-game castle. PNG file hashes differ because encoding differs.
- Mockup dimensions are 1920x1080; visually inspected the mockup, sheets, icons and browser screenshots.
- Chromium local-file review: all 49 embedded images decoded and all displayed images loaded. Four wall selections, hover mode, automatic cycling, zero HP, capped repaired HP, disabled variants, and 160/700px button stretching passed. See `validation.json` and `browser_verification.json`.

## Leftovers / integration notes

- Claude must import and wire the assets; Unity import, gameplay behavior, and user visual approval remain untested.
- HP thresholds and prices are illustrative, not an approved gameplay contract. Example repair prices use 0.4 gold per HP.
- Very small positive HP values use a minimum four-pixel inner fill; exact HP should also be shown as text.
- No unresolved asset-production questions. No commits were made.
