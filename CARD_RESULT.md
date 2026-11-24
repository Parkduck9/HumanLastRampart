# K-109 — Art constraints (written before production)

- Do not enlarge the head beyond the selected K-107 comparison A silhouette; preserve its body/head ratio and the current hero's game scale.
- Do not use soft gradients, bloom, neon rim lights, or plastic highlights on cloth. Keep stepped navy, cream, brown and restrained brass colors.
- Do not omit blue eyes, eye highlights or the mouth in visible faces. Do not add meaningless medals or ornamental clutter.
- Do not mix pixel densities, blur sprite edges, or let the torso/feet drift between frames. Native cells are 96x128 with the foot boundary at y=120.
- Do not invent a battle backdrop. Use the repository's real beacon-game night screenshot and label the 0.65 sprite composite as a preview.

Research read before drawing: [Unreal Engine / Acquire HD-2D interview](https://www.unrealengine.com/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style) and [AI pixel-art production issues](https://www.reddit.com/r/aigamedev/comments/1vs4uie/i_built_a_free_opensource_tool_that_turns/). The latter describes inconsistent grids, blurred edges, excessive near-identical colors and difficult backgrounds; the constraints above are this card's application, not quoted rules from the sources.

Status: assets delivered; Claude integration and user visual approval pending.

## What was made

Delivered Lumen Pharos variant A battle sheets, material maps, four full-body story portraits, and the four-cell tutorial expression strip. Brown messy hair, navy academy tunic, gold trim, cream mantle with a rear lighthouse emblem, belt lantern and telescope replace the old hero identity. Visible faces include eyes and mouths. The K-107 selected comparison A and current game poses were the proportion/pose references; the big-head turnaround was not used as the proportion reference.

The built-in image_gen.imagegen tool produced five saved source images, including a front-mouth correction and a portrait-face correction. Exact prompts and reference roles are in prompts.json. Deterministic processing isolates the main connected figure in each source region, resamples to the native grid, quantizes the palette, aligns the torso and foot boundary, and packs the sheets. Portrait downsampling uses a 0.75 sampling phase to retain the small eyes and mouths; no face pixels were manually painted in code.

## Files changed

Only these authorized locations were written:

- CARD_RESULT.md (this English handoff).
- Docs/References/codex_k109_v1/ (36 PNGs, Korean README.md, manifest.json, prompts.json, placement.json, validation.json, review_validation.json, and reproducible packing/review/verification scripts).
- _HTML/6_그림_화면/codex_K-109_루멘전체그림_v1.html (self-contained Korean review page).

Replacement-ready files in the output folder:

- hero_walk_run_grid.png — 1152x1536, 4 columns x 4 rows, 16 frames. Rows: back walk, left walk, left run, front walk. Existing left-to-right phase order retained.
- hero_back_idle.png — 288x384, 1 frame; same source as command frame 0.
- hero_command.png — 864x384, 3 frames: ready, hand raised to temple, right-arm command.
- hero_walk_run_grid_normal.png, hero_back_idle_normal.png, hero_command_normal.png — identical respective layouts.
- hero_walk_run_grid_spec.png, hero_back_idle_spec.png, hero_command_spec.png — identical respective layouts; cloth dark and belt/trim brass bright. These target the existing BattleArt/Spec names when Claude integrates.
- portrait_hero_neutral.png, portrait_hero_determined.png, portrait_hero_surprised.png, portrait_hero_tired.png — 256x384 each, full-body framing matching the legacy portrait margins approximately.
- officer_expressions.png — 576x144, four 144x144 cells: neutral, explaining/raised finger, urgent, pleased.

There are nine corresponding native battle color/normal/spec sheets, eight 4x previews, and five generation sources. Battle native cells are 96x128, figure height 73, torso axis x=48, foot boundary y=120. Game cells are exact nearest-neighbor 3x, 288x384, foot pivot (144,360), normalized bottom-left pivot (0.5,0.0625). Existing game sheets already use this cell size. All names, counts, row order and pivots are preserved. Manifest records hashes, sizes, layouts, source paths, map encoding and handoff notes.

The review page animates all motions beside the current in-game hero sheet over the actual beacon_game_20261022/05_pf_night_clean.png screenshot. Both compared sprites receive RGB x0.65; the already-lit background is unchanged. Default sprite display height is approximately that of the photographed hero. The page includes pause, frame step, zoom, alignment guides, portraits, tutorial strip, normals and spec masks. Idle remains a single static frame. Command holds its final pose and repeats every two seconds for review. This is a composite, not a new Unity capture.

## Verified

- verify_assets.ps1: PASS. All 20 battle frames have height 73, last opaque native row 119, torso axis within 0.5px of x=48, binary alpha, zero hidden RGB, a clear outer cell border, and one connected component (no detached stray pixels).
- Color, normal and spec game sheets were compared pixel-by-pixel against the exact 3x native expansion. Layout dimensions pass.
- All four portrait canvases and the tutorial layout pass; their alpha is binary. All 36 PNG files successfully decode through System.Drawing.
- Five existing Game reference hashes still match the earlier K-107 manifest (three battle color sheets, neutral portrait, and wall reference). No Game file was written.
- check_review.cjs: PASS. All 24 embedded PNG payloads have valid PNG headers and dimensions. Six motion entries execute, animation advances, pause holds, frame step advances, and zoom/guides execute. No external image/script resources are required. This check uses a Node VM with DOM/canvas stubs; it is not a browser-rendering test.
- Direct visual inspection of generated sources, final battle 4x sheets, final story portraits, tutorial strip and a spec mask. A missing-mouth risk after portrait downsampling was identified and corrected before delivery.

## Not performed / leftovers

- Browser visual rendering could not be verified: the browser/computer-use inventory returned no available apps or browsers. Do not describe the HTML as browser screenshot-verified.
- Unity was not opened or run. No Play, animation-transition, live jitter, actual lighting, normal/spec response or gameplay capture was performed.
- Claude must integrate the replacement files and verify transitions, right-facing mirroring, materials and in-game scale. The normal maps are silhouette-derived inward bevel RGB maps, not painted per-material surface normals. Spec masks are palette/region-derived. Their actual shader response needs integration review.
- User visual approval remains pending; numerical alignment does not prove that generated motion has no perceptual shape wobble.
- No Git write commands, branch changes, commits, Game changes, collaboration-doc/log changes or other worktree-file edits were made. Claude handles commits.
- No unresolved design question was introduced.
