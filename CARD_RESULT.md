# K-032 결과 — 캐릭터 동작·투사체 그림 전투 연결 (미리 연결, 사용자 검토 전)

작성 Claude · 2026-10-16 · 가지 card/K-032 · **유니티 실행·컴파일 안 함** (메인에서 조정 담당이 시험)

## 1. 연결한 그림 (Game/Assets/_Project/Art/Characters/ 아래로 복사, .meta는 직접 만든 guid)

| 그림 | 게임 파일 | 연결한 곳 | 그리는 때 |
|---|---|---|---|
| K-016 궁수 사격 6칸 | Fire/archer_fire.png | unit_archer.fireSheet | 쏜 순간 = 4번째 칸(놓기)→5·6칸, 다음 발이 가까우면 1~3칸(겨누기)을 미리 |
| K-016 총병 사격 6칸 | Fire/gunner_fire.png | unit_gunner.fireSheet | 같음 |
| K-016 화살 | Projectiles/arrow.png | unit_archer.projectileSheet | 날아가는 방향으로 돌림 |
| K-016 총알 흔적 | Projectiles/bullet_trace.png | unit_gunner.projectileSheet | 같음 |
| K-016 포탄 | Projectiles/cannonball.png | unit_cannon_crew.projectileSheet | 같음 (둥글어서 돌림은 티 안 남) |
| K-017 사다리 세우기 4칸 | Ladder/ladder_goblin_deploy4.png | enemy_goblin_ladder.deploySheet | 오르기 시작 뒤 첫 0.5m |
| K-017 사다리 오르기 4칸 | Ladder/ladder_goblin_climb4.png | enemy_goblin_ladder.climbSheet | Climbing 상태 (첫 0.5m 뒤) |
| K-017 사다리 정면 소품 | Ladder/ladder_front.png | enemy_goblin_ladder.ladderSheet | Climbing 동안 성벽 바로 앞 땅에 4.3m(칸 전체 5.73m) |
| K-022 고블린 쓰러짐 4칸 | Fall/goblin_fall4.png | enemy_goblin.deathSheet | 죽은 뒤 0.7초 (4칸 0.5초 + 마지막 칸) |
| K-022 오우거 공격 4칸 | Walk/ogre_attack4.png | enemy_ogre.attackSheet | AtWall·OnWall |
| K-022 오우거 쓰러짐 4칸 | Fall/ogre_fall4.png | enemy_ogre.deathSheet | 죽은 뒤 0.7초 |
| K-022 고블린 노멀 견본 | Goblin/goblin_walk_attack4_normal.png | enemy_goblin.normalSheet | **1칸 견본을 8칸에 같은 그림으로 깔아 만든 근사 시트** |
| K-023 궁수·총병·대포병 쓰러짐 | Fall/archer_fall.png, gunner_fall.png, cannon_crew_fall.png | 각 unit.deathSheet | 쓰러진 뒤 0.7초 |
| K-023 마법사 시전 4칸 | Static/wizard_cast.png | unit_wizard.castSheet | 마법이 나간 순간부터 1초 (6칸/초, 마지막 칸 유지) |
| K-023 주인공 명령 손짓 3칸 | Hero/hero_command.png | unit_hero.commandSheet | 휠 메뉴(hud.MenuOpen)가 열려 있는 동안 (6칸/초, 마지막 칸 유지) |
| K-023 총병·대포병·마법사 노멀 | Static/gunner_static_normal.png, cannon_crew_static_normal.png, wizard_back_static_normal.png | 각 unit.normalSheet | 정적 1칸 그림과 1:1 짝 |

복사만 하고 **연결하지 않은 것** (판단이 필요해서):
- Ladder/ladder_goblin_carry_run4.png (들고 달리기): 사다리 없이 손만 쥔 그림이라 사다리 소품을 함께 그려야 한다. 그런데 들판의 적은 1.2m 크기로, 성벽 앞에선 0.33배로 그려 사다리(실제 4.3m)와 비율을 정할 수 없다 → 걷기는 기존 사다리가 그려진 걷기 그림 그대로.
- Ladder/ladder_side.png (측면 사다리), Static/ogre_static_normal.png (오우거 정지 그림 짝인데 전투는 걷기 시트를 씀), Ladder/ladder_goblin_carry_normal.png (들고 달리기 짝).
- 사다리 고블린 쓰러짐 그림은 없어서 예전처럼 바로 사라진다. 고블린 쓰러짐을 같이 쓸지 결정 필요.

## 2. 그리는 방법 (묶음 그리기 유지)
- 개체 버퍼는 그대로 하나. 동작 시트마다 같은 버퍼를 한 번 더 그리고, 셰이더 `_FrameWindow`가 자기 칸 구간만 남긴다. 칸 번호 = 구간 시작 + 시트 안 칸 (`PerfLabArt`: 공격 16, 세우기 32, 오르기 48, 사격 64, 쓰러짐 96).
- 죽은 적: 고리 버퍼 대신 적마다 쓰러진 시각 하나(`eFallStart`, float)를 그리기 작업 안에서 잰다. 추가 CPU 일 없음, 추가 그리기는 무리마다 시트 하나당 1번.
- 투사체: 프레임 번호 = 무기 번호, 색 칸 = 날아가는 방향. 무기마다 1번 그리고 셰이더가 방향대로 판을 돌린다(`_ShotMode` 2). 그림 없는 무기는 예전 막대(`_ShotMode` 1, 예전 색).
- 병사: 사격·쓰러짐 그림이 있으면 병사 그림을 매 프레임 다시 쓴다(병사 수만큼 메인 스레드 반복 + 업로드). 병사는 수백~수천이라 작다고 봤지만 측정은 못 했다.
- 사다리 소품: 적 수만큼 배열(`ladderInstances`), 사다리 무리 구간만 매 프레임 올린다.

