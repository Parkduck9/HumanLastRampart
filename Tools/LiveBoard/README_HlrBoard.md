# HLR 현황판 (HlrBoard.exe)

알림 영역(작업 표시줄 오른쪽)에 아이콘 두 개를 띄웁니다. **클로드**, **코덱스**.

## 보이는 것
- 아이콘 숫자 = 5시간 한도의 **남은 %** (모르면 `--`). 한도가 다 차면 빨간색.
- 모든 숫자는 **남은 %** 입니다 (아이콘, 막대, 말풍선 모두).
- 아이콘에 마우스를 올리면 윈도우 기본 말풍선만 뜹니다. **왼쪽 누르면** 현황 창이 열리고, 바깥을 누르거나 아이콘을 다시 누르면 닫힙니다.
- 이번 주 / 5시간 남은 양 막대, 진행 중인 작업(단계·%·모델), 갱신 시각이 보입니다. 이 창은 키보드 포커스를 절대 가져가지 않습니다.
- 숨겨진 아이콘 칸(^)에 들어가 있으면 작업 표시줄로 끌어다 놓으세요. 그래야 숫자가 항상 보입니다.
- 서비스마다 아이콘이 둘씩(캐릭터 + 남은 % 숫자) 만들어집니다. 윈도우는 만든 순서대로 놓지만 숨겨진 칸(^)에 들어가면 끌어내 순서를 맞춰 주세요. 캐릭터가 필요 없으면 showCharacters를 alse로.
- 마우스만 올려도 열고 싶으면 설정 hoverOpen을 	rue로 (0.6초 뒤 열림).
- 오른쪽 누름 메뉴: 열기 / 떠 있는 창으로 보기 / 윈도우 시작 시 자동 실행 / 설정 파일 열기 / 종료.

## 실행·빌드
- 실행: `start.vbs` 또는 `HlrBoard.exe` 더블클릭 (이미 떠 있으면 창만 보여 줌).
- 다시 만들기: `app\build.cmd` (윈도우에 들어 있는 컴파일러만 사용).
- 그림 저장: `HlrBoard.exe --snapshot 파일.png`, 아이콘 그림: `--iconsnap 파일.png`.

## 친구에게 나눠 주기
- **HlrBoard.exe 파일 하나만** 복사하면 됩니다. 처음 실행하면 같은 폴더에 `settings.json`이 생깁니다.
- 코덱스 사용량은 `%USERPROFILE%\.codex\sessions` 기록에서 자동으로 읽습니다 (따로 설정 없음).
- 클로드 사용량은 `claude_usage.json` 파일이 있어야 보입니다. 없으면 "기록 없음". 필요 없으면 `showClaude`를 `false`로.
- `stages.json`이 없으면 작업 줄은 그냥 숨겨집니다.

## 설정 (settings.json, 고친 뒤 다시 시작)
| 키 | 뜻 |
|---|---|
| stagesFile | 작업 목록 파일 (비우면 안 씀) |
| claudeUsageFile | 클로드 사용량 파일 (`week`,`five`,`checked`,`fiveResetAt`,`weekResetAt`) |
| boardDataFile | 웹 현황판용 board_data.js 쓸 위치 (비우면 안 씀) |
| codexDir | 코덱스 폴더 (비우면 `%USERPROFILE%\.codex`) |
| projectFolder | `.claude\projects` 아래 프로젝트 폴더 이름 (코더 진행률용) |
| refreshSeconds | 갱신 간격(초) |
| floating, floatX, floatY | 떠 있는 창 켬/끔과 위치 |
| showClaude, showCodex | 아이콘·구역 표시 여부 |
| showCharacters | 숫자 아이콘 옆 캐릭터 아이콘 (기본 true) |
| hoverOpen | 마우스를 올리면 현황 창 열기 (기본 false) |

문제가 생기면 같은 폴더의 `HlrBoard.log`를 보세요.