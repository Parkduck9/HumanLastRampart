# K-052 결과 — 대포 끌기 3차 시안 v1

2026-10-16. 사용자 확정 전 시안(D-064). 게임 연결은 Claude 담당(D-074).

## 한 일

- 포신 중심선을 K-048 약185px에서 약463px로 늘렸다. 수작업 판독 좌표 기준 비율2.496배(약2.5배). 포구는 오른쪽, 견인은 왼쪽으로 두어 포가 꼬리를 끌게 했다.
- 기존 정면 설치 대포의 바퀴 높이144px를 정지 병사300px→210px와 같은0.7배로 환산해 새 바퀴 지름101px로 맞췄다. 포가의 옆모습 꼬리는 정면과 투영이 달라 별도 외형 검토 대상으로 표시했다.
- 기존 실제 픽셀에서 대포9색·병사14색·불씨3색을 추출하고 최종 PNG를 이 색으로만 제한했다. 주황 바퀴를 없애고 무쇠·갈색 목재·쇠테로 정리했다. 남색 코트·갈색 멜빵·흰 테두리를 유지하고 눈을 작은 선으로 줄였다.
- 앞 당김·뒤 밀기6칸과 점화6칸을 제작했다. 한 칸32px 이동과 바퀴 −이동거리/50.5 회전을 맞췄다. 디딤 발밑 픽셀 x=128,160,192 / 128,160,192, 발높이352px. 점화 하체는6칸 모두 같은 픽셀로 고정했다.
- 도화선 접점(269,132), 점화 병사 네 번째 칸 불씨 중심(132,132). 대포 배치x420·병사 배치x557에서 둘 다(689,132)에 닿는다. 접촉0.50초→발사1.50초를 유지했다.
- 검토 HTML에 기존 설치 대포·정지 대포병·달리기 옆모습과 새 그림 비교, 환산/원본1배 전환, 팔레트 견본, 외곽선 수치, 보행·점화 재생과 칸 넘김, K-048 전후 비교를 넣었다. PNG13개를 내장해 서버 없이 연다.
- 실제 견인 방향 근거: [미국 국립공원청 포가 꼬리와 전차 연결 설명](https://home.nps.gov/fosc/learn/education/artcannon.htm). 두 병사 인력 이동과 구체적인 파지는 게임용 표현이다.

## 팔레트·외곽선 비교

- 대포9색: #0C0A10, #2B2A35, #3F3F4B, #595660, #8F827D, #4E3627, #754F38, #936341, #B78154.
- 병사14색: #0A0809, #161B2E, #1E2444, #483F46, #2D2124, #562E19, #663921, #804728, #9A5031, #FBBD95, #F7DCB2, #F9B180, #3A3038, #4D424A.
- 불씨3색: #FFFA90, #FBB02A, #E75501. 불씨색은 대포에 사용하지 않았다.
- 원본 전체 상위64색과 빈도는 palette.json. 희소한 재질색이 전체 상위색에서 빠져 재질별 목표색에 가까운 실제 원본 RGB 중8회 이상 나온 색을 선택했다. 불씨만 출현 횟수 제한 없이 기존 점화 시트에서 추출했다. 최종 사용색은 used_palette.json이며 팔레트 외 불투명 RGB가0개였다.
- 어두운 외곽 깊이 중앙값: 기존 설치 대포4px·반동3px·정지/달리기 병사4px·점화5px. 새 대포2px·새 병사3종3px. 외곽색은 대포 #0C0A10, 병사 #0A0809. 원본 정지4px×0.7=2.8px에 맞춰 병사3px로 정리했다. 새 대포는 원본 환산2.8px보다 약간 가늘게 남았다.
- 측정은 실루엣 수평 경계의 어두운 연속 픽셀 깊이로, 내부 그림자도 섞이는 대용 지표다. 실제 테두리 두께의 완전한 일치나 아트 승인으로 기록하지 않았다. 표본수·사분위는 outline_measurements.json, 상세 제작법은 README.md.

## 규격

- 대포768×384, 기준점(336,352), 앞바퀴 중심(336.5,301.5), 지름101px.
- 병사 각2304×384, 한 칸384×384·6칸, 기준점(192,352), 인물210px. 불씨는 인물 높이에서 제외.
- 비교 기본값은 기존 정지 병사·설치 대포0.7배 / 새 그림1배다. 원본 픽셀1:1 비교도 별도로 제공했다. 실제 게임 표시 배율 측정이 아니다.

## 확인한 것

- 최종 PNG 검사62/62 통과: 시트 크기, 팔레트, 발높이, 실제 접지 x, 인물 높이, 점화 하체 픽셀 고정,101px 바퀴, 불씨 접점 높이, 읽기 전용 원본4개 SHA-256 재확인. qa_assets.json.
- HTML Node VM 모형 검사46/46 통과: 내장 PNG13개, 문법·그림 칸 경계, 비교 캔버스, 재생/정지/이전/다음/순환, 점화 시간 경계와1초 지연, 배율·배경·시점·접지 표시. qa_html.json.
- 기존 K-048 모아보기·설치/반동 대포·성벽 포좌·대포병 정지/달리기/점화·총병/궁수 시트를 직접 열어 봤다. 최종 모아보기, 보행3칸째, 점화 접점 PNG도 직접 열어 확인했다.
- 원본 폴더 HEAD는 develop, 이 카드 HEAD는 card/K-052임을 Git 참조 파일 읽기로 확인했다. 브랜치 변경·커밋 등 Git 쓰기는 하지 않았다.

## 확실하지 않은 것·남은 것

- 실제 브라우저 목록이 비었고 열기는 “Browser is not available: iab”로 실패했다. CSS 배치·스크롤·마우스 조작·실시간 재생 육안 검증은 미수행이다. 모형 검사를 실제 브라우저 검사로 간주하지 않았다.
- 기존 그림과 같은 사람·재질로 읽히는지, 작은 눈과 명암 단계·머리 비율이 적절한지는 사용자 외형 검토가 남는다. 팔레트 일치만으로 감성 일치를 단정하지 않았다.
- 정면 원본으로 환산한101px 바퀴와 옆모습 포가 꼬리의 무게감, 긴 포신 비례, 바퀴 옆 밀기 병사의 겹침, 손 파지,6→1 보행 연결, 높인 도화선 위치는 검토 질문이다.
- 고정 시점은 세 주기 후 화면 위치를 초기화한다. 접지 보장은 칸 경계에서 그림과 이동을 같이 갱신하는 검토 재생에 한정된다. 게임의 연속 이동 처리는 구현하지 않았다.
- Unity 실행·Play·게임 캡처·Game/ 수정·협업 live 기록·기존 HTML 수정은 하지 않았다. Git 실행 파일이 PATH에 없어 git status 조회는 미수행이다. 사용자 외형 확정 후 Claude의 연결과 실제 게임 비교가 남는다.

## 바꾼 파일 목록

모두 신규 파일이다. 지정 자료 폴더60개, 지정 검토 HTML1개, 이 결과서1개다. 상세 규격과 파일 목록은 manifest.json에도 있다.

- `_HTML/4_그림_캐릭터/codex_K-052_대포끌기_3차_v1.html`
- `CARD_RESULT.md`

아래60개는 모두 `Docs/References/codex_k052_v1/` 아래다.

- `PixelTools.cs`
- `README.md`
- `analyze.ps1`
- `build.ps1`
- `build_crew.ps1`
- `build_ignite.ps1`
- `build_review.mjs`
- `cannon_body.png`
- `cannon_chassis.png`
- `cannon_source.png`
- `cannon_wheel.png`
- `compose_verify.ps1`
- `contact_review.png`
- `crew_ignite6.png`
- `crew_ignite_1.png`
- `crew_ignite_2.png`
- `crew_ignite_3.png`
- `crew_ignite_4.png`
- `crew_ignite_5.png`
- `crew_ignite_6.png`
- `crew_ignite_source.png`
- `crew_pull6.png`
- `crew_pull_1.png`
- `crew_pull_2.png`
- `crew_pull_3.png`
- `crew_pull_4.png`
- `crew_pull_5.png`
- `crew_pull_6.png`
- `crew_pull_source.png`
- `crew_push6.png`
- `crew_push_1.png`
- `crew_push_2.png`
- `crew_push_3.png`
- `crew_push_4.png`
- `crew_push_5.png`
- `crew_push_6.png`
- `crew_push_source.png`
- `crew_source.png`
- `haul_1.png`
- `haul_2.png`
- `haul_3.png`
- `haul_4.png`
- `haul_5.png`
- `haul_6.png`
- `ignite_contact_review.png`
- `ignite_measurements.json`
- `outline_measurements.json`
- `palette.json`
- `prepare.ps1`
- `prompts.json`
- `qa_assets.json`
- `qa_html.json`
- `reference_hashes.json`
- `reference_wheel_roi.png`
- `review_template.html`
- `selected_palette.json`
- `used_palette.json`
- `verify_review.mjs`
- `walk_measurements.json`
- `manifest.json`
