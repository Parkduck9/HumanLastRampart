# K-132 result

## Anti-slop constraints (before drawing)
- Do not introduce blurred or mixed-size pixels into the existing title lettering.
- Do not add airbrushed gradients, bloom, haze, or glossy effects.
- Do not invent illegible Hangul or omit any strokes in 빛 을 잇 는 자.
- Do not repaint or add decorative clutter to the approved artwork.
- Do not use pillow shading or uncontrolled color bands in the lettering.

References consulted: https://pixelart.school/learn/pixel-art-terms and https://pixelartworkshop.com/guides/troubleshoot-jaggies-mixels-pixel-art-basics

## Deliverables
- Reused the existing gold pixel glyphs for 빛을 잇는 자, centered the five syllables, and kept LIGHTBEARERS unchanged. Erased old glyph pixels with nearby same-row background pixels, only within the declared title rectangles.
- Added `Docs/References/codex_k132_v1/store/` (7 files copied from K-131, 4 title edits), `msix/` (18 files from K-130, 4 title edits), `previews/`, `manifest.json`, Korean `README.md`, and `_HTML/6_그림_화면/codex_K-132_스토어그림_제목변경_v1.html`.
- Super hero and app tile files were copied unchanged. The other MSIX logos are text-free and copied unchanged. Source color-profile PNG chunks were preserved in modified images.

## Verification
- Compared all 25 image dimensions and RGBA pixels to the respective sources. All eight title edits have zero changed pixels outside their manifest rectangles and zero alpha changes; the other 17 files have identical SHA-256 hashes.
- Conservative minimum WCAG contrast against all non-title pixels within the title rectangle: poster 16.52:1, box 14.51:1, wide and splash 15.84:1. Title rectangles are in the top two-thirds.
- Visually inspected the poster, box, wide and splash lettering; decoded all 12 embedded review-page PNGs. No animation, foot baseline or transparent sprite stray-pixel check applies to these static store compositions.
- Unity, Git writes, package wiring and Store submission were not performed.

## Open questions / leftovers
- Browser rendering of the review page and the final Microsoft Store display have not been checked. Claude should wire and commit the assets.
