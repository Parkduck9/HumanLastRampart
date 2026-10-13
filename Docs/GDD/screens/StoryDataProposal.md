# C-009 스토리 데이터 제안 r1

- 상태: **제안 — 사용자 승인 전 구현하지 않음**
- 작성자: Codex / 2026-10-13 / T-CX-012·CY1-X2·P1-X03
- 근거: D-014 패배 엔딩, D-031 예보 힌트, D-036 작업 목록, C-001~004·C-010 승인분.
- CONTRACTS.md 정식 반영은 Claude에게 요청한다. 이 파일은 Codex 제안 원본이다.

## 첫 범위

일반 첫 스토리와 패배 엔딩을 대사 목록으로 재생한다. 인물 초상·배경·이름·대사를 표시하고 다음 버튼으로 한 줄씩 진행한다. 마지막 줄에서 Complete(Done)을 한 번 보낸다. 이동할 화면은 App이 정한다. 분기 선택·보이스·영상·자동 재생·진행 저장은 후속 범위다.

임시 ID story_intro와 story_defeat를 **첫 정식 데이터 ID로 유지**하는 것을 추천한다. 둘은 이미 App·시작 데이터에 쓰이며 값 변경만으로 얻는 이익이 없다. 임시 코드 대사 대신 이 ID의 실제 정의 에셋을 등록하면 임시 상태를 끝낼 수 있다. 이름 변경을 원하면 사용자 승인 뒤 Claude가 App/시작 데이터와 함께 바꾼다.

## 제안 데이터와 위치

| 항목 | 제안 | 작성자/위치 |
|---|---|---|
| StorySequenceDef | DefinitionBase 상속, Id·DisplayName, Kind(StoryKind), Lines | Codex Story/ 코드 |
| StoryLine | LineId, Kind(Dialogue 또는 ForecastHint), SpeakerName, Text, Portrait(Sprite 선택), Background(Sprite 선택) | Codex Story/ 코드 |
| 스토리 정의 에셋 | story_intro, story_defeat | Codex Data/Story/ |
| StoryCatalog | 기존 DefinitionCatalog에 위 정의 등록 | Codex Data/Story/ |
| 툴 | Story.Editor 전용 asmdef, 순서 편집·검사·미리보기 | Codex Story/Editor/ |

StorySequenceDef를 Core에 추가하지 않는다. Story는 Core의 DefinitionBase와 StoryRequest를 읽고 context.Definitions.Get<StorySequenceDef>(StoryId)로 자기 타입을 가져온다. App은 구체적인 Story 타입을 참조하지 않는다. Boot 카탈로그 연결은 Claude에게 경로로 요청한다. Story 데이터의 추가 유효성 검사는 Story 진입과 제작 툴이 담당한다.

LineId는 시퀀스 안에서 고유한 문자열이다. 현재는 재생 위치 추적과 편집 오류 위치를 찾는 용도이며 저장/스킵 이력은 만들지 않는다. 배경/초상 참조는 기존 에셋을 읽어 사용한다. UI 테마나 스프라이트를 이 약속으로 확정하지 않는다.

## 예보 힌트

ForecastHint 줄은 다음 전투의 초기 날씨·밤낮을 말하는 한 줄을 생성한다. BattleForecast.Get(campaign.RngSeed, campaign.BattleNumber, rules)를 사용하고 독립 난수 추첨이나 시드 변경을 하지 않는다. 첫 범위는 InitialWeather와 TimeOfDay만 사용하며 변화 시각·규모·구체적인 적 배치는 공개하지 않는다.

rules는 Definitions.All<BattleRulesDef>()의 유일한 정의를 사용한다. 0개/2개 이상이거나 판 데이터가 없으면 데이터 오류로 처리한다. 힌트는 전투 준비와 같은 seed·번호·규칙으로 계산되어야 한다. 패배 엔딩에는 예보 줄을 허용하지 않는다.

예시 문구(본문 확정 아님): 맑음 ‘하늘이 맑다’, 흐림 ‘먹구름이 드리웠다’, 비 ‘빗속에서 방어를 준비한다’, 안개 ‘성 밖이 안개에 가려졌다’. 밤이면 ‘야간 경계가 필요하다’를 덧붙인다. 대사 내용·인물 이름은 사용자가 나중에 작성한다.

## 수명·오류 처리

OnEnter 뒤 StoryRequest/Kind/StoryId와 정의를 대조한다. ID 없음·잘못된 종류·빈 대사·중복 LineId·잘못된 줄 enum·예보 규칙 오류는 ReportDataError로 전달하고 다음 입력을 차단한다. 누락 데이터를 빈 화면으로 넘어가며 숨기지 않는다.

정상 줄은 목록 순서로 한 번씩 진행한다. 마지막 클릭 직전에 입력을 막고 Done만 전달한다. OnExit에서 버튼 구독·초상/배경 표시·재생 인덱스·컨텍스트를 정리한다. 대사 재생으로 Campaign의 Gold/병력/결과/난이도를 변경하지 않는다. None Portrait는 인물 이미지를 숨기고, None Background는 기본 바탕을 사용한다.

## 승인 요청

S1 위 데이터 타입·필드·소유권, S2 story_intro/story_defeat를 정식 ID로 유지, S3 예보는 초기 날씨·밤낮만, S4 누락/불일치는 Boot E2 안내. 모두 위 추천안으로 제안한다. 사용자 승인 후 Story 코드·데이터·Editor 툴을 순차 구현한다.
