# 바퀴 1 검토·Play 준비

- 작성자: Codex
- 갱신: 2026-10-14 08:28 KST
- 상태: 시안·설계 검토 준비. Play 검증 준비 완료 아님.

현재 Game/Assets/_Project/Data/Combat/Start/campaign_start.asset에 미커밋 변경이 있어 검증 기준 커밋은 Claude 정리 후 확정한다. 이전 QA 결과는 이전 대상의 기록이며 현 작업 폴더의 통과로 확장하지 않는다.

## 검토 자료

- 스토리 데이터: Docs/GDD/screens/StoryDataProposal.md
- 0번 메인: Docs/GDD/screens/MainMenu.md
- 5번 스토리: Docs/GDD/screens/Story.md
- 4번 결과: Docs/GDD/screens/Result.md
- 3번 정비: Docs/GDD/screens/Prep.md
- 스토리 제작 툴: Docs/GDD/screens/StoryEditor.md
- 외형 후보6종: codex_cy1_art_v1/preview.html
- 통합 검토 화면: codex_cycle1_review.html

## Play 확인 순서

1. Claude가 미커밋 게임 변경을 정리하고 검증 기준 커밋 X를 제시한다.
2. 사용자가 X를 대상으로 “검증 시작”을 알린다. 양쪽 AI 중단 기록과 Game/X 일치를 조정 담당이 확인한다.
3. “검증 준비 완료” 안내 후 Unity에서 Game/Assets/_Project/Scenes/Boot.unity를 열고 Play한다.
4. 게임 시작 → 쉬움 → 스토리 진행 → 실제 전투: 4면 적 등장과 아군 사격을 확인한다.
5. 첫 전투 승리 시 결과 화면의 전투 번호·처치 수·획득 골드를 확인한다. 현재 화면은 요약 표시이며 손실 상세 UI는 제안 단계다.
6. 다음 → 정비 → 다음 전투 연결을 확인한다. 패배 시에는 결과를 건너뛰고 패배 엔딩 → 메인으로 돌아오는지 확인한다.
7. 골드·병력·성벽 및 중복 반영은 개발용 진입/상태 확인을 포함한 별도 절차로 검증한다. 요약 화면만 보고 전부 통과했다고 기록하지 않는다.
8. 콘솔 오류와 전투/결과 화면을 사용자 캡처로 전달한다. 확인·main 승인 절차 뒤 사용자가 “검증 끝”을 알린다.

실행하지 않은 Play·Unity QA는 완료로 표시하지 않는다. RULES 11.1: “Play 확인과 실제 캡처는 사용자가 직접 한다.” 설계·외형은 개별 검토 전 확정으로 처리하지 않는다.
