# K-136 — do-not list

- Do not mix pixel sizes or introduce antialiased edges; preserve the original 3 px grid.
- Do not replace directional shading with smooth gradients, bloom, or glossy surfaces.
- Do not leave incomplete limbs, flat-cut boots, or implausible foot silhouettes.
- Do not add generic fantasy ornament or change the established character design.
- Do not introduce inconsistent lighting or an unoutlined sole across animation frames.

Research: https://pixelart.dev/guides/consistent-pixel-art-game-assets and https://runway.com/resources/why-do-ai-images-look-weird

## Status: incomplete; do not integrate

The eight back/front frames (rows 0 and 3, frames 0–3) need to be redone. The attempted mirror splits at the cell center even though the original complete leg crosses that center. It only transforms y=333–359, so it does not reliably copy the complete trouser and boot, preserve their full shape while lifting, or establish a correct overlap behind the coat. The outputs are drafts for inspection, not completed replacement assets. Side boot outlines were darkened and their bottom corners trimmed; visual approval is still needed.

## Files

- `Docs/References/codex_k136_v1/hero_walk_run_grid.png` and `hero_walk_run_grid_normal.png`: draft game-sized sheets.
- `hero_walk_run_grid_native.png` and `hero_walk_run_grid_normal_native.png`: native-size counterparts.
- `preview_4x.png`, `before_after_2x.png`, `manifest.json`, and Korean `README.md` in that directory.
- `_HTML/6_그림_화면/codex_K-136_주인공다리_v1.html`: self-contained Korean before/after animated review, labeled incomplete.
- `CARD_RESULT.md`: this report.

## Checks

- Game sheets are 1152×1536; native sheets are 384×512; preview is 1536×2048; before/after image is 4608×3072.
- Original color and normal cells are uniform on the 3 px grid; output cells were reconstructed on that grid. Per-cell color diff counts are in the README; y=0–332 has zero color differences in all cells.
- Every cell has opaque pixels at y=359; the opaque pixels on that row are 100% RGB<55 in every cell. Longest vertical silhouette edge is 15–27 px across cells. Full per-cell measurements are in the README.
- Both review-page PNG data URIs were decoded successfully at 1152×1536. Browser animation was not exercised.
- Normal-map copied regions were mirrored with R changed to 255−R, but the underlying leg geometry is incomplete as described above. Alpha matching and stray-pixel connectivity were not comprehensively validated.
- Unity was not opened, and no Git writes were performed. Game assets were read only.

## Leftovers

Redraw rows 0 and 3, frames 0–3 from the original sheets, isolating each entire source leg without cutting at x=48, then place it at the reference hip spacing and lift the specified complete leg by 6 px with the coat masking its top. Rebuild the corresponding normal geometry, confirm the toe highlights and soles, and inspect each frame and animation. Verify alpha alignment, stray pixels, and the review page in a browser before integration.
