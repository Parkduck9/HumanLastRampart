# K-119 — card-specific do-not list (written before drawing)

- Do not blur, antialias, airbrush or mix pixel densities; final native pixels must have hard edges and binary alpha.
- Do not add bloom, fog, neon purple/teal or glossy highlights to these indoor workers; keep K-110's restrained fabric, skin and leather colors.
- Do not lose the workers' middle-aged faces, mouths, bulky arms, aprons and boots between directions or frames.
- Do not disguise pushing as walking: free arm swings, alternating contact/passing legs, raised stair knees and a held carry prop must read distinctly.
- Do not change the 96x128 cell, (48,120) foot anchor or character scale, or introduce detached specks and frame drift.

Research read before drawing: https://www.spritefusion.com/pixel-snapper/how-to-fix-ai-pixel-art and https://www.unrealengine.com/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style?lang=en-US . These discuss off-grid/blurred AI pixels and balancing pixel characters with 3D lighting, respectively. The rules above are the card-specific application.

## Delivered

Created K-110-matched directional motion extensions for both existing visual variants, a and b. The brief mentions three workers; the repository contains two identities and `BattleBeaconView.cs` explicitly assigns them as `{ 0, 1, 1 }` (a, b, b). Both identities are covered; no invented third identity.

Each variant has 17 one-row sheets / 118 frames:

- Walk: 8 frames each, front/back/right/left.
- Climb up and climb down: 6 frames each, back/right/left for each motion.
- Tend brazier: 6 frames each, front/right/left, lifting and tilting a small iron oil can and lowering it again.
- Carry oil can while walking: 8 frames each, front/back/right/left.

Total: 34 native sheets, 34 game sheets and 34 four-times previews, representing 236 native animation frames. Left sheets are explicit mirrored files reflected about native x=48, preserving the pivot. Mirroring also reflects sprite lighting; this is disclosed in the manifest and Korean review.

The native cell is 96x128, foot pivot (48,120), last occupied foot row y=120. Game sheets are exact 3x nearest-neighbor expansions into 288x384 cells, pivot (144,360); the last occupied native row becomes game rows 360..362. Normalized Unity pivot remains (0.5,0.0625). Root motion and actual staircase elevation must be provided by integration.

## Files changed / added

Only the authorized output locations were written:

- `Docs/References/codex_k119_v1/native/worker_{a,b}_{motion}_{direction}.png` — 34 sheets; exact file inventory and frame order are in `manifest.json`.
- `Docs/References/codex_k119_v1/game/` — corresponding 34 sheets at 3x.
- `Docs/References/codex_k119_v1/previews/` — corresponding 34 sheets at 4x, plus `review_desktop.png` and `review_mobile.png` browser QA captures.
- `Docs/References/codex_k119_v1/source/worker_a_atlas.png`, `worker_b_atlas.png` — generated source atlases, not game-ready outputs.
- `Docs/References/codex_k119_v1/manifest.json`, `README.md`, `prompts.json`, `extraction.json`, `verification.json`, `browser_verification.json`.
- `Docs/References/codex_k119_v1/build.cjs`, `build_review.cjs`, `verify.cjs` — local asset packing/review/verification helpers, with Korean comments. No runtime game code.
- `_HTML/6_그림_화면/codex_K-119_일꾼걷기_v1.html` — self-contained Korean review, every motion animated beside the actual current in-game push sheet, with play/pause, single-step, speed and filters. Includes the supplied actual courtyard capture as unchanged context, not a new in-game mockup.
- `CARD_RESULT.md` — this English AI-to-AI handoff, with the pre-drawing do-not list retained at the top.

Built-in `image_gen` was used, one source atlas per visual variant. Exact final prompts are saved in `prompts.json`. Packing uses alpha threshold 200, largest connected-component cleanup, nearest-color mapping to the corresponding original K-110 push palette, body/waist centering and foot anchoring. Generated source row spacing was detected from alpha profiles rather than assumed uniform. Standing motion targets are 73 native pixels; climb poses target 71. Actual frame heights are 69..74 pixels; horizontal occupancy stays within x=16..80. These variations represent articulated poses, while the support-foot baseline and root anchor stay fixed.

## Verified

Executed `node Docs/References/codex_k119_v1/build.cjs`, `node Docs/References/codex_k119_v1/build_review.cjs`, and `node Docs/References/codex_k119_v1/verify.cjs` successfully.

- All 102 delivery sheet PNGs decode and match manifest dimensions.
- Exhaustive 75,399,168 output pixel comparisons confirm exact nearest-neighbor correspondence for all native/game/4x sheets.
- All 236 native frames have occupied bottom row y=120, binary 0/255 alpha, one connected silhouette (no detached stray pixels), and unique pixel data within each motion sheet.
- Inspected generated atlases and representative walking, stair and tending sheets visually, plus desktop/mobile browser screenshots.
- Opened the review via a local file URL in installed headless Chrome. All 36 animation images and 35 HTML image elements decoded; 68 canvases rendered; no image decoding or page script errors.
- Verified automatic animation advances, single-step advances every motion by one frame, variant/motion filtering, and no horizontal overflow at 390px viewport width.
- Review embeds all artwork and the real context screenshot; no asset server is required.

Did not open or run Unity, import assets, run Play, or verify live-game animation/camera/stair occlusion. Did not modify `Game/`, other documents, collab/live logs, or the shared HTML index. No Git write operations were performed.

## Open questions / leftovers for Claude

- Wire the new sheets to walk/climb/tend/carry states, retain existing worker assignment a/b/b, and drive world-space stair movement independently. Existing push/idle/rest filenames and contents were left untouched.
- This is a generated directional extension, not a pixel-identical copy of K-110 faces. Final art acceptance and visual transition quality require user review; binary/pixel checks do not establish subjective animation quality.
- The supplied screenshot shows the existing lighthouse, not these new animations in-game. User Play/capture remains pending.
- Native sheet names, frame counts, pivots, frame order, mirrored sources and suggested playback rates are authoritative in this card's `manifest.json`. No unresolved asset generation or browser validation failures remain.
