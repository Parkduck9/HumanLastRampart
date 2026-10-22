using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    public sealed class PrepEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.Prep;

        protected override ScreenView BuildView(IScreenContext context)
        {
            var campaign = context.Campaign;
            PreparationTransactions session;
            TrapPreparationTransactions traps;
            try { session = new PreparationTransactions(campaign, context.Definitions);
                traps = new TrapPreparationTransactions(campaign, context.Definitions); }
            catch (ArgumentException ex)
            {
                context.ReportDataError(ex.Message);
                return new ScreenView(transform, "정비 마당을 열 수 없습니다", "장부가 맞지 않습니다. 개발 기록을 확인해 주세요.");
            }

            var view = new ScreenView(transform, "", "");
            // 장소 일은 숨긴 단추로 연다 (마당 E와 시험이 같은 길). 열쇠는 시험이 찾는 이름
            Button repair = null, recruit = null, trapButton = null;
            repair = view.AddButton("성벽 수리", () => PreparationPanel.Show(repair, context, session, false));
            recruit = view.AddButton("병력 모집·보충", () => PreparationPanel.Show(recruit, context, session, true));
            trapButton = view.AddButton("함정 구매·설치", () => TrapPreparationPanel.Show(trapButton, context, traps, session));
            // K-058 (D-079): 상점(영웅 장비 칸·단체 아이템), 병영 안 [병종 훈련]·[용병 고용]
            Button shop = null, training = null, mercenary = null;
            shop = view.AddButton("상점", () => UpgradePanel.Show(shop, context, session, UpgradePanel.Mode.Shop));
            training = view.AddButton("병종 훈련", () => UpgradePanel.Show(training, context, session, UpgradePanel.Mode.Training));
            mercenary = view.AddButton("용병 고용", () => UpgradePanel.Show(mercenary, context, session, UpgradePanel.Mode.Mercenary));
            PreparationPanel.OpenRecruit = () => recruit.onClick.Invoke();
            PreparationPanel.OpenTraining = () => training.onClick.Invoke();
            PreparationPanel.OpenMercenary = () => mercenary.onClick.Invoke();
            var ready = view.AddButton("전투 준비 완료", () => { session.Close(); traps.Close(); Complete(ScreenOutcome.Done); });
            foreach (Button b in new[] { repair, recruit, trapButton, shop, training, mercenary, ready }) ScreenView.HideButton(b);

            // D-076 정비 마당 + K-047: 위쪽은 황동 계기 띠 한 줄 — 1순위 골드, 그다음 병력·다음 전투 (S-20)
            view.DockTop(1560f, 80f + 2 * 72f);   // W8: 등대 줄 두 개. W16: 줄마다 높이 48 + 간격 24라 72씩. 밤 사이 한마디(W12)는 띠 밖 아래쪽 띠로 옮겼다 (마당 간판을 가리지 않게)
            Button mapButton = BuildSummary(view.AddArea("Summary", 48f), campaign,
                () => { session.Close(); traps.Close(); Complete(ScreenOutcome.OpenBeaconMap); });   // W10: 봉화선 지도 보기 (보기 전용, 닫으면 정비로)
            PreparationPanel.RefreshSummary(repair, context, session);
            Action refreshTowerLater = null;   // 마당(탑 그림)은 아래에서 만든다 — 수리를 사면 탑 불꽃도 바로 새로 그린다 (S5)
            BeaconPrepPanel.Build(view.AddArea("Beacon Line 1", 48f), view.AddArea("Beacon Line 2", 48f), null, context,
                () => { PreparationPanel.RefreshSummary(repair, context, session); refreshTowerLater?.Invoke(); });   // W8: 요새·밤, 등대 복구, 성벽 조건, 밝기 선택, 등대 수리, 수비대 지원금
            BuildDayLineStrip(repair.GetComponentInParent<UnityEngine.Canvas>().transform, campaign);   // W12 밤 사이 한마디: 화면 아래 가운데 작은 띠 (없으면 만들지 않음)
            PrepYard yard = PrepYard.Build(view, HeroUnit(context));   // K-089 배경·3D 바닥·가림을 마당이 함께 소유한다.
            yard.AddStation("성벽 수리", 500f, new Color(0.45f, 0.42f, 0.38f), () => repair.onClick.Invoke());
            yard.AddStation("병영", 1300f, new Color(0.42f, 0.30f, 0.24f), () => recruit.onClick.Invoke());
            yard.AddStation("함정 공방", 2100f, new Color(0.30f, 0.30f, 0.36f), () => trapButton.onClick.Invoke());
            yard.AddStation("상점", 2550f, new Color(0.46f, 0.36f, 0.22f), () => shop.onClick.Invoke());   // K-058 보급 상인 (간판 그림은 Codex 요청)
            // W9: 가운데 등대 — 다음 밤 빛줄기 방향 명령. 탑 그림은 전투 화면과 같은 규칙(꺼짐 없음, 불꽃 세기 = 복구, 점화의 밤 앞이면 완전 점화, K-110 탑 + K-102 불꽃)
            Action refreshTower = () =>
            {
                bool lit = HumanLastRampart.Core.Campaign.BeaconRules.IsIgnitionNight(campaign);
                yard.SetBeaconFlame(Resources.Load<Texture2D>(lit ? "BattleArt/Beacon/flame_lit" : "BattleArt/Beacon/flame_warming"),
                    HumanLastRampart.Core.Campaign.BeaconRules.TowerFlameStrength(campaign.BeaconRepair, lit));
            };
            refreshTowerLater = refreshTower;
            yard.AddStation("등대", 1745f, new Color(0.45f, 0.40f, 0.30f), () => yard.OpenModal(parent =>
                BeaconYardPanel.Build(parent, context, refreshTower, yard.CloseModal)), new Vector2(1560f, 640f));   // W16: 간판은 탑 왼쪽 위 (탑 뒤에 가리지 않게)
            bool beaconLit = HumanLastRampart.Core.Campaign.BeaconRules.IsIgnitionNight(campaign);
            yard.AddBeaconTower(Resources.Load<Texture2D>("BattleArt/Beacon/tower_base"),
                Resources.Load<Texture2D>(beaconLit ? "BattleArt/Beacon/flame_lit" : "BattleArt/Beacon/flame_warming"),
                HumanLastRampart.Core.Campaign.BeaconRules.TowerFlameStrength(campaign.BeaconRepair, beaconLit), 1920f, 930f);
            yard.AddStation("성벽 계단", 3000f, new Color(0.40f, 0.38f, 0.30f), () => yard.AskNextBattle(() => ready.onClick.Invoke()));
            var lifetime = repair.GetComponentInParent<UnityEngine.Canvas>().gameObject.AddComponent<PreparationSessionLifetime>();
            lifetime.Session = session; lifetime.Traps = traps;
            var tutorial = HumanLastRampart.Tutorial.PrepTutorialSource.TryBegin(transform, campaign, context.Definitions);   // 첫 전투 뒤 정비 튜토리얼 (W2): 함정 놓기 → 망원경 업그레이드 설명
            // W10: 튜토리얼이 도는 동안은 지도 단추를 잠근다 (단계는 저장돼 이어 하지만, 지도로 나가면 정비 화면이 다시 만들어져 안내가 끊기므로)
            if (tutorial != null) mapButton.gameObject.AddComponent<MapButtonGate>().Init(mapButton, tutorial);
            return view;
        }

        // 밤 사이 한마디 띠 (W12 내용, W16 자리): 화면 아래 가운데, 어두운 반투명 판 + 갈무리 글. 한마디가 없는 날은 만들지 않는다
        static void BuildDayLineStrip(Transform canvas, HumanLastRampart.Core.Campaign.CampaignState campaign)
        {
            var lines = HumanLastRampart.Core.Campaign.BeaconRules.DayLines(campaign);
            if (lines == null || lines.Count == 0) return;
            Image plate = Ui.Image(canvas, "Day Line Strip", UiTokens.Alpha(UiTokens.Outline, 0.72f));
            plate.raycastTarget = false;
            Ui.Place(plate.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(1320f, 64f), new Vector2(0.5f, 0f));
            Text text = Ui.Label(plate.transform, "Day Line", string.Join("   ·   ", lines.ConvertAll(l => $"{l.speaker}: \"{l.text}\"")), UiTokens.Small, UiTokens.Body, TextAnchor.MiddleCenter);
            Ui.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(UiTokens.S24, 0f);
            text.rectTransform.offsetMax = new Vector2(-UiTokens.S24, 0f);
        }
        sealed class MapButtonGate : MonoBehaviour
        {
            Button button;
            HumanLastRampart.Tutorial.TutorialDirector director;

            public void Init(Button target, HumanLastRampart.Tutorial.TutorialDirector tutorial)
            {
                button = target; director = tutorial;
                Refresh();
            }

            void Update() { Refresh(); }

            void Refresh() { if (button != null) button.interactable = director == null || director.Finished; }
        }

        // [정비 마당]  요새 1 · 밤 2 앞          [골드] 1,240   [병력] 520/1,200
        static Button BuildSummary(RectTransform area, HumanLastRampart.Core.Campaign.CampaignState campaign, Action openMap)
        {
            HorizontalLayoutGroup row = Ui.Row(area, "Summary Row", UiTokens.S16);
            Ui.Stretch((RectTransform)row.transform);
            Text title = Ui.Label(row.transform, "Title", "정비 마당", UiTokens.H2, UiTokens.Brass, TextAnchor.MiddleLeft, true);
            Ui.Size(title, title.preferredWidth + 8f, 48f);
            Text next = Ui.Label(row.transform, "Next Battle", HumanLastRampart.Core.Campaign.BeaconRules.NightLabel(campaign.FortressNumber, campaign.NightNumber) + " 앞", UiTokens.Text, UiTokens.BodyDim);
            Ui.Size(next, next.preferredWidth + 8f, 48f);
            Button map = Ui.Button(row.transform, "봉화선 지도", "봉화선 지도", openMap);   // W10: 작은 단추
            Ui.Size(map, 176f, 48f);
            RectTransform spacer = Ui.Rect(row.transform, "Spacer");
            Ui.Size(spacer, 0f, 48f, true);
            Image gold = Ui.HudIcon(row.transform, "Gold Icon", "Icons/stat_gold", 32f);
            if (gold == null) Ui.Size(Ui.Label(row.transform, "Gold Word", "골드", UiTokens.Small, UiTokens.BodyDim), 44f, 48f);
            Text goldValue = Ui.Label(row.transform, "Gold Value", "", UiTokens.H2, UiTokens.Body, TextAnchor.MiddleLeft, true);
            Ui.Size(goldValue, 136f, 48f);
            Image troops = Ui.HudIcon(row.transform, "Troops Icon", "Icons/stat_troops", 24f);
            if (troops == null) Ui.Size(Ui.Label(row.transform, "Troops Word", "병력", UiTokens.Small, UiTokens.BodyDim), 44f, 48f);
            Text troopsValue = Ui.Label(row.transform, "Troops Value", "", UiTokens.Text, UiTokens.Body);
            Ui.Size(troopsValue, 136f, 48f);
            return map;
        }

        // 마당을 걷는 주인공 그림: 참가 영웅 중 주인공(UnitKind.Hero)
        static HumanLastRampart.Core.Definitions.UnitDef HeroUnit(IScreenContext context)
        {
            var campaign = context.Campaign;
            if (campaign?.Heroes == null) return null;
            foreach (var hero in campaign.Heroes)
            {
                var unit = context.Definitions.Get<HumanLastRampart.Core.Definitions.UnitDef>(hero.UnitDefId);
                if (unit != null && unit.Kind == HumanLastRampart.Core.Definitions.UnitKind.Hero) return unit;
            }

            return null;
        }
    }
}
