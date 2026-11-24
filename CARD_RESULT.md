# K-107 — Lumen Pharos redesign proposals

## Completed

Created three static design proposals for Lumen Pharos, male, 20, newly graduated from the imperial academy and heir to the lamp-keeper bloodline. Each has front, left side, back, and back-facing raised-right-arm command poses, a neutral portrait bust, a belt lantern, a brass telescope, and a lamp-keeper mantle/insignia.

- A: chestnut asymmetrical fringe, petrol-teal academy uniform, ivory mantle.
- B: dark navy parted hair, ivory academy uniform, teal asymmetrical mantle.
- C: sandy blond open-forehead fringe, muted plum academy uniform, slate-blue mantle.

Used the built-in image_gen tool for six generation jobs. Full prompts are preserved in prompts.json. Generated originals are retained alongside normalized outputs. No CLI/API fallback was used.

## Files changed

Only new files within the authorized output locations:

- `Docs/References/codex_k107_v1/`: 51 PNGs, Korean README.md, manifest.json, prompts.json, palette.json, placement.json, validation.json, review_validation.json.
- `_HTML/6_그림_화면/codex_K-107_주인공새디자인_시안_v1.html`: Korean standalone review page with embedded images, scale controls, static-pose cycling, original/new comparison, portraits, and night wall comparisons.
- `CARD_RESULT.md`: this English handoff.

PNG groups: three generated turnarounds; twelve individual native poses; three native turnaround sheets and their 3x game, native 4x, and game 4x versions; three generated portraits with final 256x384 and 4x versions; two original-hero reference sheets; front/back/command night comparisons in native/game/game-4x sizes; four browser QA captures.

No Game files, other documentation, collaboration logs, existing index pages, or Unity metadata were edited. No Unity execution or Git writes occurred.

## Verified

- All 12 design poses use 96x128 native cells, 73px figure height, last opaque row 119, and foot boundary y=120. Boot-axis midpoint remains within 1px of x=48. Cell borders are transparent; alpha is binary; no detached native components smaller than four pixels remain.
- All nine turnaround enlargement outputs are pixel-identical to nearest-neighbor enlargement by the specified factors. Game cells are 288x384 with foot pivot (144,360). Portraits are 256x384 with binary alpha and exact 4x previews.
- Current in-game reference sizes were inspected: walk/run 1152x1536 (4x4), back idle 288x384, command 864x384 (3 frames), story portrait 256x384.
- Front, back, and command comparison boards preserve old/A/B/C order and apply a uniform RGB multiplier of 0.65 to the composite. They use the repository's wall_a.png; these are comparison mockups, not game captures.
- Headless Chrome opened the review page directly through file://. All eight visible image elements and eleven embedded image resources decoded; all six pose canvases rendered. Scale controls produced 96x128, 384x512, and 288x384 canvases. Command selection, automatic pose advancement, and night comparison switching passed. No page errors, external requests, or page overflow at 390px viewport width.
- Visually inspected generated art, normalized previews, night lineup, desktop review, variant A section, and mobile review. Browser captures and machine-readable results are included.
- SHA-256 aggregate of all 1,671 files under Game/ and Docs/collab/ is identical before/after: 90f6e086da3a18caac7d032857c77a03c2d317815f0f0bbd09c46780a56b64a8.

## Open decisions and limits

1. User selects A/B/C before K-108. Do not integrate any proposal as a final design yet.
2. Existing story portraits are full-body, while this brief explicitly requests busts. New portraits keep the same 256x384 canvas and central placement but use bust framing, so face scale differs. This is disclosed in the Korean review page and README; confirm final framing alongside design selection.
3. Four static poses are a design turnaround, not a completed animation. The review's cycling is a pose slideshow. Full walk/run/idle/command frames and rig alignment against every original motion frame remain for K-108. Static pivot checks do not establish animation stability.
4. Actual Unity camera, lighting, lantern emission, Play testing, and screenshots were not performed. The wall mockup cannot establish in-game readability under actual rendering.
5. Native downsampling and restricted palettes simplify the telescope and insignia. Review at native/game sizes before approval. Generated sources are not the delivery cell format and retain their original soft alpha/color detail.
