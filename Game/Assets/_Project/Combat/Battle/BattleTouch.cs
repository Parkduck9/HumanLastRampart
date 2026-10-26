using System;
using HumanLastRampart.UI;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // W40 전투 터치 조작 (K-125 시안 배치, Docs/References/codex_k125_v1/previews/mockup_*.png). 휴대 기기에서만 보인다 (TouchInput).
    // 성벽 위: 왼쪽 아래 이동 패드(→ 카메라 WalkInput·DepthInput) · 오른쪽 아래 상호작용·계단·도움말·지도·빠르기·멈춤
    // 등대 탑: 성벽 선택판(→ TowerUp) · 빛줄기 명령 · 내려온 성벽으로 · 뒤돌기(내려가는 중)
    // 지도: 왼쪽 아래 끌어 고르기/명령 표적 모드 · 멈춤·마법·대포·확인은 오른쪽 판(대포·등불) 왼쪽 옆 줄, 취소는 오른쪽 아래 모서리.
    public sealed class BattleTouch : IDisposable
    {
        enum Context { None, Wall, Tower, Map }

        readonly BattleEntry entry;
        readonly TouchLayer layer;
        readonly TouchMoveSlider slider;
        readonly TouchButton interact, stairsDown, help, wallMap, wallPause, wallSpeed;
        readonly TouchCompass compass;
        readonly TouchButton beam, stairsUp, turnBack, towerMap, towerPause;
        readonly TouchButton boxMode, orderMode, mapPause, spell, cannon, confirm, cancel;
        readonly TouchWidget[] wall, tower, map;
        Context context;
        bool driving;

        public TouchLayer Layer => layer;
        public TouchMoveSlider Slider => slider;
        public TouchCompass Compass => compass;
        public TouchButton Interact => interact;
        public TouchButton StairsDown => stairsDown;
        public TouchButton Confirm => confirm;
        public TouchButton BoxMode => boxMode;
        public TouchButton Cancel => cancel;
        public TouchButton Beam => beam;
        public TouchButton TurnBack => turnBack;
        public TouchButton StairsUp => stairsUp;
        public TouchButton OrderMode => orderMode;
        public TouchButton WallMap => wallMap;

        public BattleTouch(BattleEntry entry, Transform parent)
        {
            this.entry = entry;
            layer = TouchLayer.Create("Battle Touch Layer", parent);
            layer.ShowRing = true;

            // 성벽 위 (mockup_wall)
            slider = layer.AddMoveSlider(60f, 750f);
            interact = layer.AddButton("interact", 96, "interact", 1740f, 816f, OnInteract);
            stairsDown = layer.AddButton("stairs", 72, "stairs_down", 1644f, 828f, () => entry.Interact());
            wallSpeed = layer.AddButton("speed", 72, "speed", 1704f, 606f, entry.CycleSpeed);
            wallPause = layer.AddButton("pause", 72, "pause", 1800f, 606f, entry.TogglePause);
            help = layer.AddButton("help", 72, "help", 1692f, 714f, entry.ToggleHelp);
            wallMap = layer.AddButton("map", 72, "map", 1788f, 714f, entry.PressMapKey);
            wall = new TouchWidget[] { slider, interact, stairsDown, wallSpeed, wallPause, help, wallMap };

            // 등대 탑 (mockup_lighthouse)
            compass = layer.AddCompass(1488f, 720f, side => entry.TowerUp(side));
            beam = layer.AddButton("beam", 96, "beam", 1764f, 822f, () => entry.Interact());
            turnBack = layer.AddButton("turn_back", 72, "turn_back", 1764f, 714f, () => entry.TurnBack());
            stairsUp = layer.AddButton("stairs_up", 72, "stairs_up", 1668f, 930f, entry.LeaveTower);
            towerMap = layer.AddButton("tower_map", 72, "map", 1764f, 930f, entry.PressMapKey);
            towerPause = layer.AddButton("tower_pause", 72, "pause", 1764f, 606f, entry.TogglePause);
            tower = new TouchWidget[] { compass, beam, turnBack, stairsUp, towerMap, towerPause };

            // 전투 지도 (mockup_map)
            boxMode = layer.AddButton("box_mode", 96, "drag_select", 60f, 834f, () => entry.BoxSelectMode = !entry.BoxSelectMode);
            orderMode = layer.AddButton("order_mode", 72, "order_target", 180f, 846f, () => entry.BoxSelectMode = false);
            mapPause = layer.AddButton("map_pause", 72, "pause", 1130f, 876f, entry.TogglePause);
            spell = layer.AddButton("spell", 72, "spell", 1214f, 876f, entry.CycleSpell);
            cannon = layer.AddButton("cannon", 72, "cannon", 1298f, 876f, entry.CycleCannon);
            confirm = layer.AddButton("confirm", 96, "confirm", 1376f, 864f, entry.ConfirmMap);
            cancel = layer.AddButton("cancel", 72, "cancel", 1800f, 948f, entry.CancelSelection);
            map = new TouchWidget[] { boxMode, orderMode, mapPause, spell, cannon, confirm, cancel };
            SetContext(Context.None);
        }

        // 큰 단추: 계단 가까이·탑 앞·내려가는 중이면 E, 아니면 명령 메뉴(병사에게 말 걸기, 휠 버튼과 같다)
        void OnInteract()
        {
            BattleCameraRig rig = entry.Rig;
            if (rig != null && (rig.AtTower || rig.InTransition || (rig.OnWall && rig.NearStairs))) entry.Interact();
            else entry.ToggleCommandMenu();
        }

        void SetContext(Context next)
        {
            context = next;
            foreach (TouchWidget w in wall) w.Visible = next == Context.Wall;
            foreach (TouchWidget w in tower) w.Visible = next == Context.Tower;
            foreach (TouchWidget w in map) w.Visible = next == Context.Map;
            if (next == Context.Tower)
            {
                // 내려가는 중에는 뒤돌기만, 탑 앞에서는 나머지 (성벽 선택판·빛줄기·올라가기)
                bool at = entry.Rig != null && entry.Rig.AtTower;
                compass.Visible = beam.Visible = stairsUp.Visible = at;
                turnBack.Visible = !at;
            }
        }

        // 매 프레임 BattleEntry.Update에서: 보일 화면 고르기·단추 상태·이동 입력 전달
        public void Refresh()
        {
            BattleSim sim = entry.Sim;
            BattleCameraRig rig = entry.Rig;
            bool ready = sim != null && rig != null && !sim.Ended;
            layer.Wanted = ready;
            if (!ready || !layer.Shown)
            {
                if (driving && rig != null) { rig.WalkInput = rig.DepthInput = 0f; }
                driving = false;
                return;
            }

            Context next = entry.MapOpen ? Context.Map : (rig.AtTower || (rig.InTransition && rig.TowerTarget)) ? Context.Tower : Context.Wall;
            if (next != context || (next == Context.Tower && rig.AtTower != compass.Visible)) SetContext(next);

            if (context == Context.Wall)
            {
                bool near = rig.OnWall && rig.NearStairs;
                // 이동 패드 → 카메라 (멈춤·지도·튜토리얼 정지 중에는 카메라가 따로 안 움직인다)
                rig.WalkInput = slider.Value.x;
                rig.DepthInput = slider.Value.y;
                driving = true;
                interact.SetGlyph(near ? "stairs_down" : "interact");
                interact.Highlight = near;
                interact.Enabled = !entry.Paused && !entry.Preparing;
                stairsDown.Enabled = near && !entry.Paused;
                wallPause.Highlight = entry.Paused;
                wallSpeed.Highlight = entry.Speed > 1;
            }
            else
            {
                if (driving) { rig.WalkInput = rig.DepthInput = 0f; driving = false; }
                if (context == Context.Tower)
                {
                    beam.Highlight = rig.AtTower;
                    beam.Enabled = rig.AtTower && !entry.Paused;
                    compass.Enabled = rig.AtTower && !entry.Paused;
                    stairsUp.Enabled = !entry.Paused;
                    turnBack.Enabled = !entry.Paused;
                    towerPause.Highlight = entry.Paused;
                }
                else
                {
                    boxMode.Highlight = entry.BoxSelectMode;
                    orderMode.Highlight = !entry.BoxSelectMode;
                    mapPause.Highlight = entry.Paused;
                    spell.Enabled = sim.WizardPresent && sim.SpellCount > 0;
                    spell.Highlight = entry.SelectedSpell >= 0;
                    cannon.Enabled = sim.CannonCount > 0;
                    cannon.Highlight = entry.SelectedCannon >= 0;
                    confirm.Highlight = entry.Preparing || entry.BeamOrderMode;
                }
            }
        }

        public void Dispose()
        {
            if (layer != null) UnityEngine.Object.Destroy(layer.gameObject);
        }
    }
}


