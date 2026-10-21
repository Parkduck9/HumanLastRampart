# 설치 캐릭터 갤러리와 정적 재감사

2026-10-21. `build_review.py`는 Game 파일을 읽기만 하며 최신 원본과 설치된 색·노멀 35쌍을 검사하고 서버 없이 여는 HTML을 만든다. 아트를 재생성하거나 PNG를 재편집하지 않는다.

- 사용자용: `_HTML/codex_캐릭터전체적용_감사_v1.html`
- 같은 내용의 제작 사본: `preview.html`
- 감사 원본: `gallery_validation.json`
- 재현: 프로젝트 루트에서 `python Docs/References/codex_character_integration_v1/build_review.py`
- 모의 DOM 검사: `node Docs/References/codex_character_integration_v1/verify_review.cjs`

## 들어 있는 내용

Game의 실제 색 PNG 35장과 짝 노멀 PNG 35장, 사용자 선택 첫 `codex_hero_octopath_review_v6/generated.png`를 내장했다. 135프레임을 선택기로 확인하고 이전·다음·슬라이더로 한 칸씩 비교한다. 전체 시트도 원본 그대로 펼칠 수 있다. 갤러리 35개 썸네일과 정의/Resources 참조·해시검사 표를 포함한다. 외부 네트워크 요청·서버·의존 라이브러리가 없다.

원본색/설치색의 파일 SHA-256과 전체 RGBA 화소, 원본노멀/설치노멀의 SHA-256과 RGBA 화소, 색/노멀 크기와 알파 좌표, 메타 GUID 고유성, binding의 실제 GUID, YAML poseNormals의 색/노멀 짝을 검사했다. 35쌍 모두 통과했다. 점화병은 K-081의 구형 1728이 아니라 최신 v5 2592×384 및 그에 맞춘 expanded_ignite_normal을 쓴다.

## 사용 상태 구분

32개는 현재 정의 또는 Resources 실행 경로가 있다. 나머지 3개를 모두 실제 사용했다고 표시하지 않는다.

- `ogre_static.png`: 대체·보관용 정적 그림.
- `ladder_goblin_static.png`: 대체·보관용 정적 그림.
- `ladder_goblin_walk.png`: 정의의 sheet에는 남아 있지만 carrySheet가 MovingSheet에서 우선되어 현재 접근 동작은 운반 시트를 쓴다. 기존 걷기는 대체·보관용이다.

마법사 앞모습·걷기, 사다리 운반·측면, 대포병 밀기·당기기는 통합 담당이 별도 렌더 경로를 보완한 현재 코드 기준으로 표시한다. 코드의 경로가 있다는 정적 판정이며 그 자연스러움까지 이 자료가 증명하지는 않는다.

## 검사 범위와 한계

내장 PNG 71장을 디코딩해 정상 파일임을 확인했다. JavaScript 구문과 모의 DOM에서 35개 목록, 선택, 이전/다음 순환, 주인공 16번째 칸의 절단 좌표를 검사했다. `review_smoke_test.json`이 그 결과다. 실제 브라우저를 열어 렌더링한 검증은 하지 않았다.

갤러리는 게임 스크린샷이 아니다. Unity 실행·실제 전투 상태 전환·조명·접촉 검증은 통합 담당의 별도 시험과 캡처를 따른다. HTML의 시험 XML 표시는 생성 당시 완료 파일의 고정 스냅샷이며, 진행 중 후속 시험 결과를 자동으로 갱신하지 않는다. 최종 시험 후 위 제작 명령을 다시 실행하면 최신 완료 XML이 반영된다.

이 작업에서 Game 파일·기존 협업 문서·Git 상태는 수정하지 않았다.
