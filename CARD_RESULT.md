# K-131 — Art correction result

## Card-specific do-not list (recorded before drawing)
- Do not retain white/silver hair or use the outdated K-070 hair palette; match the current brown-haired game hero.
- Do not replace the broad white collar, dark cadet coat, gold trim and keeper cape with generic fantasy armour or glossy materials.
- Do not blur pixel clusters, mix arbitrary pixel densities, or airbrush the character edges.
- Do not add neon purple/teal, global bloom or fog that weakens the existing dark night and warm lamp light.
- Do not redesign the composition, title, fort, beam or horde, or add decorative clutter without gameplay meaning.

Research read before drawing: https://developer.mozilla.org/en-US/docs/Games/Techniques/Crisp_pixel_art_look ; https://www.sprite-lab.com/guides/how-to-make-pixel-art-sprites-for-games . These explain blur, uneven pixel blocks and excessive near-identical colours; the above constraints apply them to this correction.

Work in progress. Verification and handoff will be appended after production.

## Completed work
- Corrected the hero in all three K-130 store illustrations to brown hair, broad white shoulder collar, dark cadet coat with gold piping, keeper cape and amber hand lamp, using current Game sprites and the supplied w45 battle capture as authority.
- Read the character integration references, K-070 proportions and current archer sprite. K-070/v6 still depict silver hair, so current game identity overrides their hair colour for this card.
- Used built-in image_gen, with a collar follow-up for the box. Rejected the full-wide edit because its hero placement drifted, then edited an enlarged original crop for the wide artwork.
- Assembled only bounded hero-area rectangles into the original logical canvases. Nearby background inside those rectangles can change; all pixels outside remain identical. No broad composition, typography, beacon, fort, horde or soldier redesign.
- Exported all six requested sizes and copied the unchanged 300x300 app tile so the store filename set matches K-130.

## Files changed
New files only under Docs/References/codex_k131_v1/: store/ (7 PNGs), previews/ (logical canvases, 4x canvases, hero details and game-reference crops), manifest.json, prompts.json, verification.json, README.md.
New review page: _HTML/6_그림_화면/codex_K-131_스토어그림_주인공수정_v1.html.
Root handoff: CARD_RESULT.md.
No Game/, existing K-130 assets, collaboration logs or other existing docs were edited. No Unity invocation or git writes.

## Verification performed
- Six exports: exact requested dimensions, zero nonopaque pixels and zero mismatching pixels against nearest-neighbour 4x/2x logical sources.
- Native comparisons: zero changes outside each declared edit rectangle, and zero changes in the bottom third for all three artworks. Therefore the original title lettering, title placement and wide-art text-free layout remain intact.
- App tile: identical SHA-256 to K-130.
- HTML: all 13 embedded image tags decoded successfully with positive dimensions; no external image dependencies.
- Viewed final poster, box and wide hero details; inspected brown hair, white collar, foot contact and patch edges. Sprite frame baseline/pivot/animation checks do not apply to static store illustrations. Transparent-background stray-pixel checks do not apply to these opaque canvases.
- Detailed numeric checks and hashes: Docs/References/codex_k131_v1/verification.json.

## Open questions / leftovers
- No unresolved identity or asset-size question.
- CUA reported no available browsers (Edge unavailable; browser inventory empty). Browser rendering and button behaviour were NOT tested. Claude should open the self-contained review HTML and verify its mask toggle and original-image links.
- Claude handles final visual acceptance, Store slot upload/preview and commit. MSIX assets remain K-130's existing assets; this card supplies store art only.
- Exact prompts are preserved in prompts.json; selected outputs were made with the built-in image_gen tool, not the CLI fallback.
