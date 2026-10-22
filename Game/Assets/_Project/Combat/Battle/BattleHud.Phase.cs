using HumanLastRampart.Core.Definitions;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 단계 예고 띠 (나안, Parallel_Wave 6장): 단계가 바뀌기 5초 전 위쪽에 글만 띄운다. 그림은 카드 K-100이 오면 붙인다
    public sealed partial class BattleHud
    {
        Text phaseBanner;

        // Refresh가 부른다. 글은 처음 필요할 때 만든다
        void RefreshPhaseBanner(BattleSim sim)
        {
            string line = PhaseBannerText(sim);
            if (line == null)
            {
                if (phaseBanner != null && phaseBanner.gameObject.activeSelf) phaseBanner.gameObject.SetActive(false);
                return;
            }

            if (phaseBanner == null)
            {
                phaseBanner = Ui.Outlined(Ui.Label(root.transform, "Phase Banner", "", UiTokens.H2, UiTokens.Brass, TextAnchor.MiddleCenter, true));
                Ui.Place(phaseBanner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -(Edge + BandHeight + UiTokens.S16)), new Vector2(760f, 64f), new Vector2(0.5f, 1f));
            }

            if (!phaseBanner.gameObject.activeSelf) phaseBanner.gameObject.SetActive(true);
            phaseBanner.text = line;
        }

        static string PhaseBannerText(BattleSim sim)
        {
            if (sim.FinalAssaultStarted)
                return sim.Elapsed - sim.FinalAssaultAt < BattleSim.FinalBannerSeconds ? "최후 돌격!" : null;
            if (!sim.TryGetPhaseWarning(out BattleSim.PhaseWarning warning)) return null;
            string side = SideFull[Mathf.Clamp(warning.Side, 0, 3)];
            int seconds = Mathf.Max(1, Mathf.CeilToInt(warning.SecondsLeft));
            return warning.Phase == WavePhase.Siege
                ? $"공성 부대 접근 — {side} ({seconds}초)"
                : $"본대가 몰려옵니다 — {side}";
        }
    }
}
