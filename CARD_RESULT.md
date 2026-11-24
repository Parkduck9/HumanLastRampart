# K-106 — Beacon beam assets

Completed assets only. No Game/ edits, Unity launch, Git writes, collaboration-log changes, or changes to existing reference assets.

## Deliverables

- `Docs/References/codex_k106_v1/`: 60 manifest-listed PNGs plus two browser review screenshots; Korean README; manifest; source hashes; generation/verification scripts; verification results.
- `native/beam_{40,60,80}.png`: three warm, soft-edged, north-facing ground wedges. Native cell 384x256, pivot (192,248), radius 232px representing approximately 60m. Full quad approximately 99.31x66.21m. Geometry is top-down, allowing runtime camera projection.
- `native/shimmer_{40,60,80}.png`: six distinct looping frames per width, 6fps, identical cells and pivots. Dust islands are intentional 2x2 particles. Animation changes brightness periodically without moving the anchor.
- `native/beam_head_north.png`: one rotatable lens glow, 96x128, pivot (48,42), matching the K-102 bowl. Game sheets are exact 3x nearest-neighbor copies, including the lens sheet (288x384).
- `map/`: three semi-transparent 96x96 wedges, pivot (48,90), radius 68px.
- `ui/`: 48px and 64px Korean north/east/south/west button sheets and compass art in inactive/active/rotating states, three-width indicator sheets, and transparent-center nine-slice frames with 8px borders. UI sizes are delivered directly rather than tripled.
- `previews/`: 4x nearest-neighbor previews of every native sheet, map wedge, and UI sheet.
- `mockup_north_high_1920x1080.png`: composite night battle facing north at high brightness. Reuses K-102 tower/flame, K-103 fog edge, K-070 goblins and the existing battle reference. Enemies inside the beam are visibly warmer/brighter than those outside. This is not a Unity capture.
- `_HTML/6_그림_화면/codex_K-106_등대빛줄기_v1.html`: Korean, self-contained, offline review page. Includes all three shimmer animations, pause, width selection, one visible map wedge with a three-second direction transition, lens direction illustration, original in-game compass/movement art alongside new art, and the mockup.

## Verification performed

Ran `node Docs/References/codex_k106_v1/build.cjs` and `node Docs/References/codex_k106_v1/verify.cjs` successfully.

- All 60 manifest-listed PNG dimensions and cell/frame layouts match; all are RGBA; fully transparent pixels have zero RGB.
- Every game sheet exactly matches a 3x nearest-neighbor expansion of its native sheet.
- Ground/shimmer frames retain identical pivots and contain no pixels outside their declared angular/radial support.
- Each width has six distinct shimmer frames. Ground cones are single connected components; shimmer components contain at least four pixels. No unintended single-pixel islands were found.
- Foot-baseline testing is not applicable: no new character sheets were made. Lens and ground anchors are explicitly documented rather than assigning character-foot pivots.
- SHA-256 hashes of every reused source remain unchanged.
- Headless Edge opened the review page through a local file URL. All embedded images and animation backgrounds loaded. Animation advance, pause, all width selections, rotation status, control lock during rotation, still rotating at 2.7 seconds and completion by 3.1 seconds passed.
- No JavaScript page errors or horizontal overflow at 390px. Saved and visually inspected full-page desktop/mobile review screenshots; also inspected the mockup and button enlargement.

## Integration leftovers / open questions

No blocking asset questions. Claude must implement/import the assets and validate runtime ground-plane axes, yaw sign, lens billboard direction mapping, world scale/camera projection, depth placement and additive material (`SrcAlpha One`, straight alpha). Ground pivot uses Unity normalized (0.5,0.03125); lens pivot uses (0.5,0.671875).

The 60m range is specified by texture-to-world geometry, not measured from the illustrative mockup. Runtime enemy illumination/weakening and the three-second rotation mechanic have not been tested in Unity. The review uses illustrative lens rotation; mapping the ground yaw to the battle camera remains an integration task. Active/inactive/rotating UI PNGs are static states; only the dust is a six-frame animation sheet.

Generation uses analytic masks/pixel drawing and existing approved references. Supporting scripts read the existing K-102 PNG utility and installed Playwright dependency; they only write the designated K-106 output directory and review HTML.
