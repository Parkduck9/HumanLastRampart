# K-071 — Story portraits, v6 direction

## Work performed

Read both story assets, StoryArt, the portrait texture GUID targets, StoryCells and StoryPlayer without modifying Game. The complete named speaker inventory is officer (장교), archer (궁수), and wizard (마법사). Empty speakers are narration without portraits; gunner and cannon crew are not current story speakers.

Created ten transparent full-body dialogue portraits with the built-in image generation tool: officer and wizard each have neutral, determined, surprised and tired; archer has neutral and alarmed. Used the first generated v6 hero and approved K-065 character identities. The officer underwent an additional large-head/coarse-pixel proportion pass. Exact final prompts and source paths are preserved in generation_manifest.json. Python performs nearest-neighbor delivery resizing, binary alpha cleanup, sheet assembly and verification; it does not draw facial expressions.

## Files created

All asset deliverables are under `Docs/References/codex_k071_v1/`:

- `hero_{neutral,determined,surprised,tired}.png`, `wizard_{neutral,determined,surprised,tired}.png`, `archer_{neutral,alarmed}.png`: ten 256x384 portrait cells.
- Matching `*_64x96.png` native cells, `*_4x.png` 1024x1536 previews, and `*_generated.png` selected generation originals.
- `{hero,wizard,archer}_expressions.png` horizontal expression sheets and matching 4x previews.
- Same-name neutral compatibility copies: `wizard_front_chestnut.png`, `archer_static.png`, `hero_walk_run_grid.png`.
- `current_{hero,wizard,archer}.png`, `all_portraits.png`: comparison images.
- `README.md` (Korean), `manifest.json`, `generation_manifest.json`, `source_hashes_before.json`, `validation.json`, `build_review.py` (Korean comments).

The offline Korean review page is `_HTML/4_그림_캐릭터/codex_K-071_스토리초상6판_v1.html`. All images, CSS and JS are embedded. It compares the actual current story cell against each new portrait, automatically steps expressions every 1.2 seconds, supports manual selection/pause, and can display all expressions together.

This root `CARD_RESULT.md` is the English AI handoff. No collab logs, existing docs, shared HTML index, Game files or Unity metadata were edited. No Unity launch or Git write was performed. Read-only Git status also showed `CARD_CODEX_LOG.jsonl`; this task did not write or modify that file.

## Existing texture contract and integration

- Officer: `Art/Characters/Hero/hero_walk_run_grid.png`, RGBA PNG 1024x1536, 4 columns x 4 rows, story cell 12 (bottom row, first column).
- Archer: `Art/Characters/Static/archer_static.png`, RGBA PNG 256x384, story cell 0.
- Wizard: `Story/Resources/StoryArt/wizard_front_chestnut.png`, RGBA PNG 256x384, story cell 0. This is the only current PNG in StoryArt.
- Cell numbering is left-to-right, top-to-bottom. Delivery uses 256x384 cells, center-axis/pivot x=128 and foot boundary y=352. All final art is 4x nearest-neighbor from 64x96. Existing portrait RectTransform anchor/pivot settings are documented in manifest.json and README.
- Officer compatibility sheet preserves all 16 original cell slots and replaces ONLY cell 12. **It is a story-only copy. Do not overwrite the shared battle sheet.** Copy it to StoryArt and change only story texture references while retaining cell 12, or use `hero_neutral.png` with cell 0. Its remaining 15 cells intentionally remain the old battle artwork.
- Standalone new expressions always use cell 0. Expression sheet order is documented in manifest.json. Claude must connect expressions in story data and preserve existing GUIDs / create new .meta files when integrating into Game.
- No battle figure-size decision was required or inferred. Story portrait heights follow their current canvas occupancy: officer 76 native pixels, archer 75, wizard 74, with common foot boundary 88. Boot-region centering rounds to the nearest native pixel; measured centers are recorded separately from the fixed pivot.

## Verification

`build_review.py` verifies all ten delivery cells for exact dimensions, RGBA output, binary alpha 0/255, foot boundary 352, integer nearest-neighbor 4x scaling, and exactly one 8-connected visible component (no detached stray pixels). It assembles expression sheets and same-size compatibility copies, verifies that the officer's other 15 cells retain identical RGBA pixels, and compares SHA-256 against 117 read-only source files.

All 26 embedded review PNG occurrences decode successfully as 256x384 images. The page has no external dependencies. The final portrait overview was visually inspected for identity, expression readability and placement. See validation.json for machine-check results.

Final independent checks also passed: all 53 PNGs decode; all ten individual 4x previews exactly equal nearest-neighbor enlargement of their delivery cells; expression sheet dimensions and every frame's order/content match the individual cells; the ten saved generation originals match the selected tool output SHA-256 hashes. The output directory contains 59 files. Read-only Git status shows only this card's deliverable paths plus the untouched CARD_CODEX_LOG.jsonl.

Actual browser rendering and button interaction were **not verified**: the browser tool reported the in-app browser unavailable, and the available-browser inventory was empty. PNG decoding/static inspection is not reported as a successful browser run. Unity, in-game appearance, Play and captures were not performed.

## Open questions / leftovers

No blocking scope question. User visual acceptance, a browser check of the page, Claude's import/wiring and subsequent in-game review remain. Generated expression variants can have small local clothing/silhouette differences; this is expression artwork rather than a pixel-identical body-layer animation. Three shading steps per material and a one-native-pixel outline are visual targets, not an exhaustive per-pixel guarantee. Preserve the story-only/battle-shared sheet distinction when integrating the officer.
