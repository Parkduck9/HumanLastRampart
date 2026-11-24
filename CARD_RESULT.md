# K-039 결과 — 병사 자리·대포병 (사용자 시험 플레이 3차 피드백)

유니티는 실행하지 않았다(컴파일·화면·시험 확인 못 함). 대포 분대 인원이 바뀌어 난이도가 달라지므로 SimProbe 측정은 조정 담당이 한다.

## 한 일

| 사용자 의견 | 원인 | 고친 것 |
|---|---|---|
| 1. 궁수 위치 계속 겹침 | 0.45m 간격이 문제가 아니었다(그림 폭은 궁수 0.19m·총병 0.23m). 분대가 자기 구간(양옆 대포 지점과의 가운데까지, 약 3.6m) 안에 **무조건 한 줄**이라, 궁수 20명이면 간격이 0.19m로 줄어 그림이 겹쳤다. K-034로 그림이 1.28배 커져 더 잘 보이게 됐다 | 최소 간격 `SoldierMinSpacing` 0.24m(가장 넓은 총병 그림 폭). 이보다 좁아지면 0.3m 안쪽에 줄을 더 세운다(최대 3줄: 0.5·0.8·1.1m — 달리는 길 1.4m는 비움). 앞줄부터 채우고 줄마다 가운데 맞춤. 3줄로도 넘치면 예전처럼 간격을 좁힌다(지점 정원 160명이면 이 경우) |
| 1. (묶음 이동 도착) | 도착 자리를 '가장 드문 곳'으로만 골라 꽉 찬 성벽에서는 겹쳤다 | `ChooseSpot`: 바깥 줄부터, 묶음 양끝에서 다른 병사까지 0.24m 이상 비는 자리가 있으면 거기, 없으면 한 줄 안쪽. 가는 중인 다른 묶음 자리(줄 포함)도 피한다 |
| 2. 대포병이 너무 많이 서 있음 | 대포마다 5명 | 사용자 결정대로 대포마다 2명: `campaign_start.asset` 대포 분대 16개 Count 5→2, `economy_preparation_1.asset` `cannonCrewPostCapacity` 5→2, `PreparationEconomyDef` 기본값 5→2. 2명은 대포 양옆(±0.45m)에 선다. 대포 위치는 지점 자리로 따로 둔다(`cannonSpots`, `GetCannon().Position`) — 예전엔 쏘는 병사 자리가 곧 대포 자리였다 |
| 6. 이동 끝에 뚝 끊기며 자리 잡음 | 달리는 길(통로 안쪽 1.4m)로 달리다 닿으면 바깥 0.5m 자리로 순간 이동 | 떠날 때·닿을 때 2m(`WalkInDistance`) 동안 자기 자리와 달리는 길 사이를 부드럽게 비스듬히 걷는다(smoothstep). 묶음 이동(출발·도착 둘 다), 전령(출발·복귀), 우회 보고 두 명(출발·한 바퀴 뒤 복귀) 모두 |
| 8. 버그: 대포 쏘는 병사가 전령으로 뽑힘 | `NearestFreeSoldier`가 '무기 없는 병사'만 뺐는데 대포 분대 첫 병사는 대포를 쏘는 무기가 있다 | 대포 분대(옮길 수 없는 분대)는 통째로 뺀다. 우회 보고 짝도 같은 함수로 고르므로 함께 고쳐짐 |

## 바꾼 파일

- `Game/Assets/_Project/Combat/Battle/BattleSim.cs` — 상수(`SoldierMinSpacing` 0.24, `SoldierRowDepth` 0.3, `SoldierMaxRows` 3, `CannonCrewOffset` 0.45, `WalkInDistance` 2), 분대 줄 세우기(여러 줄·대포 양옆), 도우미 `WallSpot`·`RowOf`·`LineLayout`·`CannonSpot`·`WalkIn`
- `Game/Assets/_Project/Combat/Battle/BattleSim.Groups.cs` — `ChooseCenter` → `ChooseSpot`(가운데+줄), `ArriveSide`에 줄, 묶음 이동 출발·도착 비스듬히(`StartSpot`·`EndSpot`)
- `Game/Assets/_Project/Combat/Battle/BattleSim.Commands.cs` — `NearestFreeSoldier` 대포 분대 제외, 전령 출발·복귀 비스듬히 (한 줄씩)
- `Game/Assets/_Project/Combat/Battle/BattleSim.Flank.cs` — 우회 보고 출발·복귀 비스듬히 (한 줄)
- `Game/Assets/_Project/Combat/Battle/BattleSim.Cannons.cs` — 대포 위치를 `CannonSpot`으로 (`GetCannon` Position, `SetCannonAim` 사거리 기준점 두 줄)
- `Game/Assets/_Project/Meta/PreparationTransactions.cs` — 옛 저장 허용(아래 의심 2)
- `Game/Assets/_Project/Core/Definitions/PreparationEconomyDef.cs` — 대포 지점 정원 기본값 2
- `Game/Assets/_Project/Data/Combat/Start/campaign_start.asset`, `Game/Assets/_Project/Data/Meta/economy_preparation_1.asset`

## 의심 가는 곳 (메인에서 확인 부탁)

1. **난이도**: 대포 분대가 5→2명이라 시작 병력이 성벽마다 12명, 전체 48명 줄었다(대포 수·사격은 같음). 대포병 2명 중 뒤 번호(오른쪽)가 먼저 죽고, 쏘는 병사(왼쪽)가 죽으면 대포가 멈춘다 — 예전보다 대포가 빨리 멈출 수 있다. SimProbe로 확인 필요.
2. **옛 저장**: 정비 화면(`PreparationTransactions`)은 들어올 때 상태를 검사해 정원을 넘으면 '데이터 오류'로 막는다. 대포 분대 5명인 옛 저장이 막히지 않게 검사만 예전 최대 5명까지 받아 준다(`LegacyCannonCrewCount`). 새 모집은 2명까지. 옛 저장 대포 분대는 전투에서 대포 양옆으로 번갈아 바깥쪽에 선다.
3. **대포 위치가 조금 옮겨진다**: 예전엔 대포가 쏘는 병사(5명 줄의 왼쪽 끝) 자리, 이제 지점 자리 정가운데. 난간 사격구(BattleWorldView·BattlePropView는 `GetCannon().Position`을 따른다)도 같이 옮겨진다. 쏘는 병사는 대포에서 0.45m 왼쪽이라 포탄 출발점이 대포 그림과 0.45m 어긋난다(BattleJobs는 병사 자리에서 쏜다 — K-042 영역이라 안 건드림).
4. **`BattleSim.Cannons.cs` 두 줄**은 K-042(대포 사거리) 카드와 겹칠 수 있다. 합칠 때 확인.
5. 안쪽 줄(0.8·1.1m)은 주인공이 걷는 통로(0.75~1.8m)와 겹친다. 주인공 바로 앞에 병사 그림이 가릴 수 있다.
6. 줄 판정(`RowOf`)은 병사 위치를 줄 간격(0.3m)으로 반올림한다. 비스듬히 걸어 들어오는 중인 병사는 근처 줄로 셀 수 있지만, 이동 중인 묶음은 `TargetRow`로 따로 센다.
7. 전령 출발 알림의 '약 N초'는 비스듬히 걷는 거리를 넣지 않았다(차이 1초 미만).
8. ROADMAP md·html은 카드 가지에서 고치지 않았다(통합 때 조정 담당).
