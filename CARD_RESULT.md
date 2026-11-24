# K-101 result

Completed asset production for all seven current game sheets. Rebuilt the masks from the current source pixels rather than resizing the stale K-092 masks. Reused K-092 material values/classification as a starting point, then refined current palette handling, hero brass/epaulettes, wizard staff gems, ignite shoulder buttons, and nonreflective flame/magic regions. Skin, hair, eyes, cloth and wood retain low reflection.

## Files created

All asset files are under `Docs/References/codex_k101_v1/`:

- Game-resolution masks: `hero_back_idle_spec.png`, `hero_command_spec.png`, `hero_walk_run_grid_spec.png`, `wizard_back_static_spec.png`, `wizard_static_spec.png`, `wizard_cast_spec.png`, `cannon_crew_ignite6_spec.png`.
- `native/`: seven corresponding lossless native masks, with the same basenames.
- `previews/`: seven native masks enlarged 4x nearest-neighbor, named `<sheet>_spec_4x.png`.
- `materials/`: seven diagnostic material-color sheets, named `<sheet>_materials.png`.
- `material_contact.png`: visually inspected source/material comparison contact sheet.
- `manifest.json`: source/mask hashes, dimensions, grids, frame order, per-frame alpha bounds, pivots and per-file verification results.
- `README.md`: Korean usage, format, verification and integration notes.
- `build_masks.py`, `review_template.html`: reproducible generation tools.
- `verify.py`, `verify_review.cjs`, `validation.json`: independent pixel/embedded-image checks and JavaScript test harness/results.

Additional deliverables:

- `_HTML/6_그림_화면/codex_K-101_반사가면재제작_v1.html`: self-contained Korean review page with 10 synchronized motion cards, original/mask/material comparisons, play/pause, frame stepping, speed, R/G display and pivot guides.
- `CARD_RESULT.md`: this handoff.

No existing tracked files, Game assets, collaboration logs, or other documentation were edited. No Unity invocation or Git writes were performed. The untracked `CARD_CODEX_LOG.jsonl` visible in Git status was not written or edited by this task's commands.

## Important sheet contract

Hero/wizard cells remain 288x384, with pivot (144,360), matching 96x128 native cells and pivot (48,120).

The current ignite sheet is **2592x384 = six 432x384 cells**, not nine 288x384 cells. The K-086 v5 manifest specifies native cells 144x128, native pivot (96,120), game pivot (288,360); this existing contract is preserved. All six frames and their order remain unchanged.

RGBA8 masks contain linear R=specular strength, G=smoothness, B=0, A=exact source alpha. Standard material values match K-092; staff gems use R/G=255/230. Flame and surrounding magic effects use 0/0. Transparent RGB is zero.

## Verification performed

- All seven masks match their current source dimensions exactly; all 32 frames checked.
- Exact byte-for-byte alpha equality, B=0 and zero transparent RGB verified independently after PNG reload.
- No new alpha pixels; source alpha bounds and cutout foot lines preserved per frame. All cutout bottom boundaries remain y=360. Existing faint ignite alpha artifacts were deliberately retained.
- All sources are exact 3x nearest-neighbor grids; native/game round trips and complete 4x preview pixel equality passed.
- Current source SHA-256 hashes remain unchanged.
- All 21 embedded review PNGs decoded successfully, match their delivery/source files byte-for-byte, and have the expected dimensions.
- JavaScript syntax passed Node `--check`. A non-browser DOM/canvas harness initialized all 21 images and 10 motion cards and exercised animation, pause, frame stepping, channels and pivot-guide toggling. This harness does not verify browser rendering.
- Final material contact image inspected directly.
- Final read-only Git status showed no tracked modifications; new task files are confined to the stated output paths and this result file.

Reproduction from repository root: run `python Docs/References/codex_k101_v1/build_masks.py`, then `python Docs/References/codex_k101_v1/verify.py`. Requires Pillow, NumPy and Node.js; generation reads existing K-092 tooling and the K-086 v5 manifest.

## Open questions / leftovers

- No asset-contract questions remain.
- Actual browser visual verification is outstanding: no browser was connected, and creating an in-app browser tab returned `Browser is not available: iab`. Embedded image decoding and script tests passed, but no browser screenshot/rendering check is claimed.
- Claude still needs to copy/connect the masks under Game and preserve the existing six-frame 432px ignite grid. Import as linear data (`sRGB=false`), Point, no mipmaps, no compression, Clamp, Default texture type. No Unity `.meta` files were created for these source artifacts outside Assets; manage `.meta` files when importing into Game.
- Unity Play and final in-game lighting comparison remain for Claude/user integration and user verification. No Unity execution was attempted.
