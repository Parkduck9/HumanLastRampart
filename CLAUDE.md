# Human Last Rampart — Claude 진입점

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
- **내 소유 파일만 쓴다.** `AGENTS.md`, `Docs/collab/live/codex/`, Codex 소유 코드·씬은 읽기만 한다.
- 내 기록은 `Docs/collab/live/claude/`에만 쓰고, 모든 기록은 맨 아래에 추가만 한다.
- `live/claude/tasks.md`나 `outbox.md`를 바꾸면 `live/claude/status.js`도 갱신한다.
- **브랜치를 전환하지 않는다** (최초 develop 전환 1회는 2026-10-13 초기 준비에서 수행). 커밋은 내 파일만 파일 단위로 지정하고, Git 쓰기는 10.3 순서를 따른다.
- 검증 구간("검증 시작"~"검증 끝")에는 `Game/`을 수정하지 않는다.

## 환경 메모
- Claude 실행 환경의 PATH에는 `git`이 없다. 현재 아래 절대 경로를 사용한다 (Codex 런타임 제공. 업데이트 시 경로가 바뀔 수 있으니 실패하면 사용자에게 알린다).
  `C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe`
