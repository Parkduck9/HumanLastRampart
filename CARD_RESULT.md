# K-046 결과 — 대포병이 불을 붙이고 1초 뒤 발사

작성 Claude · 2026-10-16 · 가지 card/K-046 · **유니티 실행·컴파일 안 함**

## 발사 순서
쏠 때가 되면(재장전 끝 + 표적 있음) → **불붙이기 시작**: 포탄(목표점·표적)을 이때 정해 고정 → 0.5초에 막대가 도화선에 닿음 → **닿은 뒤 1초에 발사**(포탄·반동·연기는 예전 발사와 같은 순간).
- 시간: 막대 뻗기 0.5초 + 도화선 1초 = 결정에서 발사까지 1.5초 (Codex K-043 시안 timeline과 같음: 0.5초 접촉, 1.5초 발사). 상수는 `BattleSim.Ignite.cs` 한곳(`CannonIgniteLead`, `CannonFuseSeconds`, `IgniteFps`).
- 불붙이는 동안 조준 고정. 그 사이 지도에서 조준을 바꾸면 다음 발부터.
- 자동 표적(조준점 없음)은 걸어오는 적을 **날아가는 시간 + 불붙이는 1.5초**만큼 앞질러 겨눈다.
- 쏘는 대포병이 쓰러지거나(분대 마지막 한 명 — 병사는 뒤에서부터 잃음) 전령으로 빠지면 취소.
- **재장전은 발사 시점부터**: 발사 뒤 대기 = 간격 − 1.5초, 그다음 불붙이기 1.5초 → 연속 포격 간격은 그대로 6초.

## 난이도 영향 (계산, 측정 아님)
- 연속으로 쏘는 동안 초당 피해는 그대로(간격 6초 유지).
- **첫 발과 쉬었다 다시 쏘는 발이 1.5초 늦다** (전투 시작·조준점에 적이 새로 들어올 때). 4면 16문 × 몇 번 정도라 전체 처치 수 영향은 작을 것으로 본다.
- **움직이는 적에 대한 명중이 조금 준다**: 1.5초 동안 고블린은 약 6m 걷는다. 앞질러 겨누므로 곧게 걷는 적은 맞지만, 그 사이 성벽 앞에서 멈추는 적·방향을 바꾸는 적은 목표점을 지나쳐 폭발 반경(4m) 밖이 될 수 있다. 지도 조준점(고정 지점) 포격은 영향 없음(조준점 근처 적이 있는지는 불붙이기 시작 때 본다).
- 시뮬레이션 측정(SimProbe 4전투)은 돌리지 못했다 — 조정 담당 실행 필요.

## 그림·지도
- 대포병 불붙이기 6칸(`Art/Characters/Fire/cannon_crew_ignite6.png`, Codex `crew_ignite6.png` 복사)을 `unit_cannon_crew.igniteSheet`로 미리 연결(사용자 확정 전). 칸 구간 `PerfLabArt.IgniteFrame0 = 80`. 쏘는 대포병(분대 맨 앞)만 불붙이는 동안 이 그림, 6칸/초로 1초 재생 후 마지막 칸 유지 → 발사 뒤 원래 그림. 바꿔 끼우려면 .asset의 `igniteSheet` 한 줄.
- 불씨 반짝임은 시트 5번째 칸에 그려진 것뿐 — 따로 빛나는 겉칠·조명은 넣지 않았다.
- 대포 끌기 그림(crew_pull6·crew_rear6·cannon_body)은 K-048 대기로 연결하지 않음.
- 지도(2번 화면): 불붙이는 대포의 점 위에 **주황 불씨가 깜빡인다**. `CannonInfo.Igniting` 추가(지도 문구 '포격 중/대기'는 그대로).

## 바꾼 파일
- Combat/Battle/BattleSim.Ignite.cs (새 파일 + .meta) — 시간 상수, 남은 도화선·정해 둔 포탄 배열, 그림 칸
- Combat/Battle/BattleJobs.cs — SoldierFireJob: 불붙이기 상태(취소·발사), `Launch()`로 두 발사 지점 통일, 대포 앞질러 겨누기에 불붙이기 시간 더함
- Combat/Battle/BattleSim.cs — Schedule에 세 필드 연결만 (배치 코드 안 건드림)
- Combat/Battle/BattleSim.Poses.cs — InitIgnite 호출, 불붙이기 칸, 해제
- Combat/Battle/BattleSim.Cannons.cs — CannonInfo.Igniting 한 줄씩 (CannonSpot·배치 안 건드림)
- Combat/Battle/BattleView.cs — 불붙이기 시트 그리기 한 줄, 지도 불씨 표시
- Combat/PerfLab/PerfLabArt.cs — IgniteFrame0
- Core/Definitions/UnitDef.cs — igniteSheet
- Data/Combat/Units/unit_cannon_crew.asset — igniteSheet 연결
- Combat/_PlayProbe/PlayProbe.cs — CannonAim에 '불붙이는 대포가 있다' 확인과 사진 `cannon_ignite_map`
- Docs/collab/rules/CONTRACTS.md — 맨 아래 'C-010 v1.10 초안'

## 확실하지 않은 것
1. 불붙이기 그림은 **왼쪽 옆모습**인데 성벽 위 대포병·대포 반동 그림은 뒷모습이라 방향이 어색할 수 있다(뒤집기 안 함). 사진 확인 필요.
2. 투사체 출발점은 여전히 쏘는 병사 자리 기준 `Muzzle()`(K-035). K-039로 대포 자리(`CannonSpot`)가 병사와 따로 생겼으니 포탄이 대포 그림 포구에서 나오는지 확인 필요 — 이 카드에서는 배치 코드를 건드리지 않으려고 고치지 않았다.
3. PlayProbe `CannonAim`의 '포격 중' 문구 검사는 그대로 통과할 것으로 본다(대기 판단은 불붙이기 시작 때). 새 확인은 200프레임(×4배속 약 40초) 안에 불붙이는 대포를 찾는다.
4. 사진(불붙이는 순간)은 유니티를 못 열어 만들지 못했다 — 위 시험의 `cannon_ignite_map` 캡처로 대신.
