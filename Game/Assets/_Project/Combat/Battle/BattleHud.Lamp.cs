using System;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 지도 화면의 등불·빛줄기 단추 (Concept_Beacon_v1 9.4, 임시 모양 — 최종 그림은 K-106). 오른쪽 포병·마법판 아래에 붙는다
    public sealed partial class BattleHud
    {
        public event Action<LampBrightness> LampChosen;   // 밝기 (전투 중 바꿔도 적 수는 그대로)
        public event Action<int> BeamChosen;              // 빛줄기 방향 (0 북 · 1 동 · 2 남 · 3 서). 도는 데 3초
        readonly Button[] lampButtons = new Button[3];
        readonly Button[] beamButtons = new Button[4];
        static readonly LampBrightness[] LampOrder = { LampBrightness.Low, LampBrightness.Normal, LampBrightness.High };
        static readonly string[] LampLabels = { "약", "보통", "강" };

        void BuildLampSection(Transform parent)
        {
            VerticalLayoutGroup section = Ui.Column(parent, "Lamp", UiTokens.S8);
            Image line = Ui.Image(section.transform, "Rule", UiTokens.BrassDark);
            Ui.Size(line, -1f, UiTokens.Dot);
            Text heading = Ui.Label(section.transform, "Heading", "등대 빛줄기", UiTokens.Text, UiTokens.Brass, TextAnchor.MiddleLeft, true);
            Ui.Size(heading, -1f, 32f);

            HorizontalLayoutGroup bright = Ui.Row(section.transform, "Brightness", UiTokens.S8);
            for (int i = 0; i < LampOrder.Length; i++)
            {
                LampBrightness value = LampOrder[i];
                lampButtons[i] = Ui.Button(bright.transform, "등불 " + LampLabels[i], LampLabels[i], () => LampChosen?.Invoke(value), UiButtonKind.Tab);
                Ui.Size(lampButtons[i], 96f, 48f);
            }

            HorizontalLayoutGroup dir = Ui.Row(section.transform, "Direction", UiTokens.S8);
            for (int side = 0; side < 4; side++)
            {
                int s = side;
                beamButtons[side] = Ui.Button(dir.transform, "빛 " + SideNames[side], SideNames[side], () => BeamChosen?.Invoke(s), UiButtonKind.Tab);
                Ui.Size(beamButtons[side], 72f, 48f);
            }
        }

        // 지금 밝기·방향 표시 (지도가 열려 있을 때)
        public void RefreshLamp(BattleSim sim)
        {
            if (sim == null || lampButtons[0] == null) return;
            for (int i = 0; i < lampButtons.Length; i++) Ui.SetOn(lampButtons[i], LampOrder[i] == sim.Lamp);
            for (int side = 0; side < beamButtons.Length; side++) Ui.SetOn(beamButtons[side], side == sim.BeamSide);
        }
    }
}