# K-034 결과 — 캐릭터 그림 크기·모션 (사용자 시험 플레이 2차 피드백)

유니티는 실행하지 않았다(컴파일·화면 확인 못 함). 메인에서 시험이 필요하다.

## 한 일

| 사용자 의견 | 원인 | 고친 것 |
|---|---|---|
| 1. 궁수 공격할 때 크기 들쭉날쭉 | 칸(256×384)은 같지만 시트마다 칸 안 인물 크기가 다르다. 서 있기 300px, 궁수 사격 249px(0.83배), 총병 사격 265px | 시트별 인물 키 표 → 셰이더 `_SheetScale`(= 기준 키 ÷ 그 시트 인물 키)로 어느 시트로 그려도 같은 크기 |
| 2. 전령이 주인공보다 작다 | 달리기 시트 인물이 서 있기의 약 0.75~0.87배로 작게 그려져 있다(환산 227~262px) | 같은 표로 달리기 시트를 키움 (궁수 ×1.70, 총병 ×1.47, 대포병 ×1.54 — 병사 기준 배율) |
| 13. 크기 격자화 | — | `Tools/SheetMeasure/measure-sheets.ps1`이 모든 캐릭터 시트를 재서 표(`sheet-heights.md`)와 게임용 C# 표(`PerfLabSheetHeights.cs`)를 만든다. **개체 크기(m) = 인물 실제 키**: 병사 0.35m, 주인공·마법사 0.375m. 적은 각자 기본 시트 인물 키가 기준(기본 시트 배율 1 → 크기 그대로, 공격·쓰러짐·사다리 시트만 기본 시트에 맞춤) |
| 3. 위에서 보니 납작 | 세로 판을 내려다보면 cos(내려보는 각)만큼 짧아 보인다. 프리셋 A에서 주인공 약 28°(0.88배), 성벽 아래 적은 더 심함 | 셰이더 `_ViewStretch`: 개체마다 카메라에서 몸 가운데를 내려보는 각 φ로 높이를 1/cos φ 늘림(최대 1.4배). 발(기준점) 고정이라 발 위치 그대로, 그림자 판은 늘리지 않음(실제 키), 그림자 받는 점도 발 기준 그대로. 캐릭터 시트에만 켬(성벽 난간·대포·소품·투사체·사다리는 그대로) |
| 5. 총 쏘는 사람 모션 없음 | 시트 연결은 정상(K-032에서 총병도 연결됨, 코드 길도 궁수와 같음). 총은 간격 2.5초·사거리 30m라, 쏜 순간 앞뒤 0.75초만 사격 칸이고 나머지 1.75초는 서 있기 그림 → 거의 안 보임. 게다가 사격 시트 인물이 0.88배로 작아 깜빡이듯 보였을 것 | 계속 쏘는 중이면 쏜 뒤 다음 겨누기 전까지 마지막 칸(재장전)을 유지 → 총병은 겨누기→쏘기→재장전이 이어져 보인다(궁수도 같은 규칙). 크기는 1번으로 맞춤 |
| 9. 대포병이 다른 병사처럼 빨리 달림 | 대포 분대 맨 앞 병사(대포를 쏘는 병사)는 무기가 있어 전령·우회 보고로 뽑힐 수 있다. 속도는 모두 같았다 | 대포 운용병 달리기 속도 0.6배(`BattleSim.CannonCrewRunSpeedRatio`), 달리기 그림 재생도 0.6배로 느리게. 전령·우회 보고(둘 중 느린 쪽)·묶음 이동 모두 적용 |

**대포 끌기 그림 필요** — 지금은 있는 대포병 달리기 그림(`Run/cannon_crew_run.png`)을 느리게 재생할 뿐이다. Codex 사용 한도가 풀리면 '대포(또는 장비)를 끌며 걷는 옆모습 4칸, 칸 256×384·발 (128, 352), 왼쪽 보기'를 요청해 `unit_cannon_crew.runSheet`에 바꿔 끼우면 된다(코드 수정 없음 — 측정 도구를 다시 돌려 표만 갱신).

