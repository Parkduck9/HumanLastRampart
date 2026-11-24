# K-065 result

Status: standing reference assets delivered for visual review; not user-approved or integrated.

## Work completed

- Created independent back-idle and front-idle references for archer, gunner, cannon crew, and wizard using the built-in image_gen tool. No hero was drawn. Exact generation/edit prompts and selected source locations are preserved in generation_manifest.json.
- Used the FIRST hero v6 generated.png as the sole hero style input. Existing Static sheets supplied each soldier's hair, clothing, and weapon identity. The cannon crew retains its pale brush-tipped cleaning ramrod. Front/back weapon sides were aligned.
- Exported eight 64x96 RGBA sprites and eight exact nearest-neighbor 4x previews. Human height is 52 pixels, center axis x=32, sole boundary y=88. Visually measured heads are approximately 16-17 pixels (31-33%).
- Built the Korean, self-contained review page with 1x/4x comparisons against the selected hero, current in-game art, and the discarded K-063/K-064 direction. Existing front-idle before/after is available only for wizard; the other original Static sheets are back views.

## Measurement discrepancy recorded

Direct measurement of the selected 1024x1536 source gives alpha>=128 bounds [266,224,765,1391), or 499x1167 pixels. Hair-to-chin is approximately y=224..610, about 33% of human height.

Strong source color boundaries are spaced mainly 16-17 source pixels, rather than the brief's estimated 22. A nearest-neighbor 64x96 reconstruction yields an estimated native human height of 73 pixels and head height about 24 pixels. The source is generated art with locally uneven steps, so this is a grid estimate, not proof of a perfectly uniform original grid. The requested output/comparison height remains 52 pixels, with a head target of about 17 pixels. The 22.44 source-pixel interval is the normalization factor 1167/52, not the measured native grid pitch. This distinction and the early estimate correction are documented in README, reference_measurements.json, and the review page. Both source-grid and normalized hero comparison PNGs are included; the hero itself was not regenerated.

## Files changed

- Docs/References/codex_k065_v1/: completed the existing README and reference exports; added final *_back_64x96.png / *_front_64x96.png, *_back_4x.png / *_front_4x.png, eight selected *_generated.png sources, current/previous comparison PNGs, source-grid estimate PNGs, all_allies_4x.png, and faces_inspection_16x.png.
- In that same output folder: build_review.py, verify_review.py, generation_manifest.json, reference_measurements.json, validation.json, review_validation.json, source_hashes_before.json.
- _HTML/4_그림_캐릭터/codex_K-065_아군6판_다시_v1.html.
- CARD_RESULT.md (this handoff).

No Game files, other reference folders, shared HTML index, collaboration docs/logs, or Git state were written. Unity was not opened or run. No commit/branch operation was performed; Claude owns committing and integration.

## Verification

- build_review.py passes: all eight sprites 64x96; body-center columns x=26..38 have human bounds y=36..88; sole boundary y=88; binary alpha; exact pixel-for-pixel 4x repetition.
- verify_review.py passes: all 48 embedded PNGs decode, source size 64x96, rendered size either 64x96 or 256x384, alt text present; all 93 original character-folder files retain their recorded SHA-256.
- All eight persisted high-resolution sources are byte-identical to the selected tool outputs. The manifest now uses workspace-local filenames, allowing rebuilds without the default imagegen output directory.
- Visually inspected selected high-resolution outputs, the final contact sheet, and enlarged front faces for hair/outfit/weapon identity and blue/white eye detail. Black outline is predominantly one pixel after normalization; exhaustive per-edge thickness and per-material shading-step certification was not performed. Palette mapping targets about three shades per group, with six brown shades shared across hair/leather/wood and common blue/white eye colors.
- Actual browser rendering was attempted twice but NOT verified: normal headless Chrome failed GPU process access; an in-process GPU attempt timed out after 45 seconds. No successful browser screenshot is claimed. Static review-page checks passed. Browser retry is optional via verify_review.py --browser in a suitable environment.
- No Unity, animation, normal-map, camera, lighting, combat, or runtime integration validation was performed.

## Open questions / leftovers

- No blocking question for asset delivery. User visual approval is pending, including the documented native-grid estimate versus requested 52-pixel export distinction.
- Front/back views are independently generated; small hair, uniform fold, and accessory differences may need refinement after visual review. Two views measure about 16 head pixels rather than 17; this one-pixel variation is explicitly shown in the Korean page and README.
- This is standing reference art only. Animation sheets, normals, gameplay capture, and Game integration remain outside K-065. Claude should wire approved assets under D-074, preserving the established game import/pivot contracts rather than copying these review-cell coordinates blindly.
