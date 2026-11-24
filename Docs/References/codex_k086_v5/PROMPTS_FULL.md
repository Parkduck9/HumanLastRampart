# 점화 v5 생성 프롬프트 원문


## 생성 1
Use case: precise-object-edit
Asset type: rear-view pixel art cannon crew ignition sprite sheet for an in-game animation.
Input image 1 is the existing approved character design and the six poses. Rebuild the motion with exactly the same character identity: short dark charcoal hair, navy blue military coat with cream edging, brown crossed leather straps on the back, brown gloves and boots, no hat, no facial features visible. Keep chunky crisp pixel art and hard outlines. No new costume.
Critical correction: ONE rigid wooden torch of CONSTANT LENGTH in ALL SIX poses. Do NOT extend the shaft in pose 4. Reach by moving the shoulder, bending the elbows and leaning/lunging the torso toward the LEFT, with boots staying anchored. Right hand rests behind hip or helps the grip. No duplicate hands.
Composition: 1536x1024 transparent sprite sheet, exactly 3 columns by 2 rows, six equal 512x512 cells, read top row then bottom row. Each complete sprite contained fully in its own cell, including flame and soft glow; leave a safety margin. No background, no text, no grids, no cannon.
Torch shaft from gripping hand to flame is approximately 185 pixels long in every frame. The standing soldier hair-to-boot height is approximately 390 pixels. Same scale throughout.
Frame 1: stand upright holding the torch up-left at 55 degrees.
Frame 2: slightly bend torso left, lower torch to 35 degrees.
Frame 3: deeper knee bend and torso lean left, torch 15 degrees up-left.
Frame 4: strongest lean and extended shoulder toward LEFT, rear view, boots still placed under original foot positions; upper torso and head shift left about 105 pixels compared to standing. Torch is almost horizontal but points slightly upward, at shoulder height, NOT at boot/hip height. Flame should be about 440 pixels left and 300 pixels above the foot pivot. The flame remains completely inside cell, not cropped. Constant 185-pixel shaft; achieve reach by full body lean, do not lengthen it.
Frame 5: smoothly draw torso and arm back, torch returns to 35 degrees up-left.
Frame 6: return to EXACT frame 1 pose as a closed loop.
Keep same hair shape and hair part direction in every frame. Consistent shoulders, coat length, body volume. Every pose is viewed from behind. Transparent background with real alpha. No large painterly haze.

## 생성 2
Use case: precise-object-edit. Make ONE new pixel art sprite pose of the SAME cannon crew soldier in input1, NOT a six-frame sheet. Keep rear view, charcoal hair, navy coat with cream trim, brown X back straps and brown boots/gloves. Fully transparent background. Whole body and complete torch flame visible.
Major pose change: deeply lean the entire UPPER BODY diagonally toward image LEFT, about 40 degrees, while both boots stay near the original stance. Shoulder and head must move very far to the LEFT of the hips. Left arm stretches out LEFT, glove at SHOULDER height. Use the exact same SHORT rigid torch from the other poses, only 185 pixels shaft at a soldier standing scale of 390 pixels high. NO stretching the shaft.
Precise composition on a 768x768 transparent square: foot pivot at (565,700), left/right boots near (510,700) and (610,700), hips at (485,558), shoulder at (365,438), head centered approximately (340,332), hand holding torch at (248,399), fire center near (75,369). Torch points slightly UP-LEFT from hand to flame. The torch is almost horizontal at the height of the shoulder, NOT lowered toward the boots. Flame entirely visible with at least 35 pixels clear margin at left. Right arm folds behind torso. Back of head visible, no face. Same body scale/proportions/costume as input. Crisp chunky pixel art. No cannon, no ground, no letters, no grid. This is the strongest reaching contact pose, achieved by body lean and extended arm, NOT a longer stick.

## 생성 3
Use case: precise-object-edit. Edit this same rear-view pixel soldier contact pose. The left canvas has been expanded to allow further reach. Keep both BOOTS and HIP at exactly their existing positions. Keep the same torch and SAME SHAFT LENGTH. Keep hair identity, navy/cream uniform, brown X straps.
Change ONLY upper-body pose: shift shoulder, head, arms, glove and the rigid torch assembly 150 pixels LEFT relative to the hips. Bend the waist farther LEFT; torso must lean diagonally, not remain upright. Hips, thighs, knees and both boot positions remain unchanged. Head, left shoulder, left gripping hand and torch flame all move left 150 pixels together. Torch still angles very slightly UP-LEFT, its flame at shoulder height. Do not add shaft length. Do not shift the whole soldier. This gives a pronounced side lunge, left hand far from feet. Keep enough transparent margin around the entire torch fire.
Rebuild the coat/waist connection naturally around the lean. Right elbow stays folded behind torso. Preserve pixel art, no text, no cannon, no extra limbs. Fully transparent background. The flame and the boots must NOT be cropped.

## 생성 4
Use case: precise-object-edit. Edit ONLY a very small ignition fixture on the rear upper part of this existing pixel art cannon. Keep the entire cannon shape, perspective, barrel, wheels, wood chassis, colors, lighting, size and placement unchanged.
Canvas 768x768 as input, transparent background.
At image coordinate (384,402), which is the UPPER CENTER of the large circular rear breech cap, add one tiny dark metal touch-hole ring and a short pale tan braided fuse wick pointing slightly upward-right. Ring around 10x10 pixels on this 3x enlarged image, wick around 7 pixels long. The whole change must fit inside rectangle x354..414, y372..432. It must look physically attached to the cannon's upper rear cap, not floating. Unlit tan fuse; no flame, no glow, no smoke. Preserve crisp existing pixel art at the same dot scale. No arrows, labels, circles, crosshairs or text. Leave everything else exactly as input. Real alpha transparency.

