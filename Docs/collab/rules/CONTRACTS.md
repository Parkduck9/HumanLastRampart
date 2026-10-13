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
- 상태: **승인** (2026-10-13, 사용자 확정 D-014. r3 기준 — Codex 검토 M-CX-007 반영, C1·C2 답변 포함)
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
  | `InputSystem_Actions.inputactions` | Claude | **공용 경로** | 입력 동작 정의. Claude도 변경 전 예약. Codex는 메뉴 입력 등이 필요하면 메시지로 요청 (C1 답변) |
  | `Scenes/SampleScene.unity`, `TutorialInfo/`, `Readme.asset` | Claude | 템플릿 예제 | **Phase 2 시작 때 Claude가 삭제**하고 빌드 씬 목록을 Boot로 바꾼다. `Settings/`는 유지 (C2 답변) |

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

  **4) "공용" 경로의 파일 작성자** (r2, M-CX-007 요청 3) — `Data/Traps/`, `Audio/`, `ThirdParty/`, `Settings/`처럼 소유가 "공용"인 폴더도 **파일마다 작성자는 한 명**이다 (RULES 1.4 단일 작성자 원칙).
  - 새 파일을 만들 때: 예약 요청에 **만들 파일 경로와 작성자**를 적고, 조정 담당이 tasks.md 예약 기록에 그대로 남긴다. 그 파일의 작성자는 이때 고정된다.
  - 기존 파일을 고칠 때: **그 파일의 작성자만** 고친다. 작성자도 공용 경로이므로 예약을 먼저 기록한다. 다른 쪽은 메시지로 변경을 요청한다.
  - 예약 승인은 "지금 그 경로를 써도 된다"는 순서 허가일 뿐, **상대가 만든 파일을 고칠 권한을 주지 않는다.**
  - 템플릿이 만든 기존 파일(`Settings/`의 URP 설정 에셋 등)의 작성자는 Claude로 본다(통합 담당).
- 사용처: 모든 모듈
- 변경 규칙: 폴더·씬 추가나 소유자 변경은 새 버전으로 추가하고 사용자 승인을 받는다. 각자 자기 폴더 안의 하위 폴더는 자유롭게 만든다.

## C-002 어셈블리(asmdef) 구성과 의존 방향
- 상태: **승인** (2026-10-13, 사용자 확정 D-014. r3 기준 — C3 답변 포함)
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
  - **ARCHITECTURE와 다른 점 (C3 답변: 허용):** ARCHITECTURE 3장 표에는 "모든 모듈은 Core만 참조"로 적었지만, Q-A1에서 공용 UI를 따로 두기로 했으므로 **Codex 화면 모듈(Frontend·Story·Meta)이 `HLR.UI`를 참조하는 것을 허용**한다(Codex 동의 M-CX-007). Combat은 전투 HUD를 자기 안에 두므로 UI를 참조하지 않는다. 승인되면 ARCHITECTURE 3장도 맞춘다.
  - 에디터 전용 코드는 `Editor/` 폴더와 별도 asmdef(Include Platforms: Editor)에 둔다.
  - 기본 어셈블리(`Assembly-CSharp`)에는 게임 코드를 두지 않는다. 템플릿 예제 코드는 C2 결정에 따른다.
- 사용처: 모든 모듈
- 변경 규칙: 참조 추가는 새 버전 + 사용자 승인. 순환이 생기는 변경은 승인하지 않는다.

