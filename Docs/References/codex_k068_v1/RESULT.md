# K-068 delivery handoff (completed by K-069)

31 transparent PNGs, three power-of-two sheets, and a self-contained Korean review HTML. Rects use bottom-left pixels; pivots use normalized bottom-left coordinates. Meter dimensions are placement proposals, not Unity-tested measurements.

## Sheets

- decal_sheet.png: 512x512, 16 ground decals: three grasses, two flowers, three dirt patches, straight/curved paths, two pebble groups, two scorches, two craters. XZ ground orientation, pivot (0.5, 0.5).
- upright_sheet.png: 512x512, 10 billboards: three bushes, two tall grasses, three weapon props, fallen shield, signpost. Pivot (0.5, 0). Shield is a flat face-up ellipse depicted by a billboard; do not project/flatten it a second time.
- terrain_sheet.png: 1024x256, five 256x128 billboards: intact/cracked/collapsed earth wall, rubble, fallen tree. Pivot (0.5, 0). All wall states share a 5.6x2.8 m transparent quad and bottom baseline; keep transform unchanged during swaps.

## Intended extents in meters

| ID | Width X | Depth Z | Height Y |
|---|---:|---:|---:|
| grass_01 | 0.65 | 0.45 | 0 |
| grass_02 | 0.85 | 0.5 | 0 |
| grass_03 | 0.5 | 0.4 | 0 |
| flowers_01 | 0.7 | 0.5 | 0 |
| flowers_02 | 0.8 | 0.55 | 0 |
| dirt_01 | 2.4 | 1.7 | 0 |
| dirt_02 | 1.8 | 1.3 | 0 |
| dirt_03 | 3 | 2 | 0 |
| path_straight | 5 | 5 | 0 |
| path_curve | 5 | 5 | 0 |
| pebbles_01 | 0.9 | 0.6 | 0 |
| pebbles_02 | 1.2 | 0.8 | 0 |
| scorch_01 | 2.8 | 2.1 | 0 |
| scorch_02 | 4 | 2.6 | 0 |
| crater_01 | 2.2 | 1.8 | 0 |
| crater_02 | 3.6 | 2.8 | 0 |
| bush_01 | 1.1 | 0.7 | 0.5 |
| bush_02 | 1.4 | 0.8 | 0.65 |
| bush_03 | 0.9 | 0.6 | 0.4 |
| tallgrass_01 | 0.7 | 0.5 | 0.9 |
| tallgrass_02 | 0.9 | 0.6 | 1.1 |
| weapon_01 | 0.45 | 0.25 | 0.9 |
| weapon_02 | 0.5 | 0.25 | 0.55 |
| weapon_03 | 0.65 | 0.3 | 0.75 |
| shield | 0.7 | 0.55 | 0.3 |
| signpost | 0.8 | 0.3 | 1.15 |
| earthwall_intact | 5 | 1.5 | 1.3 |
| earthwall_cracked | 5 | 1.5 | 1.3 |
| earthwall_collapsed | 5 | 1.5 | 0.65 |
| earthwall_rubble | 5 | 2 | 0.25 |
| fallen_tree | 5 | 2 | 0.45 |

Use manifest billboard_size_m for full transparent quads. size_m describes intended visible extents. Paths are full 5x5 m tiles; normalized ground PNGs need ground projection once.

## Verification performed

- Ran build_review.py, validate_delivery.py, verify_review.cjs successfully.
- 31 atlas crops match individual PNG pixels; checked rect bounds, pixel rects, pivots, alpha, power-of-two sheets and wall registration.
- Straight north/south and curve north/east share matching four-pixel edge bands.
- 76 embedded images decode; 31 four-times views; six before/after control assertions; no browser errors or 390px mobile page overflow.
- Visually inspected final shield and terrain sheet: flat elliptical shield, fissured cracked wall, central breach and two stubs in collapsed wall, low scattered rubble. Silhouettes change intentionally; canvas and baseline remain common.
- Regenerated static 28px goblin diagnostics. These do not prove moving-game readability.
- Current Game file set and hashes match inherited K-068 snapshot: 1065 files. No Unity run or Git writes.

## K-069 fixes

Replaced source/shield.png via built-in image_gen edit, preserving wood/iron style while changing pose to flat face-up. Prompt and generated path are recorded in generation.json. Rebuilt sprite, atlas, review composites and HTML. Validator now resolves inherited Game paths relative to the current worktree and verifies actual rect/pivot pixels.

## Open questions / leftovers

Claude owns engine integration. User must check actual lighting, depth sorting, scale, moving-enemy readability, filtering/mipmaps and collision/cover behavior in Unity. Keep tall vegetation outside lanes and scorch/craters event-driven. Visual passability flags do not implement collisions. No blocking file-level questions.
