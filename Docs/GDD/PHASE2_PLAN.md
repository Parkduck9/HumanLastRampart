# Phase 2 기반 구현 계획

- 상태: **제안 r1 — 사용자 승인 전** (승인 전에는 구현하지 않는다)
- 작성: Claude (통합 담당) / 2026-10-13 / ROADMAP Phase 2
- 근거: ARCHITECTURE 승인 v1.0(D-013), CONTRACTS 승인분(C-001~C-004·C-005~C-007·C-010·C-003 v1.1·P1-C14·P1-C15), Battle.md 승인 r5(D-026~D-032), D-024(전투 구조)

## 1. 목표 — "빈 화면이 끝까지 이어진다"

Phase 2가 끝나면 **Boot 씬에서 Play 한 번으로** 아래 흐름이 실제로 이어진다. 화면 안의 게임 내용은 아직 없다(Phase 3).

```
Boot → 0 메인(시작/종료 버튼만) → 난이도 선택(쉬움/현실) → 5 스토리(빈 화면, 클릭하면 끝)
     → 1·2 전투(빈 전장 + 시험용 [승리]/[패배] 버튼)
         승리 → 4 결과(빈 화면, 다음) → 3 정비(빈 화면, 다음) → 다음 전투 … 4번째 뒤 메인
         패배 → 5 패배 엔딩(빈 화면) → 메인
```

- 이 흐름이 돌면 **화면 전환 약속(C-003)·진행 데이터(C-004)·전투 준비/결과(C-005·C-006)·결과 반영(C-007)**이 실제 코드로 검증된다.
- 각 담당은 Phase 3에서 자기 화면 안만 채우면 된다.

## 2. 작업 나누기

### 2.1 Claude
| ID | 작업 | 산출물 | 공용 경로 |
|---|---|---|---|
| **P2-C01** | `Core/` 폴더, `HLR.Core` 어셈블리 | `_Project/Core/HLR.Core.asmdef` | Core (예약) |
| **P2-C02** | 화면 전환 약속 코드 | `ScreenId`, `ScreenOutcome`, `IScreenEntry`, `IScreenContext`(v1.2: `Definitions`, `CompleteNewGame`), `Difficulty`, `NewGameOptions` | Core |
| **P2-C03** | 데이터 약속 코드 | `CampaignState`(C-004 v1.3)와 하위 클래스, 정의 클래스(C-010 v1.2: `DefinitionBase`, `UnitDef`, `WeaponDef`, `EnemyDef`, `WaveDef`, `TrapDef`, `ItemDef`, `PostDef`, `SpellDef`, `TerrainDef`, `TrapSlotDef`, `BattleRulesDef`, `CampaignStartDef`), `IDefinitionLookup`, `BattleSetup`·`BattleResult`(C-005·C-006 v1.2), `TargetingSettings`(C-011 v1.1) | Core |
| **P2-C03b** | Core 공용 함수 | `BattleForecast.Get`(날씨 예보, D-031), `TrapRules`(C-015, 1회차 회수 계산 — 틀과 기본 판정) | Core |
| **P2-C04** | App: Boot 씬과 흐름 | `App/` — `GameFlow`(C-003 순서표: 패배 엔딩·4번째 전투 후 메인 포함), 씬 추가 로딩, 화면별 새 컨텍스트·무효화, 전환 중 입력 차단·페이드, 진입 컴포넌트 0/2개·로드 실패 처리, **카탈로그 검증**(C-010 4), **새 게임 생성**(`CampaignStartDef`·난이도·시드), `BattleSetup` 생성·`BattleResult` 분기, **E2 오류 안내 패널**(D-017) | — |
| **P2-C05** | Battle 씬 뼈대 | `Scenes/Battle.unity`, `BattleEntry`(IScreenEntry), 원근 카메라 자리, 시험용 [승리]/[패배] 버튼으로 `BattleResult` 만들기 | — |
| **P2-C06** | 시험용 진입 방법 | 화면 씬 하나만 열고 Play해도 돌아가게: 임시 진행 데이터·EventSystem 자동 준비(에디터 전용). "임시 승리 결과로 결과 화면 열기" | — |
| **P2-C07** | 빌드 씬 목록 + 템플릿 정리 | 빌드 목록 = Boot·MainMenu·Story·Battle·Result·Prep, 템플릿 예제(SampleScene·TutorialInfo·Readme) 삭제 (C-001 C2) | 빌드 목록·Assets 루트 (예약) |
| **P2-C09** | 1회차 시작 데이터 초안 | `CombatCatalog`, 임시 `CampaignStartDef`(성벽 4개·대포 4문 지점·분대·마법사), 지형 4개·웨이브 4개 틀(수치는 임시) | Data/Combat |
| **P2-C10** | Combat 어셈블리 정리 | `HLR.Combat`가 `HLR.Core`를 참조하도록, PerfLab은 시험 도구로 그대로 | — |

