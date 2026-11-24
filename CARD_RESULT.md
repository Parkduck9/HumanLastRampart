# K-075 — Cannon crew motion sheets

## Delivery

Created six replacement sprite sheets, totaling 27 frames, using the user-selected FIRST hero v6 generation as the style reference and K-065 cannon crew identity/colors. D-081 supersedes the brief's stale NEEDS SIZE DECISION heading: native cells are 96×128, game cells are exact 3× nearest-neighbor enlargements at 288×384. Native pivot is (48,120); game pivot is (144,360), measured from the top left. Unity bottom-origin normalized pivot is (0.5,0.0625).

Preserved replacement filenames, frame counts and motion sequence. Idle and fall retain the existing rear view; run, ignition, push and pull use the corresponding side/three-quarter action views. Revised the initial repeated run poses, shortened an overly high ignition stick, and replaced the initial front-view fall candidate with the existing rear-view fall sequence.

## Files changed

All deliverables are NEW files within these authorized locations only:

- `Docs/References/codex_k075_v1/` — 40 files:
  - `native/Characters/Static/cannon_crew_static.png` (1 frame)
  - `native/Characters/Run/cannon_crew_run.png` (4 frames)
  - `native/Characters/Fire/cannon_crew_ignite6.png` (6 frames)
  - `native/Characters/Fall/cannon_crew_fall.png` (4 frames)
  - `native/Battle/Resources/BattleArt/Cannon/crew_push6.png` (6 frames)
  - `native/Battle/Resources/BattleArt/Cannon/crew_pull6.png` (6 frames)
  - Corresponding six paths under `game/`, using 288×384 cells.
  - `previews/{static,run,ignite,fall,push,pull}_4x.png` — six exact 4× native previews.
  - `source/` — nine built-in image_gen outputs, including three rejected candidates; selection provenance in `generation.json`.
  - `overview_2x.png`, `ignite_contact_review.png` — inspected static compositions, not browser screenshots.
  - `manifest.json`, Korean `README.md`, `validation.json`, `review_validation.json`.
  - Reproduction/verification: `PixelTools.cs`, `build.ps1`, `verify.ps1`, `build_review.ps1`, `review_template.html`, `verify_review.cjs`.
- `_HTML/4_그림_캐릭터/codex_K-075_대포병동작_v1.html` — Korean offline review, 19 embedded PNGs, animated side-by-side comparisons for every motion, frame stepping, playback speed/background/guides, ignition composite and fixed-camera hauling contact inspection.
- `CARD_RESULT.md` — this English AI-to-AI handoff.

No Game files, existing reference files, existing HTML/list files, collaboration logs, or Git metadata were written. Unity was not opened or run. No Git writes were performed. Git was unavailable on the shell PATH; repository status was not verified through Git. No .meta files were created outside Unity's asset tree.

## Verified

- **154 PNG checks passed** using decoded pixels: six sheet dimensions, exact native-to-game 3× and preview 4× nearest-neighbor equality, binary alpha, all 27 frame ground boundaries at native y=120, transparent cell-edge margins, no isolated components smaller than three pixels, distinct frames, fixed ignition lower-body pixels, hauling contact coordinates, ignition contact height, and unchanged SHA-256 hashes for the six existing Game sheets plus the current cannon sprite. Results: `validation.json`.
- **38 review checks passed** by executing the actual HTML JavaScript in a Node VM with DOM/Canvas test doubles: 19 embedded PNG headers, byte equality to the six current/native/game sheets, six comparison cards, frame source bounds, play/pause, previous/next/wrap/reset, timed stepping, independent 4-/6-frame cycles, speed/background/guides, and contact coordinates. 192 canvas draw calls were checked. Results: `review_validation.json`.
- Opened and visually inspected all six final enlarged sheets and the ignition/cannon contact composite.
- No actual browser verification: browser inventory was empty and opening the local review returned `Browser is not available: iab`. Real CSS layout, browser image decoding, mouse interaction and live-animation visual QA remain unverified. Node VM checks are not browser checks.
- No Unity compilation, Play, gameplay captures, engine import checks, or world-space attachment/continuous-motion tests were performed.

## Integration coordinates

- All game sheets have 384px height; widths are 288 / 1152 / 1728 / 1152 / 1728 / 1728 for idle/run/ignite/fall/push/pull respectively. Import/slicing must read the new 288px cell width. Preserve existing Game asset GUIDs when Claude applies the images.
- Current `haul_cannon_body.png` has fuse point (269,132) and pivot (336,352). Draw that unscaled cannon at (0,8) to align its baseline with the new y=360 baseline. Draw the ignition cell at **(173,0)**. Frame index 3 contains an actual flame pixel covering native (32,46⅔), corresponding to game (96,140); after the crew offset it overlaps the cannon fuse at **(269,140)**. These are sprite-pixel coordinates, not world-space attachment values. See `manifest.json` and the static composite.
- Hauling moves LEFT by **36 game pixels per frame**, equivalent to 32px at the former 256px scale. Planted-boot contact centers are native **36,48,60 / 36,48,60**. Measured world contact x values after subtracting frame translation are **108,108,108 / 0,0,0**. The review advances translation at frame boundaries and resets position every cycle. It does not demonstrate continuous interpolation or cannon-hand attachment.

## Open questions / leftovers

No size decision is blocking delivery. User visual approval and Claude's engine integration remain: eye/style readability, run and hauling cycle transitions, anatomical leg readability, trouser adjustment naturalness, hand-to-cannon grip, and crew overlap with the breech/wheel during ignition. The fuse-pixel overlap is verified; overall scene composition is still for review.

Existing normal maps were left untouched and must not be reused with incompatible dimensions. New normal maps are outside this card. Claude must adapt slicing/pivots and any related attachments, then validate real continuous hauling contact, cannon placement, and ignition timing in the game. Retain the existing 0.50s contact / 1.50s firing relationship where applicable; no timing code was changed here.

Rebuild from the repository root with this folder's `build.ps1`, then `verify.ps1`, then `build_review.ps1`. Run `verify_review.cjs` with Node afterward. Rebuilding uses the saved generated sources and does not call image_gen again. Korean README includes the prompt-set summary and candidate choices.
