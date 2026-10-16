# 캐릭터 시트 인물 키 표 (자동 생성: Tools/SheetMeasure/measure-sheets.ps1)

칸 256×384 기준, 알파 128 이상. 머리·발 = 칸 위에서 잰 줄(px). 키 = 발 − 머리 + 1. 넓이는 √(불투명 픽셀 수).
기준 키 = 똑바로 선 키로 환산한 값 (서 있기류: 0번 칸 키 / 쓰러짐·달리기: 짝 시트 키 × 넓이 비, 달리기는 옆모습 보정 0.92).
병사·주인공·마법사 그리기 배율 = 384 ÷ 기준 키 (인물 키 = 그리기 크기). 적은 자기 기본 시트 기준 키 ÷ 이 시트 기준 키.

| 시트 | 칸 수 | 0번 칸 키 | 키 가운데값 | 키 최대 | 머리(0번) | 발(0번) | 발 최대 | √넓이(0번) | 짝 시트 | 기준 키 | 병사 기준 배율 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Fall/archer_fall.png | 4 | 254 | 179 | 254 | 98 | 351 | 351 | 162 | archer_static | 273.3 | 1.405 |
| Fire/archer_fire.png | 6 | 249 | 249 | 264 | 103 | 351 | 351 | 148 | - | 249 | 1.542 |
| Run/archer_run.png | 4 | 197 | 183 | 199 | 152 | 348 | 351 | 123 | archer_static | 226.5 | 1.695 |
| Static/archer_static.png | 1 | 300 | 300 | 300 | 52 | 351 | 351 | 177 | - | 300 | 1.28 |
| Fall/cannon_crew_fall.png | 4 | 261 | 199 | 261 | 91 | 351 | 351 | 158 | cannon_crew_static | 253 | 1.518 |
| Run/cannon_crew_run.png | 4 | 222 | 211 | 223 | 130 | 351 | 351 | 143 | cannon_crew_static | 248.9 | 1.543 |
| Static/cannon_crew_static.png | 1 | 300 | 300 | 300 | 52 | 351 | 351 | 187 | - | 300 | 1.28 |
| Fall/goblin_fall4.png | 4 | 211 | 129 | 211 | 141 | 351 | 351 | 142 | goblin_walk_attack4 | 221 | 1.738 |
| Goblin/goblin_walk_attack4.png | 8 | 240 | 240 | 289 | 112 | 351 | 351 | 155 | - | 240 | 1.6 |
| Fall/gunner_fall.png | 4 | 222 | 152 | 222 | 130 | 351 | 351 | 139 | gunner_static | 250.5 | 1.533 |
| Fire/gunner_fire.png | 6 | 265 | 265 | 286 | 87 | 351 | 351 | 150 | - | 265 | 1.449 |
| Run/gunner_run.png | 4 | 218 | 218 | 220 | 133 | 350 | 351 | 134 | gunner_static | 262.1 | 1.465 |
| Static/gunner_static.png | 1 | 300 | 300 | 300 | 52 | 351 | 351 | 167 | - | 300 | 1.28 |
| Hero/hero_back_idle.png | 1 | 300 | 300 | 300 | 52 | 351 | 351 | 181 | - | 300 | 1.28 |
| Hero/hero_command.png | 3 | 291 | 291 | 291 | 61 | 351 | 351 | 176 | - | 291 | 1.32 |
| Hero/hero_walk_run_grid.png | 16 | 297 | 299 | 310 | 55 | 351 | 360 | 173 | - | 297 | 1.293 |
| Ladder/ladder_goblin_carry_run4.png | 4 | 260 | 260 | 260 | 92 | 351 | 351 | 153 | - | 260 | 1.477 |
| Ladder/ladder_goblin_climb4.png | 4 | 246 | 257 | 260 | 106 | 351 | 351 | 148 | - | 246 | 1.561 |
| Ladder/ladder_goblin_deploy4.png | 4 | 242 | 249 | 260 | 110 | 351 | 351 | 158 | - | 242 | 1.587 |
| Static/ladder_goblin_static.png | 1 | 272 | 272 | 272 | 80 | 351 | 351 | 194 | - | 272 | 1.412 |
| Walk/ladder_goblin_walk.png | 4 | 272 | 272 | 272 | 80 | 351 | 351 | 182 | - | 272 | 1.412 |
| Walk/ogre_attack4.png | 4 | 201 | 201 | 261 | 153 | 353 | 379 | 150 | - | 201 | 1.91 |
| Fall/ogre_fall4.png | 4 | 187 | 156 | 187 | 165 | 351 | 351 | 150 | ogre_walk | 206.8 | 1.857 |
| Static/ogre_static.png | 1 | 173 | 173 | 173 | 179 | 351 | 351 | 137 | - | 173 | 2.22 |
| Walk/ogre_walk.png | 4 | 233 | 229 | 233 | 119 | 351 | 351 | 169 | - | 233 | 1.648 |
| Static/wizard_back_static.png | 1 | 298 | 298 | 298 | 54 | 351 | 351 | 183 | - | 298 | 1.289 |
| Static/wizard_cast.png | 4 | 257 | 257 | 327 | 95 | 351 | 351 | 158 | - | 257 | 1.494 |
| Static/wizard_static.png | 1 | 296 | 296 | 296 | 56 | 351 | 351 | 189 | - | 296 | 1.297 |
