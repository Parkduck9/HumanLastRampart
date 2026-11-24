### 1. ANSWER
- **Existing Middle-Click Command Wheel**:
  - **Builder class/method**: `BattleHud` (partial in `BattleHud.Commands.cs`). Initialized by `BattleHud.BuildCommandMenu()`, invoked via `BattleHud.ToggleMenu(side)`, and dynamically populated by `BattleHud.SetOptions()` / `BattleHud.NewMenuItem()`.
  - **Layout (not radial)**: It is not a 360° circular/radial wheel. It is a vertically stacked, curved arc/bulge attached to the hero's chest screen position. Items are vertically spaced (56px) with an outward horizontal bulge (`MenuArc * (1 - k^2)`, where `MenuArc = 48f`), tilted at `MenuSlant = 6f`. Each item features a dark wood sliced band (`command_band_normal`), numbered badge (1–6), label text, optional right-aligned tag, and a gold brush underline (`command_underline`).
  - **Selection**:
    1. Mouse hover proximity dynamically scales the nearest item up to 1.45x (`MenuGrow = 0.45f`, `MenuGrowReach = 70f`) via `UpdateMenuHover()`, and left-clicking triggers `button.onClick`.
    2. Keyboard digits 1–6 trigger `hud.Choose(i)`.
  - **Closing**: Closed via `hud.CloseMenu()` triggered by right-click, middle-click toggle, Escape key, or upon selecting a leaf order (`Issue()`).
  - **Reuse for 3-option wheel + 4-quadrant N/E/S/W picker**:
    - Multi-step branching (`MenuStep`) already exists (`ShowRoot` -> `ShowWords` -> `MenuStep.Target`).
    - *3-option wheel ("1분대 보내기", "1/2 보내기", "전체 보내기")*: Fits the existing vertical arched list format directly.
    - *4-quadrant N/E/S/W picker*: Does not fit the existing layout. To be a true 4-quadrant wheel, `NewMenuItem`/`SetOptions` requires a radial positioning layout (e.g. angular or vector offsets around the center) or mouse angle detection (`Mathf.Atan2`). Alternatively, it can be kept as a 4-item vertical list.
    - *Required modifications*: (1) Enable middle-click input during map mode in `BattleEntry.Update` (currently gated by `!mapOpen`). (2) Anchor menu position (`PlaceMenu`) to mouse screen coordinates or selected marker position rather than `rig.HeroFeet`.

- **Map-Mode Mouse Input in BattleEntry.cs & Box Selection Insertion**:
  - **Left Press (`wasPressedThisFrame && !overUi`)**:
    - `group >= 0` (`hud.GroupAtScreen`): Marker drag branch. Sets `selectedGroup`, `dragStart`, `dragging = true`.
    - `selectedSpell >= 0`: Spell casting branch (`CastSelectedSpellAt`).
    - `selectedCannon >= 0 && cannonMoveArmed`: Cannon repositioning branch (`OrderCannonMove`).
    - `selectedCannon >= 0`: Cannon aiming branch (`AimSelectedCannonAt`).
    - `else`: Empty ground press branch. Sets `panPending = true`, `panMoved = false`, records `panLast` and `panPress`.
  - **Left Drag / Hold (`panPending && isPressed`)**:
    - If movement > 6px (`sqrMagnitude > 36f`), sets `panMoved = true` and pans camera via `mapCamera.DragBy`.
  - **Left Release (`panPending && !isPressed`)**:
    - Resets `panPending = false`. If not moved (`!panMoved`), attempts `DetonateTrapAt(panPress)`; if no trap detonated, deselects `selectedGroup = -1`.
  - **Marker Drag Release (`dragging && !isPressed`)**:
    - Resets `dragging = false`. If dragged > 28px, sends move order via `OrderGroupMove(selectedGroup, SideOfPoint(world))`. If <= 28px, keeps marker selected for popup UI.
  - **Shift+Left-Drag Box Selection insertion point**:
    - Best inserted at the top of left-press handling (`BattleEntry.cs:349-354`): check if Shift is held (`keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed`). If pressed, start box selection (`boxSelecting = true`, `boxStart = point`) and bypass marker picking, spell/cannon, and map pan (`panPending`).
    - In the drag/release section: while `boxSelecting && isPressed`, update `boxCurrent = point`; on release (`!isPressed`), query all markers inside the rect formed by `boxStart` and `boxCurrent`, then reset `boxSelecting = false`. This prevents conflicts with camera pan (`mapCamera.DragBy`), trap detonation (`DetonateTrapAt`), and group deselection (`selectedGroup = -1`).

