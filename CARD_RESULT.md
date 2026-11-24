# K-088 result

Completed asset production only. No Game/ files, shared documents, collaboration logs, Unity settings, or existing review pages were edited. Unity was not opened or run. No Git write operations were performed.

## Delivered

- `Docs/References/codex_k088_v1/native/`: nine RGBA assets: right-facing muzzle flash (3 frames), away/up-facing muzzle flash (3 frames), rising smoke (4 frames), four smooth contact shadows (small, medium, 5 m wall, 7 m wall), and two pixel-art dirt skirts (5 m / 7 m).
- `Docs/References/codex_k088_v1/game/`: exact 3x nearest-neighbor copies of the three VFX sheets and two dirt skirts. VFX cells are 288x384; native cells are 96x128. Smooth shadow textures remain at their intended 64–256 px source sizes and use bilinear filtering.
- `Docs/References/codex_k088_v1/previews/`: exact 4x previews of all nine final assets.
- `manifest.json`: dimensions, frame order/durations, anchor/pivot coordinates, alpha/blend/filter recommendations, intended ground-quad sizes, current-reference hashes, and integration notes.
- Korean `README.md` and `generation_prompts.md`; reproducible `build_assets.py`, `verify_assets.py`, and `verify_review.cjs`; machine-readable `verification.json` and `review_validation.json`.
- `source/`: two original transparent images generated with the built-in image_gen tool. Final sprites were extracted, reduced, palette-cleaned, alpha-adjusted, and registered. Shadows were generated mathematically.
- `_HTML/5_그림_전장/codex_K-088_총구불꽃그림자_v1.html`: standalone Korean review with embedded images, animated current gunner vs. new muzzle overlays, animated isolated effects, ground-contact comparisons, native asset gallery, and comparison with the existing K-068 dirt decal.
- `Docs/References/codex_k088_v1/review/`: desktop/mobile browser captures, four firing-time captures, and contact comparison capture.

## Verified

- All nine final assets have the declared RGBA dimensions, visible alpha, zero-alpha outer borders, zero RGB in fully transparent pixels, and one connected visible component per frame (no disconnected stray pixels).
- All five game assets are pixel-exact 3x nearest-neighbor enlargements. All nine previews are pixel-exact 4x enlargements.
- Native flash emission anchors remain registered across ignition/flame frames. Smoke rises deliberately and fades across four frames. VFX have no feet; the y=120 character reference line remains clear. The effect socket anchor is (48,80) native / (144,240) game, distinct from the character foot reference (144,360).
- All four shadows are symmetric, have more than 100 alpha levels, and monotonically fade toward transparent edges.
- Referenced current gunner, K-068 wall and dirt files still match their recorded SHA-256 hashes.
- Headless Chrome loaded the local review without external requests or JavaScript errors. All 11 HTML images and 12 canvas-source images decoded. Animation, pause/resume, restart, speed selection and background selection worked. The 390 px mobile viewport had no horizontal overflow.
- Visually inspected generated source images, final sprite previews, the desktop review, muzzle placement at 50 ms, and the contact comparison capture.

## Integration leftovers / limitations

1. Claude must import and wire these assets, choose final world scale/material/depth settings, and trigger non-looping playback on actual shots. Suggested timing: flash 35/45/40 ms (120 ms total), smoke starts 80 ms after the shot and lasts 400 ms.
2. The current `gunner_fire.png` is already 1728x384 (six 288x384 cells), and frame index 2 contains a baked-in legacy flash. The review deliberately overlays new VFX on the clean aiming frame index 1. Resolve the baked-flash duplication when wiring the game; no current sheet was replaced.
3. The current gunner is a rear/right-oblique view. The away-facing effect is demonstrated at its muzzle as a directional sample, not as a newly authored exact rear-facing gunner animation.
4. Flash uses transparent straight alpha with `SrcAlpha One`; smoke/dirt use ordinary alpha blending. Shadows contain black RGB plus smooth alpha and require alpha-weighted multiplication: `ground_rgb * (1 - texture_alpha * opacity)`. Do not use raw RGB-only multiplication, which would produce a black rectangle.
5. Unity rendering, lighting, actual socket registration, occlusion, distant sampling and in-game captures remain unverified. Review contact examples are illustrative composites, not game screenshots.

No user decision is required to consume the asset package. Final in-game visual acceptance remains with the user after Claude integrates it.
