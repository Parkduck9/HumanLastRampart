# BattleArt/World — 3D 모형 질감

성벽·통로·난간·포탑·들판 3D 상자에 입히는 질감 폴더입니다.
코드(`Combat/Battle/BattleWorldMaterials.cs`)가 실행할 때 이 폴더에서 이름으로 찾아 읽습니다.
**그림을 넣거나 바꾸면 코드 수정 없이 바로 바뀝니다.** 그림이 없는 표면은 단색(지금 목업 회색)으로 그립니다.

## 파일 이름 규칙

`<표면>_albedo.png` (기본 색 그림), `<표면>_normal.png` (노멀맵)

| 표면 | 뜻 |
|---|---|
| `wall_stone` | 성벽 바깥면 |
| `walkway_stone` | 통로 바닥 |
| `parapet_top` | 난간 윗면 |
| `parapet_side` | 난간 옆면 |
| `tower_wall` | 포탑 벽 |
| `tower_roof` | 포탑 지붕 |
| `field_grass` | 들판 풀 |
| `field_dirt` | 들판 흙 |

예: `wall_stone_albedo.png`, `wall_stone_normal.png`
- 노멀맵만 있고 기본 색 그림이 없으면 단색으로 그립니다(노멀맵도 안 씀).
- 기본 색 그림만 있으면 노멀맵 없이 그립니다.

## 크기 기준

- **1m당 32화소.** 그림 너비 ÷ 32 = 한 장이 덮는 미터 (128화소 → 4m, 256화소 → 8m).
- 상자 크기에 맞춰 자동으로 반복합니다. 이음새 없이 이어지는 타일로 만들어 주세요.

## 가져오기 설정 (그림을 넣은 뒤 인스펙터에서)

기본 색 그림 (`_albedo`)
- Texture Type: Default
- sRGB (Color Texture): 켬
- Filter Mode: Point (no filter)
- Wrap Mode: Repeat
- Generate Mip Maps: 켬
- Non-Power of 2: None (크기가 2의 거듭제곱이 아니어도 줄이지 않고 그대로)
- Compression: None 권장 (도트가 뭉개지지 않게)

노멀맵 (`_normal`)
- Texture Type: Normal map
- Filter Mode: Point (no filter)
- Wrap Mode: Repeat
- Non-Power of 2: None
- Compression: None 권장

※ 코드가 실행 중에 점 필터·반복을 한 번 더 맞추지만, 밉맵·sRGB·노멀맵 종류는 가져오기 설정에서만 바뀝니다.
※ 이 폴더의 `.meta`는 유니티가 만듭니다. 손으로 만들지 마세요.
