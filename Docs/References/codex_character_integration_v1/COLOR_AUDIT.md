# 최신 캐릭터 색 시트 통합 감사 (2026-10-21)

사용자 지시: 첨부한 첫 generated.png(D-080 정정·D-081)를 기준으로 제작한 캐릭터를 기록한 뒤 게임에 적용하고 미적용 상태를 확인한다. 범위는 K-073~080의 색 시트35개이며, 점화병은 더 최신인 T-CX-094/K-086 v5로 예외 처리한다. 아트 생성·재편집 없이 원본 PNG의 바이트 복사만 수행했다.

## 수행 결과

- 교체 전 규격·SHA-256·GUID를 color_audit.json에 먼저 보존했다.
- 35개 중 최신과 이미 동일19개(점화병 v5 포함), 이전 그림14개 교체, 누락2개 추가.
- 최종35개 모두 최신 출처와 SHA-256 동일, 기존 GUID 전부 유지, 신규2개.meta 생성. 기존폴더만 사용했다.
- 신규 `Characters/Static/wizard_walk.png`는 `unit_wizard.runSheet`에 연결. 신규 `Characters/Walk/goblin_wallhit4.png`는 `enemy_goblin.attackSheet`에 연결.
- 기존 대포병 점화2592×384는 구형K075/K081의1728×384로 되돌리지 않았다.
- 노멀맵 참조·런타임 코드·Unity 실행·Git 쓰기는 이 담당이 하지 않았다. root의 별도 연결/검증 결과와 합쳐야 하며, 아래는 색 연결 직후 상태이다.

## 실제 사용에서 남은 지점

1. 마법사 걷기는 정의의runSheet를 연결했지만, BattleView의 별도 마법사 renderer가 대기/시전만 선택하므로 이동상태 분기가 필요하다. 그림은 오른쪽을 보며 PerfLabArt.RunFacesRight가 최신규격을 인식한다.
2. 대포병 밀기/당기기는 Resources 경로로 자동 로드된다. 새288칸/발360은 자동 인식되지만 기존운반화소상수(인물210, 걸음32)와 새그림(인물219, 걸음36)의 접지·포가손위치 정합은 별도로 확인해야 한다.
3. 고블린 wallhit는 기존 EnemyDef.attackSheet 우선 규칙으로 토벽뿐 아니라 성벽과 성벽위 공격에도 사용된다. 토벽전용을 원하면 별도상태분기가 필요하다.
4. wizard_static·ladder_goblin_static·ogre_static은 다른시점/정적참고변형, ladder_goblin_carry_run4·ladder_side는 별도운반/측면상태용이다. 기존런타임이 이5개를선택하지않는다. 폴더복사완료를 모든상태사용완료로간주하지않는다.

## 시트별 연결

| 시트 | 기존 → 최신 전체크기 | 처리 | 현재 연결 |
|---|---|---|---|
| archer_static.png | 288×384 → 288×384 | same | unit_archer.asset:sheet |
| archer_run.png | 1152×384 → 1152×384 | same | unit_archer.asset:runSheet |
| archer_fire.png | 1728×384 → 1728×384 | same | unit_archer.asset:fireSheet |
| archer_fall.png | 1152×384 → 1152×384 | same | unit_archer.asset:deathSheet |
| gunner_static.png | 288×384 → 288×384 | same | unit_gunner.asset:sheet |
| gunner_run.png | 1152×384 → 1152×384 | same | unit_gunner.asset:runSheet |
| gunner_fire.png | 1728×384 → 1728×384 | same | unit_gunner.asset:fireSheet |
| gunner_fall.png | 1152×384 → 1152×384 | same | unit_gunner.asset:deathSheet |
| cannon_crew_static.png | 288×384 → 288×384 | same | unit_cannon_crew.asset:sheet |
| cannon_crew_run.png | 1152×384 → 1152×384 | same | unit_cannon_crew.asset:runSheet |
| cannon_crew_ignite6.png | 2592×384 → 2592×384 | same | unit_cannon_crew.asset:igniteSheet |
| cannon_crew_fall.png | 1152×384 → 1152×384 | same | unit_cannon_crew.asset:deathSheet |
| crew_push6.png | 2304×384 → 1728×384 | replace | CannonArt.PushSheet → Resources.Load in BattlePropView |
| crew_pull6.png | 2304×384 → 1728×384 | replace | CannonArt.PullSheet → Resources.Load in BattlePropView |
| wizard_back_static.png | 256×384 → 288×384 | replace | unit_wizard.asset:sheet |
| wizard_static.png | 256×384 → 288×384 | replace | 파일보존, 기존runtime선택없음 |
| wizard_cast.png | 1024×384 → 1152×384 | replace | unit_wizard.asset:castSheet |
| wizard_walk.png | 없음 → 1152×384 | add | unit_wizard.asset:runSheet (renderer분기필요) |
| goblin_walk_attack4.png | 1152×768 → 1152×768 | same | enemy_goblin.asset:sheet |
| goblin_fall4.png | 1152×384 → 1152×384 | same | enemy_goblin.asset:deathSheet |
| goblin_wallhit4.png | 없음 → 1152×384 | add | enemy_goblin.asset:attackSheet |
| ladder_goblin_static.png | 288×384 → 288×384 | same | 파일보존, 기존runtime선택없음 |
| ladder_goblin_walk.png | 1152×384 → 1152×384 | same | enemy_goblin_ladder.asset:sheet |
| ladder_goblin_carry_run4.png | 1152×384 → 1152×384 | same | 파일보존, 기존runtime선택없음 |
| ladder_goblin_deploy4.png | 1152×384 → 1152×384 | same | enemy_goblin_ladder.asset:deploySheet |
| ladder_goblin_climb4.png | 1152×384 → 1152×384 | same | enemy_goblin_ladder.asset:climbSheet |
| ladder_front.png | 256×1536 → 256×1536 | replace | enemy_goblin_ladder.asset:ladderSheet |
| ladder_side.png | 256×1536 → 256×1536 | replace | 파일보존, 기존runtime선택없음 |
| ogre_static.png | 256×384 → 256×384 | replace | 파일보존, 기존runtime선택없음 |
| ogre_walk.png | 1024×384 → 1024×384 | replace | enemy_ogre.asset:sheet |
| ogre_attack4.png | 1024×384 → 1024×384 | replace | enemy_ogre.asset:attackSheet |
| ogre_fall4.png | 1024×384 → 1024×384 | replace | enemy_ogre.asset:deathSheet |
| hero_back_idle.png | 256×384 → 288×384 | replace | unit_hero.asset:idleSheet |
| hero_walk_run_grid.png | 1024×1536 → 1152×1536 | replace | unit_hero.asset:sheet |
| hero_command.png | 768×384 → 864×384 | replace | unit_hero.asset:commandSheet |

전체 출처경로·기존/최신해시·GUID·참조는 `color_audit.json`을 따른다. 런타임 변경과 실제 Unity 검증 결과는 통합담당의 후속 보고를 따른다.
