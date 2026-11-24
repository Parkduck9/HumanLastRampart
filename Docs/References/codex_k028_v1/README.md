# K-028 정비 마당·결과 화면 그림 1판

작성: Codex / 2026-10-16. **D-064 사용자 확정 전 시안 / 게임 미연결.** 카드 지시의 D-076 예정 방향을 그림으로 검토한다. Game·협업 기록·다른 카드 자료는 수정하지 않았다.

검토 페이지: [정비 마당·결과 그림](../../../_HTML/codex_K-028_정비마당_결과그림_v1.html). 모든 자료를 내장하여 서버 없이 직접 열린다. 낮·저녁 전환, A/D 이동·E 상호작용의 배치 확인, 장소 버튼, 승리·패배 띠 비교를 제공한다. 실제 모집·수리·함정 구매·전투 전환은 실행하지 않는다.

## 산출물

| 경로 | 내용·규격 |
|---|---|
| `backgrounds/codex_yard_day_3840x1080.png` | 낮, 안쪽 성벽·흙과 돌길, 3840×1080 불투명 PNG |
| `backgrounds/codex_yard_evening_3840x1080.png` | 같은 배경의 저녁 조명 시안, 3840×1080 |
| `stations/codex_repair.png` | 성벽 수리 비계·돌 더미·석공 도구 |
| `stations/codex_barracks.png` | 병영 입구·올리브 깃발·빈 모집 게시판 |
| `stations/codex_workshop.png` | 함정 진열·작업대·화로 |
| `stations/codex_stairs.png` | 위로 오르는 계단·아치 문 |
| `ui/codex_interact_e.svg` | E 키가 들어간 말풍선, 160×100 투명 SVG |
| `ui/codex_name_*.svg` | 장소 이름 띠 4개, 320×60 |
| `ui/codex_ribbon_victory.svg`, `codex_ribbon_defeat.svg` | 승리·패배 장식 띠, 800×144 |
| `result/codex_wall_{north,east,south,west}_{intact,cracked,broken}.svg` | 네 면 × 멀쩡·금감·부서짐, 12개 독립 투명 조각, 각 320×110 |
| `result/codex_castle_{intact,example,defeat}.svg` | 위에서 본 성 도식 720×610. example은 북·서 금감, defeat는 서 붕괴 |
| `result/codex_portrait_{archer,gunner,cannon,wizard,hero}.png` | 궁수·총병·대포병·마법사·주인공 정면 흉상, 각 512×512 투명 PNG |
| `result/codex_result_{victory,defeat}_1920x1080.png` | 성벽·손실·영웅 상태를 묶은 한 장 결과 그림, 각 1920×1080 |
| `qa/codex_yard_{day,evening}_composite.png` | 주인공·네 장소를 얹은 3840×1080 배치 확인 그림 |
| `originals/codex_*_original.png` | 내장 imagegen 생성 원본 11장. PSD·3D 장면이 아닌 생성 PNG 원본 |
| `source/` | 생성 프롬프트, 정규화·합성 스크립트, 편집 도식 생성기, HTML 템플릿, 검사 도구 |

## 크기와 기준점

장소 PNG는 공통 **1536×1024**, 왼쪽 위 기준 **(768, 928)**을 바닥 가운데 기준점으로 삼는다. 생성 원본에서 알파 128 초과 영역의 중심과 하단을 기준으로 옮기고 비율을 유지해 확대·축소했다. 빈 여백과 옅은 그림자까지 같은 높이로 취급하지 않는다. 장소 실루엣 목표 높이는 수리 640 / 병영 704 / 공방 544 / 계단 800. 주인공 키를 약 320으로 놓으면 **2 / 2.2 / 1.7 / 2.5배**다. 실제 시트의 머리·발 여백으로 약간 달라 보일 수 있다.

기존 시트는 `Game/Assets/_Project/Art/Characters`와 `Docs/GDD/screens/CharacterArt.md`를 읽기만 했다. 캐릭터 칸 256×384, 발 (128,352), 주인공 4열×4행 1024×1536, 약 2.2등신 비율을 확인했다. 검토 페이지의 전신은 기존 주인공 시트 정면 행을 내장한 크기 비교이며 새 동작이 아니다. 결과 초상은 걷기 시트가 아니므로 512×512 흉상으로 별도 제작했다. 노멀맵·새 캐릭터 시트는 만들지 않았다.

흉상은 머리·가슴 높이 440, 중심 x=256, 하단 y=480으로 정규화했다. 궁수는 갈색 머리·크림 목도리·활, 총병은 진한 갈색 머리·금속 어깨·총, 대포병은 회흑색 머리·작업 벨트·장전 솔, 마법사는 밤색 머리·보라 옷·푸른 수정, 주인공은 은백발·검은 금장 제복으로 구분했다. 생성 시안이므로 기존 캐릭터와 얼굴의 완전한 동일성은 보증하지 않는다.

