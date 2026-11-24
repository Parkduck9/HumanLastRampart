# K-031 결과 — 승인 그림 시안을 전투 들판에 연결

작성: Claude (K-031 세션) / 2026-10-16 / 가지 `card/K-031`
**유니티를 열지 않았고 컴파일도 못 돌렸다.** 메인 폴더에서 조정 담당의 컴파일·화면 확인이 필요하다.

## 1. 연결한 그림 (복사 위치 `Game/Assets/_Project/Art/Battle/Resources/BattleArt/`)

| 시안 | 원본 | 복사한 곳 | 쓰는 코드 |
|---|---|---|---|
| K-016 대포 반동 | codex_weapons_fire_v1/cannon_recoil_sheet.png | Cannon/cannon_recoil.png (768×256, 3칸) | BattlePropView |
| K-016 포구 연기 | codex_weapons_fire_v1/muzzle_smoke_sheet.png | Cannon/muzzle_smoke.png (1536×256, 6칸) | BattlePropView |
| K-018 횃불대 | codex_torch_mount_v1/torch_mount_off.png | Props/torch_mount_off.png (128×256) | BattleEffectsView |
| K-024 성벽 손상 | codex_k024_v1/textures/{wall_stone,parapet_side,parapet_top}_damage{1,2}_albedo.png | World/ 같은 이름 6장 | BattleWorldMaterials·BattleWorldView |
| K-024 시체 더미 | codex_k024_v1/sprites/corpse_pile_1~3.png | Props/ | BattleFieldArt |
| K-024 함정 실물 | codex_k024_v1/sprites/trap_1.png(묻힘), trap_2.png(구덩이) | Props/ | BattleFieldArt |
| K-025 소품 | codex_k025_v1/sprites/oak, pine, rock_a, rock_b, bush.png | Props/ | BattleFieldArt |
| K-025 먼 산 | codex_k025_v1/backgrounds/horizon_day/night.png | Sky/ | BattleFieldArt |
| K-025 하늘 | codex_k025_v1/backgrounds/sky_day/night.png | Sky/ | BattleFieldArt |

메타 파일은 넣지 않았다 (유니티가 만든다). 가져오기 설정은 새 에디터 파일이 자동으로 건다.
윗면(_top) 그림 5장은 아직 쓰는 곳이 없어 복사하지 않았다 (아래 4장 참고).

## 2. 바꾼 파일

- **새 파일** `Combat/Battle/BattleFieldArt.cs` — 들판 소품 흩뿌리기, 시체 더미 3단계, 함정 묻힘/구덩이, 먼 산 띠·하늘 원통
- **새 파일** `Combat/Editor/BattleFieldArtImport.cs` — Props·Cannon·Sky 폴더 가져오기 설정 (점 필터, 고정, 밉맵 끔, 압축 없음, 크기 그대로 / 먼 산은 가로 거울 반복+밉맵, 하늘은 부드럽게)
- `Combat/Battle/BattleWorldMaterials.cs` — `GetDamaged(표면, 단계)` 추가 (손상 그림 재질, 크기 다르면 null)
- `Combat/Battle/BattleWorldView.cs` — 면마다 바깥면·난간·돌출부 조각을 모아 `SetWallHealth`에서 체력 비율 0.66 미만 금감, 0.33 미만 부서짐으로 재질 교체. 붉은 기는 그대로. `DamageStage(side)` 확인용
- `Combat/Battle/BattlePropView.cs` — cannon_recoil이 있으면 3방향 그림 대신 반동 그림. 새 대포알(투사체 크기 0→1.6)을 가장 가까운 대포(3m 안)에 묶어 반동 1→2→0칸(0.1초씩) + 포구 앞 연기 6칸 한 번
- `Combat/Battle/BattleEffectsView.cs` — 횃불대 받침을 낮·밤 늘 그림(BattleLights와 같은 자리, 통로 위). 받침이 있으면 밤 불꽃을 시안 겹침대로 받침 머리(통로 위 0.2375m, 칸 0.056m)에. `Attach(BattleFieldArt)`·`DrawsPiles` 추가, 들판 그림의 그리기·정리를 함께 부른다
- `Combat/Battle/BattleView.cs` — **두 줄만**:
  1. 생성자 `effectArt = new BattleEffectsView(...)` 바로 아래: `effectArt.Attach(new BattleFieldArt(sim, material, quad, root.transform));`
  2. `RefreshPiles()` 첫 줄: `if (effectArt.DrawsPiles) return;` (그림이 있으면 상자 더미를 만들지 않음)

  K-032와 합칠 때 이 두 줄만 옮기면 된다. 필드·Draw·Dispose는 BattleView에 추가하지 않았다(effectArt가 대신).

그림이 하나도 없으면 모두 예전처럼 그려진다 (대포 3방향, 상자 더미, 공 불꽃, 단색 하늘).

## 3. 배치 규칙 (화면 보고 조정할 값)

