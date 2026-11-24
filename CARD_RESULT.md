# K-138 — Avoiding generic AI art

- Do not mix pixel sizes or blur the existing 3 px grid.
- Do not use soft gradients or centered pillow shading; keep the existing top-left light.
- Do not hide either leg in a vague, symmetrical silhouette; preserve readable knee and boot poses.
- Do not add glow, noise, decorative details, or colors outside the current hero palette.
- Do not introduce stray pixels or flickering contours between frames.

Research: https://ojs.aaai.org/index.php/AIIDE/article/download/21951/21720/26003 and https://pixor.art/learn/outlines/

## Outcome

Incomplete. An image-generation attempt redrew upper bodies and side rows, violating the pixel-identical constraint. It was rejected. All eight target frames (back 0–3 and front 0–3) use the specified unchanged fallback; no new leg poses were delivered. The normal map is unchanged because the sprite is unchanged. Do not integrate this as a completed K-138 asset.

## Files

- `Docs/References/codex_k138_v1/hero_walk_run_grid.png` and `hero_walk_run_grid_normal.png`: exact copies of the current game sheets.
- `Docs/References/codex_k138_v1/comparison.png`: reference/current/fallback comparison, 2x, on stone grey.
- `Docs/References/codex_k138_v1/README.md` and `manifest.json`: Korean review notes and sheet metadata.
- `_HTML/6_그림_화면/codex_K-138_주인공앞뒤다리_v1.html`: animated reference/current/fallback review.
- `CARD_RESULT.md`: this report.

## Verification

- HEAD is d0dba54d. No Git writes, Unity execution, or Game changes were made.
- The game sprite, normal and old reference are 1152x1536. SHA-256 hashes of each delivered sheet match its game source, proving zero pixel changes in every cell.
- All 16 cells end at y=359; every opaque pixel in each cell's bottom row has all RGB components below 55. This is a per-cell bottom-row check, not a separate check of each grounded boot.
- Comparison image was visually inspected. HTML image paths refer to existing files, but browser rendering and animation were not tested.
- Alpha/normal correspondence, isolated pixels, and 31-pixel vertical edges were not checked. Native sheets and 4x previews were not produced because no new art was completed.

## Leftovers

Redraw and review all eight leg frames, preserve all non-leg pixels, update normals, produce native and 4x sheets, and perform all remaining checks before integration. No questions for the user.
