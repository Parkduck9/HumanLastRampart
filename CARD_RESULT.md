# 카드 K-059 결과

작성: Codex · 2026-10-18

## 상태

**UI 키트와 스토리 그림 시안 저장, 사용자 검토 대기. 초상 광륜 보완·글꼴 견본·실브라우저 확인은 미완료.** D-064 사용자 확정 전 자료다. 슬롭 증상 30개 전체 합격이나 게임 연결 완료로 기록하지 않는다.

## 한 일

1. 지정된 K-047 UI 요청·슬롭 정의와 스토리 요청을 읽었다. 원본 폴더 HEAD는 develop, 이 작업 폴더 HEAD는 card/K-059임을 파일로 확인했다. Git 실행 파일은 PATH에 없어 명령으로 상태 검사는 하지 못했다.
2. UI 키트 **36종 전부를 먼저** 요청 이름·크기·경계에 맞춰 만들었다. 양피지·나무·황동, 19개 흰 아이콘, 도장·가장자리 빛을 포함한다.
3. 스토리 부품 10종, 장교·마법사·궁수 각 4표정 시트 3종, 해 질 녘·밤 성벽 배경 2종을 만들었다. 납품 후보 PNG는 총 **51종**이다.
4. 검토 HTML에 작은 창·큰 창, 판 선택, 나무·황동 단추의 보통/올림/누름, 9칸 경계 켜기/끄기, 실제 3배 확대 원본, 말풍선 예시, 대사·해설·자동 넘김, 초상 4표정 비교를 넣었다. 모든 그림을 내장해 서버 없이 열 수 있다.
5. 초상·배경은 **내장 imagegen**으로 생성했다. 원안 5장·광륜 제거 재시도 2장·실제 프롬프트를 보존했다. UI는 정확한 정수 화소·경계용 코드 원본으로 제작했다.
6. README·manifest에 경계, 시트 순서, 초상 내용 영역, 복사 구분, 글꼴 후보와 공식 출처를 기록했다.

## 바꾼 파일 목록

기존 파일 수정은 없다. 아래 지정된 신규 산출물만 작성했다.

- `_HTML/6_그림_화면/codex_K-059_UI키트_스토리_v1.html`
- `CARD_RESULT.md` (이 보고서)
- 다음 표의 51종은 모두 `Docs/References/codex_k059_v1/` 아래에 있다.

| 파일 | 원본 크기 | 구분 |
|---|---|---|
| panel_parchment.png | 32×32 | UI |
| panel_wood.png | 32×32 | UI |
| panel_band.png | 32×32 | UI |
| plate.png | 16×16 | UI |
| slip.png | 20×14 | UI |
| slip_danger.png | 20×14 | UI |
| bubble.png | 32×32 | UI |
| bubble_tail.png | 12×9 | UI |
| button_wood.png | 12×12 | UI |
| button_wood_down.png | 12×12 | UI |
| button_brass.png | 12×12 | UI |
| button_brass_down.png | 12×12 | UI |
| frame_glow.png | 12×12 | UI |
| keycap.png | 10×11 | UI |
| bar_track.png | 8×8 | UI |
| icon_close.png | 11×11 | UI |
| icon_up.png | 11×11 | UI |
| icon_down.png | 11×11 | UI |
| icon_play.png | 11×11 | UI |
| icon_pause.png | 11×11 | UI |
| icon_speed1.png | 6×11 | UI |
| icon_speed2.png | 12×11 | UI |
| icon_speed4.png | 15×11 | UI |
| icon_hero.png | 11×11 | UI |
| icon_north.png | 11×11 | UI |
| icon_check.png | 11×11 | UI |
| icon_flag.png | 11×11 | UI |
| icon_auto.png | 11×11 | UI |
| icon_idle.png | 11×11 | UI |
| icon_special.png | 11×11 | UI |
| icon_minus.png | 11×11 | UI |
| icon_plus.png | 11×11 | UI |
| icon_right.png | 11×11 | UI |
| icon_warn.png | 11×11 | UI |
| seal.png | 32×32 | UI |
| vignette.png | 64×36 | UI |
| story_box.png | 96×40 | 스토리 부품 |
| story_nameplate.png | 48×18 | 스토리 부품 |
| story_next_0.png | 7×5 | 스토리 부품 |
| story_next_1.png | 7×5 | 스토리 부품 |
| story_next_2.png | 7×5 | 스토리 부품 |
| story_next_3.png | 7×5 | 스토리 부품 |
| story_button.png | 40×16 | 스토리 부품 |
| story_button_hover.png | 40×16 | 스토리 부품 |
| story_button_on.png | 40×16 | 스토리 부품 |
| story_fade_edge.png | 64×64 | 스토리 부품 |
| story_portrait_hero.png | 1024×384 | 초상 |
| story_portrait_wizard.png | 1024×384 | 초상 |
| story_portrait_archer.png | 1024×384 | 초상 |
| story_bg_rampart_dusk.png | 640×360 | 배경 |
| story_bg_rampart_night.png | 640×360 | 배경 |

