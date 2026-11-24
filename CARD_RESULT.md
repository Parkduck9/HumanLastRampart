# 카드 K-063 결과

## 한 일

궁수·총병·대포병·마법사 각각 뒷모습 서기와 앞모습 서기, 총 8장을 제작했다. 주인공은 기존 6판을 비교 기준으로만 사용했다. 내장 이미지 생성 도구로 그림을 제작·보정하고 기존 시트의 실제 색상 추출, 최근접 규격 정리와 이진 알파 처리를 수행했다. 정확한 생성·수정 프롬프트는 생성기록.json에 보관했다.

최종 그림은 56×112, 인물 높이 96점, 발선 y=104이며 최근접 4배 PNG도 제공한다. 궁수의 활·등 화살통, 총병의 머스킷·회색 어깨 보호대, 대포병의 빈손, 마법사의 밤색 머리·보라 로브·푸른 보석 지팡이를 반영했다.

검토 페이지에는 주인공 6판과의 같은 배율·발선 1배/4배 비교, 기존 뒷모습 전후 비교, 마법사 앞모습 전후 비교, 화면 높이 13% 크기의 목업을 넣었다. 이미지·스타일·스크립트 내장으로 서버 없이 열린다. 규격과 팔레트 출처는 README.md·팔레트.json에 기록했다.

## 확인한 것

- 최종 8장의 56×112 규격, 인물 불투명 높이 96점, y=8~103 영역과 발선 y=104 확인.
- 최종 8장과 4배 파일의 알파 0/255, 최근접 4배 파일의 정확한 픽셀 반복 일치 확인.
- 최종 불투명 RGB가 병종별 기존 시트에서 추출한 실제 팔레트에 속함을 확인.
- 최종 확대 접촉판 직접 시각 확인. 마법사 보석의 파랑, 총병 금속의 회색, 대포병 빈손 확인.
- 팔레트 출처 원본 5개 SHA-256 유지, 주인공 6판 비교 사본과 기존 원본 바이트 일치 확인.
- HTML의 내장 PNG 42개 유효성과 크기, 외부 HTTP 의존 없음, 전환 스크립트 구문·배경 전환 로직·540/1080 화면 높이의 인물 13% 계산 확인. 이 항목은 정적 검사다.
- Git 읽기 전용 diff에서 기존 추적 파일 변경 없음. Game·협업 문서/기록·기존 HTML은 수정하지 않았다. Unity 실행과 Git 쓰기(스테이징·커밋·가지 변경)는 하지 않았다.

## 확실하지 않은 것·남은 것

- 브라우저 연결 도구의 사용 가능한 브라우저 목록이 비어 있어 실제 브라우저 렌더링 확인은 수행하지 못했다. 브라우저 이미지 로드·상호작용 검증 완료로 기록하지 않는다.
- 13% 크기 비교는 단순 배경 목업이다. 실제 Unity 카메라·배경·가림·조명·노멀 검증은 하지 않았다.
- 앞·뒤 독립 생성으로 머리 실루엣과 제복 주름에 차이가 남아 있다. 1점 외곽선과 재질별 3~4단 주 명암의 일정함, 작은 눈·버클·화살깃은 사용자 확대 검토 대상이다. 6판과의 채도 완전 일치를 수치로 검증하지 않았다.
- 마법사는 지팡이 포함 칸 안쪽 여백을 위해 가로 위치를 최대 2점 조정했다. 칸 가운데 기준은 x=28이며 실제 불투명 영역은 검사결과.json에 기록했다.
- 서기 8장만 제작했다. 동작 시트·노멀·Game 연결은 다음 단계다. 사용자 외형 확인 후 동작을 제작하고 Claude가 D-074와 기존 칸/피벗 규약을 확인해 연결해야 한다.

## 다음 사용자 검토 질문

네 병종의 뒷모습을 우선 확인한 뒤, 이 앞·뒤 서기 기준으로 다음 동작 시트 제작을 진행해도 되는가? 이번 카드에서는 후속 동작이나 Game 연결로 범위를 넓히지 않았다.

## 바꾼 파일 목록

전부 신규 파일이다. 카드 실행 환경이 자동 기록하는 CARD_CODEX_LOG.jsonl은 직접 수정하지 않았고 제작 목록에서 제외했다.
- `Docs/References/codex_k063_v1/archer_back_4x.png`
- `Docs/References/codex_k063_v1/archer_back_56x112.png`
- `Docs/References/codex_k063_v1/archer_back_generated.png`
- `Docs/References/codex_k063_v1/archer_front_4x.png`
- `Docs/References/codex_k063_v1/archer_front_56x112.png`
- `Docs/References/codex_k063_v1/archer_front_generated.png`
- `Docs/References/codex_k063_v1/archer_palette.png`
- `Docs/References/codex_k063_v1/archer_previous_back.png`
- `Docs/References/codex_k063_v1/build_review.py`
- `Docs/References/codex_k063_v1/cannon_crew_back_4x.png`
- `Docs/References/codex_k063_v1/cannon_crew_back_56x112.png`
- `Docs/References/codex_k063_v1/cannon_crew_back_generated.png`
- `Docs/References/codex_k063_v1/cannon_crew_front_4x.png`
- `Docs/References/codex_k063_v1/cannon_crew_front_56x112.png`
- `Docs/References/codex_k063_v1/cannon_crew_front_generated.png`
- `Docs/References/codex_k063_v1/cannon_crew_palette.png`
- `Docs/References/codex_k063_v1/cannon_crew_previous_back.png`
- `Docs/References/codex_k063_v1/gunner_back_4x.png`
- `Docs/References/codex_k063_v1/gunner_back_56x112.png`
- `Docs/References/codex_k063_v1/gunner_back_generated.png`
- `Docs/References/codex_k063_v1/gunner_front_4x.png`
- `Docs/References/codex_k063_v1/gunner_front_56x112.png`
- `Docs/References/codex_k063_v1/gunner_front_generated.png`
- `Docs/References/codex_k063_v1/gunner_palette.png`
- `Docs/References/codex_k063_v1/gunner_previous_back.png`
- `Docs/References/codex_k063_v1/hero_v6_reference.png`
- `Docs/References/codex_k063_v1/HTML_검사결과.json`
- `Docs/References/codex_k063_v1/README.md`
- `Docs/References/codex_k063_v1/verify_review.cjs`
- `Docs/References/codex_k063_v1/wizard_back_4x.png`
- `Docs/References/codex_k063_v1/wizard_back_56x112.png`
- `Docs/References/codex_k063_v1/wizard_back_generated.png`
- `Docs/References/codex_k063_v1/wizard_front_4x.png`
- `Docs/References/codex_k063_v1/wizard_front_56x112.png`
- `Docs/References/codex_k063_v1/wizard_front_generated.png`
- `Docs/References/codex_k063_v1/wizard_palette.png`
- `Docs/References/codex_k063_v1/wizard_previous_back.png`
- `Docs/References/codex_k063_v1/wizard_previous_front.png`
- `Docs/References/codex_k063_v1/검사결과.json`
- `Docs/References/codex_k063_v1/생성기록.json`
- `Docs/References/codex_k063_v1/전체_4배_비교.png`
- `Docs/References/codex_k063_v1/팔레트.json`
- `_HTML/4_그림_캐릭터/codex_K-063_아군6판방향_v1.html`
- `CARD_RESULT.md`
