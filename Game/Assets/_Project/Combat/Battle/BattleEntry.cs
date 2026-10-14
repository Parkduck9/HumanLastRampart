using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanLastRampart.Combat.Battle
{
    // 1·2번 전투 화면의 진입 컴포넌트 (C-003, C-005, C-006)
    // CY1-C1: 실제 전투 — BattleSetup의 웨이브가 4면에서 오고, 성벽 위 분대가 자동으로 쏘고, 승패를 판정해 BattleResult를 만든다.
    // [시험] 버튼은 결과 화면 시험용으로 남긴다.
    public sealed class BattleEntry : MonoBehaviour, IScreenEntry
    {
        [Tooltip("적·병사·투사체 그리기 셰이더 (HLR/PerfLabSprite)")]
        [SerializeField] Shader spriteShader;

        IScreenContext context;
        Camera battleCamera;
        BattleSim sim;
        BattleView view;
        BattleCameraRig rig;
        BattleHud hud;
        BattleMapCamera mapCamera;
        bool mapOpen;
        int cannonSide;
        int selectedCannon = -1;
        int selectedSpell = -1;
        Vector3? lens;                 // 망원경 렌즈 위치 (오른쪽 버튼 누르는 동안)
        float nextSpot;
        float nextGroupRefresh;
        bool easy;
        int speed = 1;
        bool paused;
        bool endShown;
        bool finishing;

        public ScreenId Id => ScreenId.Battle;

        void Awake()
        {
            // 화면 씬마다 카메라·AudioListener 하나 (C-003 3-2). 1번 화면은 원근 낮은 카메라 (D-025)
            var cameraObject = new GameObject("Battle Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            battleCamera = cameraObject.GetComponent<Camera>();
            battleCamera.clearFlags = CameraClearFlags.SolidColor;
            battleCamera.backgroundColor = new Color(0.45f, 0.6f, 0.75f);
            battleCamera.fieldOfView = 40f;
        }

        public void OnEnter(IScreenContext screenContext)
        {
            context = screenContext;
            finishing = false;
            endShown = false;
            BattleSetup setup = context.Campaign?.ActiveBattleSetup;
            IDefinitionLookup defs = context.Definitions;

            if (spriteShader == null)
            {
                spriteShader = Shader.Find("HLR/PerfLabSprite");
            }

            string line = setup != null ? Describe(setup) : "전투 준비 정보가 없습니다 (ActiveBattleSetup 없음) — [시험] 버튼만 쓸 수 있습니다";
            if (setup != null && defs != null && spriteShader != null)
            {
                sim = new BattleSim(setup, defs);
                view = new BattleView(sim, spriteShader, transform, HeroSheet(setup, defs));
                rig = new BattleCameraRig(battleCamera, sim.CastleHalf, BattleSim.WallHeight);
                mapCamera = new BattleMapCamera(battleCamera, sim.CastleHalf, sim.FieldSize);
                easy = setup.Difficulty == Difficulty.Easy;
            }
            else if (setup != null && spriteShader == null)
            {
                Debug.LogError("[Battle] HLR/PerfLabSprite 셰이더가 없어 실제 전투를 시작하지 못합니다. [시험] 버튼만 쓸 수 있습니다.");
            }

            hud = new BattleHud(transform, line, FinishFromSim, s => speed = s,
                new System.Action[]
                {
                    () => FinishTest(BattleOutcome.Victory, DefeatReason.None),
                    () => FinishTest(BattleOutcome.Defeat, DefeatReason.WallDestroyed),
                    () => FinishTest(BattleOutcome.Defeat, DefeatReason.GarrisonLost),
                },
                new[] { "[시험] 승리", "[시험] 패배 — 성벽 붕괴", "[시험] 패배 — 병력 70%" });

            if (sim != null)
            {
                // 명령 (CY2-C3): 적용될 때 진행 데이터의 표적 설정에도 기록 (C-011 — 다음 전투에도 유지)
                sim.Notice += hud.PushNotice;
                sim.WallCommandApplied += (side, priority) => Targeting()?.SetWall(WallIds.Of((WallSide)side), priority);
                sim.GlobalCommandApplied += priority => Targeting()?.SetGlobal(priority);
                hud.CommandChosen += OnCommandChosen;
                hud.GlobalCommandChosen += priority => sim?.IssueGlobal(priority);   // MAP-2 지도 전체 명령
                hud.CannonSideChosen += side => { cannonSide = side; selectedCannon = -1; };   // MAP-3 대포 조준
                hud.CannonNumberChosen += SelectCannon;
                hud.SpellChosen += SelectSpell;   // MAP-4 마법
                hud.CannonClearChosen += () => { if (selectedCannon >= 0) sim.ClearCannonAim(selectedCannon); };
            }
        }

        // 주인공 그림 시트: 참가 영웅 중 주인공(UnitKind.Hero)의 UnitDef.Sheet (없으면 BattleView가 임시 그림)
        static Texture2D HeroSheet(BattleSetup setup, IDefinitionLookup defs)
        {
            foreach (HeroState hero in setup.Heroes)
            {
                UnitDef unit = defs.Get<UnitDef>(hero.UnitDefId);
                if (unit != null && unit.Kind == UnitKind.Hero) return unit.Sheet;
            }

            return null;
        }

        // 주인공 그리기 (P3-C02 준비): 카메라가 따라가는 자리에 서고, 서 있으면 뒷모습·A/D 옆모습 걷기·Q/E 옆모습 달리기
        const float HeroWalkFps = 6f;    // 시트 기본 (M-CX-045)
        const float HeroRunFps = 8f;     // 달리기 행 (M-CX-048 시트 JSON runFps)

        void DrawHero()
        {
            if (rig == null || view == null) return;
            int row = rig.Pose == BattleCameraRig.HeroPose.Run ? PerfLab.PerfLabArt.HeroRowSideRun
                : rig.Pose == BattleCameraRig.HeroPose.Walk ? PerfLab.PerfLabArt.HeroRowSideWalk
                : PerfLab.PerfLabArt.HeroRowBack;
            float fps = rig.Pose == BattleCameraRig.HeroPose.Run ? HeroRunFps : HeroWalkFps;
            int column = rig.Pose == BattleCameraRig.HeroPose.Idle ? 0 : (int)(Time.unscaledTime * fps) % PerfLab.PerfLabArt.FramesPerAction;
            bool flip = rig.Pose != BattleCameraRig.HeroPose.Idle && rig.FacingRight;   // 시트 옆모습은 왼쪽을 본다
            if (flip && row == PerfLab.PerfLabArt.HeroRowSideWalk) column = PerfLab.PerfLabArt.HeroWalkFlipPhase[column];
            view.SetHero(rig.HeroFeet, row * PerfLab.PerfLabArt.FramesPerAction + column, flip, !mapOpen);
        }

        TargetingSettings Targeting()
        {
            CampaignState campaign = context?.Campaign;
            if (campaign == null) return null;
            if (campaign.Targeting == null) campaign.Targeting = TargetingSettings.CreateDefault();
            return campaign.Targeting;
        }

        void OnCommandChosen(BattleSim.CommandVia via, TargetPriority priority, int targetSide)
        {
            if (sim == null || rig == null) return;
            switch (via)
            {
                case BattleSim.CommandVia.Direct:
                    sim.IssueDirect(rig.Side, priority);
                    break;
                case BattleSim.CommandVia.Messenger:
                    sim.IssueMessenger(rig.Side, rig.Along, targetSide, priority);
                    break;
                case BattleSim.CommandVia.Shout:
                    sim.IssueShout(rig.Side, targetSide, priority);
                    break;
            }
        }

        public void OnExit()
        {
            sim?.Complete();
            view?.Dispose();
            view = null;
            sim?.Dispose();
            sim = null;
            hud?.Dispose();
            hud = null;
            rig = null;
            context = null;
        }

        void OnDestroy() => OnExit();

        void Update()
        {
            if (sim == null)
            {
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (mouse != null && !sim.Ended && !mapOpen)
            {
                if (mouse.middleButton.wasPressedThisFrame) hud.ToggleMenu(rig != null ? rig.Side : 0);
                if (mouse.rightButton.wasPressedThisFrame) hud.CloseMenu();
            }

            if (keyboard != null && !sim.Ended)
            {
                // 2번 지도 모드 (BattleMap.md): M 열고 닫기, Esc 닫기(명령 메뉴가 열려 있으면 메뉴 먼저)
                if (keyboard.mKey.wasPressedThisFrame) ToggleMap();
                else if (keyboard.escapeKey.wasPressedThisFrame && mapOpen && !hud.MenuOpen) ToggleMap();
            }

            if (keyboard != null)
            {
                if (hud.MenuOpen)
                {
                    // 메뉴가 열려 있으면 숫자 키는 메뉴 고르기 (D-028)
                    Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (keyboard[keys[i]].wasPressedThisFrame) hud.Choose(i);
                    }

                    if (keyboard.escapeKey.wasPressedThisFrame) hud.CloseMenu();
                }
                else
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) speed = 1;
                    if (keyboard.digit2Key.wasPressedThisFrame) speed = 2;
                    if (keyboard.digit3Key.wasPressedThisFrame) speed = 4;
                }

                if (keyboard.pKey.wasPressedThisFrame) paused = !paused;
            }

            if (mapOpen)
            {
                mapCamera.Update(Time.unscaledDeltaTime);
                // 대포를 고른 상태에서 들판(UI 바깥)을 왼쪽 클릭 → 조준점
                // 아무것도 고르지 않았으면 내 함정 표시를 눌러 점화 (D-050)
                if (mouse != null && mouse.leftButton.wasPressedThisFrame
                    && (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
                {
                    Vector3 world = battleCamera.ScreenToWorldPoint(new Vector3(mouse.position.ReadValue().x, mouse.position.ReadValue().y, battleCamera.nearClipPlane));
                    if (selectedSpell >= 0) CastSelectedSpellAt(world);
                    else if (selectedCannon >= 0) AimSelectedCannonAt(world);
                    else DetonateTrapAt(world);
                }

                // 망원경 (MAP-5): 오른쪽 버튼을 누르고 있는 동안 렌즈, 0.15초마다 무리 확인
                lens = null;
                if (mouse != null && mouse.rightButton.isPressed)
                {
                    Vector3 world = battleCamera.ScreenToWorldPoint(new Vector3(mouse.position.ReadValue().x, mouse.position.ReadValue().y, battleCamera.nearClipPlane));
                    UseTelescope(world);
                }
            }
            else rig?.Update(Time.unscaledDeltaTime);

            // 쉬움: 지도가 열린 동안 전투 시간 정지 (확정 B4). 멈춘 동안 내린 명령은 바로 적용된다(작업이 돌지 않음, D-044 M8)
            if (paused || MapFreezesTime)
            {
                return;
            }

            // 큰 프레임 지연에도 한 번에 너무 멀리 가지 않게 자른다
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f) * speed;
            if (rig != null) sim.SetCommander(rig.Side, rig.Along);   // 발견 전령이 찾아갈 지휘관 위치 (D-053)
            sim.BeginFrame(dt);
            if (sim.Ended)
            {
                ShowEnd();
                return;
            }

            sim.Schedule(dt, Time.time);
        }

        void LateUpdate()
        {
            if (sim == null)
            {
                return;
            }

            sim.Complete();
            if (sim.WeatherDirty && !mapOpen)
            {
                view.ApplyAtmosphere(battleCamera, sim);
                sim.WeatherDirty = false;
            }

            DrawHero();
            view.Draw(mapOpen, mapOpen ? mapCamera.DotWorldSize : 0f);
            if (mapOpen)
            {
                view.DrawMapMarkers(cannonSide, selectedCannon, mapCamera.DotWorldSize, selectedSpell >= 0, lens);
                if (Time.unscaledTime >= nextGroupRefresh)
                {
                    nextGroupRefresh = Time.unscaledTime + 0.25f;
                    sim.RefreshSpottedGroups();
                }

                hud?.RefreshGroupLabels(sim, battleCamera);
                hud?.RefreshCannons(sim, cannonSide, selectedCannon);
                hud?.RefreshSpells(sim, selectedSpell);
            }
            hud?.SetMapMode(mapOpen, MapFreezesTime);
            hud?.Refresh(sim, rig, speed, paused || MapFreezesTime);
        }

        // 지도에서 누른 곳 가까이(표시 반경 + 점 크기)에 있는 묻힌 함정 표시 하나를 점화. 없으면 false
        public bool DetonateTrapAt(Vector3 world)
        {
            if (sim == null || !mapOpen) return false;
            float pick = Mathf.Max(4f, mapCamera.DotWorldSize * 3f);
            int best = -1;
            float bestSq = pick * pick;
            for (int i = 0; i < sim.TrapCount; i++)
            {
                BattleSim.TrapInfo t = sim.GetTrap(i);
                if (!t.Marked || t.State != BattleSim.TrapState.Armed) continue;
                float dx = t.Mark.x - world.x, dz = t.Mark.z - world.z;
                if (dx * dx + dz * dz <= bestSq)
                {
                    bestSq = dx * dx + dz * dz;
                    best = i;
                }
            }

            return best >= 0 && sim.DetonateTrap(best);
        }

        public bool MapOpen => mapOpen;
        public BattleMapCamera MapCamera => mapCamera;
        bool MapFreezesTime => mapOpen && easy;

        // 마법 고르기 → 들판 클릭으로 쓰기 (대포 선택은 풀린다). 같은 마법을 다시 누르면 취소
        public void SelectSpell(int index)
        {
            selectedCannon = -1;
            selectedSpell = selectedSpell == index ? -1 : index;
        }

        public void UseTelescope(Vector3 world)
        {
            lens = new Vector3(world.x, 0f, world.z);
            if (Time.unscaledTime < nextSpot) return;
            nextSpot = Time.unscaledTime + 0.15f;
            sim.LookThroughTelescope(world);
        }

        public bool CastSelectedSpellAt(Vector3 world)
        {
            if (selectedSpell < 0) return false;
            bool cast = sim.CastSpell(selectedSpell, world);
            if (cast) selectedSpell = -1;
            return cast;
        }

        // 대포 고르기: 지금 고른 벽의 1~4번
        public void SelectCannon(int number)
        {
            selectedSpell = -1;
            selectedCannon = -1;
            for (int c = 0; c < sim.CannonCount; c++)
            {
                BattleSim.CannonInfo info = sim.GetCannon(c);
                if (info.Side == cannonSide && info.Number == number) selectedCannon = c;
            }
        }

        public void SelectCannonSide(int side)
        {
            cannonSide = side;
            selectedCannon = -1;
        }

        public bool AimSelectedCannonAt(Vector3 world) => selectedCannon >= 0 && sim.SetCannonAim(selectedCannon, world);

        public void ToggleMap()
        {
            if (sim == null || mapCamera == null) return;
            mapOpen = !mapOpen;
            if (mapOpen)
            {
                hud.CloseMenu();
                mapCamera.Open(rig != null ? rig.Side : 0);
                cannonSide = rig != null ? rig.Side : 0;
                selectedCannon = -1;
                selectedSpell = -1;
                RenderSettings.fog = false;   // 위에서 멀리 보는 지도에는 안개를 끈다 (시야 규칙은 점 그리기에서 그대로)
            }
            else
            {
                battleCamera.orthographic = false;
                battleCamera.fieldOfView = 40f;
                battleCamera.nearClipPlane = BattleCameraRig.NearClip;
                battleCamera.farClipPlane = 1200f;
                rig?.SetPreset(rig.PresetIndex);
                sim.WeatherDirty = true;      // 안개·빛 다시 맞춤
            }
        }

        void ShowEnd()
        {
            if (endShown)
            {
                return;
            }

            endShown = true;
            string message;
            if (sim.Outcome == BattleOutcome.Victory)
            {
                message = $"승리!\n<size=28>처치 {sim.Kills:N0} · 얻은 골드 {sim.Gold:N0}</size>";
            }
            else
            {
                string wall = sim.DefeatSide >= 0 ? new[] { "북쪽", "동쪽", "남쪽", "서쪽" }[sim.DefeatSide] : "";
                string reason = sim.DefeatReason == DefeatReason.WallDestroyed ? $"{wall} 성벽이 무너졌습니다" : $"{wall} 성벽 병력을 70% 넘게 잃었습니다";
                message = $"패배\n<size=28>{reason}</size>";
                if (sim.DefeatSide >= 0)
                {
                    rig?.ShowSide(sim.DefeatSide);
                }
            }

            hud.ShowEnd(message);
        }

        // 실제 전투 결과로 끝내기 (C-006: Combat이 LastBattle을 쓴다)
        void FinishFromSim()
        {
            if (context == null || sim == null || !sim.Ended || finishing)
            {
                return;
            }

            finishing = true;
            sim.Complete();
            context.Campaign.LastBattle = sim.BuildResult();
            context.Complete(ScreenOutcome.Done);
        }

        // 개발용 시험 결과: 첫 분대 1명 손실·북쪽 성벽 50 피해 (결과 화면 C-007 시험용, M-CX-022)
        void FinishTest(BattleOutcome outcome, DefeatReason reason)
        {
            if (context == null || finishing || context.Campaign?.ActiveBattleSetup == null)
            {
                return;
            }

            finishing = true;
            sim?.Complete();
            CampaignState campaign = context.Campaign;
            BattleSetup setup = campaign.ActiveBattleSetup;
            var result = new BattleResult
            {
                BattleRunId = setup.BattleRunId,
                BattleNumber = setup.BattleNumber,
                Outcome = outcome,
                DefeatedEnemyCount = 120,
                DefeatedSpecialCount = 1,
                EarnedGold = outcome == BattleOutcome.Victory ? 60 : 0,
                DefeatReason = outcome == BattleOutcome.Defeat ? reason : DefeatReason.None,
                DefeatWallId = outcome == BattleOutcome.Defeat ? WallIds.North : null,
            };

            BattleResultHelper.AddUnchangedHeroes(setup, result);
            if (outcome == BattleOutcome.Victory)
            {
                SquadState squad = setup.Squads.Find(s => s.Count > 0);
                if (squad != null)
                {
                    result.SquadLosses.Add(new SquadLoss { SquadId = squad.SquadId, LostCount = 1 });
                }

                WallSegmentState north = setup.Wall.Find(w => w.SegmentId == WallIds.North);
                if (north != null)
                {
                    result.WallOutcomes.Add(new WallOutcome { SegmentId = north.SegmentId, HpAfter = Mathf.Max(0, north.Hp - 50) });
                }
            }

            campaign.LastBattle = result;
            context.Complete(ScreenOutcome.Done);
        }

        static string Describe(BattleSetup setup)
        {
            // 날씨가 언제 바뀌는지는 보여 주지 않는다 (전투 중 무작위 변화, Battle.md 6장)
            string difficulty = setup.Difficulty == Difficulty.Easy ? "쉬움" : "현실";
            return $"전투 {setup.BattleNumber} · {setup.TerrainId} · {difficulty} (적 ×{setup.EnemyCountMultiplier:0.##})";
        }
    }
}
