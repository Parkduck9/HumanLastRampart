# K-124 — anti-slop constraints (recorded before drawing)

- Do not split the roof, gallery, or masonry into blue and grey halves at the vertical center; shade each connected form continuously from the actual lights.
- Do not wash the silhouette in airbrushed bloom, grey haze, or neon teal/purple; keep a dark navy field and a restrained amber beam.
- Do not mix pixel densities or add painterly microtexture; keep crisp, chunky clusters and a limited slate/stone palette.
- Do not make the stone glossy or invent ornamental fantasy clutter; preserve the barred lamp room, slate roof, and stone gallery composition.
- Do not sacrifice the recognizable roof, bars, and lamp at 48 px for small decorative details.

Research read before drawing: https://pixeljoint.com/forum/forum_posts.asp?TID=11299 and search excerpts on pixel clusters, banding, palette ramps, and consistent lighting. Card-specific constraints above also follow COMMON.md.

# K-124 handoff

# Completed work

Redrew the K-121 lamp-room icon using the built-in image_gen editor in two passes. The second pass removes the residual blue left face and gold roof-edge lighting. The final roof and gallery use continuous neutral slate/stone materials, a restrained cool top-left rim, and localized warm light from the lamp room. The barred room, roof, gallery, finial, and beam to the upper right remain recognizable in the same composition. This is a redraw, not a pixel-identical recolor.

# Files changed

Only new card deliverables were written:
- Docs/References/codex_k124_v1/icons/: app_icon_1024.png, app_icon_512.png, app_icon_256.png, app_icon_192.png, app_icon_128.png, app_icon_48.png, lamp_foreground_128.png, adaptive_foreground_432.png, adaptive_background_432.png.
- Docs/References/codex_k124_v1/previews/: icon_48_4x.png, lamp_foreground_4x.png, adaptive_composite_432.png, review_desktop.png, review_48.png, review_mobile.png.
- Docs/References/codex_k124_v1/sources/icon_redrawn.png: final generated source.
- Docs/References/codex_k124_v1/manifest.json, README.md (Korean), prompts.json, verification_assets.json, verification_browser.json.
- _HTML/6_그림_화면/codex_K-124_아이콘수정_v1.html: self-contained Korean review, old/new at all six sizes on light/dark surroundings, transparent foreground comparisons, adaptive masks, native-size dialog.
- CARD_RESULT.md: this English handoff, with the pre-drawing do-not list preserved above.

Temporary export/build/browser-check scripts and the temporary browser profile were removed from the final deliverables. No Game/, K-121, other card, collaboration documentation, or logs were modified. No Unity execution or Git write was performed.

# Verification actually performed

- Opened and visually inspected the original K-121 target and logical foreground, final native 4x preview, final 48px 4x preview, and desktop/48px/mobile browser screenshots.
- Checked all 9 exported icon PNGs against manifest dimensions and alpha extrema. Six square icons and adaptive background are fully opaque; both foreground files retain real transparency.
- Compared all 5 non-128 square exports pixel-for-pixel against nearest-neighbor sampling of the 128px master: zero mismatches.
- Compared both 4x previews pixel-for-pixel against nearest-neighbor sampling: zero mismatches.
- Native alpha cleanup threshold was 32; checked 8-connected foreground components below 3 logical pixels: none found, zero pixels removed.
- Adaptive foreground preserves K-121's 432px canvas, 192px content at (120,120), center (216,216), and safe radius 132px. Maximum occupied pixel-center radius measured 105.422957651548px, inside the safe circle.
- Headless Edge loaded and decoded all 40 review images. No horizontal overflow at 1440px or 390px viewport widths. Original-size dialog opened and reported natural width 1024.
- Visual review confirms that the hard vertical blue/grey seam is removed. At 48px, roof silhouette, dark bars, warm lamp, and stone base remain distinguishable. This is a visual assessment, not a device usability test.
- Sprite foot lines and motion/frame alignment are not applicable to this static icon.

# Open questions / leftovers

No unresolved asset question. Claude must wire the replacement icon files, assign Android adaptive layers, commit the card, and verify platform/device rendering. No importer, PlayerSettings, runtime, Unity Play, or device checks were run. No monochrome/themed adaptive layer was requested or supplied, matching K-121 scope.

Additional research read after the initial pre-drawing pixel-art search: https://geekchamp.com/how-to-tell-ai-generated-game-art-from-human-made-art-without-guessing/ and https://www.unrealengine.com/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style . Actual generation prompts are preserved in prompts.json.

