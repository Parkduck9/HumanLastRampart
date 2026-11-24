# K-020 지도 표식 1판

사용자 확정 전 시안(D-064). M-CL-078·079 및 카드 지시의 지도 표식 범위만 제작했다. Game/ 연결이나 가져오기 설정 변경은 하지 않았다.

검토: [`../../../_HTML/codex_K-020_map_markers_v1.html`](../../../_HTML/codex_K-020_map_markers_v1.html). 서버·인터넷 없이 파일을 직접 열 수 있게 그림·스타일·스크립트를 내장했다.

## 그림과 제작 방식

- `contact_sheet.png`: 표식 10종의 원래 크기와 확대 견본.
- `map_preview.png`: 위에서 본 직교 지도 배치 시안. 실제 게임 캡처가 아니다.
- `reference_map.png`: 사용자 PC `C:/Unity/1/HumanLastRampart/Game/playprobe/62_prep_map.png`의 읽기 전용 참고 사본. 기존 지도 캡처이며 새 표식이 적용되지 않았다.
- `png/`: 실제 투명 표식 PNG 10종. 알파 0/255만 사용하며 도트 가장자리에 흐림이 없다.
- `source/svg/`: PNG와 같은 정수 화소 격자를 보존한 SVG 10종. 각 화소가 사각형으로 되어 있어 편집할 수 있다.
- `source/build.ps1`: 도형·문양·숫자의 재생성 원본. 작은 UI 아이콘을 정수 화소로 직접 그렸다. 이미지 생성 모델·외부 에셋은 사용하지 않았다.
- `source/review_template.html`·`source/build_review.ps1`: HTML 및 견본·배치 PNG 재생성 원본.
- `manifest.json`: 파일별 크기·설명·기준점.
- `verification.json`·`review_verification.json`: 실제 수행한 PNG 검사와 HTML 정적 검사 결과.
- `source/verify_review.mjs`: HTML 내장 리소스·구문 검사 원본. 브라우저 검사가 아니다.

## 표식 규격

아래 기준점은 **좌상단 원점 / 오른쪽 +X / 아래 +Y**의 화소 좌표다. UI 표식은 화면 화소 기준이며 월드 32px/m 규칙을 적용하지 않는다.

| PNG 이름 | 크기 | 기준점 | 용도 |
|---|---|---|---|
| flag_archer.png | 32×32 | 7,29 | 청록 깃발·활과 화살·10명 숫자 |
| flag_gunner.png | 32×32 | 7,29 | 자주 깃발·총·10명 숫자 |
| move_east.png | 24×24 | 12,12 | 동쪽 이동 |
| move_south.png | 24×24 | 12,12 | 남쪽 이동 |
| move_west.png | 24×24 | 12,12 | 서쪽 이동 |
| move_north.png | 24×24 | 12,12 | 북쪽 이동 |
| compass_back.png | 64×64 | 32,32 | 금색 방위 글자 뒤 어두운 나침반 받침 |
| cannon_target_normal.png | 32×32 | 16,16 | 보통 조준점 |
| cannon_target_active.png | 32×32 | 16,16 | 선택 조준점, 금색·위아래 추가 막대 |
| group_selected.png | 40×40 | 20,20 | 선택한 깃발 둘레 모서리 테두리 |

궁수·총병은 색 외에 문양으로 구별한다. 회색 성벽·녹색 들판에서 분리되도록 짙은 외곽선을 뒀다. 금색은 기존 방위 글자와 공통 UI의 황동 계열을 따른다. 받침 중앙 약 20×20에 북·동·남·서 글자를 별도로 겹친다. 견본 HTML의 글자는 약 20화소다.

깃발은 기둥 아래 기준점으로 지도 위치에 붙일 수 있다. 선택 테두리는 깃발 이미지 사각형과 중앙 정렬하고, 이동 화살표는 깃발 오른쪽 4화소 간격으로 배치하는 안이다. 실제 이동 방향은 통로 경로를 따라 네 방향 중 고른다. 현재 HTML은 방향별 외형을 비교하는 기능이며 병력 이동 구현은 아니다.

