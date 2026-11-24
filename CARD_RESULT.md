# K-128 — boss concepts

## Do not — written before drawing
- Do not mix pixel densities or soften sprite edges; keep chunky clusters and dark outlines at the same logical scale as the existing goblin and ogre.
- Do not bury faces, mouths, hands or functional weak points under tiny spikes, runes, skulls or decorative demon armour.
- Do not use airbrushed gradients, plastic highlights, purple/teal neon, broad bloom or grey fog that erases the silhouette.
- Do not make four interchangeable humanoid brutes: use a ram, a climber, a caller and a shield carrier with separate silhouettes and clear material groups.
- Do not invent a battle screenshot or assert gameplay approval; use measured concept comparisons and explicitly provisional counterplay notes.

Research read before drawing:
- https://www.rangy.ai/blog/ai-pixel-art/ — grid, palette and edge failures in generated pixel art.
- https://www.unrealengine.com/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style — developer discussion of pixel characters and day/night lighting.

These informed the card-specific constraints above, not approval of any final design.

## Work
Completed four concept-only boss designs for user selection: A, a goblin covered ram; B, an ogre wall climber; C, a goblin drum caller; D, an ogre carrying a salvaged door shield. Each concept board includes front/side views at a shared world scale, a 4x pixel detail, a 56px goblin, a comparison-normalized 96px current-game ogre, a 128px / 4m schematic wall, a 0.5x silhouette test, an illustrative night/beam contrast swatch, Korean fantasy text and provisional beam/cannon/trap counterplay.

Used the built-in image_gen tool once per accepted design, with the approved original hero, K-066 enemy lineup and K-070 species-proportion goblin as style references. Inspected all generated sources. Normalized the art through binary alpha, proportional nearest-neighbour reduction, a fixed 28-colour palette and aligned foot boundaries. Full accepted prompts, generated-source paths and hashes are in manifest.json. No fallback API/CLI generation was used.

## Files authored
- Docs/References/codex_k128_v1/A_ram_concept.png
- Docs/References/codex_k128_v1/B_climber_concept.png
- Docs/References/codex_k128_v1/C_caller_concept.png
- Docs/References/codex_k128_v1/D_shield_concept.png
- Docs/References/codex_k128_v1/lineup.png
- Docs/References/codex_k128_v1/A_ram_study.png
- Docs/References/codex_k128_v1/B_climber_study.png
- Docs/References/codex_k128_v1/C_caller_study.png
- Docs/References/codex_k128_v1/D_shield_study.png
- Docs/References/codex_k128_v1/manifest.json
- Docs/References/codex_k128_v1/README.md
- Docs/References/codex_k128_v1/review_desktop.png
- Docs/References/codex_k128_v1/review_mobile.png
- _HTML/6_그림_화면/codex_K-128_우두머리시안_v1.html
- CARD_RESULT.md

The additional transparent study PNGs contain two static orthographic views, not animation. Each study is 448x192, made of two 224x192 concept cells, with pivot (112,184) per cell and front/side order. Their wider concept cells are intentional; they are not ready-to-import production sheets. No 3x game sheet or animation was requested or made.

## Verified
- All four boards are 1600x1160; lineup is 1600x800; transparent studies are 448x192.
- All eight static views have a foot boundary at y184 (last opaque row183), bbox centre within 0.5px of x112, binary alpha {0,255}, and zero RGB data in transparent pixels.
- Each view forms one 8-connected opaque component; no disconnected stray pixels remained. No animation or temporal jitter test applies.
- Board detail pixels match the normalized front study enlarged exactly 4x nearest-neighbour.
- HTML's 22 embedded PNG occurrences decoded successfully. It is self-contained and opens using file:// without a server.
- Actual headless Chrome/Playwright verification: four cards in one row at 1600x1100; no page overflow at 1600x1100 or 390x844; colour/silhouette/night controls, 1x and 4x sizing, selection indicator, and all four full-size sheet dialogs work; no page script errors. Screenshots saved in the output directory and visually inspected.
- Source-reference hashes were unchanged. Read-only Git status showed no tracked changes. CARD_CODEX_LOG.jsonl was also present as an externally generated untracked runtime log; it was not authored, modified or deleted by this task.
- No Unity launch, Play session, fresh game capture, game integration, Game/ edits, live collaboration log edits, Git staging, commit or branch operation was performed.

## Open questions / handoff
- User choice of concept(s), final visual changes, physical scale and all counterplay rules remain pending. Nothing here establishes an approved gameplay API or behaviour.
- Proposed total heights: A104px/3.25m; B144px/4.5m; C106px/3.3125m INCLUDING STAFF (body approximately2.7m); D112px/3.5m. The ogre comparison's 96px/3m is a card-specific normalization, not a newly approved canonical size.
- The 4m wall is a height diagram. No battle/courtyard screenshot was invented. The 0.5x map test is an explicit proxy, not a measured current battle-camera zoom. Night/beam panels are contrast illustrations, not Unity lighting verification.
- Selection precedes production animation sheets, final cell/pivot decisions, implementation and actual in-game camera/light testing. Claude owns integration and commits.
