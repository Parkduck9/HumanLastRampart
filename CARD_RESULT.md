# K-129 — Wall-hand Ogre

## Do not — written before drawing
- Do not mix logical pixel sizes or retain anti-aliased fringes; use one native grid and exact nearest-neighbour scaling.
- Do not wash the silhouette with bloom, haze, gradients, or purple/teal neon.
- Do not give the iron mitts glossy plastic shading; keep restrained three-step iron, olive skin, and brown leather ramps lit from upper left.
- Do not omit eyes, mouth, tusks, long arms, or short legs from readable views, or change the selected B identity between frames.
- Do not substitute translated stills for articulated motions or let the ground/grip anchor jitter.

Pre-production reading: https://pixelartworkshop.com/guides/pixel-art-vs-ai-generated and https://ojs.aaai.org/index.php/AIIDE/article/download/21951/21720/26003 (limited palette, crisp edges, noise and shape consistency).

## Completed assets

Created the selected K-128 concept B Wall-hand Ogre as a separate boss asset set: 17 horizontal animation strips, 120 total frames, both native and exact 3x nearest-neighbour game sheets, and matching tangent-space normal sheets. This does not replace the existing ordinary ogre filenames or change their frame order.

- Walk: 8 frames each front/back/right/left. Left mirrors right, including the asymmetric equipment; normal X follows the mirror.
- Wall-foot punch: 6 frames each front/side.
- Climb: 8 frames each back/front, alternating arm reaches and braced knees.
- Lose grip/fall: 6 frames; land/stagger: 6 frames.
- Trap: fall back 4, lie loop 2, get up 6. The get-up opening reuses the lie endpoint to avoid a transition jump.
- Parapet grab/smash: 8 frames each front/back.
- Death: 10 frames each front/side, ending lying down.
- Map icon: 64x64, overhead view. HUD portrait: 96x96. Both have matching normal maps and no text.

Native cell is 256x288; game cell is 768x864. Ground pivot is (128,280) native / (384,840) game. Standing height is approximately 144 native pixels, corresponding to 4.5m at 32 px/m. The 4m scale wall is 128 native pixels high. Climb suspension origin is (128,16), fall body origin is (128,152), both multiplied by 3 for game sheets. All frame coordinates, phases, bounding boxes, file paths, and import recommendations are in manifest.json. A ten-frame game strip is 7680 pixels wide: preserve it with a texture limit of at least 8192.

## Files changed / created

Only these authorized locations were written:

1. Docs/References/codex_k129_v1/ — native/, native_normal/, game/, game_normal/, preview_4x/, icon/portrait PNGs, seven generated source atlases, manifest.json, Korean README.md, generation.json with full prompts and extraction boxes, reproducible asset/review builders, validation tools/reports, contact sheet and browser screenshots.
2. _HTML/6_그림_화면/codex_K-129_우두머리동작_v1.html — Korean self-contained local review, with every motion animated alongside the current ordinary ogre and a 4m wall scale comparison; playback/step/zoom/color/night/silhouette/normal/normal-lighting controls.
3. CARD_RESULT.md — this English AI-to-AI handoff.

Built-in image_gen was used, not a CLI/API fallback. The complete prompts and source provenance are retained in generation.json. Technical processing extracts each connected character instead of assuming perfect generated grid spacing, thresholds alpha, removes detached fragments, maps to the selected B palette plus two blue eye colors, aligns origins, derives shallow relief normal data, and exports exact nearest-neighbour scales. No Unity project/runtime code was added. The Python and CJS files are asset production/verification utilities within the authorized reference output folder.

Read the selected B study/concept/manifest/README, boss GDD, COMMON rules, current ogre walk/attack and normal/import settings, approved hero generated.png, K-066 enemy materials, and K-070 proportions. Did not use corrected.png, hero_idle_56x112.png, or preview images as the art direction.

## Verified

- validate.py: 627 checks passed, zero failures. Checked all 120 frames for dimensions, recorded bounds, binary alpha, zero RGB in transparent color pixels, one 8-connected silhouette, interior padding, ground or suspension origin as appropriate, and no detached pixel islands.
- Exact pixel equality for native-to-game 3x and native-to-preview 4x sheets, including normal scaling; exact framewise left/right albedo mirroring.
- Normal dimensions match color; transparent areas are flat RGB(128,128,255); maximum normal unit-length error is 0.004803 or less.
- Both UI images have their specified size, binary alpha and a single connected silhouette.
- All 44 embedded PNG occurrences decoded. Chrome opened the review over file://, animated all 17 sections, stepped every motion, reached final frames, switched all five modes and 2x zoom, and reported zero page script errors. Checked 1440x1100 desktop and 390x844 mobile, with no whole-page horizontal overflow. Screenshots and review_validation.json are saved.
- Visually inspected generated sources, the full contact sheet, final smash strip, icon/portrait, and desktop/night/death/mobile browser captures. An initially clipped raised gauntlet was corrected by connected-component extraction and rechecked before the final export.
- SHA256 comparison of 2,635 Game/input files found zero changes. Git read-only status/diff showed no tracked modifications; only the requested outputs and the externally produced CARD_CODEX_LOG.jsonl were untracked. The external log was not edited.

Unity was never opened or run. No Play, import, shader, actual game capture, Game write, collab/live write, Git mutation, commit or branch operation was performed. Claude owns the commit and integration.

## Open questions / integration leftovers

- Asset production is complete; Unity import and gameplay integration remain for Claude. Keep Point/Clamp, no mipmaps/compression for initial comparison, color sRGB, normal linear/NormalMap, +Y without green flip, matching UVs, and use albedo alpha for the silhouette. These are recommendations, not changes to ProjectSettings or imports.
- Normals are a restrained silhouette/luminance relief approximation, not a 3D bake or physically validated per-muscle/per-gauntlet surface map. Browser lighting/night modes are illustrative and are not a Unity beam/shadow test.
- The climb origin is a fixed suspension reference above the hands, not a hand rig that pins the supporting wrist to one identical pixel for every pose. Individual grips move as arms alternate. Actual wall contact, occlusion, 25–45 degree billboard views, in-game hand/parapet placement, and readability at the final battle distance still need Unity/user review.
- Falling is authored around a fixed body origin; world fall trajectory, climb translation, landing transition, attack hit frames, death hold, and trap-down duration must come from gameplay. Review FPS values and one-shot restarts are preview behavior only.
- Ordinary ogre comparison is normalized to 96 native pixels for readability, as in K-128; it is not a new approved world-height decision. The wall is a labeled scale diagram, not an invented battle/courtyard mockup.
- No additional design decision or permission is required to inspect the delivered assets. Existing boss GDD balance questions remain outside this asset-only card.
