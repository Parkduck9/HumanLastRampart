# K-102 — Beacon concept assets

Implemented the central courtyard beacon as asset-only deliverables. No Game files, collaboration logs, existing review pages, Git state, or Unity projects were modified or run.

## Deliverables

- `Docs/References/codex_k102_v1/`: dead/ruined, warming and lit tower sprites; combined three-state sheet; separate six-frame flame loops for warming and lit; separate additive light-pillar sprite; native sheets, exact nearest-neighbor 3x game sheets and 4x previews; three-state 48px/96px icons; two 1920x1080 concept mockups; Korean README; manifest; generation source/prompt; reproducible Node build and verification scripts; verification evidence.
- `_HTML/6_그림_화면/codex_K-102_등불탑_v1.html`: self-contained Korean review, animated flame layers, pause/background controls, existing in-game prop/wall and approved hero comparisons, full pillar composite, night battle and day courtyard mockups.
- `CARD_RESULT.md`: this handoff.

## Implementation notes

Built-in image_gen generated one transparent atlas using the existing battle wall and approved prep-yard as references. Processing extracted and aligned the artwork, removed isolated pixels from tower/flame sprites, reduced color steps, and created anchored six-frame flame deformation loops. The hero was reused without redesign. No existing in-game beacon sheet exists, so no existing filename/frame contract was replaced.

Tower cells are 96x128 native / 288x384 game, with foot anchors (48,120) / (144,360). All tower states have the same centered 74x98 silhouette bounds. Flames use the same cell placement and fixed bowl anchor (48,42), 8fps, six frames left-to-right. The separate pillar uses 96x256 / 288x768 cells and anchor (48,248) / (144,744), aligned to the bowl. Its canvas offset relative to tower is (0,-206) native / (0,-618) game. Use straight-alpha SrcAlpha One additive blending. Manifest coordinates are top-left pixels.

Night mockup multiplies the existing battle capture's RGB by exactly 0.65 before compositing the tower and additive pillar. Day mockup crops the approved yard's central 1920x1080 region. Both are concept composites, not Unity captures.

## Verification

- Asset dimensions, alpha, centered tower foot lines, stable flame bottom anchors, absence of disconnected tower/flame pixels, and exact nearest-neighbor native-to-game 3x scaling checked by `verify.cjs`; results in `verification.json`.
- Actual headless Edge loaded the local self-contained HTML. Embedded images and both flame CSS sheets decoded, both loops advanced, pause worked, no page errors, and no horizontal overflow at 390px viewport; evidence in `review_validation.json`, `review_desktop.png`, `review_mobile.png`.
- Visually inspected enlarged tower/hero comparison, complete pillar composite, both mockups and desktop/mobile review captures. Corrected a color-quantization overflow found during inspection.
- Unity, Play, import, runtime lighting, and game integration were intentionally not tested. No Git writes were performed; Claude commits and integrates.

## Open questions / leftovers

No asset-production blocker remains. Claude must choose the actual courtyard world position, renderer sorting/occlusion, additive material and light intensity, then verify through the normal Unity workflow. The dead-to-lit concept visually restores the damaged bowl; that narrative detail remains a concept choice, not a gameplay requirement. The pillar's detached embers are intentional effect pixels. The design document remains a proposal; this card does not approve or implement its unrelated gameplay systems.
