# K-127 — Concrete do-not rules (written before drawing)

- Do not introduce soft gradients, bloom, blurred edges, glossy skin or plastic clothing; retain K-119's discrete pixel clusters and material ramps.
- Do not change worker A/B identity, costume colors, head proportions or outline density between facings and frames.
- Do not hide missing facial features behind shadow: preserve readable eyes, nose and mouth in visible faces.
- Do not add a painted capstan bar, particles, ornaments or ground scenery; keep chest-height grip positions clear for the game-drawn bar.
- Do not change pixel density or let planted feet, body anchor or grip height jitter; avoid isolated noise pixels and automatic smoothing.

Research read before drawing: https://pmc.ncbi.nlm.nih.gov/articles/PMC12672458 (smoothness, polish and blur as reported visual cues); https://pixeljoint.com/forum/forum_posts.asp?TID=11299 (pixel clusters, banding and isolated noise).

## Delivered

Assets complete; Claude integration and user visual acceptance pending.

Both existing worker identities are covered: A (cream shirt, rusty leather apron, brown/grey hair) and B (bald crown, grey beard, reddish vest, olive apron). Each variant has:

- Push: front/back/left/right, 8 frames each.
- Push start: all four facings, 3 frames each.
- Push stop/rest: all four facings, 3 frames each (release, wipe brow, lower arm).
- Strain push: left/right only, 8 frames each, with deeper lean and lower head.
- Compatibility sheet `worker_a_push.png` / `worker_b_push.png`: identical to the new right-facing push, preserving the current in-game filenames, eight-frame count and gait order. The old four-frame rest is deliberately not replaced with a three-frame file.

28 directional motion sheets plus two compatibility aliases = 30 sheet sets, 90 sheet PNGs, 144 distinct directional frame slots plus 16 alias slots. Every set has native, game and 4x preview versions. No game code, existing assets, shared documents or collaboration logs were edited. No Unity launch or Git writes were performed.

## Files changed

- `Docs/References/codex_k127_v1/native/*.png`: 30 native sheets, 96x128 cells.
- `Docs/References/codex_k127_v1/game/*.png`: 30 sheets, exact 3x nearest-neighbor, 288x384 cells.
- `Docs/References/codex_k127_v1/previews/*_4x.png`: 30 exact 4x sheets.
- `Docs/References/codex_k127_v1/previews/review_desktop.png`, `review_mobile.png`: actual headless Chrome review captures.
- `Docs/References/codex_k127_v1/source/worker_{a,b}_atlas.png`, `worker_{a,b}_atlas_clean.png`: built-in image_gen originals and bar-removal edits.
- `Docs/References/codex_k127_v1/manifest.json`: cells, pivots, bar height, facing, frame order/labels, playback, alias mapping and limitations.
- `Docs/References/codex_k127_v1/README.md`: Korean production and integration notes.
- `Docs/References/codex_k127_v1/prompts.json`: generation prompt set and edit purpose.
- `Docs/References/codex_k127_v1/build.cjs`, `build_review.cjs`, `verify.cjs`: reproducible packing, embedded review construction and verification. Code comments are Korean.
- `Docs/References/codex_k127_v1/extraction.json`, `verification.json`, `browser_verification.json`: extraction and measured verification records.
- `_HTML/6_그림_화면/codex_K-127_일꾼바퀴밀기_v1.html`: Korean, self-contained, animated review with every directional motion beside same-facing K-119 walk and the actual current in-game K-110 push. Includes the supplied W35 screenshot unchanged and clearly identified as reference, not a new in-game capture.
- `CARD_RESULT.md`: this handoff.

## Production

Used the built-in image_gen tool, not the CLI/API. Generated one ten-row atlas per variant using K-119 front/back/right references, then requested removal of any generated bar. The original A front-start first pose was retained because it reaches from lower hands more clearly. Full generation prompts are persisted in `prompts.json`. Packing thresholds alpha at 200, retains the largest connected component, removes saturated red edge artifacts, maps opaque colors exclusively to each variant's actual K-119 walk palette, anchors the waist center at x=48, and places the lowest supporting sole on y=120. Left facings use K-119's reflection convention around x=48, including reflected lighting.

## Verified

- Read K-119 and K-110 manifests and current in-game push sheet dimensions (2304x384, eight 288x384 cells). Reviewed the supplied W35 screenshot, K-119 enlarged walk sheets, K-110 push and the user-picked hero generated.png.
- All 90 sheet PNGs decode; declared dimensions/frame counts match. Checked 51,118,080 output pixels for exact nearest-neighbor equality.
- Every frame's lowest opaque row is native y=120. Game foot pivot is (144,360); the scaled contact pixel occupies rows 360..362.
- Alpha is binary 0/255; transparent RGB is zero. Every opaque color belongs to that variant's K-119 front/back/right walk palette.
- Every native frame is a single 8-connected component after packing; no disconnected stray pixels. Every frame in a sheet has a unique SHA-256 pixel hash. Compatibility aliases exactly equal the right-facing push sheets.
- Visually inspected enlarged front push, side push, side strain and brow-wipe sheets, plus desktop/mobile review captures.
- Actual local-file review tested in installed Chrome headless: 38 embedded comparison images and scene screenshot decode; 84 canvases render; playback advances; single-step advances every motion by one frame; variant/motion filtering works; no script errors; no horizontal overflow at 390px width.
- Unity was not launched. No claim of gameplay, camera, actual hand/bar contact, gait speed synchronization or user approval is made.

## Integration / open questions / leftovers

- Native contract is exactly K-119: cell 96x128, foot (48,120); game cell 288x384, foot (144,360), normalized pivot (0.5,0.0625). Frames run left to right.
- Nominal bar center is native y=85 / game y=255, i.e. 35 / 105 pixels above the foot pivot. The painted hands wrap around this center; the bar itself is not painted. The manifest describes a two-native-pixel grip tolerance, not a measured in-game alignment guarantee. Claude must visually check the actual bar thickness and foreground/background occlusion, especially when facing back.
- Start plays once then enters push; stop plays once and holds final rest. Review-only repeated playback is labeled in Korean. Strain is side-only as requested.
- Synchronize external circular movement, facing switches and loop speed in the game. No world motion is baked into the sprite. The file checks establish anchored soles, not absence of foot sliding under arbitrary in-game movement speed.
- These are generated directional extensions with the exact K-119 palette; facial pixels are not exact copies of K-119. User visual acceptance remains pending.
- Git is unavailable on this shell's PATH; no Git status/diff verification was completed. All write operations explicitly targeted only this card's output directory, named HTML page and CARD_RESULT.md. Claude handles staging and commits.
