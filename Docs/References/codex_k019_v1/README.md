# K-019 명령 메뉴 그림 — 1판

작성: Codex / 2026-10-16 / **사용자 확정 전 시안** (D-064). Game 연결 없음.

## 검토

`../../../_HTML/codex_K-019_commands_v1.html`을 서버 없이 직접 연다. 모든 그림과 기존 주인공 참고 이미지를 HTML 안에 내장했다. 명령 여섯 개/첫 메뉴 세 개, 배경 세 종류, 마우스·초점·숫자 강조를 비교한다. `review_capture.png`는 검토 HTML의 브라우저 캡처이며 실제 게임 사진이 아니다.

## 그림 목록

| 파일 (`png/`) | 크기 | 용도 |
|---|---|---|
| command_band_normal.png | 380×48 | 보통 상태 |
| command_band_hover.png | 380×48 | 밝은 마우스 올림 상태 |
| command_underline.png | 320×8 | 끝이 가늘어지는 황동색 붓 밑줄 |
| command_circle.png | 34×34 | 빈 금색 테두리 표식 |
| command_number_1.png ~ command_number_6.png | 각각 34×34 | 번호 포함 표식 |
| command_line.png | 3×360 | 위아래로 흐려지는 주인공 옆 세로선 |

모두 투명 RGBA PNG. 원안은 내장 imagegen으로 생성했고, PowerShell/System.Drawing으로 부품 영역을 최근접 추출했다. 띠의 가운데 252열은 같은 세로 화소 열을 반복해 늘이기에 적합하게 정리했다. 숫자는 3×5 도트 원형의 2배 크기로 직접 찍었으며 외부 글꼴에 의존하지 않는다. 생성 원안의 알파는 유지하고 세로선 양끝 알파만 추가 감쇠했다.

## 규격과 연결 제안

- 띠는 **수평 원본**이다. 이미지를 늘인 다음 부모를 Unity 기준 Z +6° 회전해야 오른쪽이 올라간다. 그림에 각도를 구워 넣지 않아 이중 회전과 9칸 경사 왜곡을 피한다.
- 띠 9칸 경계: 왼쪽 64 / 아래 8 / 오른쪽 64 / 위 8px. 너비 280~520px 확인용. 최소 너비 128px, 최소 높이 16px이며 권장 높이는 48px 고정이다. 붓 끝의 고정 폭을 보존하기 위해 처음 제안된 좁은 끝 여백 대신 64px을 사용했다.
- UI 가져오기 제안: Sprite / Single, Point, Clamp, mipmap 끔, 압축 없음, alphaIsTransparency 켬, Image Sliced, 색상 흰색. 기존의 어두운 색을 다시 곱하면 두 번 어두워지므로 연결 시 확인 필요. .meta와 Game/ 에셋은 만들지 않았다.
- 번호 표식은 띠 왼쪽 8px, 글자는 52px부터, 밑줄은 별도 레이어로 배치한 목업이다. 배치 수치와 그림의 색/질감은 사용자 확정 대상이다.
- 현재 코드의 높이 48px / 간격 58px / 위로 6° / 가운데 돌출 46px / 최대 확대 1.45배를 읽어 목업에 반영했다. 현재 실제 띠 폭은 340px이며 카드 요청에 따라 원본을 380px로 만들었다. 연결 때 340px로 9칸 늘이기를 적용할 수 있다.
- 기존 Art의 캐릭터 256×384 및 전투 효과 256px 칸은 월드 스프라이트 규격이다. UI에 그 칸을 강제하지 않고 이번 카드의 화면 화소 크기를 따른다. 기존 cannon_aim3.png.meta의 Point/Clamp/밉맵 없음/무압축/투명 처리도 읽었다. 신규 UI 전용 그림 규격은 이번 시안 제안이다.

## 참고와 원본

- 읽기 전용: `BattleHud.Commands.cs`, `BattleSim.Commands.cs`, `Game/Assets/_Project/Art/Characters/Hero/hero_back_idle.png`, `Art/Battle/Resources/BattleArt/cannon_aim3.png.meta`와 기존 그림.
- M-CL-078·079, D-064·066·072의 요구/색상/배치 설명을 참조했다. 사용자 참고 그림 원본 파일은 메시지에 첨부되지 않아 직접 대조하지 못했다.
- `source/imagegen_atlas.png`: 생성 원본 1254×1254. `source/generation.json`: 정확한 생성 프롬프트와 도구 기록.
- `source/export_assets.ps1`: 원안 → 11 PNG 재추출. Windows PowerShell용 UTF-8 BOM 포함.
- `source/build_review.py`: PNG를 내장 HTML과 manifest로 묶음. 이미지 편집 없음.
- `source/verify_review.cjs`: 설치된 Edge의 헤드리스 브라우저 검사. 환경에 맞게 Playwright 경로를 조정해야 할 수 있다.
- `manifest.json`: 각 PNG 크기와 SHA-256. `png_verification.json`: 실제 알파 최솟값/최댓값. `verification.json`: 실행한 브라우저 검사 결과.

## 확인 결과와 남은 것

11 PNG 크기/실제 알파 확인. 브라우저 검사 11개 통과: 6개/3개 구성, 숫자 선택, 마우스 강조, 밝은 배경, 선택 해제, 내장 그림 로드, 외부 그림 요청 없음, 기본 화면 넘침 없음, 스크립트 오류 없음. 검토 캡처를 육안으로 확인했다. 전용 Chromium이 없어 설치된 Edge로 검사했다.

Unity 실행·Play·컴파일·실제 게임 캡처는 하지 않았다. 6° 회전·1.45배 확대의 화소 흔들림, 실제 들판/안개 위 명암, 6항목 확대 때 가림, 9칸 경계의 Unity 보간과 투명 여백, 최종 질감·금색·번호 가독성은 사용자 검토 및 연결 후 확인이 남았다. 현재 HTML은 한 항목만 강조하는 시안으로, 게임의 거리별 연속 확대를 재현한 시험은 아니다.
