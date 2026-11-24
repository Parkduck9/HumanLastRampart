# K-076 result

Created wizard front/back idle, four-frame back-view casting, and four-frame right-facing side walk assets. Used the built-in image_gen tool for the master artwork and a targeted correction. Followed D-080's first generated hero reference, K-065 wizard identity, and D-081's approximately 73-pixel human scale. D-081 resolves the brief's stale size-decision label.

Files created:
- `Docs/References/codex_k076_v1/`: four game sheets retaining requested filenames; matching `native/` sheets; four 4x previews; `master_generated.png`; `manifest.json`; Korean `README.md`; reproducible `build.ps1`; `verify.ps1`; `verify_review.cjs`; `measurements.json`, `source_hashes.json`, `validation.json`, and `review_validation.json`.
- `_HTML/4_그림_캐릭터/codex_K-076_마법사동작_v1.html`: Korean, self-contained animated comparison against the current game sheets, with pause, step, and speed controls. Walk explicitly has no existing counterpart.
- This `CARD_RESULT.md`.

Contract: native cells 96x128; game cells 288x384, exactly 3x nearest-neighbor. Foot bottom-exclusive baseline 120 native / 360 game; body/head anchor x=48 native / 144 game. Unity normalized pivot (0.5, 0.0625). Static sheets have one frame each; cast and walk have four frames left-to-right. Cast order: raise staff, gather light, release, settle. Manifest specifies frame order, filenames, sizes, and pivots.

Verified:
- All 10 frames: dimensions, binary alpha, bottom-exclusive baseline exactly 120, transparent cell edges, and exactly one 8-connected opaque component (no disconnected stray pixels).
- Every game pixel equals the corresponding native pixel scaled 3x nearest-neighbor.
- All 11 embedded PNG occurrences decode successfully.
- Executed the review JavaScript in a mock DOM: all seven animation source images load, drawing stays within sheet frame bounds, playback/pause/manual stepping work. This is NOT actual browser rendering.
- Visually inspected the final four enlarged sheets.
- SHA-256 hashes of the three source game PNGs remained unchanged.

Limitations / handoff:
- No connected browser was available; actual browser visual QA remains outstanding.
- Unity was not opened or run. Claude must import/copy the game sheets, adapt slicing from 256x384 to 288x384, wire walk/cast timing, check opposite walking direction, and verify gameplay appearance. Existing normal maps need silhouette compatibility review; no normal maps or Unity metadata were generated.
- Human height is approximate; outline and pose details vary slightly around the 73-pixel target. Casting includes compact attached cyan light. Final art still requires user visual acceptance.
- No Game files, collaboration logs, other docs, or Git state were written. No commits or branch operations were performed.
