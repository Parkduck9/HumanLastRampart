using System;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    // 정비 마당 가운데 등대 (W9, Concept_Beacon_v1 9.7 4차 결정): 낮에 등대 일꾼에게 다음 밤 처음 빛줄기 방향을 명령하고, 등대 수리도 여기서 한다.
    // 방향은 CampaignState.PlannedBeamSide에 저장 → BattleSetupFactory가 BattleSetup.BeamSide로. 위쪽 등대 줄(BeaconPrepPanel)은 그대로 둔다
    public static class BeaconYardPanel
    {
        public static GameObject Build(Transform parent, IScreenContext context, Action refreshGold, Action close)
        {
            CampaignState campaign = context.Campaign;
            Image shade = Ui.Shade(parent, "Beacon Yard");
            Image box = Ui.Panel(shade.transform, "Beacon Order", UiPanel.Parchment);
            Ui.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 0f));
            var column = box.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(48, 48, 32, 32);
            column.spacing = UiTokens.S16;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text title = Ui.Label(box.transform, "Title", "등대", UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Size(title, -1f, 48f);
            Text info = Ui.Label(box.transform, "Info", "", UiTokens.Text, UiTokens.InkSoft, TextAnchor.MiddleLeft);
            Ui.Size(info, -1f, 40f);
            Text ask = Ui.Label(box.transform, "Ask", "일꾼에게 명령: 다음 밤 처음 빛줄기 방향", UiTokens.Small, UiTokens.InkSoft, TextAnchor.MiddleLeft);
            Ui.Size(ask, -1f, 32f);

            Button repair = null;
            var sides = new Button[4];
            string[] names = { "북", "동", "남", "서" };
            HorizontalLayoutGroup dir = Ui.Row(box.transform, "Direction", UiTokens.S8);
            Ui.Size(dir, -1f, 56f);
            for (int i = 0; i < 4; i++)
            {
                int side = i;
                sides[i] = Ui.Button(dir.transform, "빛 " + names[i], names[i], () => { campaign.PlannedBeamSide = side; Refresh(); }, UiButtonKind.Tab);
                Ui.Size(sides[i], 120f, 56f);
            }

            repair = Ui.Button(box.transform, "등대 수리 (마당)", $"등대 수리 +{BeaconRules.DayRepairAmount:0} ({BeaconRules.DayRepairGold:N0}골드)", () =>
            {
                BeaconRules.TryBuyRepair(campaign, out _);
                refreshGold?.Invoke();
                Refresh();
            });
            Ui.Size(repair, -1f, 56f);
            Button closeButton = Ui.Button(box.transform, "등대 닫기", "닫기", () => close?.Invoke(), UiButtonKind.Primary);
            Ui.Size(closeButton, -1f, 56f);

            void Refresh()
            {
                info.text = $"{BeaconRules.NightLabel(campaign.FortressNumber, campaign.NightNumber, true)} · 복구 {Mathf.FloorToInt(campaign.BeaconRepair)}% · 골드 {campaign.Gold:N0}";
                int planned = ((campaign.PlannedBeamSide % 4) + 4) % 4;
                for (int i = 0; i < 4; i++) Ui.SetOn(sides[i], i == planned);
                repair.interactable = campaign.BeaconRepair < BeaconRules.RepairMax && campaign.Gold >= BeaconRules.DayRepairGold;
            }

            Refresh();
            return shade.gameObject;
        }
    }
}
