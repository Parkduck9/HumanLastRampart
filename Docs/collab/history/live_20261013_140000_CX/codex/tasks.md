# Codex 작업 기록 (tasks)

- 작성자: Codex만. MD 기록은 맨 아래에 추가하고 상태 변경도 새 줄로 기록한다.
- Git 요청·시작·종료 원본 기록이며 실제 잠금이 아니다. 조정 담당의 허가는 Claude tasks.md에서 확인한다.
- 이 파일 변경 시 status.js도 갱신한다.

---

## T-CX-001 15장 6단계 Codex 초기 준비
- 상태: 진행중
- 화면/영역: 협업 기반 / 저장소 루트, Docs/collab/live/codex
- 승인 근거: 사용자 협업 확인 지시(2026-10-13), D-001, M-CL-002
- 수정 범위: AGENTS.md, Docs/collab/live/codex/tasks.md, outbox.md, questions.md, status.js
- 의존: Claude 3~5단계 종료 확인, G-CX-001 허가 후 커밋
- 완료 기준: Codex 초기 파일 5개 작성, 표시 데이터 형식 및 Git 무시 확인, Claude 허가 후 AGENTS.md만 develop에 커밋
- 기준 커밋: d69c53b8776b300b4edacdef0b9025092e9b2dfb / 결과 커밋: 없음(허가 대기)
- 시작: 2026-10-13 12:26 KST
- 작업 폴더: C:\Unity\1\HumanLastRampart (원본)
- 확인 결과 [AI 직접 확인]: 현재 develop, HEAD·main·develop 모두 기준 커밋과 일치. 착수 전 git status 변경 없음. Claude G-CL-001~003 종료 확인, 미종료 허가·예약·대체 인계 없음. M-CL-002 확인.
- 문서 상태 참고: RULES 상단은 초안 표기가 남아 있으나 사용자 지시와 D-001의 최종 확정 기록을 근거로 확정 v0.3을 적용. Claude에게 표기 정리 요청 예정.
- 변경 파일: AGENTS.md, Docs/collab/live/codex/tasks.md, Docs/collab/live/codex/outbox.md, Docs/collab/live/codex/questions.md, Docs/collab/live/codex/status.js
- 사용자 확인 절차: live/status.html을 열고 F5 → Codex T-CX-001과 G-CX-001 요청 표시 확인. Unity Play 대상 없음.
- 확인 결과 [사용자 보고]: 아직 없음.
- 이미지: 없음(운영 문서 생성, 화면 변경 없음)
- 진행: 2026-10-13 12:26 KST 초기 파일 작성. 커밋 허가 대기.
- Git 요청 G-CX-001: 2026-10-13 12:26 KST AGENTS.md만 파일 단위로 지정해 develop에 커밋. 예정 메시지: [T-CX-001] Codex 협업 진입점 작성. live/codex/는 제외. Claude tasks.md의 허가 확인 전 Git 쓰기 실행하지 않음.
- 남은 문제: 파일 검증 결과 추가 및 Claude G-CX-001 허가 확인 후 커밋. 실제 현황판 열기 검증은 아직 수행하지 않음.
- 확인 결과 [AI 직접 확인]: 2026-10-13 12:28 KST Git 무시 규칙으로 live/codex/의 4개 파일이 모두 제외되고 추적되지 않음을 확인. git status에는 AGENTS.md만 미추적 표시. Node로 status.js 구문, 필수 필드, T-CX-001/G-CX-001 값 및 Claude 데이터와의 공존을 검증해 성공. Claude tasks.md에 G-CX-001 허가 없음. 실제 브라우저 표시·더블클릭 검증은 미수행.
- 상태 변경: 2026-10-13 12:28 KST 진행중 → 구현 완료 (초기 파일 작성·검증 완료, AGENTS.md 커밋은 G-CX-001 허가 대기).
- 협업 확인: 2026-10-13 12:33 KST 새 rules/ 문서와 D-010, M-CL-003·004 및 Claude의 12:29 G-CX-001 허가 확인. AGENTS.md 읽기 경로 3개 수정, 원본 develop 확인. Claude 이동·수정 파일은 커밋에서 제외.
- Git 시작 G-CX-001: 2026-10-13 12:33 KST 기준 d69c53b8776b300b4edacdef0b9025092e9b2dfb. AGENTS.md만 add 및 commit --only로 지정.
- Git 종료 G-CX-001: 2026-10-13 12:34 KST 성공. 결과 커밋 55a8b819ef2e439fb5cf440c484e5b2e610ff103. 커밋 파일은 AGENTS.md 하나. 현재 develop, main 그대로, 다른 파일의 작업 상태 및 index 항목 전후 동일 검증.
- 상태 변경: 2026-10-13 12:34 KST 구현 완료 → 통합 대기 (15장 6단계 develop 커밋 완료, main 반영 전).

