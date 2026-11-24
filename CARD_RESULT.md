# K-125 — do-not list (recorded before drawing)

- Do not use airbrushed gradients, neon, bloom halos, or glossy plastic brass. Keep the K-123 warm palette, top-left light, and three material shade steps.
- Do not add decorative vines, random texture noise, or repeated emblems to thumb controls. Shape must communicate direction, press state, or action.
- Do not mix pixel densities or anti-aliased silhouettes. Author on the native grid and enlarge exactly 3x with nearest-neighbor replication; translucency changes alpha only.
- Do not bake labels, numbers, or compass letters into assets. Use directional glyphs; the requested question mark is a pictogram. Korean labels belong to the review/mockup annotation layer.
- Do not invent or repaint the battle, lighthouse, or map. Composite controls at actual game size onto real 1920x1080 captures and keep the hero and HUD unobstructed.

Research read before drawing: K-123 do-not list (C:/Unity/1/HLR-cards/K-123/CARD_RESULT.md); Docs/GDD/UI_Art_Spec_v1.md; https://www.spritefusion.com/pixel-snapper/how-to-fix-ai-pixel-art (inconsistent grid, softened edges, and excessive colors). These are production quality checks, not provenance detection claims.

Status: asset delivery complete. Runtime integration and device validation remain with Claude.

## What was made

Created 80 native pixel assets with matching exact 3x game PNGs and 4x previews, using the K-123 parchment/wood/worn-brass family. No generative repainting of screenshots was used.

- Horizontal slider and round stick: idle, left, right, in, out; each also has a 60%-opacity variant. Separate horizontal 9-slice track and idle/pressed thumbs are included.
- Blank 96px and 72px action buttons: normal, pressed, disabled, highlighted, plus separate four-frame pulse overlays.
- Nineteen 48px game glyphs covering every requested action, with drag-select and order-target added for map controls. Compass letters are not baked in; the requested question mark is a help pictogram.
- A 192px compass plate with central lighthouse and eight independently registered normal/pressed directional segments.
- Four-frame tap ripple and eight-frame clockwise long-press ring, as sheets and individual frames.
- Three real 1920x1080 screenshot mockups: W35 wall-top battle, the exact requested W35 lighthouse capture, and the supplied battle map. Original screenshots and transparent control overlays are included.
- A self-contained Korean review HTML with source/composite comparisons, actual-size viewing controls, nine animated state demonstrations, a state sheet, K-123 family samples, and an expandable complete catalog.

## Files changed

Only the authorized output paths were written:

1. Docs/References/codex_k125_v1/
   - 1x/: 80 transparent native PNGs.
   - 3x/: 80 transparent game PNGs.
   - previews/: 80 exact 4x PNGs, 3 source captures, 3 transparent control layers, 3 composites, and 1 Korean state sheet.
   - manifest.json: roles, states, sizes, cells, frame orders, pivots, 9-slice borders, palette, import notes, source references, and mockup placement/protected-area coordinates.
   - README.md: Korean delivery and integration guidance.
   - 검증.json: Korean verification results and explicit unperformed checks.
2. _HTML/6_그림_화면/codex_K-125_터치조작_v1.html
3. CARD_RESULT.md

Total: 250 PNG files. No Game/, shared collaboration records, existing K-123 assets, or other project files were edited. No generator/source-code files are delivered. No Unity launch or git writes were performed.

## Verified

- All 80 native sizes, sheet cell counts, and frame-order lengths match the manifest.
- Every game pixel is an exact 3x nearest-neighbor replication of its native pixel; every asset preview is an exact 4x replication.
- All nontransparent RGB values belong to the declared palette, including the restrained disabled-metal brown #766347.
- Alpha is binary except the deliberate 60%-opacity pads and discrete ripple frame alphas. No anti-aliased silhouette pixels.
- No isolated single-pixel components under 8-neighbor connectivity.
- Movement and feedback sheet frames match their individual PNGs pixel for pixel.
- All mockups remain exactly 1920x1080. Pixels outside each transparent overlay are identical to the source.
- Overlay alpha is zero throughout declared hero/HUD protected rectangles. Map has no visible hero. All placements remain inside the viewport.
- Inspected the Korean state sheet and all three mockups with the image viewer.
- All 98 embedded PNG resources decode successfully. All 80 current previews, nine animated sheets, and six comparison screenshots match the delivered files embedded in HTML. No external image requests.
- JavaScript syntax checked successfully with Node.
- Character foot-line checks are not applicable to this UI-only delivery; cells retain fixed dimensions and center pivots.

A first pass detected one-channel rounding introduced when compositing translucent sheets through GDI. Sheet assembly was changed to copy RGBA pixels directly; the final palette and exact-replication checks passed.

## Open questions / leftovers

- Browser visual/interaction verification was attempted but could not run: the CUA inventory returned no available browsers or apps. HTML image decoding and script syntax were verified, but actual CSS animation playback, responsive rendering, and review-button behavior were not observed in a browser.
- Unity import, touch input, physical-phone usability, DPI/safe-area scaling, hit areas, gestures, speed cycling, and runtime state wiring were not tested and remain Claude's integration work. Control placement is a mockup, not a finalized input API.
- K-123 README.md was absent in both this worktree and the sibling K-123 worktree. Used its manifest, actual asset catalog, UI art specification, and original K-123 do-not list from CARD_RESULT.md instead.
- Existing keyboard prompts remain visible because the real screenshots were deliberately preserved. Claude should adapt them when wiring mobile UI.
- No blocking art questions remain.
