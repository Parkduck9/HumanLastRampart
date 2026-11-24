# K-083 Result

Created a two-line brass/stone pixel-art title logo, a single-line wide logo, a dusk rampart main-menu key-art background, and a 16:9 cover variant with the logo composited. Used the built-in image_gen tool; exact prompts are saved in prompts.json. The approved hero reference was generated.png, not any corrected/preview version.

## Files changed

Only new card outputs were written:

- Docs/References/codex_k083_v1/: title_logo.png, title_logo_wide.png, main_menu_keyart.png, cover.png, main_menu_keyart_960.png, cover_960.png, hero_detail_4x.png, logo_detail_4x.png; manifest.json, prompts.json, README.md; export.ps1, build_review.py, verify_review.cjs; review_desktop.png, review_mobile.png, review_validation.json.
- _HTML/6_그림_화면/codex_K-083_타이틀_v1.html: Korean, self-contained review page with current-game background comparison, menu placement overlay, 960px display toggle, transparent-logo checks and 4x detail previews.
- CARD_RESULT.md.

No Game/, collaboration logs, existing documentation, project settings or packages were modified. Unity was not opened or run. No Git writes were performed.

## Verified

- Background and cover: exactly 1920x1080; reduced previews: 960x540. Generated sources were 1672x941, exported using nearest-neighbor resampling with approximately 0.05% aspect correction; these are not native 1920 renders.
- Both logo PNGs have genuine alpha, ranging from 0 to 255, and fully transparent outer canvas edges. Dimensions and alpha bounds are recorded in manifest.json.
- Visually reviewed the art and desktop review capture: readable HUMAN / LAST RAMPART lettering, distinct blue-eyed white-haired officer, goblin army, ogre, torches, and calm left menu area. The wide logo reads HUMAN LAST RAMPART.
- Headless Chrome: all 10 embedded images decoded, no page errors, menu toggle worked, 960px view worked, no horizontal overflow at 390px viewport. Results saved in review_validation.json.
- Static artwork: sprite-sheet frame order, foot-line/pivot and motion animation checks are not applicable. No character sheet is being replaced.

## Open questions / leftovers

- No explicit game display-title decision was found in DECISIONS.md. Used the brief-authorized placeholder Human Last Rampart. MainMenuEntry.cs currently contains a Korean title, but code was not treated as a title approval. Logos are separate from the text-free background for replacement; no editable font/glyph master is included.
- Latest figure description found was the D-080 correction mentioning approximately 52 native pixels. No subsequent 52-vs-73 decision was found. K-083 is not marked NEEDS SIZE DECISION and produces portfolio artwork rather than a combat sheet.
- Faint alpha 1-16 generation residue exists outside the visible logo glyph bounds. No visible bright stray pixels were observed, but strict zero-stray-pixel compliance is NOT claimed. Original generated alpha was preserved; a threshold cleanup or regeneration remains if strict delivery requires absolutely zero residue.
- Cover is an image_gen compositing variant, not a deterministic pixel-identical background-plus-logo merge.
- Actual Unity rendering, import settings and menu overlap still require Claude integration and user Play validation. The review menu is a schematic based on MainMenuEntry.cs and ScreenView.DockLeft (x=128, width=624, height up to 512 at 1920x1080), not an in-game capture.
- export.ps1 references session-local generation source paths. Delivered PNGs and the embedded review page are standalone.