## C-003 화면 전환
- 상태: **승인** (2026-10-13, 사용자 확정 D-014. r3 기준 — Codex 검토 M-CX-007 반영, C4 답변 포함). 단 `IScreenContext.Campaign`의 타입 `CampaignState` 내용은 C-004에서 정한다
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
  - `Complete`를 부른 뒤에는 그 화면에서 진행 데이터를 바꾸지 않고, 게임 입력도 처리하지 않는다 (App이 다음 화면으로 넘어가는 중).
  - `OnExit`에서 화면은 자기가 켠 입력 처리·코루틴·이벤트 구독을 정리한다.

  **3-1) App의 방어 규칙** (r2, M-CX-007 요청 1) — 화면 쪽 실수가 있어도 흐름이 꼬이지 않도록 App이 아래처럼 동작한다.
  - **화면마다 새 컨텍스트:** App은 화면을 열 때마다 `IScreenContext`를 새로 만들어 넘긴다. 이 컨텍스트는 그 화면의 `OnExit`가 끝나면 **무효**가 된다.
  - **무효 컨텍스트 호출:** 무효가 된 컨텍스트의 `Complete`는 무시하고 경고 로그를 남긴다(이전 화면의 늦은 콜백 차단). `Campaign`에 접근하면 `InvalidOperationException`을 던진다(개발 중 실수를 바로 찾기 위해).
  - **중복 `Complete`:** 한 컨텍스트에서 처음 받아들인 `Complete`만 처리하고, 이후 호출은 무시하고 경고 로그를 남긴다.
  - **허용되지 않은 신호:** 화면별로 받을 수 있는 신호가 정해져 있다(아래 표). 표에 없는 신호는 무시하고 에러 로그를 남긴다. 이 경우 받아들인 것으로 세지 않으므로 화면은 올바른 신호를 다시 보낼 수 있다.

    | 화면 | 받을 수 있는 신호 |
    |---|---|
    | MainMenu | StartNewGame, QuitGame |
    | Story, Battle, Result, Prep | Done, BackToMainMenu |

  - **`OnEnter` 안에서 바로 `Complete`:** 허용한다. App은 `OnEnter`가 끝난 뒤 전환을 시작한다.
  - **전환 순서:** `Complete` 받아들임 → 입력 차단 → 현재 화면 `OnExit` → 컨텍스트 무효화 → 현재 씬 닫기 → 다음 씬 열기 → 진입 컴포넌트 찾기 → 새 컨텍스트로 `OnEnter` → 입력 차단 해제.
  - **입력 차단:** 전환 중에는 App이 Boot의 `EventSystem`을 꺼서 UI 클릭을 막고, 화면을 덮는 전환용 화면(검은 화면 또는 페이드)을 Boot에 띄운다. 게임 입력은 위 3)의 화면 의무(`Complete` 뒤 입력 처리 안 함)로 막는다.

  **3-2) 씬에 두는 공용 오브젝트** (r2, M-CX-007 요청 2) — 씬을 겹쳐 열 때 같은 종류가 두 개 생기지 않도록 정한다.

  | 오브젝트 | 두는 곳 | 소유 | 수명 |
  |---|---|---|---|
  | `EventSystem` + `InputSystemUIInputModule` | **Boot에만 하나** | Claude | 게임 내내 |
  | 전환용 화면(페이드 Canvas) | Boot | Claude | 게임 내내 |
  | 카메라(`Camera`) | **각 화면 씬에 하나** | 그 씬 소유자 | 그 화면이 열린 동안 |
  | `AudioListener` | 각 화면 씬의 카메라에 하나 | 그 씬 소유자 | 그 화면이 열린 동안 |

  - 화면 씬에는 `EventSystem`을 두지 않는다. Boot에는 카메라와 `AudioListener`를 두지 않는다(화면 씬은 한 번에 하나만 열리므로 카메라·리스너도 항상 하나).
  - 전환 중 화면 씬이 없는 짧은 순간에는 Boot의 전환용 화면(카메라가 필요 없는 Overlay Canvas)만 보인다.
  - 화면 씬을 단독으로 테스트할 때 필요한 `EventSystem`은 5)의 테스트용 진입 방법이 대신 만든다.

  **3-3) 오류 처리** (r2, M-CX-007 요청 2)
  - **씬 열기 실패, 또는 진입 컴포넌트가 0개·2개 이상:** App은 에러 로그(씬 이름과 원인)를 남기고 그 화면의 `OnEnter`를 부르지 않는다. 그다음 MainMenu로 돌아간다. MainMenu 자체가 실패하면 더 이동하지 않고 에러 로그만 남긴다(무한 반복 방지).
  - 1회차에서는 오류 화면을 따로 만들지 않는다. 콘솔 에러로 확인한다.

  **4) 다음 화면 순서표 (App 한 곳에서 관리)** — 1회차 완주 기준 제안

  | 현재 화면 | 받은 신호 | 다음 |
  |---|---|---|
  | (게임 시작) | — | MainMenu |
  | MainMenu | StartNewGame | Story (새 진행 데이터 생성) |
  | MainMenu | QuitGame | 게임 종료 (에디터에서는 Play 중지) |
  | Story (일반 스토리) | Done | Battle |
  | Battle | Done | 승리: Result / **패배: Story (패배 엔딩)** |
  | Story (패배 엔딩) | Done | MainMenu (진행 데이터 폐기, 처음부터) |
  | Result | Done | Prep |
  | Prep | Done | Battle (다음 전투) |
  | 어느 화면이든 | BackToMainMenu | MainMenu |

  - **패배 흐름 (C4 답변):** 전투에서 지면 **4번 결과 화면을 건너뛰고** 바로 5번 스토리 화면에서 패배 엔딩(캐릭터 + 대사)을 보여 준 뒤 메인 화면으로 간다. 새 화면을 만들지 않고 스토리 화면을 다시 쓴다. 엔딩 내용은 Codex가 스토리 데이터로 만든다.
  - 이 분기를 위해 App은 두 가지를 알아야 한다. **(a) 전투 승패**, **(b) 스토리 화면이 지금 무엇을 재생할지(일반 스토리 / 패배 엔딩).** 화면 신호(`ScreenOutcome`)를 늘리지 않고, 이 값은 진행 데이터와 전투 결과 약속에서 정한다 → C-004 `CampaignState`, C-006 `BattleResult`, C-009 스토리 데이터에 반영할 요구사항.
  - 나중에 스토리를 전투 사이에 넣는 등의 변경은 이 표(App)만 고친다.

  **5) 단독 테스트** — 화면 씬 하나만 열고 Play해도 테스트할 수 있도록 Claude가 Phase 2에서 테스트용 진입 방법을 제공한다(ROADMAP P2-C06). 방식은 그때 제안한다.
- 사용처: App(Claude) 구현, 모든 화면(Claude·Codex)의 진입 컴포넌트
- 변경 규칙: `ScreenId`·`ScreenOutcome` 값 추가나 인터페이스 변경은 새 버전 + 사용자 승인. 순서표(4)의 변경도 사용자 승인. 값을 지우거나 이름을 바꾸지 않는다.

### C-001~C-003 초안 변경 기록
| 날짜 | 판 | 내용 |
|---|---|---|
| 2026-10-13 | r1 | 초안 작성 (제안) |
| 2026-10-13 | r2 | Codex 검토 M-CX-007 반영: C-001 4) 공용 경로 파일별 작성자 절차, C-003 3-1) App 방어 규칙(컨텍스트 무효화·중복/허용 외 신호 처리·전환 순서·입력 차단), 3-2) EventSystem·카메라·AudioListener 배치, 3-3) 씬 로드·진입 컴포넌트 오류 처리. Codex는 C3와 ScreenOutcome 4값에 동의 |
| 2026-10-13 | r3 | 사용자 답변 반영: C1 입력 정의 파일 Claude 소유, C2 템플릿 예제 Phase 2 시작 때 삭제, C3 화면 모듈의 HLR.UI 참조 허용, C4 패배 시 결과 화면 건너뛰고 스토리 화면 패배 엔딩 → 메인 (C-004·C-006·C-009 요구사항 추가) |

