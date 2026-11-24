# K-114 — concrete anti-slop constraints (written before image generation)

- Do not replace the game's horizontal crenellated wall, rectangular courtyard, or ochre preparation-yard paving with an invented symmetrical fantasy castle.
- Do not wash the night in grey fog, teal/purple neon, or global bloom. Keep dark stone readable through localized amber lamps and a single directional beacon.
- Do not blur sprite contours or mix glossy airbrushed figures with chunky pixels. Retain large heads, short sturdy bodies, big boots, and restrained material shading.
- Do not omit Lumen's mouth, blue eyes, cream cape, or navy cadet uniform; do not turn the workers into slim generic adventurers.
- Do not depict disconnected stairs, floating feet, impossible capstan grips, or ornamental machinery without a clear connection to the beacon shaft.

Research read before generation: Runway, https://runway.com/resources/why-do-ai-images-look-weird (anatomy, incomplete objects, excessive gloss); Unreal Engine / Square Enix developer interview, https://www.unrealengine.com/spotlights/octopath-traveler-s-hd-2d-art-style-and-story-make-for-a-jrpg-dream-come-true (pixel art, point lighting and shadow).

## What was made

Completed three static reference concepts using the built-in image_gen tool (no CLI fallback): night courtyard with Lumen descending the right-hand wall stairs and a northbound beacon; cutaway/close view of burly workers pushing a capstan while Lumen gives an order with a visible open mouth; daytime repair of the same courtyard beacon with scaffolding.

Read the actual battle, map and preparation-yard screenshots. Used `codex_k089_v1/prep_yard.png`, also referenced by K-102, to recover the unobscured courtyard layout behind the repair dialog. Preserved its left damaged masonry, barracks arch, middle workshop, right green shop awning, right-hand stairs, and paved yard. Read the requested external Octopath mood images without copying them into this worktree.

## Files changed (new files only)

- `Docs/References/codex_k114_v1/night_courtyard_1920x1080.png`
- `Docs/References/codex_k114_v1/night_capstan_1920x1080.png`
- `Docs/References/codex_k114_v1/day_prep_1920x1080.png`
- The corresponding three `*_generated.png` source files and three `*_detail_4x.png` detail previews in the same folder.
- `Docs/References/codex_k114_v1/README.md` (Korean), `manifest.json`, `prompts.json`, `verification.json`.
- `_HTML/6_그림_화면/codex_K-114_탑과일꾼_참고_v1.html` (Korean, self-contained comparison page).
- `CARD_RESULT.md`.

Full final prompts are saved verbatim in `prompts.json`. Generator sources were 1672x941; final scenes were normalized to 1920x1080 with nearest-neighbor sampling (less than 0.1% aspect adjustment). The 4x previews enlarge selected 480x270 crops, not entire scenes; crop coordinates are in the manifest. This concept-only card has no sprite cells, pivots, motion frames, or replacement game sheets.

## Verification actually performed

- Decoded all nine delivered/source/detail PNG files successfully.
- Confirmed all three final scenes are exactly 1920x1080 and fully opaque (alpha min/max 255/255). The detail previews are also 1920x1080.
- Inspected all generated scenes and the saved final detail crops visually: stair contact, hero/worker separation, capstan grip, crown lighting, and supported repair scaffold are readable.
- Decoded all 13 embedded review-page image instances successfully; all four internal navigation anchors resolve. No external image, font, stylesheet, or script dependency is required.
- Transparent-background stray pixels, sprite foot baselines, frame order and frame jitter are not applicable to these opaque static full-scene concepts. No claim of pixel-perfect 73px sprite compliance is made.
- Browser inventory returned no available browsers. Actual browser rendering, responsive layout, and interactive image opening were NOT tested. Static embedded-image decoding is not represented as a browser test.
- Unity was not opened or run. No Game/, collaboration logs, existing documentation, or Git history was modified. No Git write operations were performed.

## Open questions / leftovers for Claude

- These are review concepts, not integrated game captures. No game integration is included or requested.
- The tower base is wider/larger than K-102 to accommodate the worker chamber. Final scale, collision, stair connection and camera framing require review during integration.
- The close view intentionally exposes the mechanism; its masonry and machine details are interpretive rather than a mechanically exact match to the wide shots. The wide shots show a drum/winch-like base, while the close shot explains the requested upright capstan. Resolve that construction detail before production assets.
- Generated art retains some soft environment texture and nonuniform pixel density. It is not a clean native-grid sprite sheet; do not extract these figures as production sprites.
- Rotation is implied by the close-view arc and beam direction. No animation or six-second timing was implemented; this card requests static paint-overs only.
- Open the self-contained HTML locally for final browser/mobile visual review. Commit and game wiring remain Claude's responsibility.
