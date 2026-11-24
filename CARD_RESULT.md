# K-089 result

Status: asset delivery complete; engine integration and user Play review pending.

Created a warm castle preparation-yard panorama with the five requested stations in order, a binary walkable mask, four aligned foreground occluder layers, and a one-frame five-station hero/shadow mockup. The ground proposal has 8 metres of depth and a consistent perspective mapping: the existing 73px native hero appears 109.5px tall at the farthest row and 219px at the nearest row.

Files created:

- `Docs/References/codex_k089_v1/`: 3840x1080 background, mask, four RGBA occluders, mockup; 1280x360 native assets; native 4x previews; manifest, Korean README, generation source/prompts, build and verification scripts, validation reports and browser screenshots.
- `_HTML/6_그림_화면/codex_K-089_정비마당배경_v1.html`: self-contained Korean review with panorama panning, depth animation, placement, mask/occluder/occlusion toggles, station triggers and current-background comparison.
- This `CARD_RESULT.md`.

Generation used the built-in image_gen tool for an initial image and one composition revision. Both returned 2048x768. The selected second image was cropped to `(0,160,2048,736)` without aspect distortion, normalized to 1280x360/192 colors, and enlarged exactly 3x nearest-neighbor. Complete generation instructions and processing details are in the delivery folder. The existing K-070 hero was reused without redesign.

Verified:

- Exact dimensions and 3x nearest-neighbor correspondence; opaque background; binary connected mask; binary occluder alpha; no tiny isolated alpha components; every opaque occluder pixel matches the background at the same coordinate.
- Native hero foot boundary at y=120; all five mockup feet exactly meet their assigned rows and stand on white ground; all station trigger rectangles and approach lanes are walkable; previews equal native 4x nearest-neighbor.
- SHA-256 equality for the four existing reference/code files consumed by the pipeline.
- Actual local-file browser load with nine embedded images, no external HTTP requests or JS errors; perspective endpoints, animation, panning, 100% zoom, all station buttons, mask/highlight/trigger toggles, occlusion pixel changes, blocked-click rejection, valid placement, and mobile layout. Screenshots were saved and visually inspected.

Open questions / leftovers:

- No blocking user questions. The final 6–10m depth instruction takes precedence over the earlier 2–4m example; this delivery uses 8m.
- The 1.7m hero world height, camera calibration, interaction boxes and collider footprints are integration proposals, not newly approved project-wide contracts. The generated environment was not physically reconstructed; the ground/hero mapping is internally consistent.
- Four near-ground occluders are supplied. Distant merchant roof posts remain baked into the background because they are beyond the current walkable region.
- Background props remain baked in; overlays restore them above the hero only when the hero is behind. Do not remove the baked copies or draw every overlay unconditionally above every character.
- Claude must wire the ground, camera/pan, collisions, depth scaling, shadows, occlusion sorting and interactions into Unity, then obtain user Play/capture review. No Unity run or engine integration verification was performed.
- No Game/, collaboration docs/logs, existing reference assets, or Git state were written. No commits or branch changes were made. The pre-existing `CARD_CODEX_LOG.jsonl` was left untouched.
