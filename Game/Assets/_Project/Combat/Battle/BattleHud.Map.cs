using System;
using HumanLastRampart.Core.Campaign;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 2번 지도 모드 표시 (BattleMap.md 2장): 위쪽 가운데 띠에 지도 상태·쉬움 시간 정지
    public sealed partial class BattleHud
    {
        Text mapBanner;
        bool mapMode;
        GameObject globalPanel;
        Text globalStatus;

        // 지도 전체 명령 (MAP-2, Battle.md 5.1-1): 4면에 한 번에. 성벽 명령은 지워진다 (D-031 K3, D-039)
        public event Action<TargetPriority> GlobalCommandChosen;

        public void SetMapMode(bool open, bool frozen)
        {
            if (mapBanner == null)
            {
                Image band = NewImage(root.transform, "Map Banner", new Color(0f, 0f, 0f, 0.6f));
                Anchor(band.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(620f, 56f), new Vector2(0.5f, 1f));
                mapBanner = NewText(band.transform, "Text", 26, TextAnchor.MiddleCenter);
                Anchor(mapBanner.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 50f), new Vector2(0.5f, 0.5f));
            }

            if (open != mapMode)
            {
                nextRefresh = 0f;   // 안내 문구 바로 바꾸기
            }

            mapMode = open;
            mapBanner.transform.parent.gameObject.SetActive(open);
            if (globalPanel == null) BuildGlobalPanel();
            globalPanel.SetActive(open);
            if (open)
            {
                mapBanner.text = frozen ? "지도 — 시간 멈춤 (쉬움)" : "지도 — 시간이 흐릅니다 (현실)";
            }
        }

        void BuildGlobalPanel()
        {
            Image panel = NewImage(root.transform, "Global Command", new Color(0.05f, 0.07f, 0.1f, 0.85f));
            globalPanel = panel.gameObject;
            Anchor(panel.rectTransform, new Vector2(0f, 0.5f), new Vector2(20f, -60f), new Vector2(330f, 560f), new Vector2(0f, 0.5f));
            Text title = NewText(panel.transform, "Title", 26, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(310f, 40f), new Vector2(0.5f, 1f));
            title.text = "전체 명령 (4면)";
            for (int i = 0; i < BattleSim.PriorityNames.Length; i++)
            {
                var priority = (TargetPriority)i;
                Button button = NewButton(panel.transform, BattleSim.PriorityNames[i], () => GlobalCommandChosen?.Invoke(priority));
                Anchor((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, -60f - i * 62f), new Vector2(300f, 54f), new Vector2(0.5f, 1f));
            }

            globalStatus = NewText(panel.transform, "Status", 18, TextAnchor.UpperLeft);
            Anchor(globalStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(300f, 110f), new Vector2(0.5f, 0f));
            globalPanel.SetActive(false);
        }

        // 지금 성벽별 명령 (패널 아래)
        void RefreshGlobalStatus(BattleSim sim)
        {
            if (globalStatus == null || !mapMode) return;
            string[] names = { "북", "동", "남", "서" };
            var lines = new string[4];
            for (int side = 0; side < 4; side++)
            {
                lines[side] = $"{names[side]}: {BattleSim.PriorityNames[(int)sim.PriorityOf(side)]}";
            }

            globalStatus.text = string.Join("\n", lines);
        }
    }
}