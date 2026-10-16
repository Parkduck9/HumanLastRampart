# 유지보수 안내 (Human Last Rampart)

- 작성 Claude · 2026-10-16 · 카드 K-054 · 기준: develop(K-050 합친 뒤)
- 이 문서의 내용은 **코드와 데이터에서 직접 확인한 것만** 적었다. 경로는 따로 적지 않으면 `Game/Assets/_Project/` 아래다. 줄 번호는 바뀔 수 있으니 이름으로 찾는다.
- 사용자용 보기: `_HTML/1_진행과규칙/19_유지보수안내.html`

## 1. 폴더와 어셈블리

### 저장소 맨 위
| 위치 | 내용 |
|---|---|
| `Game/` | 유니티 프로젝트 (유니티 6000.3) |
| `Docs/collab/rules/` | RULES·DECISIONS·CONTRACTS (규칙·결정·데이터 약속) |
| `Docs/collab/live/` | 두 AI의 작업 기록 (git 제외, status.html만 추적) |
| `Docs/GDD/` | 설계(ARCHITECTURE·ROADMAP·CYCLES·screens/…)와 이 문서 |
| `Docs/References/codex_*` | Codex 그림 시안 (README·manifest·원안·검토 그림) |
| `_HTML/` | 사용자 검토 페이지: 0_전체목록, 1_진행과규칙, 2_설계, 3_시험과후기, 4_그림_캐릭터, 5_그림_전장, 6_그림_화면 |
| `Tools/NightTest/` | 밤 시험 일괄 실행 (`run-night.ps1`) |
| `Tools/SheetMeasure/` | 그림 시트의 인물 키 재기 (`measure-sheets.ps1`) |
| `CLAUDE.md`, `AGENTS.md`, `FirstRead.md` | Claude·Codex 진입 문서, 목표 |

### `Game/Assets/_Project`
| 폴더 | 내용 |
|---|---|
| `Core` | 정의(ScriptableObject), 진행 데이터(`Campaign`), 전투 입출력(`CombatContracts`: BattleSetup·BattleResult), 화면 ID(`Flow`), 지형(`Terrain`), 함정(`Traps`), 경제 |
| `App` | 화면 흐름(`GameFlow`·`FlowRouter`), 데이터 조립(`BattleSetupFactory`·`CampaignFactory`·`DefinitionLookup`), 개발용(`DevHarness`) |
| `Frontend` | 0번 메인 메뉴 |
| `Story` | 5번 스토리 화면·데이터(`StoryScript`)·제작 도구(Editor) |
| `Combat` | 1·2번 전투: `Battle/`(계산·그리기·HUD·카메라), `PerfLab/`(그림 규격·셰이더·성능 시제품), `Render/`(화면 효과), `Tests/`, `_PlayProbe/`, `_SimProbe/` |
| `Meta` | 4번 결과(`ResultEntry`·`ResultBoard`·`BattleResultApplier`), 3번 정비(`PrepEntry`·`PrepYard`·수리·모집·함정·지형 손질) |
| `UI` | 공용 화면 바탕(`ScreenEntryBase`·`ScreenView`) |
| `Art` | `Characters/`(정의에 연결하는 시트), `Battle/Resources/BattleArt`(이름으로 읽는 전장 그림), `UI/Resources` |
| `Data` | 정의 에셋: `Combat/{Enemies,Posts,Rules,Spells,Start,Terrains,TrapSlots,Units,Waves,Weapons}`, `Meta/`, `Traps/` |
| `Scenes` | Boot, MainMenu, Story, Battle, Result, Prep |

