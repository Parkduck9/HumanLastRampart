# K-062 결과 — 메인 설정 창, 진행 저장·이어하기

작성 Claude · 2026-10-19 · 가지 card/K-062 · **유니티 실행·컴파일 안 함**

## 1. 조사한 지금 상태
- **메인 메뉴:** '게임 시작'(→ 난이도)과 '종료'뿐이었다. 설정 버튼·설정 창·설정 저장은 없었다. Codex 제안 `Docs/GDD/screens/MainMenu.md` r1은 설정으로 "마스터 음량·화면 모드 정도"를 추천하고, 저장 위치·적용 방법은 승인 대기로 남겨 두었다.
- **진행 저장·불러오기·이어하기:** 없었다. CampaignState는 처음부터 '저장 대비'로 `[Serializable]` 공개 필드·ID 문자열 구조라 통째 직렬화가 가능하다. 사전·속성·null 허용 값 형식 필드도 없다.
- **소리:** 게임에 아직 없다. 그래서 소리 크기는 자리만 만들었다(`AudioListener.volume`에 적용).

## 2. 만든 것
### 설정 (`Core/Settings/GameSettings.cs`, PlayerPrefs)
설정 창(`Frontend/SettingsPopup.cs`)은 줄마다 버튼 하나이고, 누를 때마다 다음 값으로 바뀌며 바로 저장·적용된다. 마우스·키보드 모두 쓸 수 있다.

| 항목 | 값 | 적용되는 곳 |
|---|---|---|
| 소리 크기 | 0·25·50·75·100% | App `AudioListener.volume` (소리는 아직 없음) |
| 화면 효과 | 켬·끔 | Combat `BattleLook.Refresh` — 끄면 G 키 끔과 같다(빛번짐·색 보정·미니어처 흐림). 캐릭터 빛 받기는 그대로 |
| 미니어처 흐림 | 끔·약하게·보통 | 흐림 반경에 곱한다(빛줄기는 그대로). 매 프레임 읽어서 전투 중에도 바로 바뀐다 |
| 카메라 흔들림 | 켬·끔 | `BattleCameraRig.Shake` |
| 화면 | 전체 화면·창 | App `Screen.fullScreenMode`. 한 번이라도 골랐을 때만 바꾸고, 아니면 실행 기본값 그대로 |
| 글자 크기 | 보통·크게·아주 크게 | **값만 저장** (아래 질문 1) |
| 기본값으로 | — | 모두 처음 값 |

### 진행 저장·이어하기 (`Core/Save/CampaignSave.cs`)
- **자동 저장:** 정비 화면에 들어올 때마다 슬롯 1개(`persistentDataPath/save/slot1.json`)에 저장한다. 임시 파일에 쓴 뒤 바꿔 끼운다.
- **통째 저장:** 필드 목록을 손으로 적지 않고 CampaignState를 통째로 JsonUtility로 저장한다. K-058이 필드를 더해도 그대로 따라간다.
- **판 번호:** `FormatVersion` 1을 적는다. 불러올 때는 기본값 객체 위에 덮어쓰므로 예전 파일에 없는 필드는 기본값이 된다. 더 새 판 파일은 거부한다.
- **null 필드:** JsonUtility는 null 객체를 빈 객체로 바꾸므로, null이던 맨 위 필드 이름을 기록해 두었다가 되돌린다.
- **메인 '이어하기 — N번째 전투 앞 정비 (난이도)':** 저장이 있을 때만 맨 위에 나온다. 누르면 불러온 뒤 정비 화면으로 간다. 못 읽으면 오류 안내를 띄우고 메인으로 간다.
- **저장 지우기:** 패배 엔딩 뒤, 마지막 전투 정비를 마친 뒤 지운다. 도중에 '메인으로'를 고르면 남긴다.
- **흐름 신호:** `ScreenOutcome.ContinueGame`을 맨 뒤에 추가했다(기존 값 그대로).

### 계약 (CONTRACTS 맨 아래)
- C-003 v1.4 초안(이어하기 신호), C-004 v1.5 초안(저장 파일 형식, 진행 데이터에 필드를 더할 때 지킬 것)

