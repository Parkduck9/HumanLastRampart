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
            view.DockTop(1720f, 80f);   // W36: 한 줄짜리 얇은 띠 (등대 등실이 가려지지 않게). 등대 줄 두 개는 [등대 세부] 칸으로 옮겼다. 밤 사이 한마디(W12)는 띠 밖 아래쪽 띠
            GameObject detail = null;   // [등대 세부] 칸 (아래에서 만든다)
            Button mapButton = BuildSummary(view.AddArea("Summary", 48f), campaign,
                () => { session.Close(); traps.Close(); Complete(ScreenOutcome.OpenBeaconMap); },
                () => { if (detail != null) detail.SetActive(!detail.activeSelf); }, out Text beaconBrief);   // W10: 봉화선 지도 보기 (보기 전용, 닫으면 정비로)
            PreparationPanel.RefreshSummary(repair, context, session);
            PrepYard yard = null;   // 등대 수리 단추가 마당 불빛을 새로 칠하도록 먼저 선언 (마당은 아래에서 만든다)
            // W36: 밝기 선택·등대 수리·성벽 조건은 띠 아래 접이식 칸. 닫혀 있어도 한 줄 요약(beaconBrief)은 늘 보인다
            Image detailPlate = Ui.Panel(repair.GetComponentInParent<UnityEngine.Canvas>().transform, "Beacon Detail", UiPanel.Band);
            Ui.Place(detailPlate.rectTransform, new Vector2(0f, 1f), new Vector2(100f, -(UiTokens.S32 + 80f + 8f)), new Vector2(740f, 16f * 2 + 48f * 4 + 16f * 3), new Vector2(0f, 1f));   // 왼쪽 좁은 판: 가운데 탑 기둥(등실)을 가리지 않는다
            var detailColumn = detailPlate.gameObject.AddComponent<VerticalLayoutGroup>();
            detailColumn.padding = new RectOffset(32, 32, 16, 16); detailColumn.spacing = 16f;
            detailColumn.childControlWidth = detailColumn.childControlHeight = true;
            detailColumn.childForceExpandWidth = true; detailColumn.childForceExpandHeight = false;
            var detailLines = new RectTransform[4];
            for (int i = 0; i < detailLines.Length; i++) { detailLines[i] = Ui.Rect(detailPlate.transform, "Beacon Line " + (i + 1)); Ui.Size(detailLines[i], -1f, 48f); }
            BeaconPrepPanel.Build(detailLines[0], detailLines[1], null, context,
                () => { PreparationPanel.RefreshSummary(repair, context, session); if (yard != null) yard.RefreshTower(); }, beaconBrief, detailLines[2], detailLines[3]);
            detail = detailPlate.gameObject; detail.AddComponent<DetailGuard>(); detail.SetActive(false);   // W8: 요새·밤, 등대 복구, 성벽 조건, 밝기 선택, 등대 수리, 수비대 지원금. W36: 수리하면 탑 불빛도 바로
            BuildDayLineStrip(repair.GetComponentInParent<UnityEngine.Canvas>().transform, campaign);   // W12 밤 사이 한마디: 화면 아래 가운데 작은 띠 (없으면 만들지 않음)
            yard = PrepYard.Build(view, HeroUnit(context));   // K-089 배경·3D 바닥·가림을 마당이 함께 소유한다.
            yard.BindBeacon(campaign);   // W36: 등실 불빛 덧그림이 복구·점화의 밤을 따라간다
            yard.AddStation("성벽 수리", 1095f, new Color(0.45f, 0.42f, 0.38f), () => repair.onClick.Invoke());
            yard.AddStation("병영", 3045f, new Color(0.42f, 0.30f, 0.24f), () => recruit.onClick.Invoke());
            yard.AddStation("함정 공방", 525f, new Color(0.30f, 0.30f, 0.36f), () => trapButton.onClick.Invoke());
            yard.AddStation("상점", 3165f, new Color(0.46f, 0.36f, 0.22f), () => shop.onClick.Invoke());   // K-058 보급 상인 (간판 그림은 Codex 요청)
            // W9 → K-120: 가운데 등대 — 다음 밤 빛줄기 방향 명령. 탑은 정비 마당 그림에 이미 있어 따로 그리지 않는다 (탑 자리는 마당 장애물이 막는다)
            yard.AddStation("등대", 1917f, new Color(0.45f, 0.40f, 0.30f), () => yard.OpenModal(parent =>
                BeaconYardPanel.Build(parent, context, yard.RefreshTower, yard.CloseModal)));   // W36: 탑 불빛 새로 칠하기. 간판 자리는 layout.json (탑 왼쪽 위, 탑 뒤에 가리지 않게)
            yard.AddStation("성벽 계단", 1755f, new Color(0.40f, 0.38f, 0.30f), () => yard.AskNextBattle(() => ready.onClick.Invoke()));
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
        // B 키로 [등대 세부] 칸 열고 닫기 (마우스 단추와 같은 길). 마당 창이 떠 있을 때는 건드리지 않는다
        sealed class DetailKey : MonoBehaviour
        {
            Action toggle;
            public void Init(Action action) { toggle = action; }
            void Update()
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard == null || !keyboard.bKey.wasPressedThisFrame) return;
                var yard = UnityEngine.Object.FindAnyObjectByType<PrepYard>();
                if (yard != null && yard.CanMove) toggle?.Invoke();   // 마당 창·확인창이 떠 있거나 다른 창이 입력을 막는 동안은 건드리지 않는다
            }
        }
        // 마당 창·확인창·상점 등으로 마당이 멈춰 있는 동안은 [등대 세부] 판을 접는다 (판이 그 창들 위에 그려지고 입력도 막히지 않아서)
        sealed class DetailGuard : MonoBehaviour
        {
            PrepYard yard;
            void Update()
            {
                if (yard == null) yard = UnityEngine.Object.FindAnyObjectByType<PrepYard>();
                if (yard != null && !yard.CanMove) gameObject.SetActive(false);
            }
        }
        sealed class MapButtonGate: MonoBehaviour
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
        static Button BuildSummary(RectTransform area, HumanLastRampart.Core.Campaign.CampaignState campaign, Action openMap, Action toggleDetail, out Text brief)
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
            brief = Ui.Label(row.transform, "Beacon Brief", "", UiTokens.Small, UiTokens.Body, TextAnchor.MiddleRight);   // W36: 등대 복구 % + 점화 준비 한 줄
            Ui.Size(brief, 432f, 48f);
            Button detailButton = Ui.Button(row.transform, "등대 세부", "등대 세부 (B)", toggleDetail);
            Ui.Size(detailButton, 168f, 48f);
            detailButton.gameObject.AddComponent<DetailKey>().Init(toggleDetail);
            Image gold = Ui.HudIcon(row.transform, "Gold Icon", "Icons/stat_gold", 32f);
            if (gold == null) Ui.Size(Ui.Label(row.transform, "Gold Word", "골드", UiTokens.Small, UiTokens.BodyDim), 44f, 48f);
            Text goldValue = Ui.Label(row.transform, "Gold Value", "", UiTokens.H2, UiTokens.Body, TextAnchor.MiddleLeft, true);
            Ui.Size(goldValue, 128f, 48f);
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
