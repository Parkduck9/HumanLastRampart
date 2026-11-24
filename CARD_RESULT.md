# K-140 result

## Card-specific avoid list
- Do not mix pixel sizes, blur edges, or add stray pixels around the four poses.
- Do not use airbrushed gradients, bloom, haze, or glossy plastic shading; use restrained, directional stepped shading.
- Do not obscure or crop either boot: preserve readable toes, heels, and dark soles with clear margins.
- Do not let the face, costume, proportions, or lighting drift between frames; avoid decorative clutter.
- Do not repeat symmetric or ambiguous poses: make the leg and opposite arm swings distinct and readable.

References consulted: https://www.sbgames.org/sbgames2019/files/papers/ComputacaoFull/197880.pdf and https://pixelartworkshop.com/guides/troubleshoot-jaggies-mixels-pixel-art-basics

## Work and files
- Generated one selected four-pose, left-facing side-run concept using the built-in image generation tool and the three specified references. The selected original remains at `C:\Users\User\.codex\generated_images\01a19af1-bca1-76a1-ad15-52cd4e47b682\exec-dca43224-df90-4135-a62b-48b355e74ed6.png`.
- Added `Docs/References/codex_k140_v1/hero_run_side_concept.png` and its Korean `README.md`. No Game files were changed. This file is the handoff report.
- Prompt: four distinct left-facing run poses matching the current brown-haired hero and accepted K-139 pixel-art style, with separated full figures, opposite arm swings and complete boots with toes, heels and dark soles, on transparency.

## Verification and leftovers
- PNG is 2128 x 739 with an alpha channel; it contains transparent pixels. Visual inspection shows four separated figures, with their major silhouettes within the image.
- The output does not fully satisfy the requested motion/boot quality: arm swings are similar across the poses, and the trailing boots in poses 2 and 3 lack clear toe/heel articulation. Further art revision is needed before use as a run cycle.
- Alpha inspection found low-opacity stray pixels extending to the bottom edge (alpha >= 16 is bounded by x=80..2032, y=115..643). Cleanup is required. A foot baseline and loop timing were not validated: this is an unaligned concept, not a game sheet.
- No Unity play test was performed. The worktree does not contain the ignored collaboration live files, so their messages and reservations could not be checked here. No review HTML or native/game sheets were created because the card specifically requests one unaligned concept and README.
