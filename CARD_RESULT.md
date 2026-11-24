# K-097 — Tutorial completion kit

## Completed

Assets only. Reviewed W2 in `Docs/GDD/Parallel_Wave_20261022.md`, K-093 assets/manifest/build definitions and six mockups, the approved original v6 hero, and current in-game Hero art. No Game files, collaboration documents/logs, existing reference assets, or existing review pages were edited. Unity was not opened or run. No Git writes were performed.

Delivered 13 asset groups / 79 frames as 39 PNG sheets: native, exact nearest-neighbor 3x game sheets, and 4x previews. An additional generated officer source PNG is production provenance, not an import-ready sheet.

- Clean instruction panel and new hint panel, with Unity 9-slice borders: native L/B/R/T = 8/8/8/8; game = 24/24/24/24.
- Four officer bust expressions: neutral, explaining with raised hand, urgent, pleased. Same 48x48 native cell and centered pivot (24,24) as K-093; shared scale, head alignment, opaque bottom ending at y=46 exclusive. Approved original v6 hero used for identity. Built-in imagegen produced the source; deterministic postprocessing fitted the delivery sheet.
- Six-frame highlight pulse reused unchanged from K-093. Four-frame bounce sheets for all four pointer directions.
- Eight 24px step icons in W2 order, a 24-frame step/state pip atlas, and nine complete eight-step progress strips (eight current-step states plus all done).
- Five input chips at both 24px and 32px: Shift+drag, right click, wheel, E, Esc. Right click/wheel/E extracted unchanged from K-093; Shift+drag and Esc added.
- Self-contained Korean review HTML with animated old/new comparisons, current in-game hero motion, four expression portraits, width-adjustable 9-slice examples, all pointer motions, progress selection, and input-chip comparisons.

## Files changed

Only new files in these authorized locations:

- `Docs/References/codex_k097_v1/`: 39 delivery PNGs, `officer_expressions_generated.png`, Korean `README.md`, `manifest.json`, `generation_prompt.json`, `validation.json`, `review_verification.json`, `build.ps1`, `finalize.ps1`, `verify_review.cjs`.
- `_HTML/6_그림_화면/codex_K-097_튜토리얼보충_v1.html`.
- Root `CARD_RESULT.md` (this handoff).

## Verified

- All 13 groups / 79 frames have expected sheet and cell sizes.
- All delivery PNGs have binary 0/255 alpha. All game/preview pixels exactly match nearest-neighbor 3x/4x native pixels.
- Per-frame alpha bounds and 8-connected component counts are in the manifest. The smallest component across delivery frames is 65 pixels; no small stray components. Bust frames stay inside their cells and end at the same y=46 boundary. Portraits and UI icons/pointers were visually inspected as 4x images.
- Both 9-slice interiors are uniform; border metadata and source dimensions agree.
- Node VM execution of the actual embedded HTML script, using DOM/canvas mocks, decoded all 19 embedded PNG IDAT payloads, checked every draw source rectangle, autoplay, pause, single-frame advance holding while paused, all nine progress selections, and panel widths 180/900. No external resources are required.
- Commands completed successfully: Windows PowerShell `build.ps1`, `finalize.ps1`; Node `verify_review.cjs`. Evidence: `validation.json`, `review_verification.json`.

## Open questions / leftovers

- **Real browser verification remains pending.** Headless Chrome did not expose its debugging endpoint. Computer-use inventory contained no browsers, and IAB creation returned unavailable. Node VM checks do not prove actual HTML layout, browser rendering/timing, or mobile layout. No browser screenshot is claimed or delivered.
- Unity import, Sprite Editor slicing/borders/pivots, runtime placement, Korean text layout, and tutorial completion logic remain Claude's integration work. No Unity verification was performed.
- Current in-game art contains no tutorial-specific sheets. The review labels K-093 as previous production material and compares the actual game hero sheet separately; no claim is made that K-093 is already integrated.
- These are UI assets and busts, so D-081 full-body 96x128 cells and foot baseline y=120 are not applicable. No existing game sheet was replaced or renamed.
- User visual approval of the expressions and step icons is still pending. No design/API/scene/package changes were made.