### C-001~C-003 결정 항목 (사용자 답변 완료, D-014)

| ID | 질문 | Claude 제안 |
|---|---|---|
| C1 | 입력 동작 정의 파일(`InputSystem_Actions.inputactions`)의 소유 | Claude 소유, 공용 경로(변경 시 예약). 전투 입력이 대부분이고, UI 입력 변경은 Codex가 요청 |
| C2 | 템플릿 예제(`SampleScene`, `TutorialInfo/`, `Readme.asset`) 처리 | Phase 2 시작 때 Claude가 삭제하고 빌드 씬 목록을 Boot로 교체 (URP 설정 `Settings/`는 유지) |
| C3 | Codex 화면 모듈(Frontend·Story·Meta)이 공용 UI(`HLR.UI`)를 참조하는 것 허용 | 허용 (Q-A1 공용 UI 결정을 쓰려면 필요). **Codex 동의(M-CX-007).** 확정되면 ARCHITECTURE 3장 표·문구도 고친다 |
| C4 | 전투 패배 후 어디로 갈지 | 1회차는 결과 화면(4번)에서 패배를 보여 준 뒤 **메인 화면**으로. 재도전 기능은 다음 마일스톤 |

**답변 (2026-10-13, D-014):** C1 제안대로(Claude 소유) · C2 제안대로(Phase 2 시작 때 삭제) · C3 허용 · C4 패배 시 결과 화면을 건너뛰고 스토리 화면 패배 엔딩 → 메인(처음부터). 위 표의 'Claude 제안' 열은 기록으로 남긴다.

---

## C-004 진행 데이터 `CampaignState`
- 상태: **승인** (2026-10-13, 사용자 확정 D-015. r3 기준 — Codex 검토 M-CX-009 반영, D1·D2·D5 답변 포함). 단 `TrapPlacement`·`TargetingSettings`·`BattleResult`의 내용은 C-008·C-011·C-006에서 정한다
- 위치: `HLR.Core` (`HumanLastRampart.Core.Campaign`). 실제 객체는 App이 만들고 보관한다(C-003).
- 정의:

  **1) 원칙**
  - 한 판(새 게임 ~ 패배 엔딩)의 진행 상황은 **이 객체 하나**에 모은다. 화면끼리 값을 직접 넘기지 않는다.
  - **일반 C# 클래스**로 만든다. ScriptableObject에 진행 값을 쓰지 않는다 (RULES 9장 5번).
  - 정의 데이터(C-010)는 **ID 문자열로만** 가리킨다(에셋 직접 참조 금지). 나중에 저장 기능을 붙일 때 그대로 쓸 수 있게 하기 위해서다(Q-A7: 1회차는 저장 없음).
  - 메인 화면에서 "시작"하면 App이 새로 만들고, 패배 엔딩 뒤 메인으로 가면 버린다(D-014 C4).

  **2) 필드 (제안)**
  ```csharp
  namespace HumanLastRampart.Core.Campaign
  {
      public sealed class CampaignState
      {
          public int BattleNumber;                 // 다음(또는 진행 중) 전투가 몇 번째인지. 1부터
          public int Gold;                         // 화폐. 1회차는 골드 하나 (D2)

          public List<WallSegmentState> Wall;      // 성벽 구간별 상태 (구간 수는 전투 설계 P1-C09에서)
          public List<HeroState> Heroes;           // 주인공·특수 장교·강력한 병사: 한 명씩 관리
          public List<SquadState> Squads;          // 일반·특수 병사: 분대별 인원 수로 관리 (D1)
          public List<TrapPlacement> Traps;        // 설치한 함정 — 형태는 C-008에서 정함

          public TargetingSettings Targeting;      // 표적 우선순위 설정, 다음 전투에도 유지(Q-A5) — 형태는 C-011에서 정함

          public StoryRequest NextStory;           // 스토리 화면이 다음에 재생할 것 (D-014 C4)
          public BattleResult LastBattle;          // 가장 최근 전투 결과, 없으면 null — 형태는 C-006에서 정함

          public int NextInstanceNumber;           // 고유 ID 발급용 번호 (r2). 아래 NewInstanceId만 사용
          public string NewInstanceId(string prefix);  // 예: "squad_7", "hero_2", "trap_3". 이 판 안에서 겹치지 않음
      }

      public sealed class WallSegmentState { public string SegmentId; public int MaxHp; public int Hp; }

      public sealed class HeroState
      {
          public string InstanceId;   // 이 판에서의 고유 ID ("hero_" 접두사)
          public string UnitDefId;    // C-010 UnitDef
          public int Hp;
          public bool IsAlive;
          public string PostId;       // 배치 지점 (r2). 미배치면 null. 지점 목록은 전투 설계 P1-C09
          // 장착 아이템 등은 다음 마일스톤 (FirstRead: 아이템 강화)
      }

      public sealed class SquadState
      {
          public string SquadId;      // 분대 고유 ID ("squad_" 접두사, r2). 결과 반영은 이 ID로 맞춘다
          public string UnitDefId;    // C-010 UnitDef (병종). 같은 병종이 여러 분대일 수 있다
          public int Count;           // 인원 수. 0이 되면 반영하는 쪽이 목록에서 뺀다
          public string PostId;       // 배치 지점. 미배치면 null
      }

      public enum StoryKind { Normal, DefeatEnding }
      public sealed class StoryRequest { public StoryKind Kind; public string StoryId; }  // StoryId 형식은 C-009
  }
  ```
  - `List`는 `System.Collections.Generic`. 필드 이름과 형태는 Phase 2 구현 때 C# 관례(프로퍼티 등)로 다듬을 수 있지만, **담는 정보와 쓰는 쪽(아래 3)은 이 약속을 따른다.**

  **3) 누가 언제 바꾸는가** — 같은 값을 두 화면이 동시에 고치지 않도록 정한다.

  | 필드 | 만들기·바꾸는 쪽 | 시점 |
  |---|---|---|
  | 객체 전체 | App | MainMenu `StartNewGame` 때 새로 생성(초기값은 1회차 설정), 패배 엔딩 후 폐기 |
  | `BattleNumber` | App | 전투가 끝나 Result로 갈 때 1 증가 |
  | `NextStory` | App | 새 게임 시작 시 첫 스토리, 전투 패배 시 `DefeatEnding` |
  | `LastBattle` | App: Battle을 열기 직전 `null`로 비움 (r2) / Combat: 전투 종료 직전 한 번 기록 | 이전 결과를 다시 쓰는 일 방지 |
  | `Targeting` | Combat(Battle 화면) | 전투 중 플레이어가 바꿀 때 |
  | `Gold`, `Heroes`, `Squads`, `Wall` | Meta(Result·Prep 화면) | 결과 반영(C-007)과 정비(수리·모집·상점) |
  | `Heroes`·`Squads`의 `PostId` | App: 새 게임 때 `CampaignStartDef`의 첫 배치로 채움 / **Meta(Prep 화면)**: 정비 중 배치 변경 (D5) | Battle 화면은 배치를 바꾸지 않는다(가서 대포·표적 설정만) |
  | `Traps` | Meta: Prep(구매·배치) / **Result(전투에서 쓴 함정 반영, r2)** | 함정 구매·배치는 C-008, 소비 반영은 C-007 |
  | `NextInstanceNumber` | 새 분대·영웅·함정을 만드는 쪽 | `NewInstanceId` 호출로만 |
  | (읽기) | 모든 화면 | 자기 화면이 열려 있는 동안(C-003 컨텍스트 유효 기간) |

  - **전투 중 손실은 Combat이 `CampaignState`에 직접 쓰지 않는다.** 잃은 병력·성벽 피해·**발동해서 없어진 함정**은 `LastBattle`(C-006)에 담고, Result 화면이 한 번만 반영한다(C-007). 이렇게 하면 결과가 한 번만 반영된다 (r2, M-CX-009 요청 1).
  - **식별 (r2, M-CX-009 요청 2):** 결과의 손실은 병종 ID가 아니라 `SquadId`·영웅 `InstanceId`·함정 고유 ID(C-008)로 가리킨다. 같은 병종을 두 지점에 나눠도 어느 분대가 몇 명 잃었는지 정확히 맞출 수 있다.
  - **C-006·C-007에 넘길 요구사항 (r2):** `BattleResult`는 끝난 전투의 번호(`BattleNumber`)를 담는다. Result는 결과 번호가 현재 전투와 맞는지, 이미 반영했는지 확인한 뒤 한 번만 반영한다(중복 반영 방지 규칙은 C-007에서).
  - 패배 시에는 Result를 건너뛰므로 반영하지 않는다(어차피 폐기).
  - 전투 승패: App은 Battle의 `Done` 신호를 받으면 `LastBattle`의 승패 값을 보고 Result(승리) 또는 Story 패배 엔딩(패배)으로 보낸다(C-003 순서표).

  **4) 1회차 초기값** — 시작 골드, 시작 병력, 성벽 구간 수·내구도 같은 숫자는 이 약속에 적지 않는다. 정의 데이터의 "1회차 시작 설정" 에셋(C-010 `CampaignStartDef`)에서 읽는다.
