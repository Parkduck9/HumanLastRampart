# K-045 결과 — 병사를 성벽 총안에, 횃불은 돌출부 뒤로

유니티는 열지 않았다(컴파일·단위 시험·사진 확인 못 함). 메인에서 확인 부탁.

## 한 일

1. **공용 배치 계산 `BattleWallLayout`** (새 파일 `Combat/Battle/BattleWallLayout.cs`): 돌출부 가운데·총안(돌출부 사이 빈칸) 가운데·횃불 자리를 한 곳에서 계산한다. 성벽 3D(`BattleWorldView`)가 이 함수로 돌출부를 놓고, 병사 자리·횃불도 같은 함수를 쓴다. 대포 사격구 목록도 시뮬레이션 한 곳(`BattleSim.CannonBays`)에서 나와 세 곳이 같은 값을 쓴다.
   - 돌출부: 예전 BattleWorldView 반복문 그대로(모서리 발판 끝 비움, MerlonWidth·MerlonGap 간격, 사격구 안 돌 뺌).
   - 총안: 양옆 돌이 모두 서 있는 빈칸만(사격구와 이어진 빈칸은 대포 자리라 뺌). 마지막 돌 뒤 끝자락 빈칸은 뺐다.
   - 시안이 꺼져 있어도(돌을 안 그려도) 확정 시안 1 비율로 자리를 잡는다.
2. **병사 자리 표 `BattleSim.Slots`** (새 파일 `Combat/Battle/BattleSim.Slots.cs`, 사용자 확정 규칙):
   - 줄: 0 총안(바깥에서 0.5m) → 1 돌 뒤 엇갈림(0.8m) → 2 총안 줄(1.1m) → (달리는 길 1.4m 비움) → 3 돌 뒤(1.7m) → 4 총안 줄(1.95m). 뒷줄도 쏜다(사격 코드 그대로).
   - 처음 배치: 분대마다 자기 구간(양옆 지점과의 가운데까지) 안에서 '구간 안 앞줄 → 구간 안 뒷줄 → 구간 밖 가까운 곳' 순서로, 앞 번호일수록 지점 가운데에 가까운 총안. 병사는 뒤 번호부터 잃으므로 뒷줄·먼 자리가 먼저 빈다.
   - 묶음 이동: 빈 앞줄 자리가 가장 많은 구간(폭 = 인원 × 총안 간격 0.47m)을 고르고 떠날 때 자리를 맡아 둔다. 다른 묶음이 같은 성벽으로 오면 맡아 둔 자리를 피한다. 예전 `ChooseSpot`(K-039 줄 고르기)·`LineLayout`·`RowOf`는 없앴다.
   - 쓰러짐 뒤 메우기: 병사가 쓰러지면 그 자리를 1.2m 안의 가장 가까운 뒷줄 병사가 걸어(1.2m/초) 들어와 메운다. 그 병사가 비운 자리도 다시 메운다(최대 6번).
   - 전령·우회 보고: 자리를 맡겨 둔 채 나가고, 그 총안 자리로 돌아온다(`HomeSpot`).
   - 자리가 모자랄 때: 한 면 자리는 약 270개다(총안 약 59 × 3줄 + 돌 뒤 약 47 × 2줄). 성벽 정원 500명을 다 채우면 남는 병사는 안쪽 끝(1.95m)에 0.24m 간격으로 겹쳐 선다.
   - 대포와 대포병 2명은 그대로(K-039 `CannonSpot`, 자리 표 안 씀).
