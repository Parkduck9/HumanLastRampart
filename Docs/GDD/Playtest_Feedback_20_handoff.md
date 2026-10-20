# Handoff: Playtest Feedback 20 (Codex continues if Claude is unavailable)

- Author: Claude, 2026-10-20. Korean design + user decisions (source of truth): `Docs/GDD/Playtest_Feedback_20_v1.md` (section 23 = decisions).
- WHY THIS EXISTS: the user's Claude quota may run out for ~5 days; then Codex continues the CODE work alone (user-authorised substitution, RULES 3.4). Keep this file current: Claude updates the "Progress" table after every item.
- Rules for whoever continues: reports to the user in KOREAN; commit only files you changed, file by file; never switch branches (work on `develop`); after every item update `Docs/GDD/ROADMAP.md` + `Docs/collab/view/ROADMAP.html`; ask the user instead of guessing; show photos for visual changes.
- Work loop (`Docs/GDD/Loop_Engineering_v1.md` §3, adopted by the user): write the done-condition first → implement → edit tests → play tests → photo check → commit → log. Max 3 attempts per item, then stop and ask. Log one entry per attempt at the bottom of `Docs/collab/live/claude/loop_log.md` (Codex: `Docs/collab/live/codex/loop_log.md`).

## Starting Codex on code (paste to Codex in the repo root C:\Unity\1\HumanLastRampart)

```
Read AGENTS.md, then Docs/GDD/Playtest_Feedback_20_handoff.md and Docs/GDD/Playtest_Feedback_20_v1.md (section 23 = user decisions).
Claude is unavailable; the user authorises you to continue the CODE work (RULES 3.4). Work directly on branch develop in this checkout.
Take the first item in the Progress table that is not DONE, follow the work loop in the handoff, and report to the user in Korean with photos.
```

## Starting Codex on ASSET cards (credits reset; run in two terminals)

**PAUSED (2026-10-20 15:26, M-CX-124)** → **RESUMED 2026-10-20 ~16:10** (user: "코덱스 이제 일 나눠서 진행시켜"). Claude started line A `K-087,K-086,K-080,K-081` and line B `K-089,K-088,K-076,K-084`. If a line stopped, rerun only the cards without CARD_RESULT.md.
**More lines 2026-10-20 ~16:40** (user: "코덱스 초기화됐다 많이 시켜"): C `K-090,K-093,K-072` · D `K-094,K-091,K-079` · E `K-092,K-095,K-085` (K-072/079/085 = reruns of credit-stopped cards). New briefs: K-090 squad radial UI, K-091 lens afterimage + repair map, K-092 specular masks, K-093 tutorial guidance, K-094 free-trap map art, K-095 night readability. Character reference = `Docs/References/codex_hero_octopath_review_v6/generated.png` (D-080 correction, D-081).

```
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CodexCards\run-cards.ps1 -Cards K-086,K-080,K-087,K-081
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CodexCards\run-cards.ps1 -Cards K-076,K-089,K-088,K-084
```
- If a card stops with "out of credits", rerun the same command later (cards with a CARD_RESULT.md can be skipped). Previously partial: K-072, K-079, K-085 (rerun if their review pages are incomplete).

## How to test (Unity 6000.3.25f1; the editor must be closed)

```
$u='C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'; $g='C:\Unity\1\HumanLastRampart\Game'
$env:HLR_PLAY_OUT="$env:TEMP\hlr_play"
& $u -batchmode -projectPath $g -runTests -testPlatform EditMode -testFilter "!SimProbe" -testResults edit.xml -logFile edit.log
& $u -batchmode -projectPath $g -runTests -testPlatform PlayMode -testResults play.xml -logFile play.log
```
- Baseline 2026-10-20: edit 77 (76 pass + 1 inconclusive), play 32/32. Use `Start-Process -Wait` from PowerShell so the call blocks.
- `Tools/NightTest/run-night.ps1` wrongly refuses when Unity Hub (`unity.exe serve`) runs — call Unity directly or fix its process check to `\Editor\Unity.exe`.
- Photos: `PlayProbe.FullLoop` (prep/trap/terrain `loop_*`), `PlayProbe.PortfolioShots` (day/night/map `pf_*`), `PlayProbe.NightLevels` (night brightness comparison `night_level_*`).

## Progress (update after every item)

