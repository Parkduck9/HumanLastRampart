# 마이크로소프트 스토어 1.0 준비 (2026-10-27, 초안)

사용자 결정: 이름 **빛을 잇는 자들** (영어 목록 Lightbearers), **무료**, **모든 지역**, 게임 안 글은 **한국어만** (설명에 분명히 적음). 개발자 계정은 이미 있음.

## 1. 사용자께서 직접 하실 일 (계정·로그인이 필요해 제가 대신할 수 없음)
1. 파트너 센터 → 앱 및 게임 → 새 제품 → **MSIX 또는 PWA 앱** → **게임** → 이름 "빛을 잇는 자들" 예약.
2. 예약한 제품 → **제품 관리 → 제품 ID(Product identity)** 화면에서 세 값을 복사해 알려 주세요:
   - 패키지/ID/이름 (Package/Identity/Name)
   - 패키지/ID/게시자 (Package/Identity/Publisher, `CN=...`)
   - 패키지/속성/게시자 표시 이름 (PublisherDisplayName)
   → 제가 `Tools/Build/msix/store-identity.json`에 넣고 다시 포장합니다.
3. 제출 화면에서: 가격 "무료", 시장 "모든 시장", 범주 "게임 → 전략", 연령 등급 설문(아래 4장 답안 참고), 패키지 올리기(.msix), 스토어 목록(아래 2장 글과 그림), 제출.
4. 인증 결과(보통 며칠) 메일 확인. 떨어지면 메일 내용을 붙여 주세요.

## 2. 스토어 목록 글 (한국어 — 주 목록)
**짧은 설명**
검은 안개에 덮인 대륙, 꺼진 등대 요새를 되살려 빛의 봉화선을 다시 잇는 4면 성벽 방어 전략 게임.

**설명**
검은 안개가 대륙을 덮었습니다. 안개를 밀어내던 옛 등대 요새들은 하나둘 꺼졌고, 등대를 다시 켤 수 있는 등불지기의 혈통은 사관학교를 갓 졸업한 생도 루멘 파로스 한 사람만 남았습니다.

등대 탑의 빛은 사방으로 퍼져 안개에 홀린 무리를 끌어들입니다. 요새는 동서남북 네 면에서 한꺼번에 공격받습니다.

- 성벽 위를 직접 걸으며 병사와 대포를 배치하고, 어떤 적을 먼저 노릴지 정합니다.
- 등대 빛줄기를 돌려 안개 속 적을 드러내고, 지도를 펼쳐 마법과 포격을 떨어뜨립니다.
- 낮에는 무너진 성벽과 등대를 고치고, 함정을 깔고, 병사와 용병을 모읍니다.
- 등대를 모두 고치고 성벽을 90% 넘게 세운 뒤 '점화의 밤'을 버티면 요새가 되살아납니다.
- 점화의 밤에는 성벽을 맨손으로 기어오르는 우두머리 '성벽손 오우거'가 나타납니다.

※ 게임 안 글은 한국어만 지원합니다.

**주요 특징 (최대 20개 중)**
- 4면 성벽 방어: 수백 마리의 적이 네 방향에서 동시에
- 등대 빛줄기로 안개 속 적 드러내기
- 지도에서 고르는 마법·포격
- 낮의 정비: 수리·함정·상점·모집
- 픽셀 아트 2.5D 화면

**검색어(최대 7개)**: 성벽 방어, 타워 디펜스, 전략, 픽셀 아트, 등대, 판타지, 디펜스

## 3. 영어 목록 (포장에 영어도 선언하므로 필요 — 다른 나라 손님에게 보임)
**Short description**
Relight the fallen lighthouse forts and hold the walls on all four sides against the fog-bound horde. (In-game text: Korean only.)

**Description**
This game's text is in Korean only.

Black fog has swallowed the continent. The old lighthouse forts that once held it back have gone dark, and only one heir of the lamp-keeper bloodline is left to relight them: Lumen Pharos, a cadet fresh out of the academy.

The beacon's light spreads in every direction and draws the fog-touched horde, so each fort is attacked from all four sides at once.

- Walk the wall tops yourself, post soldiers and cannons, and choose which enemies to target first.
- Swing the beacon's beam to reveal enemies in the fog, then open the map to drop spells and artillery.
- By day, repair walls and the beacon, lay traps, and recruit soldiers and mercenaries.
- Fully repair the beacon, raise the walls above 90%, and survive the Night of Kindling to bring the fort back to life.
- On that night, the Wallhand Ogre climbs the walls with its bare hands.

**Features**: Four-sided wall defence against hundreds of enemies / Reveal the fog with the beacon's beam / Map-targeted spells and artillery / Daytime repairs, traps, shop and recruiting / 2.5D pixel art

**Keywords**: wall defense, tower defense, strategy, pixel art, lighthouse, fantasy, siege

## 4. 연령 등급 설문(IARC) 답안 초안 — 사용자 확인 필요
- 폭력: 있음 — 판타지 생물·인간형 적을 활·총·대포·마법으로 쓰러뜨림, 피 표현 없음(사용자 확인 2026-10-27)
- 공포·선정성·약물·도박·욕설: 없음
- 사용자끼리 대화·위치 공유·개인정보 수집: 없음
- 게임 안 구매: 없음
→ 예상 등급: 12세 이용가 안팎(전체/12세 사이). 정확한 등급은 설문 결과로 정해집니다.

## 5. 개인정보 처리방침
- 데이터를 모으지 않으면 처리방침 주소는 필요 없습니다. 유니티 분석(Unity Analytics) 모듈이 켜져 있는지 W46에서 점검 중 → 끄고 "수집 없음"으로 제출하는 것이 목표.

## 6. 남은 확인
- [ ] W43 우두머리 합침 + 전체 시험
- [ ] W46 포장 스크립트·라이선스 목록·출시 점검
- [ ] K-130 스토어 그림 (2:3 포스터, 1:1 상자, 16:9 대표, 300 타일, 포장 로고)
- [ ] 스크린샷 4~8장 (1920x1080, 덧붙인 글·로고 없이)
- [ ] 처음부터 끝까지 한 바퀴 실행 파일로 플레이 점검
- [ ] 제품 ID 세 값 받기 → 최종 포장
