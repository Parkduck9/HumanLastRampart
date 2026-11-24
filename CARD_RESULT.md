# K-078 result

Status: asset delivery complete. Game integration and user Play review remain with Claude/the user.

## Work completed

- Created five v6-style ladder goblin sheets: one idle frame and four frames each for ladder walk, carry run, deployment, and climbing (17 character frames total).
- Used the FIRST selected hero v6 `generated.png`, the K-066/K-069 unified ladder goblin identity, and the K-070 species-size reference. Preserved the enemy's olive skin, black hair, amber eyes with white highlights, scarf, leather outfit, wrist bandages, and boots.
- Applied D-081, which supersedes the stale card title's size-decision marker and old character cell-size wording: 96x128 native cells, 288x384 game cells, native foot (48,120), game foot (144,360), nearest-neighbor 3x.
- Kept filenames, frame counts, and motion-stage order. The idle/walk sheets include the small carried ladder; carry_run4/deploy4/climb4 contain the character only, matching the existing asset composition. Carry-run generation frames were reordered 2,1,4,3 to match the existing support-foot sequence.
- Restyled the separate front/side ladders. These retain the explicit prop exception: 256x1536 game canvas, bottom (128,1472), occupied vertical extent y=320..1471, 4.3m integration height. Native prop canvases are 64x384 and scale 4x. Wood and iron use separate limited palettes.
- Created a standalone Korean review HTML with embedded images, synchronized old/new animations, pause, single-step, speed control, full-sheet views, and baseline guides.
- Generated art with built-in image_gen; preserved raw inputs and prompts. The build script only separates, quantizes, cleans, aligns, and scales generated raster art.

## Files created

All asset files are under `Docs/References/codex_k078_v1/`:

- `ladder_goblin_static.png`, `ladder_goblin_walk.png`, `ladder_goblin_carry_run4.png`, `ladder_goblin_deploy4.png`, `ladder_goblin_climb4.png`, `ladder_front.png`, `ladder_side.png`.
- Matching `_native.png` and `_4x.png` files for all seven names (21 delivery PNGs in total).
- `source_static.png`, `source_carrier.png`, `source_carry.png`, `source_deploy.png`, `source_climb.png`, `source_climb_corrected.png`, `source_climb_left.png`, `source_ladder_front.png`, `source_ladder_side.png` (generation provenance).
- `manifest.json`, `README.md` (Korean), `prompts.json`, `sources.json`, `build.py`, `verify_review.cjs`.
- `validation.json`, `review_validation.json`, `scope_validation.json`, `game_hashes_before.json`.
- `review_desktop.png`, `review_mobile.png`, `review_climb.png`.

Review page: `_HTML/4_그림_캐릭터/codex_K-078_사다리고블린동작_v1.html`.

This root `CARD_RESULT.md` is the only file created outside those two permitted output locations. No existing tracked files were modified. The pre-existing untracked `CARD_CODEX_LOG.jsonl` was left untouched.

## Verification performed

- Build assertions passed for all character and ladder sheet dimensions, binary alpha, exact nearest-neighbor scaling, and foot baselines.
- All 17 character frames have a single 8-connected opaque silhouette, no detached pixel fragments, and the belt-center body anchor at native x=48 +/-0.5 pixels. Every four-frame motion contains four distinct images.
- Both ladder props have a single connected silhouette, correct canvas size, and bottom y=1472.
- Chrome headless loaded the HTML directly from disk: all 21 embedded image sources decoded; all four motions produced four distinct rendered frames; playback, pause, and single-step passed; zero script errors; no page overflow at 390px width.
- Visually inspected the generated source images, native 4x sheets, ladder output, and browser screenshots. Climbing is front/below as explicitly requested, while the old comparison sheet is rear-facing.
- Compared SHA-256 hashes of all 1,065 files under Game/ before and after: unchanged, no added/deleted files.
- No Unity launch, Play, scene edit, git write, commit, branch change, or live collaboration-log edit was performed.

## Integration notes / leftovers

- Claude must copy the seven unsuffixed delivery PNGs into the exact destinations in `manifest.json` and adapt character slicing/pivots to 288x384. Do not copy source/preview/native PNGs into the game as replacements.
- Character normalized Unity pivot is (0.5,0.0625). Ladder normalized pivot is (0.5,0.0416667). Manifest image-space pivots use a top-left origin.
- Claude/user must check actual 4.3m ladder scale, wall contact, deployment timing, and hand/boot contact with the separately composited ladder in Play. Those checks were not performed here.
- No unresolved size question: D-081 was used. Asset appearance remains subject to user review; no art approval or game integration is claimed.