### 어셈블리 (asmdef → 참조)
- `HLR.Core` → 없음 (모두가 참조하는 바닥)
- `HLR.UI` → Core · `HLR.App` → Core, InputSystem, UI(유니티)
- `HLR.Frontend` / `HLR.Meta` / `HLR.Story` → Core, HLR.UI (+UI·InputSystem)
- `HLR.Combat` → Core, Burst, Collections, Mathematics, InputSystem, URP
- 편집기 전용: `HLR.Combat.Editor`(그림 가져오기 규칙), `HLR.Story.Editor`(스토리 제작 도구)
- 시험: `HLR.Core.Tests`, `HLR.App.Tests`, `HLR.Combat.Tests`, `HLR.SimProbe`(편집 모드 측정), `HLR.PlayProbe`(플레이 모드)

## 2. 화면 흐름
- 화면 ID: `Core/Flow/ScreenId.cs` — `MainMenu, Story, Battle, Result, Prep` (씬 이름과 같다)
- 관리자: `App/Flow/GameFlow.cs` (Boot 씬). 화면 씬을 하나씩 더해 불러오고, 씬마다 `IScreenEntry`가 정확히 하나 있어야 한다. 씬이 없으면 개발용 임시 화면
- 다음 화면 표: `App/Flow/FlowRouter.cs` `Next()`
  - 메인 → [게임 시작] → 스토리(새 진행 데이터) / [종료]
  - 스토리 끝 → 패배 결말이면 메인(진행 버림), 아니면 **전투**(`PrepareBattle` → `BattleSetupFactory.Create`)
  - 전투 끝 → 연습이면 같은 번호 전투 다시(D-063) / **승리면 결과**(전투 번호 +1) / **패배면 결과를 건너뛰고 패배 결말 스토리**(`GameFlow.DefeatStoryId = "story_defeat"`)
  - 결과 → 정비 → 다음 웨이브가 있으면 전투, 없으면 메인 (1회차 = 4전투)
  - 어디서든 `BackToMainMenu` → 메인 (진행 버림)
- 화면 입구: `Frontend/MainMenuEntry`, `Story/StoryEntry`, `Combat/Battle/BattleEntry`, `Meta/ResultEntry`, `Meta/PrepEntry`

## 3. 전투 계산 구조
### 프레임 순서
- `BattleEntry.Update`: `dt = min(Time.deltaTime, 1/20) × 배속` → `sim.BeginFrame(dt)` → 끝났으면 결과, 아니면 `sim.Schedule(dt, Time.time)`. 일시정지·쉬움에서 지도 열림이면 시간 멈춤
- `BattleEntry.LateUpdate`: `sim.Complete()` → `view.ApplyAtmosphere` → `view.Draw`
- `BeginFrame`(메인 스레드, 작업이 끝난 뒤): 미뤄 둔 명령 → 날씨 → 성벽·병사 피해 반영(`KillSoldiersFromDamage`) → 등장(`Spawn`) → 전령 → 묶음 이동 → 총안 걸음 → 우회 → 마법사 → 마법 → 함정 → 승패

### 작업(Job) — `Combat/Battle/BattleJobs.cs`, 모두 Burst, `BattleSim.Schedule` 순서
1. `EnemyStepJob` 적 이동·등반 → (지형이 있으면) `TerrainHitJob` → `SideTallyJob` 면별 집계
2. `BattleAssignCellsJob` → `BattleBuildGridJob` 4m 공간 격자
3. `SoldierFireJob` 병사 사격(표적 고르기, 대포 불붙이기)
4. `AllocateShotsJob` 투사체 풀 → `MoveShotsJob` 이동·착지 → `ApplyBattleHitsJob` 피해·사망(세대값으로 한 번만)
5. `WriteBattleEnemyInstancesJob`, `WriteShotInstancesJob` 그리기 데이터

