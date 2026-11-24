# K-036 결과 — 대포 효과, 마법 범위 미리 보기, 불바다 바닥 원 빼기

작성: Claude / 2026-10-16 / 가지 `card/K-036`
**유니티를 열지 않았고 컴파일도 못 돌렸다.** 합친 뒤 컴파일·플레이 확인이 필요하다.

## 4. 대포 효과 없음

### 원인 (코드를 읽어 찾은 것, 실행 확인은 못 함)
- K-031의 쏜 순간 찾기는 "투사체 칸 크기가 1 이하 → 1.6으로 바뀜 + 3m 안 대포"였다. K-032 뒤에도 크기 1.6은 그대로여서 **확실한 고장 지점을 코드에서 찾지는 못했다.**
- 대신 효과 자체가 너무 작고 짧았다: 반동 2칸 합쳐 0.2초, 연기 판 0.7m가 0.6초. 성벽 위 사진기 거리에서는 거의 안 보였을 가능성이 크다. 투사체로 간접 짐작하는 방식도 언제든 어긋날 수 있다.

### 고친 것
- **쏜 순간을 병사 사격 시각으로 직접 잰다**: 새 파일 `BattleSim.CannonFx.cs`의 `CannonFireAge(대포)` — K-032가 만든 `sFireTime`(SoldierFireJob이 쏠 때 기록)을 읽기만 한다. 전투 시간 기준이라 지도·일시정지와도 맞는다. BattlePropView의 투사체 추적 코드는 지웠다
- 반동 칸 0.1초 → 0.15초, 연기 판 0.7m → 1.2m, 연기 속도 10fps → 8fps(0.75초). 운용병이 없는 대포는 연기 없음
- **포탄 떨어진 자리 폭발**: BattleEffectsView가 투사체 칸마다 무기 번호를 기억하다가, 대포알 칸이 0으로 바뀌면(착지) 그 자리 땅에 `trap_burst`를 범위 반경 × 1.2(최소 1.5m) 크기로 한 번 그린다. 함정 폭발과 같은 묶음이라 동시 개수 16 → 32
- `WeaponAreaRadius(무기)` 읽기 함수도 `BattleSim.CannonFx.cs`에 넣었다

## 8. 마법 범위 미리 보기
- 지도에서 Q·W로 마법을 고른 동안, 마우스 자리에 그 마법 반경의 원이 따라다닌다. 폭발은 노랑, 불바다는 주황으로 찍은 뒤 원과 같은 색이고 가운데 점이 있다
- 성벽에서 150m 밖이거나 재사용 대기 중이면 회색이고 가운데 점이 없다. 마법사가 이동 중인 경우는 색을 바꾸지 않았다
- BattleView에 `SetSpellPreview(마법, 위치)`를 추가하고, `DrawMapMarkers` 안에서 그린다(같은 표시 버퍼라 따로 그리면 덮인다)
- `BattleEntry.cs`는 **한 줄만 넣었다**: `view.DrawMapMarkers(...)` 바로 위의 `view.SetSpellPreview(...)`. 마우스 위치는 기존 클릭과 같은 `ScreenToWorldPoint` 방식

## 12-b. 불바다 바닥 빨간 원
- BattleView.DrawEffects: 불바다 그림(fire_field)이 있으면 바닥 원판을 그리지 않는다. 그림이 없으면 예전처럼 원판
- 지도의 주황 원(DrawMapMarkers)과 폭발 대기 노랑 원판은 그대로 둔다

## 바꾼 파일
- 새 파일 `Combat/Battle/BattleSim.CannonFx.cs` (읽기 함수 2개, 전투 계산은 바꾸지 않음)
- `Combat/Battle/BattlePropView.cs` (반동·연기 시각, 크기)
- `Combat/Battle/BattleEffectsView.cs` (포탄 착지 폭발)
- `Combat/Battle/BattleView.cs` (불바다 원판 한 줄, 미리 보기 상태·그리기)
- `Combat/Battle/BattleEntry.cs` (한 줄)

## 확실하지 않은 것
1. 컴파일: `cannons[index].soldier` 튜플 이름 접근(BattleSim.Cannons.cs의 `List<(int soldier, int side, int number, string postId)>`), BattlePropView에 남은 `using Unity.Collections`(안 써도 오류는 아님)
2. 원인을 실행으로 확인하지 못했다. 고친 뒤에도 안 보이면 대포 그림 크기(0.6m)나 사진기 위치 문제일 수 있다 — 그림 크기는 K-034 범위
3. 대포알 폭발 그림으로 trap_burst를 썼다(흙 폭발 느낌). magic_burst가 낫다면 BattleEffectsView에서 묶음만 바꾸면 된다
4. art-ui(K-037)가 BattleEntry나 DrawMapMarkers를 고치면 겹칠 수 있다. 내 줄은 `view.DrawMapMarkers(...)` 바로 위 한 줄과 DrawMapMarkers 안 "쓸 수 있는 범위 네모" 바로 아래 묶음뿐이다
