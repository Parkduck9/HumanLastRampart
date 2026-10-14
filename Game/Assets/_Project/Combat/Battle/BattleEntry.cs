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
                view = new BattleView(sim, spriteShader, transform);
                rig = new BattleCameraRig(battleCamera, sim.CastleHalf, BattleSim.WallHeight);
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
            }
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
            if (mouse != null && !sim.Ended)
            {
                if (mouse.middleButton.wasPressedThisFrame) hud.ToggleMenu(rig != null ? rig.Side : 0);
                if (mouse.rightButton.wasPressedThisFrame) hud.CloseMenu();
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

            rig?.Update(Time.unscaledDeltaTime);
            if (paused)
            {
                return;
            }

            // 큰 프레임 지연에도 한 번에 너무 멀리 가지 않게 자른다
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f) * speed;
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
            view.Draw();
            hud?.Refresh(sim, rig, speed, paused);
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
            WeatherForecast f = setup.Forecast;
            string change = f.Changes
                ? $" → {f.ChangeAtSeconds:0}초에 {f.ChangedWeather}{(f.IsShower ? "(소나기)" : "")}"
                : "";
            return $"전투 {setup.BattleNumber} · {setup.TerrainId} · {setup.Difficulty} (적 ×{setup.EnemyCountMultiplier:0.##})\n" +
                   $"날씨 {f.InitialWeather} · {f.TimeOfDay}{change}  (날씨 효과는 다음 바퀴)";
        }
    }
}