3. **횃불**: 간격 3m·가운데 맞춤 자리에서 가장 가까운 돌출부로 옮겼다(1m 안에 돌이 없으면, 곧 사격구면 뺌). 난간 바로 뒤(바깥에서 0.35m, 예전 0.5m)에 서서 총안에 선 병사와 옆으로 비켜 있다. 횃불 돌 뒤(돌 뒤 줄)에는 병사 자리를 두지 않는다. 빛 세기·범위·높이는 그대로. 빛(`BattleLights`)과 불꽃·받침 그림(`BattleEffectsView`)이 같은 목록(`sim.TorchSpots`)을 쓴다.
4. **확인**: 단위 시험 `Combat/Tests/BattleWallSlotTests.cs`(앞줄 자리 = 성벽 3D 총안, 한 자리에 한 명, 횃불은 돌출부 자리이고 그 뒤에 병사 없음), PlayProbe `WallSlots`(줄별 인원 기록, 북쪽 성벽 가까이 A·B를 낮·밤으로 4장: `wall_slots_day_A/B`, `wall_slots_night_A/B`).

## 바꾼 파일

- 새 파일: `Combat/Battle/BattleWallLayout.cs`, `Combat/Battle/BattleSim.Slots.cs`, `Combat/Tests/BattleWallSlotTests.cs` (각 .meta)
- `Combat/Battle/BattleSim.cs`: 처음 배치를 총안 자리로, 사격구 목록 먼저 모음, `KillSoldiersFromDamage`에 자리 메우기 한 줄, `BeginFrame`에 `UpdateSlotWalks` 한 줄, K-039 줄 세우기 상수·함수 정리
- `Combat/Battle/BattleSim.Groups.cs`: 묶음 이동이 총안 자리를 맡아 감
- `Combat/Battle/BattleSim.Commands.cs`, `BattleSim.Flank.cs`: 전령·우회 보고 돌아올 자리 한 줄씩
- `Combat/Battle/BattleWorldView.cs`: 돌출부 자리를 공용 계산으로, 사격구 목록을 sim에서
- `Combat/Battle/BattleLights.cs`, `BattleEffectsView.cs`: 횃불 자리
- `Combat/_PlayProbe/PlayProbe.cs`: `WallSlots` 추가

K-046(대포 불붙이기·`BattleSim.Cannons.cs`·발사 시점), K-049(지형·적 이동) 부분은 건드리지 않았다. `BattleSim.cs`는 병사 배치·쓰러짐·BeginFrame 한 줄만 고쳤다.

## 의심 가는 곳

1. 총안 폭 0.21m에 병사 그림 폭 0.19~0.23m(총 포함)라 앞줄 병사끼리 0.47m 간격으로 넉넉하다. 대신 분대 구간 안 앞줄 총안이 약 8칸뿐이라 궁수 20명 분대는 3줄(총안·돌 뒤·둘째 총안 줄)로 선다. 시작 병력 기준 앞줄 약 31명, 나머지는 뒷줄로 예상한다(PlayProbe 기록에 줄별 인원이 남는다).
2. 뒷줄이 쏠 때도 사거리·명중은 예전 그대로다(성벽 안쪽 0.3~0.6m 차이라 영향 미미).
3. 주인공 통로(0.75~1.8m)에 돌 뒤 줄(0.8m)과 둘째 총안 줄(1.1m)이 있어, 주인공이 그 사이를 지나며 병사 그림과 겹칠 수 있다(K-039와 같음).
4. 횃불이 난간 쪽으로 0.15m 나갔다. 사진기에서 돌출부 높이(0.21m) 위로 받침·불꽃이 보이는지 사진으로 확인 필요. 대포 사격구 근처라 1m 안에 돌이 없는 횃불 자리는 빠지므로 횃불 수가 조금 줄 수 있다(시험이 성벽마다 5개 넘게인지 확인).
5. 전령으로 뽑힌 병사가 자리 메우러 걷는 중이었다면, 떠날 때 맡아 둔 자리 기준으로 비스듬히 나가 0.3m쯤 튈 수 있다.
6. 전투 중에 `BattleWorldView.Draft`를 바꾸면(시험 도구) 병사 자리는 전투 시작 때 시안 기준이라 돌과 어긋날 수 있다. 새 전투부터 맞는다.
7. ROADMAP md·html은 카드 가지에서 고치지 않았다.