- **Squad Group Marker Screen Positions (BattleHud.Map.cs)**:
  - Inside `BattleHud.Map.cs`, `RefreshGroupMarkers()` computes and caches the screen pixel coordinates of all active, movable groups (`!info.Movable || info.Alive <= 0` skipped) into `readonly List<Vector2> groupMarkerScreen` and their squad indices into `readonly List<int> groupMarkerIndex`.
  - Position includes an offset (+4m outward or -5m inward from wall) to prevent wall overlap before calling `camera.WorldToScreenPoint(world)`.
  - To test box containment, expose a query method on `BattleHud` (e.g. `GetGroupsInScreenRect(Rect rect)`) that iterates `groupMarkerScreen[i]` and collects `groupMarkerIndex[i]` where `rect.Contains(groupMarkerScreen[i])`.

- **Group Move API in BattleSim.Groups.cs**:
  - **Signatures**:
    - `public bool MoveGroup(int index, int targetSide, bool instant)`
    - `public int MoveWallGroups(int fromSide, int toSide, bool instant)`
  - **KeepPerWall rule**: Constant `KeepPerWall = 20`. Any bulk move order must leave at least 20 movable soldiers on the wall (10 defense garrison + 10 messenger pool). Non-movable cannon crews are ignored. If moving a candidate group would drop total movable soldiers below 20 (`movable - alive < KeepPerWall`), that group is skipped.
  - **Moving "half" of a wall's movable groups**:
    - Collect candidates (`squad.Movable && squad.Side == fromSide && squad.Alive > 0`), counting total movable soldiers.
    - Sort candidates directionally towards `toSide` as done in `MoveWallGroups` so groups closest to the destination move first.
    - Calculate half the candidate count (`candidates.Count / 2`) or half available movable soldiers above `KeepPerWall`.
    - Iterate and invoke `MoveGroup(g, toSide, instant)` for up to half the candidates while enforcing `movable - alive >= KeepPerWall`.

- **Delivery Logic in BattleEntry.OrderGroupMove / OrderWallMove**:
  - Orders follow a 3-tier delivery path:
    1. **Pre-battle setup (`preparing == true`)**: Instant delivery (`sim.IssueGroupMove(..., instant: true)` / `sim.IssueWallMove(..., instant: true)`).
    2. **Combat, Hero on same wall (`rig.Side == wall`)**: Immediate departure without messenger delay (`sim.IssueGroupMove(..., instant: false)` / `sim.IssueWallMove(..., instant: false)`).
    3. **Combat, Hero on different wall**: Dispatches a messenger from hero (`sim.IssueGroupMoveByMessenger(rig.Side, rig.Along, group, targetSide)` / `sim.IssueWallMoveByMessenger(rig.Side, rig.Along, wall, targetSide)`). Squads depart only when the messenger arrives at the destination wall.
  - Any new UI should call `BattleEntry.OrderGroupMove(group, targetSide)` / `OrderWallMove(group, targetSide)` (or an equivalent `OrderWallHalfMove` implementing the same 3-tier branch) to preserve this contract.

---

