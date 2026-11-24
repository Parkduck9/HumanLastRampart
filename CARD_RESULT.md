# K-100 — Wave pacing UI assets

Completed assets only. No Unity launch, Game/ changes, Git writes, or collaboration-log edits.

## Deliverables

- `Docs/References/codex_k100_v1/`: 65 source PNGs: 24 static warnings (3 levels × 4 directions × 2 sizes), 24 four-frame pulse sheets, 8 phase emblems, 7 nine-slice parts, and 2 mockups.
- `preview_4x/`: 63 nearest-neighbor previews for every UI source asset.
- Warnings: 32/48 px, small group / marching main force / siege ladder. Arrows point toward the incoming source; silhouettes remain upright. Fixed cell-center pivots and unchanged alpha geometry across four frames; order dim/rise/peak/fall at 250 ms per frame.
- Phase emblems: vanguard flag, marching column, ladder, horned skull. Banner has separate Korean text space. Ink/brown/brass palette follows current HUD references.
- Timeline: nine-slice track, four independent phase segments, separate remaining-enemy fill. Remaining = alive + unspawned, divided by total. Example segment widths do not prescribe durations.
- `mockup_battle_night_1920x1080.png`: composited from the repository's existing battle screenshot, world RGB brightness multiplied by 0.65, existing HUD and new UI at normal brightness. Not a runtime capture or physical lighting simulation.
- `mockup_map_timeline.png`: map layout example with a 62% remaining-enemy strip.
- `manifest.json`, Korean `README.md`, reproducible `build.py`, `verify.py`, `verify_review.cjs`, validation JSON, and desktop/mobile review screenshots.
- `_HTML/6_그림_화면/codex_K-100_전투단계경고UI_v1.html`: Korean, self-contained offline review with all 24 pulse animations, pause/resume, current in-game HUD references, emblems, slice specifications, and mockups.

## Verified

- All 65 source PNG dimensions and RGBA modes; binary alpha for UI assets; all 63 exact nearest-neighbor 4x previews.
- All 24 pulse sheets: fixed alpha silhouette/anchoring, four cells in documented order, static image equals rise frame, transparent outer cell perimeter. Rise/fall deliberately share the same appearance.
- All nine-slice borders fit source dimensions. Character foot lines and 96×128 / 288×384 character sheets are not applicable to this UI-only card. No existing game sheet was replaced.
- Visually inspected battle/map mockups and enlarged warning/emblem assets for stray pixels, direction separation, and legibility.
- Headless Chrome opened the local HTML file: all 12 embedded images and 24 pulse backgrounds decoded, animation advanced, pause/resume worked, reduced-motion began paused, no JavaScript errors, no page overflow at 390 px width. Review screenshots saved.
- Read-only Git status/diff inspection: authored work is confined to the requested output directory, review HTML, and this result file. Launcher-generated `CARD_CODEX_LOG.jsonl` was not edited by the asset scripts.

## Integration / leftovers

- Claude must import and wire the assets, implement phase timing and 5-second warning conditions, choose world-to-screen direction projection, and validate actual Play/night readability. No runtime validation was performed here.
- Manifest uses top-left pixel coordinates; Unity slice borders are explicitly left/bottom/right/top. Use Point filtering and no compression. Fill should be clipped to the remaining fraction and hidden at zero rather than stretched below its border minimum.
- No blocking questions. Design approval remains with the user; mockups are illustrative, not implemented game behavior.
