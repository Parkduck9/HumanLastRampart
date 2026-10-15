# K-003 결과 — 밤 시험 스크립트 · 전투 조작표 정리

## 한 일
1. **밤 시험 스크립트** `Tools/NightTest/run-night.ps1` (PowerShell 5.1, UTF-8 BOM)
   - 유니티가 이미 켜져 있으면 알리고 멈춤 (`Get-Process Unity`)
   - 차례로 일괄 실행: 편집 시험(`!SimProbe`) → 화면 시험(PlayMode) → 측정 `SimProbe.Fuzz10` → `SimProbe.Funnel40Sweep` (측정은 `HLR_PROBE_OUT`에 결과 파일 경로 지정)
   - 결과 폴더 `Tools/NightTest/reports/<날짜_시각>/`에 xml·로그·측정 파일·새 화면 사진(`shots/` 복사) 모음
   - 아침 보고서 `report.md`·`report.html`: 시험별 통과/실패 수, 실패 시험 이름·메시지, 로그 오류 줄(error CS·Exception, 중복 제거), 측정 내용 그대로, 새 사진 목록(화면 시험 시작 뒤 바뀐 `Game/playprobe/*.png`)
   - 덧붙임: 인자 `-ProjectPath`(기본 메인 Game 폴더)·`-UnityPath`·`-TimeoutMinutes`(기본 120, 넘으면 유니티 끄고 다음 단계)·`-SkipEdit/-SkipPlay/-SkipProbe`. 실패가 있으면 종료 코드 2
2. `Tools/NightTest/README.md` — 쓰는 법 한 단락
3. **`Docs/GDD/screens/Battle.md` 7-1 조작 정리**를 코드(BattleCameraRig·BattleEntry·BattleMapCamera·BattleHud 도움말)에 맞춰 고침, 변경 기록 한 줄 추가
   - A/D 늘 달리기·모서리 돌기(D-061), W/S 통로 안쪽(S, 앞모습)·바깥쪽(W, 뒷모습), Q/E·Tab·C·1/2/3(1배/2배/4배)·P·G·휠 굴리기(지도 확대) 추가
   - **M 열기 · Esc 닫기**로 바로잡음 (지도 안에서는 M도 닫기, 명령 메뉴가 열려 있으면 Esc가 메뉴 먼저 닫음)
   - 성벽 화면 마우스 왼쪽 '설정 창' → '명령 메뉴 고르기', 오른쪽 → '명령 메뉴 닫기', 지도 왼쪽에 함정 점화(D-050) 추가

## 바꾼 파일
- `Tools/NightTest/run-night.ps1` (새)
- `Tools/NightTest/README.md` (새)
- `Docs/GDD/screens/Battle.md`
- `CARD_RESULT.md` (이 파일)

## 확인한 것
- 지시한 명령으로 문법 확인 통과, 파서 오류 0개. 파일 첫 바이트 EF BB BF(BOM) 확인
- 유니티는 열지 않음. 대신 임시 폴더에 스크립트 복사본 + **가짜 유니티(xml·로그·측정 파일·사진을 흉내 내는 작은 스크립트)**로 끝까지 돌려 보고서 두 개가 제대로 나오는 것 확인. 이때 `Md` 함수 이름이 PowerShell 기본 별칭 `md`(mkdir)와 겹쳐 실패하는 문제를 찾아 함수 이름을 고침
- SimProbe(`Fuzz10`·`Funnel40Sweep`, 이름공간 없음)와 PlayProbe(사진 기본 위치 `playprobe` = 작업 폴더 기준)는 메인 폴더에서 이름만 확인 (이 카드 가지에는 없는 파일 — 커밋 안 된 임시 도구)

## 확실하지 않은 것
- **진짜 유니티로는 한 번도 안 돌려 봄.** 특히 `-testFilter "!SimProbe"`(제외 필터)가 이 유니티 6 버전 시험 도구에서 그대로 먹는지, PlayMode 일괄 실행에서 화면 사진이 실제로 찍히는지(-batchmode에서 그리기)는 첫 밤 시험에서 확인 필요
- 화면 사진은 유니티를 프로젝트 폴더를 작업 폴더로 해서 띄우므로 `Game/playprobe`에 쌓인다고 보고 그 폴더만 봄 (`HLR_PLAY_OUT`을 따로 지정하면 못 찾음)
- SimProbe·PlayProbe가 메인 폴더에서 커밋 안 된 임시 도구라, 지워지거나 이름이 바뀌면 측정 단계는 '시험 0개'로 나옴
- Battle.md의 F8(성능 표시)은 전투 화면 코드에 연결이 안 보여 'PerfLab 전용'으로 적음. 3.1-1의 "숫자 1~3"(실제 메뉴는 1~6까지 받음)은 조작표 범위 밖이라 그대로 둠
