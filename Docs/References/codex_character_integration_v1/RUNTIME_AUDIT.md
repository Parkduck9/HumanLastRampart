# 캐릭터 런타임 연결 읽기 검토

- 작성: Codex 보조 검토 / 2026-10-21
- 범위: K-081 `manifest_current.json` 35개 시트와 UnitDef·EnemyDef·BattleView·BattleSim.Poses·BattleJobs·BattlePropView·PerfLabArt 대조.
- 이 검토자는 Game 소스·에셋을 수정하지 않았고 Unity·Git을 실행하지 않았다. 아래는 병행 통합 **시작 시점**의 관찰이며 최종 통합 시험 결과를 대신하지 않는다.
- 기준: 사용자 선택 generated.png, D-081 일반 인물 96×128 → 288×384·발 (144,360). 오우거는 별도 예외 규격. 새 외형 제작·설계 변경을 승인한 것으로 해석하지 않는다.

## 확인 결과와 최소 수정 위치

| 대상 | 관찰 | 필요한 연결 |
|---|---|---|
| 주인공 3종 | Game에 기존 256칸(서기256×384, 이동1024×1536, 명령768×384)이 남아 있었다. UnitDef GUID 참조 자체는 존재 | K-080 최신 288칸 3종으로 같은 GUID 파일 교체. HeroLayoutOf는 최신 칸/발 지원. BattleEntry.DrawHero의 달리기 오른쪽 flip은 K-080 왼쪽 원본과 일치 |
| 마법사 4종 | 앞·뒤서기/시전은 이전 256칸, wizard_walk는 Game에 없음. 전투 마법사는 별도 렌더라 일반 병사 runSheet 설정만으로 걷지 않는다 | K-076 색·노멀 교체. UnitDef.runSheet=wizard_walk. BattleView 마법사 전용 이동 block을 추가해 WizardMoving 동안 4칸 재생·정지 시 기존 대기/시전 선택. WizardFeet 변위와 카메라 right 내적으로 flip. 실제 원본은 **오른쪽을 봄**(이미지 직접 확인) |
| goblin_wallhit4 | manifest current_game=null. 기존 EnemyDef.attackSheet 없었음 | 새 PNG·meta와 노멀을 추가하고 enemy_goblin.attackSheet 연결. MakeKind → EnemyKind.AttackFrames → EnemyInstanceJob → BattleView.AddPose 경로가 이미 있어서 새 색 필드는 불필요 |
| 사다리 운반 | carry_run4 PNG는 있지만 enemy_goblin_ladder.sheet는 사다리를 들지 않은 walk를 참조 | 기존 sheet를 carry_run4로 연결하면 접근/걷기 상태의 운반 노출. deploy/climb는 기존 분리 시트 유지. 도감용 static·대체 walk와 실제 접근 그림을 구분해 문서화 |
| 대포 밀기·당기기 | Resources 직접 로드로 실행 경로 존재. 기존 Game은 2304×384(384칸), 최신은1728×384(288칸) | 최신 PNG 교체 시 FromSheet/FrameCount는 6칸·발360 지원. 하지만 BattlePropView crewScale=384/210 고정은 최신 인물219px와 불일치. pull/push 각각 SheetScale(sheet) 사용 |
| 견인 접지 | DrawHauls의 step은 기존32px × soldierHeight/210. 최신 K-075 manifest는36px/프레임·인물219px | 최신 crew에만 step=36×soldierHeight/219 적용. 포가·바퀴 원본 좌표는 예전 체계라 전체 CannonArt.MetersPerPx를 무조건 바꾸지 말 것. 사람 접지와 포가·바퀴 축척/파지 접점을 실제 사진으로 별도 검증 |
| 점화병 | 최신 K-086 v5 2592×384, 432칸·pivotX=2/3 예외 | K-081의 옛1728 점화로 덮어쓰면 안 됨. IsExpandedIgnite 이름+실크기 판별, figure219·발360·0.23m 배치 보정 유지. 대응 최신 노멀 없으면 null이 더 정확 |
| 오우거4종 | 원본128×192, 게임256×384·발128,352·최근접2배. 최신 그림도 일반288칸이 아님 | 기존 256 parser 유지. K-079 큰 체형을 288 규격으로 강제 변환하지 말 것. 현재 코드의 기존 자동 높이표(173/233/201/206.8)는 최신 그림의 동작 간 체형 동일성을 보증하지 않으므로 실제 전환 사진으로 확인 |

## 노멀맵 연결 누락

현재 UnitDef의 NormalSheet·IdleNormalSheet·RunNormalSheet와 EnemyDef.NormalSheet만으로는 모든 동작의 노멀을 표현할 수 없다.