- 사용처: App(생성·보관·흐름), Combat, Frontend(시작 신호만), Story(`NextStory` 읽기), Meta
- 변경 규칙: 필드 추가는 새 판(r)으로 기록하고 사용자 승인. 필드 삭제·의미 변경은 사용 중인 쪽의 동의(메시지) + 사용자 승인. 쓰는 쪽(3) 변경도 사용자 승인.

## C-010 정의 데이터 클래스
- 상태: **승인** (2026-10-13, 사용자 확정 D-015. r3 기준 — Codex 검토 M-CX-009 반영, D3·D4 답변 포함). 단 `TrapDef` 필드는 C-008, `ItemDef` 효과는 Codex 정비 설계(P1-X06)에서 정한다
- 위치: 클래스는 `HLR.Core` (`HumanLastRampart.Core.Definitions`), 에셋은 C-001의 `Data/` 하위 폴더
- 정의:

  **1) 공통 규칙**
  - 모두 **ScriptableObject**이고 공통 부모 `DefinitionBase`(`Id`, `DisplayName`)를 상속한다. 기본 수치만 담고, 게임 중에 값을 바꾸지 않는다(읽기 전용으로 쓴다).
  - 모든 정의는 고유 `Id`(영문 소문자·숫자·`_`, 예: `enemy_ghoul`)와 화면에 보일 `DisplayName`(한국어, Q-A6)을 가진다. 진행 데이터는 이 `Id`로 가리킨다(C-004).
  - **클래스(코드)는 Claude가 Core에 만들고, 에셋(실제 수치)은 C-001 폴더 소유자가 만든다.** 필드 추가는 Core 변경이므로 메시지로 요청한다.
  - 수치 밸런스는 약속이 아니다. 에셋 소유자가 자유롭게 고친다.

  **2) 클래스 목록 (제안)**

  | 클래스 | 에셋 위치·작성 | 담는 것 (1회차 최소) |
  |---|---|---|
  | `UnitDef` | `Data/Combat/` Claude | 아군 유닛: 구분(`UnitKind`: Hero 주인공 / Officer 특수 장교 / Elite 강력한 병사 / Soldier 일반 병사 / SpecialSoldier 특수 병사), 최대 체력, 기본 무기(`WeaponDef` ID), 모집 비용, 스프라이트 |
  | `WeaponDef` | `Data/Combat/` Claude | 무기: 종류(`WeaponKind`: Bow 활 / Crossbow 석궁 / Cannon 대포 / Gun 총 / Magic 마법 …), 피해, 사거리, 공격 간격, 범위(광역이면 반경) |
  | `EnemyDef` | `Data/Combat/` Claude | 적: 일반/특수 구분(`IsSpecial`), 최대 체력, 이동 속도, 성벽 공격력, 처치 보상 골드, 스프라이트 |
  | `WaveDef` | `Data/Combat/` Claude | 전투 하나의 적 구성: 등장 순서·수·간격 (상세는 전투 설계 P1-C09) |
  | `TrapDef` | `Data/Traps/` 공용 | 함정: 가격, 발동 조건, 피해 — **필드와 작성자 경계는 C-008에서 정함** |
  | `ItemDef` | `Data/Meta/` Codex | 상점 품목: 가격, 효과 종류 — 1회차 품목과 효과는 Codex 정비 화면 설계(P1-X06)에서 정함 |
  | `CampaignStartDef` | `Data/Combat/` Claude (D3) | 1회차 시작 설정: 시작 골드, 시작 영웅·분대와 **첫 배치 지점**(D5), 성벽 구간과 내구도, 첫 스토리 ID, 전투 번호별 `WaveDef`. Codex는 정비 쪽 값 조정이 필요하면 메시지로 요청 |

  - 종류 목록(`UnitKind`, `WeaponKind`)은 FirstRead의 병력·무기 구분을 따른다. 1회차에 실제로 쓰는 것은 일부(무기 1~2종, 적 1~2종 — RULES 12장)이고, 나머지 값은 미리 두기만 한다.
  - `StoryDef`(스토리 데이터)는 C-009에서 Codex 제안으로 정한다. 상점 외 모집 목록·수리 비용 정의가 필요하면 Codex가 P1-X06 설계에서 제안한다.

  **3) 정의 찾기**
  ```csharp
  namespace HumanLastRampart.Core.Definitions
  {
      public interface IDefinitionLookup
      {
          T Get<T>(string id) where T : DefinitionBase;   // 없으면 null + 에러 로그
          IReadOnlyList<T> All<T>() where T : DefinitionBase;
      }
  }
  ```
  - App이 구현해서 화면에 넘긴다. `IScreenContext.Definitions`로 받는다 (D4, C-003 v1.1).
  - 에셋 목록은 소유자별 **카탈로그 에셋**으로 모은다: `Data/Combat/CombatCatalog`(Claude), `Data/Meta/MetaCatalog`(Codex), `Data/Story/StoryCatalog`(Codex), `Data/Traps/TrapCatalog`(C-008에서 작성자 결정). 각 소유자가 자기 카탈로그에 에셋을 등록하고, Claude가 Boot에서 카탈로그들을 연결한다.
  - `Resources` 폴더와 Addressables는 1회차에 쓰지 않는다.

  **4) 카탈로그 검증 (r2, M-CX-009 요청 3)** — 잘못된 데이터로 새 게임이 반쯤 만들어지는 일을 막는다.
  - **검증 시점:** App이 Boot에서 카탈로그를 합칠 때 한 번, 메인 화면을 열기 전에 검사한다.
  - **검사 항목:** 비어 있는 ID, 카탈로그 안·카탈로그 사이의 **중복 ID**, 빈 칸(null) 항목, 정의 종류가 맞지 않는 참조(예: `UnitDef`의 기본 무기 ID가 `WeaponDef`가 아님), 없는 ID를 가리키는 참조(무기, 적, 웨이브, 시작 설정의 영웅·분대·첫 스토리 등), `CampaignStartDef`가 정확히 하나인지.
  - **실패하면:** 문제를 **모두** 에러 로그로 남긴다(카탈로그 이름·에셋 이름·ID). 중복 ID를 "나중 것이 이긴다"는 식으로 덮어쓰지 않는다. 메인 화면은 열리지만 App은 `StartNewGame`을 받아들이지 않고 에러 로그를 남긴다(망가진 새 게임을 만들지 않음).
  - 에디터에서 미리 검사하는 메뉴 도구는 Phase 2에서 Claude가 제공할 수 있다(선택).
