using System;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 지도 화면의 등불 밝기 단추와 등대 빛줄기 안내 (Concept_Beacon_v1 9.4, 임시 모양 — 최종 그림은 K-106). 오른쪽 포병·마법판 아래에 붙는다.
    // W9: 빛줄기 방향 단추는 이 판에 없다 — 방향은 주인공이 계단으로 내려가 등대 탑 일꾼에게 명령할 때만 정한다 (BeamOrderPanel).
    // 지도는 언제나 지금 비추는 방향과 도는 중인 목표 방향을 글로도 보여 준다 (부채꼴 그림은 BattleBeamView)
    public sealed partial class BattleHud
    {
        public event Action<LampBrightness> LampChosen;   // 밝기 (전투 중 바꿔도 적 수는 그대로)
        public event Action<int> BeamChosen;              // 빛줄기 방향 (0 북 · 1 동 · 2 남 · 3 서). 탑 명령 판에서만 나온다. 도는 데 BattleSim.BeamRotateSeconds
        public event Action TowerLeaveChosen;             // 명령 판의 [성벽으로 올라가기]
        public event Action BeamOrderClosed;              // 명령 판의 [닫기]
        readonly Button[] lampButtons = new Button[3];
        Text beamStatus;
        int statusKey = -1;
        string statusText, statusPanelText;
        static readonly LampBrightness[] LampOrder = { LampBrightness.Low, LampBrightness.Normal, LampBrightness.High };
        static readonly string[] LampLabels = { "약", "보통", "강" };

        void BuildLampSection(Transform parent)
        {
            VerticalLayoutGroup section = Ui.Column(parent, "Lamp", UiTokens.S8);
            Image line = Ui.Image(section.transform, "Rule", UiTokens.BrassDark);
            Ui.Size(line, -1f, UiTokens.Dot);
            Text heading = Ui.Label(section.transform, "Heading", "등대 등불", UiTokens.Text, UiTokens.Brass, TextAnchor.MiddleLeft, true);
            Ui.Size(heading, -1f, 32f);

            HorizontalLayoutGroup bright = Ui.Row(section.transform, "Brightness", UiTokens.S8);
            for (int i = 0; i < LampOrder.Length; i++)
            {
                LampBrightness value = LampOrder[i];
                lampButtons[i] = Ui.Button(bright.transform, "등불 " + LampLabels[i], LampLabels[i], () => LampChosen?.Invoke(value), UiButtonKind.Tab);
                Ui.Size(lampButtons[i], 96f, 48f);
            }

            beamStatus = Ui.Label(section.transform, "Beam Status", "", UiTokens.Small, UiTokens.BodyDim);
            Ui.Size(beamStatus, -1f, 56f);
            beamStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // 지금 밝기·빛줄기 방향 표시 (지도가 열려 있을 때)
        public void RefreshLamp(BattleSim sim)
        {
            if (sim == null || lampButtons[0] == null) return;
            for (int i = 0; i < lampButtons.Length; i++) Ui.SetOn(lampButtons[i], LampOrder[i] == sim.Lamp);
            // 글은 바뀔 때만 새로 만든다 (매 프레임 문자열 할당 방지)
            int nowSide = NearestSide(sim.BeamAngleDegrees);
            int leftSeconds = sim.BeamRotating ? Mathf.CeilToInt(sim.BeamSecondsLeft) : 0;
            int key = (nowSide * 8 + (sim.BeamRotating ? 1 + sim.BeamSide : 0)) * 1000 + leftSeconds;
            if (key != statusKey)
            {
                statusKey = key;
                statusText = BeamStatusText(sim);
                statusPanelText = statusText + "\n방향은 가운데 등대 탑에서 명령 (E)";
            }

            if (beamStatus != null && beamStatus.text != statusPanelText) beamStatus.text = statusPanelText;
            RefreshBeamOrder(sim, statusText);
        }

        // "빛줄기 북쪽" / "빛줄기 북쪽 → 동쪽 (도는 중, 4초 남음)"
        public static string BeamStatusText(BattleSim sim)
        {
            string now = SideFull[NearestSide(sim.BeamAngleDegrees)];
            if (!sim.BeamRotating) return "빛줄기 " + now;
            int left = Mathf.Max(1, Mathf.CeilToInt(sim.BeamSecondsLeft));   // 올림 — 0초 남음이 보이지 않게
            return $"빛줄기 {SideFull[sim.BeamSide]}으로 도는 중 ({left}초 남음)";
        }

        static int NearestSide(float degrees)
        {
            int best = 0;
            float bestDelta = 999f;
            for (int s = 0; s < 4; s++)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(degrees, BattleSim.SideAngle(s)));
                if (d < bestDelta) { bestDelta = d; best = s; }
            }

            return best;
        }

        // ── 탑 명령 판 (주인공이 등대 탑에서 E): 방향 단추 넷 + 성벽으로 올라가기 + 닫기 ──
        GameObject beamOrderBoard;
        Text beamOrderStatus;
        readonly Button[] beamOrderButtons = new Button[4];
        bool beamOrderWanted;

        public bool BeamOrderOpen => beamOrderBoard != null && beamOrderBoard.activeSelf;

        void BuildBeamOrderPanel()
        {
            Image panel = Ui.Panel(root.transform, "Beam Order Board", UiPanel.Wood);
            beamOrderBoard = panel.gameObject;
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, Edge + 56f), new Vector2(640f, 0f), new Vector2(0.5f, 0f));
            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(24, 24, 16, 24);
            column.spacing = UiTokens.S8;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text title = Ui.Label(panel.transform, "Title", "등대 일꾼에게 명령", UiTokens.H2, UiTokens.Brass, TextAnchor.MiddleLeft, true);
            Ui.Size(title, -1f, 48f);
            beamOrderStatus = Ui.Label(panel.transform, "Status", "", UiTokens.Small, UiTokens.Body);
            Ui.Size(beamOrderStatus, -1f, 32f);
            HorizontalLayoutGroup dir = Ui.Row(panel.transform, "Direction", UiTokens.S8);
            Ui.Size(dir, -1f, 56f);
            for (int side = 0; side < 4; side++)
            {
                int s = side;
                beamOrderButtons[side] = Ui.Button(dir.transform, "빛 " + SideNames[side], SideNames[side], () => BeamChosen?.Invoke(s), UiButtonKind.Tab);
                Ui.Size(beamOrderButtons[side], 120f, 56f);
            }

            HorizontalLayoutGroup row = Ui.Row(panel.transform, "Actions", UiTokens.S8);
            Ui.Size(row, -1f, 48f);
            Button up = Ui.Button(row.transform, "성벽으로 올라가기", "성벽으로 올라가기", () => TowerLeaveChosen?.Invoke());
            Ui.Size(up, 280f, 48f);
            Button close = Ui.Button(row.transform, "명령 닫기", "닫기", () => BeamOrderClosed?.Invoke());
            Ui.Size(close, 160f, 48f);
            beamOrderBoard.SetActive(false);
        }

        // 지도가 열려 있고 명령 중일 때만 보인다 (BattleEntry가 지도 열기·닫기와 함께 부른다)
        public void SetBeamOrder(bool wanted)
        {
            beamOrderWanted = wanted;
            if (beamOrderBoard == null) BuildBeamOrderPanel();
            UiAppear.Toggle(beamOrderBoard, wanted, new Vector2(0f, -UiTokens.S24));
        }

        void RefreshBeamOrder(BattleSim sim, string status)
        {
            if (beamOrderBoard == null || !beamOrderBoard.activeSelf) return;
            if (beamOrderStatus.text != status) beamOrderStatus.text = status;
            for (int side = 0; side < 4; side++) Ui.SetOn(beamOrderButtons[side], side == sim.BeamSide);
        }

        // ── 아래 가운데 한 줄 안내 (E 계단·탑 상호작용). key가 null이면 숨김 ──
        RectTransform towerPrompt;
        string towerPromptKey, towerPromptText;

        public void SetTowerPrompt(string key, string text)
        {
            if (key == towerPromptKey && text == towerPromptText) return;
            towerPromptKey = key;
            towerPromptText = text;
            if (towerPrompt != null) UnityEngine.Object.Destroy(towerPrompt.gameObject);
            towerPrompt = null;
            if (key == null) return;
            towerPrompt = Ui.KeyHint(root.transform, key, text);
            Ui.Place(towerPrompt, new Vector2(0.5f, 0f), new Vector2(0f, Edge + 8f), new Vector2(520f, 40f), new Vector2(0.5f, 0f));   // N6: 글 앞뒤 공백 없음
            towerPrompt.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
