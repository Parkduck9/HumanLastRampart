# K-133 result

## Avoid generic AI-art defects
- Do not blur or airbrush the boot edges; retain the existing discrete pixels and dark outline.
- Do not mix pixel scales or introduce stray isolated pixels in the repaired silhouette.
- Do not use pillow shading or change the established top-left light direction.
- Do not add glossy gradients, glow, or decorative details to the existing palette.
- Do not force a clipped or mechanically symmetric stride; keep the neighboring poses readable.

References consulted before drawing: https://tips.clip-studio.com/en-us/articles/11408 and https://developer.mozilla.org/en-US/docs/Games/Techniques/Crisp_pixel_art_look

## Work and files
- Extended the two clipped trailing boots in run row 2, frames 1 and 2, and rebuilt the corresponding local normal-map contours.
- Produced `Docs/References/codex_k133_v1/hero_walk_run_grid.png`, `hero_walk_run_grid_normal.png`, `before_frames_1_2_4x.png`, `after_frames_1_2_4x.png`, `README.md`, and `manifest.json`.
- Produced `_HTML/6_그림_화면/codex_K-133_주인공달리기발_v1.html` with synchronized before/after animation and crops.

## Verification
- Both sheets remain 1152×1536, with 4×4 cells of 288×384. All 16 color frames end at y=359; no opaque color pixels touch a cell edge. The two original cuts were internal straight edges at x=245 and x=242, not the actual cell borders. No other apparent clipped edges were found on visual inspection.
- RGBA pixel differences by run frame 0–3: color 0, 585, 441, 0; normal 0, 1026, 792, 0. All other rows: zero differences. The normal map retains the original opaque background.
- Inspected the enlarged color crops for disconnected stray marks. All four image paths in the review page resolve to existing files; browser rendering was not tested.

## Open questions / leftovers
- Unity was not opened and in-game animation, lighting, and normal-map appearance were not verified. The coordinator must copy the two sheets into Game and the user should check them in Play mode. Git status could not be checked because git was unavailable in PATH; no Git writes were attempted.