### 배열·분대·묶음 (`BattleSim.cs`, 모두 NativeArray)
- 적: `ePos, eVel, eState, eClimb, eSide, eSpeed, eStop, eKind, eHp, eMaxHp, eGen, eIncoming, eAnim, eGroup` — 같은 종류끼리 이어 붙여 종류마다 한 번에 그린다(`enemyGroups`). 적 상태는 `EnemyState`(Waiting·Walking·AtWall·Climbing·OnWall·Dead), 죽은 슬롯은 다시 쓰지 않는다
- 병사: `sPos, sAlive, sWeapon, sCooldown, sSide, sAway, …` + 부분 파일의 `sAim, sHasAim, sIdle, sCannonReach, sFireTime, sFuse, sPendingShot`. 병종끼리 이어 붙임(`soldierGroups`). 대포 분대는 맨 앞 병사만 무기(나머지 `sWeapon = -1`), 병사는 뒤에서부터 잃는다
- 분대: `SquadInfo`(State·Side·FirstSoldier·Alive·Start·Movable·UnitName·SoldierHp). **10명 묶음**으로 나눈다(`BattleSim.Groups.cs` `GroupSize = 10`, 대포 분대는 한 묶음). 결과(`BuildResult`)에서 원래 분대별로 다시 더한다
- 투사체 풀: `shotCapacity = Clamp(병사 수 × 4, 1024, 65536)`

### 부분 파일 (`BattleSim.*.cs`)
| 파일 | 하는 일 |
|---|---|
| `BattleSim.cs` | 준비·프레임·승패·결과 |
| `.Burn` | 불바다 화상, 투사체 출발점 |
| `.CannonFx` | 대포 발사 효과용 읽기 |
| `.Cannons` | 대포 지도 조준 (성벽마다 4문, 닿는 들판) |
| `.Commands` | 성벽별 명령·전령·소리 지르기, 주인공 속도 |
| `.Flank` | 정면·우회 무리, 우회 보고 |
| `.Groups` | 10명 묶음 성벽 간 이동 |
| `.Ignite` | 대포 불붙이기 시간 |
| `.Poses` | 사격·쓰러짐·오르기 그림 상태 (그리기 전용) |
| `.Runners` | 성벽 위를 달리는 병사 그림 |
| `.Slots` | 총안 자리·빈자리 채우기 걸음 |
| `.Spells` / `.Wizard` | 마법(폭발·불바다), 마법사 자리·이동 |
| `.Telescope` | 지도 망원경 |
| `.Terrain` | 무작위 지형·토벽 반응 |
| `.Traps` | 전투 중 함정 |
| `.Weather` | 날씨·밤낮 |

- `BattleEntry`: 전투 화면 입구 — 계산(`BattleSim`)과 그리기(`BattleView`)를 만들고 입력(Tab/M 지도, 마법, 대포, 묶음 끌기, 망원경)을 받는다. 결과를 `Campaign.LastBattle`에 쓴다
- `BattleView`: 땅·성벽·빛, 적·병사·투사체·주인공을 GPU 인스턴싱(`HLR/PerfLabSprite` 셰이더)으로 그린다

## 4. 자주 고치는 수치 — 어디에 있나
> **데이터 에셋 값이 코드 기본값보다 우선한다.** 정의 클래스(`Core/Definitions/*.cs`)의 기본값은 새 에셋을 만들 때만 쓰인다.