- 사용처: 모든 모듈 (읽기)
- 변경 규칙: 클래스·필드 추가는 Core 변경(공용 경로 예약) + 사용자 승인. `Id` 값은 한 번 쓰기 시작하면 바꾸지 않는다(진행 데이터가 가리키므로).

### C-004·C-010 결정 항목 (사용자 답변 완료, D-015)

| ID | 질문 | Claude 제안 |
|---|---|---|
| D1 | 일반·특수 병사를 어떻게 셀까 | **병종별 인원 수**(예: 궁수 20명). 주인공·특수 장교·강력한 병사만 한 명씩 관리. FirstRead의 "일반 병사는 단체 훈련·단체 아이템으로 강화"와 맞고, 대규모 전투에 가볍다 |
| D2 | 화폐 종류 | 1회차는 **골드 하나** |
| D3 | 1회차 시작 설정(시작 골드·병력·성벽) 에셋의 작성자 | **Claude**. 전투 균형과 직결되므로 전투 담당이 만든다. Codex는 정비 쪽 값 조정이 필요하면 요청 |
| D4 | `IScreenContext`에 정의 찾기(`Definitions`) 추가 (C-003 변경) | 추가. 모든 화면이 이름·가격을 표시하려면 필요 (Codex 동의 M-CX-009) |
| D5 | 영웅·분대를 성벽 위 어느 지점에 둘지(배치)는 어느 화면에서 정할까 (r2 추가) | 1회차는 **정비 화면(3번, Codex)에서 배치**, 전투 화면에서는 배치된 병사에게 가서 대포·표적 설정만 한다. 첫 전투 배치는 시작 설정(D3)에 넣는다 |

