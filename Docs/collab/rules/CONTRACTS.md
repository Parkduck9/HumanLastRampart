# 인터페이스 약속 (CONTRACTS)

- 작성자: Claude만 작성한다. Codex는 자기 outbox로 의견을 보낸다 (RULES 14장).
- 상태가 `제안`인 약속은 확정된 API로 사용하지 않는다 (RULES 8장).
- 승인된 약속을 바꿀 때는 새 버전을 추가하고 이전 내용을 남긴다.

### 형식
```markdown
## C-001 이름
- 상태: 제안 / 승인(날짜, 승인 근거)
- 위치:
- 정의:
- 사용처: (모듈·담당)
- 변경 규칙:
```

---

## 작성 예정 목록 (아키텍처 설계 단계에서 제안 예정)

| 예정 | 내용 | 관련 담당 |
|---|---|---|
| 폴더·씬 소유권 | `Game/Assets/_Project/` 하위 폴더와 씬별 소유자 | 공통 |
| asmdef 의존 방향 | 모듈 목록, 허용 의존 방향, 순환 참조 금지 | 공통 |
| 화면 전환 방식 | 0~5번 화면 간 이동, 부트스트랩 씬 | Claude(통합) |
| 스토리 → 전투 | 스토리 종료 후 전투 시작에 넘기는 데이터 | Codex → Claude |
| 전투 결과 데이터 | 4번 보상·손실 화면으로 전달 | Claude → Codex |
| 정비 결과 데이터 | 수리·모집·함정 배치 결과를 다음 전투로 전달 | Codex → Claude |
| 함정 데이터 경계 | 배치·구매·저장은 Codex, 감지·발동·피해는 Claude | 공통 |
| 스토리 데이터 형식 | 스토리 툴이 만드는 데이터 구조 | Codex |

---

<!-- 약속은 이 아래에 추가한다 -->

## C-001 폴더·씬 소유권
- 상태: **제안** (2026-10-13 초안, 근거 D-013·ARCHITECTURE 승인 v1.0 2·4장)
- 위치: `Game/Assets/` 전체
- 정의:

  **1) 폴더 소유자** — 소유자만 파일을 만들고 고친다. 다른 쪽은 읽기만 하고, 변경은 메시지로 요청한다 (RULES 5장).

  | 경로 (`Game/Assets/` 기준) | 소유 | 구분 | 비고 |
  |---|---|---|---|
  | `_Project/Core/` | Claude | **공용 경로** | Claude도 수정 전 예약 기록. Codex는 메시지로 변경 요청 |
  | `_Project/App/` | Claude | | Boot 씬 코드, 화면 전환, `CampaignState` 보관 |
  | `_Project/Combat/` | Claude | | 1·2번 전투, 전투 HUD, 함정 발동·피해 |
  | `_Project/Frontend/` | Codex | | 0번 메인, 설정 팝업 |
  | `_Project/Story/` | Codex | | 5번 스토리 재생. `Story/Editor/`에 스토리 툴 |
  | `_Project/Meta/` | Codex | | 4번 보상·손실, 3번 정비 |
  | `_Project/UI/` | Codex | **공용 경로** | 둘 이상의 화면이 쓰는 UI 부품, 한글 폰트 에셋 |
  | `_Project/Scenes/` | 씬별 (아래 표) | | 폴더 자체는 Claude가 만든다 |
  | `_Project/Data/Combat/` | Claude | | 적·무기·병종·웨이브 정의 에셋 |
  | `_Project/Data/Meta/` | Codex | | 상점 품목, 모집 목록, 수리 비용 |
  | `_Project/Data/Story/` | Codex | | 스토리 데이터 |
  | `_Project/Data/Traps/` | 공용 | **공용 경로** | 함정 정의 에셋. 세부 경계는 C-008에서 정함 |
  | `_Project/Art/Characters/`, `Art/Environment/` | Claude | | 전투용 도트 스프라이트, 성벽·지형. 다른 화면은 읽어서 사용 |
  | `_Project/Art/UI/`, `Art/Story/` | Codex | | 공용 UI 이미지, 스토리 초상화·배경 |
  | `_Project/Audio/` | 공용 | **공용 경로** | 1회차는 최소한만 |
  | `ThirdParty/` | 공용 | **공용 경로** | 외부 에셋 추가는 사용자 승인 (RULES 8장) |
  | `Settings/` | 공용 | **공용 경로** | URP 설정 에셋 (템플릿이 만든 위치 그대로) |
  | `InputSystem_Actions.inputactions` | Claude | **공용 경로** | 입력 동작 정의. 변경은 예약 (결정 필요 C1) |
  | `Scenes/SampleScene.unity`, `TutorialInfo/`, `Readme.asset` | — | 템플릿 예제 | 처리 방법은 결정 필요 C2 |

  **2) 씬 소유자** — 씬 파일은 소유자 한 명만 연다·저장한다 (RULES 9장).

  | 씬 (`_Project/Scenes/`) | 화면 | 소유 |
  |---|---|---|
  | `Boot.unity` | 바탕 씬 (항상 열림) | Claude |
  | `MainMenu.unity` | 0번 | Codex |
  | `Story.unity` | 5번 | Codex |
  | `Battle.unity` | 1·2번 (2번은 씬 안의 지도 모드) | Claude |
  | `Result.unity` | 4번 | Codex |
  | `Prep.unity` | 3번 | Codex |

  **3) 빌드 씬 목록**(`ProjectSettings/EditorBuildSettings.asset`)은 공용 경로이고 Claude가 관리한다. Codex 씬을 추가할 때는 메시지로 요청한다. 첫 번째 씬은 `Boot`이다.
