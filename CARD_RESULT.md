# K-074 result

Implemented the gunner motion asset package using the built-in image generation tool and the approved first v6 hero image for style, K-065 gunner identity references, and K-070 human proportions. Applied the brief's D-081 decision; no additional size decision is required.

## Files created

- `Docs/References/codex_k074_v1/`: four native PNG sheets, four game PNG sheets, four 4x previews, four generated source images, a contact sheet, `manifest.json`, Korean `README.md`, prompts, build/review verification scripts, validation JSON, input hashes, and desktop/mobile review screenshots.
- `_HTML/4_그림_캐릭터/codex_K-074_총병동작_v1.html`: self-contained Korean animated comparison with current game sheets.
- `CARD_RESULT.md`: this handoff.

Retained replacement basenames `gunner_static.png`, `gunner_run.png`, `gunner_fire.png`, `gunner_fall.png` and frame counts 1/4/6/4. Fire order: shoulder, aim, muzzle flash, recoil, lower, reload hint. Native cells are 96x128; game cells are exact nearest-neighbor 3x, 288x384. Foot anchor from top-left is (48,120) native / (144,360) game; Unity normalized bottom-left pivot is (0.5,0.0625).

## Verified

- All 15 frames: RGBA, binary alpha, last opaque row 119, no cell-edge clipping, no isolated 1-2 pixel components. Run/static figure heights normalized to 73 pixels; collapse height decreases naturally.
- All four game sheets match exact nearest-neighbor 3x scaling; all four old input sheets are 256x384 per frame.
- Chrome: 12 embedded images decoded, eight comparison canvases loaded, playback/pause/frame stepping/flip/speed controls work, zero script errors, no horizontal overflow at 390px. See review validation JSON and screenshots.
- Visually inspected generated motion references, final contact sheet, and browser screenshots.
- SHA-256 comparison confirms all 1,020 files under `Game/Assets/_Project` unchanged across asset production.

## Open questions / leftovers

User visual approval and Claude integration remain. Claude should replace the corresponding sheets under Game only after review and adapt cell size, pivot, and playback settings. No normal maps were requested or produced. Generated art normalization does not guarantee every local outline is exactly one pixel wide.

Unity was never opened or run. No Game edits, collaboration log edits, or Git writes were performed. A pre-existing/unrelated untracked `CARD_CODEX_LOG.jsonl` was observed and left untouched. Git commit is left to Claude.