#### C-004·C-010 변경 기록
| 날짜 | 판 | 내용 |
|---|---|---|
| 2026-10-13 | r1 | 초안 작성 (제안) |
| 2026-10-13 | r2 | Codex 검토 M-CX-009 반영: 분대 `SquadId`·영웅 `PostId`·고유 ID 발급, 함정 소비는 `LastBattle`→Result 반영, `LastBattle`은 전투 직전 비움, C-006·007 요구사항(끝난 전투 번호, 1회 반영), 카탈로그 검증과 실패 시 새 게임 차단. 결정 D5 추가 |
| 2026-10-13 | r3 | 사용자 답변 반영 후 승인(D-015): D1 분대별 인원 수, D2 골드 하나, D3 시작 설정 Claude, D4 화면 컨텍스트에 Definitions(C-003 v1.1), D5 배치는 정비 화면·첫 배치는 시작 설정 |

**답변 (2026-10-13, D-015):** D1 분대별 인원 수 · D2 골드 하나 · D3 Claude · D4 추가 · D5 정비 화면에서 배치(첫 전투 배치는 시작 설정). 위 표의 'Claude 제안' 열은 기록으로 남긴다.

---

## C-003 v1.1 화면 컨텍스트에 정의 찾기 추가
- 상태: **승인** (2026-10-13, 사용자 확정 D-015, D4 답변). C-003(v1)의 나머지 내용은 그대로이고, 이 항목만 더한다.
- 위치: `HLR.Core` (`HumanLastRampart.Core.Flow`)
- 정의: `IScreenContext`에 속성 하나를 추가한다.
  ```csharp
  public interface IScreenContext
  {
      CampaignState Campaign { get; }          // v1
      IDefinitionLookup Definitions { get; }   // v1.1 추가 (C-010 3)
      void Complete(ScreenOutcome outcome);    // v1
  }
  ```
  - 컨텍스트가 무효가 된 뒤(C-003 3-1) `Definitions`에 접근하면 `Campaign`과 같이 `InvalidOperationException`을 던진다.
- 사용처: 모든 화면 (읽기)
- 변경 규칙: C-003과 같다.

---

## C-005 전투 준비 정보 BattleSetup
- 상태: **제안 r1**
- 위치 제안: HLR.Core / HumanLastRampart.Core.CombatContracts (클래스 구현 담당 Claude, 이번에는 문서만 작성).
- 만드는 쪽: App. 읽는 쪽: Combat.

### 전달할 정보
| 항목 | 의미 |
|---|---|
| BattleRunId | 이번 전투 실행의 고유 문자열 ID. App이 매번 새로 발급하며 새 게임에서도 이전 실행과 겹치지 않음 |
| BattleNumber | 시작하는 전투 번호. CampaignState.BattleNumber의 시작 시점 값 |
| WaveDefId | 시작 설정에서 이 전투 번호에 대응하는 웨이브 ID |
| Heroes | 영웅 InstanceId·UnitDefId·Hp·IsAlive·PostId의 복사본 |
| Squads | 분대 SquadId·UnitDefId·Count·PostId의 복사본 |
| Wall | SegmentId·MaxHp·Hp의 복사본 |
| Traps | 고유 배치 ID를 보존한 설치 함정 복사본. 상세 필드는 C-008에서 확정 |
| ParticipatingHeroIds | 이번 전투에 실제 참가하는 영웅 InstanceId 목록. 배치 규칙으로 App이 선정하고 Combat이 준수 |
| InitialTargeting | 표적 설정 복사본. 상세 필드는 C-011에서 확정 |

- App은 현재 Campaign과 정의 조회로 준비 정보를 만들고, Battle을 열기 직전 LastBattle을 null로 비운다(승인 C-004 유지).
- 준비 정보의 리스트와 내부 객체까지 복사해 Campaign과 가변 객체를 공유하지 않는다. Combat은 준비 정보를 전투 내부 상태로 다시 복사한다. 전투 손실로 진행 데이터나 ScriptableObject를 직접 변경하지 않는다.
- Combat이 전투 중 바꾼 표적 설정만 승인 C-004대로 Campaign.Targeting에 기록한다. 준비 정보 자체는 변경하지 않는다.
- ID 중복, 누락된 정의/웨이브, Hp 범위, 음수 분대 수, 없는 배치 지점은 시작 전 검증한다. 지점 규격은 전투 설계에서 확정한다. 0명 분대·사망 영웅·미배치 병력의 실제 참가 규칙은 전투 설계에서 정하며 임의로 전장에 넣지 않는다.
- 검증 실패 시 Battle을 열지 않고 메인으로 복귀하거나 시작을 거부한다. 실패 원인을 로그로 남기고 일부 병력만 빼서 전투를 시작하지 않는다.
- 웨이브가 없는 다음 전투 번호의 완료/엔딩 정책은 결정 필요 E3. 마지막 웨이브 자동 반복은 하지 않는다.
- 전달 경로 제안: CampaignState.ActiveBattleSetup을 App이 설정하고, Combat이 OnEnter의 context.Campaign에서 읽는다(C-004 v1.1 추가 제안, 아래 참조). App은 Result/패배 엔딩 처리가 끝날 때까지 유지한다.

