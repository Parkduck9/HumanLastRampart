# K-026 UI 아이콘 시안 1판

상태: **사용자 확정 전 시안(D-064), Game 미연결**. 제작: Codex, 2026-10-16.

## 결과물

- `png/24/`, `png/32/`, `png/48/`: 크기별 투명 PNG 각 22장, 합계 66장.
- `svg/`: 24×24 도트 원본 SVG 22장. 한 화소 사각형으로 구성하며 편집 가능한 제작 원본은 `source/build.ps1`이다.
- `source/build.ps1`: 팔레트·형태 정의와 PNG/SVG/검토 그림/HTML 생성기. 외부 이미지·폰트·패키지 다운로드 없음.
- `source/review.template.html`: 검토 HTML 원본. 완성 페이지는 루트 `_HTML/codex_K-026_UI아이콘_v1.html`.
- `manifest.json`: 아이콘 ID, 한글 이름, 의미, 기존 명령 enum 대응, 파일 경로, 팔레트.
- `sheets/codex_k026_contact_v1.png`: 22종의 세 크기를 한 장에 모은 검토 그림.
- `source/verify.ps1`, `qa-static.txt`: 이미지·HTML 정적 검증 원본과 실제 수행 결과.
- `source/verify-ui.mjs`, `qa-ui.json`: DOM 모형에서 페이지 스크립트를 실행한 검사. 실제 브라우저 검사가 아니다.

## 포함 범위

| 구분 | 아이콘 |
|---|---|
| 병종 5종 | 궁수, 총병, 대포병, 마법사, 주인공 |
| 자원·전황 4종 | 골드, 병력, 성벽 체력, 처치 수 |
| 날씨 5종 | 맑음, 흐림, 비, 소나기, 안개 |
| 시간 2종 | 낮, 밤 |
| 우선순위 6종 | 체력 낮은 적부터, 체력 높은 적부터, 가까운 적부터, 오르는 적부터, 특수 적 먼저, 밀집한 곳 쏘기 |

석궁병은 추가하지 않았다. 명령 대응은 기존 `Core/Campaign/TargetingSettings.cs`의 `TargetPriority` 여섯 값과 일치한다. 새 게임 API를 제안하거나 연결한 것이 아니다.

## 읽은 기준과 제작 방식

- FirstRead → RULES → DECISIONS → CONTRACTS와 원본 폴더의 Claude outbox M-CL-078·079를 읽었다. 원본 브랜치는 `.git/HEAD`로 develop, 카드 브랜치는 card/K-026임을 읽기 확인했다. Git 쓰기는 하지 않았다.
- 기존 `Game/Assets/_Project/Art/Characters/Static/archer_static.png`, `wizard_static.png`를 육안으로 확인했다. 병종은 인물 축소 대신 활·장총·대포·수정 지팡이·장교 제복이라는 상징으로 단순화했다.
- Static PNG는 256×384이며, `wizard_static.png.meta`는 Point 필터, Mipmap 없음, 알파 투명, 무압축이다. 기존 인물 격자를 UI에 적용하지 않고 카드 지정 UI 크기 24/32/48px를 사용했다. Art 아래에 기존 UI 아이콘은 없었다.
- 공통 UI는 D-066의 바탕 `#101613`, 창 `#26352d`, 황동 `#d5b16b`, 글 `#eee4cf`, 위험 `#c37e70`을 따른다. 추가 보조색은 그림자 `#786344`, 물·수정 `#88b9c7`이며 시안 팔레트다. 녹색 `#91ad8b`은 예약색으로 이번 그림에는 쓰지 않았다.
- 단순 UI 아이콘이므로 코드로 직접 그린 도트를 사용했다. AI 이미지 생성 모델이나 외부 아이콘 묶음은 사용하지 않았다. 생성 프롬프트는 없고 제작 정의는 `source/build.ps1`에 전부 있다.

## 규격과 재생성

모든 개별 PNG는 RGBA, 완전 투명 또는 완전 불투명 화소만 사용한다. 기준 도트는 24×24, 48px는 정확한 2배, 32px는 중앙 좌표 기준 최근접 변환이다. 32px에는 도트 폭 1~2px 차이가 생기므로 그 크기도 별도 검토한다. 최외곽은 투명하며, 아이콘과 버튼 바탕은 분리했다. 노멀맵은 UI에 만들지 않았다.

Windows PowerShell과 기본 System.Drawing으로 재생성한다. 저장소 루트에서:

```powershell
& ./Docs/References/codex_k026_v1/source/build.ps1
& ./Docs/References/codex_k026_v1/source/verify.ps1
```

`source/verify-ui.mjs`는 Node.js에서 실행한다. 재생성은 이 카드의 산출물만 갱신하며 Game 파일을 쓰지 않는다. SVG는 도트 좌표 원본이며, 32px 배포 그림과 정확히 같은 외곽이 필요하면 제공 PNG를 사용한다. PNG와 SVG는 그림이므로 `.meta`는 게임 연결 시 Claude가 에셋과 함께 생성·커밋할 대상이다. 이번 Docs 시안에는 Unity 가져오기 파일을 만들지 않았다.

## 확인과 남은 검토

전체 검토 PNG를 육안으로 확인했다. 정적 검증 결과는 `qa-static.txt`, DOM 모형 검증 결과는 `qa-ui.json`에 남긴다. 브라우저 도구에서 사용 가능한 앱·브라우저가 없었으므로 실제 HTML 렌더링·다운로드 동작·창 폭별 넘침은 미검증이다. Unity 실행·Play·실제 게임 캡처는 수행하지 않았다.

사용자 확인 사항: 24px의 여섯 명령 구분, 장교 제복이 주인공을 나타내기에 적절한지, 뿔 달린 적이 특수 적을 나타내기에 적절한지, 보조 청색과 비·소나기 표현의 적합성. 색만으로 뜻을 전달하지 않도록 명령 이름을 함께 쓰는 안이다. 실제 버튼 선택·경고 상태는 연결 담당이 확정 후 적용한다.

게임 연결 단계에서는 Point, Mipmap 끔, 알파 투명, 무압축을 후보로 삼고 Canvas 배율과 아이콘 중앙 정렬을 실제 화면에서 확인한다. Sprite 가져오기 형태와 폴더 위치는 이번 시안에서 확정하지 않는다. 사용자 방향 확정 → Claude 연결 → 실제 캡처 비교 절차가 남아 있다.
