# K-050 결과 — 5번 스토리 화면 + 스토리 제작 도구

작성: Claude (카드 세션) / 2026-10-16. **유니티를 열지 않았고 컴파일·시험을 돌리지 못했다.** 손으로 쓴 에셋(.asset·.meta)이 유니티에서 그대로 읽히는지 첫 확인이 필요하다.

## 만든 것

1. **데이터** `Story/StoryScript.cs` (ScriptableObject): 이야기 = ID·제목(도구용)·종류(보통/패배 엔딩)·작가 메모 + **장면 목록**. 장면 = 이름·배경 그림·줄 목록. 줄 = 말하는 사람·쪽(내레이션/왼쪽/오른쪽)·인물 그림·**표정 칸**(256×384 칸 격자 번호)·대사·이 줄만 배경·효과(없음/흔들림/암전). 검사 함수(`Validate`)가 빈 대사·이름 빠짐을 "장면 2 · 3줄" 식으로 알려 준다.
2. **화면** `Story/StoryPlayer.cs` + `StoryEntry.cs`: 배경(화면 덮기) + 좌우 인물(256×384 칸을 정수 2배로, 말하는 쪽 밝고 한 점 위로·듣는 쪽 어둡게, 장면이 바뀌면 내림) + 아래 대사 상자(이름표, **한 자씩**, 다 나오면 넘김 표시 까딱). 누르기(마우스·터치, 화면 어디든)·스페이스·엔터: 나오는 중이면 끝까지 → 다음 줄 → 마지막 다음이면 `Complete(Done)`. 오른쪽 위 **[자동]**(다 나오고 1.1초+글 길이만큼 뒤 저절로)·**[넘기기]**(바로 끝). 효과: 흔들림 0.45초, 암전 = 닫혔다가 이 줄과 함께 열림(첫 줄이면 까만 화면에서 열림). `CampaignState.NextStory`(Kind·StoryId) 흐름 그대로 — App이 다음 화면을 정한다.
3. **제작 도구** `Story/Editor/StoryEditorWindow.cs` (메뉴 **HLR/스토리 제작 도구**, 편집기 전용 asmdef): 이야기 고르기·[새 이야기], 장면 추가·삭제·위아래·이름·배경, 줄 추가(앞 줄 사람·그림 이어받기)·복사·삭제·위아래, 줄 편집(쪽 고르기, 말하는 사람 + 이미 나온 이름 단추, 인물 그림 끌어다 놓기, **표정 칸을 그림 단추로 고르기**, 대사, 효과, 이 줄만 배경), **게임과 같은 배치의 미리보기**(이 줄까지 서 있는 인물, 말하는 쪽 밝게, 대사 상자·이름표), [검사], [저장](파일 이름을 ID에 맞춤). 모든 편집은 Ctrl+Z 되돌리기.
4. **임시 이야기** `Story/Resources/Stories/story_intro.asset`(5줄: 내레이션 암전 → 궁수 → 장교 → 마법사 → 장교 흔들림, 메인 배경), `story_defeat.asset`(2줄, 패배 배경). 제목·메모에 **'임시'** 표시(화면에는 안 나옴). 에셋을 못 읽으면 `StoryPlaceholder.cs`의 같은 내용(그림 일부 없음)으로 재생해 흐름이 멈추지 않는다(경고 로그).
5. **그림 요청** `Docs/GDD/Story_에셋요청.md`: 대사 상자(손그림 잉크 테두리 양피지 9칸)·이름표·넘김 표시 움직임·작은 단추·암전 가장자리 + 상반신 표정 시트 제안. 지금은 코드로 그린 임시 도트(점 3px, 2점 잉크 테두리, 깎은 귀, 종이·잉크·나무·황동 색만, 글자 32/24, 글자 기호 대신 그린 삼각형).
6. **시험** `PlayProbe.cs`: 새 `StoryFlow`(누르기 판·[넘기기]·[자동] 존재, 첫 줄 사진 `story_0_typing`, 이름표 줄 사진 `story_1_speaker`, 누르면 끝까지 보이고 더 늘지 않음, 다음 줄로, 3줄 이상 뒤 전투). 기존 시험 20곳의 스토리 `Click("다음")`·패배 엔딩 `Click("메인으로")` → **`Click("넘기기")`**(스토리가 여러 줄이 되어 한 번 누르기로는 끝나지 않으므로).

