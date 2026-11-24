# K-087 result

Completed the terrain connection asset package in `Docs/References/codex_k087_v1/` and the self-contained Korean review page `_HTML/5_그림_전장/codex_K-087_토벽언덕이음_v1.html`.

- Created straight, diagonal, left cap, right cap and joint pieces for intact, cracked, collapsed and rubble states: 20 native parts plus exact nearest-neighbour 3x game versions.
- Added native/game part atlases and single-frame aliases retaining the four existing earthwall filenames. The existing Game assets were not replaced.
- Created 32x32 hub and eight directional arms for each of earth wall and hill, plus nine-frame atlases.
- Added five composed battle/map examples, four damage-state previews and 4x part-atlas previews. Each layout records three valid two-cell placement pairs.
- Added manifest, Korean README, original image_gen sources and exact prompts, assembly/validation scripts, validation reports and browser captures. There are 76 primary PNG assets listed in the manifest.

Battle scale is 40 px/m native and 120 px/m game; intact/cracked maximum height is 112 px / 2.8 m. Baseline is the bottom pixel edge, with bottom-centre pivots. Terrain uses its own dimensions rather than the character-cell specification. The 283-pixel diagonal rounds 5*sqrt(2) metres within half a native pixel. Atlas part rectangles and half-pixel centre offsets are recorded.

Verification:

- `verification.json`: 470/470 checks passed, including dimensions, measured visible heights, RGBA/binary alpha, zero RGB in transparent pixels, connected silhouettes without isolated pixels, bottom contact, equal periodic edge pixels, exact 3x scaling, paired directional connections, closed diagonal corners, all 256 neighbour masks for each terrain type, and two-cell placement pairs.
- `review_validation.json`: file:// Chrome check passed; 23 embedded images loaded, damage state changes and pause worked, all/none neighbour controls and hill selection worked, no page/console errors and no horizontal overflow at 390 px. Desktop and mobile screenshots saved and desktop top inspected visually.
- `scope_validation.json`: all 1,202 indexed Game files match their Git-index blob hashes (text line endings normalized where needed). Original checkout remains develop; this worktree remains card/K-087. No Git writes or Unity execution.

Leftovers / integration notes:

- Claude must wire the two-cell placement rule, reciprocal neighbour masks, XZ segment orientation, joint layering and diagonal movement blocking into the game. Use nominal 5*sqrt(2) world distance for diagonal endpoints rather than deriving it from rounded pixels.
- Draw map hubs first, then arms; use opposite arms in adjacent cells. Battle joints cover connected endpoints; preserve the upright height axis.
- Hill battle examples project the new map parts onto the ground. A new upright battle hill asset was not requested or delivered.
- Actual Unity camera appearance, world sizing, sorting and movement blocking remain untested. User art review and user-run Play/captures remain outstanding. No blocking questions.

Only the named output folder, named review HTML and this CARD_RESULT.md were written. Collaboration logs and other documentation were not edited.
