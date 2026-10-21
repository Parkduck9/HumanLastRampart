# 최신 캐릭터 법선 연결 자료 — 2026-10-21

## 복사 결과

- 기준: `Docs/References/codex_k081_v1/manifest_current.json`, 35시트·135프레임.
- 제작 원색과 법선의 크기·알파 일치 및 원색 manifest SHA256을 35/35 확인했다. 기존 기술 검증 210/210과 별도로 이번에 직접 재검사했다.
- **34시트 법선을 Game의 해당 원색과 같은 폴더에 그대로 복사했다.** 픽셀 재처리·그림 생성·색 변환은 하지 않았다. 복사 후 소스와 출력 SHA256이 모두 일치했다.
- 복사 시점 Game 원색 34개와 법선 크기·알파가 모두 일치했다. 정확한 경로·해시는 `normal_audit.json`에 있다.
- 기존 법선 PNG 7개는 기존 `.meta`를 건드리지 않아 GUID가 유지됐다. 신규 법선 27개는 부모가 Unity 임포트와 `.meta` 설정을 처리한다.
- `crew_push6`·`crew_pull6`는 원색과 동일한 `Art/Battle/Resources/BattleArt/Cannon/`에 두었다. `goblin_wallhit4_normal.png`는 색 담당과 협의한 `Art/Characters/Walk/`에 두었다.
- 정의 asset, 코드, 기존 `.meta`, Git, Unity 실행은 이 작업에서 변경·수행하지 않았다. **PNG 설치 완료와 런타임 연결 완료는 구분한다.**

## 의도적으로 보류한 1시트

`cannon_crew_ignite6_normal.png`는 K081 구형 원색 1728×384와 짝이다. Game에 적용된 T-CX-094 v5 원색은 2592×384다. 서로 알맞지 않으므로 **구형 법선을 Game에 복사하거나 연결하지 않았다.** 현재 v5 원색·접점 보정을 유지한다. v5 전용 법선이 필요하면 그 최신 원색을 기준으로 별도 제작·검증해야 한다.

## 부모 작업: 정의와 렌더러 연결

현재 UnitDef에는 `normalSheet`, `idleNormalSheet`, `runNormalSheet`가 있고 EnemyDef에는 `normalSheet`만 있다. 현재 노멀 참조 필드로 연결 가능한 기본값은 아래와 같다.

| 정의 | 필드 | 법선 PNG |
|---|---|---|
| unit_hero | normalSheet | Hero/hero_walk_run_grid_normal.png |
| unit_hero | idleNormalSheet | Hero/hero_back_idle_normal.png |
| unit_archer | normalSheet | Static/archer_static_normal.png |
| unit_archer | runNormalSheet | Run/archer_run_normal.png |
| unit_gunner | normalSheet | Static/gunner_static_normal.png |
| unit_gunner | runNormalSheet | Run/gunner_run_normal.png |
| unit_cannon_crew | normalSheet | Static/cannon_crew_static_normal.png |
| unit_cannon_crew | runNormalSheet | Run/cannon_crew_run_normal.png |
| unit_wizard | normalSheet | Static/wizard_back_static_normal.png |
| unit_wizard | runNormalSheet（이동 원색이 wizard_walk일 때） | Static/wizard_walk_normal.png |
| enemy_goblin | normalSheet | Goblin/goblin_walk_attack4_normal.png |
| enemy_goblin_ladder | normalSheet（carry_run4를 기본 원색으로 쓸 때） | Ladder/ladder_goblin_carry_run4_normal.png |
| enemy_ogre | normalSheet（ogre_walk를 기본 원색으로 쓸 때） | Walk/ogre_walk_normal.png |

다음 짝 법선은 PNG는 설치했으나 현재 정의에 대응 필드가 없어 부모의 코드·정의 연결이 필요하다. 동작 원색을 바꿀 때 이전 기본 법선이 잘못 남지 않도록 각 재질 블록이 해당 동작의 짝을 받게 해야 한다.

- 아군 `fireSheet` → archer_fire_normal / gunner_fire_normal.
- 아군 `deathSheet` → archer_fall_normal / gunner_fall_normal / cannon_crew_fall_normal.
- 마법사 `castSheet` → wizard_cast_normal.
- 주인공 `commandSheet` → hero_command_normal.
- 적 `attackSheet` → goblin_wallhit4_normal / ogre_attack4_normal.
- 적 `deathSheet` → goblin_fall4_normal / ogre_fall4_normal.
- 사다리 적 `deploySheet`, `climbSheet`, `ladderSheet` → 각각 ladder_goblin_deploy4_normal / ladder_goblin_climb4_normal / ladder_front_normal.
- 끄는 대포의 crew_push6 / crew_pull6 → 동일 폴더의 짝 법선. `BattlePropView`의 소품 재질 경로는 별도로 확인한다.

wizard_static, ladder_goblin_static, ladder_goblin_walk, ladder_side, ogre_static는 기본 원색으로 선택되지 않으면 대체 시안/자료다. 설치됐다는 이유만으로 현재 게임에서 보이는 상태라고 기록하지 않는다. 기존 `Ladder/ladder_goblin_carry_normal.png`는 별도 옛 원색의 짝이며 이번 새 run4 시트와 섞어서 쓰지 않는다.

## 검증 범위와 한계

법선은 3D 베이크가 아닌 기존 K081의 근사 기술 법선이다. 원색/알파/시트 정합과 무손실 복사만 검증했다. 실제 조명·동작별 UV·발 피벗·게임 촬영·정의 참조 검증은 부모의 최종 통합 시험 결과를 따른다. 본 문서의 수치를 Unity 테스트 통과 수로 해석하지 않는다.

`audit_normals.py`는 재현용 exact-copy 도구다. Game 쓰기가 포함되므로 Unity 시험 중에는 실행하지 않는다. `normal_audit.json`은 최초 복사 시점의 기록이다.
