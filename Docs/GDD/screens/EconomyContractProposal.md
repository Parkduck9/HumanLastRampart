# 모집·수리 데이터 약속 제안 r1

- 작성자: Codex / T-CX-023 / 2026-10-14
- 상태: **제안. 확정 API·구현 승인이 아님.**
- 근거: D-042 잠정 가격 A, M-CL-038 Core 검토 요청, 기존 C-001·C-002·C-004·C-010.
- 정비 화면 설계 승인은 별도다. 이 문서로 Game·Core·카탈로그를 수정하지 않는다.

## 1. 작성자와 위치

| 대상 | 제안 위치 | 작성자 | 역할 |
|---|---|---|---|
| PreparationEconomyDef | Core/Definitions/ | Claude | DefinitionBase를 상속하는 가격·정원 정의 타입 |
| PreparationEconomyRules | Core/Economy/ | Claude | Campaign을 바꾸지 않는 순수 가격 계산·정의 검사 |
| economy_preparation_1.asset | Data/Meta/ | Codex | D-042 값으로 만든 1회차 가격 정의 |
| MetaCatalog 등록 | Data/Meta/ | Codex | 위 정의를 기존 DefinitionCatalog에 등록 |
| 카탈로그 Boot 연결·시작 검사 | 기존 App/Boot 소유 경로 | Claude | 메인 전에 누락·중복·잘못된 가격 차단 |
| 정비 견적·구매 처리와 UI | Meta/ | Codex | 현재 상태 검사·확인·일괄 반영 |

Core는 Meta를 참조하지 않는다. Meta는 기존 Core 참조로 계산과 정의를 사용한다. 새 서비스 수명·어셈블리·씬·패키지는 추가하지 않는다. Data/Meta는 C-001의 Codex 소유 경로다. Core·App·Boot 수정은 Claude에게 요청하며, 공용 경로 변경이 생기면 별도 예약을 받는다.

## 2. 가격 정의

단 하나의 정의 ID `economy_preparation_1`을 추천한다. 실행 중 선택 가능한 A/B 옵션으로 만들지 않고, 승인된 A를 데이터로 둔다.

| 필드 | 값/형식 | 검사 |
|---|---|---|
| TierStartCounts | int[] = 0, 600, 1200 | 첫 값0, 엄격한 증가, 각 값 GlobalCapacity 미만 |
| ArcherPrices | int[] = 5, 8, 12 | 단계 수 일치, 모두 양수 |
| GunnerPrices | int[] = 8, 12, 18 | 단계 수 일치, 모두 양수 |
| CannonCrewPrices | int[] = 10, 15, 20 | 단계 수 일치, 모두 양수 |
| GlobalCapacity | 2000 | 양수, 이번 1회차 시험 한도 |
| WallCapacity | 500 | 양수, 전체 한도 이하 |
| SquadPostCapacity | 160 | 일반 분대 지점 한도 |
| CannonCrewPostCapacity | 5 | 대포 한 지점 운용병 한도, 대포 수 증가에 사용하지 않음 |
| RepairHpPerGold | 10 | 양수 |
| DefaultRepairHp | 100 | 양수 |

동일 정의 타입이 0개·2개 이상, ID가 다름, 배열 길이 불일치·음수·0·중복 경계·비정상 정원이면 데이터 오류다. 정의 불일치를 임의 기본값으로 숨기지 않는다.

`UnitDef.RecruitCost`는 기존 필드로 보존한다. 이 정비 기능의 실제 견적에는 새 정의의 병종별 단계 가격만 사용하고 기존 값으로 되돌아가는 fallback을 만들지 않는다. Boot의 기존 UnitDef 검사는 유지한다. 영웅·마법사·미지원 병종을 일반 병사 가격으로 구매하지 않는다.

## 3. 순수 계산 인터페이스 후보

아래는 Claude가 검토할 형태 제안이며 아직 호출 가능한 API가 아니다.

```csharp
public static bool TryQuoteRecruit(
    PreparationEconomyDef rules, UnitKind kind,
    int ownedSoldierCount, int requestedCount,
    out int goldCost, out string reason);

public static bool TryQuoteRepair(
    PreparationEconomyDef rules, int currentHp, int maxHp,
    int requestedHp, out int actualRepairHp,
    out int goldCost, out string reason);
```

첫 범위는 가격과 전체 인원 범위만 Core 계산에 둔다. 지점·성벽·분대 소유 상태와 거래 반영은 Meta가 검사한다. 함수가 골드나 Campaign의 목록을 바꾸거나 ID를 발급하지 않는다. 정상적인 골드 부족·정원 부족은 사용자 안내 사유이고, 손상된 정의·잘못된 ID는 ReportDataError로 전달할 데이터 오류다. 순수 함수의 false 사유와 UI 오류 종류를 구현 시 구분한다.

인원·골드 중간 합계는 long 또는 checked 연산으로 계산하고 int 범위 밖이면 실패한다. 음수 인원·요청·HP, 현재HP가 최대보다 큼, 미지원 병종은 실패한다. 0명 구매·0HP 요청은 UI에서 비활성화하고, 함수는 비용0·변화0으로 처리한다. 만피 성벽도 실제 복구0·비용0이다.

## 4. 모집 계산과 배치