## 바꾼 파일

- `Game/Assets/_Project/Combat/PerfLab/PerfLabSprite.shader` — `_SheetScale`, `_ViewStretch`, `_MaxViewStretch`(1.4). `Billboard`에 높이 배율 인자 추가(그림자 패스는 1)
- `Game/Assets/_Project/Combat/PerfLab/PerfLabArt.cs` — `FigureReferencePx`(384), `DefaultFigurePx`(300, 표에 없는 시트), `FigurePx(sheet)`, `SheetScale(sheet, referencePx)`
- `Game/Assets/_Project/Combat/PerfLab/PerfLabSheetHeights.cs` (+ .meta, 새 파일, 자동 생성) — 시트 이름 → 기준 인물 키(px)
- `Game/Assets/_Project/Combat/Battle/BattleView.cs` — 캐릭터 그리기 부분만: `Apply`에 배율·납작함 보정 인자, `ApplyFigure`, `AddPose`에 기준 키. 적·병사·달리기·주인공(걷기·서 있기·손짓)·마법사(정적·시전)에 연결. 투사체·효과·지도는 안 건드림
- `Game/Assets/_Project/Combat/Battle/BattleSim.Runners.cs` — 병사별 달리기 속도 배율 `sRunSpeed`, `RunSpeedRatio`, 달리기 그림 재생 속도
- `Game/Assets/_Project/Combat/Battle/BattleSim.Poses.cs` — 사격 재장전 칸 유지 (1분기 추가)
- `Game/Assets/_Project/Combat/Battle/BattleSim.Groups.cs` — 속도 한 줄 (`UpdateGroupMoves`)
- `Game/Assets/_Project/Combat/Battle/BattleSim.Commands.cs` — 속도 한 줄 (`UpdateMessengers`) ※ 카드 범위 밖이지만 전령 속도가 여기서 계산됨
- `Game/Assets/_Project/Combat/Battle/BattleSim.Flank.cs` — 속도 한 줄 (`UpdateRunners`) ※ 같은 이유
- `Tools/SheetMeasure/measure-sheets.ps1`, `Tools/SheetMeasure/sheet-heights.md` — 측정 도구와 표

UnitDef/EnemyDef와 데이터 .asset은 바꾸지 않았다(표를 PerfLabArt 쪽에 둠 — 텍스처 이름으로 찾음).

## 시트별 인물 키 표

알파 128 이상(셰이더 잘라내기 0.5와 같음), 칸 위에서 잰 px. 발 줄은 모든 시트 0번 칸이 351(= 기준점 352 바로 위)로 규격과 맞아 기준점은 바꾸지 않았다.
기준 키: 서 있기류는 0번 칸 키, 쓰러짐·달리기는 자세가 낮아 키로 못 재므로 '짝 서 있기 시트 키 × √(넓이 비)', 달리기는 옆모습 보정 ÷0.92(주인공 시트의 옆/뒤 넓이 비). 서 있기 그림 그대로인 사격 0번 칸으로 확인: 키 비(0.83·0.88)와 넓이 비(0.84·0.90)가 맞는다.

