# K-118 — production result

## Card-specific do-not list (written before drawing)
- Do not introduce airbrushed gradients, glossy plaster, neon teal/purple, or broad bloom; keep matte earth materials and discrete pixel clusters.
- Do not mix pixel densities or use blurred/antialiased silhouettes; use 32 pixels per metre and nearest-neighbour previews.
- Do not invent illegible lettering or decorative symbol noise; use distinct hammer-and-stone, crossed-spears, spiked-trap, and pouch silhouettes.
- Do not enlarge the low buildings or obscure the 12 m beacon, tower ring, north road, or stairs.
- Do not repaint the camera/layout for mockups or wash the night in grey fog; retain the actual screenshot and keep window lighting warm and local.

Research read before drawing: https://spritemend.com/blog/ai-pixel-art-cleanup ; https://pixelartworkshop.com/guides/pixel-art-vs-ai-generated ; https://the-pixel.art/articles/seamless-tileable-pixel-art-textures/ . These are practical visual checks, not proof of image provenance.

## Completed work
Created the K-118 asset package under `Docs/References/codex_k118_v1/` and the self-contained Korean review page `_HTML/6_그림_화면/codex_K-118_안마당건물_v1.html`.

- Seven opaque 256x256 tiling materials: plaster/timber framing, rough stone foundation, plank wall, clay tiles, wood shingles, slate, thatch.
- Four functional front facades, each with door/frame, windows and distinct repair / crossed-spears / spiked-trap / pouch sign. Each is 144x64 native pixels, with day/night states and a separate window-only emission mask.
- Eight transparent props: smoke-free capped chimney, bell belfry, fence, hay, woodpile, front-view well, stable trough, beacon-order banner.
- Sixteen exact nearest-neighbor 3x game exports for facade states and props; native assets remain at 32 px/m, enlarged game exports use 96 px/m. Character-cell contracts are not applicable to this static building card.
- 4x previews, 3x3 material repeats, day/night screenshot composites, manifest, Korean README, saved generation sources/prompts and reproducible offline packaging/check scripts.

The package contains 88 PNG files: 7 textures, 12 facade/state/mask images, 8 props, 16 game exports, 32 previews/masks, 2 mockups and 11 generation sources. All new project files are within the two authorized output paths plus this root result file. No Game/, collaboration records, existing HTML index or other project files were edited. No Unity launch, Play, Git write, commit or branch operation was performed.

## Reference and production decisions
Read the courtyard building dimensions and placement from `BattleCourtyardBuildings.cs` without editing it. Inspected actual courtyard day/top-down/night screenshots, the actual prep yard image at `Game/Assets/_Project/Meta/Resources/PrepYard/prep_yard.png`, K-115 roof and existing wall stone. Preserved the ten existing building footprints/heights in the manifest; the tallest existing building roof is 2.5 m. Chimney/belfry placement must remain at or below 3 m. Materials have a matte, reduced earth/stone palette; roof pattern density is normalized for small buildings.

Used the built-in `image_gen.imagegen` tool for generation and screenshot edits, not the API/CLI fallback. Final prompts and original tool output paths are in `generation.json`; local copies are in `sources/`. Packaging performs nearest-neighbor size normalization, limited-palette/stone normalization, periodic edge conditioning, alpha cleanup, tiny-component removal, window-state masking and protected screenshot compositing.

The review page embeds every image and script and has original/current screenshots beside the proposed mockups, automatic/manual facade day/night state stepping, pause/resume, material-repeat inspection and prop previews. User-facing text is Korean.

## Verification actually performed
- `verify.ps1`: 23 core native images passed expected opaque/binary alpha, nonempty content and padding checks. Static facade/prop artwork ends at height minus 4; the baseline is the lower edge immediately after that row, with three transparent rows below.
- Transparent art has no 8-connected component smaller than three pixels and no opaque pixels touching the canvas edge. Transparent RGB was cleared to zero to prevent white fringes. Intentional fine emission-mask components are excluded from the grounded-art component/baseline test.
- All seven materials passed matching opposite-edge checks and were exported with 3x3 repeat previews.
- All 16 game exports passed pixel-for-pixel exact nearest-neighbor 3x checks. Manifest dimensions were checked independently against PNG headers for all 23 core assets and four emission masks, including game export dimensions.
- Night/day facade differences match the emission masks exactly and occur only within the two bounded window regions; doors and icon signs remain unchanged.
- Both 1920x1080 mockups passed exact pixel comparison outside their saved masks: zero protected pixels changed. Thus the tower, UI and unrelated scene areas are original screenshot pixels.
- `review_check.js`: all 38 embedded PNG references passed PNG IDAT decompression; no external image/script/style resources are required. Node VM checks using minimal DOM stubs passed manual day/night, automatic stepping, pause and resume behavior.
- Visually inspected generated sources, final day/night composites, representative materials, every functional facade and representative 4x prop previews. Technical verification covers all core images; this is not a claim that every delivered PNG received individual visual review.

Machine-readable records: `verification.json` and `review_verification.json`.

## Open questions / integration leftovers
1. Claude must add meter-based UVs: existing courtyard box mesh UVs are zero. One 256px material covers 8x8 m; do not stretch a single tile over an entire building. Mount functional facades on the courtyard-facing face and honor the door-center pivot in the manifest. Replace existing chimney/belfry blockouts rather than stacking duplicate geometry. Static front-view props are not full 3D replacements for every viewing angle.
2. Mockups are generated direction/placement composites, not renders of the exact delivered textures on the game meshes. The day composite clips proposed details to the existing blockout mask; use the standalone prop PNGs as the silhouette source.
3. Screenshot 279 predates courtyard buildings. Its night composite shows illustrative far-perimeter placement and warm window intensity, not a validated game projection. Actual occlusion, scale/readability from all battle cameras, lighting and tower dominance require integration and user captures.
4. No browser surface was available: `cua.createBrowserTab('iab', ...)` reported unavailable and `cua.getState()` returned `apps=[]`, `browsers=[]`. Browser layout, actual image.decode behavior, real clicks and responsive screenshots were NOT verified. The static PNG/manifest/script checks above passed; the browser limitation is explicitly recorded in the Korean README and review verification JSON.
5. Unity was intentionally not opened. Import settings, actual runtime UVs, emission/material setup and Play verification remain Claude/user work. Wood shingles and thatch are optional supplied materials; no existing roof classification was changed.
