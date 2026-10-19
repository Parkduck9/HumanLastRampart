# Human Last Rampart — Claude 진입점

> **가장 먼저: 사용자에게 보내는 모든 글은 한글로만 쓴다.** 긴 보고, 짧은 중간 알림("시험 돌리는 중" 같은 한 줄), 제목·표 머리, 정정 문장까지 전부. 영어는 코드·파일 이름·명령어만. 보내기 전에 영어 문장이 섞였는지 한 번 더 본다. (사용자가 여러 번 지적함 — 2026-10-14)

@FirstRead.md

## 세션 시작 시 반드시
1. 위 FirstRead.md와 [`Docs/collab/rules/RULES.md`](Docs/collab/rules/RULES.md)(확정 v0.3)를 읽는다. **FirstRead > RULES > 기타 문서.**
2. [`Docs/collab/rules/DECISIONS.md`](Docs/collab/rules/DECISIONS.md) → [`Docs/collab/rules/CONTRACTS.md`](Docs/collab/rules/CONTRACTS.md)를 읽는다.
3. Codex의 메시지 `Docs/collab/live/codex/outbox.md`와 내 기록 `Docs/collab/live/claude/tasks.md`를 확인한다.
4. 사용자가 **"협업 확인"**이라고 하면 RULES 6.1 절차를 따른다.

## Claude의 역할 (RULES 3장)
- 조정 담당, 통합 담당, main 반영 실행
- 작업 영역: 아키텍처, Core, CONTRACTS, 1·2번 전투, 함정의 전투 중 감지·발동·피해, status.html, RULES.html
- Codex 토큰 부족 시 사용자 지시로 대체 담당 (RULES 3.4)

## 꼭 지킬 것
- 사용자 보고는 **한글**로 한다. 모르는 것은 추측하지 않고 묻는다.
- **AI끼리 주고받는 글은 영어가 기본이다** (2026-10-19 사용자 지시, 토큰 절약): Codex 카드 지시문과 `CARD_RESULT.md`, 하위 에이전트·다른 세션에 보내는 지시와 메시지. 사용자가 보는 것(답장, ROADMAP·DECISIONS·검토 페이지, 화면·현황판에 나오는 기록, 게임 안 글, 코드 주석)은 그대로 한글.
- 토큰 절약 (2026-10-19 사용자 지시): 자동 압축은 60만 토큰(`~/.claude/settings.json` `autoCompactWindow`). 토큰이 갑자기 많이 줄면 여러 에이전트·세션·예약 작업이 의도치 않게 돌고 있는지 먼저 확인한다.
- **내 소유 파일만 쓴다.** `AGENTS.md`, `Docs/collab/live/codex/`, Codex 소유 코드·씬은 읽기만 한다.
- 내 기록은 `Docs/collab/live/claude/`에만 쓰고, 모든 기록은 맨 아래에 추가만 한다.
- `live/claude/tasks.md`나 `outbox.md`를 바꾸면 `live/claude/status.js`도 갱신한다.
- **어떤 작업이든 끝나면 [`Docs/GDD/ROADMAP.md`](Docs/GDD/ROADMAP.md)와 [`Docs/collab/view/ROADMAP.html`](Docs/collab/view/ROADMAP.html)을 함께 갱신한다** (체크 상태, 다음 할 일, 결정 대기, 변경·특이사항 기록). status.js의 `roadmap` 값도 맞춘다 (D-011, RULES 7.3).
- 설계 기준 문서: [`Docs/GDD/ARCHITECTURE.md`](Docs/GDD/ARCHITECTURE.md) (승인 v1.0, D-013. 6장 데이터 약속은 CONTRACTS 승인 전까지 확정 API가 아니다).
- **브랜치를 전환하지 않는다** (최초 develop 전환 1회는 2026-10-13 초기 준비에서 수행). 커밋은 내 파일만 파일 단위로 지정하고, Git 쓰기는 10.3 순서를 따른다.
- 검증 구간("검증 시작"~"검증 끝")에는 `Game/`을 수정하지 않는다.

## 환경 메모
- Claude 실행 환경의 PATH에는 `git`이 없다. 현재 아래 절대 경로를 사용한다 (Codex 런타임 제공. 업데이트 시 경로가 바뀔 수 있으니 실패하면 사용자에게 알린다).
  `C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe`
