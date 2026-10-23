# 소리 열쇠 목록 v1 (W24)

소리 파일을 구하면 `Resources/Audio/SoundBank` (메뉴 HLR/Sound Bank로 만든 자료)의 항목 `Id`에 아래 열쇠를 그대로 적고, 파일·크기·높낮이 범위·동시 수·쿨다운을 채운다. 항목이 비어 있으면 게임은 조용히 넘어간다 (경고 없음).
코드 쪽 목록은 `Core/Audio/AudioCues.cs` 한 곳이다. 열쇠를 더하면 코드와 이 문서를 함께 고친다.

- 갈래(bus): Master(전체, 설정 창 "소리 크기") / Music(설정 "음악 크기") / Sfx·Ui·Ambience(설정 "효과음 크기"를 따름)
- 묶음: 같은 프레임에 여러 번 알려진 소리는 한 번만 울리고 개수가 많을수록 조금 크게(최대 1.8배) 나온다
- 동시 소리 상한: PC 24 / 모바일 12 (소리마다 동시 수·쿨다운은 은행 항목에서 정한다)
- 권장 형식: 효과음 mono 44.1kHz ogg (3D로 쓸 것은 반드시 mono), 음악·주변 소리 stereo ogg, 반복 소리는 이음매 없이

| 열쇠 | 갈래 | 설명 | 권장 길이 | 반복 | 불리는 곳 |
|---|---|---|---|---|---|
| sfx_bow_volley | Sfx | 활 일제 사격 (여러 발을 한 소리로) | 0.8초 | 아니오 | 투사체 수 늘 때 (BattleAudioDirector) |
| sfx_crossbow_volley | Sfx | 석궁 일제 사격 | 0.7초 | 아니오 | (아직 안 불림: 무기 종류 구분 필요) |
| sfx_musket_volley | Sfx | 머스킷 일제 사격 | 1.0초 | 아니오 | (아직 안 불림: 무기 종류 구분 필요) |
| sfx_cannon_fire | Sfx | 대포 발사, 낮고 큰 폭음 | 1.8초 | 아니오 | 대포가 쏠 때 (BattleEffectsView) |
| sfx_cannon_impact | Sfx | 대포알 착탄 폭발 | 1.5초 | 아니오 | 대포알 착지 |
| sfx_magic_cast | Sfx | 마법 시전, 모으는 소리 | 1.0초 | 아니오 | (아직 안 불림) |
| sfx_magic_explosion | Sfx | 마법 폭발 | 1.8초 | 아니오 | 마법 폭발 순간 |
| sfx_trap_burst | Sfx | 함정 폭발 | 1.2초 | 아니오 | 함정 터질 때 |
| sfx_fire_pool | Sfx | 불바다 생길 때 타오르는 소리 | 2.0초 | 아니오 | 새 불바다 |
| sfx_arrow_hit | Sfx | 화살이 땅·적에 맞는 소리 (묶음) | 0.4초 | 아니오 | 화살 착지, 프레임당 한 번 |
| sfx_enemy_hit | Sfx | 적이 맞는 소리 (묶음) | 0.4초 | 아니오 | (아직 안 불림) |
| sfx_enemy_death | Sfx | 적이 쓰러지는 소리 (묶음, 많을수록 크게) | 0.8초 | 아니오 | 죽은 수 늘 때 |
| sfx_wall_hit | Sfx | 성벽이 맞는 둔탁한 소리 | 0.7초 | 아니오 | 성벽 체력 줄 때 |
| sfx_wall_breach | Sfx | 성벽이 무너지는 소리 | 2.0초 | 아니오 | 성벽 체력 0 |
| sfx_wave_horn | Sfx | 단계 시작 뿔나팔 (선봉·본대·공성·최후 돌격) | 3.0초 | 아니오 | 전투 단계 바뀔 때 |
| sfx_beacon_ignite | Sfx | 봉화 점화 | 2.5초 | 아니오 | 등불 밝기 바꿀 때 |
| sfx_beacon_beam_turn | Sfx | 봉화 빛줄기 돌리는 윈치 소리 | 2.0초 | 가능 | (아직 안 불림) |
| sfx_courier_run | Sfx | 전령이 뛰어가는 발소리 | 0.8초 | 아니오 | 전령 출발 |
| sfx_result_stinger | Sfx | 결과 화면 짧은 곡 (승리·패배) | 4.0초 | 아니오 | 결과 화면 진입 |
| ui_click | Ui | 단추 누름 | 0.15초 | 아니오 | 모든 UI 단추 (Ui.Button) |
| ui_confirm | Ui | 확인·결정 | 0.4초 | 아니오 | (아직 안 불림) |
| ui_cancel | Ui | 취소·닫기 | 0.3초 | 아니오 | (아직 안 불림) |
| ui_story_tick | Ui | 이야기 글자 한 칸 나옴 | 0.05초 | 아니오 | (아직 안 불림) |
| music_day | Music | 낮(준비·지도·메인) 배경 음악 | 90초 | 예 | 화면 바뀔 때 (GameFlow) |
| music_night | Music | 밤 전투 배경 음악 | 90초 | 예 | 밤 전투 시작 |
| music_battle | Music | 낮 전투 배경 음악 | 90초 | 예 | 낮 전투 시작 |
| music_story | Music | 이야기 화면 배경 음악 | 90초 | 예 | 이야기 화면 |
| amb_wind | Ambience | 성벽 위 바람 | 30초 | 예 | 전투 중 |
| amb_crickets | Ambience | 밤 귀뚜라미 | 30초 | 예 | 밤 전투, 적이 많으면 줄어듦 |
| amb_crowd_roar | Ambience | 적 무리 함성 | 30초 | 예 | 살아 있는 적 수에 따라 커짐 |
