using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 우두머리 표시 (성벽손 오우거, BattleSim.Boss.cs): 위쪽 체력 막대(이름 + 막대 + 상태 한 줄)와 지도의 큰 표식·가려는 성벽 자리.
    // 막대는 K-123 막대 그림(Ui.Bar). 우두머리가 나와 살아 있는 동안(처치 뒤 3초까지)만 보인다
    public sealed partial class BattleHud
    {
        const float BossBarWidth = 640f;

        RectTransform bossRoot;
        Text bossName, bossState, bossNumbers;
        UiBar bossBar;
        string bossStateShown;

        // 위쪽 가운데, 단계 띠(64) 바로 아래. Refresh가 부른다
        void RefreshBossBar(BattleSim sim)
        {
            bool show = sim.BossSpawned && (sim.BossActive || (sim.BossKilled && sim.Elapsed - sim.BossKilledAt < 3f));
            if (!show)
            {
                if (bossRoot != null && bossRoot.gameObject.activeSelf) bossRoot.gameObject.SetActive(false);
                return;
            }

            if (bossRoot == null) BuildBossBar();
            if (!bossRoot.gameObject.activeSelf) bossRoot.gameObject.SetActive(true);
            int max = Mathf.Max(1, sim.BossMaxHp), hp = Mathf.Clamp(sim.BossHp, 0, max);
            bossBar.Set((float)hp / max);
            bossNumbers.text = $"{hp:N0} / {max:N0}";
            string state = !sim.BossActive ? "쓰러뜨림"
                : sim.BossDownLeft > 0f ? $"쓰러짐 {Mathf.CeilToInt(sim.BossDownLeft)}초"
                : sim.BossOnWalkway ? "통로 위 — 병사를 쓰러뜨리는 중!"
                : sim.BossClimbing ? "성벽을 기어오르는 중"
                : sim.BossLit ? "빛에 드러남 — 빨라짐" : "쇠장갑과 안개 — 화살이 잘 안 듣는다";
            if (state != bossStateShown)
            {
                bossStateShown = state;
                bossState.text = state;
            }
        }

        void BuildBossBar()
        {
            bossRoot = Ui.Rect(root.transform, "Boss Bar");
            Ui.Place(bossRoot, new Vector2(0.5f, 1f), new Vector2(0f, -(Edge + 64f + UiTokens.S8)), new Vector2(BossBarWidth, 76f), new Vector2(0.5f, 1f));
            bossName = Ui.Outlined(Ui.Label(bossRoot, "Boss Name", "성벽손 오우거 · 우두머리", UiTokens.Text, UiTokens.DangerLight, TextAnchor.MiddleLeft, true));
            bossName.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Place(bossName.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(360f, 32f), new Vector2(0f, 1f));
            bossState = Ui.Outlined(Ui.Label(bossRoot, "Boss State", "", UiTokens.Small, UiTokens.Body, TextAnchor.MiddleRight));
            bossState.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Place(bossState.rectTransform, new Vector2(1f, 1f), Vector2.zero, new Vector2(BossBarWidth - 370f, 32f), new Vector2(1f, 1f));
            bossBar = Ui.Bar(bossRoot, "Boss HP", BossBarWidth, 24f, false);
            bossBar.FixedColor = UiTokens.Danger;
            Ui.Place((RectTransform)bossBar.transform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(BossBarWidth, 24f), new Vector2(0.5f, 0f));
            bossNumbers = Ui.Outlined(Ui.Label(bossRoot, "Boss Numbers", "", UiTokens.Small, UiTokens.Body, TextAnchor.MiddleCenter));
            bossNumbers.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Place(bossNumbers.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(BossBarWidth, 24f), new Vector2(0.5f, 0f));
            bossRoot.gameObject.SetActive(false);
        }

        // 지도: 우두머리 큰 표식(발 위)과 가려는 성벽 자리 표식. 지도가 닫혀 있거나 우두머리가 없으면 숨김. RefreshGroupLabels 끝에서 부른다
        RectTransform bossMapMarker, bossTargetMarker;
        Image bossMapIcon, bossTargetIcon;

        void RefreshBossMap(BattleSim sim, Camera camera)
        {
            bool show = mapMode && sim.BossActive;
            if (!show)
            {
                if (bossMapMarker != null && bossMapMarker.gameObject.activeSelf) bossMapMarker.gameObject.SetActive(false);
                if (bossTargetMarker != null && bossTargetMarker.gameObject.activeSelf) bossTargetMarker.gameObject.SetActive(false);
                return;
            }

            if (bossMapMarker == null) BuildBossMapMarkers();
            Vector3 boss = sim.BossPosition;
            Vector3 screen = camera.WorldToScreenPoint(new Vector3(boss.x, 0f, boss.z));
            bossMapMarker.gameObject.SetActive(screen.z > 0f);
            bossMapMarker.anchorMin = bossMapMarker.anchorMax = Viewport(screen);
            bossMapMarker.anchoredPosition = new Vector2(0f, UiTokens.S24);
            bossMapIcon.color = sim.BossDownLeft > 0f ? UiTokens.BodyDim : sim.BossClimbing || sim.BossOnWalkway ? UiTokens.DangerLight : UiTokens.Danger;

            Vector3 target = camera.WorldToScreenPoint(sim.BossTargetPoint);
            bossTargetMarker.gameObject.SetActive(target.z > 0f);
            bossTargetMarker.anchorMin = bossTargetMarker.anchorMax = Viewport(target);
            bossTargetMarker.anchoredPosition = new Vector2(0f, UiTokens.S16);
            bossTargetIcon.color = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f ? UiTokens.Danger : UiTokens.DangerLight;
        }

        void BuildBossMapMarkers()
        {
            // 판들보다 뒤 — 큰 표식 (그림 점 ×4) + "우두머리" 글
            bossMapMarker = Ui.Rect(root.transform, "Boss Map Marker");
            bossMapMarker.pivot = new Vector2(0.5f, 0f);
            bossMapMarker.sizeDelta = new Vector2(160f, 96f);
            bossMapIcon = Ui.Icon(bossMapMarker, "Boss Icon", UiArt.IconSpecial, 4f, UiTokens.Danger);
            Ui.Place(bossMapIcon.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, bossMapIcon.rectTransform.sizeDelta, new Vector2(0.5f, 0f));
            Text name = Ui.Outlined(Ui.Label(bossMapMarker, "Boss Label", "우두머리", UiTokens.Small, UiTokens.DangerLight, TextAnchor.MiddleCenter, true));
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Place(name.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(160f, 28f), new Vector2(0.5f, 1f));
            bossMapMarker.SetAsFirstSibling();

            bossTargetMarker = Ui.Rect(root.transform, "Boss Target Marker");
            bossTargetMarker.pivot = new Vector2(0.5f, 0f);
            bossTargetMarker.sizeDelta = new Vector2(160f, 80f);
            bossTargetIcon = Ui.Icon(bossTargetMarker, "Target Icon", UiArt.IconFlag, 3f, UiTokens.Danger);
            Ui.Place(bossTargetIcon.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, bossTargetIcon.rectTransform.sizeDelta, new Vector2(0.5f, 0f));
            Text target = Ui.Outlined(Ui.Label(bossTargetMarker, "Target Label", "노리는 자리", UiTokens.Small, UiTokens.DangerLight, TextAnchor.MiddleCenter));
            target.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Place(target.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(160f, 28f), new Vector2(0.5f, 1f));
            bossTargetMarker.SetAsFirstSibling();
            bossMapMarker.gameObject.SetActive(false);
            bossTargetMarker.gameObject.SetActive(false);
        }
    }
}
