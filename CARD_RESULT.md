# K-029 결과 — 함정 지도 그림 1판

2026-10-16 / Codex / 사용자 확정 전 시안(D-064)

## 한 일

- 5m 한 칸 = 32×32화소 지도용 그림 제작: 풀 2종, 흙, 언덕 높이 느낌 3단계, 나무·바위 덩이, 성벽 네 면과 모서리.
- 흙길 연결 16종, 성 둘레 8×8칸 조립 조각, 반투명 격자 5종, 묻힌 함정·터진 자리 표시 2종 제공. 최종 PNG 에셋 94장.
- 새 지형 조합·격자/함정 표시 전환·클릭/방향키 칸 선택·확대·반복 비교가 있는 독립 HTML 제작.
- K-024·K-025와 Game/Art 규격을 읽어 색감·도트 방향을 참고. 이번 지도는 기존 전투용 32화소/m와 달리 **6.4화소/m**임을 명시.

## 바꾼 파일 목록

모두 신규 파일이며 상세 에셋 목록은 `Docs/References/codex_k029_v1/manifest.json`에 있다.

- `_HTML/codex_K-029_함정지도_v1.html`
- `Docs/References/codex_k029_v1/README.md`, `manifest.json`
- 같은 폴더 `tiles/*.png` 94장
- 같은 폴더 `source/terrain_source.png`, `castle_source.png`, `oak_top.png`, `pine_top.png`, `rock_a_top.png`, `rock_b_top.png`, `generation.json`, `review.template.html`
- 같은 폴더 `contact_sheet.png`, `map_preview.png`, `map_grid_review.png`
- 같은 폴더 `build.py`, `verify.py`, `verify_html.cjs`
- 같은 폴더 `qa/verification.json`, `html_model.json`, `game_before.sha256`, `game_after.sha256`
- 루트 `CARD_RESULT.md`

`CARD_CODEX_LOG.jsonl`은 작업 시작 시 이미 존재한 미추적 파일이다. 직접 수정하지 않았고 산출물에 포함하지 않는다. Game/·다른 카드·기존 HTML·협업 기록은 수정하지 않았다. 유니티를 열거나 실행하지 않았으며 Git 쓰기·커밋·가지 변경을 하지 않았다.

## 확인한 것

- 파일·알파·타일 경계·길 연결·성 조각 복원·HTML 내장 검사 564항목 통과.
- JS 구문·최소 DOM 모형 동작 10항목 통과. 실제 브라우저 검증과 구분한다.
- 생성 원안과 최종 PNG 비교판/지도 합성 직접 열어 확인.
- Game 파일 목록·SHA256 작업 전후 일치. 추적 파일 diff 없음. 현재 card/K-029 유지.
- 내장 image_gen 도구 사용. 원안과 정확한 프롬프트 2개는 source/에 보관. API/CLI 이미지 생성은 사용하지 않았다.

## 확실하지 않은 것·남은 것

- 브라우저 연결 목록이 비어 있어 실제 HTML 렌더링·반응형·실제 버튼/키 조작·file 주소 실행은 미검증.
- Unity/Play/실제 게임 캡처·밤 시인성·지도 축소 가독성·임포트·성벽 실제 치수 정합·노멀맵 미검증. Claude 연결과 사용자 확정이 남는다.
- 성 둘레는 원안에 옆면 표현이 조금 남아 있다. 전체 40m 견본을 칸으로 분할한 세트이며 성벽 길이 임의 증감용 반복 세트는 아니다.
- 언덕은 한 칸짜리 독립 둔덕 3단계다. 큰 연속 언덕의 등고선 연결 타일은 후속 작업이다. 경계 화소 일치를 자연스러운 반복 외형 승인으로 해석하지 않는다.
- 카드가 인용한 D-076 본문은 현재 카드 DECISIONS에 없었다. 카드 지시의 지도 위 격자 겉칠 요구를 기준으로 제작했다.

사용자 검토 질문: ① 한 칸짜리 독립 언덕 3단계로 충분한가, 여러 칸짜리 연속 언덕이 필요한가? ② 성벽 옆면 느낌을 더 줄여 엄격한 수직 도면으로 다듬을까? ③ 묻힌 함정/터진 자리 기호와 격자 투명도·색이 적합한가?
