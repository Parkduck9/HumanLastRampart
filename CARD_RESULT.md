# K-103 — Wall Braziers and Black Fog Edge

## Completed

- Created unlit wall-top brazier and low / normal / high six-frame loops. Also delivered separate flame overlays and one additive glow sprite per brightness level.
- Delivered native 96×128 brazier/flame cells and exact 3× nearest-neighbor 288×384 game cells. Fixed shared base, centre x=48, bottom pixel y=120; game foot pivot (144,360).
- Created seamless horizontal purple-black fog band, matching NW corner (rotate for other corners), and a slow four-frame wisp overlay. Included native and 3× game versions. Environmental band/corner dimensions are explicitly documented separately from figure cells.
- Created brass/ink lamp icons at exactly 32 and 48 pixels, a 32×32 nine-slice frame with 8-pixel borders, and three 288×80 selected-state HUD exports.
- Created a 1920×1080 north-wall night mockup using the existing battle reference at RGB brightness 0.65, high braziers, additive glows, outer fog, and the new HUD.
- Created a self-contained Korean review HTML with animated new braziers, existing in-game flame comparison, scrolling fog and animated wisps, pause control, night background control, and selectable HUD states.
- Preserved three built-in image_gen source atlases and complete prompts. Included Korean README, machine-readable manifest, reproducible asset assembly/verification helpers, and browser evidence.

## Files Changed / Added

All new deliverables are confined to:

- `Docs/References/codex_k103_v1/` — 61 manifest-listed PNG deliverables under native/, game/, icons/, hud/, previews/ and the root mockup; three source PNG atlases; two review screenshots; `manifest.json`, `README.md`, `prompts.json`, `build.py`, `verify.py`, `verify_review.cjs`, `verification.json`, `review_validation.json`.
- `_HTML/6_그림_화면/codex_K-103_성벽화로와안개_v1.html`
- `CARD_RESULT.md`

No changes to Game/, other docs/logs, existing review pages, branches, or Git index. No Git writes or Unity execution. The dedicated worktree remains on card/K-103. Claude owns import, integration, and committing.

## Verified

- `verify.py`: **220 checks passed**, including registered dimensions/RGBA, alpha presence, exact 3× nearest-neighbor pixels, baseline and horizontal centre for all 19 complete brazier frames, identical lower base pixels, no clipped canvas boundaries or single-pixel islands, additive glow clear borders and soft alpha, exact horizontal fog endpoint equality, exact two corner join boundaries, and transparent inner fog edge. Embedded HTML PNG data decoded successfully.
- `verify_review.cjs`: **passed** in headless Microsoft Edge using a direct file URL. All inline and CSS background images loaded; all five stepped previews changed; pause stopped them; all three toggle selections updated correctly; no JavaScript errors or horizontal overflow at 390px. Desktop 1440px and mobile 390px screenshots saved.
- Visually inspected 4× low/normal/high sheets, HUD state export, night mockup, and review capture. Removed a source-row crop artifact discovered during visual review and re-ran image/browser checks successfully.
- Read existing wall texture, torch mount, six-frame in-game flame, HUD frame, approved figure reference and existing battle screenshot. Existing game assets were used read-only.

## Integration Notes / Leftovers

- The art cannot enforce a 45m gameplay visibility rule. Place **all fog geometry, its falloff and wisps entirely beyond 45m from the wall**. Corner right edge joins band x=0; bottom edge joins its rotated counterpart. The sample clear-field proof is illustrative, not a calibrated world-distance screenshot.
- Use either a complete brazier loop OR unlit brazier plus separated flame, not both. Additive glow uses SrcAlpha One, centred at native brazier (48,72) / game (144,216). Fog uses ordinary alpha.
- World lighting/exposure at 0.65, camera scale against wall art, transparent sorting, actual enemy visibility within 45m, Unity texture import settings and Play remain untested and belong to integration. The mockup is a composition, not a new Unity capture.
- The source design document is marked proposal. This card authorizes the requested art deliverables only; gameplay balancing and broader Beacon changes have not been implemented or treated as approved APIs.
- No remaining asset-production blocker. No runtime code or Unity .meta files were generated in Game/.
