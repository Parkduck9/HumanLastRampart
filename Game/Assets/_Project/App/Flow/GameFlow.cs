using System.Collections;
using System.Collections.Generic;
using HumanLastRampart.App.Data;
using HumanLastRampart.App.Dev;
using HumanLastRampart.App.Ui;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Save;
using HumanLastRampart.Core.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace HumanLastRampart.App.Flow
{
    // Boot 씬에 하나. 화면 전환·진행 데이터·전투 준비/결과 분기를 맡는다 (C-003, C-004, C-005~C-007, D-017 E2).
    // 화면 씬은 한 번에 하나만 추가로 열고, 다음 화면으로 갈 때 닫는다.
    public sealed class GameFlow : MonoBehaviour
    {
        const string DefeatStoryId = "story_defeat";   // C-009(Codex 제안) 확정 전 임시 ID

        [SerializeField] List<DefinitionCatalog> catalogs = new List<DefinitionCatalog>();
        [SerializeField] string scenesFolder = "Assets/_Project/Scenes";
        [Tooltip("화면 씬이 아직 없으면 개발용 임시 화면으로 대신한다 (에디터·개발 빌드만, P2-C06)")]
        [SerializeField] bool useDevPlaceholders = true;

        public static GameFlow Instance { get; private set; }

        public CampaignState Campaign { get; private set; }
        public IDefinitionLookup Definitions => lookup;
        public ScreenId CurrentScreen { get; private set; }

        DefinitionLookup lookup;
        BootOverlay overlay;
        EventSystem eventSystem;

        Scene currentScene;
        IScreenEntry currentEntry;
        ScreenContext currentContext;
        bool transitioning;
        bool stopped;
        (ScreenContext context, ScreenOutcome outcome, NewGameOptions options)? pending;
        string pendingError;

        // 개발용: 화면 씬 하나만 열고 Play했을 때 (DevHarness)
        bool adoptOnStart;
        Scene adoptScene;
        IScreenEntry adoptEntry;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[App] GameFlow가 둘 이상입니다. 나중 것을 끕니다.");
                enabled = false;
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                GameSettings.Changed -= ApplySystemSettings;
            }
        }

        // 소리 크기·전체 화면 (K-062). 전체 화면은 사용자가 한 번이라도 골랐을 때만 바꾼다
        static void ApplySystemSettings()
        {
            AudioListener.volume = GameSettings.MasterVolume;
            if (GameSettings.FullscreenChosen && Screen.fullScreen != GameSettings.Fullscreen)
                Screen.fullScreenMode = GameSettings.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        void Start()
        {
            lookup = DefinitionLookup.Build(catalogs);
            foreach (string error in lookup.Errors)
            {
                Debug.LogError("[정의 검사] " + error);
            }

            EnsureEventSystem();
            overlay = BootOverlay.Create(transform);
            GameSettings.Load();                       // 설정 (K-062): 소리·전체 화면은 App이 적용, 나머지는 각 화면이 읽는다
            GameSettings.Changed += ApplySystemSettings;
            ApplySystemSettings();

            if (adoptOnStart)
            {
                StartCoroutine(AdoptRoutine());
            }
            else
            {
                StartCoroutine(Transition(FlowStep.Open(ScreenId.MainMenu), null));
            }
        }

        public void ConfigureCatalogs(IEnumerable<DefinitionCatalog> list)
        {
            catalogs = new List<DefinitionCatalog>(list);
        }

        // DevHarness가 Start 전에 부른다
        internal void AdoptOnStart(Scene scene, IScreenEntry entry)
        {
            adoptOnStart = true;
            adoptScene = scene;
            adoptEntry = entry;
        }

        // ── 화면이 끝났다는 신호 ──────────────────────────────────────────

        internal void OnScreenCompleted(ScreenContext context, ScreenOutcome outcome, NewGameOptions options)
        {
            if (stopped)
            {
                return;
            }

            if (transitioning)
            {
                pending = (context, outcome, options);   // OnEnter 안에서 바로 끝낸 경우: 전환이 끝난 뒤 처리
                return;
            }

            Handle(context, outcome, options);
        }

        // 화면이 알린 데이터 오류 (C-003 v1.3)
        internal void OnScreenDataError(ScreenContext context, string reason)
        {
            if (stopped || context != currentContext)
            {
                return;
            }

            if (transitioning)
            {
                pendingError = $"{context.Screen}: {reason}";   // 전환이 끝난 뒤 안내
                return;
            }

            ShowDataError($"{context.Screen}: {reason}");
        }

        void Handle(ScreenContext context, ScreenOutcome outcome, NewGameOptions options)
        {
            if (context != currentContext)
            {
                return;
            }

            var state = new FlowState();
            if (outcome == ScreenOutcome.StartNewGame)
            {
                if (!lookup.IsValid)
                {
                    Debug.LogError("[App] 정의 데이터 검사에 실패해 새 게임을 시작하지 않습니다 (C-010 4). 콘솔의 [정의 검사] 오류를 확인하세요.");
                    context.Reopen();
                    return;
                }

                CampaignState created = CampaignFactory.Create(lookup, options, NewSeed());
                if (created == null)
                {
                    Debug.LogError("[App] 시작 설정이 없어 새 게임을 만들 수 없습니다.");
                    context.Reopen();
                    return;
                }

                Campaign = created;
            }
            else if (outcome == ScreenOutcome.ContinueGame)
            {
                // 이어하기 (K-062): 저장한 진행을 불러온다. 못 읽으면 오류 안내 → 메인
                if (!CampaignSave.TryLoad(out CampaignState loaded, out _, out string loadError))
                {
                    ShowDataError("저장된 진행을 불러오지 못했습니다 — " + loadError);
                    return;
                }

                Campaign = loaded;
                state.ResumeStory = BeaconRules.IsPendingStory(Campaign.NextStory);   // W12: 점화·1회차 완료 이야기를 못 보고 닫았다면 다시 보여 준다
            }
            else if (context.Screen == ScreenId.Story && Campaign != null)
            {
                state.StoryIsDefeatEnding = Campaign.NextStory != null && Campaign.NextStory.Kind == StoryKind.DefeatEnding;
                state.StoryIsRunComplete = Campaign.NextStory != null && Campaign.NextStory.Kind == StoryKind.RunComplete;
                if (outcome == ScreenOutcome.Done)
                {
                    // W12: 이야기 사슬 (첫 이야기 → 요새 1 도착, 점화 → 다음 요새 도착). 끝난 이야기는 비운다
                    string playedId = Campaign.NextStory?.StoryId;
                    string chain = Campaign.NextStory != null && Campaign.NextStory.Kind == StoryKind.Normal ? BeaconRules.NextStoryAfter(playedId) : null;
                    state.StoryChainNext = chain != null;
                    state.StoryToDay = BeaconRules.StoryLeadsToDay(playedId);
                    Campaign.NextStory = chain != null ? new StoryRequest { Kind = StoryKind.Normal, StoryId = chain } : null;
                }
            }
            else if (context.Screen == ScreenId.Battle && outcome == ScreenOutcome.Done && Campaign?.ActiveBattleSetup != null && Campaign.ActiveBattleSetup.IsPractice)
            {
                // 연습 결과는 진행 데이터에 남기지 않는다 — 결과 반영(C-007)으로 보내지 않고 버린다 (D-063)
                Campaign.LastBattle = null;
                Campaign.ActiveBattleSetup = null;
                state.LastBattleWasPractice = true;
            }
            else if (context.Screen == ScreenId.Battle && outcome == ScreenOutcome.Done)
            {
                string error = BattleResultCheck.Validate(Campaign);
                if (error != null)
                {
                    ShowDataError(error);
                    return;
                }

                state.LastBattleOutcome = Campaign.LastBattle.Outcome;
                state.LossCanRetry = Campaign.LastBattle.Outcome == BattleOutcome.Defeat && !BeaconRules.LossEndsRun(Campaign);   // W8: 연속 두 번째 패배만 엔딩
            }
            else if (context.Screen == ScreenId.Result && Campaign != null)
            {
                state.RunComplete = Campaign.NextStory != null && Campaign.NextStory.Kind == StoryKind.RunComplete;   // W8: 마지막 요새 점화
                state.RelitFortress = Campaign.LastNightRelit;   // W10: 점화한 밤이면 결과 뒤 봉화선 지도
                state.ResultWasLostNight = Campaign.LastBattle != null && Campaign.LastBattle.Outcome == BattleOutcome.Defeat;   // W11: 진 밤의 보고
                state.ResultEndsRun = state.ResultWasLostNight && BeaconRules.LossEndsRun(Campaign);   // 연속 두 번째 패배 = 보고 뒤 패배 엔딩
            }
            else if (context.Screen == ScreenId.BeaconMap && Campaign != null)
            {
                state.MapIsPreview = Campaign.BeaconMapPreview;
                state.RunComplete = Campaign.NextStory != null && Campaign.NextStory.Kind == StoryKind.RunComplete;
                state.ResultStory = !state.MapIsPreview && BeaconRules.IsRelitStory(Campaign.NextStory?.StoryId);   // W12: 지도 다음에 점화 이야기
            }
            else if (context.Screen == ScreenId.Prep && Campaign != null)
            {
                state.HasNextBattle = NextBattleExists();
            }

            FlowStep step = FlowRouter.Next(context.Screen, outcome, state);
            if (step.Action == FlowAction.Stay)
            {
                context.Reopen();
                return;
            }

            StartCoroutine(Transition(step, context));
        }

        bool NextBattleExists()
        {
            IReadOnlyList<CampaignStartDef> starts = lookup.All<CampaignStartDef>();
            return starts.Count > 0 && starts[0].WaveForNight(Campaign.BattleNumber) != null;
        }

        // W18: 시험이 새 게임 씨앗을 고정하는 자리 (0 = 평소처럼 무작위). 시험마다 지형·웨이브가 달라 들쑥날쑥하던 것을 막는다
#if UNITY_INCLUDE_TESTS
        public static int TestSeedOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTestSeedOverride() => TestSeedOverride = 0;   // 도메인 리로드를 꺼 둔 에디터에서 취소된 시험이 값을 남기지 않게
#endif

        static int NewSeed()
        {
#if UNITY_INCLUDE_TESTS
            if (TestSeedOverride != 0) return TestSeedOverride;
#endif
            return System.Environment.TickCount ^ System.Guid.NewGuid().GetHashCode();
        }

        // ── 전환 ─────────────────────────────────────────────────────────

        IEnumerator Transition(FlowStep step, ScreenContext from)
        {
            transitioning = true;
            SetInputBlocked(true);

            yield return CloseCurrent();

            if (step.Action == FlowAction.Quit)
            {
                Quit();
                yield break;
            }

            ApplyCampaignChanges(step);
            if (step.PrepareBattle && !PrepareBattle())
            {
                transitioning = false;
                SetInputBlocked(false);
                yield break;   // 오류 안내가 떠 있다
            }

            // 진행 자동 저장 (K-062): 정비 화면에 들어올 때 슬롯 1개. 실패해도 게임은 계속 (경고만)
            // W12: 점화한 밤의 결과 화면을 지나면 지도·이야기를 보는 도중에 닫아도 점화 밤부터 다시 하지 않도록 저장한다 (이어하기는 못 본 이야기부터)
            // W11: 연속 두 번째 패배(패배 엔딩 예약)로 이야기 화면에 가는 넘어감은 저장하지 않는다 — 저장하면 이어하기가 정비로 가서 엔딩을 보고도 판이 계속된다
            bool afterRelit = from != null && from.Screen == ScreenId.Result && !step.QueueDefeatEnding && (step.Screen == ScreenId.BeaconMap || step.Screen == ScreenId.Story);
            if ((step.Screen == ScreenId.Prep || afterRelit) && Campaign != null && !CampaignSave.Save(Campaign, ScreenId.Prep, out string saveError))
            {
                Debug.LogWarning("[저장] 진행을 저장하지 못했습니다: " + saveError);
            }

            yield return OpenScreen(step.Screen);

            transitioning = false;
            SetInputBlocked(false);

            if (pendingError != null)
            {
                string error = pendingError;
                pendingError = null;
                pending = null;   // 오류를 알린 화면의 신호는 처리하지 않는다
                ShowDataError(error);
                yield break;
            }

            if (pending.HasValue)
            {
                var p = pending.Value;
                pending = null;
                Handle(p.context, p.outcome, p.options);
            }
        }

        void ApplyCampaignChanges(FlowStep step)
        {
            if (step.DeleteSave)
            {
                CampaignSave.Delete();   // 한 판이 끝남 (K-062)
            }

            if (step.DiscardCampaign)
            {
                Campaign = null;
            }

            if (Campaign == null)
            {
                return;
            }

            if (step.Screen == ScreenId.BeaconMap)
            {
                Campaign.BeaconMapPreview = step.MapPreview;   // W10: 정비에서 연 보기 전용인지 지도 화면이 읽는다
            }

            if (step.AdvanceBattle)
            {
                Campaign.BattleNumber++;   // C-006: App이 한 번 증가 (W8: 결과의 번호는 전투 자리라 밤 번호 자체를 올린다)
            }

            if (step.ApplyNightLoss)
            {
                // W11 (사용자 규칙): 진 밤의 결과(병력 손실·성벽 피해, 보상 골드 없음)는 결과 화면(ResultEntry)이 이미 반영했다.
                // 여기서는 복구 절반·연속 패배 +1, 그리고 무너진 성벽을 규칙 데이터의 최소 비율까지 되돌린다
                BeaconRules.ApplyLoss(Campaign);   // LastBattle·ActiveBattleSetup은 다음 전투 준비(PrepareBattle) 때 갱신된다
                IReadOnlyList<BattleRulesDef> ruleList = lookup.All<BattleRulesDef>();
                if (ruleList.Count > 0) BeaconRules.RaiseWallFloor(Campaign, ruleList[0].LostNightWallFloorPercent);
            }

            if (step.BeginDay)
            {
                BeaconRules.BeginDay(Campaign);   // W8: 낮 시작 수비대 지원금
            }

            if (step.QueueDefeatEnding)
            {
                Campaign.NextStory = new StoryRequest { Kind = StoryKind.DefeatEnding, StoryId = DefeatStoryId };
            }
        }

        bool PrepareBattle()
        {
            Campaign.LastBattle = null;   // 이전 결과 재사용 방지 (C-004 r2)
            // 용병 급료 (K-058, D-079 U8): 전투 시작 전에 빠지고, 못 주면 그 계약은 떠난다
            foreach (string note in HumanLastRampart.Core.Upgrades.UpgradeRules.PayWages(Campaign, lookup)) Debug.Log("[용병] " + note);
            BattleSetup setup = BattleSetupFactory.Create(Campaign, lookup, BattleSetupFactory.NewRunId(), out string error);
            if (setup == null)
            {
                ShowDataError(error);
                return false;
            }

            Campaign.ActiveBattleSetup = setup;
            return true;
        }

        IEnumerator CloseCurrent()
        {
            if (currentEntry != null)
            {
                try
                {
                    currentEntry.OnExit();
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            currentContext?.Invalidate();
            currentContext = null;
            currentEntry = null;

            if (currentScene.IsValid() && currentScene.isLoaded && currentScene != gameObject.scene)
            {
                yield return SceneManager.UnloadSceneAsync(currentScene);
            }

            currentScene = default;
        }

        IEnumerator OpenScreen(ScreenId screen)
        {
            var load = new ScreenLoad();
            yield return LoadScreenScene(screen, load);

            if (load.Entry == null)
            {
                Debug.LogError($"[App] {screen} 화면을 열지 못했습니다: {load.Error} (C-003 3-3)");
                if (load.Scene.IsValid() && load.Scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(load.Scene);
                }

                if (screen == ScreenId.MainMenu)
                {
                    stopped = true;   // 무한 반복 방지
                    Debug.LogError("[App] 메인 화면도 열 수 없어 멈춥니다.");
                    yield break;
                }

                Campaign = null;
                yield return OpenScreen(ScreenId.MainMenu);
                yield break;
            }

            Enter(screen, load.Scene, load.Entry);
        }

        void Enter(ScreenId screen, Scene scene, IScreenEntry entry)
        {
            currentScene = scene;
            currentEntry = entry;
            CurrentScreen = screen;
            currentContext = new ScreenContext(this, screen);
            HumanLastRampart.App.Audio.AudioService.Ensure()?.OnScreen(screen, Campaign?.LastBattle != null && Campaign.LastBattle.Outcome == BattleOutcome.Victory);   // 화면별 음악 (W24, 소리 은행이 비면 조용)
            if (scene.IsValid())
            {
                SceneManager.SetActiveScene(scene);
            }

            try
            {
                entry.OnEnter(currentContext);
                HumanLastRampart.App.Audio.AudioService.Current?.AdoptSceneListeners();   // 화면 카메라의 리스너를 서비스 하나로 모음 (W24)
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        sealed class ScreenLoad
        {
            public Scene Scene;
            public IScreenEntry Entry;
            public string Error;
        }

        IEnumerator LoadScreenScene(ScreenId screen, ScreenLoad load)
        {
            string sceneName = screen.ToString();
            string path = scenesFolder + "/" + sceneName + ".unity";
            AsyncOperation operation = null;

            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            }
#if UNITY_EDITOR
            else if (System.IO.File.Exists(path))
            {
                // 빌드 씬 목록에 아직 없을 때(P2-C07 전) 에디터에서만 경로로 연다
                operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            }
#endif

            if (operation == null)
            {
                if (useDevPlaceholders && (Application.isEditor || Debug.isDebugBuild))
                {
                    load.Scene = SceneManager.CreateScene("Dev_" + sceneName);
                    load.Entry = DevPlaceholderScreen.Create(load.Scene, screen);
                    yield break;
                }

                load.Error = $"씬 '{path}'이 없습니다";
                yield break;
            }

            yield return operation;
            load.Scene = SceneManager.GetSceneByName(sceneName);
            if (!load.Scene.IsValid())
            {
                load.Scene = SceneManager.GetSceneByPath(path);
            }

            load.Entry = FindSingleEntry(load.Scene, screen, out load.Error);
        }

        public static IScreenEntry FindSingleEntry(Scene scene, ScreenId screen, out string error)
        {
            error = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "씬을 불러오지 못했습니다";
                return null;
            }

            var found = new List<IScreenEntry>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is IScreenEntry entry)
                    {
                        found.Add(entry);
                    }
                }
            }

            if (found.Count != 1)
            {
                error = $"진입 컴포넌트가 {found.Count}개입니다 (정확히 1개여야 함)";
                return null;
            }

            if (found[0].Id != screen)
            {
                error = $"진입 컴포넌트의 Id가 {found[0].Id}입니다 ({screen}이어야 함)";
                return null;
            }

            return found[0];
        }

        // ── 개발용: 화면 씬을 직접 Play했을 때 ─────────────────────────────

        IEnumerator AdoptRoutine()
        {
            transitioning = true;
            ScreenId screen = adoptEntry.Id;
            if (screen != ScreenId.MainMenu)
            {
                Campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Realistic), NewSeed());
                if (Campaign == null)
                {
                    Debug.LogError("[개발 진입] 시작 설정이 없어 임시 진행 데이터를 만들 수 없습니다.");
                }
                else
                {
                    PrepareBattle();
                    if (screen == ScreenId.Result)
                    {
                        // 임시 승리 결과로 결과 화면 열기 (P2-C06)
                        Campaign.LastBattle = DevPlaceholderScreen.MakeTestResult(Campaign.ActiveBattleSetup, BattleOutcome.Victory, DefeatReason.None);
                        Campaign.BattleNumber++;
                    }
                }
            }

            Debug.Log($"[개발 진입] {screen} 화면을 Boot 없이 시작합니다. 임시 진행 데이터: {(Campaign != null ? "있음(현실)" : "없음")}");
            yield return null;
            Enter(screen, adoptScene, adoptEntry);
            transitioning = false;
        }

        // ── 입력·오류·종료 ───────────────────────────────────────────────

        void EnsureEventSystem()
        {
            eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                go.transform.SetParent(transform, false);
                eventSystem = go.GetComponent<EventSystem>();
            }
        }

        void SetInputBlocked(bool blocked)
        {
            if (eventSystem != null && !(overlay != null && overlay.ErrorShown))
            {
                eventSystem.enabled = !blocked;
            }

            overlay?.SetFade(blocked);
        }

        void ShowDataError(string reason)
        {
            Debug.LogError("[App] 데이터 오류로 진행을 멈춥니다 (D-017 E2): " + reason);
            if (eventSystem != null)
            {
                eventSystem.enabled = true;   // 안내 버튼은 눌려야 한다
            }

            overlay.SetFade(false);
            overlay.ShowError(reason, () =>
            {
                overlay.HideError();
                StartCoroutine(Transition(FlowStep.ToMainMenu(), currentContext));
            });
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
