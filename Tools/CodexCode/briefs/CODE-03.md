CODE-03 — Telescope lens afterimage (items 11/19, user-confirmed 2026-10-20).

Rule: the telescope is right-click-hold on the battle map (BattleEntry.cs:408-414 UseTelescope, BattleSim.Telescope.cs, BattleView.cs:24-26 and :794-817 lens drawing, shader PerfLabSprite.shader:235-239 lens branch). While held, enemy dots inside the lens circle are shown even beyond map sight. NEW (only when the hero has the telescope lens item equipped — item_telescope_lens, UpgradeRules / BattleBoosts.TelescopeRadiusMul): when an enemy dot LEAVES the circle (lens moved away or enemy walks out) or the player RELEASES right-click, that enemy's LAST position inside the circle stays drawn on the map for 4 s, fading out. Use a small fixed-size buffer (e.g. 512 afterimages), oldest replaced. Draw them with the existing map dot rendering (another instance list with alpha) or HUD images — keep it cheap. Keep the existing 1.4× radius effect.
Without the lens: behaviour unchanged.

Tests: EditMode/PlayProbe proving an enemy seen in the lens then outside it remains in the afterimage list for ~4 s and is gone after 5 s; no afterimages without the lens item. Update handoff Progress row 11/19.