- 사용처: 모든 모듈
- 변경 규칙: 폴더·씬 추가나 소유자 변경은 새 버전으로 추가하고 사용자 승인을 받는다. 각자 자기 폴더 안의 하위 폴더는 자유롭게 만든다.

## C-002 어셈블리(asmdef) 구성과 의존 방향
- 상태: **제안** (2026-10-13 초안, 근거 D-013·ARCHITECTURE 3장)
- 위치: 각 모듈 폴더의 `.asmdef` 파일
- 정의:

  | 어셈블리 이름 | 폴더 | 기본 네임스페이스 | 소유 | 참조할 수 있는 것 |
  |---|---|---|---|---|
  | `HLR.Core` | `_Project/Core/` | `HumanLastRampart.Core` | Claude | (없음) |
  | `HLR.App` | `_Project/App/` | `HumanLastRampart.App` | Claude | Core |
  | `HLR.Combat` | `_Project/Combat/` | `HumanLastRampart.Combat` | Claude | Core |
  | `HLR.Frontend` | `_Project/Frontend/` | `HumanLastRampart.Frontend` | Codex | Core, UI |
  | `HLR.Story` | `_Project/Story/` | `HumanLastRampart.Story` | Codex | Core, UI |
  | `HLR.Story.Editor` | `_Project/Story/Editor/` | `HumanLastRampart.Story.Editor` | Codex | Core, Story (에디터 전용) |
  | `HLR.Meta` | `_Project/Meta/` | `HumanLastRampart.Meta` | Codex | Core, UI |
  | `HLR.UI` | `_Project/UI/` | `HumanLastRampart.UI` | Codex | Core |

  ```
  HLR.Core  ◀── App, Combat, UI
     ▲
     └── Frontend, Story, Meta ──▶ HLR.UI
                     Story ◀── Story.Editor
  ```
  - **금지:** 표에 없는 참조, 순환 참조. 특히 화면 모듈끼리(Combat↔Meta, Frontend↔Story 등) 직접 참조하지 않는다. App도 화면 모듈을 참조하지 않는다 (C-003의 인터페이스로만 연결).
  - **Unity 패키지 참조:** 각 어셈블리는 필요한 Unity 패키지 어셈블리(`Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI` 등)를 자유롭게 참조할 수 있다. 새 **패키지 설치**는 사용자 승인 (RULES 8장).
  - **ARCHITECTURE와 다른 점 (확인 필요 C3):** ARCHITECTURE 3장 표에는 "모든 모듈은 Core만 참조"로 적었지만, Q-A1에서 공용 UI를 따로 두기로 했으므로 **Codex 화면 모듈(Frontend·Story·Meta)이 `HLR.UI`를 참조하는 것**을 허용해야 공용 UI를 쓸 수 있다. Combat은 전투 HUD를 자기 안에 두므로 UI를 참조하지 않는다.
  - 에디터 전용 코드는 `Editor/` 폴더와 별도 asmdef(Include Platforms: Editor)에 둔다.
  - 기본 어셈블리(`Assembly-CSharp`)에는 게임 코드를 두지 않는다. 템플릿 예제 코드는 C2 결정에 따른다.
- 사용처: 모든 모듈
- 변경 규칙: 참조 추가는 새 버전 + 사용자 승인. 순환이 생기는 변경은 승인하지 않는다.

