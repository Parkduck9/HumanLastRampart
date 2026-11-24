# K-112 — Card Result

## Do-not list (written before image creation)
- Do not wash this battlefield in grey haze, global bloom, or airbrushed light; keep unlit ground dark and the wall pixels sharp.
- Do not replace the real north-wall camera, soldier rows, crenellations, or terrain with a generic fantasy fortress.
- Do not use purple/teal neon, glossy plastic stone, decorative sparks, or unrelated fantasy props.
- Do not mix pixel densities or smear character outlines; preserve the screenshot's readable sprites.
- Do not light enemies without shadows consistent with the courtyard-side beacon; keep output distinct from beam width and lantern brightness.

Research read before drawing: https://www.theatlantic.com/technology/archive/2024/08/why-does-all-ai-art-look-same/679488/ (airbrushed smoothing and exaggerated saturation); https://www.spritefusion.com/pixel-snapper/how-to-fix-ai-pixel-art (inconsistent/off-grid pixels). These inform quality constraints, not an AI-detection claim.

## Status
In progress. No Unity execution or Git writes.

## Work performed
Created three built-in image_gen lighting paintovers from the real north-wall night screenshot: Fortress 1 / first night / 40%, Fortress 1 / ignition / 70%, Fortress 2 / ignition / 100%. Kept the horizontal wall, camera composition, soldier formations, wall lamps and terrain recognizable. Added three goblins for light/shadow comparison and subdued the existing left-side fire. The 100% version extends a crisp warm shaft to the field edge and shows courtyard-side source spill entering the bottom edge.
Generated at 1672x941; delivered at 1920x1080 using nearest-neighbor resizing (no crop). This is conceptual lighting paintover art, not pixel-identical screenshot grading or runtime-ready sprite art.
Made a small three-panel comparison strip and a self-contained Korean review HTML with original-versus-selected comparison, stage buttons, and timed still switching. Exact generation prompts and input provenance are in manifest.json.

## Files changed / added
Only the authorized deliverables:
- CARD_RESULT.md
- Docs/References/codex_k112_v1/beacon_40_f1_night1.png
- Docs/References/codex_k112_v1/beacon_70_f1_ignition.png
- Docs/References/codex_k112_v1/beacon_100_f2_ignition.png
- Docs/References/codex_k112_v1/beacon_growth_strip.png
- Docs/References/codex_k112_v1/manifest.json
- Docs/References/codex_k112_v1/README.md
- _HTML/6_그림_화면/codex_K-112_등대출력성장_참고_v1.html

No Game/, source screenshots, collaboration logs, or other repository documents modified. No Git writes or Unity execution.

## Verification
- Visually inspected all three generated paintovers and the side-by-side strip: progressive reach and enemy readability, consistent north-wall composition, no obvious floating stray marks outside scene content. This is visual review, not a guarantee of unchanged sprite pixels.
- Verified three final PNGs are exactly 1920x1080 and alpha is 255 at every pixel.
- Verified strip is 1440x270. Detected semi-transparent resize-edge pixels in its first export, corrected by flattening onto an opaque RGB background, and refreshed its embedded HTML copy.
- Decoded all six HTML image sources successfully (strip, three stages, original screenshot, selected-stage viewer). All image data is embedded; no external image URLs needed.
- Browser rendering / interaction verification NOT completed: installed Chrome headless crashed (exit -2147483645; encryption/GPU initialization errors), and CUA reports no available browsers. Do not count browser display or button behavior as tested.
- Sprite frame count, foot baseline, pivot and 4x sprite previews are not applicable to full-scene stills.
- No gameplay, renderer, animation, Unity Play, or integration checks performed.
- Git status could not be checked with the command available in this shell (git not on PATH); no Git writes attempted.

## Open questions / leftovers
- IMPORTANT: The supplied close north-wall camera does not show the courtyard or lighthouse. These deliverables do not directly depict the courtyard being dimly lit or the burst behind the visible tower top. The 100% bottom-edge spill only implies an offscreen source. A real screenshot showing courtyard and tower is needed to finish those specific viewpoints without inventing the game's layout.
- Weak flicker is a static snapshot intent; temporal flicker remains unverified.
- Beam width and wall-lantern brightness are held fixed as art intent, not physically calibrated values. Visible footprint changes with intensity/falloff. Percentages are concept stages, not measured runtime lighting.
- Generative editing introduces minor redraws; the source screenshot's exact pixels are not preserved.
- User approval and Claude integration remain outside this reference-art card.

## Final status
Delivered the three battlefield lighting references, comparison strip, provenance manifest and Korean review package. Partially meets the full scene brief: visible courtyard / tower-top burst viewpoints remain outstanding as described above. Browser rendering remains unverified.
