using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 우두머리 표시 (성벽손 오우거, BattleSim.Boss.cs): 위쪽 체력 막대(이름 + 막대 + 상태 한 줄)와 지도의 큰 표식·가려는 성벽 자리.
    // 막대는 K-123 막대 그림(Ui.Bar). 우두머리가 나와 살아 있는 동안(처치 뒤 3초까지)만 보인다
    public sealed partial class BattleHud
    {
        const float BossBarWidth = 520f;   // 좁게 (위쪽 가운데 멀리 있는 우두머리를 덜 가린다)

        RectTransform bossRoot;
        Text bossName, bossState, bossNumbers;
        UiBar bossBar;
        int bossHpShown = -1, bossMaxShown = -1, bossStateCode = -1;

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
            if (hp != bossHpShown || max != bossMaxShown)   // 같은 값이면 글자를 다시 만들지 않는다 (문자열·글자 메쉬)
            {
                bossHpShown = hp;
                bossMaxShown = max;
                bossNumbers.text = $"{hp:N0} / {max:N0}";
            }

            int downSeconds = sim.BossActive && sim.BossDownLeft > 0f ? Mathf.CeilToInt(sim.BossDownLeft) : 0;
            // 상태 번호: 0 쓰러뜨림 · 1 쓰러짐(초는 따로) · 2 통로 위 · 3 오르는 중 · 4 빛 · 5 빛 밖
            int stateKey = !sim.BossActive ? 0 : downSeconds > 0 ? 1 : sim.BossOnWalkway ? 2 : sim.BossClimbing ? 3 : sim.BossLit ? 4 : 5;
            int stateCode = stateKey * 1000 + downSeconds;
            if (stateCode != bossStateCode)
            {
                bossStateCode = stateCode;
                bossState.text = stateKey == 0 ? "쓰러뜨림"
                    : stateKey == 1 ? $"쓰러짐 {downSeconds}초"
                    : stateKey == 2 ? "통로 위 — 부수는 중!"
                    : stateKey == 3 ? "기어오르는 중"
                    : stateKey == 4 ? "빛에 드러남 · 빨라짐" : "화살이 잘 안 듣는다";
            }
        }

        void BuildBossBar()
        {
            // K-129 체력 막대용 초상 (96×96, 글자 없음). 없으면 막대만
            bossRoot = Ui.Rect(root.transform, "Boss Bar");
            Image portrait = Ui.HudIcon(bossRoot, "Boss Portrait", "Boss/wallhand_hud_portrait", 96f);
            float left = portrait != null ? 104f : 0f;
            float width = BossBarWidth - left;
            Ui.Place(bossRoot, new Vector2(0.5f, 1f), new Vector2(0f, -(Edge + 64f + UiTokens.S8)), new Vector2(BossBarWidth, portrait != null ? 96f : 76f), new Vector2(0.5f, 1f));
            if (portrait != null) Ui.Place(portrait.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, portrait.rectTransform.sizeDelta, new Vector2(0f, 0.5f));
            bossName = Ui.Outlined(Ui.Label(bossRoot, "Boss Name", "성벽손 오우거",UiTokens.Text, UiTokens.DangerLight, TextAnchor.MiddleLeft, true));
            bossName.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Place(bossName.rectTransform, new Vector2(0f, 1f), new Vector2(left, 0f), new Vector2(width * 0.55f, 32f), new Vector2(0f, 1f));
            bossState = Ui.Outlined(Ui.Label(bossRoot, "Boss State", "", UiTokens.Small, UiTokens.Body, TextAnchor.MiddleRight));
            bossState.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Place(bossState.rectTransform, new Vector2(1f, 1f), Vector2.zero, new Vector2(width * 0.45f, 32f), new Vector2(1f, 1f));
            bossBar = Ui.Bar(bossRoot, "Boss HP", width, 24f, false);
            bossBar.FixedColor = UiTokens.Danger;
            Ui.Place((RectTransform)bossBar.transform, new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(width, 24f), new Vector2(1f, 0.5f));
            bossNumbers = Ui.Outlined(Ui.Label(bossRoot, "Boss Numbers", "", UiTokens.Small, UiTokens.Body, TextAnchor.MiddleCenter));
            bossNumbers.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Place(bossNumbers.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(width, 24f), new Vector2(1f, 0.5f));
            bossRoot.gameObject.SetActive(false);
        }

        // 지도: 우두머리 큰 표식(발 위)과 가려는 성벽 자리 표식. 지도가 닫혀 있거나 우두머리가 없으면 숨김. RefreshGroupLabels 끝에서 부른다
        RectTransform bossMapMarker, bossTargetMarker;
        Image bossMapIcon, bossTargetIcon;
        bool bossMapIconArt;

        // 시험용: 글·띠를 다음 프레임에 바로 다시 그린다 (사진 직전)
        public void ForceRefresh() => nextRefresh = 0f;

        void HideBossMap()
        {
            if (bossMapMarker != null && bossMapMarker.gameObject.activeSelf) bossMapMarker.gameObject.SetActive(false);
            if (bossTargetMarker != null && bossTargetMarker.gameObject.activeSelf) bossTargetMarker.gameObject.SetActive(false);
        }

        // 시험용: 지도 표식이 지금 보이는지
        public bool BossMapMarkersVisible => (bossMapMarker != null && bossMapMarker.gameObject.activeSelf) || (bossTargetMarker != null && bossTargetMarker.gameObject.activeSelf);

        void RefreshBossMap(BattleSim sim, Camera camera)
        {
            bool show = mapMode && sim.BossActive;
            if (!show)
            {
                HideBossMap();
                return;
            }

            if (bossMapMarker == null) BuildBossMapMarkers();
            Vector3 boss = sim.BossPosition;
            Vector3 screen = camera.WorldToScreenPoint(new Vector3(boss.x, 0f, boss.z));
            bossMapMarker.gameObject.SetActive(screen.z > 0f);
            bossMapMarker.anchorMin = bossMapMarker.anchorMax = Viewport(screen);
            bossMapMarker.anchoredPosition = new Vector2(0f, UiTokens.S24);
            Color bossColor = sim.BossDownLeft > 0f ? UiTokens.BodyDim : sim.BossClimbing || sim.BossOnWalkway ? UiTokens.DangerLight : UiTokens.Danger;
            bossMapIcon.color = bossMapIconArt ? (sim.BossDownLeft > 0f ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white) : bossColor;   // 그림은 제 색 그대로, 표식 대용만 칠한다

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
            // K-129 지도 아이콘 (64×64, 위에서 내려본 모습). 없으면 특수 적 표식. 일반 무리 점보다 훨씬 크게(×2 = 128px)
            bossMapIcon = Ui.HudIcon(bossMapMarker, "Boss Icon", "Boss/wallhand_map_icon", 128f);
            if (bossMapIcon == null) bossMapIcon = Ui.Icon(bossMapMarker, "Boss Icon", UiArt.IconSpecial, 4f, UiTokens.Danger);
            bossMapIconArt = UiArt.Hud("Boss/wallhand_map_icon") != null;
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