## T-CX-002 주인공 레퍼런스 보관 및 SpriteFlow 미리보기
- 상태: 진행중
- 시작: 2026-10-13 12:41 KST
- 승인 근거: 사용자 요청 "SpriteFlow에 넣어서 HTML 미리보기 생성, 레퍼런스 파일 보관" 및 최근 눈썹 수정 이미지 선택.
- 수정 범위: Docs/collab/live/codex/hero-reference-v1.html, 자기 MD·status.js, Docs/collab/live/review/codex_T-CX-002_hero_reference_v1.png
- 기준 커밋: 69a0af67edf72789832f12fd95c5881839015d71 / 결과 커밋: 없음 (이번 요청은 미리보기·레퍼런스 준비)
- 완료 기준: 선택한 PNG 원본 복사, 단독 HTML 미리보기 생성·표시 확인, SpriteFlow 결과가 나오면 생성 결과 확인 및 미리보기 반영.
- 진행: Computer Use로 SpriteFlow 방향 생성기에 선택 이미지와 4방향·기본 카메라·도트 유지 설명 입력. 생성 시 로그인 창이 표시되어 로그인 대기. 생성 결과는 아직 없음.
- 확인 결과 [AI 직접 확인]: 원본 PNG 1086×1448, RGBA, 알파 범위 0~255. 실제 투명 채널 있음. 이 이미지는 정면 외형 레퍼런스이며 64×64 원본 도트 시트나 걷기 애니메이션은 아님.
- 사용자 확인 절차: HTML에서 배경·표시 높이를 바꿔 확인. SpriteFlow 열린 탭에서 로그인 후 알려주면 생성 진행.
- 남은 문제: SpriteFlow 로그인 필요. Unity 최종 도트 규격·방향·동작 확정 및 Game 반영은 별도 작업.

- 진행: 사용자 요청의 영구 보관용 단독 레퍼런스를 Docs/References/codex_hero_reference_v1.html에 작성(Codex 소유, 이미지 내장). live/codex/ HTML은 작업용 미리보기 사본이다. 사용자 요청 범위에서 작성했으며 Git 커밋은 하지 않음.
- 확인 결과 [AI 직접 확인]: 원본과 보관 PNG의 SHA256 일치. HTML의 이미지 표시·배경 변경·320px 표시 높이 변경·PNG 불러오기(원본 1×1 테스트) 성공. SpriteFlow 결과 시트는 아직 없음.
- 상태 변경: 2026-10-13 12:46 KST 진행중 → 보류 (레퍼런스·HTML 준비 완료, SpriteFlow 생성은 로그인 대기).

- 사용자 보고: 2026-10-13 12:49 KST "로그인했어 만들어줄래". SpriteFlow 생성 계속 진행 요청.
- 재확인 [AI 직접 확인]: 연결된 Codex 인앱 브라우저의 SpriteFlow 탭을 새로고침한 뒤에도 Log in 표시. 선택 이미지·4방향·입력 설명은 유지됨. 접근 가능한 브라우저 목록에는 Codex 인앱 브라우저만 있으며 다른 브라우저의 로그인 세션은 확인할 수 없음. 연결된 탭 로그인 또는 로그인한 브라우저 이름을 사용자에게 요청함.
- 진행: 생성 결과 없음. T-CX-002의 보류 상태 유지. 생성 완료나 크레딧 사용으로 기록하지 않음.

