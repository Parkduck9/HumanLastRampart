# K-024 성벽 손상·전장 흔적 — 1판

작성: Codex / 2026-10-16 / **사용자 확정 전 시안(D-064)**. Game 연결·확정 API·파손 조건 구현은 포함하지 않는다.

## 검토 자료

- 독립 검토 페이지: [`../../../_HTML/codex_K-024_성벽손상_전장흔적_v1.html`](../../../_HTML/codex_K-024_성벽손상_전장흔적_v1.html)
- 정적 비교 그림: [contact_sheet.png](contact_sheet.png)
- 정확한 출력 크기·원안 자르기 영역·접점: [manifest.json](manifest.json)
- 생성 도구·전체 프롬프트: [generation.json](generation.json), 원안은 `sources/` 3장
- 후처리 원본: [build.ps1](build.ps1), HTML 원본: [review.template.html](review.template.html)

페이지에는 최종 그림 14장과 생성 원안 3장이 모두 내장되어 있다. 인터넷·서버 없이 HTML 파일을 직접 열 수 있게 구성했다. 1/2/4배 확대, 질감 2×2 반복, 소품 배경 전환을 제공한다. **실제 브라우저 조작은 아래 사유로 미검증**이다.

## 읽은 기준

FirstRead → RULES → DECISIONS → CONTRACTS 및 원본 폴더의 Claude outbox M-CL-078·079, 양측 tasks를 읽었다. 카드 폴더에는 live가 없으므로 `C:/Unity/1/HumanLastRampart/Docs/collab/live/`를 읽기만 했다. 원본 `.git/HEAD`는 develop이었다. 사용자의 이번 카드 지시를 우선하여 Git 쓰기·live 기록 갱신을 하지 않았다.

그림은 현재 카드 폴더의 `Game/Assets/_Project/Art/Battle/Resources/BattleArt/World/` PNG·README, 기존 질감 자료 `codex_world_materials_v1`, `BattleWorld3D.md`, `BattleWorldMaterials.cs`를 읽어 맞췄다. 기존 자료 README의 난간 옆면 크기 표기와 달리 **현재 Game PNG는 128×64**라 실제 파일 크기를 따랐다.

## 성벽·난간 3단계

| 표면 | 멀쩡 사본 | 새 파일 | 크기 | 32px/m 타일 범위 |
|---|---|---|---|---|
| 바깥면 | references/wall_stone_albedo.png | textures/wall_stone_damage1_albedo.png / wall_stone_damage2_albedo.png | 128×128 | 4×4m |
| 난간 옆면 | references/parapet_side_albedo.png | textures/parapet_side_damage1_albedo.png / parapet_side_damage2_albedo.png | 128×64 | 4×2m |
| 난간 윗면 | references/parapet_top_albedo.png | textures/parapet_top_damage1_albedo.png / parapet_top_damage2_albedo.png | 128×64 | 4×2m |

멀쩡 단계 3장은 기존 승인 PNG의 바이트 동일 사본이다. 이름은 `<표면>_damage1_albedo.png` / `<표면>_damage2_albedo.png`를 제안한다. `_albedo` 역할 접미사를 유지하고 표면 이름 뒤에 `_damage1`·`_damage2`를 붙인다. 현재 재질 로더는 기본 표면 이름만 읽으므로 **이 파일을 복사하는 것만으로 손상 전환이 동작하지 않는다.** 채택 이후 Claude가 연결 방식을 검토해야 한다.

금감은 좁은 균열·칩, 부서짐은 깊은 균열·속돌·패인 자국이다. 모든 질감은 불투명하며 실제 구멍이나 실루엣 붕괴가 아니다. 바깥 네 화소 띠를 기존 질감에서 그대로 복사했다. 기존 타일의 경계 특성을 보존하지만 생성 원안 안쪽의 줄눈 크기·위치는 멀쩡 그림과 완전히 같지 않으며, 경계 띠 안쪽에서 줄눈 변화가 보인다. 2×2 반복과 혼합 배치로 사용자 확인이 필요하다. 이번 시안은 모든 손상 타일의 첫·끝 행이 동일한 새 무이음 질감이라고 주장하지 않는다.

난간 윗면·옆면은 같은 생성 원안에서 추출한 형제 견본이다. 모서리 돌 위치·면별 UV 연결은 검증하지 않았다. 노멀맵은 만들지 않았다. 기존 멀쩡 노멀맵을 그대로 붙이면 패인 면과 조명이 어긋날 수 있어, 채택 후 손상별 노멀 제작 또는 노멀 없는 색 비교가 필요하다.

## D-068 크기 해석

S=0.35m, 난간 벽 0.30S=0.105m, 돌출부 높이 0.105m·폭 0.2625m·사이 0.21m, 사격구 폭 1.12m 기준은 그대로다. 난간 4×2m는 **그림 한 장의 반복 범위**다. 난간 모형의 실제 높이를 2m로 바꾸는 제안이 아니다. 32px/m에서 난간 벽 높이는 약 3.36화소이므로 세밀한 금이 보이지 않을 수 있다. 파손 단계마다 모형 크기나 UV 축척을 늘리지 않는다.

