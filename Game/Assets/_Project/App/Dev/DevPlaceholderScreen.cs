using HumanLastRampart.App.Ui;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HumanLastRampart.App.Dev
{
    // 개발용 임시 화면 (P2-C06). 화면 씬이 아직 없을 때 App이 대신 띄운다.
    // 화면 이름·진행 데이터 요약과 그 화면이 보낼 수 있는 신호 버튼만 있다. 실제 화면이 생기면 쓰이지 않는다.
    public sealed class DevPlaceholderScreen : MonoBehaviour, IScreenEntry
    {
        ScreenId id;
        IScreenContext context;
        GameObject ui;

        public ScreenId Id => id;

        public static IScreenEntry Create(Scene scene, ScreenId screen)
        {
            var go = new GameObject("[임시 화면] " + screen);
            SceneManager.MoveGameObjectToScene(go, scene);
            var placeholder = go.AddComponent<DevPlaceholderScreen>();
            placeholder.id = screen;

            // 화면 씬마다 카메라·AudioListener 하나 (C-003 3-2)
            var cameraObject = new GameObject("Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(go.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.16f);
            return placeholder;
        }

        public void OnEnter(IScreenContext screenContext)
        {
            context = screenContext;
            ui = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            ui.transform.SetParent(transform, false);
            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Text title = BootOverlay.NewText(ui.transform, "Title", 48);
            Place(title.rectTransform, 0.5f, 0.82f, 1400f, 120f);
            title.text = $"[임시 화면] {id}\n<size=26>실제 {id} 씬이 생기면 이 화면 대신 그 씬이 열립니다</size>";

            Text info = BootOverlay.NewText(ui.transform, "Info", 26);
            Place(info.rectTransform, 0.5f, 0.6f, 1400f, 200f);
            info.text = Describe(context.Campaign);

            float y = 0.4f;
            if (id == ScreenId.MainMenu)
            {
                AddButton("쉬움으로 시작", ref y, () => context.CompleteNewGame(new NewGameOptions(Difficulty.Easy)));
                AddButton("현실로 시작", ref y, () => context.CompleteNewGame(new NewGameOptions(Difficulty.Realistic)));
                AddButton("종료", ref y, () => context.Complete(ScreenOutcome.QuitGame));
                return;
            }

            if (id == ScreenId.Battle)
            {
                // 실제 Battle 씬이 없을 때만 쓰인다
                AddButton("[시험] 승리", ref y, () => FinishBattle(BattleOutcome.Victory, DefeatReason.None));
                AddButton("[시험] 패배", ref y, () => FinishBattle(BattleOutcome.Defeat, DefeatReason.WallDestroyed));
            }
            else
            {
                AddButton("다음", ref y, () => context.Complete(ScreenOutcome.Done));
            }

            AddButton("메인으로", ref y, () => context.Complete(ScreenOutcome.BackToMainMenu));
        }

        public void OnExit()
        {
            if (ui != null)
            {
                Destroy(ui);
            }

            context = null;
        }

        void FinishBattle(BattleOutcome outcome, DefeatReason reason)
        {
            CampaignState campaign = context.Campaign;
            campaign.LastBattle = MakeTestResult(campaign.ActiveBattleSetup, outcome, reason);
            context.Complete(ScreenOutcome.Done);
        }

        public static BattleResult MakeTestResult(BattleSetup setup, BattleOutcome outcome, DefeatReason reason)
        {
            return new BattleResult
            {
                BattleRunId = setup != null ? setup.BattleRunId : null,
                BattleNumber = setup != null ? setup.BattleNumber : 0,
                Outcome = outcome,
                DefeatedEnemyCount = 100,
                EarnedGold = outcome == BattleOutcome.Victory ? 50 : 0,
                DefeatReason = outcome == BattleOutcome.Defeat ? reason : DefeatReason.None,
                DefeatWallId = outcome == BattleOutcome.Defeat ? WallIds.North : null,
            };
        }

        static string Describe(CampaignState campaign)
        {
            if (campaign == null)
            {
                return "진행 데이터 없음 (메인 화면)";
            }

            string story = campaign.NextStory != null ? $"{campaign.NextStory.Kind} / {campaign.NextStory.StoryId}" : "-";
            return $"전투 번호 {campaign.BattleNumber} · 골드 {campaign.Gold} · 난이도 {campaign.Difficulty}\n" +
                   $"성벽 {campaign.Wall.Count}개 · 영웅 {campaign.Heroes.Count} · 분대 {campaign.Squads.Count} · 다음 스토리 {story}";
        }

        void AddButton(string label, ref float y, System.Action onClick)
        {
            Button button = BootOverlay.NewButton(ui.transform, label, onClick);
            Place((RectTransform)button.transform, 0.5f, y, 420f, 80f);
            y -= 0.1f;
        }

        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}