### 시험
- **편집:** `Core/Tests/CampaignSaveTests`(4개)
  - 저장 → 불러오기가 같은 JSON인지 (필드를 손으로 비교하지 않음)
  - null 유지, 고유 ID 번호 이어짐, 미리 보기·지우기
  - 없는 필드는 기본값, 깨진 파일·더 새 판 거부
- **편집:** `App/Tests/SaveFlowTests`(2개) — 이어하기 → 정비, 판이 끝날 때만 저장 지움
- **플레이:** PlayProbe `SettingsAndContinue` — 저장은 임시 폴더에서만 하고 사용자 저장은 건드리지 않는다.
  - 사진: `main_menu_no_save`, `settings_popup`, `settings_popup_toggled`, `main_menu_continue`, `prep_after_continue`
  - 확인: 설정 값 바로 바뀜, 닫으면 메뉴 입력 복귀, 이어하기로 2번째 전투 정비

## 바꾼 파일
- **새 파일:** `Core/Save/CampaignSave.cs`, `Core/Settings/GameSettings.cs`, `Frontend/SettingsPopup.cs`, `Core/Tests/CampaignSaveTests.cs`, `App/Tests/SaveFlowTests.cs` (+ .meta, 폴더 .meta)
- `Core/Flow/ScreenOutcome.cs` — ContinueGame
- `App/Flow/FlowRouter.cs` — LoadSave·DeleteSave, 이어하기 경로, 판 끝 저장 지움
- `App/Flow/GameFlow.cs` — 설정 불러오기·소리·전체 화면 적용, 이어하기 불러오기, 정비 진입 자동 저장, 저장 지움
- `Frontend/MainMenuEntry.cs` — 이어하기·설정 버튼, 설정 창 열고 닫기
- `Combat/Render/BattleLook.cs` — 화면 효과 설정·흐림 세기
- `Combat/Battle/BattleCameraRig.cs` — 흔들림 설정
- `Combat/_PlayProbe/PlayProbe.cs` — SettingsAndContinue
- `Docs/collab/rules/CONTRACTS.md` — 초안 2개

## 질문 (사용자 결정 필요)
1. **글자 크기**를 화면에 어떻게 반영할지: 글자만 키울지, UI 전체 배율을 키울지. 지금은 값만 저장한다. K-047 UI 부품(UI/Kit)이 들어오면 거기서 `GameSettings.FontScale`을 읽게 하는 것을 추천한다.
2. **새 게임을 시작하면 기존 저장은?** 지금은 바로 지우지 않고, 새 판이 첫 정비 화면에 들어올 때 덮어쓴다. 그 전에 새 판이 패배하면 저장도 지워진다. 시작할 때 "기존 진행이 지워집니다" 확인을 띄울지 정해야 한다.
3. **저장 시점:** 지금은 정비 화면에 들어올 때만 저장한다. 전투 도중·스토리 도중 저장은 없다. 전투 중에 끄면 그 전투 직전 정비부터 다시 하게 된다(같은 전투를 다시 시작).
4. **설정 창을 전투 중(일시정지 메뉴)에서도 열지:** 지금은 메인에서만 연다. 설정 값은 전투 중에도 바로 적용되게 만들어 두었다.
5. **창 모드 해상도 고르기**는 넣지 않았다(전체 화면/창만).

## 확실하지 않은 것
- 컴파일을 못 돌렸다.
- 설정 창 모양은 UiStyle 색만 쓴 임시다. K-047 UI 부품이 develop에 들어오면 창·버튼 부품만 바꿔 끼우면 된다(줄 목록·기능은 그대로).
- 다른 PlayProbe 흐름 시험도 정비에 들어오면 실제 저장 폴더에 저장을 남긴다. 그 뒤 메인 메뉴 맨 위에 '이어하기'가 생긴다. 시험들은 '게임 시작'을 글자로 찾아 누르므로 깨지지 않을 것으로 본다.
- 메인 창 높이를 버튼 수에 맞춰 늘렸다(640/760).
