using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 2번 지도 모드 표시 (BattleMap.md 2장): 위쪽 가운데 띠에 지도 상태·쉬움 시간 정지
    public sealed partial class BattleHud
    {
        Text mapBanner;
        bool mapMode;

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
            if (open)
            {
                mapBanner.text = frozen ? "지도 — 시간 멈춤 (쉬움)" : "지도 — 시간이 흐릅니다 (현실)";
            }
        }
    }
}