- 소품: 씨앗 31로 늘 같은 들판. 적 깔때기(BattleSim D-065 식 그대로) 밖 + 성벽에서 14m 밖에 숲 260개(침엽수 45%·넓은잎 35%·풀숲·바위), 들판 끝 바깥 60m까지. 언덕마다 둘레에 4~14개(풀숲·바위, 깔때기 밖이면 나무도). 깔때기 안은 성벽 35m 밖에 작은(0.6배) 풀숲 60·바위 20만. 크기는 시안 32화소/m(넓은잎 약 5m 칸, 침엽수 7m) × 0.8~1.2
- 그리기: 소품 5종·더미 3단계·함정 2종 = 최대 10번 인스턴싱 그리기. 소품 버퍼는 처음 한 번 + 밝기 바뀔 때만. 더미·함정은 0.25초마다 갱신. 전투 계산(작업)에는 손대지 않음
- 시체 더미: 칸(2m)마다 판 하나, 판 크기 2.67m(내용 폭이 칸을 덮게). 높이 0.6m 미만 1단계, 1.2m 미만 2단계, 그 위 3단계이고 더미가 더 높아지면 판을 키운다(성벽 높이 4m까지 → 약 6.4m 판)
- 함정: 실제 위치(Position)에 묻힘·점화 중·불발은 trap_1(2m), 터짐은 trap_2(구덩이 반경을 덮게). 그림자 없음. BattleView의 어두운 구덩이 원판은 그대로 아래에 남는다
- 먼 산: 반경 들판+120m(280m) 둘레에 12번 거울 반복, 높이 약 50m. 하늘: 반경 560m, 높이 500m 원통. 둘 다 빛 안 받는 URP Unlit(안개는 받음), 지도 모드에서 숨김. 날씨에 따라 조금 어둡게
- 대포: 반동 그림 칸 0.6m(보이는 폭 약 0.55m), 연기 칸 0.7m를 대포 자리 바깥 0.45m·위 0.12m
- 횃불대: 칸 0.4m(보이는 0.3m), 기존 빛 높이 0.24m는 그대로 — 불꽃 바닥 0.2375m와 거의 같다

## 4. 확실하지 않은 것 (컴파일 못 함 — 의심 가는 곳)

1. **컴파일**: 튜플 이름이 다른 `List<(Renderer renderer, Surface surface)>`와 `List<(Renderer, Surface)>` 대입(BattleWorldView), `BattlePropView`의 새 `using Unity.Collections`/`UnityEngine.Rendering`, BattleFieldArt의 지역 함수 `Add`. 문제 없을 것으로 보지만 확인 필요
2. **대포 쏜 순간 찾기**는 대포알 투사체 크기(WriteShotInstancesJob의 1.6)에 기댄다. **K-032가 투사체 그림·크기를 바꾸면 반동·연기가 안 나올 수 있다** (BattleEffectsView의 맞음 불꽃도 같은 규칙). 합칠 때 확인
3. 반동 그림은 바깥을 향한 한 방향뿐이라 **좌우 조준 그림이 사라졌다** (예전 cannon_aim3 3방향). 크기 0.6m·연기 자리는 화면 확인 필요
4. **횃불 불꽃이 아주 작다** (칸 0.056m, 보이는 불꽃 약 4cm — 시안 겹침 비율 그대로). 화면에서 안 보이면 `BattleEffectsView.MountFlameSize`를 키운다. 받침 0.3m가 병사(0.35m)와 거의 같은 키라 커 보일 수 있다
5. **먼 산·하늘이 밤에는 안개에 묻힐 수 있다**: 밤은 `LowVisibility`라 안개가 켜져 있어 Unlit 재질이 안개색으로 덮인다(비·안개 날도). 맑은 낮에만 확실히 보일 것. URP Unlit 알파 잘라내기(`_ALPHATEST_ON`)를 실행 중에 켜서 **빌드에서는 변형이 빠질 수 있다**(에디터는 됨)
6. 먼 산 띠는 좌우 끝이 안 맞는 그림이라 거울 반복으로 이음새를 감췄다 → 능선이 좌우 대칭으로 되풀이된다(README에 적힌 한계)
7. 소품·더미·함정은 세로로 선 카메라 판이라 **땅 그림(함정·구덩이)도 서 있다**. 시안은 기울여 본 땅 그림이라 사진기 각도에서는 자연스러울 것으로 보지만, 위에서 가까이 보면 판처럼 보일 수 있다. 지도 모드에서는 그리지 않는다(윗면 그림 미사용 — 지도 점을 가리지 않게)
8. 손상 질감에 **멀쩡한 노멀맵을 그대로** 쓴다(손상별 노멀 없음, README 경고). 난간 윗면(parapet_top) 손상 그림은 복사했지만 지금 난간 상자가 옆면 재질 하나만 써서 실제로는 안 보인다
9. 함정을 실제 위치에 그리므로 들판 화면에서 진짜 위치가 보인다(지도는 표시 위치만). 기획 의도와 맞는지 사용자 확인 필요
10. 소품이 깔때기 밖에만 있어도 망원경·적 시야에는 영향 없음(그리기만). 나무 그림자 260개 정도가 성능에 문제없는지 확인 필요
11. ROADMAP·status는 카드 지시에 따라 고치지 않았다(조정 담당이 합칠 때 갱신)
