# K-028 작업 결과

작성: Codex / 2026-10-16. **사용자 확정 전 시안 완료, 게임 미연결.**

## 한 일

- 성벽 안 한 줄 길의 낮·저녁 배경 2장(3840×1080), 수리 자리·병영·함정 공방·성벽 계단 투명 그림 4장 제작.
- 장소 그림 1536×1024, 바닥 가운데 기준점 (768,928), 주인공 대비 목표 높이 2 / 2.2 / 1.7 / 2.5배로 정규화.
- E 말풍선·장소 이름 띠 4개·승리/패배 장식 띠 제작.
- 성벽 네 면 × 손상 3단계의 독립 SVG 12개, 성 도식 3개, 정면 흉상 5개, 한 장 결과 합성 PNG 2장 제작.
- 기존 캐릭터 시트의 비율·복장·머리색과 K-027 배경, D-066 공통 색을 참고. 생성 원본 11장·전체 프롬프트·편집 원본·재출력 도구·README 보관.
- 모든 그림을 내장한 HTML 검토 페이지 작성. 낮/저녁·A/D 이동·E 장소 확인·승패 띠 비교는 배치 시연이며 게임 기능은 미연결.

## 바꾼 파일 목록

- `_HTML/codex_K-028_정비마당_결과그림_v1.html`
- `Docs/References/codex_k028_v1/README.md`
- 같은 폴더 `backgrounds/`: 낮·저녁 PNG 2개
- `stations/`: 장소 PNG 4개
- `originals/`: imagegen 생성 원본 PNG 11개
- `ui/`: E 말풍선·이름 띠·승패 띠 SVG 7개
- `result/`: 성벽 SVG 12개·성 도식 SVG 3개·흉상 PNG 5개·결과 합성 PNG 2개
- `source/`: `codex_build.py`, `codex_export.ps1`, `codex_composite.ps1`, `codex_verify.py`, `codex_dom_check.js`, `codex_prompts.md`, `codex_portrait_prompts.md`, `codex_generated_paths.json`, `codex_bounds.json`, `codex_review.template.html`
- `qa/`: 낮/저녁 합성 PNG 2개, `codex_validation.json`, `codex_page_script.js`. 기존 `codex_game_before.json`은 읽기만 함.
- 루트 `CARD_RESULT.md` (이 보고서)

## 확인한 것

- 생성 원본 11장과 정비 마당·결과 합성 그림을 직접 열어 확인.
- 파일 규격·투명도·SVG 수·HTML 내장 자료·Game 해시 검사 20항목 통과.
- JavaScript 구문 검사, 모의 DOM 조작 8항목 통과. 실제 브라우저 검사는 아님.
- 보관된 작업 전 목록과 Game 594개 파일의 SHA-256 및 파일 목록 동일.
- Unity 실행 없음, Git 쓰기 없음, Game·다른 카드·협업 문서 수정 없음.

## 확실하지 않은 것·남은 것

- 실제 브라우저가 연결되지 않아 HTML 렌더·한글 글꼴·실제 키 입력·좁은 화면 배치 미확인. 제공한 합성 PNG는 HTML 캡처가 아님.
- 생성형 그림의 도트 밀도·얼굴 동일성·반투명 가장자리와 최종 카메라 가림은 사용자 검토 필요. 배경 좌우 무봉제 반복은 보증하지 않음.
- 사용자 질문: 네 장소 비율·밝기와 결과 도식·흉상 방향이 적절한가? 실제 한 화면 노출 범위와 장소 간 간격은 얼마로 할 것인가?
- 손상 단계 체력 경계, 영웅 부상/사망 표시 규칙은 미정. 기존 D-014의 패배 결과 건너뛰기와 새 결과 방향의 관계도 후속 확정 필요. 패배 그림은 비교용.
- 사용자 아트 확정 후 Claude의 별도 게임 연결, 사용자 Play·실제 캡처 비교가 남음.
