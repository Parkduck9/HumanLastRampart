# K-058 결과 — 강화·상점·용병 1판 구현 (D-079)

작성: Claude (카드 세션) / 2026-10-19. **유니티를 열지 않았고 컴파일·시험을 돌리지 못했다.** 손으로 쓴 .asset·.meta(정의 22개, 카탈로그 추가)가 읽히는지 첫 확인 필요.

## 만든 것

- **데이터**: `ItemDef` 확장(칸·소모품·성벽 아이템·최대 수·순서·효과 목록), 새 `TrainingDef`·`MercenaryDef`, 효과 종류 `UpgradeEffect/UpgradeStat`. 정의 = 품목 11 · 훈련 9(궁수·총병·대포병 × 3단계) · 용병 2(궁수·총병), MetaCatalog에 등록. 정의 검사(`DefinitionLookup`) 추가
- **진행 데이터**: `CampaignState.Items / Training / Mercenaries` (`Core/Campaign/UpgradeState.cs`)
- **규칙**: `Core/Upgrades/UpgradeRules.cs` — 구매(영웅 아이템은 빈 칸에 바로 끼움), 끼우기·빼기(같은 칸은 바꿔 끼움), 훈련(차례로·고정가), 고용(성벽마다 50·계약금), 해고, 급료(전투 준비 직전, 못 주면 계약 통째로 떠남), 강화 수치 계산(살아 있는 영웅이 끼운 것만)
- **전투 연결**: `BattleSetup.Boosts`(App 계산) · `Mercenaries` · `ConsumedItemIds`, `BattleResult.MercenaryLosses`
  - 무기 값에 병종 강화(훈련·단체 아이템) 구워 넣음 — 용병은 정예(2단계, 훈련이 더 높으면 그쪽) 무기 따로
  - 망원경 반경(×1.4), 전령 속도, 외침 잘못 듣기, 주인공 성벽 공격 간격(지휘 휘장, `commanderSide`), 폭발 피해·반경, 불바다 지속·화상 확률, 마법 대기, 방패 난간(성벽 위 적 피해 ×0.7), 불화살(맞힌 적 20% 화상 — 불바다 화상 규칙 재사용, 작업 → 큐 → 다음 프레임)
  - 용병: 분대처럼 서고 싸우지만 성벽 병력·70% 패배 계산·묶음 이동 밖. 손실은 결과에 따로
- **결과 반영** (`BattleResultApplier`): 용병 손실(계약 ID 검사), 소모품 1개씩 사용, 군의관 천막(분대마다 잃은 수 × 10% 반올림 복귀)
- **정비 화면**(기능 위주, 모양은 K-047): 마당 새 장소 **'상점'**(2550 자리) → 장비 칸 표시·구매·끼우기/빼기·성벽 아이템은 성벽별 단추. 병영 창 맨 위 **[병종 훈련]·[용병 고용]** → `Meta/UpgradePanel.cs`. 숨은 단추 '상점'·'병종 훈련'·'용병 고용'도 PrepEntry에
- **급료**: `GameFlow.PrepareBattle`에서 전투 준비 직전 `UpgradeRules.PayWages` (떠난 계약은 로그)
- **시험**
  - `Core/Tests/UpgradeRulesTests.cs` 7개: 구매·자동 장착·바꿔 끼우기·가방 아이템 효과 없음, 골드 부족, 소모품 쌓기·전투 사용 목록, 성벽 아이템, 훈련 단계·효과 누적, 용병 정원·계약금·급료·떠남·정예 단계, 쓰러진 영웅
  - `Combat/Tests/BattleUpgradeTests.cs` 3개: 정의 개수, 강화 없으면 무기 = 정의, 강화 시 무기 피해·간격·명중·화상·망원경·폭발 반경·용병이 성벽 병력 밖·용병 손실 분리
  - `PlayProbe.Upgrades.cs` `UpgradeShop`: 상점(렌즈·불화살)·훈련(궁수 1단계)·용병(북 10명) 사진 4장 + 다음 전투 준비에 급료·강화·소모품·용병 들어감 + 전투 사진
  - **FullLoop**: 장소 수 4 → **5**로 고침(상점 추가). 나머지 흐름은 그대로
  - `SimProbe.UpgradeCompare`: 현실·쉬움 × (강화 없이 / 강화 사용), 둘 다 D-075 기준(정면 1/3 옮기기)+보조+정면 먼저 모집, 정원·값 단계 지킴. 강화 사용 정책 = 마법사 아이템 → 지휘 휘장 → 불화살 → 군의관 → 북 방패 난간 → 훈련(골드 절반 안) → 정원 차면 북 용병 → 남은 골드 궁수