### 2. EVIDENCE
- `BattleHud.cs:78`: Calls `BuildCommandMenu()` during HUD construction.
- `BattleHud.Commands.cs:66-89`: `BuildCommandMenu()` instantiates root menu container, title, and line graphics.
- `BattleHud.Commands.cs:91-100`: `ToggleMenu(int side)` toggles open state and starts `ShowRoot()`.
- `BattleHud.Commands.cs:103-108`: `CloseMenu()` hides panel and sets `step = MenuStep.Closed`.
- `BattleHud.Commands.cs:111-114`: `Choose(int index)` selects option by digit index.
- `BattleHud.Commands.cs:116-160`: Hierarchical menu step progression (`ShowRoot`, `ShowWords`, `ChooseWord`, `MenuStep.Target`).
- `BattleHud.Commands.cs:162-166`: `Issue()` calls `CloseMenu()` and triggers `CommandChosen`.
- `BattleHud.Commands.cs:168-173, 191-199, 204-205, 220`: Arched layout parameters (`MenuSpacing = 56f`, `MenuArc = 48f`, `MenuSlant = 6f`, `bulge = MenuArc * (1 - k^2)`).
- `BattleHud.Commands.cs:213-279`: `NewMenuItem()` builds band, number icon, label, tag, and underline.
- `BattleHud.Commands.cs:281-289`: `PlaceMenu(screen)` positions menu relative to screen coordinates.
- `BattleHud.Commands.cs:292-313`: `UpdateMenuHover()` computes mouse distance and scales closest item up to 1.45x (`MenuGrow = 0.45f`, `MenuGrowReach = 70f`).
- `BattleEntry.cs:284-288`: Middle-click toggle (`hud.ToggleMenu`) and right-click close (`hud.CloseMenu`), gated by `!mapOpen`.
- `BattleEntry.cs:305-315`: Digit keys 1–6 call `hud.Choose(i)`; Escape key calls `hud.CloseMenu()`.
- `BattleEntry.cs:453-457`: `LateUpdate` anchors menu to `rig.HeroFeet + Vector3.up * 0.25f` when `!mapOpen`.
- `BattleEntry.cs:348-374`: Map-mode mouse press branches (group drag lines 354–362, spell cast line 363, cannon move line 364, cannon aim line 365, empty ground pan pending lines 366–373).
- `BattleEntry.cs:376-394`: Map pan deadzone (>6px, line 382), camera drag (`mapCamera.DragBy`, line 385), and mouse release trap detonation / deselection (lines 389–393).
- `BattleEntry.cs:396-406`: Squad marker drag release deadzone (>28px, line 401) and group move order (`OrderGroupMove`, line 404).
- `BattleHud.Map.cs:418-419`: Declarations of `groupMarkerScreen` and `groupMarkerIndex`.
- `BattleHud.Map.cs:468-528`: `RefreshGroupMarkers()` world offset calculation, world-to-screen projection, and list population (`groupMarkerScreen.Add`, `groupMarkerIndex.Add`).
- `BattleHud.Map.cs:629-640`: `GroupAtScreen(screen)` radius distance search against `groupMarkerScreen`.
- `BattleSim.Groups.cs:16`: `public const int GroupSize = 10;`.
- `BattleSim.Groups.cs:72`: Signature `public bool MoveGroup(int index, int targetSide, bool instant)`.
- `BattleSim.Groups.cs:131`: `public const int KeepPerWall = 20;`.
- `BattleSim.Groups.cs:133`: Signature `public int MoveWallGroups(int fromSide, int toSide, bool instant)`.
- `BattleSim.Groups.cs:146-149`: Directional sorting of candidate squads towards destination wall.
- `BattleSim.Groups.cs:154`: `if (movable - alive < KeepPerWall) continue;`.
- `BattleEntry.cs:125-134`: `OrderGroupMove(group, targetSide)` 3-tier delivery logic (`preparing` -> instant, `hero on wall` -> direct, else messenger).
- `BattleEntry.cs:151-160`: `OrderWallMove(group, targetSide)` 3-tier delivery logic.
- `BattleSim.Commands.cs:197-210, 264`: `IssueGroupMoveByMessenger` and execution upon messenger arrival.
- `BattleSim.Commands.cs:214-226, 265`: `IssueWallMoveByMessenger` (`MoveGroup = -2`) and execution upon messenger arrival.
- `BattleSim.Commands.cs:228-234`: `IssueWallMove` and `IssueGroupMove` methods.

---

### 3. UNSURE
- None. All requested logic, signatures, and line numbers were confirmed directly in the source code.
