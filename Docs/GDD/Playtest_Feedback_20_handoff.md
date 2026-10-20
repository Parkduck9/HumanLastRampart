# Handoff: Playtest Feedback 20 (for Codex if Claude runs out of tokens)

- Author: Claude, 2026-10-20. Korean design (source of truth for intent): `Docs/GDD/Playtest_Feedback_20_v1.md`.
- Status: **design draft, waiting for user decisions** (questions in section 22 of the Korean doc). Do NOT implement decision-dependent items before the user answers. Bug items in Tier 1 need no decision.
- Ownership: normally code is Claude's (D-074). Codex takes over code ONLY if the user explicitly says so (RULES 3.4). Always: reports to the user in Korean; AI-to-AI notes in English; commit only the files you changed, file by file; never switch branches; update `Docs/GDD/ROADMAP.md` + `Docs/collab/view/ROADMAP.html` after each item.
- Work loop: follow `Docs/GDD/Loop_Engineering_v1.md` section 3 (write the done-condition first, max 3 attempts, then report). Progress log: append to `Docs/collab/live/claude/loop_log.md` (if Codex takes over, use `Docs/collab/live/codex/` for its own log).

## How to test (Unity 6000.3.25f1, close the editor first)

```
$u='C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'; $g='C:\Unity\1\HumanLastRampart\Game'
# edit tests (skip heavy probes)
& $u -batchmode -projectPath $g -runTests -testPlatform EditMode -testFilter "!SimProbe" -testResults edit.xml -logFile edit.log
# play tests (photos go to $env:HLR_PLAY_OUT)
& $u -batchmode -projectPath $g -runTests -testPlatform PlayMode -testResults play.xml -logFile play.log
```
- Baseline 2026-10-20: edit 77 (76 pass, 1 inconclusive), play 32/32.
- `Tools/NightTest/run-night.ps1` refuses to run when Unity Hub (`unity.exe serve`) is running — known false positive; call Unity directly as above or fix the check to match only `\Editor\Unity.exe`.
- Photo check: `PlayProbe.FullLoop` saves `loop_*` PNGs (prep yard, trap workshop, terrain tab incl. free placement `loop_11b/11c`).

All paths below are under `Game/Assets/_Project/`.

## Tier 1 — bugs, no decision needed

### #8 Gunner dry-fire animation
- Cause: `Combat/Battle/BattleSim.Poses.cs:125` aim branch triggers when `0 < sCooldown < lead` (lead = 3/fps = 0.375s for guns). When no target is found, `BattleJobs.cs:602-606` sets cooldown = `RetryInterval` 0.25s (`BattleSim.cs:47`) → soldier loops aim frames forever without firing.
- Fix: add per-soldier "has target" flag written by `SoldierFireJob` (false when `best < 0`, true on fire/target found); gate the aim branch on it (cannons already gate on `sIdle`). Also apply `HeroSideIntervalMultiplier` in the pose interval (Poses.cs:117) like the job does (BattleJobs.cs:415,439).
- Add a small additive muzzle-flash sprite per gun shot (no light-count limit). Lights: `BattleLights.cs` GunFlashMax 6 / FlashCapacity 28 (guns added last → starved).
- Done when: edit+play tests pass; a probe/test asserts a gunner with no enemy in range stays in idle pose.

### #10 Side cannons fire backwards
- Cause: `BattleJobs.cs:545-553` cannons skip the front-arc test and only check field quadrant (`reach` bits, `WallGeometry.FieldSide`); `BattleSim.Cannons.cs:69-85` gives every cannon an adjacent field. Same rule in map aim `CannonCanReach` (Cannons.cs:88-110) and preview `BattleView.cs:956`.
- Fix: require target in front of the cannon's own wall line: `dot(pos, Outward(side)) > CastleHalf + margin` (adjacent field allowed only if that holds). Apply in all three places.
- Sprite: `BattlePropView.cs:288-329` recoil cannon is camera-facing; set `_FixedRight` per wall (or flip by `dot(Outward, camRight)`) so the barrel points outward.
- Done when: test: north cannon never selects an enemy with `dot(pos, north) <= CastleHalf`; photo of side wall cannon firing outward.

### #14 Barracks count / −10 / squad type
- `Meta/PreparationPanel.cs`: `count` (:31) never reset. Reset to 1 in wall tab (:89), post cell (:149), unit chip (:176) handlers and after successful commit (:302).
- Add "인원 −10" button next to :185. Clamp max to remaining room (cap − existing squad count); cannon posts use `CannonCrewPostCapacity` (as :148).
- Show squad type in post cell label (:147-153): e.g. "총병 분대 7/30" using `squadAt.UnitDefId` → UnitDef.DisplayName (+ icon).
- Update `PlayProbe.FullLoop` keys if labels change (`LoopKeys.PlusTen` = "인원 +10").

### #15 Purchase "done" stamp locks the button
- `Meta/UpgradePanel.cs:243-249`: `commit.interactable = can && done == null`. Make `done` a transient notice only; button = `can`, label from next level. Same pattern: shop Buy (:195-199), mercenary hire (:306-312), `PreparationPanel.cs:302` (recruit/repair).

## Tier 2 — small, after decisions

