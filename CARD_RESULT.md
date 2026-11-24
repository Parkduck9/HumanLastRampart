# K-115 — Tall lighthouse tower art

## Card-specific do-not list (written before drawing)
- Do not use airbrushed stone, pillow shading, random speckle, or mixed pixel densities: use readable ashlar clusters, restrained material ramps, and nearest-neighbor delivery.
- Do not flood the courtyard with haze, bloom, or ambient orange: retain the real dark screenshot and confine warmth to the lamp room and door lantern.
- Do not use purple/teal neon, plastic gold, or elaborate generic fantasy ornament: use soot-dark stone, weathered bronze, timber, and one restrained beacon-order emblem.
- Do not invent a new tower silhouette or scene: keep the actual 226/236 camera, tapered square shaft, gallery, pointed roof, and existing scale.
- Do not bake theatrical lighting into repeatable wall textures or leave detached glitter pixels around sprites; keep lighting in separate overlays and flame animation anchored.

Research read before drawing: [Epic / Acquire HD-2D production interview](https://www.unrealengine.com/spotlights/octopath-traveler-s-hd-2d-art-style-and-story-make-for-a-jrpg-dream-come-true), [Tsubu pixel-art shading tutorial](https://tsubu.art/blog/pixel-art-shading). The concrete constraints above are this card's application of the sources and the supplied COMMON rules, not a claim that either source defines all AI art.

## Work completed
- Created separate production textures and alpha sprites for the existing tall 3D lighthouse: weathered ashlar wall, gallery trim, slate roof, window frame/bars/broken glass, bronze brazier/reflector, arched timber door with lantern, standalone lantern, and restrained interior glow.
- Delivered DEAD and LIT window, brazier, door and lantern states. The broken glass remains after relighting. Shared alpha masks keep the principal state silhouettes aligned.
- Created three distinct six-frame fire loops, anchored at the same baseline. Preserved the existing `flame_warming.png` and `flame_lit.png` replacement names and six-frame left-to-right order; `flame_bright.png` is new. Bright fire was shortened to remain inside the lamp-room lintel; a resampling orphan pixel was removed.
- Delivered native 96x128 cells, exact nearest-neighbor 3x game cells (288x384), and 4x previews. Texture density is 32 px/m; game sprites use 96 PPU to retain native world size.
- Painted new tower art over actual screenshots 226 and 236, composited back into the original 1920x1080 screenshots only within explicitly listed tower-region rectangles. The rest of each screenshot remains pixel-identical.
- Built a self-contained Korean review page with original/mockup comparison, DEAD/LIT components, three animated assembled lamp rooms, current in-game flame-sheet comparison, repeated texture previews, pause/step/speed controls, day/night tabs and image zoom.
- Used built-in image_gen for artistic generation and edits. Mechanical preparation included crop/alpha cleanup, fixed state masks, palette reduction, wrap-edge reconciliation, baseline alignment and nearest-neighbor exports. No Python image editing or external image API was used.

## Files changed
Only these authorized locations were written:
- `Docs/References/codex_k115_v1/`: 48 exported asset/preview/mockup PNGs; `manifest.json`; Korean `README.md`; Korean prompt summaries in `prompts.json`; `verification.json`; `review_validation.json`; three browser QA PNGs (`review_desktop.png`, `review_fire.png`, `review_mobile.png`).
- `_HTML/6_그림_화면/codex_K-115_등대탑_v1.html`.
- Root `CARD_RESULT.md` (this report, including the pre-drawing do-not list).

Runtime asset families: `textures/` contains four textures; `native/` and `game/` each contain eleven sheets. All exported textures/sprites/previews/mockups are enumerated with sizes, cells, pivots, tiling and frame order in `manifest.json`. Browser QA screenshots are evidence, not runtime textures.

No Game files, existing references, collaboration logs, other documentation or HTML indexes were edited. Unity was not opened or run. No Git writes were performed.

## Verified
- All 48 manifest-listed PNG sizes match their declarations.
- All 26 native animation/static frames have binary alpha, one connected component, no detached pixels, and occupied bottom edge y=120. Cell pivot stays (48,120); silhouette bounds are centered within 0.5 px (odd-width pixel silhouettes), with no frame re-trimming during playback.
- All eleven game sheets exactly equal 3x nearest-neighbor native pixels; game pivot is (144,360), Unity normalized pivot (0.5,0.0625).
- Each fire strength has six unique frames. The existing game flame sheets were read and confirmed to contain six 288x384 cells.
- Wall and roof opposite edges match in both axes. Trim matches in X and is explicitly clamped in Y.
- Interior glow maximum alpha is 94/255 and its entire border is transparent.
- Both mockups preserve every original pixel outside their declared tower edit regions.
- Headless Edge loaded the standalone review page: 27 image elements and five animation backgrounds decoded, all embedded; animation, pause, frame step, scene tabs and zoom worked; no page errors; no horizontal overflow at 390 px. Desktop, fire-section and mobile captures were visually inspected. Final page verification is recorded in `review_validation.json`.

## Open questions / handoff
- No blocking questions. Claude must wire textures/sprites into the existing 3D tower and confirm UV scale, face orientation, alpha material ordering, point-light intensity, beam interaction and actual camera views in Unity. No Unity import, rendering or Play verification is claimed.
- The mockups are generated tower paintovers, not renders of the exported textures on the actual mesh; use them for appearance and darkness review. Actual surface mapping and lighting still require integration review.
- Manifest provides separate lamp assembly mount positions: window pivot (48,120), brazier pivot (48,113), fire pivot (48,99), glow rectangle (16,57,64,56), all native assembly pixels. Multiply offsets by three for game sheets. Clip the glow to the lamp room; use existing scene lights for world illumination.
- The door includes a lit/unlit lantern; standalone lantern sprites are alternatives and should not be stacked over the included lantern.
- Optional banner and gallery chain were omitted. No design/API/scene changes were made.
