# K-137 result

## Card-specific anti-slop constraints
- Do not mix pixel sizes or soften the 3 px grid with filtered edges.
- Do not use airbrushed gradients, glow, or glossy plastic highlights on boots.
- Do not make both legs identical cut-and-paste shapes; show the alternating knee and foot.
- Do not use featureless straight sole strips or hide the toe and heel silhouette.
- Do not change the established palette or reverse the top-left lighting.

References consulted: https://pixelart.dev/guides/consistent-pixel-art-game-assets and https://pixor.art/learn/outlines/; walk-cycle poses: https://www.youtube.com/watch?v=7T6yOk5n-zk.

## Work performed
Partial delivery only. Edited the two side rows (walk and run, frames 0–3): inset the contact outline ends and added a dark leather sole band; updated corresponding normal-map pixels using the source's opaque neutral background convention. No mirroring was used. Back walk frames 0–3 and front walk frames 0–3 remain exactly Claude's source: the requested bent knees, foreshortened soles, and front-view boots were not completed.

## Files
- `Docs/References/codex_k137_v1/hero_walk_run_grid.png` and `hero_walk_run_grid_normal.png`: game-sized partial sheets.
- `hero_walk_run_grid_native.png`, `hero_walk_run_grid_normal_native.png`: native-size equivalents.
- `preview_native_4x.png`, `before_after_2x.png`, `manifest.json`, `README.md` in the same folder.
- `_HTML/6_그림_화면/codex_K-137_주인공다리_그림체_v1.html`: self-contained Korean review with four animated comparisons.

## Verified
- 1152×1536 game sheets, 288×384 cells; 384×512 native sheets, 96×128 cells. The color game sheet exactly equals its native sheet scaled 3× nearest-neighbor.
- All 16 cells end at y=359; 100% of each bottom opaque row has all RGB channels below 55. Longest vertical alpha silhouette edge is 27 pixels.
- Color changes are confined to side rows y=354–359; the front/back rows are pixel-identical to the source. The two embedded review PNGs decode at 1152×1536; the page implements four frame-stepped cycles. The page was not opened in a browser.
- The normal map uses an opaque neutral background as in the source; alpha equality with the transparent color sheet is not expected. Unity was not opened.

## Leftovers
- Back walk 0–3 and front walk 0–3 need the requested original hand-drawn leg and boot poses. Side sole refinements remain subject to visual review. No independent connected-component/stray-pixel check was performed. No runtime validation was performed. Do not treat this card as complete or integrate it without review.