| 무엇 | 파일 | 이름 (지금 값) |
|---|---|---|
| 난이도 적 수 배율 | `Data/Combat/Rules/battle_rules.asset` | `easyEnemyMultiplier` 0.75, `easyEnemyMultiplierByBattle` [0,0,0,0.6] (0 = 기본값), `realisticEnemyMultiplierByBattle` [1.5,1.5,1.5,1.5], `spawnPerSecond` 15, `corpseHeightPerKill` 0.1 |
| 날씨·소리 지르기·패배선 | 같은 에셋 (`BattleRulesDef.cs`) | 비 공격속도 0.8, 비·소나기 불 피해 0.1·0.5, 안개·밤 시야 25·30m, 병력 손실 패배 0.7 등 |
| 웨이브 | `Data/Combat/Waves/wave_1..4.asset` | 무리마다 면(0 북·1 동·2 남·3 서)·적·수·등장 초. 수는 현실 ×1 기준 |
| 등장 퍼짐 | `BattleSim.cs` | `SpawnSpreadSeconds` 12, `FieldBand` 40 |
| 1회차 시작 | `Data/Combat/Start/campaign_start.asset` | 골드 100, 성벽 북 15000·나머지 10000, 분대 배치, 전투별 웨이브·지형 순서 |
| 무기 | `Data/Combat/Weapons/weapon_bow·gun·cannon.asset` | 피해·사거리·간격·범위·속도·거리별 명중·퍼짐 (활 10/50m/1초, 총 25/30m/2.5초, 대포 40/45m/6초 범위 4m) |
| 병종·적 | `Data/Combat/Units/*.asset`, `Enemies/*.asset` | 체력·모집비 / 체력·속도·성벽 피해·보상 |
| 마법 | `Data/Combat/Spells/*.asset` | 불바다: 반경 10, 초당 3, 15초, 대기 50, 화상 60%·초당 2.5·10초 / 폭발: 반경 12, 150, 대기 40 |
| 마법 거리·지연 | `BattleSim.Spells.cs` | `SpellCastRange` 150, `ExplosionDelay` 1, `FireTickSeconds` 0.5 |
| 대포 불붙이기 | `BattleSim.Ignite.cs` | `CannonIgniteLead` 0.5, `CannonFuseSeconds` 1 (합 1.5초), `IgniteFps` 6 |
| 투사체 출발점 | `BattleSim.Burn.cs` | `MuzzleHeightRatio` 0.6, `MuzzleForwardDistance` 0.15, `CannonMuzzleDistance` 0.7, `CannonMuzzleLift` 0.12 |
| 그림 크기·간격 | `BattleSim.cs` | `WallFigureScale` 0.25, `EnemyWallScale` 0.33, `WallScaleBand` 30, 병사 0.35m, `FunnelFieldHalfWidth` 75, 적 `DrawSize`(특수 2.0·보통 1.2) |
| 총안 자리 | `BattleSim.Slots.cs` | `SlotRowInset`, `RefillReach` 1.2, `SlotWalkSpeed` 1.2 |
| 성벽 3D 비율 | `BattleWorldView.cs` | 1안 (돌출부 0.75S, 틈 0.6S, 사격구 3.2S) |
| 지형 크기 | `Data/Combat/Terrains/terrain_1..4.asset` | 들판 160, 성 반폭 20, 고정 언덕 |
| 무작위 지형·토벽 | `Core/Terrain/TerrainLayout.cs` `TerrainRules` | 돌아감 0.5·넘음 0.3·넘을 때 속도 0.7, 토벽 체력 80·초당 10 부숨, 토벽 20골드·언덕 10골드, 언덕 4~7개, 나무 6~10무리, 바위 4~8무리 |
| 지형 켜기·끄기 | `BattleSim.Terrain.cs` | `TerrainEnabled` (측정용 static) |
| 카메라 | `BattleCameraRig.cs` | 프리셋 A·B·C(뒤·위·숙임), `HeroInset`, 시야각 40, 흔들림 |
| 지도 카메라 | `BattleMapCamera.cs` | `SideHalfHeight` 80, `CloseHalfHeight` 40, `DotPixels` 7 |
| 화면 효과 | `Combat/Render/BattleLook.cs` | 미니어처 흐림, 빛번짐, 노출, 색온도, 비네트 (낮·밤) — G 키로 끄기 |
| 횃불·폭발 빛 | `BattleLights.cs` | `TorchSpacing` 3, 세기, 폭발 빛 |
| 그림 동작 속도 | `BattleSim.Poses.cs`, `BattleEntry.cs` | 쓰러짐 0.7초, 사격 8칸/초, 주인공 걷기 6·달리기 8 |

