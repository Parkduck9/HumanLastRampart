# K-048 작업 결과

2026-10-16. 사용자 확정 전 시안(D-064), 게임 연결 대기(D-074).

## 한 일

- 앞 병사는 당기고 뒤 병사는 미는 6칸 보행 시안을 다시 만들었다. 왼발·오른발을 명암으로 구분하고, 부츠를 실측해 디딤 동안 같은 지면 좌표에 남도록 배치했다.
- 설치 대포와 정지 대포병 그림을 직접 측정해 새 병사210px와 같은 비율로 대포242×224px를 만들었다. 병사 칸256×384, 발 기준(128,352). 대포 칸512×384, 기준(256,352).
- 높은 도화선에 닿는 점화6칸을 다시 만들었다. 0.50초 점화→1.50초 발사로 K-043의 1초 대기를 유지했다.
- 같은 배율 비교, 설치 대포3방향, 재생/정지/이전·다음 칸, 접지선·지면 눈금, 바퀴 회전, 점화 시간 이동을 독립 HTML에 넣었다.

## 직접 잰 값과 적용 근거

알파128 초과 경계를 측정했다. 지시의 Combat/Battle/Resources/BattleArt 경로는 없어 실제 Art/Battle/Resources/BattleArt 원본을 읽었다.

| 원본 | 설치 대포 폭×높이 / 병사 키 | 정지 병사 대비 비율 | 병사210px 환산 |
|---|---|---|---|
| 3방향 왼쪽 | 321×320px / 300px | 1.070 / 1.067 | 224.7×224px |
| 3방향 정면, 채택 기준 | 345×320px / 300px | 1.150 / 1.067 | 241.5×224→242×224px |
| 3방향 오른쪽 | 338×320px / 300px | 1.127 / 1.067 | 236.6×224px |
| K-016 반동 첫 칸 | 230×208px / 300px | 0.767 / 0.693 | 161×145.6px |

정지 대포병은 원본 실체300px였다. 새 병사210px를 유지하면서 같은 비율을 비교하기 위해 설치 대포와 정지 병사를 함께0.7배로 표시했다. 기존 달리기 그림 전체 알파 높이는222/203/223/211px로 별도 기록했다. K-043 대포241×116px에 비하면 새 대포 높이가 약1.93배다. 이는 원본 픽셀 비율이며 실제 게임 캡처의 화면 배율 실측은 아니다.

보행 접지x는84→124→164 / 84→124→164px, 칸마다 왼쪽40px 이동한다. 양 병사의 실제 PNG 부츠를 다시 측정한 지면x는 왼발84/84/84, 오른발-36/-36/-36px로 고정된다. 기본6칸/초, 주기240px, 바퀴 반지름71px, 회전각=-이동거리/71. 이동과 회전은 칸 경계에서 갱신한다.

점화4번째 칸 불씨(66,133)와 대포 도화선(346,133)을 맞췄다. 대포 왼쪽 위(380,0), 점화 병사 칸 왼쪽 위(660,0), 접점(726,133)이다. 발은352px에 유지한다.

## 바꾼 파일 목록

아래 파일은 모두 이번 카드의 신규 파일이다. 기존 원본·Game/·live/·전체 목록 HTML은 수정하지 않았다.

