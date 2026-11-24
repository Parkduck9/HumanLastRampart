# 카드 K-056 결과

## 한 일
- 궁수 9칸(후면 서기 1·후면 걷기 4·사격 4), 주인공 6칸(후면 서기 1·왼쪽 달리기 4·정면 서기 1)의 약 3등신 시험 그림을 제작했다. 사용자 확정 전 자료다.
- 내장 image_gen 4회로 원안과 달리기 보완본을 만들고 원안·프롬프트를 보관했다. 최종 그림은 인물별 기존 시트에서 추출한 실제 RGB만 사용한다.
- 원본 칸 64×96·발 (32,88), 게임용 칸 256×384·발 (128,352). 인물 키는 원본 48점이며 게임용은 정확한 최근접 4배다.
- 궁수 사격 4칸과 기존 6칸 대응본을 함께 제공했다. 주인공은 현재 Game 시트의 달리기 2행을 따른 위치 검토본과 개별 동작 시트를 제공했다. 미제작 걷기 칸이 비어 있는 위치 검토본은 전체 교체용이 아니다.
- 오프라인 HTML에 기존/새 그림/3등신 비율 도식의 동일 키 비교, 화면 높이 13% 비교, 동작 선택·재생·정지·이전·다음·속도 조절을 넣었다.
- 칸 순서, 출처, 크기 차이, 확인 결과와 한계를 README 및 manifest에 기록했다.

## 확인한 것
- 최종 전체 시트 PNG를 직접 열어 외형과 큰 잘림을 확인했다.
- PNG 규격 검사 137개 통과: 원본 색 포함, 허용 팔레트만 사용, 반투명 없음, 칸 크기, 최근접 4배 픽셀 일치, 키 48점·발끝·여백, 동작 4칸 각각 다름, 사격 6칸 대응 순서.
- Node의 최소 화면 모형에서 검토 페이지 로직 13개 통과: 19개 내장 이미지 로드 정보, 재생·정지·수동 이동·끝/첫 순환·인물 및 동작 전환·비율선 전환. 실제 브라우저 시험으로 기록하지 않았다.
- Git 읽기 전용 diff로 Game/ 변경 없음(종료 코드 0) 확인. Git 쓰기·커밋·브랜치 변경 없음. Unity 실행 없음. 다른 문서·live 기록 수정 없음.
- 시작할 때 이미 존재한 CARD_CODEX_LOG.jsonl은 작업 산출물에 포함하지 않았고 수정하지 않았다.

## 확실하지 않은 것·남은 것
- 실제 참고 사진은 카드 입력에 없었다. 사용자 설명의 머리 1/3·전체 48점 조건으로 제작했다. 페이지 오른쪽은 비율 도식이며 실제 참고 사진이 아니다.
- 현재 저장소의 기존 정적 인물은 불투명 영역 키가 약 300점이고 새 게임용은 192점이다. 같은 게임 배율에서는 새 그림이 약 64% 크기로 보인다. HTML은 같은 키로 정규화했으며 게임에서 같은 키를 맞추는 배율은 Claude 후속 작업이다.
- 연결된 브라우저가 없어 HTML의 실제 화면 렌더링·실제 버튼 클릭·모바일 줄바꿈·캡처는 미검증이다. Node 검사는 화면 모형의 로직 검사다.
- 달리기의 팔 교대는 보완했으나 다리 위상·공중/접지 구분이 약할 수 있다. 완성 애니메이션 승인으로 간주하지 않는다. 기존 얼굴·머리·제복과 같은 사람인지 및 활/화살통 일관성은 사용자 검토가 필요하다.
- 노멀맵, 미요청 방향·걷기·나머지 동작, Unity 가져오기·실제 UV·프레임 발사 시점·게임 조명·발 접지·카메라 배율은 이번에 제작/검증하지 않았다.
- 질문/결정 대기: 이 비율·점 크기로 전체 교체를 진행할지, 두 인물의 외형·동작부터 추가 보완할지. 사용자 확정 전 전체 교체나 Game 연결은 하지 않는다.