**알아 둘 어긋남** (코드 확인 2026-10-16)
- `BattleSetup.cs`의 주석은 쉬움 배율이 0.5라고 하지만 실제 데이터 값은 0.75다.
- 언덕 반응은 D-078에 '돌아감 70%·넘음 30%'로 적혀 있지만, 코드(`BattleSim.Terrain` `Decide`)는 돌아감 50%·넘음 50%다.

## 5. 시험 돌리는 법
| 묶음 | 종류 | 무엇 |
|---|---|---|
| `HLR.Core.Tests` | 편집 | 약속·경제·지형 생성·함정 규칙 |
| `HLR.App.Tests` | 편집 | 화면 흐름 표, 실제 데이터 검사(`RealDataTests`) |
| `HLR.Combat.Tests` | 편집 | 그림 연결, 우회, 연습, 함정, 총안, 마법사 |
| `HLR.SimProbe` | 편집(측정) | 4전투 균형·배율 훑기·프레임 비용·불바다 통과율 등 14개 |
| `HLR.PlayProbe` | 플레이 | Boot부터 버튼을 눌러 돌리는 한 바퀴·화면·그림 확인과 사진 23개 |

- 유니티 안에서: Window → General → Test Runner에서 EditMode / PlayMode를 고른다.
- 일괄(밤 시험): 유니티를 모두 닫고
  ```
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools\NightTest\run-night.ps1
  ```
  - 옵션: `-ProjectPath`(기본 `C:\Unity\1\HumanLastRampart\Game`), `-UnityPath`, `-TimeoutMinutes 120`, `-SkipEdit`, `-SkipPlay`, `-SkipProbe`
  - 순서: 편집 시험(SimProbe 제외) → 플레이 시험 → `SimProbe.Fuzz10` → `SimProbe.Funnel40Sweep`
  - 결과: `Tools/NightTest/reports/<날짜_시각>/`(report.md·html, xml, 로그, 사진). 실패가 있으면 종료 코드 2
  - 직접 부를 때의 유니티 인자: `-batchmode -projectPath <Game> -runTests -testPlatform EditMode|PlayMode -testResults <xml> -logFile <log> [-testFilter <이름>]`
- 출력 위치를 정하는 환경 변수:
  - `HLR_PLAY_OUT` — PlayProbe의 기록·사진 폴더 (기본 `Game/playprobe`)
  - `HLR_PROBE_OUT` — SimProbe 결과 파일 (기본 `probe.txt`, 불바다 측정은 `probe_firefield.txt`)

