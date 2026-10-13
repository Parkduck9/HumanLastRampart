# C-005~C-007 초안 r1 — Codex 대체 담당 작성

- 상태: **제안 / 사용자 검토 대기**. 확정 API가 아니며 구현 시작 허가가 아니다.
- 작성: Codex / 2026-10-13
- 기준: d56311779dbf50c84f7cc5e6063dd16db7950e26, D-013~D-015, 승인 C-001~004·010 및 C-003 v1.1.
- 사용자 지시: Claude 토큰 소진으로 Codex가 다음 데이터 약속 작성 인계.
- 이 사본은 Codex 소유다. CONTRACTS.md·ROADMAP.md·ROADMAP.html 반영은 경로별 사용자 허가 확인 후 진행한다.

## C-005 전투 준비 정보 BattleSetup
- 상태: **제안 r1**
- 위치 제안: HLR.Core / HumanLastRampart.Core.CombatContracts (클래스 구현 담당 Claude, 이번에는 문서만 작성).
- 만드는 쪽: App. 읽는 쪽: Combat.

### 전달할 정보
| 항목 | 의미 |
|---|---|
| BattleRunId | 이번 전투 실행의 고유 문자열 ID. App이 매번 새로 발급하며 새 게임에서도 이전 실행과 겹치지 않음 |
| BattleNumber | 시작하는 전투 번호. CampaignState.BattleNumber의 시작 시점 값 |
| WaveDefId | 시작 설정에서 이 전투 번호에 대응하는 웨이브 ID |
| Heroes | 영웅 InstanceId·UnitDefId·Hp·IsAlive·PostId의 복사본 |
| Squads | 분대 SquadId·UnitDefId·Count·PostId의 복사본 |
| Wall | SegmentId·MaxHp·Hp의 복사본 |
| Traps | 고유 배치 ID를 보존한 설치 함정 복사본. 상세 필드는 C-008에서 확정 |
| InitialTargeting | 표적 설정 복사본. 상세 필드는 C-011에서 확정 |

- App은 현재 Campaign과 정의 조회로 준비 정보를 만들고, Battle을 열기 직전 LastBattle을 null로 비운다(승인 C-004 유지).
- 준비 정보의 리스트와 내부 객체까지 복사해 Campaign과 가변 객체를 공유하지 않는다. Combat은 준비 정보를 전투 내부 상태로 다시 복사한다. 전투 손실로 진행 데이터나 ScriptableObject를 직접 변경하지 않는다.
- Combat이 전투 중 바꾼 표적 설정만 승인 C-004대로 Campaign.Targeting에 기록한다. 준비 정보 자체는 변경하지 않는다.
- ID 중복, 누락된 정의/웨이브, Hp 범위, 음수 분대 수, 없는 배치 지점은 시작 전 검증한다. 지점 규격은 전투 설계에서 확정한다. 0명 분대·사망 영웅·미배치 병력의 실제 참가 규칙은 전투 설계에서 정하며 임의로 전장에 넣지 않는다.
- 검증 실패 시 Battle을 열지 않고 메인으로 복귀하거나 시작을 거부한다. 실패 원인을 로그로 남기고 일부 병력만 빼서 전투를 시작하지 않는다.
- 웨이브가 없는 다음 전투 번호의 완료/엔딩 정책은 결정 필요 E3. 마지막 웨이브 자동 반복은 하지 않는다.
- 전달 경로 제안: CampaignState.ActiveBattleSetup을 App이 설정하고, Combat이 OnEnter의 context.Campaign에서 읽는다(C-004 v1.1 추가 제안, 아래 참조). App은 Result/패배 엔딩 처리가 끝날 때까지 유지한다.

## C-006 전투 결과 BattleResult
- 상태: **제안 r1**
- 위치 제안: HLR.Core / HumanLastRampart.Core.CombatContracts.
- 만드는 쪽: Combat. 읽는 쪽: App(승패 분기), Meta.Result(승리 결과 표시·반영), Story(패배 엔딩 재생; 전투 데이터 상세 사용은 C-009에서 정함).

### 전달할 정보
| 항목 | 의미 |
|---|---|
| BattleRunId | 시작 BattleSetup과 같은 실행 ID |
| BattleNumber | **끝난 전투 번호**. 다음 번호로 바뀐 Campaign.BattleNumber를 복사하지 않음 |
| Outcome | Victory 또는 Defeat. 판정 조건은 전투 설계에서 정함 |
| DefeatedEnemyCount | 처치 수. 0 이상 |
| EarnedGold | 얻은 골드. 0 이상. 승리 시만 반영 |
| HeroOutcomes | InstanceId별 HpAfter·IsAliveAfter. 참가 영웅 각각 정확히 한 항목 |
| SquadLosses | SquadId별 LostCount. 시작 Count 이하, 0 이상. 손실 있는 분대만 기록 가능 |
| WallOutcomes | SegmentId별 HpAfter. 시작 Hp 이하, 0 이상. 피해 있는 구간만 기록 가능 |
| ConsumedTrapIds | **발동해 소비된** 함정의 고유 배치 ID 목록. 함정 정의 ID와 다름 |

