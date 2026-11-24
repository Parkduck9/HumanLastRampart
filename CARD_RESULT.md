# K-105 — Beacon title and story backgrounds

## Completed

- Created the Korean working-title logo `빛을 잇는 자들` as transparent PNGs at 1600×560 and 800×280.
- Created four opaque 1920×1080 backgrounds: dark main menu with a lit hilltop beacon above black fog; a city swallowed by the fog tide; arrival at a ruined unlit beacon fortress at dusk; the same fortress relit with a light pillar and retreating fog.
- Newly generated images contain no identifiable hero or face. The fortress scenes contain distant anonymous expedition silhouettes only. Story backgrounds reserve a calm lower third for dialogue.
- Used the built-in image_gen tool for five generations, with the unlit fortress as the edit reference for the relit scene. Original generations and exact prompts are retained. Final exports and 4× previews use nearest-neighbor resizing; logo exports preserve source alpha and add transparent padding.
- Built a standalone Korean review HTML with embedded images, current in-game background comparisons, menu/dialogue/portrait-area overlays, and manual/automatic before/after switching. Existing hero artwork appears only in clearly labelled, unchanged current-game comparison images.

## Files added

- `Docs/References/codex_k105_v1/`: six final PNGs, `sources/` (five originals), `previews/` (five 4× PNGs), `manifest.json`, Korean `README.md`, `prompts.json`, `generation.json`, `build.py`, `verify_assets.py`, `verify_review.cjs`, `validation.json`, `review_validation.json`, and three `review_*.png` browser screenshots.
- `_HTML/6_그림_화면/codex_K-105_제목과이야기배경_v1.html`
- `CARD_RESULT.md`

## Verified

- `verify_assets.py` passed: all 11 exported final/preview PNG dimensions match the manifest; all four backgrounds are fully opaque; both title logos have alpha range 0–255, fully transparent perimeter pixels, and zero isolated visible pixels.
- Visually inspected all five generated originals, desktop/mobile review layouts, and the story overlay layout. Checked Korean title spelling, distant silhouettes, quiet lower thirds, recognizable fortress continuity, and absence of visible stray background marks.
- `verify_review.cjs` passed in headless Chrome: all 13 embedded images decoded; no page errors; overlay toggle, manual switching, and automatic switching work; no horizontal overflow at 390×844.
- Confirmed `story_intro.asset` references the same `ScreenArt/main.png` used by the existing main menu. Its SHA-256 is recorded in the manifest for the read-only comparison.
- Read-only Git status showed only this card's new output paths plus the pre-existing untracked `CARD_CODEX_LOG.jsonl`; no tracked files were modified. No Git write commands were run. The pre-existing log was not edited by this task.
- Unity was never opened or run. Game/, collaboration logs, portraits, soldiers, enemies, and existing wall assets were not modified.

## Remaining / integration notes

- Claude must import/wire the assets and adapt title/background placement. No game code or Unity metadata was changed.
- Unity Play, actual game captures, portrait overlap, final UI readability, and aspect-ratio behavior remain unverified in-game.
- These are static backgrounds, not character sheets. The 96×128 native/288×384 game-cell and foot-baseline requirements do not apply; the manifest explicitly records this. The review's animation is a two-image comparison, not a delivered game animation.
- No blocking questions. The title remains a working title as requested. Source backgrounds were generated at 1672×941 and exported to the requested 1920×1080; 4× files are inspection enlargements, not additional source detail.