- 사용자 답변: 로그인한 브라우저는 Chrome. 연결 요청으로 확인했으나 현재 Chrome 브라우저는 사용 불가. Chrome 로그인 정보나 쿠키를 추출하지 않고, 인앱 로그인 또는 사용자 생성 결과 전달 방식으로 진행 가능함. 생성 결과 없음.

- 재개: 2026-10-13 13:18 KST 사용자 인앱 로그인 보고 후 현재 SpriteFlow 탭 로그인 확인. 선택 PNG 재등록 및 4방향 설정 유지.
- 진행 [AI 직접 확인]: 초기 생성 요청은 크레딧 부족 안내로 중단. 무료 Daily check-in의 Claim Reward(+5) 진행 후 재시도했으며 Generating / Initializing 10% 표시 확인. 유료 결제·구독 구매 없음.
- 상태 변경: 2026-10-13 13:18 KST 보류 → 진행중 (SpriteFlow 생성 시작, 결과 대기).

- 결과: 2026-10-13 13:22 KST SpriteFlow 정적 4방향 생성 완료. 표시 이름 four-way-idle-1318. 사이트 제공 512×512 썸네일 확인(정면·양측면·후면). 무료 일일 보상 5크레딧 사용, 유료 결제 없음.
- 제한 [AI 직접 확인]: Sprite Sheet 다운로드 버튼 클릭 시 Unlock downloads 안내(구독 또는 Credits 구매 필요). 다운로드 파일은 없음. 다운로드 제한을 우회하지 않고 공식 미리보기 썸네일만 HTML에 연결.
- 변경 파일: Docs/References/codex_hero_reference_v1.html 및 live/codex/hero-reference-v1.html의 진행 상태·생성 결과 미리보기 추가. 원본 레퍼런스 PNG 내장은 유지. 온라인 결과 썸네일은 인터넷 필요, 원본 시트가 아니며 걷기 애니메이션도 아님.
- 확인 결과 [AI 직접 확인]: HTML의 생성 결과 이미지 정상 로드(512×512), 생성명·다운로드 제한 표시 확인. Game/ 에셋 반영·Unity Play·Git 쓰기는 없음.
- 상태 변경: 2026-10-13 13:22 KST 진행중 → 사용자 검토 대기 (생성·미리보기 완료, 원본 다운로드는 유료 제한으로 대기).

### 협업 확인 | 2026-10-13 12:51 KST
- 확인: FirstRead → RULES 확정 v0.3(D-011·7.3 포함) → DECISIONS → CONTRACTS, M-CL-005·006·007, 조정 담당 tasks와 자기 작업 기록 확인.
- 확인 결과 [AI 직접 확인]: 원본 develop=7043218b6e66c99870ef2afa00a81242efed56ba, main=69a0af67edf72789832f12fd95c5881839015d71. T-CX-001의 55a8b81은 R-001에 포함돼 main 반영됨. G-CL-004~007 종료 확인, 미종료 Git 허가·공용 예약 없음. 단일 Claude 조정 세션 결정 확인.
- Unity 확인 [AI 직접 확인]: Game/ 바로 아래 Assets·Packages·ProjectSettings 존재, ProjectVersion=6000.3.25f1. 기본 파일은 cf004c2로 커밋됨. Unity Play는 실행하지 않음.
- ROADMAP 갱신: P0-X01=완료, P0-X02=완료. P1-X01~X08=대기 (사용자 화면 설계 시작 지시 및 해당 의존 약속 대기). status.js roadmap 반영.
- 대기: R-002 main 반영 사용자 승인, ARCHITECTURE 제안 v0.1의 Q-A1~A7 검토. CONTRACTS는 아직 빈 틀이므로 구현 근거 없음.
- T-CX-002 상태: 보류 유지. 기존 기록상 연결된 SpriteFlow 로그인 확인 대기. 이번 확인에서는 브라우저 재확인하지 않음. 레퍼런스 HTML은 미추적 상태로 보존하며 새 Git 요청·커밋하지 않음.
- 특이사항: ROADMAP의 P0-C10은 미체크이나 Claude tasks에는 R-002 승인 요청 기록이 존재. Claude에게 진행표 동기화 요청.
- 범위: 협업 기록과 표시 요약만 갱신. 브랜치 전환, Game 수정, Git 쓰기 없음.

