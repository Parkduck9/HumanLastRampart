# K-092 result

Completed specular/smoothness masks for all 41 source color sheets (142 frames) in the requested Characters and Cannon directories, including ladder props, projectiles, hauling crew, cannon components, and effects. Existing normal maps were treated as inputs, not as color sheets. No Unity launch, Game edits, collaboration-log edits, or Git writes were performed.

## Deliverables

- `Docs/References/codex_k092_v1/Characters/**/<sheet>_spec.png`: 34 source-sized RGBA masks.
- `Docs/References/codex_k092_v1/Cannon/<sheet>_spec.png`: 7 source-sized RGBA masks.
- `Docs/References/codex_k092_v1/native/**`: 41 lossless working-resolution masks. The 19 exact D-081 sources have 96x128 native cells and exact 3x game masks. The other 22 preserve their existing source resolution; no lossless lower-resolution source exists in these inputs.
- `Docs/References/codex_k092_v1/previews/**`: 41 full-sheet 4x nearest-neighbor previews, including every frame.
- `manifest.json`, Korean `README.md`, source/material contact images, source inventory, build/review templates and verification scripts, three verification JSON reports, and desktop/mobile review screenshots, all under the same output directory.
- `_HTML/5_그림_전장/codex_K-092_반사마스크_v1.html`: Korean, self-contained offline review page with an animated source/mask gallery for all 45 motion rows, selectable diffuse versus diffuse-plus-Blinn-Phong rendering, moving point light, independent animation/light controls, frame scrubbing, and explicit normal-map availability labels.

## Encoding and material decisions

R = specular strength; G = smoothness; B = 0; A = source alpha byte-for-byte. Values: matte 13/26, leather 77/64, rough iron 242/140, polished metal 255/191, gold trim 255/217, effects 0/0. Wood, skin, hair, cloth, bone, and dark outlines stay matte. Gun barrels have frame-specific annotations; wooden stocks and clubs remain matte. Current cannon art depicts iron and wood, so no bronze recoloring was invented. Material assignment uses source colors and positional rules; it is not a claim of exhaustive manual semantic annotation.

Masks use the source stem plus `_spec`, retaining the source-relative folder structure inside the deliverable. The existing `ladder_goblin_carry_normal.png` is an input naming exception; its color sheet and delivered mask use `ladder_goblin_carry_run4`.

## Verified

- `validation.json`: all 41 sheets passed exact size/alpha matching, zero blue, zero RGB outside source alpha, native-to-game nearest-neighbor roundtrip, and full-sheet 4x preview dimensions. All 142 frame alpha bounds and alpha-cutout bottoms match their source frames. No new alpha pixels were introduced. Source hashes remained unchanged during generation.
- `material_validation.json`: 15 independent representative probes passed for hair, skin, cloth, wooden cannon carriage/club, gun barrel, gold trim, cannon metal, and ogre buckle.
- `review_validation.json`: local-file headless Chrome successfully loaded all 85 embedded images, exercised all 41 sheets and 142 frames, found zero preview alpha mismatches outside the light marker, confirmed identical images with specular gain zero, and confirmed animation advances. All 45 motion cards exist. No page errors or failed requests; no mobile horizontal overflow.
- Opened and visually inspected source/material contact images and desktop/mobile review captures. These are browser captures, not Unity screenshots.
- Read-only Git status/diff showed no tracked-file changes. Deliverables are untracked for Claude to review and commit. An unrelated `CARD_CODEX_LOG.jsonl` also appeared in status and was not edited or removed.

## Integration notes / leftovers

1. K-081 output is absent from this worktree. Only three existing, dimension-matched normal maps are used in the review (`hero_walk_run_grid`, `ogre_static`, `wizard_back_static`). Five existing normal maps have incompatible dimensions and are explicitly skipped. Other sheets use flat billboard normals. Recheck with K-081 after integration; no new normals were generated.
2. The supplied Game art mixes D-081 and legacy layouts: hero/wizard/ogre remain 256x384 cells, hauling crew 384x384, installed cannon/effects 256x256, hauling cannon 768x384, wheel 101x101, and ladder props 256x1536. Exact matching masks take precedence over changing these source layouts. All sizes, pivots, grid order, and per-frame bounds are recorded in the manifest. If those source sheets are replaced, regenerate matching masks.
3. Original low-alpha fringe pixels and original pose/foot movement are deliberately preserved to meet the exact-alpha/layout requirement. No source cleanup or frame realignment was attempted.
4. Claude must import as linear data (sRGB off, Point filtering, no compression/mipmaps, Clamp, ordinary texture type), connect R/G sampling to the shader, pair with matching K-081 normals, and arrange actual Unity lighting validation. Browser lighting is an illustrative comparison, not URP parity. User aesthetic approval and actual game lighting remain outstanding integration checks. No additional user decision was required to produce this asset package.
