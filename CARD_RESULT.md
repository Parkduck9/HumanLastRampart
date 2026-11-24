# K-057 결과 — 3등신 시험 그림(K-056) 바꿔 끼우기와 비교 사진 시험

작성 Claude · 2026-10-18 · 가지 card/K-057 · **유니티 실행·컴파일 안 함** (메인에서 PlayProbe `ProportionTrial` 실행 필요)

## 스위치
- `HumanLastRampart.Combat.Battle.ProportionTrial.Enabled` (static bool, **기본 꺼짐**)
- 켜면 **궁수(서기·사격)와 주인공(시트·서기)만** 시험 그림으로 그린다. 다른 병종·적·쓰러짐·달리기 그림은 그대로라 섞여 보이는 게 정상.
- 전투 중에 켜고 꺼도 다음 그리기부터 바뀐다. `BattleView.Draw`가 바뀐 것을 보고 해당 블록의 그림만 바꿔 끼운다(버퍼·칸 구간 그대로). 그래서 거의 멈춘 같은 순간에 old/new 사진을 찍는다.
- 꺼져 있으면 그림을 불러오지 않고 아무것도 바꾸지 않는다. 지금 게임과 같다. 매 프레임 bool 비교 하나만 더해진다.

## 그림 (원본 Docs는 그대로, Game 안에 사본)
`Art/Battle/Resources/BattleArt/ProportionTrial/` — .meta는 기존 캐릭터 그림과 같은 설정(점 필터, 압축 없음, 밉맵 없음). 자동 가져오기 규칙(`Combat/Editor/*Import.cs`)이 닿지 않는 폴더다.

| 파일 | 출처 | 쓰는 곳 |
|---|---|---|
| `trial3_archer_static.png` (256×384) | `archer_back_idle_256` | 궁수 서기 |
| `trial3_archer_fire.png` (4칸) | `archer_fire4_256` — 놓기 = 4번째 칸, 기존 사격 규칙(놓기 칸 3)과 같은 자리 | 궁수 사격 |
| `trial3_hero_grid.png` (4×4) | 기존 주인공 시트와 같은 행 순서로 합성: 0행 뒷모습 서기×4, 1행(옆 걷기)·2행(옆 달리기) 달리기 4칸, 3행 앞모습 서기×4 | 주인공 시트 |
| `trial3_hero_back_idle.png` | `hero_back_idle_256` | 주인공 서기(D-066) |

- 시안에 없는 칸(뒷모습·앞모습 걷기, 옆 걷기)은 서기·달리기로 채웠다. 걷기는 발이 움직이지 않는 게 정상이다.
- 6칸 대응본(`archer_fire6_compat`)은 놓기가 5번째 칸이라 기존 놓기 칸(4번째)과 어긋나서 4칸본을 썼다. 사격 칸 수는 `BattleSim.SetFireSheetFrames`로 4칸에 맞춘다.
- 원래 노멀맵은 칸이 안 맞아서 시험 그림에는 붙이지 않는다(궁수·주인공 노멀 없음).

## 화면 키 맞추기
- 시험 그림 인물 키는 192px(48점 × 4), 기존 서기는 300px다.
- `PerfLabArt.FigurePx`가 이름 앞글자 `trial3_`를 알아보고 192px로 계산한다(`TrialSheetPrefix`·`TrialFigurePx`). 그래서 `SheetScale`이 384/192 = 2.0(기존 384/300 = 1.28)이 되고, 둘 다 같은 월드 키(병사 0.35m)로 선다.
- 자동 표 `PerfLabSheetHeights.cs`는 손대지 않았다.
- 칸은 같은 256×384이고 발 기준 (128, 352)도 같다.

## PlayProbe `ProportionTrial` (새 시험)
현실 난이도 1번 전투, 정면(북) 성벽, 카메라 A, 전투를 거의 멈춘 채 찍는다.

| 순서 | 사진 이름 |
|---|---|
| 낮 끔 → 켬 | `proportion_day_A_old`, `proportion_day_A_new` |
| 밤 끔 → 켬 | `proportion_night_A_old`, `proportion_night_A_new` |
| 주인공 오른쪽 달리기 끔 → 켬 | `proportion_hero_run_A_old`, `proportion_hero_run_A_new` |
| 서기 켬 → 끔 | `proportion_hero_idle_A_new`, `proportion_hero_idle_A_old` |

- 기록 파일은 `proportion_trial.txt`이고, 끝나면(실패해도) 스위치를 끈다.
- 사진 앞에는 다른 사진처럼 일련번호가 붙는다.

## 바꾼 파일
- `Combat/Battle/ProportionTrial.cs` (+ .meta, 새 파일) — 스위치·그림 불러오기
- `Combat/Battle/BattleView.cs` — 궁수·주인공 블록 기억, `ApplyProportionTrial`, Draw 첫 줄 확인
- `Combat/Battle/BattleSim.Poses.cs` — 시작 때 사격 칸 수(시험 켬이면 4), `SetFireSheetFrames`
- `Combat/PerfLab/PerfLabArt.cs` — `trial3_` 인물 키 192
- `Combat/_PlayProbe/PlayProbe.cs` — `ProportionTrial` 시험
- `Art/Battle/Resources/BattleArt/ProportionTrial/` 그림 4장 + .meta, 폴더 .meta

## 확실하지 않은 것
1. 컴파일을 못 돌렸다. PlayProbe의 시험 메서드 이름 `ProportionTrial`과 형식 `ProportionTrial`이 같은 이름이라, 시험 안에서는 형식을 전체 이름(`HumanLastRampart.Combat.Battle.ProportionTrial`)으로 썼다.
2. 궁수 사격 시험 그림은 왼쪽을 향한 뒷모습 사선이고(README), 기존 사격은 뒷모습이다. 방향이 달라 보일 수 있다.
3. 주인공을 좌우 뒤집는 오른쪽 달리기는 시험 그림도 뒤집어 그린다. 시안에 오른쪽 그림이 따로 없어서 기존과 같은 방식이다.
4. 사진 순간에 궁수가 사격 칸인지 서기 칸인지는 그때 쏘는 중인지에 달렸다. 80프레임(약 4초) 뒤에 찍으므로 정면 궁수 일부는 사격 중일 것으로 본다.