- #20 First north group spawns 60–70 m from wall within 0–2 s (`BattleSim.cs:48-49,364,379-384`; or a per-wave "first group distance" field). Cannon scale ×(decision, rec. 1.5): `BattlePropView.cs:38` RecoilCannonSize; widen crew offsets `CannonCrewAlong` (BattleSim.cs) and cannon-bay slot removal (`BattleSim.Slots.cs:73-76`); shrink cannonball draw size 1.6 (`BattleJobs.cs:1149`).
- #2 Overlap: per-wall capacity check in `Groups.cs:72-163` (send only what fits, notice "가득 참"); overflow spread across whole wall instead of 9 stacked spots (`BattleSim.Slots.cs:116-120`); wider row insets (`:16`).
- #11 Path (decision): attracting path = in `TerrainField.Steer` (`BattleSim.Terrain.cs:327-367`) enemies within ~15 m of a path cell follow the flow field even when unblocked; or remove the Path action.
- #1 Night (decision): `BattleSim.Weather.cs:52-63` night brightness 0.5→0.7; `BattleView.cs:665` ambient floor; `Combat/Render/BattleLook.cs` ExposureNight −0.15→+0.1, VignetteNight 0.38→0.25; torches `BattleLights.cs:15-25` 0.8→1.4, range 2→4; optional night rim on enemies in `PerfLabSprite.shader`.

## Tier 3 — features

- #12/#13 Terrain pieces snap to grid, exactly 2 cells, rotate H/V (replaces today's free 6 m piece, commit 0d9873c). Reuse `Meta/FreePlacementInput.cs`, ghost + cover drawing in `Meta/TrapPreparationPanel.cs`. Data: `TerrainWork` Free/X/Z/Rotation (`Core/Campaign/TerrainWork.cs`) → cover = the two cells; `TerrainRules.FreeCover` in `Core/Terrain/TerrainLayout.cs`. Keep old saves loading. #13 autotile: choose wall/hill piece variant by 4-neighbour mask (battle `Combat/Battle/BattleFieldArt.cs` RefreshTerrainBoards/RefreshFreeWalls, map icons in the panel). Needs Codex art card: earth-wall end/middle/corner/T × 3 damage stages + hill single/joined, battle + map versions.
- #9 Free trap placement: SlotId "free:x:z"; change `Meta/TrapPreparationTransactions.cs` IsPlaceable (:79), Occupied (:87, distance test on ActualX/Z), TryCommit (:117-124), CanBuyMany (:142-149), Validate (:198-201, accept free ids); `TerrainWorkTransactions.cs` overlap uses `layout.CellOf(trap.ActualX, trap.ActualZ)`. Battle already uses ActualX/Z. Update probes/tests that build cell ids (SimProbe.cs:869-872, PlayProbe.cs:233-237,300,346-356, CampaignSaveTests.cs:39, BattleTrapTests.cs:36,112).
- #4/5/6 Squad selection (decision on left-drag): `Combat/Battle/BattleEntry.cs:349-406` (marker drag, pan), `BattleHud.Map.cs:467-528,643-685` (markers, popup), `Groups.cs` (10-soldier groups, MoveGroup/MoveWallGroups). Add box select, wall-label select-all, click wall to send, amount bar [전부][절반][10명] (half = ceil(groups/2)).
- #16 Repair map: reuse trap-map ground composition (`TrapPreparationPanel.ComposeGround`), colour 4 wall edges by Hp/MaxHp (`campaign.Wall` 4 segments), click wall → repair options (existing quotes in `PreparationTransactions.TryRepair`).
- #7 Grounding: blob contact shadows under props/enemies/earth walls, dirt decal under walls, shadow distance 50→~90 m (`Assets/Settings/PC_RPAsset.asset`, measure FrameCost). Optional PBR-lite in `PerfLabSprite.shader:282-324` (normal map + roughness spec, hemisphere ambient, fake bounce light near big flashes).
- #18 Prep yard: pre-rendered background + invisible ground plane (shadow catcher) + few box colliders + occluder cut-outs + trigger zones per station; hero = billboard sprite walking in 3D. Replace `Meta/PrepYard.cs` strip; keep station actions from `Meta/PrepEntry.cs:29-54`. Needs Codex background art + occluder masks.
- #17/#19 Tutorial: data-driven step list (name, text, highlight key, done condition, locked inputs), saved "tutorial done" flag. Stage 1 north-only (`Data/Combat/Waves/wave_1.asset`), daytime. Prep forced order: repair → trap → barracks (+10) → shop (buy & equip telescope lens) → stairs. Lens new effect: after releasing the telescope, spotted dots stay on the map 5 s fading (`Combat/Battle/BattleSim.Telescope.cs`, `BattleView.cs:24-26,794-817`, shader lens branch).

## Open decisions (copy of Korean doc section 22, short)
1 night brightness level · 2 full wall: keep overflow vs refuse · 3 UI mockup order · 4 left-drag: box select vs pan · 5 lighting scope · 6 traps fully free vs grid · 7 path attract vs remove · 8 diagonal 2-cell pieces · 9 repair per wall vs per segment · 10 battle tutorial 8 steps · 11 lens effect & price · 12 yard walking L/R only vs depth · 13 cannon scale · 14 adopt loop engineering v1
