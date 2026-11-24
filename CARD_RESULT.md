# K-130 — Anti-slop constraints (written before production)

1. Do not replace the existing lamp icon, Lumen's cadet identity, or the four-sided beacon fortress with generic fantasy stock imagery.
2. Do not use airbrushed gradients, glossy materials, neon purple/teal, or bloom that turns black fog into grey haze. Use clustered pixels and one warm beacon source, with the hero's small lamp as a local accent.
3. Do not mix arbitrary pixel densities or smooth scaling; preserve stepped silhouettes, dark outlines, short sturdy figures and restrained material shades.
4. Do not generate the final Korean lettering. Composite verified pixel lettering for every syllable: 빛 을 잇 는 자 들; keep it and all essential subjects above the bottom-third Store overlay.
5. Do not add malformed hands, unreadable faces, ornamental clutter, gore, UI, or viewer-facing weapons.

Pre-production reading: https://developer.mozilla.org/en-US/docs/Games/Techniques/Crisp_pixel_art_look and https://learn.microsoft.com/en-us/gaming/game-publishing/concepts/store-listing . The first supports nearest-neighbor scaling/pixel density; the second supports Store artwork placement constraints. Additional anti-slop constraints come directly from the card brief.

## Delivery

Completed the K-130 version 1.0 Store artwork asset pack for **빛을 잇는 자들 / Lightbearers**. Used the built-in image_gen tool for three separately composed illustrations and targeted lighting cleanup. Final exact Korean/English titles, palette cleanup, nearest-neighbor scaling, existing-icon derivatives, sRGB embedding and packaging were done with Pillow. Actual generation prompts are in `prompts.json`. Temporary build/check scripts were removed; no runtime code is delivered.

### Files changed

- `Docs/References/codex_k130_v1/`: 57 delivery files, including 52 PNGs, Korean README, manifest, prompts, asset verification and browser verification. The manifest enumerates every file, purpose and PNG size; PNGs and other supporting files have hashes. Its self-entry intentionally omits a recursive hash/size.
- `_HTML/6_그림_화면/codex_K-130_스토어그림_v1.html`: Korean self-contained review with all 25 submission assets, an existing-icon comparison, six-syllable title proof, bottom-third overlay toggle and full-resolution dialog with fit/restore controls.
- `CARD_RESULT.md`: this report.

The 25 submission PNGs are:

1. Poster 1440×2160 and 720×1080.
2. Square box cover 2160×2160 and 1080×1080.
3. Text-free super hero art 3840×2160 and 1920×1080.
4. Existing K-124 beacon-lantern icon cleaned/resampled for the 300×300 app tile.
5. Five MSIX logo families at scale-100 and scale-200: Square44x44Logo, Square150x150Logo, Wide310x150Logo, StoreLogo and SplashScreen.
6. Eight transparent unplated absolute-size icons: 16, 24, 32, 48, 64, 96, 256 and 512px.

Additional PNGs comprise final generated sources, logical-resolution artwork, exact 4× previews, syllable proof, icon comparison and actual browser screenshots. All outputs stay in the authorized card paths.

### Direction and interpretation

Read the story and inspected the selected original hero v6, hero/soldier references, K-124 lamp icon and foreground, actual menu fortress layer, actual game wall/map screenshots, beacon art and K-128/K-129 wallhand ogre. K-120/K-122 in this checkout are courtyard/dressing assets rather than menu parallax, so the actual in-game menu fortress layer was used as the architecture reference. Read-only reference hashes are recorded in the manifest.

Lumen retains silver hair, short sturdy cadet proportions, black/gold uniform, dark outlines, large boots and a small lamp. The fortress uses four straight walls, low red roofs and a single conical-roof beacon. The wide art depicts enemies approaching all four sides and contains no text, UI, gore or viewer-facing weapons. These are promotional illustrations, not screenshots or replacement sprite sheets.

Titles use the exact Unicode text `빛을 잇는 자들` and `LIGHTBEARERS`, rasterized as binary pixel lettering from locally installed Malgun Gothic Bold; no font is redistributed. Every syllable was inspected enlarged. Hero, beacon and titles survive the bottom-third cover; subordinate dark enemy silhouettes and shadowed masonry may be covered. The title's actual underlying background pixels were measured before compositing, rather than assuming contrast from a nominal swatch.

`targetsize` is an absolute-pixel MSIX qualifier and is not combined with `scale`. Requested 16/24/32/48/256 sizes plus their 2× counterparts are delivered as the union 16/24/32/48/64/96/256/512. Regular logo families separately have both scale-100 and scale-200.

### Verification performed

- Checked all 52 PNG dimensions and embedded sRGB profiles. All seven Store submission images are opaque; package icon transparency is allowed and preserved. Splash/wide package images use opaque dark backgrounds.
- Verified large artwork equals exact nearest-neighbor 4× logical originals; small artwork uses 2× originals. No smooth interpolation or dithering was used.
- Measured minimum title-to-actual-background contrast **16.5138:1**, exceeding 4.5:1. Verified title bounds are in the upper two-thirds.
- Inspected final imagery, character silhouettes/features, title syllables, package icon boundaries and overlay views visually. All 14 transparent MSIX images have a single 8-connected alpha component, with no isolated alpha pixels. Opaque artwork was visually checked; no claim is made of automated semantic stray-pixel detection.
- Headless Chrome loaded all **27 embedded review images**. No page errors or external network requests. Overlay toggle, 1440×2160 real-size dialog, fit and real-size restoration passed. No horizontal overflow at 1440px desktop or 390px mobile. Actual screenshots are in `previews/review_*.png`.
- Git status showed no tracked-file modifications and only card outputs plus the external runner's `CARD_CODEX_LOG.jsonl`. Did not edit that runner log. No Git write operation or Unity launch was performed. No changes to Game/, existing docs or collab/live records.
- Static Store artwork has no animation frames or sprite foot baseline to validate; the gameplay sheet contract is not applicable.

### Open questions / handoff

No artwork-production dependency remains. Claude must assign package/Store slots, integrate the assets, inspect final Partner Center cropping and Windows shell rendering, and commit the authorized files. No MSIX build, Unity Play, Store upload, platform validation or real-device test was performed. Final Store acceptance is not claimed.
