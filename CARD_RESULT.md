# K-067 result

Completed the upgrade-shop, training, mercenary and shared-mark artwork package.

## Work performed

- Drew 26 distinct native 32×32 RGBA pixel-art icons: 11 items, 9 training ranks, 2 mercenaries and 4 shared marks.
- Used the existing K-059 coordinate-drawn UI workflow, the current HudArt gold/cannon icons and UiTokens colors as references. No image-generation service or external artwork was used. There is no `codex_k033*` reference folder in this worktree; the existing Game UI icons were inspected directly instead.
- Kept training class emblems identical across ranks, with 1/2/3 separate rank pips. Mercenaries use the corresponding emblem plus a contract coin.
- Created exact nearest-neighbor 2×/4× previews and a Korean-labeled contact sheet showing every icon on parchment and dark wood.
- Created a self-contained Korean review HTML with every icon on both surfaces at 1×/2×/4×, category filters and upgrade-row mockups using actual asset names and prices. Mercenary prices and wages in the mockup are explicitly per person.

## Files changed

Only new files under these authorized locations were written:

- `Docs/References/codex_k067_v1/`: 26 native PNGs named after the data assets/shared marks; `preview_2x/` and `preview_4x/` containing 26 PNGs each; `contact_sheet.png`; `manifest.json`; Korean `README.md`; `build_icons.ps1`, `build_review.ps1`, `verify_assets.ps1`, `verify_review.ps1`; `qa_assets.json`, `qa_review.json`; three browser review screenshots (`review_desktop.png`, `review_mobile.png`, `review_training.png`).
- `_HTML/6_그림_화면/codex_K-067_강화아이콘_v1.html`.
- `CARD_RESULT.md` (this file).

No Game files, existing references, shared index pages or collaboration logs were changed. No Git writes were performed. Worktree HEAD was read as `refs/heads/card/K-067`.

## Verification performed

- All 26 native images: exactly 32×32, nonempty, binary alpha, at least one transparent pixel around every edge, foreground bounding-box center within 2.5px of canvas center, colors restricted to 11 existing UiTokens colors.
- All 52 previews: checked every pixel against the native image's exact 2× or 4× replication.
- Verified 22 data asset basenames map exactly to the 22 item/training/mercenary icons, all 26 native images are distinct, and training emblem pixels above the rank region match across levels.
- Opened the local HTML in headless Chrome: 26 cards, 156 comparison images, 10 mock rows, zero broken images and zero external image/link dependencies. Exercised all five category filters and checked their counts (26/11/9/2/4) and pressed state.
- Checked 1440px and 390px viewports for document/component horizontal overflow; none found. Saved browser screenshots and visually inspected the desktop, mobile and training views, plus the full contact sheet.
- No Unity editor, Play Mode, in-game capture or integration test was run.

## Open questions / leftovers

- No blocking questions. Artwork remains a v1 review proposal; user acceptance and Claude's Game integration remain outside this card (D-074).
- Shared `badge_per_wall.png` is intended for the shield rail. The current surgeon-tent asset has `perWall: 0`; it deliberately receives no per-wall mark.
- Use the native PNGs for integration. Point filtering, no compression and no mipmaps are recommended. Preview PNGs are review artifacts. Unity import metadata was not created because Game changes are excluded.
- The review HTML is not registered in Claude-owned index pages, which were outside the authorized scope.