| 시트 | 0번 칸 키 | 기준 키 | 병사 기준 배율(384÷기준 키) | 적 배율(기본 시트 기준) |
|---|---|---|---|---|
| Static/archer_static | 300 | 300 | 1.28 | |
| Fire/archer_fire | 249 | 249 | 1.542 | |
| Run/archer_run | 197 | 226.5 | 1.695 | |
| Fall/archer_fall | 254 | 273.3 | 1.405 | |
| Static/gunner_static | 300 | 300 | 1.28 | |
| Fire/gunner_fire | 265 | 265 | 1.449 | |
| Run/gunner_run | 218 | 262.1 | 1.465 | |
| Fall/gunner_fall | 222 | 250.5 | 1.533 | |
| Static/cannon_crew_static | 300 | 300 | 1.28 | |
| Run/cannon_crew_run | 222 | 248.9 | 1.543 | |
| Fall/cannon_crew_fall | 261 | 253 | 1.518 | |
| Hero/hero_walk_run_grid | 297 | 297 | 1.293 | |
| Hero/hero_back_idle | 300 | 300 | 1.28 | |
| Hero/hero_command | 291 | 291 | 1.32 | |
| Static/wizard_back_static | 298 | 298 | 1.289 | |
| Static/wizard_cast | 257 | 257 | 1.494 | |
| Goblin/goblin_walk_attack4 (기본) | 240 | 240 | | 1 |
| Fall/goblin_fall4 | 211 | 221 | | 1.086 |
| Walk/ladder_goblin_walk (기본) | 272 | 272 | | 1 |
| Ladder/ladder_goblin_deploy4 | 242 | 242 | | 1.124 |
| Ladder/ladder_goblin_climb4 | 246 | 246 | | 1.106 |
| Walk/ogre_walk (기본) | 233 | 233 | | 1 |
| Walk/ogre_attack4 | 201 | 201 | | 1.159 |
| Fall/ogre_fall4 | 187 | 206.8 | | 1.127 |

전체 표(칸 수·가운데값·최대·머리·발): `Tools/SheetMeasure/sheet-heights.md`. 다시 재기:

```bash
powershell -ExecutionPolicy Bypass -File Tools/SheetMeasure/measure-sheets.ps1
```

## 의심 가는 곳 (메인에서 확인 부탁)

1. **전체 크기가 커진다.** 지시대로 '인물 실제 키 = 병사 키 0.35m'로 맞췄더니 예전보다 병사·주인공·마법사가 1.28배 커진다(예전 인물 키는 0.35 × 300/384 = 0.27m였다). 납작함 보정까지 겹치면 주인공은 화면에서 약 1.45배. 너무 크면 `PerfLabArt.FigureReferencePx`를 300으로 바꾸면 서 있는 병사가 예전 화면 크기로 돌아가고 시트 간 맞춤은 그대로 남는다. 적은 크기 그대로.
2. 성벽 위 병사가 커져 난간·대포 그림(BattlePropView, BattleWorldView의 S=0.35)과 비율이 달라 보일 수 있다.
3. 납작함 보정(1/cos φ, 최대 1.4)은 셰이더에서 `_WorldSpaceCameraPos`를 쓴다. 지도 모드는 다른 길이라 영향 없음. 그림자 판은 보정하지 않아 그림자 길이가 그림보다 조금 짧을 수 있다(실제 키 기준이라 의도). 성벽 바로 아래 적은 1.4배로 늘어나 길쭉해 보일 수 있다 → `_MaxViewStretch`로 조절.
4. 쓰러짐·달리기 기준 키는 넓이로 환산한 추정값이다. 화면에서 달리는 병사가 서 있는 병사보다 크거나 작아 보이면 `measure-sheets.ps1`의 `$SideFactor`(0.92)나 표 값을 조정.
5. 총병 모션 원인은 코드로 추정했다(연결 길은 궁수와 같음). 사거리 30m라 적이 가까이 와야 쏘므로, 시험할 때 적이 성벽 30m 안에 들어온 뒤 보면 된다. 궁수도 이제 쏘는 사이 재장전 칸(화살통에 손)을 유지한다.
6. 대포병 속도: 대포 분대는 지금 묶음 이동이 막혀 있고(대포는 옮길 수 없음), 달리는 대포병은 전령·우회 보고로 뽑힌 '맨 앞 사수'뿐이다. 우회 보고는 두 명이 같은 거리를 재므로 대포병이 끼면 짝도 함께 느려진다. 전령 출발 알림의 '약 N초'는 대포병 배율을 반영하지 않는다.
7. `BattleSim.Commands.cs`·`BattleSim.Flank.cs`는 카드 범위 밖인데 속도 한 줄씩 고쳤다(전령·우회 보고 이동 계산이 거기 있음). 조정 담당이 같은 줄을 고쳤다면 합칠 때 확인.
8. ROADMAP md·html은 카드 가지에서 고치지 않았다(통합 때 조정 담당이 갱신).
