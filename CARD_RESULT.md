# K-104 — Beacon Route Map

Completed the asset package and Korean standalone review page. No Game/ files, collaboration logs, existing reference files, or index pages were edited. Unity was not opened or run. No Git writes were performed; Claude owns committing and integration.

## Deliverables

- `Docs/References/codex_k104_v1/`: 19 delivery PNGs, `manifest.json`, Korean `README.md` and `PROMPTS.md`, `validation.json`, and `browser_verification.json`.
- A dark, fog-covered parchment/ink continent background and composed mockup, both 1920×1080. Mockup shows two lit fortresses with sample garrisons, one current unlit fortress, and one fogged fortress.
- Fogged, discovered/unlit, and lit fortress markers at 64×64 and 96×96, individually and in three-frame state sheets.
- A 32×32 garrison shield, fixed 112×40 count badge with separate number area, current-location ring, and route arrow.
- Dim/lit repeatable 96×24 dotted route tiles and a six-frame 576×24 travel sheet (120 ms/frame).
- Empty title/button frames at 640×96 and 340×52, with 16-pixel nine-slice borders and documented text-safe areas. Both derive from the existing K-096 frame using nine-slice resizing; the original is unchanged.
- `source/`: original built-in image_gen map and marker-sheet outputs. `previews/`: 17 exact nearest-neighbor 4× UI previews and two browser-review screenshots.
- `_HTML/6_그림_화면/codex_K-104_봉화선지도_v1.html`: self-contained Korean review with embedded images, animation playback/pause/frame selection, side-by-side K-096 mockup comparison, and the current Game/ castle-map texture for comparison. These are references, not captured gameplay.

## Verification performed

- All 19 delivery PNG dimensions match explicit expected sizes. Both full-screen images are fully opaque; UI assets preserve RGBA transparency.
- All six individual fortress markers have one connected alpha component, transparent outer margins, and consistent bottom boundaries: y=58 for 64-pixel cells and y=90 for 96-pixel cells (last visible rows 57 and 89). Cell centers and Unity pivots are documented.
- Every pixel of all 17 enlarged previews matches exact 4× nearest-neighbor replication.
- Both static route tiles repeat at a 16-pixel dot period with transparent edges. All six travel frames are distinct and have the same alpha footprint, preventing position jitter.
- Edge headless loaded the actual local file:// review page: 15/15 embedded images decoded. Animation advanced; pause held the frame; manual selection reached frame 6 at translateX(-1920px). At a 390-pixel viewport, document width remained 390 pixels.
- Visually inspected the map mockup, enlarged fortress state sheet, and desktop/mobile browser screenshots.
- Current Game/ map reference was read only; its SHA-256 is recorded in the manifest. Temporary production scripts and isolated browser profiles were removed from the delivery.

## Integration / remaining work

- Claude must import the textures, wire screen navigation and button states, supply actual localized text/garrison values, and place/rotate/repeat route segments. Slice animation frames before repeating; do not repeat the whole six-frame atlas.
- Garrison values 120 and 86 are illustrative. This card does not approve proposed gameplay percentages, rewards, or support rules from the GDD.
- No pre-existing beacon-route screen/sheet exists in this worktree. Existing repair art is the comparison reference. K-091-v2 reference material was available through the K-096 reference folder.
- The generated map source is 1672×941, nearest-neighbor resampled to the requested 1920×1080. Originals remain available for future art iteration.
- This is map UI without characters, so D-081 character cells, foot y=120, and 3× character game sheets do not apply. Requested marker sizes and their own common baselines are provided instead.
- Unity import, Play behavior, real game captures, and final user art approval remain unverified. No blocking open questions for the asset handoff.
