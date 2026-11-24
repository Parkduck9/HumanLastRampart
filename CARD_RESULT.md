# K-070 result

Completed the figure-size decision aid. Recommendation: approximately 73 px for humans, using 96x128 cells, because it better preserves the selected hero's native estimated pixel density and eye construction at the brief's 70-90 screen-pixel battle size. This is a recommendation, not an approved game/import specification.

## Work performed

- Read the existing K-065/K-066 documentation, sprites, generation methods, and the selected hero's original generated.png. Used the original, not corrected.png, as the style reference.
- Used the built-in image_gen tool for four independent generated originals. No CLI/API image generation was used. Full prompts, reference roles, and original output locations are in prompts.json.
- Delivered archer back-idle, archer front-idle, and goblin front-idle, each with an exact 73 px alpha-bounds figure height in a 96x128 cell, body-axis anchor x48, and foot-bottom boundary y120 (last opaque row119).
- Also supplied a separately generated 56 px goblin to retain the existing species ratio: K-066's goblin was actually40 px against a52 px human. The strict73 px goblin is the main comparison deliverable; the56 px variant is used in the battle-scale mock. Both roles are explicitly labeled in Korean.
- Converted generated originals using binary alpha at128, aspect-preserving nearest-neighbor resizing, anchor placement, and palette mapping to the corresponding K-065/K-066 final sprite colors. No programmatic part-by-part anatomy edits were made.
- Produced three comparison boards with all three subjects at the same73 px on-screen figure height: existing52 px direction (actual goblin40 px), new73 px sprite, and the selected hero sampled at its estimated native grid. Supplied1x boards and exact nearest-neighbor4x boards.
- Downsampled the whole1024x1536 hero original to64x96 using nearest neighbor, thresholded alpha, and moved it to the shared cell/baseline without redrawing. Its resulting figure height is73 px.
- Used the prescribed real1920x1080 fallback screenshot; no usable Game/playprobe/portfolio capture was present. Made identical800x400 crops at(560,200)-(1360,600), and pasted old/new versions at matched positions and screen heights. Back archers show70/80/90 px; front archer80 px; goblin62 px; hero80 px. All mock resampling is nearest neighbor, with noninteger repetition explicitly disclosed.
- Wrote the Korean self-contained review HTML, Korean README, memory/frame/cell cost comparison, and this English handoff.

## Changed files

Only these authorized locations were written:

1. `_HTML/4_그림_캐릭터/codex_K-070_인물크기비교_v1.html`
2. `CARD_RESULT.md`
3. The43 new files under `Docs/References/codex_k070_v1/` listed below.

```text
archer_back_96x128.png
archer_back_96x128_4x.png
archer_back_generated.png
archer_back_old_cell.png
archer_back_old_cell_4x.png
archer_front_96x128.png
archer_front_96x128_4x.png
archer_front_generated.png
archer_front_old_cell.png
archer_front_old_cell_4x.png
board_archer_back.png
board_archer_back_4x.png
board_archer_front.png
board_archer_front_4x.png
board_goblin_front.png
board_goblin_front_4x.png
build.py
generation.json
goblin_front_96x128.png
goblin_front_96x128_4x.png
goblin_front_generated.png
goblin_front_old_cell.png
goblin_front_old_cell_4x.png
goblin_species56_96x128.png
goblin_species56_96x128_4x.png
goblin_species56_generated.png
hero_native_96x128.png
hero_native_96x128_4x.png
mock_native.png
mock_old.png
native_lineup.png
native_lineup_4x.png
prompts.json
README.md
review_desktop_1x.png
review_desktop_4x.png
review_mobile.png
review_mock.png
review_validation.json
screenshot_crop_before.png
source_hashes_before.json
validation.json
verify_review.cjs
```

## Verification actually performed

- Ran build.py successfully. Validated the three73 px sprites' cell sizes, figure heights, foot boundaries, binary alpha, exact4x pixel replication, and palette membership. Validated the56 px supplementary cell, alpha, height, baseline, and4x replication.
- Checked that every item in each equal-height comparison has73 px of opaque figure height and a common foot boundary110. Board outputs are480x128 and1920x512.
- Decoded all10 PNG images embedded in the HTML; checked that no external HTTP resources are required.
- Ran verify_review.cjs successfully in actual headless Chrome using the local file URL. All images decoded; the three boards displayed at480 CSS px for1x and1920 CSS px for4x; mocks retained800 CSS px/native width; no script errors; no page-level horizontal overflow at390 px viewport width. Horizontal scrolling remains intentional inside large art regions. Browser evidence is in review_validation.json and review_*.png.
- Visually inspected generated originals, the final three comparison boards, native lineup, both old/new mock comparison in the browser capture, desktop views, and mobile view.
- Recorded and rechecked SHA-256 hashes for1,231 preexisting files across Game/, K-065/K-066 references, the selected-hero folder, and K-068 input. All remained unchanged.
- Read the original repository HEAD as develop and this dedicated worktree HEAD as card/K-070. No branch switches or Git writes were performed. Unity was not opened or run. No collaboration logs, existing HTML indexes, other documents, or Game files were edited.

## Cost / limitations / open decisions

- 64x96 ->96x128 doubles cell area and uncompressed RGBA32 memory:24 ->48 KiB per frame. Six horizontal frames:384x96 /144 KiB ->576x128 /288 KiB. A6x4 example:384x384 /576 KiB ->576x512 /1,152 KiB. Frame counts and timing need not increase. Compression, padding, mipmaps, and normal maps are separate costs.
- This is not an animation sheet. Independently generated front/back archer details need consolidation before animation. Existing52 px animations would need redraw/cleanup, not only resizing.
- Head estimates are about25 px for both archers,29 px for the comparison goblin,22 px for the supplementary goblin, versus about24 px for the hero. The goblin retains its larger species head ratio. Exact local outline weights, perfectly uniform source grids, and exact three-tone shading everywhere are not claimed.
- Mocks are controlled screen-size readability samples placed in open ground immediately before the wall on an unchanged screenshot. They are not world-coordinate/perspective registrations or new gameplay captures. The70-90 px range and0.35 m width come from the brief; no Unity camera measurement or runtime verification was performed. All selected screen-size scales are fractional; no claim of integer-perfect gameplay sampling is made.
- User decision still needed:52 px or approximately73 px human production height? If73 px is selected, should the goblin retain its approximately77% species height (recommended56 px) rather than using the equal-height73 px comparison variant in game?
- Existing game contracts reference256x384 cells. Mapping/packing the proposed96x128 production art, pivots, weapon margins, normal maps, animation sheets, import settings, and final in-game camera validation remain future work after the user's choice. This card changes none of those contracts/settings.
- Claude retains commit/integration responsibility. No Git writes were requested or performed here.