| # | Item | State | Notes |
|---|---|---|---|
| — | Commit 621c77d (2026-10-20) | DONE | items 1, 8, 10, 14, 15, 7a and cannon ×1.5 are committed and tested (edit 76/77 + 1 inconclusive, play 32/32). Rows below marked CODED are now DONE |
| 1 | Night brightness | DONE | user picked Level 0.4 (`NightTuning.DefaultLevel`) |
| 1-old | (history) Night brightness 3 photos | DONE | `Combat/Render/NightTuning.cs` (one Level 0..1 drives sprite brightness, exposure, vignette, ambient + floor, moon, torch intensity/range). Wired in BattleSim.Weather.cs, BattleView.ApplyAtmosphere, BattleLook.Apply, BattleLights torch block. Next: probe `NightLevels` captures levels 0/0.35/0.7/1 → user picks → set `NightTuning.DefaultLevel` |
| 8 | Gunner dry-fire | CODED, UNTESTED | `BattleJobs.cs` SoldierFireJob: no target → `SoldierIdle[s]=1`, target → 0; pose aim branch already skips idle. Not done: pose interval × HeroSideIntervalMultiplier, muzzle sprite (K-088) |
| 10 | Cannon backwards | CODED, UNTESTED | `SoldierFireJob.CannonFrontMargin = 2` m: cannon target must satisfy dot(d, Outward) ≥ 2 (job), same in `BattleSim.CannonCanReach` and `BattleView.CannonReaches`. Sprite facing unchanged (camera always looks at the current wall from behind) |
| 14 | Barracks count/−10/type | CODED, UNTESTED | `Meta/PreparationPanel.cs`: count reset on wall/post/unit change and after commit; "인원 −10" button; max = remaining room; post cell shows "지점 · 병종"; right column "지점 — 병종 분대 N명" |
| 15 | "done" stamp locks buy | CODED, UNTESTED | `UpgradePanel.cs` (shop/training/mercenary) + `PreparationPanel.cs` (recruit/repair): stamp is a notice in the price line, button stays usable |
| 20 | First group in 5 s, cannon ×1.5 | HALF | cannon ×1.5 CODED (`BattlePropView.CannonScale`, crew offsets 0.55/0.4 in BattleSim.cs, muzzle distance in BattleSim.Burn.cs). First-group spawn TODO |
| 7a | Remove Path & Move actions | CODED, UNTESTED | `TrapPreparationPanel.RenderTerrainTab` shows only 토벽·언덕·철거; FullLoop probe presses "할 일:철거" |
| 12/13 | 2-cell terrain pieces, 45° steps, no gap | TODO | needs K-087 art for connection; logic can start before art |
| 9 | Free trap placement | TODO | |
| 2 | Overlap: squeeze into gaps | TODO | ask user about overflow beyond minimum spacing |
| 4/5/6 | Shift box select + radial N/E/S/W picker | TODO | amount (all/half/10) placement to confirm |
| 11/19 | Lens afterimage 3–5 s | TODO | last position when a dot LEAVES the lens circle (or on release) stays ~4 s, fading |
| 16 | Repair map | TODO | waiting: user asked for a longer explanation (per-wall vs segments) |
| 7 | Contact shadows → Simple-Lit-style lighting | TODO | K-088 art; shader: Blinn-Phong spec + normal maps (K-081) |
| 18 | 2.5D prep yard | TODO | K-089 art |
| 17/19 | Tutorial | TODO | order: walk → telescope → map → squad → cannon(talk) → magic → trap → lens upgrade; focus-target & cannon = explanation only |
| 3 | UI mockups | TODO | recommendation given: in-game grey-box first, then Codex image |

## Tier 1 — bugs (no decision needed)

### #8 Gunner dry-fire animation
- Cause: `Combat/Battle/BattleSim.Poses.cs:125` aim branch triggers when `0 < sCooldown < lead` (lead = 3/fps = 0.375 s for guns). With no target, `BattleJobs.cs:602-606` sets cooldown = `RetryInterval` 0.25 s (`BattleSim.cs:47`) → aim frames loop without firing.
- Fix: per-soldier "has target" flag written by `SoldierFireJob` (false when `best < 0`, true when a target is chosen); gate the aim branch on it (cannons already gate on `sIdle`). Pose interval should apply `HeroSideIntervalMultiplier` like the job (BattleJobs.cs:415,439). Add a muzzle-flash sprite (K-088) per shot.
- Done when: tests pass + a test asserts a gunner with no enemy in range stays idle.

### #10 Side cannons fire backwards
- Cause: `BattleJobs.cs:545-553` cannons skip the front-arc test (quadrant bits only); `BattleSim.Cannons.cs:69-85` gives every cannon an adjacent field. Same in `CannonCanReach` (Cannons.cs:88-110) and map preview `BattleView.cs:956`.
- Fix: require `dot(pos, Outward(side)) > CastleHalf + margin` in all three. Sprite: `BattlePropView.cs:288-329` set `_FixedRight` per wall so the barrel faces outward.

