# K-117 — concrete do-not rules (recorded before drawing)

1. Do not replace the square four-wall game fortress with a fantasy castle, extra turrets, cliffs, or ornamental clutter. Keep the K-111 pulled-back composition and the real courtyard circulation.
2. Do not draw a pedestal brazier: use the 12 m square stone lighthouse, approximately three wall heights, with trim bands, slate roof, barred lamp room and arched door, following K-115.
3. Do not wash the night in grey haze, bloom or purple/teal neon. Keep blue moonlit shadows and local amber light with visible falloff; the beam must read as a broad curved fan, not a laser.
4. Do not use plastic gloss, airbrushed stone or inconsistent pixel density. Keep crisp stepped pixel clusters, matte blocks and coherent directional shadows.
5. Do not place faces, foreground figures, bright lights or high-contrast clutter in the lower third (y=720–1079), reserved for dialogue.

## Research read before generation

- https://www.aigamedevs.org/wiki/style-consistent-2d/ — fixed style constraints, reference composition and consistent pixel treatment help avoid mismatched generated assets.
- https://www.unrealengine.com/spotlights/octopath-traveler-s-hd-2d-art-style-and-story-make-for-a-jrpg-dream-come-true — developer discussion of pixel art, point lights and environmental shadows.
- The supplied COMMON.md and K-117 brief provide the binding project-specific avoid list.

## Work

Completed two opaque 1920x1080 static story backgrounds using the built-in image_gen tool. Replaced the pedestal brazier with a tall square lighthouse matching K-115: large stone blocks, trim bands, slate roof, barred lamp room, arched door. Preserved the pulled-back four-wall fortress composition, added the north stair approach, paved road, round plaza, lamp posts and low perimeter buildings. Dusk has a cold unlit lamp room; night has a blazing lamp room, a broad warm curved field fan and road lamp pools. The lower third remains dark for dialogue.

The dusk road was corrected from an initial foreground approach to the north approach. The night beam received a separate correction to emphasize its curved ground footprint. The resulting beam is a static illustration of one sweep direction; it is not an animation or a physically rendered game light.

## Files changed

Only the following new paths were written:

- `CARD_RESULT.md` (this English handoff).
- `Docs/References/codex_k117_v1/story_dead_beacon_1920x1080.png`
- `Docs/References/codex_k117_v1/story_relit_beacon_1920x1080.png`
- `Docs/References/codex_k117_v1/previews/story_dead_beacon_1920x1080_4x.png`
- `Docs/References/codex_k117_v1/previews/story_relit_beacon_1920x1080_4x.png`
- `Docs/References/codex_k117_v1/sources/story_dead_generated.png`
- `Docs/References/codex_k117_v1/sources/story_relit_generated.png`
- `Docs/References/codex_k117_v1/manifest.json`
- `Docs/References/codex_k117_v1/README.md` (Korean).
- `Docs/References/codex_k117_v1/prompts.json` (Korean prompt-set summary).
- `Docs/References/codex_k117_v1/build_review.cjs`
- `Docs/References/codex_k117_v1/verify.cjs`
- `Docs/References/codex_k117_v1/verification.json`
- `Docs/References/codex_k117_v1/review_desktop.png`
- `Docs/References/codex_k117_v1/review_mobile.png`
- `_HTML/6_그림_화면/codex_K-117_이야기배경_등대탑_v1.html` (Korean, all images embedded).

## Verification actually performed

- Inspected the K-111 backgrounds, K-115 tower mockup and real courtyard screenshots 259, 279 and 01 before generating; read the K-111/K-115 documentation and COMMON.md.
- Inspected the selected generated images and desktop/mobile review captures visually.
- Both deliverables are exactly 1920x1080 RGBA, with every alpha value 255. No transparent stray pixels or holes. Visible small stone/star/ground details were inspected visually, not claimed as an automated semantic stray-pixel test.
- Both 7680x4320 previews match exact nearest-neighbor 4x enlargement at every channel of every pixel.
- Measured lower-third luminance on the 0–255 weighted RGB scale: dusk mean 33.65, night mean 19.39. Fraction above 100: dusk 0.0371%, night 0.0048%.
- Headless Microsoft Edge opened the HTML directly via file URL. All nine embedded images decoded. Dialogue overlay on/off, expansion controls and 390px mobile horizontal overflow checks passed; no page JavaScript errors. Exact results are in verification.json.
- Foot baseline, sprite pivot, native/game cell pairs and motion animation are not applicable to these opaque static story backgrounds; manifest records null pivots/baselines and one frame each.
- Generated source images were normalized to 1920x1080 with nearest-neighbor resampling, without cropping; source PNGs are retained. Preview generation and review verification scripts are included.
- Read the worktree HEAD as `refs/heads/card/K-117`. No Git writes occurred. Git was unavailable on PATH, so no git status/diff check is claimed.

## Open questions / leftovers

No blocking questions. Claude must connect the selected assets to the game, verify the actual story dialogue layout in Unity and commit. Unity was not opened or run. Game/, existing references, other HTML, collaboration docs and live logs were not modified. No source texture or runtime code changes are part of this card.