## C-003 화면 전환
- 상태: **제안** (2026-10-13 초안, 근거 D-013 Q-A2·Q-A3, ARCHITECTURE 2·5장)
- 위치: `HLR.Core` (`HumanLastRampart.Core.Flow`), 구현은 `HLR.App`
- 정의:

  **1) 기본 방식** — `Boot` 씬은 게임 내내 열려 있다. 화면 씬은 한 번에 하나만 추가로 열고(Additive), 다음 화면으로 갈 때 닫는다. 진행 데이터는 Boot(App)에 있어 화면을 바꿔도 사라지지 않는다.

  **2) Core에 둘 약속 (코드 형태 제안)**
  ```csharp
  namespace HumanLastRampart.Core.Flow
  {
      // 화면 목록. 씬 이름과 같다 (C-001).
      public enum ScreenId { MainMenu, Story, Battle, Result, Prep }

      // 화면이 끝날 때 App에 알리는 이유. 어디로 갈지는 App이 정한다 (Q-A3).
      public enum ScreenOutcome
      {
          Done,           // 이 화면의 일을 마침 (기본)
          StartNewGame,   // 메인: 시작
          QuitGame,       // 메인: 종료
          BackToMainMenu  // 어느 화면이든: 메인으로 돌아가기
      }

      // 각 화면 씬에 하나씩 두는 진입 컴포넌트가 구현한다 (화면 담당자 소유).
      public interface IScreenEntry
      {
          ScreenId Id { get; }
          void OnEnter(IScreenContext context);  // 씬이 열린 뒤 App이 한 번 호출
          void OnExit();                          // 씬을 닫기 직전 App이 한 번 호출
      }

      // App이 구현해서 화면에 넘겨준다.
      public interface IScreenContext
      {
          CampaignState Campaign { get; }         // 진행 데이터 (형태는 C-004에서 정함)
          void Complete(ScreenOutcome outcome);   // "끝났다" 신호. 한 화면에서 한 번만
      }
  }
  ```
  - 화면은 다른 화면 이름을 알지 않는다. `GoTo(다음 화면)` 같은 직접 이동은 두지 않는다.
  - 화면 데이터 전달(전투 시작 정보 `BattleSetup`, 전투 결과 `BattleResult`)은 `CampaignState` 안이나 C-005·C-006에서 정한다. C-003은 "열기·끝내기"만 정한다.

  **3) 진입 컴포넌트 규칙**
  - 각 화면 씬의 루트에 `IScreenEntry`를 구현한 컴포넌트를 **정확히 하나** 둔다. App은 씬을 연 뒤 이것을 찾아 `OnEnter`를 부른다.
  - 화면은 `OnEnter` 전에 진행 데이터를 읽거나 바꾸지 않는다.
  - `Complete`를 부른 뒤에는 그 화면에서 진행 데이터를 바꾸지 않는다 (App이 다음 화면으로 넘어가는 중).

  **4) 다음 화면 순서표 (App 한 곳에서 관리)** — 1회차 완주 기준 제안

  | 현재 화면 | 받은 신호 | 다음 |
  |---|---|---|
  | (게임 시작) | — | MainMenu |
  | MainMenu | StartNewGame | Story (새 진행 데이터 생성) |
  | MainMenu | QuitGame | 게임 종료 (에디터에서는 Play 중지) |
  | Story | Done | Battle |
  | Battle | Done | Result |
  | Result | Done | 승리: Prep / **패배: 결정 필요 C4** |
  | Prep | Done | Battle (다음 전투) |
  | 어느 화면이든 | BackToMainMenu | MainMenu |

  - 나중에 스토리를 전투 사이에 넣는 등의 변경은 이 표(App)만 고친다.

  **5) 단독 테스트** — 화면 씬 하나만 열고 Play해도 테스트할 수 있도록 Claude가 Phase 2에서 테스트용 진입 방법을 제공한다(ROADMAP P2-C06). 방식은 그때 제안한다.
- 사용처: App(Claude) 구현, 모든 화면(Claude·Codex)의 진입 컴포넌트
- 변경 규칙: `ScreenId`·`ScreenOutcome` 값 추가나 인터페이스 변경은 새 버전 + 사용자 승인. 순서표(4)의 변경도 사용자 승인. 값을 지우거나 이름을 바꾸지 않는다.

### C-001~C-003 결정 필요 항목 (사용자)

| ID | 질문 | Claude 제안 |
|---|---|---|
| C1 | 입력 동작 정의 파일(`InputSystem_Actions.inputactions`)의 소유 | Claude 소유, 공용 경로(변경 시 예약). 전투 입력이 대부분이고, UI 입력 변경은 Codex가 요청 |
| C2 | 템플릿 예제(`SampleScene`, `TutorialInfo/`, `Readme.asset`) 처리 | Phase 2 시작 때 Claude가 삭제하고 빌드 씬 목록을 Boot로 교체 (URP 설정 `Settings/`는 유지) |
| C3 | Codex 화면 모듈(Frontend·Story·Meta)이 공용 UI(`HLR.UI`)를 참조하는 것 허용 | 허용 (Q-A1 공용 UI 결정을 쓰려면 필요) |
| C4 | 전투 패배 후 어디로 갈지 | 1회차는 결과 화면(4번)에서 패배를 보여 준 뒤 **메인 화면**으로. 재도전 기능은 다음 마일스톤 |
