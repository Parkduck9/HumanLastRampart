# K-077 — Goblin animation sheets

## Work completed

- Applied the already approved D-081 size decision, superseding the card title's stale size warning: approximately 56-pixel goblins in 96x128 native cells; exact nearest-neighbor 3x game cells of 288x384.
- Created a four-frame walk and four-frame club attack in the existing 4x2 layout, a four-frame fall, and a new four-frame planted-foot wall strike.
- Preserved replacement basenames, frame counts, row meanings, and semantic frame order. The brief's old-size instruction is superseded by its explicit D-081 cell-size contract.
- Revalidated the existing K-077 deliverables already present at session start. Corrected their yellow irises with built-in image_gen to satisfy the brief's blue-iris/white-highlight requirement, preserving the olive/brown goblin identity. Rebuilt all final sheets and the embedded review images from `blue_eyes_generated.png`.
- Retained the original generated masters as historical production inputs. `generation.json.source_file` now explicitly selects the blue-eye master; `prompts.json.blue_eye_correction` stores the exact final edit prompt. Earlier exploratory generations made during this session were rejected for style mismatch and are not referenced by any deliverable.
- Added a Korean, fully self-contained local review HTML with animated comparisons against the current in-game sheets, pause/step/reset controls, and a clearly labeled illustrative earth wall. The current walking row is shown as the wall-strike comparator because no dedicated old wall-strike sheet exists.

## Files changed

Only these authorized locations were written:

- `Docs/References/codex_k077_v1/`: three game PNG sheets, matching `_native.png` and `_4x.png` sheets, generation sources/configuration/prompts, `manifest.json`, Korean `README.md`, `build.py`, verification scripts/results, browser review captures, and the baseline Game hash inventory.
- `_HTML/4_그림_캐릭터/codex_K-077_고블린동작_v1.html`
- Root `CARD_RESULT.md`.

Game sheet dimensions: walk/attack 1152x768; fall and wall strike 1152x384 each. Native dimensions: 384x256; 384x128; 384x128 respectively. The pivot is (144,360) from the game cell's top-left; Unity bottom-left normalized pivot is (0.5,0.0625).

## Verification

- Verified all 16 native frames are within their cells, have binary alpha, and have exactly one 8-connected opaque component after fragment cleanup. No isolated opaque components remain.
- Verified blue iris pixels in all 15 open-eye frames; the final closed-eye fall frame is exempt. The final palette extends the original goblin palette with three blue tones.
- Every frame's last opaque row is 119, giving the prescribed bottom-exclusive ground line y=120. In fallen poses this is the ground contact line. Body axes are explicitly registered to x=48; intentional leaning and collapsing are retained.
- Reopened saved PNGs and checked full-array exact 3x nearest-neighbor game expansion and 4x previews.
- Verified all 1,065 baseline Game files retain their SHA-256 hashes.
- Opened the local HTML in headless Chrome: all 12 embedded images decoded, step changed the displayed frame, reset worked, no page errors, and no page overflow at mobile width 390. Inspected the review capture.
- Did not open/run Unity, execute Play, take actual game captures, modify Game, edit live collaboration logs, or perform Git writes.

## Handoff / leftovers

- Claude must import the delivered sheets, adapt cell slicing/pivots to D-081, replace the two existing sheets, and wire the new wall strike into earth-wall breaking. Existing normal maps do not match these new silhouettes; no normal maps were authored under this card.
- Review playback uses illustrative 180ms steps and is not a game timing decision. The final fall pose holds in the review.
- Actual in-game camera scale, visual acceptance, strike contact and event timing require user Play review after Claude integration. No additional size decision is required under D-081.
- Geometry checks establish the specified ground line, nominal registered body axes and cell containment. They do not prove anatomically identical foot positions or absence of all perceived animation jitter; visual acceptance remains part of the integration review.
