# K-120 — Card-specific do-not list (written before drawing)

1. Do not use uneven pseudo-pixel blocks, feathered edges, airbrush gradients, or noisy painterly stone. Keep deliberate crisp pixel clusters and readable material shade steps.
2. Do not wash the daytime yard in bloom, grey haze, teal/purple neon, or glossy plastic highlights. Use muted warm masonry, restrained olive/terracotta/slate, and one directional daylight source.
3. Do not invent a symmetric fantasy village or replace the actual courtyard layout. Keep the central square lighthouse, north stair-to-plaza road and lamps, low perimeter buildings, and a clear walking foreground.
4. Do not bury the four functional buildings in clutter or illegible decorative lettering. Preserve the K-118 hammer-and-stone, crossed-spears, spike-trap, and coin-pouch identities.
5. Do not move the camera to the battle overview or crop the lighthouse out. Retain the existing 3840 × 1080 prep panorama framing and ground projection as the composition reference.

Research read before drawing: Retro-Diffusion's pixel-art-fixer documentation describes warped, anti-aliased pseudo-pixels and noisy blur (https://github.com/Retro-Diffusion/pixel-art-fixer/blob/main/docs/HOW_IT_WORKS.md); TU Delft's Pixel Fixer identifies banding and pillow shading as pixel-placement defects (https://repository.tudelft.nl/record/uuid%3Ab3c91b18-d9e8-48e0-91ce-8afafd2439d8). These informed points 1–2; points 3–5 apply the supplied project references and card constraints.

## Delivered

Created an assets-only daytime prep-yard redraw using the existing prep panorama as the frontal framing/ground-perspective reference and the actual courtyard screenshots plus K-115/K-118 references for the layout and material identities. The new image includes the square lighthouse, circular plaza, north stair/road with lamps, ten low perimeter buildings, well, hay, and woodpile. The four functional buildings have separate readable icon signs.

The replacement PNG is exactly the current file's **3840 × 1080**. A 1280 × 360 native image and exact nearest-neighbor 3x delivery / 4x preview are supplied. This is a static environment background, not a character motion sheet.

`hotspots.json` contains the four functional building bounds and the tower bound, individual door-ground points, proposed approach rectangles, and stair/plaza landmarks. Coordinates originate at the top left; rectangles are `[x,y,width,height]` with half-open edges. Bounds are manually measured envelopes including attached signs/props, not segmentation masks or approved gameplay triggers. Existing station IDs are retained (`repair`, `barracks`, `traps`, `shop`, `beacon`).

No dead-beacon background was required by the conditional brief: `PrepYard.BuildScene` always loads one fixed `PrepYard/prep_yard` image. Beacon state changes the separate flame overlay, not the background. The supplied background shows a localized lit lantern room.

## Files created

- `Docs/References/codex_k120_v1/prep_yard.png`
- `Docs/References/codex_k120_v1/prep_yard_native.png`
- `Docs/References/codex_k120_v1/prep_yard_preview4x.png`
- `Docs/References/codex_k120_v1/hotspots.json`
- `Docs/References/codex_k120_v1/manifest.json`
- `Docs/References/codex_k120_v1/README.md` (Korean)
- `Docs/References/codex_k120_v1/generation.json` (both actual built-in image-generation prompts and processing provenance)
- `Docs/References/codex_k120_v1/sources/generated_padded.png`
- `Docs/References/codex_k120_v1/package.ps1` (crop/resample and pixel verification)
- `Docs/References/codex_k120_v1/build_review.js`
- `Docs/References/codex_k120_v1/review_check.js`
- `Docs/References/codex_k120_v1/pixel_verification.json`
- `Docs/References/codex_k120_v1/review_verification.json`
- `Docs/References/codex_k120_v1/review_desktop.png`
- `Docs/References/codex_k120_v1/review_viewport.png`
- `Docs/References/codex_k120_v1/review_mobile.png`
- `_HTML/6_그림_화면/codex_K-120_정비마당_v1.html` (Korean, all resources embedded)
- `CARD_RESULT.md`

No existing tracked file was changed. No edits to `Game/`, shared docs, live collaboration records, existing HTML/index files, or logs. No Git writes. The runner-managed `CARD_CODEX_LOG.jsonl` was already present in the first successful status inspection and was not edited by this task.

## Verification performed

- Ran `package.ps1`: delivery/native/preview dimensions match the manifest; every delivery pixel equals its native source pixel at exact 3x, every preview pixel equals its native source at exact 4x. Both mismatch counts are zero.
- Delivery alpha is fully opaque: zero nonopaque pixels. No magenta padding pixels remain. A static opaque background has no transparent stray components or common character foot baseline; door-ground points are supplied separately. Visually inspected the native image and browser detail crops for floating props, clipped signs, tower framing, and padding artifacts.
- Checked all five building boxes and proposed approach rectangles are within the 3840 × 1080 canvas, native/game bounds agree at 3x, and door-ground points lie within their building envelopes.
- Opened the self-contained HTML via `file://` in actual headless Google Chrome through Playwright. All three embedded images decoded at expected sizes (old/new 3840 × 1080; K-118 reference 1920 × 1080). Zero page errors and zero external network requests.
- Exercised building overlay hide/show, approach overlay, full/1920px viewport modes, left/right pan endpoints, and all five place jumps. Checked desktop 1500 × 1100 and mobile 390 × 844; mobile had no document-level horizontal overflow. Saved and visually inspected the three browser screenshots.
- Compared SHA-256 hashes of `PrepYard.cs`, `PrepYardProjection.cs`, the original `prep_yard.png`, and `layout.json`: all unchanged. Final read-only Git diff showed no tracked changes.

## Integration leftovers / limitations for Claude

1. The new background already includes the lighthouse. Adjust the existing `AddBeaconTower`/flame overlays so a second tower is not drawn. If future gameplay requires a background-level dead state, produce a localized lantern-room variant then; current code does not switch backgrounds.
2. Remap station data and review the old `walkable_mask`, occluder pictures, obstacle polygons, and hero spawn. The existing masks and colliders describe the old yard and cannot be assumed compatible. The supplied approach rectangles are proposals only; reachability was not tested.
3. The original image size, panorama presentation, frontal composition and ground-perspective reference are retained. This is an illustrated redraw, **not** a metrically calibrated rerender through the original Unity projection matrix. The 12m tower is a design reference; exact world-to-picture building heights and collision alignment are not certified. The existing projection formula is recorded in the manifest for remapping.
4. Unity was not opened or run. Actual in-game scrolling, sprite scale, occlusion, lighting/state behavior, movement/collision and integration remain unverified.

No unresolved asset-production question requires user input. Game wiring and gameplay validation remain with Claude.