## 바꾼 파일 목록
모두 이번 카드의 신규 파일이다. 지정된 자료 폴더·검토 HTML·이 결과 파일만 작성했다.
- `Docs/References/codex_k056_v1/all_frames_256.png`
- `Docs/References/codex_k056_v1/all_frames_64.png`
- `Docs/References/codex_k056_v1/archer_back_idle_256.png`
- `Docs/References/codex_k056_v1/archer_back_idle_64.png`
- `Docs/References/codex_k056_v1/archer_back_walk4_256.png`
- `Docs/References/codex_k056_v1/archer_back_walk4_64.png`
- `Docs/References/codex_k056_v1/archer_fire4_256.png`
- `Docs/References/codex_k056_v1/archer_fire4_64.png`
- `Docs/References/codex_k056_v1/archer_fire6_compat_256.png`
- `Docs/References/codex_k056_v1/archer_fire6_compat_64.png`
- `Docs/References/codex_k056_v1/archer_palette.txt`
- `Docs/References/codex_k056_v1/build_assets.ps1`
- `Docs/References/codex_k056_v1/build_review.py`
- `Docs/References/codex_k056_v1/check_review.cjs`
- `Docs/References/codex_k056_v1/frame_00_256.png`
- `Docs/References/codex_k056_v1/frame_00_64.png`
- `Docs/References/codex_k056_v1/frame_01_256.png`
- `Docs/References/codex_k056_v1/frame_01_64.png`
- `Docs/References/codex_k056_v1/frame_02_256.png`
- `Docs/References/codex_k056_v1/frame_02_64.png`
- `Docs/References/codex_k056_v1/frame_03_256.png`
- `Docs/References/codex_k056_v1/frame_03_64.png`
- `Docs/References/codex_k056_v1/frame_04_256.png`
- `Docs/References/codex_k056_v1/frame_04_64.png`
- `Docs/References/codex_k056_v1/frame_05_256.png`
- `Docs/References/codex_k056_v1/frame_05_64.png`
- `Docs/References/codex_k056_v1/frame_06_256.png`
- `Docs/References/codex_k056_v1/frame_06_64.png`
- `Docs/References/codex_k056_v1/frame_07_256.png`
- `Docs/References/codex_k056_v1/frame_07_64.png`
- `Docs/References/codex_k056_v1/frame_08_256.png`
- `Docs/References/codex_k056_v1/frame_08_64.png`
- `Docs/References/codex_k056_v1/frame_09_256.png`
- `Docs/References/codex_k056_v1/frame_09_64.png`
- `Docs/References/codex_k056_v1/frame_10_256.png`
- `Docs/References/codex_k056_v1/frame_10_64.png`
- `Docs/References/codex_k056_v1/frame_11_256.png`
- `Docs/References/codex_k056_v1/frame_11_64.png`
- `Docs/References/codex_k056_v1/frame_12_256.png`
- `Docs/References/codex_k056_v1/frame_12_64.png`
- `Docs/References/codex_k056_v1/frame_13_256.png`
- `Docs/References/codex_k056_v1/frame_13_64.png`
- `Docs/References/codex_k056_v1/frame_14_256.png`
- `Docs/References/codex_k056_v1/frame_14_64.png`
- `Docs/References/codex_k056_v1/generated.png`
- `Docs/References/codex_k056_v1/generated_run.png`
- `Docs/References/codex_k056_v1/generated_run_v2.png`
- `Docs/References/codex_k056_v1/generated_v2.png`
- `Docs/References/codex_k056_v1/hero_back_idle_256.png`
- `Docs/References/codex_k056_v1/hero_back_idle_64.png`
- `Docs/References/codex_k056_v1/hero_front_idle_256.png`
- `Docs/References/codex_k056_v1/hero_front_idle_64.png`
- `Docs/References/codex_k056_v1/hero_layout_preview_256.png`
- `Docs/References/codex_k056_v1/hero_layout_preview_64.png`
- `Docs/References/codex_k056_v1/hero_palette.txt`
- `Docs/References/codex_k056_v1/hero_side_run4_256.png`
- `Docs/References/codex_k056_v1/hero_side_run4_64.png`
- `Docs/References/codex_k056_v1/manifest.json`
- `Docs/References/codex_k056_v1/prompts.json`
- `Docs/References/codex_k056_v1/prompts_run_revision.json`
- `Docs/References/codex_k056_v1/qa.json`
- `Docs/References/codex_k056_v1/qa_review_logic.json`
- `Docs/References/codex_k056_v1/README.md`
- `Docs/References/codex_k056_v1/validate.py`
- `_HTML/4_그림_캐릭터/codex_K-056_3등신시험_v1.html`
- `CARD_RESULT.md`
