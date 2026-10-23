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
        BattleBeamView beamView;   // 등대 빛줄기 임시 그림
        readonly BattleAudioDirector audioDirector = new BattleAudioDirector();   // 전투 소리 (W24, 표현 전용)
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
        bool preparing;   // 전투 준비 (D-071) — 시작 전 지도
        int selectedGroup = -1;   // 지도에서 고른 병력 묶음 (D-072)
        bool dragging;
        Vector2 dragStart;
        bool panPending, panMoved;   // 빈 지도 끌기 (D-073)
        Vector2 panLast;
        Vector3 panPress;
        bool boxSelecting, amountWheel;
        Vector2 boxStart, wheelScreen;
        int sourceWall = -1, wallMoveCount;
        readonly System.Collections.Generic.List<int> selectedGroups = new System.Collections.Generic.List<int>();

        public ScreenId Id => ScreenId.Battle;

        // 튜토리얼(W2)이 읽는 공개 상태 — BattleTutorialSource. TutorialTimeScale: 0 = 전투 시간 멈춤, 1 = 보통
        public BattleSim Sim => sim;
        public BattleCameraRig Rig => rig;
        public Camera BattleCamera => battleCamera;
        public bool TelescopeActive => lens.HasValue;
        public float TutorialTimeScale { get; set; } = 1f;
        public int SquadOrders { get; private set; }   // 분대 이동 명령을 실제로 낸 횟수 (우선순위·대포 전령은 세지 않는다)
        public bool IsPracticeSetup => context?.Campaign?.ActiveBattleSetup != null && context.Campaign.ActiveBattleSetup.IsPractice;

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
            preparing = false;
            mapOpen = false;
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
                UnitDef heroUnit = HeroUnit(setup, defs);
                view = new BattleView(sim, spriteShader, transform, HeroSheet(setup, defs), heroUnit?.IdleSheet,
                    heroUnit?.NormalSheet, heroUnit?.IdleNormalSheet);   // 노멀맵 짝 (D-062 G4) — 없으면 null
                view.SetHeroCommandArt(heroUnit?.CommandSheet, heroUnit?.NormalFor(heroUnit.CommandSheet));
                rig = new BattleCameraRig(battleCamera, sim.CastleHalf, BattleSim.WallHeight);
                mapCamera = new BattleMapCamera(battleCamera, sim.CastleHalf, sim.FieldSize);
                easy = setup.Difficulty == Difficulty.Easy;
                beamView = new BattleBeamView(sim);
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
            if (sim == null) hud.ToggleDev();   // 실제 전투를 못 만든 경우: 시험 단추만 쓸 수 있으니 처음부터 보인다

            if (sim != null)
            {
                // 명령 (CY2-C3): 적용될 때 진행 데이터의 표적 설정에도 기록 (C-011 — 다음 전투에도 유지)
                sim.Notice += hud.PushNotice;
                sim.SquadOrderStarted += () => SquadOrders++;
                sim.WallCommandApplied += (side, priority) => Targeting()?.SetWall(WallIds.Of((WallSide)side), priority);
                sim.GlobalCommandApplied += priority => Targeting()?.SetGlobal(priority);
                hud.CommandChosen += OnCommandChosen;
                hud.GlobalCommandChosen += priority => sim?.IssueGlobal(priority);   // MAP-2 지도 전체 명령
                hud.CannonPicked += PickCannon;   // MAP-3 대포 조준 (K-037: 지도 위 대포 표시를 바로 누른다)
                hud.SpellChosen += SelectSpell;   // MAP-4 마법
                hud.CannonClearChosen += () => { if (selectedCannon >= 0) sim.ClearCannonAim(selectedCannon); };
                hud.WallCommandChosen += OnMapWallCommand;
                hud.BattleStartChosen += StartBattle;
                hud.LampChosen += b => sim?.SetLamp(b);     // 등불 밝기 (빛줄기 폭·효과만 바뀜)
                hud.BeamChosen += OnBeamChosen;           // 빛줄기 방향 (탑 명령 판에서만, 일꾼이 BeamRotateSeconds에 걸쳐 돌림)
                hud.TowerLeaveChosen += LeaveTower;
                hud.TowerGoChosen += side => TowerUp(side);   // 탑 앞 북·서·동·남 단추 (W35)
                hud.BeamOrderClosed += CloseBeamOrder;
                hud.GroupDestinationChosen += side => OrderGroupMove(selectedGroup, side);
                hud.WallGroupsDestinationChosen += side => OrderWallMove(selectedGroup, side);

                // 전투 준비 (D-071): 지도가 열린 채 시간이 멈춘다. 명령·대포 조준을 정하고 [전투 시작] (지도 닫기도 시작)
                preparing = true;
                ToggleMap();
                mapCamera.OpenCastle();
                hud.SetPreparing(true);
                if (!setup.IsPractice) BattleTutorialSource.TryBegin(this, transform, setup.BattleNumber);   // 1번째 전투 튜토리얼 (W2). 대상이 아니면 아무것도 안 한다
            }
        }

        // D-072 병력 묶음 이동: 준비 중이면 바로 그 자리에, 전투 중 주인공 성벽 묶음은 바로 출발, 다른 성벽 묶음은 전령이 닿아야 출발
        public void OrderGroupMove(int group, int targetSide)
        {
            if (sim == null || group < 0 || group >= sim.GroupCount) return;
            BattleSim.GroupInfo info = sim.GetGroup(group);
            if (info.Side < 0 || info.Side == targetSide) return;
            if (preparing || CommanderAt(info.Side))
            {
                sim.IssueGroupMove(group, targetSide, preparing);
                SquadOrders++;
            }
            else sim.IssueGroupMoveByMessenger(MessengerFrom, rig.Along, group, targetSide);   // 실제로 출발하면 SquadOrderStarted가 센다 (W9)
            selectedGroup = -1;
        }

        // K-060 대포 옮기기: 다른 성벽의 빈 사격구로. 준비 중이면 바로, 전투 중 주인공 성벽 대포는 바로 출발, 다른 성벽 대포는 전령이 닿아야 출발
        bool cannonMoveArmed;

        public void OrderCannonMove(int cannon, int targetSide)
        {
            if (sim == null || cannon < 0 || cannon >= sim.CannonCount) return;
            int side = sim.GetCannon(cannon).Side;
            if (side == targetSide) return;
            if (preparing) sim.IssueCannonMove(cannon, targetSide, true);
            else if (CommanderAt(side)) sim.IssueCannonMove(cannon, targetSide, false);
            else sim.IssueCannonMoveByMessenger(MessengerFrom, rig.Along, cannon, targetSide);
            selectedCannon = -1;
        }

        // 한 번에 옮기기 (사용자 2차 피드백 11): 고른 묶음이 있는 성벽의 묶음 전부를, 20명 남기고. 전령 규칙은 묶음 이동과 같다
        public void OrderWallMove(int group, int targetSide)
        {
            if (sim == null || group < 0 || group >= sim.GroupCount) return;
            int wall = sim.GetGroup(group).Side;
            OrderWallMoveCount(wall, targetSide, int.MaxValue);
        }

        public void OrderWallMoveCount(int wall, int targetSide, int maxGroups)
        {
            if (sim == null || maxGroups <= 0 || wall > 3 || targetSide < 0 || targetSide > 3) return;
            if (wall < 0 || wall == targetSide) return;
            if (preparing || CommanderAt(wall))
            {
                sim.IssueWallMove(wall, targetSide, preparing, maxGroups);
                SquadOrders++;
            }
            else sim.IssueWallMoveByMessenger(MessengerFrom, rig.Along, wall, targetSide, maxGroups);
            selectedGroup = -1;
        }

        // 성벽 통로 띠만 누를 수 있다. 마당·들판은 지도 끌기와 함정 조작에 남긴다
        public static int WallAtPoint(Vector3 world, float castleHalf)
        {
            float reach = Mathf.Max(Mathf.Abs(world.x), Mathf.Abs(world.z));
            return reach >= castleHalf - 2f && reach <= castleHalf ? SideOfPoint(world) : -1;
        }

        public int SelectGroupsInScreenRect(Rect rect)
        {
            selectedGroups.Clear();
            selectedGroup = -1;
            hud.RefreshGroupMarkers(sim, battleCamera, -1, null);
            selectedGroups.AddRange(hud.GroupsInScreenRect(sim, rect));
            hud.SetBoxGroups(selectedGroups);
            return selectedGroups.Count;
        }

        public void SendSelectedGroups(int side)
        {
            foreach (int group in selectedGroups) OrderGroupMove(group, side);
            CancelSquadSelection();
        }

        void CancelSquadSelection()
        {
            boxSelecting = false;
            selectedGroups.Clear();
            sourceWall = -1;
            hud?.HideSelectionBox();
            hud?.CloseSquadWheel();
            hud?.SetBoxGroups(null);
        }

        int SelectedSoldiers()
        {
            int count = 0;
            foreach (int group in selectedGroups) count += sim.GetGroup(group).Alive;
            return count;
        }

        bool ChooseSquadWheel(Vector2 point)
        {
            int choice = hud.PickSquadWheel(point);
            if (choice < 0) return false;
            if (amountWheel)
            {
                wallMoveCount = sim.WallMoveGroupCount(sourceWall, choice);
                amountWheel = false;
                hud.ShowSquadWheel(wheelScreen, false, WallMoveSoldiers(sourceWall, wallMoveCount), sourceWall);
            }
            else if (sourceWall >= 0)
            {
                OrderWallMoveCount(sourceWall, choice, wallMoveCount);
                CancelSquadSelection();
            }
            else SendSelectedGroups(choice);
            return true;
        }

        int WallMoveSoldiers(int wall, int maxGroups)
        {
            int total = 0, count = 0;
            for (int g = 0; g < sim.GroupCount; g++)
            {
                BattleSim.GroupInfo info = sim.GetGroup(g);
                if (info.Side != wall || !info.Movable || info.Moving || info.Alive <= 0) continue;
                total += info.Alive;
                count++;
            }
            return Mathf.Min(Mathf.Max(0, total - BattleSim.KeepPerWall), Mathf.Min(count, maxGroups) * BattleSim.GroupSize);
        }

        // 지도 위 한 점에서 가장 가까운 성벽 (성 가운데 기준으로 더 많이 치우친 축)
        static int SideOfPoint(Vector3 world)
        {
            if (Mathf.Abs(world.x) > Mathf.Abs(world.z)) return world.x > 0f ? 1 : 3;
            return world.z > 0f ? 0 : 2;
        }

        // 지도에서 성벽 하나에 내린 명령: 준비 중이면 바로, 전투 중이면 주인공이 있는 성벽만 바로·나머지는 전령 (D-071)
        void OnMapWallCommand(int side, TargetPriority priority)
        {
            if (sim == null) return;
            if (preparing || CommanderAt(side)) sim.IssueDirect(side, priority);
            else sim.IssueMessenger(MessengerFrom, rig.Along, side, priority);
        }

        public bool Preparing => preparing;

        // 주인공이 그 성벽을 지키고 있는가 (W9: 계단으로 내려가 등대 탑에 있으면 어느 성벽에도 없다 → 명령은 전령이 전한다)
        bool CommanderAt(int side) => rig == null || (rig.OnWall && rig.Side == side);
        // 전령이 떠나는 곳: 성벽 위면 주인공 곁, 탑에 있으면 등대 탑 (어느 성벽이든 탑 전령이 닿아야 한다 — 북쪽도)
        int MessengerFrom => rig != null && rig.OnWall ? rig.Side : BattleSim.TowerOrigin;

        // ── 등대 탑 (W9, Concept_Beacon_v1 9.7 4차 결정) ──
        // 네 성벽 어디서든 통로 가운데 안쪽 끝(계단)에서 E → 마당 가운데 탑으로 내려간다 (W35). 탑에서 E → 빛줄기 명령 (지도가 떠서 방향 단추 + 지금 방향).
        // 방향을 고르면 일꾼이 돌리기 시작하고, 지도는 BeamMapStaySeconds 뒤 저절로 접혀 일꾼 모습을 보게 한다 (쉬움은 지도가 열린 동안 시간이 멈추므로).
        // 탑에서 W 북 · A 서 · D 동 · S 남 → 그 성벽 가운데로 올라간다. [성벽으로 올라가기] 단추는 내려온 성벽으로. 탑에 있는 동안은 OnWall = false (지휘 휘장·같은 성벽 발견 알림·직접 명령이 없다)
        public float BeamMapStaySeconds = 0.8f;
        bool beamOrderMode;
        float beamMapCloseAt = -1f;

        public bool BeamOrderMode => beamOrderMode;
        public bool AtTower => rig != null && rig.AtTower;

        // E 키가 하는 일. 일어난 일이 있으면 true
        // 탑 오르내리기에 쓰는 전투 시간: 멈춤·지도 시간 멈춤·튜토리얼 정지면 0, 아니면 배속 반영 (S1)
        float TowerDelta() => paused || MapFreezesTime || TutorialTimeScale <= 0f ? 0f : Mathf.Min(Time.deltaTime, 1f / 20f) * speed * TutorialTimeScale;

        public bool Interact()
        {
            if (sim == null || rig == null || sim.Ended || preparing) return false;
            if (rig.InTransition) return TurnBack();   // W35: 내려가는 중 E = 되돌아가기
            if (beamOrderMode) { CloseBeamOrder(); return true; }
            if (mapOpen || paused || TutorialTimeScale <= 0f) return false;   // 멈춘 동안은 계단·탑을 쓰지 않는다
            if (rig.AtTower) { OpenBeamOrder(); return true; }
            return rig.OnWall && rig.GoDown();
        }

        public void OpenBeamOrder()
        {
            if (sim == null || rig == null || !rig.AtTower || beamOrderMode) return;
            if (!mapOpen) ToggleMap();
            mapCamera.SetLevel(BattleMapCamera.Level.Full);   // 들판 전체 — 지금 비추는 부채꼴이 한눈에 보인다
            beamOrderMode = true;
            beamMapCloseAt = -1f;
            hud.SetBeamOrder(true);
        }

        public void CloseBeamOrder()
        {
            if (!beamOrderMode) return;
            beamOrderMode = false;
            beamMapCloseAt = -1f;
            hud?.SetBeamOrder(false);
            if (mapOpen) ToggleMap();
        }

        // 내려온 성벽으로 올라가기 (명령 판 단추·시험)
        public void LeaveTower()
        {
            CloseBeamOrder();
            rig?.GoUp();
        }

        // W35: 계단·길을 내려가는 중(아직 탑 앞이 아님)에 E 또는 내려오던 성벽 키 → 그 자리에서 돌아 그 성벽으로 올라간다
        public bool TurnBack()
        {
            if (sim == null || rig == null || sim.Ended || preparing || mapOpen || paused || TutorialTimeScale <= 0f) return false;
            if (!rig.TowerTarget || !rig.InTransition) return false;
            return rig.GoUp(rig.PathSide);
        }

        // W35: 탑 앞에서 W 북(0) · D 동(1) · S 남(2) · A 서(3) 성벽 가운데로 올라가기. 탑 앞에 도착해 있을 때만 (지도·명령 판이 열려 있으면 먼저 닫는다)
        public bool TowerUp(int side)
        {
            if (sim == null || rig == null || sim.Ended || preparing || !rig.AtTower) return false;
            if (paused || TutorialTimeScale <= 0f) return false;
            CloseBeamOrder();
            return rig.GoUp(side);
        }

        void OnBeamChosen(int side)
        {
            if (sim == null || !beamOrderMode) return;
            sim.RotateBeam(side);   // 일꾼이 BeamRotateSeconds에 걸쳐 돌린다
            beamMapCloseAt = Time.unscaledTime + BeamMapStaySeconds;
        }

        void UpdateTowerPrompt()
        {
            if (hud == null || rig == null) return;
            int buttons = 0;
            if (mapOpen || sim == null || sim.Ended || preparing) hud.SetTowerPrompt(null, null);
            else if (rig.InTransition && rig.TowerTarget) { hud.SetTowerPrompt("E", "되돌아가기"); buttons = 2; }   // 내려가는 중 (W35)
            else if (rig.InTransition) hud.SetTowerPrompt(null, null);
            else if (rig.AtTower) { hud.SetTowerPrompt("E", "빛줄기 명령", "WASD", "성벽으로"); buttons = 1; }
            else if (rig.OnWall && rig.NearStairs) hud.SetTowerPrompt("E", "계단으로 내려가 등대로");
            else hud.SetTowerPrompt(null, null);
            hud.SetTowerButtons(buttons);
        }

        public void StartBattle()
        {
            if (!preparing) return;
            preparing = false;
            hud?.SetPreparing(false);
            if (mapOpen) ToggleMap();
        }

        // 주인공 그림 시트: 참가 영웅 중 주인공(UnitKind.Hero)의 UnitDef.Sheet (없으면 BattleView가 임시 그림)
        static UnitDef HeroUnit(BattleSetup setup, IDefinitionLookup defs)
        {
            foreach (HeroState hero in setup.Heroes)
            {
                UnitDef unit = defs.Get<UnitDef>(hero.UnitDefId);
                if (unit != null && unit.Kind == UnitKind.Hero) return unit;
            }

            return null;
        }

        static Texture2D HeroSheet(BattleSetup setup, IDefinitionLookup defs)
        {
            foreach (HeroState hero in setup.Heroes)
            {
                UnitDef unit = defs.Get<UnitDef>(hero.UnitDefId);
                if (unit != null && unit.Kind == UnitKind.Hero) return unit.Sheet;
            }

            return null;
        }

        // 주인공 그리기 (P3-C02 준비): 카메라가 따라가는 자리에 서고, 서 있으면 뒷모습·A/D 옆모습 달리기
        const float HeroWalkFps = 6f;    // 시트 기본 (M-CX-045)
        const float HeroRunFps = 8f;     // 달리기 행 (M-CX-048 시트 JSON runFps)
        const float HeroCommandFps = 6f; // 명령 손짓 3칸 (Codex K-023 검토 속도), 마지막 칸 유지
        const int HeroCommandFrames = 3;
        float heroCommandSince = -1f;    // 휠 메뉴를 연 시각 (닫혀 있으면 -1)

        void DrawHero()
        {
            if (rig == null || view == null) return;
            // 휠 메뉴를 여는 동안 명령 손짓 (K-032). 시트가 없으면 BattleView가 무시한다
            bool menuOpen = hud != null && hud.MenuOpen && view.HasHeroCommandArt;
            if (!menuOpen) heroCommandSince = -1f;
            else if (heroCommandSince < 0f) heroCommandSince = Time.unscaledTime;
            int command = menuOpen ? Mathf.Min((int)((Time.unscaledTime - heroCommandSince) * HeroCommandFps), HeroCommandFrames - 1) : -1;
            // D-061: 옆으로는 늘 달리기, 통로 안쪽·바깥쪽은 앞모습·뒷모습 걷기. 서 있기는 서 있기 1칸 그림(D-066)
            int row = rig.Pose == BattleCameraRig.HeroPose.Run ? PerfLab.PerfLabArt.HeroRowSideRun
                : rig.Pose == BattleCameraRig.HeroPose.Walk ? (rig.FacingFront ? PerfLab.PerfLabArt.HeroRowFront : PerfLab.PerfLabArt.HeroRowBack)
                : PerfLab.PerfLabArt.HeroRowBack;
            float fps = rig.Pose == BattleCameraRig.HeroPose.Run ? HeroRunFps : HeroWalkFps;
            int column = rig.Pose == BattleCameraRig.HeroPose.Idle ? 0 : (int)(Time.unscaledTime * fps) % PerfLab.PerfLabArt.FramesPerAction;
            bool flip = rig.Pose == BattleCameraRig.HeroPose.Run && rig.FacingRight;   // 시트 옆모습은 왼쪽을 본다
            // 마당 계단·층계참 위에서는 그림만 그 높이를 따라 오르내린다 (W19, 계산 위치는 그대로)
            view.SetHero(HeroDrawFeet(), row * PerfLab.PerfLabArt.FramesPerAction + column, flip, !mapOpen, rig.Pose == BattleCameraRig.HeroPose.Idle, command);
        }

        // 그린 발 위치: 계산 위치 + 마당 계단 높이 (메뉴·안내가 그림을 따라가게)
        Vector3 HeroDrawFeet()
        {
            Vector3 feet = rig.HeroFeet;
            feet.y += BattleCourtyard.HeroLift(feet, sim.CastleHalf);
            return feet;
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
            beamView?.Dispose();
            beamView = null;
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
            bool cancelSquad = mapOpen && (boxSelecting || hud.SquadWheelOpen)
                && ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame));
            if (cancelSquad) CancelSquadSelection();
            if (mouse != null && !sim.Ended && !mapOpen)
            {
                if (mouse.middleButton.wasPressedThisFrame)
                {
                    if (rig == null || rig.OnWall) hud.ToggleMenu(rig != null ? rig.Side : 0);
                    else hud.PushNotice("성벽을 비운 동안에는 명령 메뉴를 쓸 수 없습니다 (지도에서 전령으로 명령)");
                }
                if (mouse.rightButton.wasPressedThisFrame) hud.CloseMenu();
            }

            if (keyboard != null && !sim.Ended)
            {
                // 2번 지도 모드 (BattleMap.md): Tab·M 열고 닫기, Esc 닫기(명령 메뉴가 열려 있으면 메뉴 먼저). D-071: Tab = 지도
                // 전투 준비 중에 지도를 닫으면 전투가 시작된다
                bool toggle = keyboard.tabKey.wasPressedThisFrame || keyboard.mKey.wasPressedThisFrame
                    || (keyboard.escapeKey.wasPressedThisFrame && mapOpen && !hud.MenuOpen && !cancelSquad);
                if (toggle)
                {
                    if (preparing) StartBattle();
                    else ToggleMap();
                }

                // 등대 탑 (W9·W35): E 계단 내려가기·탑 명령, 탑 앞에서 W 북 · A 서 · S 남 · D 동 성벽으로 올라가기 (W·A·S·D는 지도에서 지도 이동·마법 키라 지도가 닫혀 있을 때만)
                if (!toggle && !hud.MenuOpen && !cancelSquad)
                {
                    if (keyboard.eKey.wasPressedThisFrame) Interact();
                    if (!mapOpen && rig != null && (rig.AtTower || (rig.InTransition && rig.TowerTarget)))
                    {
                        // 방향 키(W·A·S·D와 화살표): 0 북 · 1 동 · 2 남 · 3 서. 내려가는 중에는 내려오던 성벽의 키가 되돌아가기
                        int dir = -1;
                        if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) dir = 0;
                        else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) dir = 1;
                        else if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) dir = 2;
                        else if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) dir = 3;
                        if (dir >= 0)
                        {
                            if (rig.AtTower) TowerUp(dir);
                            else if (dir == rig.PathSide) TurnBack();
                        }
                    }
                }
            }

            if (beamOrderMode && beamMapCloseAt > 0f && Time.unscaledTime >= beamMapCloseAt) CloseBeamOrder();

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

                // 마법 빠른 쓰기 (D-073): 지도에서 Q = 첫째 마법, W = 둘째 마법 고르기 → 왼쪽 클릭으로 그 자리에. 다시 누르면 취소
                if (mapOpen && !hud.MenuOpen)
                {
                    if (keyboard.qKey.wasPressedThisFrame && sim.SpellCount > 0) SelectSpell(0);
                    if (keyboard.wKey.wasPressedThisFrame && sim.SpellCount > 1) SelectSpell(1);
                    // 대포 옮기기 (K-060): 대포를 고른 상태에서 R → 보낼 성벽 쪽 들판을 누른다. 다시 R = 취소 (M은 지도 닫기. 화면 단추는 K-047에서)
                    if (keyboard.rKey.wasPressedThisFrame && selectedCannon >= 0)
                    {
                        cannonMoveArmed = !cannonMoveArmed;
                        if (cannonMoveArmed) sim.NoticeCannonMoveArmed(selectedCannon);
                    }
                }

                if (keyboard.pKey.wasPressedThisFrame) paused = !paused;
                if (keyboard.hKey.wasPressedThisFrame) hud.ToggleStatusDetail();   // 4면 자세히 (K-002)
                if (keyboard.f1Key.wasPressedThisFrame) hud.ToggleHelp();          // 조작 판 (K-047 S-15: 키 목록은 여기에만)
                if (keyboard.f12Key.wasPressedThisFrame) hud.ToggleDev();          // 개발 표시 (K-047 S-16: [시험] 단추·내부 정보)
                if (keyboard.gKey.wasPressedThisFrame) HumanLastRampart.Combat.Render.BattleLook.Enabled = !HumanLastRampart.Combat.Render.BattleLook.Enabled;   // 화면 효과 켜고 끔 (비교용)
            }

            if (mapOpen)
            {
                rig?.TickTower(TowerDelta());   // 지도가 열려 있어도 탑 오르내리기는 전투 시간대로 이어진다
                mapCamera.Update(Time.unscaledDeltaTime);
                // 대포를 고른 상태에서 들판(UI 바깥)을 왼쪽 클릭 → 조준점
                // 아무것도 고르지 않았으면 내 함정 표시를 눌러 점화 (D-050)
                bool overUi = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
                bool shiftHeld = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
                bool pressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
                // 둥근 표식 바깥을 누르면 표식을 닫고 그 누름을 평소처럼 처리한다 (닫히지 않아 다음 상자를 못 그리던 문제)
                if (pressed && hud.SquadWheelOpen)
                {
                    if (ChooseSquadWheel(mouse.position.ReadValue())) pressed = false;
                    else { CancelSquadSelection(); overUi = false; }
                }
                // 시프트를 누른 채면 UI 판 위에서 시작해도 상자를 그린다
                if (pressed && (!overUi || shiftHeld) && !cancelSquad)
                {
                    Vector2 point = mouse.position.ReadValue();
                    Vector3 world = battleCamera.ScreenToWorldPoint(new Vector3(point.x, point.y, battleCamera.nearClipPlane));
                    // D-072: 병력 묶음 표시를 누르면 고르고 끌기 시작 (마법·대포보다 먼저)
                    int group = hud.GroupAtScreen(point);
                    if (shiftHeld)
                    {
                        CancelSquadSelection();
                        selectedGroup = selectedSpell = selectedCannon = -1;
                        panPending = dragging = false;
                        boxStart = point;
                        boxSelecting = true;
                        hud.ShowSelectionBox(point, point);
                    }
                    else if (group >= 0)
                    {
                        selectedGroup = group;
                        selectedSpell = -1;
                        selectedCannon = -1;
                        dragStart = point;
                        dragging = true;
                    }
                    else if (selectedSpell >= 0) CastSelectedSpellAt(world);
                    else if (selectedCannon >= 0 && cannonMoveArmed) { OrderCannonMove(selectedCannon, SideOfPoint(world)); cannonMoveArmed = false; }
                    else if (selectedCannon >= 0) AimSelectedCannonAt(world);
                    else
                    {
                        // 아무것도 고르지 않았으면: 끌면 지도 옮기기(D-073), 그냥 떼면 함정 점화·묶음 선택 해제
                        panPending = true;
                        panMoved = false;
                        panLast = point;
                        panPress = world;
                    }
                }

                if (boxSelecting && mouse != null)
                {
                    Vector2 point = mouse.position.ReadValue();
                    hud.ShowSelectionBox(boxStart, point);
                    if (!mouse.leftButton.isPressed)
                    {
                        boxSelecting = false;
                        hud.HideSelectionBox();
                        Vector2 lo = Vector2.Min(boxStart, point), hi = Vector2.Max(boxStart, point);
                        if (SelectGroupsInScreenRect(Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y)) > 0)
                        {
                            amountWheel = false;
                            sourceWall = -1;
                            hud.ShowSquadWheel(point, false, SelectedSoldiers());
                        }
                        else hud.PushNotice("상자 안에 보낼 수 있는 분대가 없습니다 (이동 중인 분대는 고를 수 없음)");
                    }
                }

                if (panPending && mouse != null)
                {
                    Vector2 point = mouse.position.ReadValue();
                    if (mouse.leftButton.isPressed)
                    {
                        // 누른 자리에서 6화소 넘게 움직이면 그때부터 끌기 (손떨림으로 함정 점화가 끌기로 바뀌지 않게)
                        if (!panMoved && (point - panLast).sqrMagnitude > 36f)
                        {
                            panMoved = true;
                            // 누른 뒤에 시프트를 눌렀어도 끌기 시작 순간 시프트가 눌려 있으면 상자 고르기로 바꾼다
                            if (shiftHeld)
                            {
                                panPending = false;
                                boxStart = panLast;
                                boxSelecting = true;
                                hud.ShowSelectionBox(boxStart, point);
                            }
                        }
                        if (panMoved && panPending)
                        {
                            mapCamera.DragBy(point - panLast);
                            panLast = point;
                        }
                    }
                    else
                    {
                        panPending = false;
                        if (!panMoved)
                        {
                            int wall = WallAtPoint(panPress, sim.CastleHalf);
                            selectedGroup = -1;
                            if (wall >= 0)
                            {
                                CancelSquadSelection();
                                sourceWall = wall;
                                amountWheel = true;
                                wheelScreen = point;
                                hud.ShowSquadWheel(point, true, WallMoveSoldiers(wall, int.MaxValue));
                            }
                            else DetonateTrapAt(panPress);
                        }
                    }
                }

                // 끌어다 놓기: 28화소 넘게 움직였으면 놓은 곳에서 가까운 성벽으로 보낸다. 조금만 움직였으면 고른 채로(창에서 성벽 고르기)
                if (dragging && mouse != null && !mouse.leftButton.isPressed)
                {
                    dragging = false;
                    Vector2 point = mouse.position.ReadValue();
                    if ((point - dragStart).sqrMagnitude > 28f * 28f && selectedGroup >= 0)
                    {
                        Vector3 world = battleCamera.ScreenToWorldPoint(new Vector3(point.x, point.y, battleCamera.nearClipPlane));
                        OrderGroupMove(selectedGroup, SideOfPoint(world));
                    }
                }

                // 망원경 (MAP-5): 오른쪽 버튼을 누르고 있는 동안 렌즈, 0.15초마다 무리 확인
                lens = null;
                if (mouse != null && mouse.rightButton.isPressed && !cancelSquad && !hud.SquadWheelOpen && !boxSelecting)
                {
                    Vector3 world = battleCamera.ScreenToWorldPoint(new Vector3(mouse.position.ReadValue().x, mouse.position.ReadValue().y, battleCamera.nearClipPlane));
                    UseTelescope(world);
                }
            }
            else rig?.Update(Time.unscaledDeltaTime, TowerDelta());

            // 쉬움: 지도가 열린 동안 전투 시간 정지 (확정 B4). 멈춘 동안 내린 명령은 바로 적용된다(작업이 돌지 않음, D-044 M8)
            if (paused || MapFreezesTime || TutorialTimeScale <= 0f)
            {
                return;
            }

            // 큰 프레임 지연에도 한 번에 너무 멀리 가지 않게 자른다
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f) * speed * TutorialTimeScale;
            if (rig != null) sim.SetCommander(rig.Side, rig.Along, rig.OnWall);   // 발견 전령이 찾아갈 지휘관 위치 (D-053). 탑에 있으면 성벽에 없음 (W9)
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
            UpdateTowerPrompt();
            // 명령 메뉴는 주인공 가슴 높이 오른쪽에 붙는다 (D-072)
            if (hud.MenuOpen && rig != null && !mapOpen)
            {
                Vector3 screen = battleCamera.WorldToScreenPoint(HeroDrawFeet() + Vector3.up * 0.25f);
                if (screen.z > 0f) hud.PlaceMenu(new Vector2(screen.x, screen.y));
            }
            view.Draw(mapOpen, mapOpen ? mapCamera.DotWorldSize : 0f);
            beamView?.Draw(mapOpen);
            if (!preparing) audioDirector.Tick(sim, battleCamera != null ? battleCamera.transform.position : Vector3.zero, mapOpen);
            if (mapOpen)
            {
                view.SetSpellPreview(selectedSpell, selectedSpell >= 0 && Mouse.current != null ? battleCamera.ScreenToWorldPoint(new Vector3(Mouse.current.position.ReadValue().x, Mouse.current.position.ReadValue().y, battleCamera.nearClipPlane)) : (Vector3?)null);   // 마법 범위 미리 보기 (K-036)
                view.DrawMapMarkers(cannonSide, selectedCannon, mapCamera.DotWorldSize, selectedSpell >= 0, lens);
                if (Time.unscaledTime >= nextGroupRefresh)
                {
                    nextGroupRefresh = Time.unscaledTime + 0.25f;
                    sim.RefreshSpottedGroups();
                }

                hud?.RefreshGroupLabels(sim, battleCamera);
                hud?.RefreshCompass(battleCamera, sim.CastleHalf);
                if (rig != null) hud?.RefreshHeroMarker(battleCamera, HeroDrawFeet(), rig.Side, preparing);
                Mouse dragMouse = Mouse.current;
                hud?.RefreshGroupMarkers(sim, battleCamera, selectedGroup, dragging && dragMouse != null ? dragMouse.position.ReadValue() : (Vector2?)null);
                hud?.RefreshCannons(sim, cannonSide, selectedCannon);
                hud?.RefreshLamp(sim);
                hud?.RefreshCannonMarkers(sim, battleCamera, selectedCannon);
                hud?.RefreshSpells(sim, selectedSpell);
            }
            hud?.SetMapMode(mapOpen, MapFreezesTime);
            hud?.Refresh(sim, rig, speed, paused || MapFreezesTime || TutorialTimeScale <= 0f);
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
        bool MapFreezesTime => mapOpen && (easy || preparing);

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

        // 지도 위 대포 표시를 눌러 고르기 (K-037). 그 대포의 성벽으로 바뀌고, 같은 대포를 다시 누르면 선택 해제
        public void PickCannon(int index)
        {
            if (sim == null || index < 0 || index >= sim.CannonCount) return;
            selectedSpell = -1;
            cannonSide = sim.GetCannon(index).Side;
            selectedCannon = selectedCannon == index ? -1 : index;
            cannonMoveArmed = false;
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
            CancelSquadSelection();
            panPending = dragging = false;
            if (mapOpen)
            {
                hud.CloseMenu();
                mapCamera.Open(rig != null ? rig.Side : 0);
                cannonSide = rig != null ? rig.Side : 0;
                selectedCannon = -1;
                cannonMoveArmed = false;
                selectedSpell = -1;
                selectedGroup = -1;
                dragging = false;
                RenderSettings.fog = false;   // 위에서 멀리 보는 지도에는 안개를 끈다 (시야 규칙은 점 그리기에서 그대로)
            }
            else
            {
                if (beamOrderMode)
                {
                    beamOrderMode = false;   // 지도를 닫으면 탑 명령도 끝 (CloseBeamOrder가 부른 경우 이미 false)
                    beamMapCloseAt = -1f;
                    hud?.SetBeamOrder(false);
                }

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
            CloseBeamOrder();   // 전투가 끝나면 탑 명령 판도 접는다 (N1)
            bool victory = sim.Outcome == BattleOutcome.Victory;
            string line;
            if (victory)
            {
                line = $"적 {sim.Kills:N0}을 막아 냈습니다\n얻은 골드 {sim.Gold:N0}";
            }
            else
            {
                string wall = sim.DefeatSide >= 0 ? new[] { "북쪽", "동쪽", "남쪽", "서쪽" }[sim.DefeatSide] : "";
                line = sim.DefeatReason == DefeatReason.WallDestroyed ? $"{wall} 성벽이 무너졌습니다" : $"{wall} 성벽을 지키던 병사 대부분을 잃었습니다";
                if (sim.DefeatSide >= 0)
                {
                    rig?.ShowSide(sim.DefeatSide);
                }
            }

            hud.ShowEnd(victory, line);
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
            // W11: 진 밤의 결과도 반영되므로 패배 시험 결과에도 손실·성벽 피해를 넣는다 (결과 반영 경로 시험)
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

            campaign.LastBattle = result;
            context.Complete(ScreenOutcome.Done);
        }

        static string Describe(BattleSetup setup)
        {
            // 날씨가 언제 바뀌는지는 보여 주지 않는다 (전투 중 무작위 변화, Battle.md 6장)
            string difficulty = setup.Difficulty == Difficulty.Easy ? "쉬움" : "현실";
            string night = setup.FortressNumber > 0 ? $"요새 {setup.FortressNumber} · 밤 {setup.NightNumber}" : $"전투 {setup.BattleNumber}";   // W8
            return $"{night}{(setup.IsIgnitionNight ? " (점화의 밤)" : "")} · {setup.TerrainId} · {difficulty} (적 ×{setup.EnemyCountMultiplier:0.##})";
        }
    }
}