- BattleSetup의 영웅/분대/성벽/함정 ID로 결과를 식별한다. 같은 병종을 여러 지점에 배치해도 SquadId로 손실 대상을 정확히 맞춘다.
- 목록은 null이 아닌 빈 목록을 허용한다. 동일 ID의 중복 항목, 알 수 없는 ID, 잘못된 범위는 유효한 결과로 받지 않는다. 병종 ID로 대신 매칭하거나 중복을 합쳐 숨기지 않는다.
- 영웅의 사망은 HpAfter=0, IsAliveAfter=false와 일치해야 한다. 부활·전투 중 회복/모집·수리는 현재 초안에 포함하지 않으며 추가 시 결과 규격을 다시 검토한다.
- 소비되지 않는 함정의 반복 발동 횟수와 재사용 여부는 C-008에서 정한다. ConsumedTrapIds에 있는 설치물만 제거하고 모든 발동 함정을 임의로 삭제하지 않는다.
- Combat은 전투 종료 시 결과를 한 번 완성하고 Campaign.LastBattle에 기록한 뒤 Complete(Done)를 보낸다. 이후 결과를 수정하지 않는다.
- App은 실행 ID와 끝난 번호, 승패와 결과 형식을 먼저 검사한다. null/이전 실행/잘못된 결과이면 승리·패배 분기를 진행하지 않고 에러를 기록해 메인으로 복귀한다.
- **승리:** C-004에 따라 App이 Campaign.BattleNumber를 끝난 번호+1로 한 번 증가시키고 Result를 연다.
- **패배:** 전투 번호를 증가시키지 않고 StoryRequest.Kind=DefeatEnding으로 패배 엔딩을 연다. Result를 건너뛰며 보상·손실·함정 소비를 Campaign에 반영하지 않는다(D-014). 엔딩 종료 후 Campaign을 폐기한다.

## C-007 결과 반영 규칙
- 상태: **제안 r1**
- 담당: Meta.Result(Codex). App은 흐름과 전투 번호만 관리한다. Combat은 결과만 만든다.

### 반영 시점과 중복 방지
- Result.OnEnter에서 승리 결과를 검사하고 **표시 전에 한 번** 반영한다(E1 제안). 다음 버튼은 반영을 다시 실행하지 않고 Complete(Done)만 보낸다.
- 반영 대상: 골드, 영웅 체력·생존, 분대 인원, 성벽 체력, 소비된 함정.
- 현재 ActiveBattleSetup과 LastBattle의 BattleRunId·BattleNumber가 모두 같아야 한다.
- 승리 Result에서 Campaign.BattleNumber는 LastBattle.BattleNumber+1이어야 한다. 현재 번호와 결과 번호가 같아야 한다고 검사하면 승인 C-004의 전투 번호 증가 시점과 충돌하므로 **끝난 번호+1**로 검사한다.
- LastAppliedBattleRunId가 같은 결과의 실행 ID이면 **이미 반영됨**으로 처리해 아무 값도 다시 변경하지 않는다. 화면에는 보관된 결과를 표시할 수 있다.
- App이 새 게임을 만들 때 반영 표시는 비운다. 전투마다 새로운 실행 ID로 다른 게임의 1번 전투 결과가 섞이지 않게 한다.
- LastBattle/ActiveBattleSetup은 Result 화면과 이후 Prep에서 반영 검사·표시를 마칠 때까지 보존하고, 다음 Battle 시작 때 새 준비 정보를 설정하며 LastBattle을 비운다.

### 모든 값 검증 → 새 상태 준비 → 한 번에 반영
1. 실행 ID·번호·Victory 여부·중복 여부 검사.
2. 모든 영웅/분대/성벽/함정 ID와 수치 범위를 시작 스냅샷 및 현재 Campaign과 대조. 결과 반영 전 Campaign의 해당 상태가 시작 상태와 다르면 잘못된 재반영/예상 밖 변경으로 거부한다.
3. 정비 이전의 새 리스트와 최종 골드를 메모리에서 준비한다. Gold+EarnedGold 정수 범위 초과도 먼저 검사한다. 이 단계에서는 Campaign을 수정하지 않는다.
4. 검증·준비가 모두 성공하면 동기 처리 한 구간에서 준비된 값들을 Campaign에 설정하고 **마지막에** LastAppliedBattleRunId를 기록한다. 중간에 기다리거나 화면 전환·외부 콜백을 호출하지 않는다.
5. 실패하면 준비한 값을 폐기한다. Campaign의 골드·병력·성벽·함정·반영 표시는 그대로 두고, 다음 버튼을 비활성화해 정비 진입을 막는다. 오류 로그 후 메인 복귀만 허용하는 것을 제안한다(E2).

