# 캐릭터 그림·노멀맵 교체 안내

작성 Claude / 2026-10-15 / 카드 K-008 / D-062 G4(모든 캐릭터 노멀맵)

전투 화면(1·2번)의 도트 캐릭터는 셰이더 `HLR/PerfLabSprite`(`Combat/PerfLab/PerfLabSprite.shader`) 하나로 GPU에서 한꺼번에 그린다. 그림은 **데이터(.asset)의 칸에 연결된 텍스처**를 그대로 읽으므로, 코드를 고치지 않고 그림 파일과 데이터 칸만 바꾸면 교체된다.

## 1. 그림 규격 (색 그림)

| 항목 | 규칙 |
|---|---|
| 칸 크기 | 256×384 픽셀 (`PerfLabArt.CellWidth/CellHeight`) |
| 발 위치 | 각 칸 왼쪽 위 기준 (128, 352) — 칸 아래에서 32픽셀 위. 모든 칸에서 같아야 발이 미끄러지지 않는다 |
| 열·행 | 그림 크기로 센다: 가로 ÷ 256 = 열, 세로 ÷ 384 = 행 |
| 동작당 칸 | 4칸(한 행 = 한 동작 4칸, `FramesPerAction`) |
| 배경 | 투명(알파). 반투명 대신 알파 잘라내기로 그린다 |
| 방향 | 옆모습은 **왼쪽을 본다**. 오른쪽은 게임이 좌우 뒤집어 그린다 |

시트 종류별 배치

| 종류 | 크기 | 배치 |
|---|---|---|
| 정적 1칸 (병사 뒷모습, 마법사, 주인공 서 있기) | 256×384 | 1열×1행 |
| 적 걷기·공격 (고블린) | 1024×768 | 4열×2행 — 1행 걷기, 2행 공격 |
| 적·병사 걷기/달리기 | 1024×384 | 4열×1행 |
| 주인공 | 1024×1536 | 4열×4행 — 0 뒷모습 걷기, 1 왼쪽 걷기, 2 왼쪽 달리기, 3 정면 걷기 |

## 2. 노멀맵 규격

- **색 그림과 크기·칸 배치가 완전히 같다.** 같은 칸 번호, 같은 발 위치. 색 그림의 칸을 옮기면 노멀맵도 똑같이 옮긴다.
- 탄젠트 공간, OpenGL 방식(Y+): **빨강 = 오른쪽, 초록 = 위, 파랑 = 앞(화면 밖)**. 평평한 곳은 (128, 128, 255).
- 투명도는 색 그림 알파만 쓴다. 노멀맵 알파는 쓰지 않는다(불투명 RGB면 된다). 빈 곳은 (128, 128, 255)로 채운다.
- 좌우 뒤집기 때 노멀 X는 셰이더가 알아서 뒤집는다 — 노멀맵을 따로 뒤집어 만들 필요 없다.
- 파일 이름: 색 그림 이름 뒤에 `_normal` (예: `archer_static.png` → `archer_static_normal.png`).

## 3. 가져오기 설정 (.meta / Inspector)

| 항목 | 색 그림 | 노멀맵 |
|---|---|---|
| Texture Type (`textureType`) | Default (0) | **Normal map (1)** |
| sRGB (`sRGBTexture`) | 켬 (1) | **끔 (0)** |
| Alpha Is Transparency | 켬 | 끔 |
| Filter Mode | Point (0) | Point (0) |
| Generate Mip Maps | 끔 | 끔 |
| Wrap | Clamp | Clamp |
| Non-Power of 2 | None (0) | None (0) |
| Max Size | 2048 이상 | 2048 이상 |
| 압축 | 없음 | 없음 (견본 단계) |
| Flip Green Channel | — | 끔 |

유니티 안에서 새 그림을 넣으면 같은 폴더의 기존 그림 설정을 Inspector에서 맞추면 된다. 유니티 밖에서 넣을 때는 같은 폴더 기존 `.meta`를 복사해 `guid`만 새 32자리 소문자 16진으로 바꾸고, 노멀맵이면 위 표대로 고친다.

## 4. 어디에 넣고 어디에 연결하나

그림 폴더: `Game/Assets/_Project/Art/Characters/`

| 폴더 | 내용 |
|---|---|
| `Hero/` | 주인공 시트, 서 있기 |
| `Static/` | 병사·마법사·적 정적 1칸 |
| `Walk/` | 적 걷기 |
| `Run/` | 병사 달리기(전령·우회 보고) |
| `Goblin/` | 고블린 걷기·공격 |

데이터 칸

| 데이터 | 칸 | 쓰임 | 짝 노멀맵 칸 |
|---|---|---|---|
| 아군 `UnitDef` (`Data/Combat/Units/unit_*.asset`) | `sheet` | 기본 그림 (병사 분대, 마법사, 주인공 4행) | `normalSheet` |
| | `idleSheet` | 주인공 서 있기 1칸. 없으면 `sheet` 0번 칸 | `idleNormalSheet` |
| | `runSheet` | 성벽 위 달리기 4칸. 없으면 달리기 그림 없음 | `runNormalSheet` |
| 적 `EnemyDef` (`Data/Combat/Enemies/enemy_*.asset`) | `sheet` | 적 무리 | `normalSheet` |

- 노멀맵 칸은 **선택**이다. 비우면 그 그림은 노멀맵 없이(기본 감싸는 빛) 그려진다.
- 그림 칸이 비면 임시 그림으로 그린다. 노멀맵만 있고 그림이 없으면 노멀맵은 쓰지 않는다.
- 연결은 유니티 Inspector에서 칸에 끌어 놓거나, `.asset` 파일에 `normalSheet: {fileID: 2800000, guid: <그림 meta의 guid>, type: 3}` 한 줄을 넣는다.

교체 순서: ① 같은 규격으로 그림(과 노멀맵)을 그린다 → ② 위 폴더에 넣고 가져오기 설정 확인 → ③ 데이터 칸에 연결 → ④ 전투에서 발 위치·칸 순서·빛 방향 확인(해·달·좌우 뒤집기).

## 5. 지금 연결된 그림 (2026-10-15)

| 데이터 | 칸 | 그림 (`Art/Characters/`) | 노멀맵 |
|---|---|---|---|
| unit_hero | sheet | `Hero/hero_walk_run_grid.png` (4×4) | `Hero/hero_walk_run_grid_normal.png` — Codex 견본 v1, 사용자 검토 전 |
| unit_hero | idleSheet | `Hero/hero_back_idle.png` (1×1) | 없음 |
| unit_archer | sheet | `Static/archer_static.png` (1×1) | `Static/archer_static_normal.png` — Codex 견본 v1, 사용자 검토 전 |
| unit_archer | runSheet | `Run/archer_run.png` | 없음 |
| unit_gunner | sheet / runSheet | `Static/gunner_static.png` / `Run/gunner_run.png` | 없음 |
| unit_cannon_crew | sheet / runSheet | `Static/cannon_crew_static.png` / `Run/cannon_crew_run.png` | 없음 |
| unit_wizard | sheet | `Static/wizard_back_static.png` (1×1) | 없음 |
| enemy_goblin | sheet | `Goblin/goblin_walk_attack4.png` (4×2) | 없음 |
| enemy_goblin_ladder | sheet | `Walk/ladder_goblin_walk.png` | 없음 |
| enemy_ogre | sheet | `Walk/ogre_walk.png` | 없음 |

견본 출처: `Docs/References/codex_normal_samples_v1/` (메인 폴더, Codex T-CX-048). 견본 두 장의 사용자 확인 전에는 다른 캐릭터 노멀맵을 만들지 않는다(같은 README).