같은 폴더의 제작·인계 자료 18개:

- `README.md`, `manifest.json`, `prompts.json`, `qa_assets.json`, `qa_review_logic.json`
- `build_ui.ps1`, `prepare_story.ps1`, `build_review.ps1`, `verify_assets.ps1`, `review_template.html`
- `source_hero.png`, `source_wizard.png`, `source_archer.png`, `source_bg_dusk.png`, `source_bg_night.png`
- `source_hero_clean_attempt.png`, `source_wizard_clean_attempt.png` (미채택 편집 재시도)
- `proof_ui_3x.png` (자체 확대 도판, 브라우저·유니티 캡처 아님)

총 신규 파일 71개: 결과 폴더 69개 + 검토 HTML + 이 보고서. `source_*`·`proof_*`·제작 스크립트는 게임으로 복사할 납품 PNG가 아니다. 납품 대상은 manifest의 51종이다.

## 확인한 것

- 파일 검사: PNG 51종 이름·요청 크기, 36종 UI 누락 없음, 10종 스토리 부품, RGBA 형식, 9칸 가운데 양수.
- 판·단추·아이콘 등 해당 그림의 0/255 알파, 해당 판의 지정 팔레트 검사 통과. 빛·도장·가장자리 효과는 용도상 반투명을 허용한다. 초상 이진 알파 합격을 주장하지 않는다.
- 원안·최종 초상·배경과 UI 3배 도판을 직접 열어 확인했다. 양피지 반복 무늬를 줄이고 작은 빠르기 아이콘의 외곽선 여유를 보완했다.
- JavaScript 구문 검사 통과. 가상 DOM·Canvas 스텁에서 51개 데이터, 모든 9칸 소스 경계, 화자·배경·표정·대사·자동 넘김 연산을 확인했다. **실브라우저 픽셀 렌더링·클릭·글꼴 측정 시험은 아니다.**
- 최종 HTML 내장 PNG 51종의 SHA-256이 manifest와 일치하고 외부 실행 리소스·미치환 자리표시자가 없음을 확인했다.
- 유니티 열기·실행·Play·실제 게임 캡처·Git 쓰기·브랜치 전환을 하지 않았다. Game/·요청 원본 문서·협업 기록·상대 HTML·공용 목록에 쓰지 않았다.

## 확실하지 않은 것·남은 것

1. **초상 바깥의 연한 반투명 광륜 보완 필요.** 장교와 마법사에 imagegen 편집을 각각 1회 추가 요청했지만 광륜이 남았다. 해결로 기록하지 않고 첫 원안의 알파를 보존했다. 초상 3종은 게임 즉시 채택 후보가 아니라 외형 검토용이다.
2. 초상은 256×384 칸마다 x=64, y=160, 128×192 내용 영역이다. 기존 Story 배치와의 크기·위치 연결은 미검증이며 README 기준으로 Claude가 확인해야 한다. K-056 전신 비율을 확정하지 않았다.
3. 기존 주인공과 같은 사람으로 읽히는지, 초상 도트 밀도가 장면과 맞는지는 사용자 검토가 필요하다. 궁수는 기존 뒷모습만 있어 얼굴을 새로 제안했다.
4. 요청의 글꼴 후보 2종(갈무리11·Neo둥근모)과 공식 OFL 1.1 출처·동봉 조건은 정리했다. 네트워크 제한으로 파일 다운로드가 실패해 **후보 실물 견본 사진은 미완료**다. 검토 페이지의 바탕/맑은 고딕은 배치 확인용이며 S-06 해결 완료가 아니다.
5. 브라우저 제어에 연결된 브라우저가 없어 실제 HTML 클릭·스크롤·줄바꿈·픽셀 렌더링 확인은 못 했다. 유니티와 실제 게임도 미검증이다.
6. Claude가 사용자 선택 뒤 UI 36종을 UiKit으로 복사하고, 스토리 이름·경계·초상 여백을 연결해야 한다. 큰 판의 실제 반복·전환·상태 피드백·소리·게임 캡처 비교가 남았다. `0_전체목록.html` 동기화도 Claude 담당이며 이 카드에서는 수정하지 않았다.