### 반영 내용
- 골드: 현재 Gold + EarnedGold.
- 영웅: 참가 영웅별 Hp·IsAlive만 갱신. InstanceId·UnitDefId·PostId는 유지. 사망 영웅은 표시를 위해 목록에 남긴다; 이후 회복/퇴장 정책은 정비 설계에서 결정한다.
- 분대: 시작 Count에서 LostCount를 뺀다. 0명 분대는 승인 C-004대로 목록에서 제거한다. 살아남은 분대의 SquadId·UnitDefId·PostId는 유지한다.
- 성벽: 결과가 보고한 구간의 Hp를 HpAfter로 갱신. MaxHp·SegmentId는 유지한다.
- 함정: ConsumedTrapIds와 일치하는 설치물만 목록에서 제거한다. 소비되지 않은 설치물의 위치·정의는 유지한다. 전투 결과가 Prep의 구매 비용을 다시 청구하지 않는다.
- 표적 설정: Combat이 갱신한 값 유지. Result가 시작 스냅샷 값으로 되돌리지 않는다.
- 실패 엔딩: Result를 열지 않으므로 이 반영 절차를 실행하지 않는다.

## 연결 변경 제안 — C-004 v1.1 (별도 승인 필요)
기존 승인 C-004 r3는 수정·대체하지 않고 다음 두 정보를 추가하는 새 버전을 제안한다.

| 추가 정보 | 초기값 | 작성 권한 |
|---|---|---|
| ActiveBattleSetup | null | App: 시작 준비 및 다음 전투 전 교체. 다른 화면은 읽기만 |
| LastAppliedBattleRunId | null | App: 새 게임 초기화. Meta.Result: 성공 반영 직후 한 번 기록 |

- 이 변경과 C-005~007을 함께 승인해야 제안한 전달·중복 방지 방식이 확정된다.
- 새로운 타입·필드·위치의 실제 C# 구현은 승인 이후 담당자 작업이며 이번 문서는 구현하지 않는다.
- C-008의 함정 고유 배치 ID·소비 여부, C-011의 표적 설정, 전투 배치 지점과 승패 조건은 해당 약속/설계 승인 전까지 미정이다.

## 사용자 결정 항목
| ID | 질문 | 제안 |
|---|---|---|
| E1 | 승리 보상·손실은 언제 실제 반영할까? | 결과 화면이 열릴 때 한 번 반영. 다음 버튼은 정비 이동만 |
| E2 | 잘못된 결과/반영 실패 시 어떻게 할까? | 아무 값도 부분 반영하지 않고 에러 기록. 정비 이동 차단 후 메인으로 복귀 |
| E3 | 설정된 마지막 전투를 이긴 뒤 다음 웨이브가 없으면? | 첫 완주 검증용으로 최소 두 전투의 웨이브를 준비. 그 이후는 메인으로 복귀하며 승리 엔딩 확장은 다음 범위에서 결정. 최종 전투 결과·정비 표시는 유지 |
| E4 | 전달·중복 방지를 위한 C-004 v1.1 두 필드 추가? | ActiveBattleSetup·LastAppliedBattleRunId 추가. 실행 ID와 끝난 전투 번호 둘 다 검사 |

## 문서 검토 사례 (실제 런타임 시험 아님)
| 사례 | 기대 정책 |
|---|---|
| 1번 승리, Campaign.BattleNumber=2 | 결과 번호 1/실행 ID 대조 후 반영 허용 |
| 같은 결과 화면 재진입·버튼 연타 | LastAppliedBattleRunId 대조로 골드·손실 재반영 없음 |
| 새 게임 1번에 이전 게임 1번 결과 도착 | 실행 ID 불일치로 거부 |
| 같은 병종의 두 분대 중 하나만 손실 | SquadId에 맞는 분대만 인원 감소 |
| 함정 A 소비, 함정 B는 재사용 | A만 제거, B 유지 |
| 목록 마지막 항목이 잘못됐거나 골드 넘침 | 검증 단계 거부; 골드·병력·벽·함정 모두 미변경 |
| 패배 | 결과 반영 없이 패배 엔딩, 종료 후 진행 데이터 폐기 |

- 한계: 문서 정합성만 검토. 코드·Unity Play·런타임 동작 검증은 수행하지 않음.
