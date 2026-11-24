# K-060 결과 — 대포 끌기 3차(K-052) 연결 + 대포 옮기기 + 설치 대포 크기

작성: Claude / 2026-10-19 / 가지 `card/K-060`
**유니티를 열지 않았고 컴파일·시험도 못 돌렸다.** 합친 뒤 편집 시험(새 4개)·PlayProbe `CannonArt` 사진 확인이 필요하다.

## 1. 대포 옮기기 (사용자 확정 2026-10-18, 질문 답 (가))
- **빈 사격구**: 성벽마다 사격구 **6곳(제안값 `BattleSim.CannonBaysPerSide`)** — 대포가 놓인 지점 4곳 + 빈 사격구 2곳
  - 빈 사격구는 기존 사격구에서 가장 먼 곳에 놓는다(사격구 폭의 1.6배 넘게 떨어진 곳, 성벽 가운데 쪽 우선)
  - `cannonBays`에 넣었으므로 성벽 3D 난간 낮춤(BattleWorldView)·총안·병사 자리(BattleWallLayout·Slots)·횃불 자리가 함께 바뀐다
  - 총안이 한 면에 대략 18칸 줄어든다(시험 기준 '40칸 넘게'는 여전히 넘을 것으로 본다)
- **지도 조작(기능만, 화면 단추는 K-047)**: 대포를 고른 상태에서 **R** → 보낼 성벽 쪽 들판을 누른다. 다시 R이면 취소한다(M은 이미 지도 닫기 키라 R로 했다)
  - 대포를 다시 고르거나 지도를 닫으면 취소된다
- 규칙 (`BattleSim.Haul.cs`):
  - 둘레 길로 가장 가까운 **빈 사격구**로 간다. 빈 사격구가 없으면 "○○에 빈 대포 사격구가 없습니다" 알림을 띄우고 거절한다
  - 대포와 운용병이 같이 성벽 통로를 따라 간다(떠날 때·닿을 때 비스듬히 — 묶음 이동과 같게). 가는 동안 쏘지 않고, 불붙이던 중이면 취소된다
  - 떠날 때·도착할 때 성벽 병력(GarrisonStart/Alive)은 묶음 이동과 같은 `LeaveSide`·`ArriveSide`로 처리한다
  - 도착하면 그 사격구의 대포가 된다. 번호는 그 성벽 끝 번호 + 1이다. 예전 조준점은 지우고, **칠 수 있는 들판(K-042)은 `InitCannonReach`로 다시 계산**한다
  - 전투 준비 중이면 바로 옮긴다. 전투 중에는 주인공 성벽 대포면 바로 출발하고, 다른 성벽 대포면 **전령이 닿아야 출발**한다(Messenger.MoveCannon)
- **속도**: 발 미끄러짐이 없게 '걸음 길이 × 칸 속도'로 정했다
  - 걸음은 32px = 약 0.053m이고 칸 속도는 12fps라 **약 0.64m/초**(`HaulFps`)
  - K-034의 대포병 달리기(전령 속도 × 0.6, 약 2m/초)보다 훨씬 느리다. 2m/초로 맞추면 걸음 칸이 초당 약 40칸이라 다리가 떨리듯 보인다 → **질문 Q1**

## 2. 끌기 그림 연결 (K-052, 사용자 확정 전 미리 연결)
- 그림 이름·칸·기준점·병사 자리는 **`Combat/PerfLab/CannonArt.cs` 한곳**에 모았다
- 복사한 그림(`Resources/BattleArt/Cannon/`): crew_pull6·crew_push6(2304×384, 6칸 384×384, 인물 210px), haul_cannon_body·chassis·wheel
- `BattlePropView.DrawHauls`:
  - 끄는 대포마다 포가 + 도는 바퀴 + 앞 병사(당김) + 뒤 병사(밈, 운용병 2명 이상일 때)를 그린다
  - **발 미끄러짐 방지**: Codex 규칙(칸마다 32px 나아가고 디딘 발은 제자리)대로 위치와 걸음 칸을 32px 단위로 함께 끊는다
  - 바퀴는 끈 거리 ÷ 반지름만큼 돈다(투사체 회전과 같은 셰이더 기능)
  - 그림은 왼쪽으로 끌기라, 사진기 오른쪽으로 가면 좌우를 뒤집는다
- 끄는 동안 운용병의 서 있는 그림은 숨긴다(`RefreshSoldierInstances`에 한 조건). 설치 대포 그림·포구 연기도 숨긴다
- **불붙이기 교체**: 병종 정의가 가리키는 그림 파일 `Art/Characters/Fire/cannon_crew_ignite6.png`의 내용을 K-052 것으로 바꿔 끼웠다
  - guid는 그대로라 정의를 고치지 않았다
  - 메타의 최대 크기를 2048에서 4096으로 바꿨다(2304px 시트가 줄지 않게)
  - 칸 폭 384·인물 키 210은 `CannonArt` 표로 `PerfLabArt.FromSheet·FrameCount·FigurePx`가 읽는다
  - 시간(0.5초 접촉 → 1.5초 발사)은 K-046과 같다