## T-CX-003 CONTRACTS C-001~C-003 검토 (P1-X07 일부)
- 상태: 구현 완료
- 시작·검토 완료: 2026-10-13 13:26 KST
- 승인 근거: 사용자 '검토요청', M-CL-009. 아키텍처 승인 D-013 및 M-CL-008 확인. 승인 대신 기술 검토만 수행.
- 수정 범위: 자기 tasks.md, outbox.md, status.js (검토 기록). Claude 문서·Game/ 수정 없음.
- 기준 커밋: edbc4edd4cb67a87fe4b722e2a262bf9a2eddbbc
- 검토 대상: ARCHITECTURE 승인 v1.0, CONTRACTS 제안 C-001~003 및 C1~C4. API는 승인 전이며 구현 미착수.
- 결과: 모듈별 소유권과 App 중앙 전환 원칙 수용. C3의 Frontend·Story·Meta → UI 허용 동의(최종 사용자 승인 필요). ScreenOutcome 네 값은 현재 최소 흐름에 충분하며 설정 팝업은 화면 완료 신호가 필요 없음. 아래 세 보완 요청을 M-CX-007로 전달.
- 보완 1: C-003 Complete의 중복 호출·이전 화면 지연 콜백·현재 화면에 맞지 않는 outcome을 App이 거부하는 정책과 전환 중 입력 차단을 명시. 화면별 컨텍스트 유효기간과 OnExit 후 무효화 필요. '한 번만'이라는 호출자 의무만으로 방어 규칙이 정해지지 않음.
- 보완 2: Boot와 화면 씬 사이 EventSystem·UI 입력 모듈·카메라·AudioListener 배치 및 수명/소유를 정해 추가 로딩 중 중복 객체를 예방. 씬 진입 컴포넌트 누락·중복 또는 로드 실패 시 처리도 정의 요청.
- 보완 3: 공용으로만 표기된 Data/Traps·Audio·ThirdParty·Settings는 개별 파일 작성자를 고정하는 절차를 명시. 공용 예약 승인과 단일 작성자 소유는 별개임을 유지.
- 이후 의존: C-004·006·008 초안이 없어 데이터 경계 검토 미완료. P1-X07=진행중(C-001~003 검토 의견 전달, 나머지 약속 대기). 화면 설계 P1-X01~06은 사용자 시작 지시 전 대기.
- 특이사항 정정: 이전 12:51 기록은 당시 상태이며, 최신 D-013에 따라 아키텍처는 승인 완료, CONTRACTS는 초안 작성됨. R-002는 Claude G-CL-009 종료 기록상 main 반영 완료. T-CX-002는 13:22 생성·미리보기 완료 후 사용자 검토 대기 기록을 유지.
- 검증 [AI 직접 확인]: 문서 간 소유권·의존 방향·신호·전환 규칙 비교. 런타임 검증·Unity Play 미수행, Git 쓰기 없음.

