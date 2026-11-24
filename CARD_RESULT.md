# K-064 작업 결과

## 한 일

- 주인공 6판 채택 문서를 읽고 고블린·사다리 고블린(빈손/운반)·오우거의 앞/옆 서기 총8장을 내장 이미지 생성 도구로 제작했다. 주인공은 새로 그리지 않았다.
- 작은 머리·작은 눈·또렷한 몸통/다리 방향으로 보정했고 기존 피부/머리/복장/무기를 참조했다. 기존 시트의 실제 RGB를 추출해 최종 색을 제한했다.
- 1배8장/최근접4배8장, 원안·프롬프트·팔레트·변환 좌표·검사 기록·README를 보관했다. 고블린 칸56×112/발y104/중심x28, 오우거112×160/발y152/중심x56이다.
- 지정 검토 HTML에 6판·병사 나란히1/4배, 전후, 코드 크기 비율, 화면높이13% 도식 비교를 넣었다. 모든 리소스를 내장했다.
- Game/은 읽기만 했다. Unity 실행, Git 쓰기, 협업 기록 및 기존 HTML 수정은 하지 않았다.

## 확인한 것

- 최종8장과4배8장의 크기·발선·알파0/255. 반투명 픽셀0개.
- 최종8장 모두 추출 팔레트 위반0개, 원본8장과 확대8장의 8쌍에서 모든 확대 픽셀 불일치0개.
- PNG 확대본 직접 확인. 오우거 옆모습에 섞인 앞모습 조각을 발견해 분리 경계를 고치고 재확인했다.
- HTML 자바스크립트 구문/간이 DOM 초기화/4배·기존 보기 함수 실행 통과. 생성 이미지 참조32개 미정의0, 외부리소스 없음. 실제 브라우저 렌더와는 구분한다.
- 기본 시트 asset/meta GUID·PerfLabSheetHeights·표시 코드를 대조했다. 성벽 앞 병사0.35m 대비 고블린0.707, 사다리병0.801, 오우거1.144배. 계산·출처는 README와 HTML에 적었다.

## 확실하지 않은 것·남은 것

- 연결된 브라우저가 없어 실제 HTML 렌더/캡처는 미수행했다. Unity Play와 실제 게임 화면 비교도 미수행했다. 화면높이13% 비교는 병사94점/화면720점의 도식이며 캡처가 아니다.
- 사용자 외형 채택 대기. 작은 눈/송곳니의 실게임 가독성, 고블린의 구부정함, 사다리 손 접촉·피벗, 빈손/운반 모습의 세부 복장·머리 점 차이는 확인이 필요하다.
- 외곽선1점/재질3~4단은 제작 방향으로 적용했으나 모든 국소 영역을 수작업 도트로 확정한 것은 아니다.
- 사다리 운반은 사다리 포함98점/몸 약95~96점. 큰 적은128점으로 제작하고 게임 비율 비교에서 별도 표시 크기를 적용했다.
- 기존 빈손 사다리병·기존 옆 서기 원화가 없어 해당 기존 모습을 만들어 비교하지 않았다. 기존 고블린 전후는 걷기 첫 칸이다.
- 동작 시트·노멀맵·게임 연결은 후속이다. 정지 복제를 동작 완료로 기록하지 않았다. Claude가 사용자 확인 후 D-074 연결을 진행해야 한다.
- 별도 질문은 없다.

## 바꾼 파일 목록

- CARD_RESULT.md
- _HTML/4_그림_캐릭터/codex_K-064_적6판방향_v1.html
- Docs/References/codex_k064_v1/build.ps1
- Docs/References/codex_k064_v1/build_review.ps1
- Docs/References/codex_k064_v1/goblin_before.png
- Docs/References/codex_k064_v1/goblin_before_4x.png
- Docs/References/codex_k064_v1/goblin_corrected.png
- Docs/References/codex_k064_v1/goblin_front.png
- Docs/References/codex_k064_v1/goblin_front_4x.png
- Docs/References/codex_k064_v1/goblin_generated.png
- Docs/References/codex_k064_v1/goblin_selected.png
- Docs/References/codex_k064_v1/goblin_side.png
- Docs/References/codex_k064_v1/goblin_side_4x.png
- Docs/References/codex_k064_v1/hero_v6_reference.png
- Docs/References/codex_k064_v1/ladder_carry_before.png
- Docs/References/codex_k064_v1/ladder_carry_before_4x.png
- Docs/References/codex_k064_v1/ladder_carry_front.png
- Docs/References/codex_k064_v1/ladder_carry_front_4x.png
- Docs/References/codex_k064_v1/ladder_carry_generated.png
- Docs/References/codex_k064_v1/ladder_carry_side.png
- Docs/References/codex_k064_v1/ladder_carry_side_4x.png
- Docs/References/codex_k064_v1/ladder_empty_before.png
- Docs/References/codex_k064_v1/ladder_empty_before_4x.png
- Docs/References/codex_k064_v1/ladder_empty_corrected.png
- Docs/References/codex_k064_v1/ladder_empty_front.png
- Docs/References/codex_k064_v1/ladder_empty_front_4x.png
- Docs/References/codex_k064_v1/ladder_empty_generated.png
- Docs/References/codex_k064_v1/ladder_empty_side.png
- Docs/References/codex_k064_v1/ladder_empty_side_4x.png
- Docs/References/codex_k064_v1/manifest.json
- Docs/References/codex_k064_v1/ogre_before.png
- Docs/References/codex_k064_v1/ogre_before_4x.png
- Docs/References/codex_k064_v1/ogre_front.png
- Docs/References/codex_k064_v1/ogre_front_4x.png
- Docs/References/codex_k064_v1/ogre_generated.png
- Docs/References/codex_k064_v1/ogre_side.png
- Docs/References/codex_k064_v1/ogre_side_4x.png
- Docs/References/codex_k064_v1/palettes.json
- Docs/References/codex_k064_v1/prompts.json
- Docs/References/codex_k064_v1/README.md
- Docs/References/codex_k064_v1/soldier_reference.png
- Docs/References/codex_k064_v1/soldier_reference_4x.png
- Docs/References/codex_k064_v1/validation.txt
