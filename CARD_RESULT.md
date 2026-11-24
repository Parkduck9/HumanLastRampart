# K-086 result — blocked pending geometry decisions

This card is NOT production complete. Do not swap the current game sheet with the proof images.

Created one transparent six-frame rear-view cannon-crew source using the built-in image generator, preserving K-075 navy/cream clothing, crossed brown back straps and boots. Normalized a review-only six-frame proof and a six-frame composite with the installed cannon. Created the requested self-contained Korean review HTML with current/new animation comparison, composite animation, stepping, playback and cell/fuse-candidate guides.

## Files changed

Only new files under `Docs/References/codex_k086_v1/`, the requested `_HTML/4_그림_캐릭터/codex_K-086_대포불붙이기다시_v1.html`, and this `CARD_RESULT.md`.

The asset directory contains the generated source, enlarged cannon reference, 144x128 review-cell proof and its 4x preview, six-frame composite and its 4x preview, manifest, Korean README, build scripts, HTML template and validation scripts/results. No Game files, collaboration logs, existing documents or existing review pages were modified. No Unity or Git writes were run.

## Verified

- PNG decoding and dimensions; six proof frames.
- Binary proof alpha; all frame ground bounds end at y=120 (exclusive); no connected component smaller than three pixels.
- Visual inspection of the enlarged rear-view proof and composite.
- Embedded PNG byte equality and animation controls via Node VM DOM/Image mocks; see review_validation.json. This is not a real browser rendering test.
- Geometry: 73px / 0.35m = 208.5714 native px/m. At 0.30m right offset, a central fuse requires x = 48 - 62.5714 = -14.5714 native px, or -43.7143 game px. This is outside the required 96/288px cell.

## Open questions / leftovers

1. User approval is required to widen the cell. Proposed review layout: native 144x128 with foot pivot (96,120), game 432x384 with pivot (288,360). This adds 48 native pixels on the left without changing figure size or world foot placement. The user has been asked directly in chat; no approval has arrived.
2. The installed recoil cannon does not have an independently identifiable fuse. The manifest's approximately (128,100.43) source coordinate is a candidate on the rear upper barrel, back-projected from the proof's fourth-frame flame height, NOT an independently measured fuse. Confirm a real fuse coordinate before finalizing contact. The guide is not painted into the cannon art.
3. The worktree code differs from the brief: CannonScale=1.5 (~0.60m visible width), CannonShooterOffset=0.4, CannonCrewOffset=0.55. The proof follows the brief's 0.40m / 0.30m geometry. Claude must resolve this before integration.
4. Final native/game sheets named cannon_crew_ignite6.png were deliberately not delivered as ready: the required geometry cannot fit the approved cell. After the decision, refine body-centering/step continuity, add the contact spark, lock the approved fuse position, produce both final scales and verify exact 3x replication and per-frame fire-to-fuse contact.
5. Real browser rendering, Unity playback, normal maps, integration and game captures were not performed.