## T-CX-004 직접 제작 주인공 걷기 및 HTML 동작 검토
- 상태: 사용자 검토 대기
- 날짜: 2026-10-13
- 승인 근거: 사용자 "저 사이트에서 받는 게 아니라 너가 직접 만들건데 비슷하게 제작" 및 움직이는 결과를 HTML로 확인 요청.
- 수정 범위: Docs/References/codex_hero_walk_v1.png, codex_hero_walk_v1.json, codex_hero_walk_preview_v1.html, 기존 Codex 레퍼런스 HTML 링크, 자기 작업·표시·메시지 기록.
- 제작: 내장 Imagegen으로 선택한 흰 머리·검은 금색 장교 캐릭터를 참고해 새 걷기 시트 생성. SpriteFlow 다운로드·크레딧·원본 생성물 사용 없음.
- 제작 지시 요약: 투명 배경, 정면·왼쪽·오른쪽·후면 4방향, 방향당 4프레임, 다리 교대·팔 반대 움직임, 동일 제복·비율·도트 표현 유지.
- 결과: 1254×1254 RGBA PNG, 실제 16개 포즈. 생성 시트의 행 간격이 완전한 균일 격자가 아니어서 읽기 분석으로 각 프레임의 경계와 발 기준점을 JSON에 기록. 원본 이미지는 수정하지 않음.
- HTML: PNG·메타데이터 내장 단독 파일. 방향 전환, FPS·표시 높이, 정지·한 프레임, 발 기준선 정렬, 4방향 동시 재생, 프레임 비교, 방향키/WASD와 좌우 자동 이동, PNG·JSON 다운로드.
- 기존 레퍼런스 HTML에 새 걷기 미리보기 링크를 추가함.
- 확인 결과 [AI 직접 확인]: 브라우저에서 이미지 로드, 정지·한 프레임 이동(2/4 표시), 오른쪽 방향 전환, FPS 8→6 변경, 자동 이동 및 재생 상태 확인. 투명 배경과 4방향 미리보기 표시 확인. PNG 원본 복사 및 JSON 사각형 범위 확인.
- 사용자 확인 절차: 새 HTML을 열고 자동 이동 또는 방향키 조작 → 4방향 동시 재생·한 프레임으로 머리/옷/발 비교. FPS 4~8 범위에서 발 움직임과 이동 속도 조합 확인.
- 제한: 게임용 최종 원본 도트 규격은 미정. AI 시트의 머리·의상 픽셀 미세 변화와 4프레임 동작의 거친 타이밍은 검토 후 보정 대상. Game 반영·Unity Play·Git 쓰기 없음.
- 참고: SpriteFlow 유료 다운로드 질문 Q-CX-002는 이 직접 제작 결과의 사용 조건이 아니며, 이전 생성물에만 해당.

### T-CX-004 재생 보완 — 2026-10-13
- 사용자 요청: 1→2→3→4→3→2→1 왕복 재생, 재생 버튼을 눌러도 자동 프레임이 실행되지 않는 문제 수정.
- 원인 [AI 직접 확인]: 이동 tick에서 표시 높이 h가 정의되지 않아 ReferenceError가 나며 기존 requestAnimationFrame 루프가 중단됨. 이전 자동 이동 확인 기록만으로 지속 재생 검증이 충분하지 않았음.
- 수정: 이동 계산에 높이 값을 명시적으로 읽고, 프레임 전환 타이머와 화면 갱신을 분리. 재생 버튼은 타이머를 다시 시작하고 정지·한 프레임·FPS 변경도 같은 왕복 순서를 사용함.
- 재생 순서: 0 기반 [0,1,2,3,2,1] 반복. 화면에서는 1→2→3→4→3→2→1→2…로 보이며 양 끝 프레임 중복 정지는 없음.
- 변경 파일: Docs/References/codex_hero_walk_preview_v1.html, codex_hero_walk_v1.json (playbackMode·playbackSequence 및 HTML 내장 메타데이터 동기화). 원본 PNG는 유지.
- 검증 [AI 직접 확인]: 재생 버튼 후 수동 클릭 없이 프레임 1→3 변화 관찰. 한 프레임 클릭으로 2·3·4·3·2·1·2 순서 확인. 최신 페이지 새로 열기 후 새 오류 로그 없음. 자동 이동 체크·재생 동작 확인. Game·Git 쓰기 없음.
- 상태: 사용자 검토 대기 유지. 기존 열린 HTML은 새로고침하거나 닫고 다시 열어 최신 코드 확인.

