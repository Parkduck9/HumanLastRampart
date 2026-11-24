# CODE-02 result — functional verification passed; performance evidence limited

Resumed by Codex on develop at the user's direct instruction to take over Claude's stopped work. No Git writes were performed.

## Runtime changes

- BattleSim.cs / BattleSim.Flank.cs: first attack group remains frontal, spawns within 0–2 seconds at 60–70m from the wall; later groups retain their previous band and scheduling.
- BattleSim.Slots.cs: precompute 0.25m dense slots excluding cannon bays and torch locations. Expand a crowded wall's slot table and update existing occupants and in-flight destinations/messenger homes. Remove the repeated nine-point overflow placement.
- BattleSim.Groups.cs: reserve destination capacity before departure; split only the living tail when a group fits partially, retaining original squad ID and loss totals. Reject a full destination with a Korean notice. Wall commands count actual moved soldiers and report refused counts.
- BattleStartCrowdingTests.cs (+ Unity-created meta): 8 cases from the preserved partial test draft, covering four battles, spacing beyond original slots, immediate/delayed partial arrivals and source garrison retention.

## Verification

- Standalone Combat Roslyn compilation passed. Initial invocation used the wrong working directory and was corrected; this was not a code test failure.
- Initial sandboxed Unity launch stalled at licensing initialization. Only our launched Editor PID57796 was stopped. Retried with approved environment access; no unrelated Unity/Hub process was closed.
- Targeted EditMode: 8/8 passed (`qa/targeted.xml`).
- Full EditMode: 93 passed, 1 existing inconclusive, 0 failed (`qa/edit.xml`). Unity returned exit2 despite a Passed XML run; the inconclusive was retained, not relabelled pass.
- First full PlayMode: 31/33, failures FrameTiming and PortfolioShots: both injected about4200 soldiers into a40m wall scenario beyond physical slot capacity.
- Corrected those two stress fixtures only: temporarily use80m test walls via in-memory terrain fields, restore all fields in finally, retain4200 target and add population/spacing assertions. Actual terrain asset files were not changed. Existing test assertions were not weakened or removed.
- Full PlayMode rerun: 33/33, exit0 (`qa/play_attempt3.xml`). Compilation of the modified probes occurred in this run.
- Visually inspected `49_timing_1_early.png` (crowded80m stress wall) and `58_loop_03_battle_prepare.png` (normal40m setup).

Evidence: Docs/References/codex_code02_resume_v1/qa/. Earlier failures and logs are preserved.

## Limits / next work

- Performance capture is not a valid improvement claim: test terrain changed; several GPU/draw counters were zero; the run ended near21s and the intended late magic casts failed. CPU timings exist but must not be reported as complete rendering/magic-stage validation or compared directly with the old40m baseline.
- The physical minimum applies to valid placements and incoming transfers. Invalid initial armies above the maximum slot capacity still fail explicitly; normal setup and current regression tests pass. Old custom over-capacity saved scenarios need explicit handling if they must be supported.
- User aesthetic acceptance of dense sprite overlap is separate from minimum center spacing / no identical positions.
- CODE-03 and CODE-04 remain pending. K081 inspection found four stale wizard normals, reported separately. No normal PNGs or other art were modified.