## 바꾼 파일

- 새: `Story/StoryScript.cs`, `StoryPlayer.cs`, `StoryPlaceholder.cs`, `Story/Editor/StoryEditorWindow.cs`, `Story/Editor/HLR.Story.Editor.asmdef`, `Story/Resources/Stories/story_intro.asset`·`story_defeat.asset` (+ 모든 .meta), `Docs/GDD/Story_에셋요청.md`
- 고침: `Story/StoryEntry.cs`, `Story/HLR.Story.asmdef`(Unity.InputSystem 참조 추가), `Combat/_PlayProbe/PlayProbe.cs`
- 공용 UI(`ScreenView.cs` 등)는 고치지 않았다. `ScreenView(transform, "", "")`를 입력 막기·정리용으로만 만들고 `PanelGroup.transform.parent`(화면 캔버스) 위에 스토리 화면을 직접 그린다 — **K-047이 `ScreenView`의 `PanelGroup`이나 3인자 생성자를 바꾸면 여기를 맞춰야 한다.**

## Codex 제안(P1-X02~X04)과 다른 점

| 제안 | 이번 구현 | 이유 |
|---|---|---|
| `StorySequenceDef : DefinitionBase`, StoryCatalog에 등록, Data/Story에 에셋 | `StoryScript : ScriptableObject`, `Story/Resources/Stories/{ID}`에서 이름으로 읽음 | Boot 카탈로그·장면 연결을 건드리지 않고 Story 폴더 안에서 끝내려고. ID(story_intro·story_defeat)는 제안대로 유지 |
| 줄 = 대사 목록 하나 | 장면 → 줄 (장면마다 배경) | 사용자 범위 '장면 목록'. 장면이 바뀌면 인물을 내린다 |
| 처음엔 전체 대사 즉시, 타이핑·자동·건너뛰기는 나중 | 한 자씩·자동·넘기기 모두 지금 | 이번 카드 범위 |
| ForecastHint(예보 줄) | **안 만듦** | 이번 범위 밖. 필요하면 줄 종류에 추가 |
| 데이터 오류 → ReportDataError로 막기 | 에셋이 없으면 임시 이야기로 재생 + 경고 | 손으로 쓴 에셋이 처음에 안 읽혀도 흐름·시험이 멈추지 않게. 확인 뒤 오류로 바꿀 수 있음 |
| 도구: Undo/Redo, 검사, 정적 미리보기 | 같음 + 표정 칸 고르기, 이미 나온 이름 단추 | — |

## 확실하지 않은 것

1. 손으로 쓴 `.asset`(한글 대사, 텍스처 GUID 참조)과 `StoryScript.cs.meta` GUID 연결이 맞게 읽히는지. 안 읽히면 임시 이야기(장교·궁수 그림 없음)로 재생된다 — 로그의 "[스토리] … 임시 이야기를 씁니다"로 알 수 있다.
2. 장교 그림 = `hero_walk_run_grid` 12번 칸(4×4 시트의 4째 줄 첫 칸 = 앞모습, PrepYard 주석 기준). 다른 칸이면 도구에서 칸만 바꾸면 된다.
3. `Time.captureDeltaTime`이 `Time.unscaledTime`도 고정 간격으로 흐르게 하는지 — 아니면 시험에서 한 자씩 나오는 속도가 실제 시간 기준이 된다(시험은 글이 나오기 시작할 때까지 기다리므로 통과에는 문제없게 짰음).
4. 인물 512×768이 큰 화면에서 대사 상자에 발이 가려지는 높이(아래 140px)가 적당한지, 이름표·상자 색이 배경 그림 위에서 충분히 떨어져 보이는지 — 사진 확인 필요.
5. 이름표 너비는 글 길이로 정한다(`preferredWidth`, 첫 프레임 계산).
