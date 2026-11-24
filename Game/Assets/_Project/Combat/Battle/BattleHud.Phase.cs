using System.Collections.Generic;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 단계 예고 띠 (나안, Parallel_Wave 6장): 단계가 바뀌기 5초 전 위쪽에 띠 + 글을 띄운다.
    // K-100 그림: 9-slice 띠(왼쪽 상징 구역 40점) · 단계 상징 · 오는 쪽 화면 가장자리의 방향 경고 표식(맥동 4칸)
    public sealed partial class BattleHud
    {
        Text phaseBanner;
        RectTransform phaseBannerRoot;
        Image phaseBand, phaseEmblem, phaseWarningIcon;

        const float PhaseArtScale = 2f;                                     // 그림 한 점 = 화면 2px (띠 높이 32점 → 64px)
        static readonly Vector4 PhaseBandBorder = new Vector4(10f, 8f, 10f, 8f);   // 좌·아래·우·위 (K-100)
        const float PhaseEmblemZone = 40f * PhaseArtScale;                  // 띠 왼쪽 상징 구역 폭
        const float PhasePulseSeconds = 0.25f;                              // 맥동 한 칸 (K-100: 250ms)

        // 방향 표식 캐시 (단계·면이 같으면 이름 문자열·위치를 다시 만들지 않는다)
        int warningSlotKey = -1;
        string warningSlot, warningSlotPulse;
        Sprite warningStatic;
        bool warningPlaced;

        static readonly Dictionary<string, Sprite[]> phasePulseFrames = new Dictionary<string, Sprite[]>();
        static readonly HashSet<string> phasePulseMissing = new HashSet<string>();

        // Refresh가 부른다. 글은 처음 필요할 때 만든다
        void RefreshPhaseBanner(BattleSim sim)
        {
            string line = PhaseBannerText(sim, out WavePhase phase, out int side, out bool warning);
            if (line == null)
            {
                if (phaseBannerRoot != null && phaseBannerRoot.gameObject.activeSelf) phaseBannerRoot.gameObject.SetActive(false);
                if (phaseWarningIcon != null && phaseWarningIcon.gameObject.activeSelf) phaseWarningIcon.gameObject.SetActive(false);
                return;
            }

            if (phaseBanner == null) BuildPhaseBanner();

            if (!phaseBannerRoot.gameObject.activeSelf) phaseBannerRoot.gameObject.SetActive(true);
            // 글·띠 폭·상징은 글이 바뀔 때(단계·면·남은 초가 바뀔 때)만 다시 맞춘다 — 매 프레임 문자열을 만들지 않는다
            if (!ReferenceEquals(line, shownPhaseLine))
            {
                shownPhaseLine = line;
                phaseBanner.text = line;

                // 띠: 글 길이만큼 늘린다 (최소 96×32점). 그림이 없으면 글만
                if (phaseBand.enabled)
                {
                    float width = Mathf.Max(96f * PhaseArtScale, PhaseEmblemZone + phaseBanner.preferredWidth + 10f * PhaseArtScale + 16f);
                    phaseBannerRoot.sizeDelta = new Vector2(width, 32f * PhaseArtScale);
                }

                // 단계 상징 (왼쪽)
                Sprite emblem = UiArt.Hud("Phase/emblem_" + PhaseKey(phase));
                phaseEmblem.sprite = emblem;
                phaseEmblem.enabled = emblem != null && phaseBand.enabled;
            }

            RefreshPhaseWarningIcon(phase, side, warning);
        }

        void BuildPhaseBanner()
        {
            var rootRect = Ui.Rect(root.transform, "Phase Banner Band");
            Ui.Place(rootRect, new Vector2(0.5f, 1f), new Vector2(0f, -Edge), new Vector2(760f, 64f), new Vector2(0.5f, 1f));
            phaseBannerRoot = rootRect;

            Sprite band = UiArt.Hud("Phase/phase_banner", PhaseBandBorder);
            phaseBand = Ui.Image(rootRect, "Band", Color.white);
            phaseBand.sprite = band;
            phaseBand.type = Image.Type.Sliced;
            phaseBand.pixelsPerUnitMultiplier = 1f / PhaseArtScale;
            phaseBand.enabled = band != null;
            Ui.Stretch(phaseBand.rectTransform);

            phaseEmblem = Ui.Image(rootRect, "Emblem", Color.white);
            phaseEmblem.preserveAspect = true;
            Ui.Place(phaseEmblem.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f * PhaseArtScale, 0f), new Vector2(32f * PhaseArtScale, 32f * PhaseArtScale), new Vector2(0f, 0.5f));
            phaseEmblem.enabled = false;

            phaseBanner = Ui.Outlined(Ui.Label(rootRect, "Phase Banner", "", UiTokens.H2, UiTokens.Brass, TextAnchor.MiddleCenter, true));
            phaseBanner.horizontalOverflow = HorizontalWrapMode.Overflow;
            // 글은 상징 구역 오른쪽, 띠가 없을 때는 옛 자리(가운데)와 같다
            RectTransform t = phaseBanner.rectTransform;
            t.anchorMin = Vector2.zero;
            t.anchorMax = Vector2.one;
            t.offsetMin = new Vector2(band != null ? PhaseEmblemZone : 0f, 0f);
            t.offsetMax = new Vector2(band != null ? -10f * PhaseArtScale : 0f, 0f);

            if (band != null) phaseBannerRoot.sizeDelta = new Vector2(96f * PhaseArtScale, 32f * PhaseArtScale);
        }

        // 오는 쪽 화면 가장자리의 방향 표식. 최후 돌격에는 방향이 없어 숨긴다
        void RefreshPhaseWarningIcon(WavePhase phase, int side, bool warning)
        {
            if (!warning)
            {
                if (phaseWarningIcon != null && phaseWarningIcon.gameObject.activeSelf) phaseWarningIcon.gameObject.SetActive(false);
                return;
            }

            side = Mathf.Clamp(side, 0, 3);
            int iconKey = (int)phase * 4 + side;
            if (iconKey != warningSlotKey)
            {
                warningSlotKey = iconKey;
                warningSlot = $"Phase/warning_{PhaseKey(phase)}_{"NESW"[side]}";   // 단계·면이 바뀔 때만 만든다
                warningSlotPulse = warningSlot + "_pulse";
                warningStatic = null;
                warningPlaced = false;
            }

            int frame = (int)(Time.unscaledTime / PhasePulseSeconds) % 4;
            Sprite sprite = PhasePulseFrame(warningSlotPulse, frame);
            if (sprite == null) sprite = warningStatic ?? (warningStatic = UiArt.Hud(warningSlot));
            if (sprite == null)
            {
                if (phaseWarningIcon != null) phaseWarningIcon.gameObject.SetActive(false);
                return;
            }

            if (phaseWarningIcon == null)
            {
                phaseWarningIcon = Ui.Image(root.transform, "Phase Warning Icon", Color.white);
                phaseWarningIcon.preserveAspect = true;
            }

            if (!phaseWarningIcon.gameObject.activeSelf) phaseWarningIcon.gameObject.SetActive(true);
            phaseWarningIcon.sprite = sprite;
            if (warningPlaced) return;   // 자리는 단계·면이 바뀔 때만 다시 잡는다
            warningPlaced = true;

            // 북·동·남·서 = 위·오른쪽·아래·왼쪽 가장자리 (화면 방향과 성벽 방향이 같다고 본다)
            Vector2 size = new Vector2(32f, 32f) * PhaseArtScale;
            float top = Edge + 64f + UiTokens.S8;   // 위쪽은 단계 띠 아래
            RectTransform r = phaseWarningIcon.rectTransform;
            switch (side)
            {
                case 0: Ui.Place(r, new Vector2(0.5f, 1f), new Vector2(0f, -top), size, new Vector2(0.5f, 1f)); break;
                case 1: Ui.Place(r, new Vector2(1f, 0.5f), new Vector2(-Edge, 0f), size, new Vector2(1f, 0.5f)); break;
                case 2: Ui.Place(r, new Vector2(0.5f, 0f), new Vector2(0f, BottomClear), size, new Vector2(0.5f, 0f)); break;
                default: Ui.Place(r, new Vector2(0f, 0.5f), new Vector2(Edge, 0f), size, new Vector2(0f, 0.5f)); break;
            }
        }

        // 시험이 쓴다: 맥동 시트(32×32 4칸)에서 한 칸. 시트가 없으면 null
        public static Sprite PhasePulseFrame(string slot, int frame)
        {
            frame = Mathf.Clamp(frame, 0, 3);
            // 칸 4개를 슬롯별 배열로 둔다 — 이미 만든 칸이나 시트가 없다고 확인된 슬롯은 문자열·불러오기 없이 바로 돌려준다
            if (phasePulseMissing.Contains(slot)) return null;
            if (!phasePulseFrames.TryGetValue(slot, out Sprite[] cells)) phasePulseFrames[slot] = cells = new Sprite[4];
            if (cells[frame] != null) return cells[frame];
            var texture = Resources.Load<Texture2D>("HudArt/" + slot);
            if (texture == null || texture.width < 128) { phasePulseMissing.Add(slot); return null; }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            int cell = texture.width / 4;
            var sprite = Sprite.Create(texture, new Rect(frame * cell, 0f, cell, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = slot + "#" + frame;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            cells[frame] = sprite;
            return sprite;
        }

        // 그림 이름의 단계 낱말 (경고 표식은 선봉·본대·공성 셋, 자동은 본대로)
        public static string PhaseKey(WavePhase phase) =>
            phase == WavePhase.Vanguard ? "vanguard" : phase == WavePhase.Siege ? "siege" : phase == WavePhase.Final ? "final" : "main";

        // 띠에 띄울 글 (없으면 null). 방향 표식은 예고 중일 때만 (최후 돌격은 방향 없음)
        // 같은 (단계, 면, 남은 초)면 지난번 문자열 그대로 돌려준다 (매 프레임 새 문자열을 만들지 않는다)
        string cachedPhaseLine;
        int cachedPhaseLineKey = -1;
        string shownPhaseLine;

        string PhaseBannerText(BattleSim sim, out WavePhase phase, out int side, out bool warning)
        {
            phase = WavePhase.Final;
            side = 0;
            warning = false;
            // 우두머리가 나오기 5초 전: 공성 표식을 쓰고 글만 따로 (BattleSim.Boss.cs). 글은 면이 같으면 같은 문자열 그대로
            if (sim.TryGetBossWarning(out int bossSide, out _))
            {
                phase = WavePhase.Siege;
                side = bossSide;
                warning = true;
                int bossKey = (1 + Mathf.Clamp(bossSide, 0, 3)) * -1000;
                if (bossKey != cachedPhaseLineKey)
                {
                    cachedPhaseLineKey = bossKey;
                    cachedPhaseLine = $"성벽손 오우거가 나타납니다 — {SideFull[Mathf.Clamp(bossSide, 0, 3)]}";
                }

                return cachedPhaseLine;
            }

            if (sim.FinalAssaultStarted)
                return sim.Elapsed - sim.FinalAssaultAt < BattleSim.FinalBannerSeconds ? "최후 돌격!" : null;
            if (!sim.TryGetPhaseWarning(out BattleSim.PhaseWarning w)) return null;
            phase = w.Phase;
            side = w.Side;
            warning = true;
            int sideIndex = Mathf.Clamp(w.Side, 0, 3);
            bool siege = w.Phase == WavePhase.Siege;
            // 본대 문구에는 초가 없다. 계획 시각이 지나고도 안 나온 공성 무리는 0 = "곧"
            int seconds = siege && w.SecondsLeft > 0f ? Mathf.Max(1, Mathf.CeilToInt(w.SecondsLeft)) : 0;
            int key = ((siege ? 1 : 0) * 4 + sideIndex) * 1000 + seconds;
            if (key != cachedPhaseLineKey)
            {
                cachedPhaseLineKey = key;
                string name = SideFull[sideIndex];
                cachedPhaseLine = siege ? (seconds > 0 ? $"공성 부대 접근 — {name} ({seconds}초)" : $"공성 부대 접근 — {name} (곧)") : $"본대가 몰려옵니다 — {name}";
            }

            return cachedPhaseLine;
        }
    }
}