## C-006 전투 결과 BattleResult
- 상태: **제안 r1**
- 위치 제안: HLR.Core / HumanLastRampart.Core.CombatContracts.
- 만드는 쪽: Combat. 읽는 쪽: App(승패 분기), Meta.Result(승리 결과 표시·반영), Story(패배 엔딩 재생; 전투 데이터 상세 사용은 C-009에서 정함).

### 전달할 정보
| 항목 | 의미 |
|---|---|
| BattleRunId | 시작 BattleSetup과 같은 실행 ID |
| BattleNumber | **끝난 전투 번호**. 다음 번호로 바뀐 Campaign.BattleNumber를 복사하지 않음 |
| Outcome | Victory 또는 Defeat. 판정 조건은 전투 설계에서 정함 |
| DefeatedEnemyCount | 처치 수. 0 이상 |
| EarnedGold | 얻은 골드. 0 이상. 승리 시만 반영 |
| HeroOutcomes | InstanceId별 HpAfter·IsAliveAfter. 참가 영웅 각각 정확히 한 항목 |
| SquadLosses | SquadId별 LostCount. 시작 Count 이하, 0 이상. 손실 있는 분대만 기록 가능 |
| WallOutcomes | SegmentId별 HpAfter. 시작 Hp 이하, 0 이상. 피해 있는 구간만 기록 가능 |
| ConsumedTrapIds | **발동해 소비된** 함정의 고유 배치 ID 목록. 함정 정의 ID와 다름 |

- BattleSetup의 영웅/분대/성벽/함정 ID로 결과를 식별한다. 같은 병종을 여러 지점에 배치해도 SquadId로 손실 대상을 정확히 맞춘다.
- 목록은 null이 아닌 빈 목록을 허용한다. 동일 ID의 중복 항목, 알 수 없는 ID, 잘못된 범위는 유효한 결과로 받지 않는다. 병종 ID로 대신 매칭하거나 중복을 합쳐 숨기지 않는다.
- 영웅의 사망은 HpAfter=0, IsAliveAfter=false와 일치해야 한다. 부활·전투 중 회복/모집·수리는 현재 초안에 포함하지 않으며 추가 시 결과 규격을 다시 검토한다.
- 소비되지 않는 함정의 반복 발동 횟수와 재사용 여부는 C-008에서 정한다. ConsumedTrapIds에 있는 설치물만 제거하고 모든 발동 함정을 임의로 삭제하지 않는다.
- Combat은 전투 종료 시 결과를 한 번 완성하고 Campaign.LastBattle에 기록한 뒤 Complete(Done)를 보낸다. 이후 결과를 수정하지 않는다.
- App은 실행 ID와 끝난 번호, 승패와 결과 형식을 먼저 검사한다. null/이전 실행/잘못된 결과이면 승리·패배 분기를 진행하지 않고 에러를 기록해 메인으로 복귀한다.
- **승리:** C-004에 따라 App이 Campaign.BattleNumber를 끝난 번호+1로 한 번 증가시키고 Result를 연다.
- **패배:** 전투 번호를 증가시키지 않고 StoryRequest.Kind=DefeatEnding으로 패배 엔딩을 연다. Result를 건너뛰며 보상·손실·함정 소비를 Campaign에 반영하지 않는다(D-014). 엔딩 종료 후 Campaign을 폐기한다.

## C-007 결과 반영 규칙
- 상태: **제안 r1**
- 담당: Meta.Result(Codex). App은 흐름과 전투 번호만 관리한다. Combat은 결과만 만든다.

### 반영 시점과 중복 방지
- Result.OnEnter에서 승리 결과를 검사하고 **표시 전에 한 번** 반영한다(E1 제안). 다음 버튼은 반영을 다시 실행하지 않고 Complete(Done)만 보낸다.
- 반영 대상: 골드, 영웅 체력·생존, 분대 인원, 성벽 체력, 소비된 함정.
- 현재 ActiveBattleSetup과 LastBattle의 BattleRunId·BattleNumber가 모두 같아야 한다.
- 승리 Result에서 Campaign.BattleNumber는 LastBattle.BattleNumber+1이어야 한다. 현재 번호와 결과 번호가 같아야 한다고 검사하면 승인 C-004의 전투 번호 증가 시점과 충돌하므로 **끝난 번호+1**로 검사한다.
- LastAppliedBattleRunId가 같은 결과의 실행 ID이면 **이미 반영됨**으로 처리해 아무 값도 다시 변경하지 않는다. 화면에는 보관된 결과를 표시할 수 있다.
- 중복 여부 검사는 시작 상태와 현재 상태 비교보다 먼저 수행한다. 이미 반영됐으면 상태가 달라진 것이 정상이며 다시 검증·반영하지 않는다.
- App이 새 게임을 만들 때 반영 표시는 비운다. 전투마다 새로운 실행 ID로 다른 게임의 1번 전투 결과가 섞이지 않게 한다.
- LastBattle/ActiveBattleSetup은 Result 화면과 이후 Prep에서 반영 검사·표시를 마칠 때까지 보존하고, 다음 Battle 시작 때 새 준비 정보를 설정하며 LastBattle을 비운다.