- UnitDef: FireNormalSheet, DeathNormalSheet, CastNormalSheet, CommandNormalSheet, IgniteNormalSheet 대응 직렬화 필드/읽기 속성 추가가 필요하다. 점화는 최신 v5와 맞는 노멀만 허용한다.
- EnemyDef: AttackNormalSheet, DeathNormalSheet, DeployNormalSheet, ClimbNormalSheet 대응 필드/속성이 필요하다.
- BattleView.AddPose는 현재 ApplyFigure(block, sheet, **null**, referencePx)를 호출한다. normal 선택 인수를 추가하고 각 정의의 대응 노멀을 넘겨야 한다. 인수 순서를 바꾸면서 referencePx를 잘못 바인딩하지 않도록 호출을 함께 수정한다.
- 마법사 cast는 ApplyFigure(wizardCastBlock, CastSheet)만 호출한다. CastNormalSheet를 전달한다. 이동 block은 RunNormalSheet를 전달한다.
- BattleEntry의 SetHeroCommandArt(CommandSheet)는 CommandNormalSheet를 함께 전달해야 한다. 서기·이동 hero 노멀은 생성자에 이미 연결돼 있다.
- BattlePropView의 Apply에는 현재 _NormalTex/_HasNormal 처리 자체가 없다. crew_pull6/crew_push6 노멀을 Resources.Load한 뒤 해당 block에 적용하는 최소 경로가 필요하다. 포가/바퀴 노멀을 없는 상태에서 임의 생성하거나 이전 색의 노멀로 대신하지 않는다.

## 규격·방향 위험

1. IsSixSheet는 `width % 288 == 0 && width % 256 != 0` 휴리스틱이다. 이번 최신 일반 시트의 1·3·4·6칸은 모두 판별된다. 8칸 단일행2304px 같은 미래 시트는 겹치므로 별도 명시가 필요하다. 오우거/사다리 소품/점화 예외는 유지한다.
2. RunFacesRight는 모든 Six 시트를 오른쪽으로 본다고 판정한다. 병사 최신 달리기와 wizard_walk에는 부합하지만 **K-080 hero의 옆행은 왼쪽**이므로 hero에 이 일반 함수로 방향을 통합하면 안 된다.
3. FireFacesRight는 최신 gunner_fire만 오른쪽, archer_fire는 왼쪽으로 처리한다. 이 분기는 기존 BattleSim.Poses에 전달돼 있다.
4. ResultBoard.Portrait는 폭/256과 고정 aspect2/3를 사용한다. 현재 서기1칸은 UV가 맞지만 최신288/384의 실제 비율0.75를0.667로 눌러 그린다. Meta 모듈은 Combat에 의존시키지 말고 로컬 칸 크기 분기 또는 Core의 공통 데이터로 폭/높이를 맞출 것.
5. wizard_static(앞서기), ladder_goblin_static, ogre_static, ladder_side는 저장되어 있어도 현재 전투 전용 상태/정의에서 소비되지 않는다. 대기/앞보기/측면사다리를 쓰는 상태가 없는데 억지로 번갈아 노출하면 의미가 달라진다. 실제 사용 대상과 예비 그림을 최종 목록에서 구분해야 한다.

## 검증 권장 (미실행)

- 실제 정의에서 모든 색/노멀 크기·알파·GUID 짝 확인, parser 열/행/발/키·v5 점화 예외 확인.
- 이동하는 마법사의 양방향(4면 및 모서리)과 정지/시전 전환, 사다리 접근→세우기→등반, 고블린 걷기→성벽 타격을 실제 카메라로 촬영.
- 대포 견인의 사람 6칸·손잡이·발접지·바퀴 회전을 양방향으로 확인. 36px 시트 교체만으로 파지점 정합까지 자동 통과했다고 기록하지 않는다.
- 이 문서는 정적 코드 검토이며 위 검증 통과를 주장하지 않는다. 최종 시험은 통합 담당 결과로 별도 기록한다.

## 후속 허가 범위 수정 — 결과 초상 비율

- root의 경로별 허가를 받아 `Meta/ResultBoard.cs`의 Portrait 함수만 수정했다. 최신288칸은 aspect0.75·발360, 기존256칸은 aspect2/3·발352로 처리한다. 기존 행 중심을 기준으로 계산한 발선에 pivot을 놓아 새 그림의 하단 여백8px 차이도 맞췄다. 기존256 그림의 사각형 위치·크기·UV는 그대로다.
- 별도 `App/Tests/ResultPortraitLayoutTests.cs`와 meta를 추가했다. 최신 정적1칸·최신4×4·기존4×4·IdleSheet 우선의 네 사례에서 실제 비공개 Portrait 호출 결과의 가로세로 비율·발선·첫칸UV·선택 텍스처를 검사한다. Meta/UI asmdef 참조 변경 없이 reflection으로 호출한다.
- Unity·Git은 실행하지 않았다. 이 네 시험은 통합 담당이 실행해야 하며 아직 통과로 기록하지 않는다. 단순 계산상 기존256 사각형 하단은 `at.y-height/2`, 신규288 발선은 기존과 동일하다.