## 시체 더미 3단계

| 파일 | 캔버스 | 내용 영역 목표 | 32px/m 환산 제안 |
|---|---|---|---|
| sprites/corpse_pile_1.png | 64×64 | 48×15 | 폭 1.5m / 높이 약 0.469m |
| sprites/corpse_pile_2.png | 64×64 | 48×25 | 폭 1.5m / 높이 약 0.781m |
| sprites/corpse_pile_3.png | 64×64 | 48×40 | 폭 1.5m / 높이 1.25m |

세 장 모두 위에서 잰 지면 접점 **(32,56)**, 정규화 피벗은 Unity 아래 원점 기준 **(0.5,0.125)** 제안이다. 캔버스 전체는 32px/m일 때 2×2m이다. 내용 영역 크기는 원안의 불투명 영역을 추출한 뒤 맞춘 사각형이며, 실제 불투명 화소 분포는 일부 여백이 있을 수 있다.

고블린 계열의 녹색 피부·갈색 옷·철제 조각이 겹치는 비고어 견본이다. 오우거·아군 사망 그림으로 확정한 것이 아니다. 높은 더미는 S 약 3.57배라 성벽 앞을 크게 가릴 수 있다. 위 수치는 시각 비교를 위한 제안이고 사용자 확정값이 아니다. 등장 조건·누적 처치 수·충돌·사다리 역할·잔존 시간은 구현하지 않았다.

## 함정 실물 2상태

- `sprites/trap_1.png`: 덮인 흙·짧은 나무 표식·일부 드러난 금속판. 묻힌 상태임을 알아볼 최소 표시를 둔 견본.
- `sprites/trap_2.png`: 같은 계열의 흙이 벌어진 구덩이·파편·드러난 뿌리. 연기·불꽃 애니메이션 없음.
- 두 장 모두 64×64, 접점 (32,56), 내용 폭 56화소. 32px/m에서 캔버스 2m, 내용 폭 1.75m 제안. 내용 높이는 28/31화소지만 **기울여 본 땅 그림의 투영 높이**로, 구덩이 깊이 수치가 아니다.
- 모든 함정 종류를 대표하는 공용 표식으로 확정하지 않았다. 설치 칸 전체 폭·피해 범위와도 별개다. 바닥판 배치·빌보드 방식·지도 시점에서 보이는 형태는 Claude 연결 뒤 검토한다.

## 원안과 후처리

내장 image_gen 도구로 석재 손상 원안 1장, 시체 더미 시트 1장, 함정 시트 1장을 생성했다. 프롬프트의 요청 해상도와 실제 생성 크기는 다를 수 있어 실제 파일에서 잘랐다. 생성 당시 경로는 `$CODEX_HOME/generated_images/`였으며 선택한 세 PNG를 이 자료의 `sources/`로 복사했다. 원본을 덮어쓰지 않았다.

후처리는 Windows PowerShell의 System.Drawing으로 자르기·최근접 축소·여백·접점 정렬·기존 질감 경계 복사를 수행한다. 소품은 알파 128을 경계로 0/255로 정리한다. 원본 알파는 `sources/`에 보존했다. 최종 PNG와 비교판은 별도 파일이며 Game 그림을 덮어쓰지 않는다. `build.ps1`은 같은 폴더에서 실행하면 그림·manifest·HTML·비교판을 다시 출력한다. 재생성용 AI 프롬프트와 자르기 좌표는 각각 generation·manifest에 있다.

## 확인 및 미확인

- `verify.ps1`: PNG 크기, 질감 불투명도, 기존 경계 4화소 일치, 멀쩡 사본 SHA256, 소품 투명 여백·이진 알파·외곽 잘림·접점, 더미 높이 순서, HTML 리소스 내장 등 **59개 통과**. [qa.json](qa.json)
- `verify_html.cjs`: HTML 자바스크립트 구문, 내장 PNG 14장의 크기·실제 파일 바이트 일치, 원안 3장 포함 확인 통과. [html_qa.json](html_qa.json)
- AI가 생성 원안 3장과 최종 contact_sheet.png를 직접 열어 육안 비교했다. 최종 비교판은 브라우저 캡처가 아니다.
- 브라우저: iab를 열 수 없었고 사용 가능한 브라우저 목록이 비어 있었다. 따라서 확대·반복·배경 버튼의 실제 조작, 콘솔, 반응형 배치, file 주소 직접 실행은 **미검증**.
- Unity·Play·실제 캡처·노멀 조명·밤 시인성·3D 경계·대규모 시체 가림·실제 함정 배치는 **미검증**. Unity를 열거나 실행하지 않았다.
- `.meta`는 Game에 넣지 않은 문서용 시안이므로 생성하지 않았다. 채택 후 에셋 담당자가 Unity에서 생성한 에셋·meta를 함께 관리한다.

검토 뒤 확정할 질문은 루트 CARD_RESULT.md에 적었다. 코드 연결·피해 조건·소유권 변경은 이번 카드 범위에 없다.
