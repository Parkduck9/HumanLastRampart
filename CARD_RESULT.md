# K-082 result

Completed the character-only prep-yard / result-board asset refresh, using the user-picked v6 `generated.png` as the style reference. No size decision was needed for this card.

## Deliverables and changed files

- `Docs/References/codex_k082_v1/result/codex_portrait_{hero,wizard,archer,gunner,cannon}.png`: five transparent 512x512 bust portraits, preserving K-028 filenames and centered bust placement (x=256, bottom=480).
- `Docs/References/codex_k082_v1/TrapMap/worker_{dig,earth,log}_{archer,gunner}.png`: six transparent 1024x384 sheets, four 256x384 cells in the original left-to-right pose order. Existing worker size and horizontal torso placement were retained to the nearest native pixel; every frame ends at y=352. The canonical cell pivot remains (128,352). The existing asymmetric tool layouts were not forcibly recentered.
- `Docs/References/codex_k082_v1/previews/`: eleven nearest-neighbor 4x previews.
- `Docs/References/codex_k082_v1/native/` and `originals/`: logical-grid processing masters and all eleven built-in image_gen outputs.
- `Docs/References/codex_k082_v1/manifest.json`: dimensions, frame order, pivots, source paths, original bounds, placement bounds and cleanup counts.
- `Docs/References/codex_k082_v1/README.md`: Korean asset, validation and integration notes.
- `Docs/References/codex_k082_v1/validation.json` and `review_validation.json`: asset and review-page check results.
- `Docs/References/codex_k082_v1/source/{export.ps1,verify.ps1,build_review.mjs,verify_review.mjs,prompts.json,generated_paths.json,source_hashes.json}`: reproducible processing, exact prompt set, provenance and checks. Re-export prefers the included originals and does not require the external generation cache.
- `_HTML/6_그림_화면/codex_K-082_정비결과6판_v1.html`: Korean, self-contained review page with current-vs-new animation pairs for all six worker motions; K-028 portraits, actual current game-sheet first cells, and new portraits shown separately. Includes pause/resume, frame selection, speed and background controls.
- This `CARD_RESULT.md`.

All generated art used built-in image_gen with transparent output requested. Processing uses nearest-neighbor sampling, binary alpha, a shared 33-color palette, one-native-pixel dark exterior outlines, and removal of disconnected islands of at most two native pixels. Large intended digging-soil fragments remain.

## Verified

- All eleven delivered PNGs decode and exactly match their corresponding source canvas sizes; all six sheets retain four frames.
- Every worker frame has exclusive alpha bottom y=352; portrait exclusive alpha bottom is y=480.
- All final images contain transparent and opaque pixels, with zero partial-alpha pixels, zero nearest-neighbor grid mismatches, at most 33 opaque colors and no disconnected islands of <=2 native pixels.
- All eleven 4x previews have the expected dimensions. Opened and visually inspected all eleven final PNGs.
- SHA-256 comparison passed for 199 existing UI-art / K-028 files; none changed.
- All 34 embedded review-page PNG occurrences decode. Mock-DOM checks passed for 17 canvas image loads (12 animated worker canvases, five current-game-sheet canvases), animation progression, manual frame selection, pause/resume, background switching and absence of external dependencies.
- Resolved the five unit definitions' texture GUIDs to the actual game sheets used in the comparison. The wizard comparison correctly uses `wizard_back_static.png`.
- No Unity execution, Game writes, background edits, collaboration-log writes, Git writes or commits were performed.

## Open questions / leftovers

- Actual browser rendering is unverified: CUA returned no browsers, and the in-app browser was unavailable. PNG decoding and mock-DOM validation are explicitly not claimed as browser rendering or actual animation playback. A browser layout / playback review remains.
- Unity import, Play testing and actual game captures remain for Claude / the user.
- `ResultBoard.cs` currently displays the first cell of `IdleSheet` or `Sheet`, rather than loading K-028 standalone portraits. Claude must wire the new result-only portraits separately; do not replace combat sheets with these busts.
- Claude can copy worker replacements into the existing TrapMap destination names while preserving the existing destination `.meta` files / import settings. No Unity `.meta` files are needed for these documentation-folder outputs.
- `Docs/References/codex_k033_v1` was absent in this worktree. Existing in-game worker PNGs supplied the replacement contract. K-028 backgrounds and old composite result images remain untouched.
- User review of appearance and motion is still pending. No new design decisions were introduced.
