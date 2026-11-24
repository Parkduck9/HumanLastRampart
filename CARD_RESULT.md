# K-054 결과 — 계약 반영, 유지보수 안내, 로드맵 상태 점검

작성 Claude · 2026-10-16 · 가지 card/K-054 · 문서만 (코드 파일 안 건드림, 유니티 안 엶)

## 1. CONTRACTS.md (맨 아래에 추가)
- **C-004 v1.4 초안** — `CampaignState.TerrainWorks : List<TerrainWork>`(토벽·언덕). 새 형식 `TerrainWork { TerrainId, CellId, TerrainWorkKind Kind }`. 무작위 지형 씨앗은 새 필드가 아니라 기존 `RngSeed`로 다시 만든다(배치 자체는 저장 안 함).
- **C-005 v1.6 초안** — `BattleSetup.TerrainSeed`(= RngSeed), `BattleSetup.TerrainWorks`(그 지형 것만 복사). 작성은 App `BattleSetupFactory`, 읽기는 Combat `BattleSim.Terrain`.
- **C-009 r0 초안** — 스토리 데이터 형식(`StoryScript`·`StoryScene`·`StoryLine`, `Resources/Stories/<StoryId>`)과 NextStory 흐름. 지금까지 비어 있던 약속을 구현에서 옮겨 적었다.
- K-045 총안 자리·K-046 불붙이기는 계약 영향 없음으로 확인해 적었다(불붙이기 그림은 이미 C-010 v1.10).
- **사용자 승인·결정이 필요한 것** (CONTRACTS 맨 아래에도 모아 둠):
  1. 위 세 초안 승인
  2. 전투에서 부서진 토벽을 저장할지 (지금은 다음 전투에 다시 선다)
  3. 패배 결말 이야기 ID `story_defeat`(코드 상수)를 정의 데이터로 옮길지
  4. 코드와 결정이 다른 곳: 언덕 반응(D-078은 돌아감 70%·넘음 30%, 코드는 50%·50%), 나무 쓰러뜨리기(D-078 '지금 넣음' — 코드에서 못 찾음, K-051 확인 필요)
  5. `CampaignState`·`BattleSetup` 머리 주석의 판 번호가 옛것 (코드 파일이라 손대지 않음)

## 2. 유지보수 안내
- `Docs/GDD/MAINTENANCE.md` + 사용자용 `_HTML/1_진행과규칙/19_유지보수안내.html` (같은 내용, 밝은·어두운 화면)
- 내용: 폴더·어셈블리, 화면 흐름 표, 전투 계산(프레임 순서·작업 순서·배열·분대·10명 묶음·부분 파일), 자주 고치는 수치 표(파일·이름·지금 값), 시험(묶음별, 밤 시험 명령·옵션, HLR_PLAY_OUT/HLR_PROBE_OUT), 카드 방식(card-new·codex-card·codex-send, 합치기는 수동), 그림 넣는 법(256×384·발 128,352, SheetScale·인물 키 재기 도구, 칸 구간, 둘 곳, .meta)
- 모두 코드·데이터에서 확인한 값. 어긋남 2개를 문서에 적었다: `BattleSetup` 주석 쉬움 0.5 ↔ 데이터 0.75, 언덕 반응 70/30 ↔ 50/50
- `_HTML/0_전체목록.html`에는 링크를 넣지 않았다(목록은 조정 담당이 관리) — 합칠 때 '19 유지보수 안내'를 추가해 주세요.

## 3. 로드맵 — '대기'로 남았지만 실제로 끝난 항목 (ROADMAP은 직접 안 고침)