## 6. 카드 작업 방식 (RULES 10.6, D-067)
- 큰 일을 겹치지 않는 카드(K-###)로 나눈다. 목록은 `Docs/collab/live/claude/cards.md`(카드 ID·건드리는 파일·가지·상태).
- 스크립트는 메인 폴더 `C:\Unity\1\HumanLastRampart\.claude\`에 있다(저장소 밖).
  - `card-new.ps1 -Card K-NNN`: develop에서 `card/K-NNN` 가지로 `C:\Unity\1\HLR-cards\K-NNN` 작업 폴더를 만든다(이미 있으면 그대로 둔다).
  - `codex-card.ps1 -Card K-NNN -BriefFile <지시 파일>`: Codex를 그 폴더에서 실행한다(규칙 머리말을 붙인다: 정한 파일만, 유니티·git 쓰기 금지, `CARD_RESULT.md` 한글 보고). 커밋은 Claude가 한다.
  - `codex-send.ps1 -Message <글>`: 돌고 있는 Codex 대화에 메시지를 넣는다.
- 카드 폴더에서는 **유니티를 열지 않는다**. 카드 가지 안의 커밋은 10.3 허락이 필요 없다. 결과는 `CARD_RESULT.md`(커밋하지 않음).
- 합치기는 스크립트가 없다. 메인 폴더에서 조정 담당이 하나씩 `git merge --no-ff card/<카드>`로 합치고(10.3 순서), 합칠 때마다 빠른 컴파일 확인을 한다. 전체 시험은 밤 시험으로 돌린다. 끝난 카드는 폴더와 가지를 정리한다.
- 이 셸 환경에는 PATH에 git이 없다. `C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe`를 쓴다.

## 7. 그림 넣는 법
### 순서 (D-064, D-074)
1. Claude가 Codex에게 목적·화면·규격을 보낸다.
2. Codex가 `Docs/References/codex_<주제>_v<N>/`(또는 `codex_k0NN_v1/`)에 시안을 만든다: README·manifest(크기·기준점·해시)·원안·검토 그림. 사용자용 검토 페이지는 `_HTML/<분류>/codex_K-0NN_<주제>_v1.html`.
3. **사용자가 확정**한다.
4. Claude가 게임에 연결한다: 그림을 `Art/` 아래로 복사하고, `.meta`를 두고, 정의 에셋 필드에 연결한다.
5. 게임 안 캡처로 시안과 비교한다: 모양·크기·방향·도트 밀도·가독성·가림.
   - 사용자 확정 전에 미리 연결하는 일도 있다("미리 연결, 사용자 검토 전" 주석). 바꿔 끼울 때는 에셋의 그림 연결 한 줄만 고치면 된다.

### 캐릭터 시트 규격 (`Combat/PerfLab/PerfLabArt.cs`)
- 한 칸 **256×384**, 발 기준점 **(128, 352)**(위에서 352px). 그림 크기로 열·행을 센다(`FromSheet`). 칸 번호는 왼쪽 위부터 `행 × 열 수 + 열`이고, 음수면 좌우 뒤집기다.
- 칸 안의 인물 키가 시트마다 달라도 같은 크기로 보이게 한다: `SheetScale = 기준 384 / 그림 인물 키`. 인물 키 표는 `Tools/SheetMeasure/measure-sheets.ps1`이 `PerfLabSheetHeights.cs`로 다시 만든다. **새 시트를 넣으면 이 도구를 다시 돌린다.**
- 주인공 4×4(행: 0 뒷모습 걷기 · 1 옆 걷기 · 2 옆 달리기 · 3 앞), 고블린 4×2(걷기·공격)
- 동작 시트는 같은 개체 버퍼에서 칸 구간으로 나눠 그린다(셰이더 `_FrameWindow`): 원래 0~15, 공격 16, 세우기 32, 오르기 48, 사격 64, 불붙이기 80, 쓰러짐 96
- 투사체 1칸 256×128(가운데 기준, +X = 앞), 사다리 256×1536(아래 1472, 실체 1152px = 4.3m)
- 노멀맵은 짝 시트와 같은 칸 배치로 둔다. 초록이 위, 가져오기 종류는 노멀맵(sRGB 끔).

### 어디에 두나
- **정의에 연결하는 시트**는 `Art/Characters/{Static,Walk,Run,Fire,Fall,Goblin,Hero,Ladder,Projectiles}`에 두고 `UnitDef`·`EnemyDef` 필드로 연결한다.
  - `UnitDef`: sheet, idle/run, normal, fire, death, cast, command, projectile, ignite
  - `EnemyDef`: sheet, normal, attack, death, deploy, climb, ladder
  - 정의는 `Data/Combat/Units·Enemies/*.asset`에서 guid로 가리킨다.
- **이름으로 읽는 그림**은 `Resources` 아래에 둔다: `Art/Battle/Resources/BattleArt/`(Cannon·Effects·Props·Sky·Terrain·World), `Art/UI/Resources/{HudArt,ScreenArt,TrapMap}`, `Story/Resources/{Stories,StoryArt}`. 가져오기 설정은 `Combat/Editor/*Import.cs`가 자동으로 맞춘다.
- `.meta`를 직접 만들 때는 같은 종류의 기존 그림 `.meta`를 복사하고 guid만 새 값으로 바꾼다(필터 점, 압축 없음, 밉맵 없음).
