# 마이크로소프트 스토어 제출 방법 (빛을 잇는 자 1.0)

## 1. 이름 예약 (한 번만)
1. https://partner.microsoft.com/dashboard 로그인 → **앱 및 게임** → **새 제품** → **MSIX 또는 PWA 앱**.
2. 제품 종류에서 **게임** 선택 → 이름 `빛을 잇는 자` 입력 → 확인 후 **제품 이름 예약**. (예약은 3개월 유지)

## 2. 포장 파일에 제품 ID 넣기
1. 예약한 제품 → 왼쪽 **제품 관리 → 제품 ID** 화면에서 세 값을 확인:
   - `Package/Identity/Name`
   - `Package/Identity/Publisher` (`CN=`으로 시작)
   - `Package/Properties/PublisherDisplayName`
2. 둘 중 하나:
   - **가) 값을 Claude에게 알려 주기** → 최종 포장 파일을 만들어 드립니다.
   - **나) 직접 하기** → `Tools/Build/msix/store-identity.json`의 세 값을 바꾸고, 최종 출시판이 만들어진 뒤 아래 명령 실행:
     ```
     powershell -ExecutionPolicy Bypass -File Tools\Build\package-msix.ps1
     ```
     결과: `C:\Unity\1\HLR-builds\msix\Lightbearers_1.0.0.0_x64.msix` (자리표시 값 경고가 안 나오면 정상)
3. 값이 하나라도 다르면 올릴 때 "패키지 ID 불일치"로 거절됩니다.

## 3. 제출 만들기
제품 화면 → **제출 시작**. 아래 칸을 차례로 채웁니다.

| 칸 | 넣을 것 |
|---|---|
| 가격 및 사용 가능 여부 | 시장: 모든 시장 / 가격: **무료** / 공개: 공개 |
| 속성 | 범주 **게임 → 전략**. "개인 정보를 수집·전송합니까?" → **아니요** (게임이 밖으로 보내는 데이터 없음 — 분석 기능 제거함). 시스템 요구 사항: 키보드·마우스 |
| 연령 등급 | 설문 시작 → 범주 "게임". 폭력: 판타지 적과의 전투, **피 표현 없음**. 공포·선정성·약물·도박·욕설·사용자 대화·위치 공유·게임 안 구매: 모두 **없음** |
| 패키지 | `.msix` 파일을 끌어다 놓기 → 검사가 끝날 때까지 대기. 기기: **데스크톱**만 체크 |
| 스토어 목록 | 언어마다 한 장씩: **한국어**(주), **영어**. 글은 `Docs/Store/MSStore_1.0_준비.md` 2장(한국어)·3장(영어) 그대로 복사 |
| 스토어 목록 그림 | 스크린샷(데스크톱, 1920x1080, 4~8장) + `Docs/References/codex_k132_v1/store/`의 `poster_1440x2160.png`(2:3 포스터), `box_2160x2160.png`(1:1 상자), `super_hero_3840x2160.png`(16:9 대표), `app_tile_300x300.png`(타일) — 언어마다 따로 올려야 함 |
| 제출 옵션 | "제한된 기능" 설명 칸이 나오면: `runFullTrust — Unity로 만든 데스크톱 게임 실행 파일(Lightbearers.exe)을 실행하기 위해 필요합니다. 인터넷 연결이나 개인 정보 접근은 하지 않습니다.` |

4. 모든 칸이 녹색이 되면 **스토어에 제출**.

## 4. 제출 뒤
- 인증은 보통 1~3 영업일. 결과는 메일과 파트너 센터에 옵니다.
- 떨어지면 보고서 내용을 Claude에게 붙여 주세요 → 고친 포장 파일을 다시 만듭니다.
- 업데이트할 때: 판 번호(프로젝트 설정 `bundleVersion`)를 올려 다시 포장 → 새 제출에서 패키지만 바꿔 올림. 같은 판 번호는 다시 올릴 수 없습니다.

## 5. 실제 화면 기준 답안 (2026-10-27, 앱 제품 9P56FVZZTZPJ)
- **속성**: 범주 **Entertainment**(앱 제품에는 "게임" 범주가 없음), 개인 정보 **No**, 최소 하드웨어 키보드·마우스, 메모리 최소 4 GB·권장 8 GB, DirectX 최소 Version 11. 생성형 AI 기능 칸은 체크하지 않음(게임 안에 AI 기능 없음). → **저장됨(Complete)**
- **가격**: 통화 USD - United States, 소매 가격 **0** → 저장 눌렀으나 개요에 "Not started"로 보여 다시 확인 필요
- **연령 등급 (IARC)**: App Type **Game** / 폭력 **Yes** → "인간 대상 폭력"과 "인간 외(판타지 생물) 대상 폭력" 둘 다 체크
  - 인간 대상: Fantastical / Yes, pixelated style / Unrealistic / Often depicted from a distant perspective / 피 None / 실제·역사 전쟁 No / 무고한 인물 No / 위협적 소리·어두운 분위기 Yes
  - 인간 외 대상: Fantastical / pixelated Yes / Unrealistic / Often distant / 피 None / 사람처럼 행동 Yes(고블린·오우거) / 실제 동물 No / 어두운 분위기 Yes
  - 나머지 질문 전부 **No** (공포, 성적 내용, 도박, 욕설, 약물, 저속한 유머, 디지털 구매·NFT, 사용자 대화, 위치 공유, 나치 상징, 대한민국 정체성 훼손, 테러, 범죄 수법)
  - 마지막 "등급 기관 직접 등급/실물 매체" **No** → **Preview ratings** → 결과 확인 후 저장
