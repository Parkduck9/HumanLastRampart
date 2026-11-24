# K-111 — Do-not list (written before image production)

1. Do not omit Hagen's mouth or substitute a beard/shadow for expression-specific lips and a visible mouth opening where appropriate.
2. Do not wash the night scene in grey haze, full-screen bloom, glossy surfaces, or purple/teal neon. Keep blue-black ambient values and localized amber light.
3. Do not invent a picturesque castle: retain the real game's square four-wall footprint, masonry, crenellations, courtyard, camera and scale.
4. Do not introduce blurred painterly detail, inconsistent pixel density, stray opaque pixels, or shifting character feet.
5. Do not add foreground faces, ornamental clutter, or bright detail in the lower dialogue third; heroes remain distant silhouettes.

Research read before production: https://runway.com/resources/why-do-ai-images-look-weird ; https://www.unrealengine.com/spotlights/octopath-traveler-s-hd-2d-art-style-and-story-make-for-a-jrpg-dream-come-true . These identify incomplete features, overly polished lighting, and the deliberate use of point lighting and shadows in HD-2D. The above constraints are specific to this brief.

Status: complete — asset delivery and review page verified. Integration remains with Claude.

## Work completed

- Hagen: edited all four expressions in neutral / determined / alarmed / tired order. Transferred only dark mouth pixels from the built-in image generator's edited sheet. Every original pixel outside story-canvas mouth rectangle [128,128,152,140) is preserved, including original alpha, hair, eyes, equipment, stance and foot bounds. Delivered individual 256x384 portraits, 4-frame story sheet, native 96x128-cell sheet, and exact nearest-neighbor 3x game sheet with 288x384 cells.
- Braziers: replaced the three 192x192 glow sprites with compact four-step alpha falloff. Native radii are 36/46/56 pixels; maximum alpha 40/64/88. Existing K-103 body and six-frame flame animations remain unchanged. Supplied exact 3x game glows.
- Fog: replaced the broad fog bank with a blue-black horizontally tiling strip occupying only y36..63 within the original 512x128 canvas; the lower half is transparent. Supplied exact 3x game strip. Legacy corners and wisps must be disabled during integration.
- Battle mockup: built directly on the specifically requested beacon_game_20261022/05_pf_night_clean.png. Preserved the screenshot's camera, wall, terrain and soldier layout; applied blue-black grading, localized warm falloff, two existing K-103 brazier sprites, cast shadows, and the thin band only at the distant upper screen edge. The screenshot's original torches and distant fire effects remain. This is an asset composite, not a Unity capture.
- Story: redrew dead beacon at dusk and the same fortress relit at night. Used real tactical and battle screenshots as footprint and masonry references: low square four-wall fortress, large open courtyard, flat surrounding field, no cliff castle or tall keep. People are distant faceless silhouettes. Lower third stays dark and low contrast for dialogue. The story camera is a pulled-back oblique view to show all four walls, not the close battle camera.
- Created a Korean, self-contained offline review page with before/after comparisons, real screenshot references, current in-game portrait/torch/background comparisons, expression stepping, preserved six-frame flames under each revised glow, story before/after switching, pause/resume, and dialogue-area overlay. Existing-card proposals are explicitly distinguished from installed game assets; Hagen and the K-105 backgrounds are not installed at this snapshot.

## Files changed

Only new files in the three authorized output locations:

- `CARD_RESULT.md` — this English AI-to-AI handoff, do-not list written before production.
- `_HTML/6_그림_화면/codex_K-111_사용자의견수정_v1.html` — Korean self-contained review page.
- `Docs/References/codex_k111_v1/`:
  - Four `portrait_hagen_*.png` files and `hagen_expressions_story_256x384.png`, `hagen_expressions_native_96x128.png`, `hagen_expressions_game_288x384.png`.
  - `native/glow_low.png`, `glow_normal.png`, `glow_high.png`, `fog_band.png`, and corresponding `game/` files.
  - `story_dead_beacon_1920x1080.png`, `story_relit_beacon_1920x1080.png`, `mockup_north_wall_high_1920x1080.png`. The inherited mockup filename is retained; its compositing example uses the unchanged K-103 normal brazier body/flame.
  - `previews/`: 12 exact nearest-neighbor 4x previews covering all individual portraits, native expression sheet, glows, fog, story backgrounds and battle mockup.
  - `sources/`: five unmodified generated images (mouth edit, glow, fog, dead story, relit story).
  - `README.md` (Korean), `manifest.json`, `prompts.json` (full final prompt set, built-in image_gen), `build.ps1`, `build_review.cjs`, `verify_assets.py`, `verify_review.cjs`, `validation.json`, `review_validation.json`, `review_desktop.png`, `review_mobile.png`, `review_night.png`.

`manifest.json` provides the exact deliverable image inventory, dimensions, alpha extrema, pivots, frame order, blending and integration conditions. Original K-103/K-105/K-108 directories, Game/, shared documents, live logs and existing HTML pages were not modified. No Git writes, Unity launch, Play, or external messaging occurred. Git was unavailable on PATH; branch names were read directly from the worktree and original repository HEAD files (card/K-111 and develop respectively), without changing either.

## Verification performed

- `validation.json`: **96/96 checks passed** on final outputs, including dimensions, unchanged Hagen alpha and foot bounds, changes confined to the mouth rectangle, single connected character silhouettes, exact nearest-neighbor 3x sheets, exact 4x previews, restrained glow alpha/transparent borders, connected fog strip, exact matching tile edges, empty fog lower half, opaque backgrounds, clean transparent RGB, and low luminance/contrast of both dialogue thirds.
- Final Hagen native foot edge y120 and game foot edge y360; original horizontal bounds preserved. Story portraits retain foot edge y352.
- Fixed an initial GDI+ scaling issue that rounded RGB on translucent pixels: scaling now copies RGBA bytes directly. Final pixel-exact comparisons pass.
- `review_validation.json`: actual headless Edge opened the HTML via file URL. **25 images and 16 CSS image backgrounds loaded from embedded data**, all five frame-stepping previews advanced, story switching worked, pause froze animation, dialogue overlay appeared, 390px mobile had no horizontal overflow, and no JavaScript errors occurred. Desktop/mobile/full-page and night-detail screenshots saved. The story test waits for an observed image change instead of assuming exact wall-clock timer timing.
- Visually inspected source references, generated edits, final Hagen sheet and 4x native preview, final fog/glow, battle composite, generated story pair, and browser desktop/night/mobile captures.

## Open questions / leftovers

No unanswered content question blocks asset delivery. User visual approval remains pending.

Claude must import/wire the assets, replace the three glows and fog strip, disable old broad fog corners/wisps, enforce the entire fog geometry beyond 45m, and set actual point lighting/occlusion/shadows. The mockup's geometric cast shadows do not implement runtime lighting. Keep existing K-103 body/flame sheets and their frame counts/order.

Unity import, runtime exposure/sorting, world-space fog distance, actual shadow behavior, Play and fresh in-game captures were **not performed**. The result is assets only; this card does not claim the game itself has been fixed.