- CARD_RESULT.md
- _HTML/4_그림_캐릭터/codex_K-048_대포끌기_다시_v1.html
- Docs/References/codex_k048_v1/BitmapTools.cs
- Docs/References/codex_k048_v1/README.md
- Docs/References/codex_k048_v1/build.ps1
- Docs/References/codex_k048_v1/cannon_body.png
- Docs/References/codex_k048_v1/cannon_candidate_long.png
- Docs/References/codex_k048_v1/cannon_source.png
- Docs/References/codex_k048_v1/compose.ps1
- Docs/References/codex_k048_v1/contact_review.png
- Docs/References/codex_k048_v1/crew_candidate_initial.png
- Docs/References/codex_k048_v1/crew_candidate_middle.png
- Docs/References/codex_k048_v1/crew_ignite6.png
- Docs/References/codex_k048_v1/crew_pull6.png
- Docs/References/codex_k048_v1/crew_push6.png
- Docs/References/codex_k048_v1/crew_source.png
- Docs/References/codex_k048_v1/generation.json
- Docs/References/codex_k048_v1/haul_1.png
- Docs/References/codex_k048_v1/haul_2.png
- Docs/References/codex_k048_v1/haul_3.png
- Docs/References/codex_k048_v1/haul_4.png
- Docs/References/codex_k048_v1/haul_5.png
- Docs/References/codex_k048_v1/haul_6.png
- Docs/References/codex_k048_v1/ignite_candidate_long.png
- Docs/References/codex_k048_v1/ignite_candidate_short.png
- Docs/References/codex_k048_v1/ignite_contact_review.png
- Docs/References/codex_k048_v1/ignite_source.png
- Docs/References/codex_k048_v1/installed_aim3.png
- Docs/References/codex_k048_v1/installed_crew.png
- Docs/References/codex_k048_v1/installed_recoil3.png
- Docs/References/codex_k048_v1/manifest.json
- Docs/References/codex_k048_v1/measurements.json
- Docs/References/codex_k048_v1/muzzle_smoke.png
- Docs/References/codex_k048_v1/previous_cannon.png
- Docs/References/codex_k048_v1/prompts.json
- Docs/References/codex_k048_v1/qa_assets.json
- Docs/References/codex_k048_v1/qa_html.json
- Docs/References/codex_k048_v1/review_template.html
- Docs/References/codex_k048_v1/scale_review.png
- Docs/References/codex_k048_v1/verify.mjs
- Docs/References/codex_k048_v1/verify_assets.ps1

## 확인한 것

- 저장된 PNG 픽셀·투명도·규격·실제 부츠 접지·참조 원본 해시·점화 높이 90개 검사 통과: qa_assets.json.
- HTML 문법·내장 그림과 원본 일치·좌표 범위·재생/정지/칸 순환·시간 이동·점화→발사1초·연기6칸·배경/배율/시점 등 Node VM 모형 63개 검사 통과: qa_html.json. 합계153개. 실제 브라우저 검사가 아니다.
- 최종 시트와 scale_review.png, haul_1~6.png, ignite_contact_review.png, contact_review.png를 직접 열어 비교했다. 브라우저 캡처가 아닌 정적 합성이다.
- 보관 원안만으로 build.ps1→compose.ps1→verify_assets.ps1 재실행 성공.
- 원본 저장소 HEAD는 develop, 카드 HEAD는 card/K-048로 읽기 확인했다. Git 실행 파일이 명령 경로에 없어 git status/diff는 수행하지 않았다. Git 쓰기는 하지 않았다.
- Unity 실행·Play·실제 게임 캡처·Game/ 수정·협업 기록 수정·기존 HTML 수정 없음.

## 확실하지 않은 것·남은 것·질문

1. 실제 브라우저 목록이 비어 있었고 열기는 Browser is not available: iab로 실패했다. CSS·스크롤·마우스 조작·실시간 재생의 육안 확인은 남아 있다. 정적 그림과 모형 검사만 완료로 기록했다.
2. 원본 정지 병사300px와 요청210px 사이 차이를 위와 같이0.7배 환산했다. 설치3방향과 K-016 반동 원본도 크기가 다르다. 실제 게임에서 적용 중인 화면 배율 확인은 Claude 연결·사용자 캡처 후 필요하다. 질문: 정면3방향 원본의 비율을 채택 기준으로 삼은 것이 의도에 맞는가?
3. 새 원안은 실측 외곽에 맞춰 가로0.2056075/세로0.2191781로 정리해 약6.6% 비율 보정을 했다. 정면 설치 그림과 옆모습 운반 그림의 투영도 다르다. 짧고 큰 대포가 같은 무기로 읽히는지와 바퀴 형태는 사용자 외형 검토가 필요하다.
4. 발 고정은 칸마다40px 이동하는 이 페이지의 규칙에서 검증했다. 연속 이동 보간은 구현하지 않았다. 실제 게임에서 연속 이동할 때 발 고정 처리와 바퀴 회전 연결을 다시 확인해야 한다.
5. 6→1 연결의 최종 자연스러움·무게감·손의 파지·앞 끈/뒤 밀대·점화 자세는 사용자 검토 대상이다. 사용자 확정 후 Claude의 게임 연결과 실제 화면 비교가 남는다.