## T-CX-005 CONTRACTS C-004·C-010 초안 검토
- 상태: 구현 완료
- 시작·종료: 2026-10-13 13:38 KST
- 승인 근거: 사용자 '검토', M-CL-012. D-014 및 M-CL-010·011 확인.
- 기준 커밋: 5e53c012a0c598ba4ec8cc02add3357be4e02a3d
- 수정 범위: 자기 tasks.md·outbox.md·status.js 검토 기록만.
- 결과: 기존 M-CX-007 보완 세 건 반영 및 C-001~003 승인 확인. C-004에서 Combat은 LastBattle·Targeting만 기록하고 Meta가 결과 반영하는 경계, StoryRequest(일반/패배 엔딩 + StoryId) 읽기, MetaCatalog·StoryCatalog의 Codex 소유, Definitions 제공 방향에 동의. 사용자 D1~D4를 대신 확정하지 않음.
- 보완 요청 1: Traps 쓰기가 Prep에만 허용돼 있어 전투에서 소모한 함정을 승리 후 Result가 제거/갱신할 수 없다. C-006·007·008과 연결해 소비 반영 담당·시점·1회 반영 규칙을 명시할 것.
- 보완 요청 2: SquadState는 병종·Count·PostId뿐이라 동일 병종을 두 지점에 나누면 병종 ID로 결과를 매칭할 수 없다. 분대별 고유 ID 또는 고유 키를 정할 것. HeroState에도 배치 정보가 없어 영웅/장교 배치를 저장할 위치를 정의할 것(고정 배치라면 그 제한 명시).
- 보완 요청 3: C-010의 전역 고유 ID 원칙을 검증하는 시점과 잘못된 카탈로그 처리 정책이 없다. 중복 ID·공백 ID·잘못된 참조/타입은 시작 전 검증해 임의 덮어쓰기·부분 진행을 방지하도록 명시 요청.
- 추가 연결: BattleNumber는 다음 전투 번호이므로 지난 결과 번호는 LastBattle에 따로 담아야 함(C-006). LastBattle 초기 null/이전 결과 재사용 방지, 카탈로그 등록 누락 검증도 후속 약속에서 필요.
- ROADMAP: P1-X07 진행중 유지(C-004 검토 의견 전달, C-006·008 미작성). P1-X03 스토리 제안·화면 설계 착수 없음.
- 검증: 문서상 필드/쓰기 권한/소유 비교만. 런타임 검증·Game 변경·Git 쓰기 없음.

### T-CX-004 팔다리 교대·정면 머리 보완 — 2026-10-13 13:56 KST
- 사용자 요청: 측면 팔다리 앞뒤 교대, 후면 양쪽 다리·팔 교대, 정면 머리 흔들림 감소.
- 승인 범위: 기존 Codex 걷기 HTML 및 메타데이터·자기 기록. Game·Git 작업 없음.
- 제작 검토: Imagegen으로 4×4 교대 걷기 재생성 2회 시도. 결과에도 측면의 같은 밝은 부츠가 계속 앞에 오는 문제가 남아 새 PNG를 채택하지 않음. 기존 원본 PNG는 유지.
- 보완: HTML에 측면·후면 분절 렌더링을 추가. 고정된 한 포즈의 팔·다리를 잘라 각각 반대 회전/높이 위상으로 표시해 실제 교대를 보장. 오른쪽은 왼쪽 보정 포즈를 반사. 정면 몸은 기존 4프레임을 유지하고 머리는 첫 프레임 동일 좌표로 고정.
- UI: 기본 켜짐인 '정면 머리 고정', '측면·후면 팔다리 교대 보정' 체크 추가. 보정을 끄면 기존 원본과 비교. 기존 1→2→3→4→3→2→1 재생 유지.
- 변경: Docs/References/codex_hero_walk_preview_v1.html, codex_hero_walk_v1.json, 신규 codex_walk_rendering_v2.js(HTML 안에도 내장). PNG 다운로드에 렌더링 보정이 포함되지 않는다는 안내와 JSON previewCorrection 추가.
- 검증 [AI 직접 확인]: 브라우저 정지·측면 선택·보정 해제/재적용·재생 클릭·수동 입력 없이 프레임 1→2 변화 확인. 새 오류 로그 없음. 4포즈 비교 화면 시각 확인. 코드 확인: 측면·후면 팔/다리 반대 위상 및 정면 네 프레임 동일 머리 좌표, 전체 스크립트 구문 통과.
- 캡처: C:/Unity/1/HumanLastRampart/Docs/collab/live/review/codex_T-CX-004_walk_correction_v2.jpg
- 제한: 보정은 HTML에서 렌더링하는 동작 시안이며 다운로드 PNG에는 없음. 분절 경계·도트 회전·좌우 반사에 따른 제복 비대칭은 검토 필요. 자연스러움 최종 판단과 게임용 픽셀 프레임 확정/Unity 검증은 미완료. 단독 HTML은 보정 JS를 내장하므로 추가 파일 없이 열림.
- 상태: 사용자 검토 대기. MD와 HTML/표시 요약 갱신 완료.