| 항목 | 지금 | 바뀔 상태 | 근거 |
|---|---|---|---|
| P1-C06 C-008 함정 약속 | [?] | [x] | C-008 r5(D-022)·r6(D-051), D-050 확정, `BattleSim.Traps`·`Meta/TrapPreparation*` (C-009 연계는 따로) |
| P1-X01 0번 메인 설계 | [ ] | [~] | `screens/MainMenu.md` r1 제안만, 승인 없음 |
| P1-X02 5번 스토리 설계 | [ ] | [x] (대체) | D-078·K-050 스토리 화면 구현 |
| P1-X03 스토리 데이터 형식(C-009) | [ ] | [~] | `StoryScript.cs` 구현, 이번에 C-009 r0 초안 — 승인 남음 |
| P1-X04 스토리 툴 설계 | [ ] | [x] (대체) | `Story/Editor/StoryEditorWindow.cs` (D-078, K-050) |
| P1-X05 4번 결과 설계 | [ ] | [x] (대체) | D-076 그림판 방식 → `Meta/ResultBoard.cs` |
| P1-X06 3번 정비 설계 | [ ] | [x] | D-048 승인, D-076 마당 방식 |
| P1-U03 Codex 화면 설계 승인 | [ ] | [~] | D-048·D-066·D-076·D-078. 메인 설계·C-009 미승인 |
| P1-U04 CONTRACTS 승인 | [~] | [~] (유지) | 대부분 승인(D-017·D-022·D-048), C-009·새 초안 남음 |
| P2-C04 App/Boot/GameFlow | [?] | [x] | `App/Flow/GameFlow.cs`·`FlowRouter.cs`·`AppFlowTests`, D-038 자동 Play |
| P2-C05 Battle 씬 뼈대 | [?] | [x] | `Scenes/Battle.unity`, `BattleEntry` (실제 전투까지) |
| P2-C06 시험용 진입 | [?] | [x] | `App/Dev/DevHarness`·`DevPlaceholderScreen`, [시험] 버튼 |
| P2-C09 1회차 시작 데이터 | [?] | [x] | campaign_start·wave_1~4·terrain_1~4·지점 에셋, `RealDataTests` |
| P3-X01 0번 시작·종료 | [ ] | [x] | `Frontend/MainMenuEntry.cs` (시작→난이도, 종료) |
| P3-X03 5번 대사 표시·넘김 | [ ] | [x] | `Story/StoryPlayer.cs`·`StoryEntry.cs` (K-050) |
| P3-X04 스토리 툴 | [ ] | [x] | `StoryEditorWindow.cs`, `StoryScript.Validate()` |
| P3-X05 1회차 짧은 스토리 | [ ] | [x] (임시 대본) | `Story/Resources/Stories/story_intro`·`story_defeat` |
| P3-X06 4번 보상·손실 표시 | [ ] | [x] | `Meta/ResultBoard`·`ResultEntry`·`ResultDetailPopup` (D-076) |
| P3-X07 결과 반영(함정 소비·이동) | [ ] | [x] | `Meta/BattleResultApplier.cs` PrepareTraps |
| P3-X08 성벽 수리 | [ ] | [x] | `PreparationTransactions.TryRepair`, C-017·D-042 |
| P3-X09 상점 | [ ] | [~] | 함정 상점만, `ItemDef`는 쓰는 곳 없음 |
| P3-X10 용병·군인 모집 | [ ] | [~] | `TryRecruit`(궁수·총병·대포병) — '용병' 구분 없음 |
| P3-X11 함정 배치·구매 | [ ] | [x] | `Meta/TrapPreparationPanel`·`TrapPreparationTransactions`, D-051, 지형 탭 |
| P3-C01 성벽 구간·내구도 | [ ] | [x] | `BattleSim`·`BattleWallLayout`·HUD 성벽 체력 |
| P3-C02 주인공 좌우 이동 | [ ] | [x] | `BattleCameraRig.cs`, D-046·D-061·D-071 |
| P3-C03 병사 배치·상호작용 | [ ] | [x] | `BattleHud.Commands`(D-072), `BattleSim.Commands·Cannons·Groups·Slots` |
| P3-C04 기본 무기 자동 공격 | [ ] | [x] | weapon_bow·gun·cannon, `SoldierFireJob`, `BattleSim.Ignite` |
| P3-C05 적 종류·대량 웨이브 | [ ] | [x] | 적 3종, wave_1~4, `BattleJobs`(Burst), `BattleSim.Flank` |
| P3-C06 표적 우선순위 | [ ] | [x] | `TargetingSettings`, D-073 |
| P3-C10 함정 감지·발동·피해 | [ ] | [x] | `BattleSim.Traps.cs`, `BattleTrapTests`, D-050 |
| P3-C11 승패·BattleResult | [ ] | [x] | `BattleSim.BuildResult`, `BattleEntry` |
| P3-C12 카메라 리그 | [ ] | [x] | `BattleCameraRig`·`BattleMapCamera`, D-040·D-061·D-078 |
| P3-U02 검증 1·2번 전투 | [ ] | [~] | 시험 플레이 1~3차 피드백, 공식 통과 기록 없음 |
| P3-U03 검증 4번·3번 | [ ] | [~] | D-076 재설계, 새 화면 사용자 검토 대기 |
| P4-C01 화면 연결 점검 | [ ] | [~] | D-038 자동 Play 한 바퀴, R-005 — 지형·스토리 포함 다시 필요 |
| P4-C02 진행 데이터 흐름 점검 | [ ] | [~] | 10회 40전투 반영 정상 — 새 지형 데이터 다시 확인 필요 |
| P4-C04 main 반영 | [ ] | [~] | R-003~R-005 반영, 1회차 마일스톤 묶음은 아직 |
| P5 유지보수 안내 문서 (할 일 13번) | [ ] | [x] | 이번 카드 `Docs/GDD/MAINTENANCE.md` |
| CY1~CY4, MAP-1~6 (CYCLES.md) | 표기 없음 | [x] | CYCLES.md 변경 기록, ROADMAP 기록(MAP-1~5 커밋, MAP-6 측정, CY4-C1~C4) |

**아직 대기가 맞는 것**: P3-X02 0번 설정 팝업(메인에 시작·종료만), P1-U05 한글 폰트(임시 글꼴), P1-X08 공용 UI 범위 제안(D-074로 담당 재지정·삭제 검토), P3-U01 0·5번 검증, P4-C03 통합 검증 절차서, P4-X01(담당 재지정 필요), P4-U01·U02, P4-C05 live 백업 `_CL`, P5 나머지.

**함께 손볼 것**
- ROADMAP '현재 위치'가 아직 '바퀴 1 진행'으로 되어 있다. 실제로는 카드 방식(D-067)으로 진행 중이다.
- Codex 설계 문서 번호가 ROADMAP과 다르다: `Result.md`=P1-X04, `Prep.md`=P1-X05, `StoryEditor.md`=P1-X06라고 적혀 있지만, ROADMAP은 X04 스토리 툴·X05 결과·X06 정비다(D-048은 ROADMAP 번호).

## 바꾼 파일
- `Docs/collab/rules/CONTRACTS.md` (맨 아래 추가만)
- `Docs/GDD/MAINTENANCE.md` (새로 만듦)
- `_HTML/1_진행과규칙/19_유지보수안내.html` (새로 만듦)
