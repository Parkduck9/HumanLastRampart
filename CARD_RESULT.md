# K-085 result

Created UI redesign v2 review mockups using the existing project art and icons, with deterministic raster composition and real Korean font rendering. No Unity launch, Game changes, collaboration-log edits, or Git writes were performed.

## Files changed

- `Docs/References/codex_k085_v1/`: five 640x360 native mockups (battle, battle danger, yard, shop, training), corresponding 1920x1080 after images and 4x native previews; three untouched before screenshots and three 3840x1080 before/after pairs; a 1920x1080 font board; Korean README; manifest; reproducible build and verification scripts; validation JSON and browser screenshots.
- `_HTML/6_그림_화면/codex_K-085_UI2판_기대사진2_v1.html`: Korean, self-contained offline review page with screen comparisons, specification references, a selection-lift animation with pause/replay and reduced-motion support, font inventory, measured contrast, and explicit limitations.
- `CARD_RESULT.md`: this handoff.

## What changed visually

- Battle: one top band, five larger values/icons, one F1 help hint; separate example showing wall danger with warm red color and a drawn warning triangle.
- Yard: clean source background resampled onto the native grid and reduced to 96 colors; no meaningless gold floor lines; larger top-band text; K-070 hero reused.
- Shop: eleven K-067 items on two compartmented dark wooden shelf rows, face markers, hanging prices, raised selected lens and brass outline. One right-hand hanging tag contains icon, name, one-line effect, price and one purchase button.
- Training: three columns with ranks 1 through 3 stacked bottom to top; completed ranks and current rank shown redundantly by text and brass outline. Example current levels are 2/1/3.
- Font board: all requested example strings on wood and aged-paper colors, at actual 12px and nearest-neighbor 36px, using installed Gulim and Dotum as explicitly labeled diagnostic substitutes.

## Verified

- `verify.py` passed: native/after/4x dimensions; exact nearest-neighbor 3x and 4x equivalence for all five screens; fully opaque RGBA; no off-grid output pixels; before screenshots byte-identical to the supplied originals; pair and font-board dimensions.
- `verify_review.cjs` passed in headless Chrome using a direct file URL: all 14 embedded images decode; full-size preview is 1920 CSS pixels; no page overflow at 390px; no JavaScript errors; selection animation pause, replay and reduced-motion behavior work.
- Text contrast: conservative bright-wood background 11.28:1 for body and 7.01:1 for brass text; paper ink 7.10:1; button ink 8.65:1; danger text 7.57:1. All exceed 4.5:1. Font masks are binary, with no blended text edge colors.
- Visually inspected the five screen compositions, font board, desktop shop comparison and mobile page screenshot. Native-sheet foot-baseline checks are not applicable to this UI mockup card; no replacement character sheets were created.

## Open questions / leftovers

- Galmuri11, Galmuri14, NeoDunggeunmo and DungGeunMo were not found in the worktree, Windows installed-font inventory/font directories, user font directory, or the original repository's candidate-name search. No candidate samples were fabricated. Obtain actual font files, review their licenses, and select the final typeface before implementation.
- Size ambiguity is explicit in the page and manifest: 64px shelf icons and 4px selection lift are interpreted as native-art units, therefore 192px and 12px at 1080p. Literal screen values would conflict with a strict 3px grid.
- Shelf row placement of shared consumables/equipment is presentation-only, not an equipment ownership contract. Final grouping needs confirmation.
- No current training screenshot was supplied or found; training has an after mockup only, with no invented before image.
- Background grid snapping does not redraw the original art's larger internal shading clusters. Further visual review is required for perceived pixel consistency.
- Former battle HUD/help regions were reconstructed from adjacent screenshot pixels. This is disclosed; it is not a clean in-engine capture. Existing blue-cross artifact diagnosis, sound, first-three-uses hints, purchases and runtime integration remain outside this asset card.

Claude should integrate only after visual approval. No game behavior or final font choice is claimed as implemented.
