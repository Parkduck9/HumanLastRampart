# K-027 생성 프롬프트 원문

내장 imagegen 도구를 사용했습니다. 외부 검색·유료 에셋·CLI/API 대체 경로는 사용하지 않았습니다.
메인 보정 입력: 이 카드의 최초 메인 그림(편집 대상), 기존 hero_back_idle.png(인물 외형 참고).
아래 영문은 생성 도구에 전달한 원문이며, 사용자 표시 문구나 코드 주석이 아닙니다.

## 메인 최초 생성

```text
Use case: stylized-concept. Create ONE original game background, 1920x1080 16:9 landscape, no collage. Main menu for Human Last Rampart. HD-2D pixel-art miniature diorama: crisp visible pixel clusters on stone and a small sprite-like officer, dimensional layered landscape, restrained bloom, light tilt-shift only at extreme edges. A lone officer seen strictly from behind stands on a fortress rampart at the rightmost quarter (x=80%), looking toward tiny distant enemy hordes across the plain. Dark olive military coat, modest brass shoulder trim, boots, no giant cape. Sunset amber horizon, muted forest green shadows, charcoal stone, desaturated cream sky. Wall runs horizontally, deep panoramic distance. Left half and central 50% are calm shadowed sky/haze and low contrast wall paving, reserved for readable title and central UI panel. Detail and orange rim light at right edge, sparse banners at outer edge. Atmospheric ominous dignity. No text, no letters, no logo, no UI, no border, no watermark. This is a review concept, not a screenshot. Save image output and return its local path.
```

## 메인 인물 보정 · 최종

```text
Edit target image 1, existing main menu fortress sunset background. Image 2 supporting character design reference only. Change ONLY the officer at right: give him short tousled SILVER WHITE hair (not dark hair), BLACK officer tunic with GOLD piping, gold epaulettes, brown belt, brown high boots, shorter split tunic hem above the knees, as in supporting pixel character. Seen strictly from behind. Keep the same officer position, overall scale, facing direction and standing pose. Preserve every other part: sunset, skyline, wall, enemy hordes, composition, empty title area, pixel-art diorama style, lighting. Keep no text, no logo, no UI. Return wide landscape background and saved local path.
```

## 승리

```text
Use case: stylized-concept. ONE original 1920x1080 16:9 background for a victory results screen of a fortress defense game. HD-2D pixel-art miniature diorama matching dark olive / charcoal fortress stone and brass amber lighting. View from slightly above along a broad stone rampart after the battle. Wall remains standing, battered crenellations at outer left and right, a few tiny surviving soldiers ONLY at far right edge, upright muted olive banner at far left. Pale warm late evening light breaks through smoke beyond the battlements, distant battlefield now silent. Smoke wisps at outer edges, hope mixed with loss, no active battle. Central half is quiet low contrast shadowed stone walkway and distant haze, no strong silhouette or flame inside x25%-75%, reserved for 960x740 opaque green results panel. Stronger silhouettes and warm sunlight only on outer 20%. Crisp pixel clusters and pixel-textured 3D depth, subtle atmospheric layers and restrained edge tilt-shift, no painterly brushwork. No text, no logo, no UI, no frame, no watermark. Return saved local path.
```

## 패배

```text
Use case: stylized-concept. Create ONE original game background 1920x1080 landscape 16:9 for DEFEAT result screen, a companion to a victory rampart image with charcoal pixel-textured stone, dark olive army banners and layered miniature depth. HD-2D visible crisp pixel clusters, 3D diorama perspective, subdued bloom and slight edge tilt-shift. After the battle on a fortress rampart. Breached broken battlements at far left and right, a collapsed dark olive banner hanging at left edge, discarded equipment along the edges, no bodies, no gore, no people. Cold overcast blue-gray smoky dusk, tiny dying orange embers far right, distant smoking fortress silhouettes. Central half is broad quiet low-contrast dark stone walkway fading into gray smoke, empty and unobtrusive behind a central 960x740 opaque green report panel. Drama concentrated at outer 20%, absence and defeat conveyed by broken masonry and banner. Camera slightly elevated, stone parapet horizontal across middle distance. Original fantasy architecture. No text, no lettering, no UI, no logo, no watermark. Return saved local path.
```

## 정비

```text
Use case: stylized-concept. ONE original 1920x1080 landscape 16:9 game background: preparation screen, interior courtyard of the last fortress after battle. HD-2D miniature diorama, visible small crisp pixel clusters on stone and timber, sprite-scale small props, layered 3D depth with restrained bloom and mild extreme-edge tilt-shift. Dark olive / charcoal / warm brass palette. Camera slightly elevated looking across a broad empty stone-paved yard. LEFT OUTER EDGE: forge under timber roof with small amber furnace, anvil and barrels. RIGHT OUTER EDGE: modest barracks stone building with muted olive awning, orderly weapon racks and crates. Back wall and towers with faint smoke rising into overcast dusk, safe but watchful. Center 50% is quiet desaturated low-contrast paving and faint haze, no large person, no bright light, no busy focal prop, reserved for a 960x740 opaque green central preparation UI window. Buildings and meaningful warm light mostly in outer 20%, keep forge visible at x0-20%, barracks visible at x80-100%. No large characters. Original fantasy environment, readable atmospheric pixel diorama, not photorealistic. No text, no logos, no UI, no frame, no watermark. Return saved local path.
```