- 전체 일반 병력은 Campaign.Squads의 모든 Count 합계다. 궁수·총병·대포 운용병을 포함하며 영웅·마법사는 제외한다. 대기 분대가 있으면 전체 한도에는 포함하지만 해당 성벽 인원에는 배치된 분대만 센다.
- 구매 직전 전체 인원으로 각 1명의 단계 가격을 적용한다. 묶음 구매는 경계까지 인원 수로 나누어 합산하며 병사 1명마다 UI/GameObject를 생성하지 않는다.
- 예: 궁수 599명에서 2명 구매는 5+8=13골드. 1199명에서 2명 구매는 8+12=20골드. 590명에서 30명을 한 번에 사거나 10+20으로 나누어도 총액은 같다.
- 전체2000·성벽500·일반지점160·대포지점5를 모두 검사한다. 성벽은 PostDef.WallId로 찾고, 같은 지점에 둘 이상의 분대가 있으면 데이터 오류다.
- 첫 구현은 **선택한 지점으로 직접 보충/모집**을 제안한다. 기존 분대는 동일 UnitDefId만 보충한다. 기존 병종 교체·분대 합치기·새 대기창은 후속 범위다.
- 비어 있는 일반 지점은 궁수/총병 중 허용 병종으로 새 분대를 만든다. 대포 지점은 운용병만, 최대5명까지 복원한다. 전멸해 제거된 운용병 분대도 같은 대포 지점으로 재모집할 수 있다. 대포 지점은 기존4개만 사용한다.
- 새 SquadId는 성공 반영에 필요한 값으로만 발급한다. 구매 실패로 NextInstanceNumber가 증가하거나 Count0인 분대가 남지 않는다.

## 5. 수리 계산

실제복구 = min(요청HP, 최대HP−현재HP). 비용 = 실제복구를 RepairHpPerGold로 나눈 올림값. 벽별로 계산해 합산한다.

- 5HP 손상, 100HP 요청: 실제5HP·1골드.
- 11HP 손상: 실제11HP·2골드.
- 두 벽이 각각1HP 손상: 각각1골드, 합2골드. 전체2HP를 합쳐1골드로 계산하지 않는다.
- 기본100HP와 선택 벽 최대수리 버튼을 제안한다. 전체수리는 별도 버튼을 승인하는 경우 같은 방식으로 벽별 견적을 합산한다.
- 골드가 부족하면 일부 HP만 몰래 수리하지 않는다. 전체수리는 네 벽 모두 검증·계산 후 한 번에 적용하거나 아무것도 바꾸지 않는다.

## 6. 견적·반영과 중복 입력

Meta는 화면 진입 시 가격 정의를 확보하고, 사용자가 지점/벽·수량을 바꾸면 현재 상태에서 견적을 만든다. 확인 화면에는 실제 구매/복구량·비용·잔액·단계 구간·자리 한도를 보여준다.

실행 직전에 현재 골드·전체/성벽/지점 인원·병종·HP·정의와 견적을 다시 대조한다. 달라졌으면 실행하지 않고 새 견적을 표시한다. 검증 → 수정할 목록/값 준비 → 한 번에 반영 → UI 갱신 순서다. 검사나 준비가 실패하면 골드·목록·체력·ID 발급 값 모두 보존한다.

확인 명령은 현재 정비 화면에만 속하는 한 번 사용 가능한 견적 토큰으로 제안한다. 같은 토큰의 재실행·처리 중 연타·종료한 화면의 오래된 토큰은 거부한다. 정상 완료 뒤 사용자가 새 견적을 확인하면 새 구매이며, 이를 중복 거래로 차단하지 않는다. 이 첫 로컬 동기 UI에는 영속 거래 ID·Campaign 새 필드를 추가하지 않는 것을 추천한다. 저장/네트워크/비동기 재전달을 도입할 때는 별도 약속이 필요하다.

전투 결과의 LastAppliedBattleRunId는 수리·모집으로 변경하지 않는다. BattleNumber·LastBattle·ActiveBattleSetup·난이도·표적 설정도 유지한다. Done 신호 뒤·OnExit 뒤에는 거래 입력을 차단한다.

## 7. 검증과 완료 기준

1. Core 정의 검증/가격 경계는 Claude, Meta 거래 검증은 Codex가 담당한다. Unity 실행은 Claude 단독.
2. 가격19개 기존 산식 시험을 실제 승인 API로 옮겨 값·정원·수리 경계를 확인한다.
3. 거래 시험: 골드 부족·지점/병종 불일치·정원·오버플로·만피·전멸 분대 재모집·실패 시 ID/전체 상태 무변경·동일 토큰 재실행 차단.
4. 수리/모집 후 다음 BattleSetup에 새 병력·HP가 반영되고 기존 전투 시작 스냅샷은 훼손되지 않아야 한다.
5. 결과 상세 재진입으로 보상을 다시 적용하거나, 오래된 결과를 새 병력에 덮어쓰지 않아야 한다. 기존 결과 표시가 시작 스냅샷을 사용하는 구조를 유지한다.
6. 실제 전투 A안 실측은 저장소의 동일 소스·dt·시드와 확인할 구현/시험 정책을 명시한다. 가격/정원 확정과 전투4 완주 검증을 구분한다.

## 8. Claude 검토 요청

- E1: Core 정의/순수 계산 Claude, Data/Meta 정의·카탈로그/거래·화면 Codex 경계에 동의하는지.
- E2: UnitDef.RecruitCost 보존, 실제 정비 견적은 새 A단계 정의를 유일한 가격 원본으로 쓰는지.
- E3: Meta가 지점·예산·상태·일회 견적 토큰을 검증하고 첫 로컬 동기 UI에는 Campaign 새 거래 필드를 만들지 않는지.
- E4: 직접 지점 보충/빈 지점 모집과 전체 한도 규칙이 기존 PostDef·BattleSetupFactory와 맞는지.

정비 화면 설계와 이 데이터 약속을 사용자 승인받은 뒤 구현한다. 후보 A의 가격·정원 자체는 D-042 잠정 승인 상태이며 다시 승인 요청하지 않는다.