- 문서: `CONTRACTS.md` 끝에 초안(C-004 v1.5·C-005 v1.7·C-006 v1.3·C-010 v1.11), `Upgrade_Shop.md` 10절(구현 상태)

## 확실하지 않은 것

1. 손으로 쓴 정의 에셋(효과 목록 `Stat` 숫자 = enum 순서)이 맞게 읽히는지 — `BattleUpgradeTests.Definitions_HaveShopTrainingMercenaries`가 개수를 본다
2. `MoveShotsJob`에 큐(`NativeQueue<int>.ParallelWriter`)를 더했다 — Burst에서 문제없을 것으로 보지만 컴파일 확인 필요
3. 지휘 휘장은 `SetCommander`로 들어오는 주인공 성벽 기준. SimProbe는 `SetCommander`를 안 불러 북쪽(기본값)으로 계산된다
4. 불화살 화상은 활(궁수) 화살만 — 총병 화약 통은 피해 +30%만. 대포에는 화상 없음
5. 용병은 그 성벽 가운데 분대 지점에 같이 선다 — 총안 자리(K-045)가 모자라면 뒷줄. 지도 묶음 표시는 안 나옴(옮기지 않으므로)
6. **develop에 .meta 없이 들어온 파일**(K-049): `BattleSim.Terrain.cs`, `TerrainWork.cs`, `TerrainLayout.cs`, `TerrainGeneratorTests.cs`, `TerrainWorkerAnimation.cs`, `TerrainWorkTransactions.cs` — 이 카드에서는 만들지 않았다(메인 유니티가 만든 것과 GUID가 부딪힐 수 있어서)
7. 화면 글은 K-047 슬롭 기준을 아직 안 따른다(기능 위주) — 단추 글자 '구매: …', '훈련: …', '고용: …'는 PlayProbe가 앞부분으로 찾는다

## develop 합치기 (2026-10-19, 커밋 43365fa · e8f2e97)

- 충돌 6곳: BattleSim.Commands·Spells(알림 새 문구 + 강화 수치), BattleView(K-047 망원경 색 + 렌즈 반경), PrepEntry·PreparationPanel(K-047 판 위에 상점 장소·숨은 단추·병영 머리 줄 [병종 훈련]·[용병 고용] 탭 다시 얹음), CONTRACTS(K-062 C-004 v1.5 저장이 먼저 → 강화는 **C-004 v1.6**)
- 상점·훈련·용병 장부(UpgradePanel)를 UI/Kit로 새로: 어둠막 + 양피지, 머리 줄(이름·골드·닫기), 왼쪽 2열 고르기 탭, 오른쪽 이름·설명·값 줄 + 황동 단추 하나. 열쇠: `물건:<id>`·`확정 구매`, `병종:<id>`·`확정 훈련`, `성벽:<id>`·`용병:<id>`·`확정 고용`, `해고:<계약>`, 병영 `병영:훈련`·`병영:용병`
- 저장(K-062): CampaignState 통째 JsonUtility → 강화 필드 그대로 저장. null 글자가 ""로 돌아오는 것은 모두 IsNullOrEmpty로 비교 — 왕복 시험 추가 (`UpgradeFields_SurviveJsonRoundTrip`)
- 유니티 DLL로 어셈블리 11개(Core·UI·App·Combat·Meta·Story·Frontend·Core.Tests·Combat.Tests·PlayProbe·SimProbe) 따로 컴파일 → 오류 0. develop ← card/K-058 merge-tree 충돌 없음
