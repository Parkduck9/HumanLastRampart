# K-113 — Beam focus and lamp brightness

## Do not — established before drawing
1. Do not wash the field in grey haze or bloom: keep the unlit field dark and terrain readable inside the beam.
2. Do not invent a fantasy fortress or camera: preserve the supplied battle and map screenshot layouts, scale, walls and units.
3. Do not blur sprite outlines, mix pixel densities, or replace faces with glowing blobs; keep readable eyes and mouths.
4. Do not use neon purple/teal or glossy materials: use restrained warm ivory light, earthy goblin colours and directional shadows.
5. Do not obscure focus geometry with decorative rays: make 30/60/90-degree coverage and the inverse intensity comparison explicit.

Research read before drawing: [Sprite Fusion — How to Fix AI-Generated Pixel Art](https://www.spritefusion.com/pixel-snapper/how-to-fix-ai-pixel-art) describes inconsistent grids and blurred edges; [Why Does AI Art Look Like That?](https://www.theatlantic.com/technology/archive/2024/08/why-does-all-ai-art-look-same/679488/) discusses recurring generic aesthetics. Card-specific constraints above combine this research with the approved brief.

## Completed work
- Created six static concept paint-overs based on the actual game screenshots: three north-facing battle focus variants, two north-facing map wedges, and a high/low brightness comparison.
- Narrow focus shows restored goblin skin colours and suppressed glowing eyes inside strong light. Normal/wide retain transformation. Wide received a targeted additional edit to reduce its illumination and restore dark goblin tint.
- Used the built-in image_gen tool, not the API/CLI fallback. Each final prompt is recorded in the corresponding `manifest.json` asset entry. Source imagery and the external Alan Wake / Octopath mood references were inspected before production.
- Normalized generated 1672x941 images to the requested 1920x1080 using nearest-neighbor; supplied separate 7680x4320 nearest-neighbor previews.
- Created a Korean, self-contained offline review page with original screenshots beside each paint-over, manual focus selection, and 1.8-second automatic comparison. The page embeds its eight unique images once; no server, CDN, or external assets are required.

## Files changed / added
Only the following card-owned paths were written:
- `CARD_RESULT.md`
- `Docs/References/codex_k113_v1/01_battle_narrow_30.png`
- `Docs/References/codex_k113_v1/02_battle_normal_60.png`
- `Docs/References/codex_k113_v1/03_battle_wide_90.png`
- `Docs/References/codex_k113_v1/04_map_narrow_30.png`
- `Docs/References/codex_k113_v1/05_map_wide_90.png`
- `Docs/References/codex_k113_v1/06_brightness_high_low.png`
- Six matching `*_4x.png` preview files in that directory.
- `Docs/References/codex_k113_v1/README.md` (Korean), `manifest.json`, `validation.json`, `review_validation.json`, `review_desktop.png`, `review_mobile.png`.
- `_HTML/6_그림_화면/codex_K-113_빛모으기_참고_v1.html`

No Game/, shared docs, live logs, existing HTML indexes, or source reference images were written. Unity was not opened or run. No Git writes were performed; Claude retains commit/integration responsibility.

## Verified
- All six deliverables decode at 1920x1080; all six previews decode at 7680x4320.
- For each preview, 147 sampled native-to-preview pixel comparisons match the expected nearest-neighbor 4x replication (`validation.json`).
- Scanned every alpha sample in each of the six final scene images through browser canvas: minimum alpha is 255 for all six. These are intentionally opaque full-scene concepts.
- Opened the review using Chrome/Playwright over file://. All 13 displayed image elements decoded successfully with 1920x1080 dimensions and embedded PNG URLs.
- Manual 90-degree selection, automatic transition to 30 degrees, and stop control passed. No browser script errors; no document horizontal overflow at 390x844. Desktop viewport: 1500x1000.
- Inspected generated imagery and desktop/mobile review screenshots visually: beam coverage hierarchy, wide dimming, narrow transformation stripping, enemy population contrast, readable dark exterior, and crisp map sector boundaries. No obvious isolated stray marks were observed at review scale; this is visual inspection, not proof of pixel-perfect sprite topology.
- Foot line, cell size, sprite pivots, animation frame ordering and transparent sprite cleanup are not applicable: this card contains scene concepts, not sprite sheets. The review animation switches still comparison images.
- One initial browser invocation failed because PowerShell stdin replaced Korean path literals. Retried with filesystem-discovered Unicode names; the final verification passed. No Unity/runtime verification was performed.

## Open questions / leftovers for Claude
- These are AI paint-overs, not pixel-identical screenshot overlays. Small soldiers, map markers, existing UI text and positions may drift. Use the supplied original screenshots for integration geometry and UI, not generated details.
- 30/60/90 degrees and 2/1/0.67 multipliers are design values, not measured photometry. Battle images are perspective illustrations. Map arcs illustrate coverage and are not authoritative coordinates.
- Brightness comparison intends the same 60-degree focus on both sides; the visibly lit footprint shrinks on the low side due to falloff. Do not infer a focus change from that footprint.
- Enemy counts are illustrative. Transformation-strip thresholds, attraction counts, weakening amounts, and final in-engine lighting remain Claude's integration/tuning work under the approved design.
- No pending asset-production blocker. No code or runtime asset wiring is included, as requested.
