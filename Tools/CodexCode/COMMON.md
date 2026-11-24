[Common rules for Codex CODE work — HumanLastRampart, user decision 2026-10-20: Codex writes code, Claude only verifies]
- You work in the main checkout C:\Unity\1\HumanLastRampart on branch develop. Unity 6000.3, C# under Game/Assets/_Project/.
- Read first: AGENTS.md, Docs/GDD/Playtest_Feedback_20_handoff.md (progress table + per-item notes), Docs/GDD/Playtest_Feedback_20_v1.md section 23 (user decisions, Korean), Docs/GDD/Loop_Engineering_v1.md section 3 (work loop).
- Do ONLY the item(s) in the brief. Match the surrounding code style: Korean comments, same naming, same comment density. Keep changes minimal and focused.
- Allowed to edit: Game/Assets/_Project/** (.cs code and tests; add .cs files without .meta — Unity creates metas), and the Progress table rows of Docs/GDD/Playtest_Feedback_20_handoff.md for your item.
- NOT allowed: git writes (add/commit/branch/merge), running or opening Unity, editing art PNGs, ProjectSettings, packages, Docs/collab/**, other docs.
- Do not break existing behaviour: plain left-drag on the map must still pan, existing marker drag/popup must keep working, existing tests (Game/Assets/_Project/**/Tests, _PlayProbe) must still compile. If you change behaviour a test asserts, update that test and explain why in the result file.
- Add or update at least one automated test (EditMode under Core/Tests or Combat/Tests, or a PlayProbe step) that proves the new behaviour.
- When done, write the result file named in [Result file] in ENGLISH: what you changed (file:line), the test(s) you added, what Claude must verify by running Unity tests / photos, open questions. Then stop.
- In-game text and code comments stay KOREAN. Never use English UI strings.
