# 화면 회귀 기준 사진 (W30)

- 시험: `PlayProbe.GoldenScreenshots` (PlayMode, `Combat/_PlayProbe/PlayProbe.Golden.cs`), 비교기: `GoldenImage.cs`.
- 8장: main_menu, story, day_wall_top, night_wall_beam, night_courtyard_tower, result, map, prep.
- 찍는 법: 960×540 렌더 텍스처, 씨앗 고정(TestSeedOverride), captureDeltaTime 1/20, 프레임 수로만 기다림, 전투 장면은 표시판(시계·HUD) 없이.
- 비교: 240×135로 줄여 화소별 비교. 채널 차이 24 초과 화소가 3% 넘거나 평균 오차 0.02 넘으면 실패.
- 기준 없이도 보는 검사: 거의 한 색(92% 초과), 마젠타(2% 초과, 셰이더 없음), 장면별 평균 밝기 범위.
- 실패하면 `C:\Unity\1\HLR-work\visual_diff\`(환경 변수 `HLR_VISUAL_DIFF`로 바꿈)에 `이름_actual/baseline/diff.png`를 남긴다.

## 기준을 새로 만들 때 (그림을 일부러 바꿨을 때만)

1. 바뀐 사진이 의도한 것인지 `visual_diff` 폴더에서 눈으로 확인한다.
2. 환경 변수 `HLR_UPDATE_BASELINES=1`을 켜고 `GoldenScreenshots`를 한 번 돌린다 (이 폴더의 PNG가 다시 쓰인다).
3. 환경 변수를 끄고 한 번 더 돌려 통과하는지 본다. 바뀐 PNG를 함께 커밋한다.

그래픽 장치가 없는 실행(`-nographics`)에서는 이 시험을 건너뛴다.
