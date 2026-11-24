# K-066 result

## Completed

Redrew standing references using the user-selected FIRST hero v6 image, `Docs/References/codex_hero_octopath_review_v6/generated.png`, following D-080 correction. The reduced-head corrected hero and K-063/K-064 are explicitly labeled comparisons only.

Delivered eight transparent native sprites and exact nearest-neighbor 4x exports: front and left-profile idle for club goblin, empty-handed ladder goblin, ladder-carrying goblin, and ogre. Preserved existing species colors, eye colors, clothing, and weapons through source-sheet references and actual RGB palette restriction. Goblin is small and hunched; ogre is broad and heavy. Human comparison uses the existing archer rear idle artwork at 52 pixels.

Created a Korean offline review page with 55 embedded images, 1x/4x controls, common foot baselines, selected hero comparison, human size comparison, K-063/K-064 comparison, and current artwork before/after. Missing existing side/empty-handed artwork was not fabricated as a before image.

## Files changed

- `Docs/References/codex_k066_v1/`: updated existing README, measure.ps1 and hero grid/4x; added generation sources, proportion revisions, selected sources, eight final sprites and 4x exports, comparison PNGs and 4x exports, measurement/palette/manifest/prompt JSON, reproducible build/review scripts, validation reports, and browser screenshots.
- `_HTML/4_그림_캐릭터/codex_K-066_적6판_다시_v1.html`: replaced the existing placeholder with the complete self-contained Korean review page.
- `CARD_RESULT.md`: this handoff.

No writes to Game/, collaboration live files, other reference folders, or other review pages. No Unity launch, git writes, commit, or branch change. `CARD_CODEX_LOG.jsonl` was present in read-only git status and was not edited by this work.

## Measurement and coordinates

Selected source: 1024x1536 RGBA. Alpha >=128 body bounds (266,224)-(765,1391), exclusive maximum, 499x1167 pixels. Hair-top to chin approximately 386 source pixels, 33.1% of stature. Source blocks visually suggest roughly 16-pixel spacing; whole-source nearest-neighbor reduction to 64x96 yields approximately 73 figure pixels. The generated source lacks a perfectly uniform recoverable grid.

The brief's 52-pixel figure is explicitly an output normalization, not falsely reported as an automatically recovered native height: approximately 22.44 source pixels per output pixel, head about 17 pixels, predominantly one-pixel outline. README and review explain this discrepancy.

| Subject | Cell | Center / foot boundary | Body | Estimated head |
|---|---|---|---|---|
| Hero normalization | 64x96 | x32 / y88 | 52 | 17 / 32.7% |
| Goblin | 64x96 | x32 / y88 | 40 | 15 / 37.5% |
| Empty ladder goblin | 64x96 | x32 / y88 | 42 | 15 / 35.7% |
| Carrying ladder goblin | 64x96 | x32 / y88 | about42, 54 with ladder | 14-15 / 33.3-35.7% |
| Ogre | 96x112 | x48 / y104 | 68 | 24 / 35.3% |

Head boundaries are manually marked semantic estimates with about one-pixel uncertainty. Image generation handled anatomy revisions. Final conversion uses aspect-preserving nearest-neighbor resizing, palette restriction, alpha thresholding and placement, without separately stretching anatomy.

## Verified

- All eight final sprites: cell sizes, last opaque row = foot boundary minus one, binary alpha, source RGB palette membership, every 4x4 block matching its native pixel. SHA256 in manifest.json.
- Inspected front/profile lineups. Used an empty-column split to prevent adjacent-pose fragments, including the ogre.
- Actual Chrome headless local-file rendering: 55 embedded images decoded, zero page script errors, 1x switch works, foot positions coincide at 1x and 4x, no whole-page horizontal overflow at mobile width390. Desktop4x and goblin/ogre row screenshots visually inspected.
- Details: validation.txt, review_validation.json, review_*png.
- Unity Play and in-game capture NOT performed. No animation, normal maps, or Game integration claimed.

## Questions / leftovers

1. Review whether goblin head37.5% is close enough to hero33% while retaining the species silhouette. Other new heads are approximately33-36%. Outlines are predominantly one pixel, with thicker dark masses at overlaps.
2. Empty/carry ladder variants share a derived identity but are not pixel-identical animation frames. Unify hair, hands, clothing details and ladder contacts before subsequent motion sheets.
3. The brief's approximate52 native claim differs from the visible source's roughly16-pixel grid / 73-pixel reduction. This delivery honors the52-pixel production scale and documents both measurements.
4. Await user visual selection before motion sheets, normal maps and Claude's D-074 Game wiring. Claude handles commits and review-index synchronization.