성벽 12조각은 회전 전 같은 규격이며 면별로 독립 교체한다. 북은 그대로, 동은 시계방향 90도, 남 180도, 서 270도로 배치하는 도식 시안이다. 게임 좌표·확정 API가 아니다. 금감은 짙은 균열, 부서짐은 중앙 결손과 돌 조각으로 색과 함께 구분한다. 체력 비율에 따른 단계 경계는 정하지 않았다.

## 분위기와 원본

K-027 `codex_prep_1920x1080.png`의 회색 석재·올리브 천·따뜻한 조명에 맞췄다. D-066 공통 UI 색 `#101613`·`#26352d`·황동 `#d5b16b`·크림 `#eee4cf`·위험 `#c37e70`을 쓴다. 한글은 그림에 생성하지 않고 SVG·페이지·합성 단계에서 넣어 편집 가능하게 분리했다. 글꼴 파일은 배포하지 않으며 시스템 맑은 고딕·바탕을 사용한다.

생성에는 **내장 imagegen**을 사용했다. 전체 프롬프트는 `source/codex_prompts.md`, `source/codex_portrait_prompts.md`에 보관한다. 저녁은 낮 생성 원본을 수정 대상으로 하여 구조·구도를 유지하고 빛·하늘만 바꾸도록 요청했다. 반환 원본은 1983×793 등 도구 크기를 그대로 보존하고 배경은 위 18%부터 32:9 비율로 잘라 3840×1080으로 확대했다. 도트 격자·색 수가 고정된 생산용 픽셀 아트가 아니라 분위기 검토용이다.

`codex_generated_paths.json`의 외부 경로는 최초 생성 출처 기록이다. 재출력은 `originals/`의 저장본을 우선 사용하므로 외부 생성 폴더 없이 가능하다. `codex_bounds.json`은 원본의 알파 기준 범위다. 그림자·등불의 반투명 가장자리를 보존했으며 실제 게임에서의 가장자리 처리는 미검증이다.

## 재출력

저장소 루트에서 Python(Pillow)와 PowerShell/System.Drawing을 사용한다. 생성 모델을 다시 호출하지 않는다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Docs/References/codex_k028_v1/source/codex_export.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Docs/References/codex_k028_v1/source/codex_composite.ps1
python Docs/References/codex_k028_v1/source/codex_build.py
python Docs/References/codex_k028_v1/source/codex_verify.py
node Docs/References/codex_k028_v1/source/codex_dom_check.js
```

합성·HTML 재출력은 이 작업 폴더의 기존 주인공 시트를 읽는다. 쓰기는 이 카드 산출물 안에서만 한다. SVG·템플릿은 생성기에서 다시 출력되므로 편집 후 재출력할 때는 `codex_build.py` 안의 원본 내용도 함께 바꿔야 한다.

## 확인 범위와 남은 결정

- 생성 원본 11장과 대표 합성 그림을 직접 열어 구도·장소 종류·초상 복장과 머리색을 확인했다.
- PNG 규격·알파, 성벽 12조각, UI 7종, 생성 원본 수, HTML 외부 참조·내장 데이터·미치환 항목, 결과 PNG 규격 및 Game 해시 대조 **20항목 통과**. 상세: `qa/codex_validation.json`.
- JavaScript 구문 검사 및 **모의 DOM 8항목 통과**: 배경 전환, 장소 네 개, 계단 상호작용, 승패 띠, 확인 예시, A 이동. 실제 브라우저 렌더 검사와 다르다.
- 기존 `qa/codex_game_before.json`의 594개 파일과 현재 Game의 전체 파일 목록·SHA-256이 동일하다. Unity는 열거나 실행하지 않았다. Git 쓰기도 하지 않았다.
- 사용 가능한 브라우저 연결이 없어 실제 HTML 표시·한글 글꼴·키보드 입력·좁은 화면 레이아웃은 미확인이다. 합성 PNG는 별도 출력 그림이며 HTML 캡처가 아니다.
- **사용자 검토 필요:** 마당의 깊이감·밝기·네 장소 비율, 카메라가 한 번에 보여 줄 범위, 초상의 도트 밀도·동일성, 손상 도식과 영웅 상태의 전달력.
- **미정:** 배경 좌우 무봉제 반복(현재 보증하지 않음), 장소 간 최종 거리·상호작용 범위, 체력별 손상 단계 경계, 영웅 부상·사망 표시 규칙, 패배 결과 진입 여부. 기존 D-014는 패배 결과를 건너뛰므로 이 카드의 패배 그림은 비교용이며 흐름 변경 승인으로 해석하지 않는다.
- 확정 후 Claude가 별도 카드에서 게임 연결·가져오기 설정을 다루고, 사용자가 Play와 실제 캡처로 가림·도트 밀도·가독성을 확인한다.
