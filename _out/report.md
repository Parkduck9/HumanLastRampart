# W9 second pass (commit 72a299c, branch work/W9-beacon)
Tests: EditMode 229 pass / 0 fail (1 skip); PlayMode 56/56 (EasyFullRun failed once on a full run, passed alone and on rerun: flaky soldier-count assert, unrelated).
B1: 3 tower couriers (BattleSim.Commands.cs). Standby at tower, immortal (TowerMessengersMortal=false). Order -> free courier runs TowerRoute (~5 s, notice shows seconds); 4th queues with notice; SquadOrders counted only via SquadOrderStarted.
S1: stairs use sim time (TowerDelta, 0 when paused/map-frozen); Interact blocked when paused. S2: same-side ignored, 6 s x |delta|/90 (180 = 12 s), ceil status. S3: workers on sim.Elapsed. S4/S5/S6/N1-N7 done; K-110 art wired (capstan overlay skipped).
Yard: tower is a 5th obstacle (blocks walking), flame refreshes after repair, Esc closes the modal, repair button removed from yard panel (top-line button stays).
Photos: _out probe dirs under %TEMP%\claude\W9out\photos4/photos5 (tower_01..08, yard_04 tower, yard_05 panel).
Open: 등대 sign plate overlaps tower art in yard; capstan overlay not wired; scales (flame/pillar/burst) tuned by eye once.
