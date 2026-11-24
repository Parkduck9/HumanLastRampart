# K-121 — anti-slop constraints (written before image creation)

- Do not wash the dark plain or fortress in gray haze, bloom, or airbrushed gradients; reserve warm light for the lamp and surfaces it actually reaches.
- Do not replace the approved square four-wall fortress and 12 m masonry lighthouse with a coastal tower, cliff citadel, or generic ornate fantasy castle.
- Do not mix pixel densities, smear masonry into painterly noise, or add glossy plastic stone; use crisp clusters and readable material steps.
- Do not fill the left menu area with beams, props, decorative runes, painted lettering, or high-contrast detail.
- Do not make the icon a tiny full landscape: the iron-framed lamp room and warm lamp must survive at 48 px without neon teal/purple or excessive rays.

Research read before creation: https://geekchamp.com/how-to-tell-ai-generated-game-art-from-human-made-art-without-guessing/ (lighting, textures, lettering are diagnostic clues, not proof); https://www.unrealengine.com/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style (developer discussion of pixel art and lighting).

## Completed work

Created the new main-menu illustration using the approved K-115 lighthouse and K-117 flat square fortress. The lamp and beam sit on the right; the left remains available for the current menu panel. No letters are painted into the assets. Read the actual courtyard overview, current menu background/layout, UiTokens font definitions, and ProjectSettings icon references.

Used built-in image_gen to create and separate sky, filled far field, fortress/courtyard/tower/beam, and foreground layers. Each has a 640x360 logical original and exact nearest-neighbor 3x and 4x versions. Final backgrounds are composites of the delivered layers. Removed alpha below 32 and components smaller than 3 logical pixels. Retained an edge-correction generation attempt as source only; selected the first fortress extraction for its more restrained colors.

Created a lamp-room app icon in all six requested sizes, adaptive foreground/background, and 4x inspection previews. The self-contained Korean review embeds the actual Hahmlet Black font and requested title, current/new background comparison, parallax animation/pause, panel toggle, all icon sizes on light/dark surfaces, and adaptive masks/safe-zone guides.

## Files changed

Only new files in card-authorized locations were written:

- `Docs/References/codex_k121_v1/`: 23 manifest-listed production PNGs (2 backgrounds, 12 layers, 9 icon assets), 6 inspection previews, 7 generation sources, Korean README, actual prompts, manifest, cleanup/verification JSON, and 3 browser captures.
- `_HTML/6_그림_화면/codex_K-121_메인화면_아이콘_v1.html`.
- `CARD_RESULT.md`.

No Game/, ProjectSettings, existing references, collaboration docs/logs, or other files were modified. No Git writes or Unity execution were performed.

## Verification actually performed

- Dimensions and alpha ranges checked for all 23 production PNGs.
- All 8 layer expansions exactly match nearest-neighbor output. The 1920 background exactly matches the delivered layer composite; the 2560 background matches nearest-neighbor enlargement. All 6 inspection previews match exact 4x enlargement.
- Visually inspected final background, enlarged 48px icon, captured menu, and mobile review. No detached alpha islands below the 3-pixel native threshold remained. Stars and ground texture are intentional content.
- Adaptive foreground lies entirely inside the centered 132px-radius safe circle: maximum content radius 116.9124px; alpha bounds x=138..308, y=127..310 on the 432px canvas.
- Headless Edge: all 30 embedded images loaded; Hahmlet font loaded; animation transforms changed; pause and panel toggle worked; no page errors; no horizontal overflow at 390px. Saved desktop, menu detail, and mobile captures.
- SHA-256 hashes of the read-only current main background, ProjectSettings, MainMenuEntry.cs, and ScreenView.cs remained unchanged.
- Foot baseline/frame alignment is not applicable to static scenery/icons. Manifest records null baselines and single-frame order. No character sheets were replaced.

Detailed results: `Docs/References/codex_k121_v1/verification.json`.

## Handoff / limitations

- Claude must import/wire assets, apply the requested Korean title, assign platform icons, create required Unity metadata, and perform actual game/device validation. Current menu source still has the previous title; left untouched.
- This worktree's ProjectSettings icon texture arrays are empty. No assigned legacy app icon texture was found; review compares the current menu background and shows the new icon independently.
- Beam is baked into the fortress layer, depicting the start of a sweep. Independent continuous beam rotation is not supplied. Parallax uses 1.035x overscan and recommended 1920px horizontal limits of 2/4/6/10px, vertical limit 3px.
- The 2560 version shares the 640x360 logical source; it adds no painted detail. Generation-based extraction alters some pixels from the initial source, but final layers and composite agree exactly.
- No monochrome Android themed-icon layer was requested or supplied. Numeric/browser mask checks are not Android device testing.
- No open design questions block asset delivery.
