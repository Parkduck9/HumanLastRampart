# 캐릭터 노멀맵 견본 v1 — T-CX-048

작성 Codex / 2026-10-15 / M-CL-066·D-062 G4 / 사용자 검토 전, Game 미연결.

## 견본 두 개

| 대상 | 색 원본 | 크기·배치 | 출력 |
|---|---|---|---|
| 주인공 | codex_hero_grid_v2/hero_walk_run_grid.png | 1024×1536, 4열×4행, 칸256×384 | hero_normal.png |
| 궁수 후면 | Game/Assets/_Project/Art/Characters/Static/archer_static.png | 256×384, 1열×1행 | archer_normal.png |

두 색 파일은 복사본 `*_albedo.png`로 보관하고 원본과 SHA256이 같다. 피벗은 각 칸 발128·352 / Unity정규화(0.5, 0.0833333333). 주인공의 기존 후면걷기/좌걷기/좌달리기/정면걷기 순서와 프레임은 바꾸지 않는다. 새 후면정지 에셋의 노멀맵을 추가 제작한 것은 아니다.

## 제작·출력

- image_gen.imagegen으로 색 그림을 참고해 표면 방향 원안 두 개 생성. 프롬프트·참고·원본 경로는 generation.json, 원안은 *_normal_source.png.
- 최종 출력은 같은 전체 캔버스로 최근접 맞춤, 원본 알파 마스크에 따른 빈 영역 정리, RGB 탄젠트 방향의 재정규화 등 기술 데이터 인코딩이다. 색 그림을 다시 그리거나 부분 이동하지 않는다.
- 궁수 생성본은 원래 발보다 약6px 아래로 늘어나 있었다. 각 칸의 노멀 원안 바운드를 색 그림 바운드에 칸 전체로 정렬한 뒤 알파 마스크로 제한했다. 셀별 원안/색 바운드는 JSON의 cellRegistration에 기록한다. 머리·팔다리를 나눠 보정하지 않는다. 내부 세부 대응의 근사 한계는 여전히 남는다.
- 생성본의 빈 공간이 정확한 평면색이 아니어서 배경 중간값을 기준으로 XY를 중심화. XY세기0.55의 약한 굴곡 견본. 실제 투명도는 색 그림 alpha 사용, alpha≤32 영역은 노멀 RGB(128,128,255). 원본의 모든 완전투명 픽셀에서 이 값과 정확히 같다.
- OpenGL/Unity +Y: R은 접선 오른쪽, G는 접선 위쪽, B는 표면 앞쪽. 벡터 길이를 정규화한 불투명 RGB PNG다. 8비트 인코딩 오차 허용범위0.012 안.
- **AI 내부 표면·눈·옷 주름의 미세한 대응은 근사**다. 색 캔버스/칸/마스크가 맞는 것과 정확한 3D 베이크는 다르다. 사용자에게 색·노멀·조명/평면 비교를 함께 보여 주고 필요 부분은 재생성한다. 본 견본은 3D 모델 베이크나 Unity 검증을 완료한 자료가 아니다.

## 검토 HTML

`_HTML/codex_캐릭터노멀맵견본_v1.html`: 대상/주인공4행·4칸, 재생/일시정지, 광원 방향/높이, 평면 노멀 비교. 색·노멀·빛적용 세 창. 모든 그림 내장, 서버 없이 열 수 있는 독립 HTML. CPU Lambert·간단한 감마 근사이며 실제 URP와 동일한 렌더가 아니다.

## Unity 연결 요청 (Claude)

- Normal map 타입·선형 데이터·초록 뒤집기 끔(Y+)을 기본으로, NPOT 스케일 None / MaxSize≥2048로 주인공1024×1536 유지. 색과 노멀의 동일 UV행열/피벗·Point·Clamp, 초기 견본 압축 없음/밉맵 끔으로 비교 권장. 실제 프로젝트 설정은 Claude 검토 후.
- 노멀 텍스처는 Unity 인코딩/압축과 셰이더의 UnpackNormal 계열 처리를 맞춘다. 단순 RGB 샘플과 Unity 패킹을 혼용하지 않는다.
- 빌보드의 접선/위/앞 방향과 Y+, 좌우반전 시 normal X 방향을 함께 검증한다. 색 알파를 노멀 알파로 대체하지 않는다. 그림자 실루엣은 색 알파를 쓴다.
- 태양/좌우/위/횃불 광원, 4행 모든 프레임, 반전·모서리카메라·점멸·발 위치를 실제 캡처 비교. 에셋/셰이더/계약 추가는 Claude 소유권과 기존 승인 절차를 따른다.

## 현재 검증과 다음 순서

원본 색 해시·복사 일치, 캔버스와 칸·피벗 메타데이터 일치, 완전투명 영역 평면색, 유한 단위벡터 확인. 브라우저 실제 확인은 qa.json에 기록. Unity Play/임포트/3D 실제 조명은 미검증.

**견본 사용자 확인 전에는 총병·대포병·마법사·고블린·오우거·사다리 고블린·다른 달리기/걷기 노멀맵을 추가 제작하지 않는다.**

참고: [Unity Normal map 형식 +Y](https://docs.unity.cn/2021.3/Documentation/Manual/StandardShaderMaterialParameterNormalMap.html), [노멀맵 가져오기](https://docs.unity.com/en-us/engine/7000.0/manual/materials-and-shaders/built-in/shader-built-in-configure-properties/standard-shader-texture-maps/material-parameter-normal-map/import), [Unity 선형 비색상 데이터](https://docs.unity.cn/6000.3/Documentation/ScriptReference/TextureImporter-sRGBTexture.html). 실제 Unity 6.3 프로젝트에서 확인할 것.

## 브라우저 검토 기록

주인공4행16칸 방문·자동재생 갱신23→125 확인, 궁수 로드와 정지버튼 비활성, 광원방향/높이·평면비교 확인. 셀 바운드 정렬 뒤 최종 캡처 확인. 콘솔오류0·기본창 가로넘침 없음.
Unity Play·실제 모형·셰이더·4K·file 주소 직접 실행은 미검증.