### 2.2 Codex
| ID | 작업 | 시작 조건 |
|---|---|---|
| **P2-X01** | `Frontend/`·`Story/`·`Meta/`·`UI/` 폴더와 어셈블리(C-002) | P2-C02·C03 커밋 후 (Core를 참조해야 함) |
| **P2-X02** | MainMenu·Story·Result·Prep 씬 뼈대 + 진입 컴포넌트. **메인: 시작 → 난이도 선택 창 → `CompleteNewGame`**, 종료 / 스토리·결과·정비: 클릭하면 `Complete(Done)` | P2-X01 후 |
| **P2-X03** | 공용 UI 기초(버튼·팝업) + 한글 글꼴 | 글꼴 결정(4장 F1) |

### 2.3 사용자
| ID | 작업 |
|---|---|
| **P2-U01** | 검증: Boot에서 Play → 1장 흐름 전체(승리 경로·패배 경로·종료) 확인, 콘솔 에러 0 |

## 3. 순서

```
① Claude P2-C01~C03b (Core 코드)  ──커밋──▶  ② Codex P2-X01~X03 (자기 씬 뼈대)
                                   └──────▶  ② Claude P2-C04~C06, C09, C10 (App·Battle·시험 진입)
                                                        │
③ Claude P2-C07 (빌드 목록·템플릿 정리) ◀────── 둘 다 끝나면
                                                        │
④ 사용자 P2-U01 검증 (검증 구간 RULES 11.2)  →  main 반영 묶음 승인
```

- ①은 Codex가 기다리는 일이라 **가장 먼저** 한다. Core는 공용 경로라 Claude도 예약을 기록한다(RULES 5장).
- ②는 서로 다른 폴더라 동시에 진행할 수 있다. Codex의 진입 컴포넌트가 없어도 App은 "진입 컴포넌트 없음 → 메인 복귀"(C-003 3-3)로 처리되므로 막히지 않는다.
- Phase 1에서 남은 Codex 화면 설계(P1-X01~X06)는 Phase 2 뼈대에 꼭 필요하지 않다(빈 화면). 다만 Phase 3 전에는 필요하다.

## 4. 결정 필요 항목 (사용자)

| ID | 질문 | Claude 제안 |
|---|---|---|
| F1 | 한글 글꼴(P1-U05) — 지금 정할지 | **Phase 2는 임시 글꼴**(Unity 기본 글꼴 또는 무료 한글 글꼴 하나를 임시로)로 진행하고, 정식 글꼴은 Phase 3 화면 구현 전에 후보 이미지를 보고 고른다 |
| F2 | 자동 시험(유닛 테스트) 어셈블리 추가 | **추가 추천.** 화면 순서표·결과 반영 검증·예보 함수·카탈로그 검증을 Play 없이 자동 확인. C-002 표에 에디터 전용 `HLR.Core.Tests`·`HLR.App.Tests`(Claude) 추가 — 약속 변경이라 승인 필요 |
| F3 | 템플릿 예제 삭제 시점 | C-001 C2대로 **P2-C07에서 삭제**(빌드 목록을 Boot로 바꿀 때 함께) |
| F4 | Codex 시작 시점 | **Claude ① 커밋 직후** 사용자가 Codex에게 "협업 확인"으로 전달 |

## 5. 위험과 대응

| 위험 | 대응 |
|---|---|
| Codex 검토(M-CL-016·019)로 약속이 바뀜 | ①의 Core 코드는 승인된 약속만 넣는다. 검토 의견이 오면 새 판(r3)으로 반영 후 Core를 고친다 — Codex 뼈대는 진입 컴포넌트만 쓰므로 영향 작음 |
| 씬을 AI가 직접 못 만듦(RULES 11.1) | PerfLab처럼 **씬은 YAML로 작성**하거나 **에디터 메뉴로 생성**한다. 사용자는 Play·확인만 |
| 공용 경로 동시 수정 | Core·빌드 목록은 Claude만, UI는 Codex만. 예약 기록 후 작업 |
| 4개 씬 뼈대가 늦어짐 | App은 진입 컴포넌트 없음을 오류 처리로 견딘다. Claude의 시험용 진입으로 Battle·흐름 먼저 확인 가능 |

## 6. 완료 기준 (Phase 2)

- P2-U01: Boot Play → 메인 → 난이도 → 스토리 → 전투(승리) → 결과 → 정비 → 전투 … 4번째 뒤 메인 / 전투(패배) → 패배 엔딩 → 메인 / 메인 종료 — 모두 콘솔 에러 0
- 같은 결과 화면을 다시 열어도 골드 등이 두 번 반영되지 않음(C-007) — 자동 시험(F2) 또는 시험 버튼으로 확인
- 카탈로그 오류(빈 ID 등)를 일부러 넣으면 새 게임이 거부됨(C-010 4)
- 이후 main 반영 묶음(R-003) 승인 요청

## 변경 기록

| 날짜 | 내용 |
|---|---|
| 2026-10-13 | r1 작성 (Claude). 결정 F1~F4 대기 |
