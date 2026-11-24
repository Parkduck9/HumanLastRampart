# Codex 직접 QA 결과

- 시각: 2026-10-14 09:47 KST
- 사용자 지시: “너가 QA까지해봐”. 이번 직접 QA 허가로 해석하며 기존 규칙 본문은 수정하지 않음.
- 기준 HEAD: 38e4d4582e2fb4b36cb9b3de828d038460b65458 (develop).
- 결론: 결과 반영과 검토 HTML은 확인. Unity 컴파일·실제 전투 Play는 미완료. 전체 QA 통과 아님.

## 직접 실행한 검사

- 현재 Core 데이터/Meta 반영기 소스를 .NET 9 csc로 새 컴파일하고 기존 경계 사례 41개 실행: **41/41 통과**. 로그: live/codex/qa/current_result/tests_current.log. 보상·영웅·분대·성벽, 중복 실행 10회 무변경, 골드 오버플로, 누락/중복 ID, 손실 범위, 마지막 항목 오류에도 부분 반영 없음. Unity 화면 시험과 구분.
- 브라우저 검토 HTML: 6종 이미지 모두 로드(각 naturalWidth > 0), 스토리 설계 원문 펼치기 동작, 브라우저 오류 로그 0 확인.
- 이전 검사: 로컬 참조16개 존재 및 status.js 구문 정상. 이를 Unity 시험 통과로 확대하지 않음.

## Unity 실행과 제한

- 정확한 프로젝트 버전 6000.3.25f1 배치 EditMode 실행 시도. 라이선스 초기화/LicenseClient 연결 실패 및 com.unity.editor.headless 미발견 오류. 테스트 XML 미생성, 통과 수 없음. 제가 시작한 대기 프로세스 PID6252만 종료. 이전 Claude 27/27 기록은 이번 재실행 결과가 아님.
- Computer Use로 Hub에서 올바른 프로젝트를 선택하고 편집기 실행·Untitled 화면 확인. 관리자 권한 경고의 Ignore Warning은 누르지 않음. Restart as Standard User 선택 시 입력 결과 오류, 이후 관리자 편집기가 나타나 원인/외부 조작을 추정하지 않음.
- Boot 열기 대화상자에서 입력 helper가 settable/대상 창 불일치 오류. Play 시작하지 않았으며 메인/전투/결과 실제 화면을 확인하지 못함.
- 검사 중 Claude 소유 Combat/_PlayProbe/ 및 .meta가 새로 나타남. 이 파일들은 생성/수정/삭제하지 않음. 같은 프로젝트의 동시 QA 충돌을 피하고 조정 담당에게 단일 실행 창/담당 조정 요청.
- Game 변경·Git 쓰기 없음. 원격 게시/main 반영 없음. 현재 QA는 커밋X의 전체 Game 무변경 검증으로 확정하지 않음.