- 가져오기 설정: Cannon 폴더 최대 크기 4096 (`BattleFieldArtImport`)

## 3. 설치 대포 크기
- Codex 측정(정면 설치 대포 폭 345 ÷ 서 있는 대포병 키 300 ≈ 1.15)으로 **보이는 폭 = 병사 키 0.35m × 1.15 ≈ 0.40m**이다
  - cannon_recoil 칸 높이는 0.6 → **0.44m**가 된다(K-052 끄는 대포와 같은 축척)
  - 사격구 폭 1.12m 안이다. 운용병 자리(대포 ±0.45m)와도 대포 반폭 0.2m + 병사 반폭 약 0.12m라 겹치지 않는다
- **'작은 검은 점' 원인 추정**: 크기 자체는 오히려 컸다(보이는 폭 0.55m). 병사는 위에서 볼 때 납작해지지 않게 세로를 늘리는데(`_ViewStretch`) 대포만 그 보정이 없었다. 그래서 대포만 납작하게 눌려 작고 어둡게 보였을 것으로 본다 → 설치 대포에도 보정을 켰다
  - 실제 원인인지는 사진으로 확인해야 한다 → `cannon_installed_back_A/B`, `cannon_installed_front`

## 4. 시험·사진
- 편집 시험 `Combat/Tests/BattleCannonMoveTests.cs` 4개:
  - 성벽마다 사격구 6곳·빈 사격구 수
  - 바로 옮기기(빈 사격구 자리·병력 이동·칠 수 있는 들판)
  - 끌고 가기(끌기 그림 → 도착)
  - 빈 사격구가 없으면 거절
- PlayProbe `CannonArt`: 설치 대포 뒤 A·B, 정면(들판 쪽 임시 사진기), 끌고 가는 중 뒤 A·B·옆 사진. 크기·속도는 cannon_art.txt에 남긴다

## 바꾼 파일
- 새 파일:
  - `Combat/PerfLab/CannonArt.cs`
  - `Combat/Battle/BattleSim.Haul.cs`
  - `Combat/Tests/BattleCannonMoveTests.cs`
- 고친 파일:
  - `PerfLab/PerfLabArt.cs`: 칸 폭·인물 키 표
  - `Battle/BattlePropView.cs`: 설치 대포 크기·보정, 끌기 그리기, 빈 사격구 그림
  - `Battle/BattleSim.cs`: 두 줄 — 빈 사격구 더하기, 끄는 병사 숨기기
  - `Battle/BattleSim.Cannons.cs`: Moving 표시
  - `Battle/BattleSim.Commands.cs`: 전령 MoveCannon
  - `Battle/BattleSim.Groups.cs`: 대포 옮기기 갱신 한 줄
  - `Battle/BattleEntry.cs`: R 키·OrderCannonMove
  - `Editor/BattleFieldArtImport.cs`
  - `_PlayProbe/PlayProbe.cs`
- 그림:
  - 바꿔 끼움: `Art/Characters/Fire/cannon_crew_ignite6.png`(+ .meta 최대 크기)
  - 새로 넣음: `Resources/BattleArt/Cannon/`의 crew_pull6·crew_push6·haul_cannon_*

## 질문·걱정
- **Q1 끄는 속도**: 발이 안 미끄러지게 맞추면 약 0.64m/초다(면 하나 40m에 1분쯤). 더 빨라야 하면 `HaulFps`를 올린다(다리 동작이 빨라짐). 아니면 걸음 맞춤을 포기하고 K-034 속도로 할지 정해야 한다
- **Q2 빈 사격구 수 6곳**은 제안값이다. 빈 사격구 2곳만큼 총안·병사 자리가 줄어든다
- **병사 자리 값**: 앞 병사 −391px·뒤 병사 +188px는 합성 비교 그림(haul_1.png)을 눈으로 읽은 값이다. 밧줄은 Codex 합성 화면에만 있고 병사 그림에는 없어서, 앞 병사가 빈손으로 당기는 것처럼 보일 수 있다
- **크기 차이**: 포가·바퀴는 납작함 보정을 껐다(바퀴 중심이 어긋나지 않게). 병사는 보정을 받아 위에서 볼 때 대포보다 조금 커 보일 수 있다
- **칠 수 있는 들판**: 대포가 떠난 성벽의 남은 대포는 도착 때 다시 계산된다(떠나는 순간은 아님)
- **끄는 중 병사 손실**: 끄는 중인 운용병이 성벽 피해로 쓰러지는 경우는 묶음 이동과 같이 따로 처리하지 않았다