## 3. 바꾼 파일
- Core/Definitions/UnitDef.cs — fireSheet·deathSheet·castSheet·commandSheet·projectileSheet
- Core/Definitions/EnemyDef.cs — attackSheet·deathSheet·deploySheet·climbSheet·ladderSheet
- Combat/PerfLab/PerfLabArt.cs — 칸 구간 상수, Window(), ProjectileLayout(), LadderLayout, LadderDrawSize
- Combat/PerfLab/PerfLabSprite.shader — `_FrameWindow`(기본 0~100000이라 다른 그리기는 그대로), `_ShotMode`·`_ShotColor`, 그림자 패스도 구간 적용 **(카드 허용 목록 밖, 아래 4 참고)**
- Combat/Battle/BattleJobs.cs — EnemyKind 칸 수 필드, SoldierFireJob이 쏜 시각 기록, 적 그리기 작업(쓰러짐·공격·세우기·오르기·사다리), 투사체 그리기 작업(무기 번호·방향) **(허용 목록 밖)**
- Combat/Battle/BattleSim.Poses.cs (새 파일 + .meta) — 동작 그림 상태
- Combat/Battle/BattleSim.cs — MakeKind, 무기→병종 목록, InitPoseArt 호출, 병사 쓰러진 시각, 작업 필드 연결, RefreshSoldierInstances 칸, Dispose
- Combat/Battle/BattleSim.Spells.cs — StartSpellEffect에서 시전 시각 한 줄
- Combat/Battle/BattleView.cs — 동작 시트 그리기, 사다리, 무기별 투사체, 마법사 시전, 주인공 손짓(SetHeroCommandArt, SetHero의 command 인자). **생성자 서명과 BattlePropView·WorldView·Lights·EffectsView 생성 줄은 손대지 않음**
- Combat/Battle/BattleEntry.cs — 뷰 만든 직후 SetHeroCommandArt 한 줄, DrawHero 손짓
- Data/Combat/Units/unit_archer·gunner·cannon_crew·wizard·hero.asset, Enemies/enemy_goblin·goblin_ladder·ogre.asset — 위 표대로 맨 아래 줄 추가
- Docs/collab/rules/CONTRACTS.md — 맨 아래 'C-010 v1.8 초안' 추가

## 4. 확실하지 않은 것 (컴파일·실행을 못 했으니 먼저 볼 곳)
1. **허용 목록 밖 파일 2개**: PerfLabSprite.shader(투사체 회전·칸 구간은 셰이더 없이는 불가), BattleJobs.cs(적 그리기 작업·쏜 시각이 여기 있음). K-031이 셰이더를 고치면 합칠 때 충돌 가능.
2. **K-031 BattleEffectsView와의 약속**: 투사체 개체의 크기(TintSize.w)는 그대로 화살·총알 0.45, 대포알 1.6이라 '맞음 불꽃' 판정은 그대로다. 다만 투사체 PosFrame.w(이제 무기 번호)와 색 rgb(이제 방향)는 바뀌었다 — 효과 쪽이 이 값을 읽으면 어긋난다(지금 코드는 위치·크기만 읽음).
3. 셰이더: 구간 밖 개체를 `(0,0,-2,1)`로 보내 잘라 낸다(그림자 패스가 쓰던 방식). 지도 점 모드도 같은 검사를 거친다 — 지도에서 쓰러지는 개체는 점이 안 나온다.
4. 투사체 회전 방향 부호: 그림이 +X를 보고, 판 오른쪽 = 카메라 오른쪽 기준으로 돌렸다. 화살이 거꾸로 보이면 `d`의 부호를 바꾼다. 그림자 패스는 빛 기준 오른쪽으로 돌아서 그림자 각도는 대충 맞는다.
5. 사다리 소품을 카메라를 보는 판으로 세웠다(성벽 면에 붙이지 않음). 성벽에 가리는지·화면 크기는 캡처로 확인 필요. 세우기는 '오른 높이 0.5m까지'로 정해서 세우는 동안에도 조금씩 올라간다 (전투 계산은 안 바꿈).
6. 쓰러짐: 오르다 죽은 적은 그 높이에서 쓰러짐 그림이 나온다(떨어지는 움직임 없음).
7. 노멀맵: 고블린 근사 시트는 걷기 첫 칸 노멀을 8칸에 깐 것이라 다른 칸·공격 칸과 내부가 안 맞는다. 이상하면 enemy_goblin.normalSheet 한 줄을 지우면 예전으로 돌아간다. 새 노멀 PNG는 기존 archer_static_normal과 같은 가져오기 설정(노멀맵 종류, sRGB 끔).
8. 시험 `BattleArtPreviewTests.SpecialEnemies_WalkFourFrames`는 오우거·사다리 고블린의 보이는 칸이 0~3이라고 본다. 4칸을 다 보기 전에 오우거가 성벽을 치거나 사다리 고블린이 오르면 이제 16번대·32/48번대 칸이 나와 실패할 수 있다(보통은 걷기에서 먼저 끝남).
9. 사격 속도: 8칸/초, 공격 간격이 0.75초보다 짧으면 '칸 수 ÷ 간격'으로 빨라진다. 대포병은 사격 그림이 없어 예전 그대로(대포 몸체·연기는 다른 카드).
10. 새 그림 .meta guid는 직접 만든 값(기존 그림과 같은 방식). 유니티가 처음 가져올 때 다른 .meta 설정 문제가 없는지 확인 필요.
11. ROADMAP.md·ROADMAP.html은 세 카드가 동시에 고치면 충돌해서 이 카드에서는 고치지 않았다 — 합칠 때 조정 담당이 반영해 주세요.