### #14 Barracks
- `Meta/PreparationPanel.cs`: reset `count = 1` in handlers at :89, :149, :176 and after commit (:302); add "인원 −10" next to :185; clamp to remaining room (cannon posts: `CannonCrewPostCapacity`); show squad type in post cells (:147-153) e.g. "총병 분대 7/30".

### #15 "done" stamp
- `Meta/UpgradePanel.cs:243-249` (`commit.interactable = can && done == null`) → button = `can`, `done` only as a notice. Same in shop (:195-199), mercenary (:306-312), `PreparationPanel.cs:302`.

## Tier 2/3 details
- #20: first north group 60–70 m from wall within 0–2 s (`BattleSim.cs:48-49,364,379-384`). Cannon ×1.5: `BattlePropView.cs:38` RecoilCannonSize, widen `CannonCrewAlong` and cannon-bay slot gap (`BattleSim.Slots.cs:73-76`), cannonball draw 1.6 → ~1.0 (`BattleJobs.cs:1149`).
- #7a: `Meta/TrapPreparationPanel.cs` RenderTerrainTab chips (5 → 3), `Meta/TerrainWorkTransactions.cs` TerrainAction enum keeps Path/Move values for saves but UI hides them; tests in `PlayProbe.FullLoop` press "할 일:길 내기" (change to Demolish or remove).
- #12/13: replace today's free 6 m piece (commit 0d9873c). Piece = anchor cell + direction (8 dirs, 45°): straight = anchor + E/N neighbour, diagonal = anchor + NE/NW neighbour; store in `TerrainWork` (Free=true, X/Z = anchor cell centre, Rotation = 0/45/90/135). `TerrainRules.FreeCover` → the two cells. Diagonal: flow field must not step diagonally between the two blocked cells' free neighbours across the shared corner (`BattleSim.Terrain.cs` Dijkstra neighbour loop). Connection art via K-087 (hub + 8 arms on the map; joint posts + 5 m / 7.07 m boards in battle).
- #9: SlotId "free:x:z"; `Meta/TrapPreparationTransactions.cs` IsPlaceable :79, Occupied :87 (distance test on ActualX/Z), TryCommit :117-124, CanBuyMany :142-149, Validate :198-201 (accept free ids); `TerrainWorkTransactions.cs` trap overlap via `layout.CellOf(ActualX, ActualZ)`. Battle already uses ActualX/Z. Fix probes/tests that build cell ids (SimProbe.cs:869-872, PlayProbe.cs:233-237,300,346-356, CampaignSaveTests.cs:39, BattleTrapTests.cs:36,112).
- #2: `BattleSim.Slots.cs` — when a wall is over capacity, recompute slot pitch so all soldiers fit evenly (min spacing ~0.25 m), instead of stacking 9 overflow spots (:116-120).
- #4/5/6 (user-confirmed): `Combat/Battle/BattleEntry.cs:349-406` (map input), `BattleHud.Map.cs:467-528,643-685`, `Groups.cs`. (a) Shift+left-drag draws a box selecting squad markers; ON RELEASE a round 4-quadrant picker appears at the cursor (up=N, right=E, down=S, left=W); clicking a quadrant sends all selected squads there. (b) Left-click a wall (N/E/S/W) on the map → amount wheel like the middle-click command wheel: [1분대 보내기][1/2 보내기][전체 보내기] → then the same 4-quadrant picker for the destination. Plain left-drag still pans. No rectangular panels.
- #11/19 lens: `Combat/Battle/BattleSim.Telescope.cs`, `BattleView.cs:24-26,794-817`, shader lens branch (`PerfLabSprite.shader:235-239`). Keep a list of (position, time) for dots that were inside the lens last frame and are now outside (or lens released); draw them ~4 s fading. Only with the lens item (`item_telescope_lens`).
- #7: contact shadow quads under props/enemies/earth walls (K-088), shadow distance 50 → ~90 m (`Assets/Settings/PC_RPAsset.asset`, measure FrameCost). Lighting: in `Combat/PerfLab/PerfLabSprite.shader:282-324` add Blinn-Phong specular (URP `LightingSpecular`) with per-kind smoothness, use normal maps (K-081). Ground/wall meshes may use URP Simple Lit.
- #18: replace `Meta/PrepYard.cs` strip with a 3D ground plane + box colliders + occluder quads + trigger boxes from K-089 manifest; hero billboard moves with WASD/arrows in X and Z; keep station actions from `Meta/PrepEntry.cs:29-54`.
- #17/19 tutorial: data-driven steps (name, Korean text, highlight key, done condition, locked inputs), saved flag; stage 1 north-only (`Data/Combat/Waves/wave_1.asset`), daytime; prep forced order repair → trap → barracks (+10) → shop (lens, gold granted) → stairs.
