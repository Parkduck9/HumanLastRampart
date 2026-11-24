# K-108 — Hagen story portraits

Completed assets only. No Unity launch, Game/ edits, collaboration-log edits, Git writes, commits, or branch changes were performed.

## Deliverables

- `Docs/References/codex_k108_v1/portrait_hagen_{neutral,determined,alarmed,tired}.png`: four transparent 256×384 full-body story portraits, matching the existing story canvas and 4× pixel treatment. Grey temples/stubble, weathered face, scar, worn blue/steel field equipment and small lamp badge distinguish Hagen as an older veteran.
- Matching 4× previews; combined story-expression sheet; comparison sheet beside the unchanged existing wizard and archer, plus its 4× preview.
- Companion D-081 sheets: four 96×128 native cells, and exact nearest-neighbor 3× conversion into four 288×384 cells. These are supplementary; use the individual 256×384 portraits for the existing story system.
- Korean README, manifest with frame order/pivots/sizes, original generated sources, exact generation prompts, reproducible asset/review builders, validation records and browser screenshots, all inside the same reference directory.
- `_HTML/6_그림_화면/codex_K-108_부관하겐초상_v1.html`: Korean self-contained review with embedded assets, manual/automatic expression switching, background controls and side-by-side current portraits.

## Verified

- All four story PNGs are 256×384. Their visible top is y=56 and their last foot row is y=351; pivot is (128,352). Opaque bounds are recorded in `validation.json`.
- Pixel-by-pixel checks confirmed exact nearest-neighbor 3× native-to-game conversion and all portrait/comparison 4× previews. Foot rows across all four cells are native 119, game 359, story 351, corresponding to edge pivots 120/360/352. Evidence: `sheet_validation.json`.
- Final portrait alpha is binary; zero partially transparent pixels. Each portrait has exactly one 8-connected component, with no disconnected stray pixels. Transparent RGB is zero. Raw generated alpha is retained unmodified under `sources/`.
- Inspected the delivered expression sheet, comparison sheet and rendered desktop review screenshot visually.
- Headless Chrome opened the HTML from a file URL, decoded all eight embedded images, exercised manual/automatic switching and light-background selection, reported no script errors and no page overflow at 390px viewport width. Evidence: `review_validation.json`, `review_desktop.png`, `review_mobile.png`.
- Original repository HEAD was read directly and is `develop`; no branch switch was made.

## Integration / leftovers

- Claude should import the approved individual story portraits, create Unity metadata and connect expressions. No runtime/Play/capture verification was attempted.
- The card-specific exact 256×384 story requirement takes precedence over the common battle-sheet cell size. Both formats are supplied separately and clearly described in the manifest.
- All expressions align the feet and canvas center, but generated clothing/hand details vary slightly. Review their conversation transitions and visual direction with the user before final acceptance. No user approval is claimed.
- Actual visible native figure height is 74px (the common direction is approximately 73px).
- No blocking design questions remain; visual approval and integration remain with the user/Claude.