## T-CX-006 Claude 대체 인계 및 C-005~C-007 초안
- 상태: 사용자 검토 대기
- 사용자 지시 근거: Claude 토큰 소진, '너가 이어서할거야' 및 다음 C-005·006·007 작성 요청.
- 대체 시작: 2026-10-13 14:00 KST. 조정·통합·main 반영 대체 담당 Codex. 종료는 사용자 복귀 지시 때 기록.
- 인계 상태 [AI 직접 확인]: Claude 마지막 G-CL-016 종료(d56311779dbf50c84f7cc5e6063dd16db7950e26), 추적 파일 미커밋·스테이징 없음. 유효 공용 예약·미종료 허가 없음. 별도 인계 메모 없음. 사용자 토큰 소진 보고와 완료 기록을 근거로 인계. 새로운 Claude 작업 재개는 사용자 복귀 지시 필요.
- 현재 develop=d56311779dbf50c84f7cc5e6063dd16db7950e26, main=7043218b6e66c99870ef2afa00a81242efed56ba. 최근 미반영 커밋은 _HTML·아키텍처 승인·CONTRACTS 순차 기록. 신규 main 승인/반영 없음.
- 대기 문서 권한: CONTRACTS.md, ROADMAP.md, ROADMAP.html 수정 경로별 사용자 허가 질문 중. 해당 파일을 수정하지 않고 Codex 소유 초안으로 준비 완료.
- 작성 파일: Docs/collab/live/codex/contracts_C005_C007_draft.md (제안 r1, Git 무시). 자기 작업·질문·표시 기록.
- 포함: 전투 준비 깊은 복사, 끝난 전투 번호+실행 ID, App 번호 증가와 Result 번호 검사 정합성, 결과 중복 방지 표시, 보상/손실/소비 함정 검증 후 일괄 반영, 패배 엔딩 무반영, C-004 v1.1 연결 필드 제안.
- 사용자 결정: E1 반영 시점, E2 오류 복귀, E3 마지막 웨이브 처리, E4 C-004 추가 필드. 설계 확정 아님.
- ROADMAP: P1-C05 초안 준비·검토대기(대체 기간 Codex 기록). 원본 ROADMAP 두 파일은 경로 허가 대기. P1-X07은 C-008 검토가 남아 진행중 유지.
- 남은 일: 경로별 허가 확인 후 CONTRACTS 및 ROADMAP md/html 반영. 대체 전환 live 백업 생성·동일성 확인 예정. Game 구현·브랜치 전환·Git 쓰기 없음.

### T-CX-004 SpriteFlow 기준 재제작 시작 — 2026-10-13
- 사용자 피드백: 머리 고정과 분절 팔다리 보정은 더 부자연스러우며 겹침 발생. 모두 해제·제거하고 SpriteFlow 생성 결과를 기준으로 다시 제작 요청.
- 이번 범위: 공개 SpriteFlow 미리보기 참고 파일, 신규 걷기 PNG·JSON·참고 MD, 기존 미리보기 HTML 교체, 자기 기록. Game 및 Git 쓰기 없음.
- 참고 확인: SpriteFlow four-way-idle-1318의 공개 512×512 미리보기를 실제 확인하고 Docs/References/codex_spriteflow_idle_reference.png에 보관. 유료 원본/ZIP 내보내기 사용 없음.
- 제작 방식: Imagegen에 해당 파일을 직접 참고로 넣어 실제 걷기 프레임 재생성 진행. 머리 따로 덧씌우기·팔/다리 분절 회전은 사용하지 않음.