### 모든 값 검증 → 새 상태 준비 → 한 번에 반영
1. 실행 ID·번호·Victory 여부·중복 여부 검사.
2. 모든 영웅/분대/성벽/함정 ID와 수치 범위를 시작 스냅샷 및 현재 Campaign과 대조. 결과 반영 전 Campaign의 해당 상태가 시작 상태와 다르면 잘못된 재반영/예상 밖 변경으로 거부한다.
3. 정비 이전의 새 리스트와 최종 골드를 메모리에서 준비한다. Gold+EarnedGold 정수 범위 초과도 먼저 검사한다. 이 단계에서는 Campaign을 수정하지 않는다.
4. 검증·준비가 모두 성공하면 동기 처리 한 구간에서 준비된 값들을 Campaign에 설정하고 **마지막에** LastAppliedBattleRunId를 기록한다. 중간에 기다리거나 화면 전환·외부 콜백을 호출하지 않는다.
5. 실패하면 준비한 값을 폐기한다. Campaign의 골드·병력·성벽·함정·반영 표시는 그대로 두고, 다음 버튼을 비활성화해 정비 진입을 막는다. 오류 로그 후 메인 복귀만 허용하는 것을 제안한다(E2).

### 반영 내용
- 골드: 현재 Gold + EarnedGold.
- 영웅: 참가 영웅별 Hp·IsAlive만 갱신. InstanceId·UnitDefId·PostId는 유지. 사망 영웅은 표시를 위해 목록에 남긴다; 이후 회복/퇴장 정책은 정비 설계에서 결정한다.
- 분대: 시작 Count에서 LostCount를 뺀다. 0명 분대는 승인 C-004대로 목록에서 제거한다. 살아남은 분대의 SquadId·UnitDefId·PostId는 유지한다.
- 성벽: 결과가 보고한 구간의 Hp를 HpAfter로 갱신. MaxHp·SegmentId는 유지한다.
- 함정: ConsumedTrapIds와 일치하는 설치물만 목록에서 제거한다. 소비되지 않은 설치물의 위치·정의는 유지한다. 전투 결과가 Prep의 구매 비용을 다시 청구하지 않는다.
- 표적 설정: Combat이 갱신한 값 유지. Result가 시작 스냅샷 값으로 되돌리지 않는다.
- 실패 엔딩: Result를 열지 않으므로 이 반영 절차를 실행하지 않는다.

## C-004 v1.1 전투 준비 및 결과 반영 표시 (제안)
기존 승인 C-004 r3는 수정·대체하지 않고 다음 두 정보를 추가하는 새 버전을 제안한다.

| 추가 정보 | 초기값 | 작성 권한 |
|---|---|---|
| ActiveBattleSetup | null | App: 시작 준비 및 다음 전투 전 교체. 다른 화면은 읽기만 |
| LastAppliedBattleRunId | null | App: 새 게임 초기화. Meta.Result: 성공 반영 직후 한 번 기록 |

- 이 변경과 C-005~007을 함께 승인해야 제안한 전달·중복 방지 방식이 확정된다.
- 새로운 타입·필드·위치의 실제 C# 구현은 승인 이후 담당자 작업이며 이번 문서는 구현하지 않는다.
- C-008의 함정 고유 배치 ID·소비 여부, C-011의 표적 설정, 전투 배치 지점과 승패 조건은 해당 약속/설계 승인 전까지 미정이다.

## 사용자 결정 항목
| ID | 질문 | 제안 |
|---|---|---|
| E1 | 승리 보상·손실은 언제 실제 반영할까? | 결과 화면이 열릴 때 한 번 반영. 다음 버튼은 정비 이동만 |
| E2 | 잘못된 결과/반영 실패 시 어떻게 할까? | 아무 값도 부분 반영하지 않고 에러 기록. 정비 이동 차단 후 메인으로 복귀 |
| E3 | 설정된 마지막 전투를 이긴 뒤 다음 웨이브가 없으면? | 첫 완주 검증용으로 최소 두 전투의 웨이브를 준비. 그 이후는 메인으로 복귀하며 승리 엔딩 확장은 다음 범위에서 결정. 최종 전투 결과·정비 표시는 유지 |
| E4 | 전달·중복 방지를 위한 C-004 v1.1 두 필드 추가? | ActiveBattleSetup·LastAppliedBattleRunId 추가. 실행 ID와 끝난 전투 번호 둘 다 검사 |

## 문서 검토 사례 (실제 런타임 시험 아님)
| 사례 | 기대 정책 |
|---|---|
| 1번 승리, Campaign.BattleNumber=2 | 결과 번호 1/실행 ID 대조 후 반영 허용 |
| 같은 결과 화면 재진입·버튼 연타 | LastAppliedBattleRunId 대조로 골드·손실 재반영 없음 |
| 새 게임 1번에 이전 게임 1번 결과 도착 | 실행 ID 불일치로 거부 |
| 같은 병종의 두 분대 중 하나만 손실 | SquadId에 맞는 분대만 인원 감소 |
| 함정 A 소비, 함정 B는 재사용 | A만 제거, B 유지 |
| 목록 마지막 항목이 잘못됐거나 골드 넘침 | 검증 단계 거부; 골드·병력·벽·함정 모두 미변경 |
| 패배 | 결과 반영 없이 패배 엔딩, 종료 후 진행 데이터 폐기 |

- 한계: 문서 정합성만 검토. 코드·Unity Play·런타임 동작 검증은 수행하지 않음.

### 작성·변경 기록
- 2026-10-13 r1: Codex 임시 조정 담당이 작성(T-CX-006). 사용자 '세 파일 수정 허가'로 CONTRACTS.md·ROADMAP.md·ROADMAP.html에 반영. 승인된 기존 본문은 보존하고 새 제안만 추가했다.
- E1~E4 답변 및 최종 승인 전까지 C-005~C-007과 C-004 v1.1은 구현 근거로 사용하지 않는다.
