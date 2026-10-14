# Human Last Rampart — Codex 진입점

## 세션 시작과 협업 확인
1. `FirstRead.md` → `Docs/collab/rules/RULES.md` → `Docs/collab/rules/DECISIONS.md` → `Docs/collab/rules/CONTRACTS.md` 순서로 읽는다. 우선순위는 FirstRead > RULES > 기타 문서다.
2. Claude의 `Docs/collab/live/claude/outbox.md`에서 새 메시지를 확인한다.
3. 현재 유효한 조정 담당의 tasks.md에서 예약·인계·미종료 Git 허가를 확인하고, 자기 tasks.md의 진행 중 작업을 확인한다. 평상시 조정 담당은 Claude다.
4. 처리 결과와 막힌 점을 한글로 보고한다. 수행하지 않은 검증을 완료로 기록하지 않는다.

## Codex 담당 (RULES 3장)
- 0번 메인 화면: 시작 / 설정 / 종료
- 3번 상점·모집·수리·함정 배치/구매/저장
- 4번 보상·손실 화면
- 5번 스토리 화면과 스토리 제작 툴
- 상대 코드 리뷰는 읽기와 수정 요청으로 진행한다.
- 조정·통합·main 반영 대체는 사용자의 명시적인 지시가 있을 때만 RULES 3.3을 따른다.

## 소유권과 승인
- 자기 소유 파일만 작성한다. Claude 소유 파일은 읽기만 하고, 수정이 필요하면 자기 outbox로 요청한다.
- 설계·인터페이스·씬/폴더 구조·패키지·ProjectSettings·UI/아트/카메라 방향은 사용자 확정 후 구현한다. 제안 상태 CONTRACTS는 확정 API로 사용하지 않는다.
- 공용 경로는 조정 담당 tasks.md의 예약 승인 후에만 수정한다. Unity 폴더·씬 소유권과 asmdef 의존 방향은 승인된 CONTRACTS를 따른다.
- `.meta`는 에셋과 함께 커밋한다. Unity 에디터의 Play와 실제 캡처는 사용자가 수행한다.
- 검증 구간("검증 시작"~"검증 끝")에는 Game/ 수정을 중단하고 RULES 11.2를 따른다.

## 기록과 소통
- 자기 기록은 `Docs/collab/live/codex/tasks.md`, `outbox.md`, `questions.md`에만 작성한다. MD 기록은 맨 아래에 추가하고, 과거 기록은 수정·삭제하지 않는다.
- MD 변경 시 `Docs/collab/live/codex/status.js`도 갱신한다. 형식은 `Docs/collab/live/status.html` 상단 주석을 따른다. MD가 원본이고 JS는 표시용 요약이다.
- ID는 T-CX-### / M-CX-### / Q-CX-### / G-CX-###를 사용한다. 회신은 원본 메시지 ID를 인용한다.
- 메시지는 사용자의 "협업 확인"으로 전달한다. 같은 주제의 요청·회신 3회 후 미해결 의존 작업은 보류하고 사용자 질문으로 올린다.
- live/는 Git 무시 대상이며 worktree와 공유되지 않는다. 현재 협업 작업 폴더는 `C:\Unity\1\HumanLastRampart` 원본이다.

## Git (RULES 10장)
- 원본 폴더의 현재 브랜치가 develop인지 확인한다. 브랜치를 전환하지 않는다.
- Git 쓰기는 자기 tasks.md의 요청(+ outbox 알림) → 조정 담당 tasks.md의 허가 확인 → 자기 시작 기록 → 실행 → 종료 기록 순서다. 이전 미종료 허가를 추정으로 종료하지 않는다.
- 자기 소유 파일을 파일 단위로 지정하고 커밋 메시지에 작업 ID를 넣는다. `git add .`, `commit -a`, 상대 스테이징 해제, rebase/amend/reset/force push와 lock 직접 삭제는 금지한다.
- main 반영은 사용자 승인 커밋 묶음을 대상으로 조정 담당이 RULES 10.4에 따라 처리한다. 로컬 반영과 원격 게시를 구분한다.
- 초기 T-CX-001 커밋 대상은 AGENTS.md 하나다. live/codex/는 커밋하지 않는다.

## 사용자 표시·질문 선호 (2026-10-14 직접 지시)
- 사용자에게 묻는 질문은 이 채팅 본문에 직접 적는다. 질문 도구/외부 페이지에만 질문을 두지 않는다.
- 앞으로 Codex 확인용 HTML은 저장소 루트 `_HTML/`에 넣는다. 자기 신규 파일은 `codex_` 접두어로 만들고 서버 없이 직접 열 수 있게 필요한 리소스를 내장한다.
- 사용자가 이번 경로에 새 HTML을 두도록 명시 허가했다. Claude 소유 기존 파일/0_전체목록.html은 수정하지 않고 목록 동기화를 자기 outbox로 요청한다. 원본 MD와 제작 자료는 Docs에 보관하고 변경 때 관련 HTML도 갱신한다.
