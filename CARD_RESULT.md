# K-134 result

## Do not
- Do not mix pixel sizes or soften the existing 3× grid.
- Do not introduce jagged, irregular toe contours or disconnected pixels.
- Do not add airbrushed gradients, bloom, or noisy texture to the boots.
- Do not use pillow shading or change the existing top-left lighting and limited palette.

Research: https://pixelart.school/learn/pixel-art-terms ; https://www.wayline.io/learn/pixel-art/1

## Work completed
- Repaired the clipped right-side boots in walk row 1 frames 1 and 3, and moved corresponding normal-map edge data on the same 3-pixel grid.
- Scanned all 20 color-sheet cells across walk, back idle and command for horizontal and vertical alpha-boundary runs of at least 30 pixels, excluding ground-line soles. Two additional 30-pixel runs in walk row 2 frame 2 were judged intentional and left intact; see README.
- Created an animated Korean before/after review page, 4x frame crops and a manifest.

## Files
- `Docs/References/codex_k134_v1/hero_walk_run_grid.png`
- `Docs/References/codex_k134_v1/hero_walk_run_grid_normal.png`
- `Docs/References/codex_k134_v1/walk_r1_f1_before_4x.png`, `walk_r1_f1_after_4x.png`, `walk_r1_f3_before_4x.png`, `walk_r1_f3_after_4x.png`
- `Docs/References/codex_k134_v1/README.md`, `manifest.json`
- `_HTML/6_그림_화면/codex_K-134_주인공걷기발_v1.html`
- `CARD_RESULT.md`

## Verification
- Both output sheets are 1152×1536; color sheet retains transparency and all 16 frames end on y=359. Changed color blocks are uniform 3×3.
- Per-cell RGBA differences: color 405/405 and normal 504/459 for row 1 frames 1/3 respectively; every other cell is zero, preserving K-133.
- All review image paths exist and each PNG decodes with expected dimensions. A browser display/animation check could not be performed because no browser surface was available. Unity was not opened.
- No game files or Git state were modified.

## Open questions / leftovers
- Human visual review in the supplied page and Unity integration remain for the user and Claude. Browser rendering remains unverified.
- Collaboration live files were absent from this worktree and could not be checked here.
