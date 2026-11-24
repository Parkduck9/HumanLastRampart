# K-135 — Do not list

- Do not mix pixel sizes or blur the 3-pixel grid.
- Do not use pillow shading or smooth gradients on the boots.
- Do not flatten toe and heel silhouettes into cut edges.
- Do not add noisy pixels or change the established palette and top-left light.
- Do not hide the lifted leg in an unreadable stub.

References: https://derekyu.com/makegames/pixelart2.html and https://pixor.art/learn/outlines/

## Status: incomplete draft — do not integrate

Created draft sheets, 4x bottom crops, a Korean review page, a manifest and a Korean per-cell comparison table. Side-walk and side-run bottom rows were darkened; a provisional raised boot was drawn on front/back frames. The front/back leg shapes still do not convincingly depict the requested bent legs, and the side normal map was not updated. The bottom band needs visual refinement and the existing long-cut-edge constraint has not been verified. Native sheets have not been produced.

Files: `Docs/References/codex_k135_v1/` and `_HTML/6_그림_화면/codex_K-135_주인공밑창_v1.html`. Game files were read only. The normal map is a provisional copy with edits to the front/back boots; it needs proper matching geometry and review.

Verified the two draft sheets are 1152 × 1536, with 16 cells of 288 × 384; the last opaque art row is y=359 in each cell. No color changes occur above y=329. The measured dark share of the side bottom rows is 100% under the RGB <55 threshold; the README records all cell counts and before/after percentages. Both image paths in the review page exist. The page has not been opened in a browser, and stray pixels, motion quality, and the normal map have not passed review. The original normal map's alpha convention differs from the color sheet, so matching alpha masks was not established.

Leftovers: redraw both front/back legs with plausible bent, foreshortened lifted feet; refine the side soles and their normal map; verify grid, alpha convention, stray pixels, vertical edges, and animated cycles; deliver native sheets and perform browser review. Unity was not opened or run.
