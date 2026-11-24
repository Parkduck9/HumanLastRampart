# K-079 result

## Completed

Created ogre idle (1), walk (4), heavy club attack (4), and fall (4) replacement assets in the approved v6 direction. Used the first hero v6 generated image and K-066 ogre identity; did not use corrected hero variants. Built-in image_gen produced three motion strips; sprite preparation normalized alpha, palette, pixel grid, anchors, and blue irises. Idle reuses the grounded second walk frame.

The card's existing-size condition applies: current ogre sheets use 256x384 cells and an already large figure. Preserved game cell size, names, frame counts, order, and pivot (128,352). Delivered an explicitly documented ogre exception: native 128x192 cells, exact 2x nearest-neighbor game sheets. Standing figure is approximately 120 native pixels / 240 game pixels. This is intentionally different from the ordinary 96x128 / 3x contract, and is recorded in the manifest and Korean review page.

## Files created

Only the following allowed locations were written:

- `Docs/References/codex_k079_v1/`: four game sheets (`ogre_static.png`, `ogre_walk.png`, `ogre_attack4.png`, `ogre_fall4.png`), matching `_native.png` and `_native_4x.png` files, three `_generated.png` source strips, `manifest.json`, `prompts.json`, Korean `README.md`, `validation.json`, `review_validation.json`, and desktop/narrow browser screenshots.
- `_HTML/4_그림_캐릭터/codex_K-079_오우거동작_v1.html`: self-contained Korean animated before/after review with pause, stepping, speed, guides, background controls, and expandable complete sheets.
- `CARD_RESULT.md`: this handoff.

No Game files, .meta files, collaboration docs/logs, or other repository files were edited. No Unity launch or Git writes were performed.

## Verification performed

- Native dimensions: idle 128x192, motions 512x192. Game dimensions: idle 256x384, motions 1024x384. Frame counts 1/4/4/4.
- All 13 native frames: binary alpha, transparent cell borders, one 8-connected silhouette per frame; no detached pixel islands.
- Exact pixel-for-pixel nearest-neighbor comparison: game sheets 2x native; previews 4x native, all passed.
- Ground contact at native row 175 / boundary y=176; fixed body anchor and pivot. Attack impact has boots on that line while the club extends to boundary y=185. Falling rotates around a fixed ground/pivot rather than artificially keeping raised boots at standing positions. Visually inspected all three enlarged motion strips.
- SHA-256 of the four existing Game sheets matches the hashes captured before output preparation.
- Opened the self-contained page through file:// in Chrome headless. All 12 preloaded images loaded; DOM reported ready and 10+ actual animation advances over 2.5 virtual seconds. Desktop and 500px viewport captures inspected; content width matched viewport width.

## Handoff / leftovers

- Claude must copy the four delivered game sheets to their matching existing Art/Characters locations and perform integration. Existing filenames, cell sizes, frame counts, and action ordering are preserved.
- Idle was brought up to the walking figure scale; review this visible increase from the smaller previous idle during integration.
- `ogre_static_normal.png` was outside the named replacement list and was not produced or changed. If used by the game, Claude should check whether a matching normal texture is needed.
- Unity import, Play, actual game animation timing/wall contact, and user capture remain unverified. Browser controls were source-reviewed; automatic loading and animation playback were exercised. No 390px browser viewport claim is made because headless Chrome enforced a minimum window width.
- No unresolved content question blocks this asset handoff. The size exception follows the card's conditional keep-existing-size instruction; no new global size rule was introduced.
