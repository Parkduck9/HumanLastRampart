CODE-04 — Free trap placement in the trap workshop (item 9, user decision: fully free).

Today traps use grid cell ids "cell_{side}_{col}_{row}" (Core/Traps/TrapGrid.cs). Earth walls/hills already use free ids "free:x:z:rot" with a mouse-follow ghost (Meta/FreePlacementInput.cs, Meta/TrapPreparationPanel.cs FreeMode/PaintGhost/PlaceFree, Core/Terrain/TerrainLayout.cs TerrainRules.FreeId/TryParseFree). Do the same for traps:
- SlotId for new traps: "free:x:z" (or reuse FreeId with rotation 0). ActualX/Z = MarkX/Z = the point. Battle already uses ActualX/Z (Combat/Battle/BattleSim.Traps.cs:74) — no battle change expected.
- Meta/TrapPreparationTransactions.cs: IsPlaceable (:79), Occupied (:87 → distance test: no other trap within the trap's radius), TryCommit (:117-124), CanBuyMany (:142-149), Validate (:198-201 accept free ids AND old cell ids). A trap must be inside the trap band (TrapNear..TrapFar from the wall, any side incl. corners) and on trap ground (TerrainCell.TrapGround at that point — not earth wall/hill/trees/rocks).
- Meta/TerrainWorkTransactions.cs: overlap with traps via layout.CellOf(trap.ActualX, trap.ActualZ) instead of CellOfTrapCell.
- Panel (trap tab): mouse-follow ghost with the blast-radius ring (green valid / red invalid), left-click picks, click a picked one to remove, "N개 묻기" button. Old saved cell-id traps still show and work.
- Update probes/tests that build cell ids for traps where needed (SimProbe.cs:869-872, PlayProbe.cs:233-237/300/346-356, CampaignSaveTests.cs:39, BattleTrapTests.cs:36/112) — keep old-id tests too (save compatibility).
Tests: free trap placement + spacing rule + old save with cell ids still validates. Update handoff Progress row 9.
