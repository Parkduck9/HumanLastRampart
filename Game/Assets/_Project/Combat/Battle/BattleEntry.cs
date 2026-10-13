using System.Text;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 1·2번 전투 화면의 진입 컴포넌트 (C-003, C-005, C-006) — Phase 2 뼈대
    // 지금은 전투 준비 정보를 보여 주고, 시험용 [승리]/[패배] 버튼으로 전투 결과를 만든다.
    // Phase 3에서 PerfLab 구조(D-024)로 실제 전투를 채운다.
    public sealed class BattleEntry : MonoBehaviour, IScreenEntry
    {
        IScreenContext context;
        GameObject ui;
        Camera battleCamera;

        public ScreenId Id => ScreenId.Battle;

        void Awake()
        {
            // 화면 씬마다 카메라·AudioListener 하나 (C-003 3-2). 1번 화면은 원근 낮은 카메라(D-025) 자리
            var cameraObject = new GameObject("Battle Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            battleCamera = cameraObject.GetComponent<Camera>();
            battleCamera.clearFlags = CameraClearFlags.SolidColor;
            battleCamera.backgroundColor = new Color(0.35f, 0.45f, 0.4f);
            battleCamera.fieldOfView = 40f;
        }

        public void OnEnter(IScreenContext screenContext)
        {
            context = screenContext;
            CampaignState campaign = context.Campaign;
            BattleSetup setup = campaign != null ? campaign.ActiveBattleSetup : null;

            ui = new GameObject("Battle UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            ui.transform.SetParent(transform, false);
            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Text title = NewText("Title", 44);
            Place(title.rectTransform, 0.5f, 0.85f, 1500f, 120f);
            title.text = "전투 (Phase 2 뼈대)\n<size=24>실제 전투는 Phase 3에서 PerfLab 구조로 채운다</size>";

            Text info = NewText("Info", 26);
            Place(info.rectTransform, 0.5f, 0.58f, 1500f, 340f);
            info.text = setup != null ? Describe(setup, context.Definitions) : "전투 준비 정보가 없습니다 (ActiveBattleSetup 없음)";

            AddButton("[시험] 승리", 0.3f, () => Finish(BattleOutcome.Victory, DefeatReason.None));
            AddButton("[시험] 패배 — 성벽 붕괴", 0.2f, () => Finish(BattleOutcome.Defeat, DefeatReason.WallDestroyed));
            AddButton("[시험] 패배 — 병력 70% 손실", 0.1f, () => Finish(BattleOutcome.Defeat, DefeatReason.GarrisonLost));
        }

        public void OnExit()
        {
            if (ui != null)
            {
                Destroy(ui);
            }

            context = null;
        }

        // 전투 결과를 한 번 완성해 진행 데이터에 기록하고 끝낸다 (C-006: Combat이 LastBattle을 쓴다)
        void Finish(BattleOutcome outcome, DefeatReason reason)
        {
            if (context == null)
            {
                return;
            }

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

            // 참가 영웅마다 정확히 한 항목 (C-006). 시험 결과라 시작 상태 그대로
            foreach (string heroId in setup.ParticipatingHeroIds)
            {
                HeroState hero = setup.Heroes.Find(h => h.InstanceId == heroId);
                result.HeroOutcomes.Add(new HeroOutcome
                {
                    InstanceId = heroId,
                    HpAfter = hero != null ? hero.Hp : 0,
                    IsAliveAfter = hero != null && hero.IsAlive,
                });
            }

            if (outcome == BattleOutcome.Victory)
            {
                // 시험용 손실: 병사가 있는 첫 분대 1명, 북쪽 성벽 50 피해 — 결과 화면(C-007)의 반영 확인용
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

        static string Describe(BattleSetup setup, IDefinitionLookup defs)
        {
            var text = new StringBuilder();
            WeatherForecast f = setup.Forecast;
            text.AppendLine($"전투 {setup.BattleNumber} · 지형 {setup.TerrainId} · 웨이브 {setup.WaveDefId}");
            text.AppendLine($"난이도 {setup.Difficulty} (적 수 ×{setup.EnemyCountMultiplier:0.##}, 최대 동시 {setup.MaxConcurrentEnemies:N0})");
            string change = f.Changes
                ? $" → {f.ChangeAtSeconds:0}초에 {f.ChangedWeather}{(f.IsShower ? "(소나기)" : "")}{(f.Reverts ? $" → {f.RevertAtSeconds:0}초에 원래대로" : "")}"
                : " (변화 없음)";
            text.AppendLine($"날씨 {f.InitialWeather} · {f.TimeOfDay}{change}");

            int enemies = 0;
            WaveDef wave = defs != null ? defs.Get<WaveDef>(setup.WaveDefId) : null;
            if (wave != null)
            {
                var bySide = new int[4];
                foreach (WaveGroup group in wave.Groups)
                {
                    int count = Mathf.Max(1, Mathf.RoundToInt(group.Count * setup.EnemyCountMultiplier));
                    bySide[(int)group.Side] += count;
                    enemies += count;
                }

                text.AppendLine($"적 {enemies:N0} (북 {bySide[0]:N0} · 동 {bySide[1]:N0} · 남 {bySide[2]:N0} · 서 {bySide[3]:N0})");
            }

            int soldiers = 0;
            foreach (SquadState squad in setup.Squads) soldiers += squad.Count;
            text.Append($"아군 분대 {setup.Squads.Count}개 · 병사 {soldiers:N0} · 영웅 참가 {setup.ParticipatingHeroIds.Count} · 성벽 {setup.Wall.Count}개 · 함정 {setup.Traps.Count}");
            return text.ToString();
        }

        Text NewText(string name, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(ui.transform, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");   // 임시 글꼴 (D-033 F1)
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        void AddButton(string label, float y, System.Action onClick)
        {
            var go = new GameObject("Button " + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(ui.transform, false);
            go.GetComponent<Image>().color = new Color(0.25f, 0.3f, 0.4f, 1f);
            go.GetComponent<Button>().onClick.AddListener(() => onClick());
            Place((RectTransform)go.transform, 0.5f, y, 520f, 76f);

            Text text = NewText("Label", 28);
            text.transform.SetParent(go.transform, false);
            text.text = label;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}