# K-084 result

Completed the existing K-084 draft mockups and review package for UI redesign v2, part 1. These are static approval mockups, not game assets or Unity captures.

## Deliverables and files

- `Docs/References/codex_k084_v1/after_main.png`: main menu with K-083 key art, Korean embossed title, one brass primary action and plain secondary menu text.
- `after_story_paper.png`, `after_story_wood.png`: both required dialogue material variants using the K-071 native archer portrait. Moved the portrait away from the character baked into the background.
- `after_result.png`: sealed aged-paper report covering 61.87% of the screen, enlarged victory seal, prominent loss count, four wall sides with colors and numbers, class icons/bars and hero/wizard busts.
- Four `compare_after_*.png` files, four native 640x360 images, four corrected 4x detail crops.
- Korean `README.md`, `manifest.json`, `contrast.tsv`, image/browser validation JSON and desktop/mobile review screenshots.
- Reproduction and verification scripts within the same output directory: `build_mockups.ps1`, `build_review.ps1`, `verify_images.ps1`, `verify_review.cjs`.
- Self-contained Korean review page: `_HTML/6_그림_화면/codex_K-084_UI2판_기대사진1_v1.html`.
- This English `CARD_RESULT.md`.

Existing artwork was reused and procedurally composited; no new image-generation call was made. Only the authorized output directory, review page and this result file were written. No Game files, collaboration logs or unrelated documentation were edited. No Unity launch or Git write was performed.

## Verification performed

- Reran the mockup and review builders after the portrait and detail-crop corrections.
- Reran image checks: all four after images are 1920x1080; alpha range 255..255; zero nonuniform 3x3 blocks, zero transparent pixels.
- Text contrast measured against actual background pixels at each glyph position using sRGB relative luminance: minimum 6.409:1; wood 10.321:1; brass primary action 7.070:1; title 7.940:1; victory seal 8.736:1. Full measurements in `contrast.tsv`.
- Reran Chrome headless review checks: all 10 embedded images loaded, expandable images and original-size control worked, no JavaScript errors, no horizontal overflow at 390px viewport width.
- Visually inspected main, result, both final story variants and mobile review capture. No obvious isolated compositing debris observed. This is not an automated classification of every small background detail as a stray pixel.
- Motion frame order, character foot-baseline and pivot checks are inapplicable to these static full-screen mockups. No Unity runtime, interaction or game integration checks were performed.

## Open decisions and limitations

- User must choose paper versus dark wood for story dialogue.
- No offline Korean pixel font was found in the repository/font inventory searched. Malgun Gothic Bold is rendered without antialiasing on the native grid; it is a documented fallback, not a genuine pixel font.
- K-083 key art is used. Its English logo was not inserted because user-facing text must remain Korean. The current Korean title uses embossed typesetting, not final bespoke lettering art; final Korean logo approval remains open.
- Backgrounds were snapped to 640x360 with nearest-neighbor sampling and reduced to at most 256 colors before exact 3x enlargement. Grid consistency does not guarantee hand-authored pixel-art detail density.
- Story body is 33px with 51px line spacing, rounding 49.5px to the nearest 3px grid line.
- Wall maximum HP 1000 and north HP 950 are explicit illustrative assumptions based on the current screenshot's north loss of 50; they are not live game data.
- Victory only; defeat, transitions, responsive runtime behavior and final 9-slice game asset delivery are outside this mockup package. Claude handles integration and commits after approval.
