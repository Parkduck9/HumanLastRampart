# K-116 — Courtyard assets

## Do not — established before drawing
1. Do not use soft airbrushed gradients, bloom, haze, or neon teal/purple accents on neutral daytime albedo.
2. Do not paint glossy plastic wood or stone, conflicting highlights, or baked directional cast shadows into reusable textures.
3. Do not use mirrored cobbles, aligned brick-like rows, or repeated prominent landmarks in the floor tile.
4. Do not stretch a perspective painting across box faces; give each crate face a square UV region and the barrel a dimensioned wrap.
5. Do not mix pixel densities, add speckled decorative noise, or alter the screenshots' camera, wall layout, UI, and surrounding scene in mockups.

Research read before drawing: https://www.gamedeveloper.com/art/how-devs-can-spot-ai-generated-3d-models (baked lighting and projected textures); https://pixeli.pebbly.space/knowledge/rules/11-clusters-and-noise (readable pixel clusters and avoiding noise). These are production constraints, not a claim that visual clues prove AI provenance.

## Completed work

Produced 39 PNG files under `Docs/References/codex_k116_v1/`, a Korean README, a machine-readable manifest, production/verification records, and the self-contained Korean review page `_HTML/6_그림_화면/codex_K-116_안마당_v1.html`.

- 512x512 neutral grey/taupe courtyard cobbles, periodic in both axes without mirrored duplication; native 32 px/m means a 16 m tile. Included a 1152x1152 / 36 m repeat inspection and a 4x preview.
- Four transparent ground decals: dirt, puddle, straw, cracked stones.
- A face atlas containing six independent 32x32 crate faces, two barrel caps, and a 100x32 barrel wrap. The crate is a 1 m cube. The barrel uses a cylinder plus separate disc caps; it is not a stretched box image.
- Transparent front-view weapon rack, handcart and arrow bundles, each with native, exact 3x game and exact 4x preview variants. The same variants are provided for the decals and face atlas.
- Width-tileable stone tread (64x10), riser (64x6), combined step atlas (64x16), and flank (128x64), all with 4x previews.
- Painted mockups over actual screenshots 62 and 57, normalized to the original 1920x1080 framing. UI and selected protected scene areas were restored from the originals. The overview stair was refined to show a substantially longer straight descent.
- A Korean offline review with original/mockup comparison sliders, side-by-side overview, animated repeat inspection, a rotating crate using the actual six atlas regions, transparent-prop previews and a dimensioned 20-step stair diagram.

The built-in `image_gen.imagegen` tool produced the source art and screenshot edits. Technical postprocessing used nearest-neighbor resizing, minimum-error overlap cuts for repetition, 32-color palette reduction, alpha/fringe cleanup, packing, and restoration of protected screenshot areas. No alternative model/API/CLI image generator was used. The normalized prompt set and source-generation paths are recorded in `generation.json`; final deliverables do not depend on those external paths.

## Files changed

Only new K-116 deliverables were written:

1. `CARD_RESULT.md` (this report).
2. `_HTML/6_그림_화면/codex_K-116_안마당_v1.html`.
3. `Docs/References/codex_k116_v1/`:
   - `courtyard_cobble_512.png`, `courtyard_cobble_preview4x.png`, `courtyard_repeat_36m.png`.
   - `props_box_atlas_{native,game,preview4x}.png`.
   - `barrel_side_100x32.png`, `barrel_side_preview4x.png`.
   - `{weapon_rack,cart,arrow_bundles,decal_dirt,decal_puddle,decal_straw,decal_cracked}_{native,game,preview4x}.png`.
   - `stair_tread_64x10.png`, `stair_tread_preview4x.png`, `stair_riser_64x6.png`, `stair_riser_preview4x.png`.
   - `stair_step_atlas_64x16.png`, `stair_step_atlas_preview4x.png`, `stair_flank_128x64.png`, `stair_flank_preview4x.png`.
   - `mockup_62.png`, `mockup_57.png`.
   - `README.md`, `manifest.json`, `generation.json`, `verification.json`, `dimensions.json`, `review_verification.json`, `scale_verification.json`.

No Game files, existing reference images, collaboration logs, or unrelated documents were written. Unity was neither opened nor run. No Git writes were performed. Git status/diff could not be collected because `git` was not on the command PATH; ownership/scope statements here describe the actual write operations, not a successful Git diff audit.

## Verification performed

- Decoded all 39 final PNGs and recorded their dimensions in `dimensions.json`.
- Floor: exactly 512x512; all 262,144 pixels opaque; 32 colors. Inspected the repeated field visually. Mean RGB wrap-neighbor deltas were X=17.942 and Y=14.786, versus mean interior deltas X=13.260 and Y=13.449. This is a measured periodic boundary check, not an assertion of identical opposite-edge pixels or zero perceptual repetition.
- Barrel, tread, riser, and flank: fully opaque; width-repeat boundary measurements are in `verification.json`. Vertical repeat is intentionally not specified for these assets.
- All seven native transparent sprites: zero partial-alpha pixels, zero opaque border pixels, no isolated one- or two-pixel components. Rack, cart and arrows each form one connected alpha component. Separate decal clusters are intentional (smallest retained cluster >=6 pixels).
- Standing props have last opaque row h-3, two transparent rows below, contact-line pivot at (w/2,h-2). Ground decals use center pivots. No character foot baseline or character motion sheet applies to these environment assets.
- All 16 whole-image comparisons for eight native files against their 3x and 4x variants passed with zero pixel mismatches.
- All nine face-atlas region rectangles fit within the native atlas. Barrel strip and step-atlas pixels were synchronized with their standalone textures before final delivery.
- Inspected floor/repeat, face atlas, weapon rack, straw, cart, both camera mockups, and the refined overview stair through the image viewer.
- The review page has 22 embedded PNG occurrences; all decoded successfully; no unresolved template markers remain. Its inline script passed Node syntax checking. Images, styling and interactions are embedded, so there are no external image/font/script dependencies.
- Browser rendering was NOT verified: the in-app browser was unavailable and the browser inventory was empty. This limitation is visible in the Korean review page and recorded in `review_verification.json`.

## Integration notes / leftovers

- Claude must import and wire the assets. Native sprites use 32 PPU and 3x variants use 96 PPU to preserve world scale. Floor and stone textures remain native; do not apply human sprite cell dimensions to them.
- Use Point sampling, no mipmaps, and preferably no texture compression. Atlas regions have no padding; clamp the full atlas and map exact per-face UVs. Repeat only the standalone textures or local face UV domains specified in the manifest.
- Barrel cap corners are opaque brown outside the circular cap and must lie outside the disc mesh UV footprint. The 100-pixel wrap circumference rounds a 1 m diameter by about 0.53%.
- Stair geometry reference: 4 m rise, 1.5 m width, 20 risers at 0.2 m, 0.3 m treads, 6 m run, flank coping about 0.6 m above treads. The PNGs are textures, not meshes or collision assets. Physical step dimensions are independent of the rounded 10/6 pixel texture depths.
- Mockups communicate placement and camera appearance. They are generated paintovers, not exact projections of the supplied textures; their painted stone scale, step counts, shadows and some nearby small details are illustrative. Use the manifest/dimension diagram for geometry. Preserve actual runtime camera and wall placement when integrating.
- Some repeated stone clusters remain inherent in a finite tile; use sparse, offset decals to break the 16 m motif. Actual 36 m readability, point-filter behavior at the battle camera, daytime/nighttime lighting, depth sorting, and stair traversal require Unity/user verification after integration.
- No blocking design questions were introduced. User visual acceptance and the browser/Unity checks above remain outstanding.