대포 조준점은 화면상 위치 표시다. 피해 반경·사거리·명중 판정과 관계없다. 번호는 별도 글자이며 방향을 바꿔도 조준점 그림을 회전할 필요가 없다. 선택은 색과 형태 두 가지로 표시한다.

## 읽은 기존 자료

- `Game/Assets/_Project/Art/Battle/Resources/BattleArt/wall_a.png`: 1024×512.
- 같은 폴더 `cannon_aim3.png`: 1536×384. 대포 몸체 그림이며 UI 조준점 규격으로 사용하지 않았다.
- `World/wall_stone_albedo.png`: 128×128. 배치 시안에서 실제 석재를 참고하고 HTML에 내장했다.
- `World/README.md`: 월드 질감 32px/m와 점 필터 규칙 확인.
- `Characters/Static/archer_static.png.meta`: 점 필터·압축 없음·투명 알파·밉맵 끔 확인.
- `Combat/Battle/BattleHud.Map.cs`: 지도 명령·대포·준비 단계와 방위/묶음 표시 영역 읽기만 수행.
- M-CL-078·079: 원본 협업 폴더에서 읽기만 수행. 과거 커밋 지시보다 이번 카드의 Git 쓰기 금지 지시를 우선했다.

연결 설정 제안은 점 필터·압축 없음·sRGB 켬·투명 알파·밉맵 끔·가장자리 고정이다. 사용자 확정 후 Claude가 실제 화면 배율에 맞춰 결정한다. Unity의 정규화 피벗으로 옮긴다면 좌상단 좌표의 Y축을 뒤집어야 한다. 이 묶음은 Docs 자료이므로 Unity `.meta`를 생성하지 않았다.

## 재생성과 확인

저장소 루트 PowerShell에서 실행한다. Windows 기본 `System.Drawing`을 사용하며 외부 패키지를 설치하지 않는다. PowerShell 5.1의 한글 인식을 위해 PS1은 UTF-8 BOM으로 저장했다.

```powershell
& ./Docs/References/codex_k020_v1/source/build.ps1
& ./Docs/References/codex_k020_v1/source/build_review.ps1
& ./Docs/References/codex_k020_v1/source/build.ps1 -VerifyOnly
```

Node 환경에서는 `source/verify_review.mjs`의 `verify(자료폴더의 절대경로)`를 호출한다. PNG 10종의 규격·실제 불투명 화소 존재·투명 배경·반투명 화소 없음·캔버스 테두리와의 여백·SHA-256을 확인했다. HTML은 구문·PNG 내장 사본 일치·외부 의존 없음 등 13개 정적 검사를 통과했다. 견본과 배치 PNG를 직접 열어 외형과 설명 글자 겹침을 확인했다.

연결된 브라우저가 없어 HTML의 실제 렌더링·버튼·모바일 배치·콘솔 검사는 수행하지 못했다. Unity를 열거나 실행하지 않았다.

## 사용자 검토와 남은 항목

1. 청록 궁수 / 자주 총병과 문양이 원래 크기에서 충분히 구별되는가?
2. 방위 받침의 장식 밀도와 64화소 크기가 적절한가?
3. 대포 조준점의 보통·선택 차이가 충분한가?
4. 1~9명 잔여 묶음은 숫자를 동적 글자로 분리할지 결정이 필요하다. 현재 그림의 10은 완전한 10명 묶음 시안이며 잔여 병력을 10명으로 표시하자는 제안이 아니다.
5. 밀집한 표식의 간격·쌓기·클릭 영역과 전령 대기/이동 중 상태 구분은 실제 게임 화면에서 검토해야 한다. 클릭 영역은 시안 그림보다 넓게 확보하는 안을 권하지만 수치를 확정하지 않았다.
6. 실제 해상도·UI 배율·밤·안개·성벽 가림·적 및 함정과의 겹침은 미검증이다. 사용자 방향 확정 후 Claude 연결과 실제 캡처 비교가 남아 있다.
