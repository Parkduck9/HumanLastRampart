using System;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    // 성벽 손상 단계 (K-091 v2 수리 그림 이름과 같다: normal / cracked / heavy / breach)
    public enum RepairWallState { Normal, Cracked, Heavy, Breach }

    // 순수 규칙: 체력 비율 → 단계 → 색·그림 이름 (시험에서도 그대로 쓴다)
    public static class RepairWallLook
    {
        public const float NormalFrom = 0.70f, CrackedFrom = 0.40f, HeavyFrom = 0.15f;

        public static RepairWallState StateOf(int hp, int maxHp)
        {
            float r = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 1f;
            return r >= NormalFrom ? RepairWallState.Normal : r >= CrackedFrom ? RepairWallState.Cracked
                : r >= HeavyFrom ? RepairWallState.Heavy : RepairWallState.Breach;
        }

        public static string StateKey(RepairWallState s) =>
            s == RepairWallState.Normal ? "normal" : s == RepairWallState.Cracked ? "cracked" : s == RepairWallState.Heavy ? "heavy" : "breach";

        public static string StateName(RepairWallState s) =>
            s == RepairWallState.Normal ? "튼튼함" : s == RepairWallState.Cracked ? "금이 감" : s == RepairWallState.Heavy ? "크게 부서짐" : "무너짐";

        // 그림이 없을 때 쓰는 색 (그림이 오면 흰색 위에 그림이 올라간다)
        public static Color Tint(RepairWallState s) =>
            s == RepairWallState.Normal ? new Color(0.50f, 0.64f, 0.35f) : s == RepairWallState.Cracked ? new Color(0.84f, 0.69f, 0.42f)
            : s == RepairWallState.Heavy ? new Color(0.85f, 0.50f, 0.24f) : new Color(0.70f, 0.28f, 0.20f);

        // 그림 자리 이름 (Resources/HudArt/Repair/…) — 없으면 null로 두고 색으로 그린다
        public static string WallSlot(string wallId, RepairWallState s) => "Repair/wall_" + Direction(wallId) + "_" + StateKey(s);
        public const string MapSlot = "Repair/map_bg", KeepSlot = "Repair/keep", BarSlot = "Repair/hp_bar";

        public static string Direction(string wallId) =>
            wallId == WallIds.North ? "north" : wallId == WallIds.East ? "east" : wallId == WallIds.South ? "south" : "west";
    }

    // 수리 지도: 성을 위에서 본 그림 한 장, 네 성벽이 따로 눌리는 조각. 고른 성벽은 두꺼운 황동 테두리, 올려 둔 성벽은 얇은 테두리.
    // 단추 이름은 "btn:성벽맵:<wallId>" (시험이 같은 열쇠로 누른다)
    internal sealed class RepairMapView
    {
        public const string KeyOf = "성벽맵:";
        const float MapW = 500f, MapH = 360f;

        sealed class Piece : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Outline Rim;
            public bool Selected;
            public void OnPointerEnter(PointerEventData e) { if (!Selected) Rim.effectDistance = new Vector2(1f, -1f); Rim.enabled = true; }
            public void OnPointerExit(PointerEventData e) { if (!Selected) Rim.enabled = false; }
        }

        // 지도를 만든다. hp(i)·max(i)는 WallIds.All[i]의 체력, pick(i)는 눌렀을 때
        public static RectTransform Build(Transform parent, int selected, Func<int, (int hp, int max)> wall, Action<int> pick)
        {
            RectTransform map = Ui.Rect(parent, "RepairMap");
            Ui.Size(map, MapW, MapH);
            Image ground = Ui.Image(map, "Ground", UiTokens.PaperShade);
            ground.sprite = UiArt.Hud(RepairWallLook.MapSlot);
            if (ground.sprite != null) ground.color = Color.white;
            Ui.Stretch(ground.rectTransform);

            Image keep = Ui.Image(map, "Keep", UiTokens.Wood);
            keep.sprite = UiArt.Hud(RepairWallLook.KeepSlot);
            if (keep.sprite != null) keep.color = Color.white;
            Ui.Place(keep.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 190f));
            Text keepLabel = Ui.Label(keep.transform, "Label", "성", UiTokens.H2, UiTokens.BodyDim, TextAnchor.MiddleCenter, true);
            Ui.Stretch(keepLabel.rectTransform);

            // 북·동·남·서 (WallIds.All 순서). 위쪽이 북쪽
            Vector2[] pos = { new Vector2(0f, 150f), new Vector2(184f, 0f), new Vector2(0f, -150f), new Vector2(-184f, 0f) };
            Vector2[] size = { new Vector2(440f, 52f), new Vector2(72f, 248f), new Vector2(440f, 52f), new Vector2(72f, 248f) };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                string id = WallIds.All[i];
                var (hp, max) = wall(i);
                RepairWallState state = RepairWallLook.StateOf(hp, max);

                var go = new GameObject(Ui.KeyPrefix + KeyOf + id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(Piece));
                go.transform.SetParent(map, false);
                var image = go.GetComponent<Image>();
                image.sprite = UiArt.Hud(RepairWallLook.WallSlot(id, state));
                image.color = image.sprite != null ? Color.white : RepairWallLook.Tint(state);
                image.raycastTarget = true;
                Ui.Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), pos[i], size[i]);

                var button = go.GetComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => pick(index));

                var rim = go.GetComponent<Outline>();
                rim.effectColor = UiTokens.Brass;
                rim.useGraphicAlpha = false;
                var piece = go.GetComponent<Piece>();
                piece.Rim = rim;
                piece.Selected = i == selected;
                rim.effectDistance = piece.Selected ? new Vector2(4f, -4f) : new Vector2(1f, -1f);
                rim.enabled = piece.Selected;   // 올려 둔 순간에만 얇게, 고른 것은 늘 두껍게

                int pct = max > 0 ? Mathf.RoundToInt(100f * hp / max) : 100;
                Text text = Ui.Label(go.transform, "Text", $"{PreparationTransactions.WallName(id)}\n{pct}%", UiTokens.Small, UiTokens.Ink, TextAnchor.MiddleCenter);
                text.fontStyle = FontStyle.Bold;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                Ui.Stretch(text.rectTransform);
            }

            return map;
        }

        // 체력 막대: 지금 체력(단계 색) + 수리 뒤 체력의 옅은 그림자. after <= now 이면 그림자 없음
        public static void HpBar(Transform parent, float width, float height, int hp, int max, int after)
        {
            Image track = Ui.Image(parent, "HpBar", Color.white);
            track.sprite = UiArt.Hud(RepairWallLook.BarSlot) ?? UiArt.Get(UiArt.BarTrack);
            track.type = Image.Type.Sliced;
            Ui.Size(track, width, height);
            RectTransform inner = Ui.Rect(track.transform, "Inner");
            Ui.Stretch(inner, UiTokens.Dot);
            float now = max > 0 ? Mathf.Clamp01((float)hp / max) : 1f;
            float then = max > 0 ? Mathf.Clamp01((float)after / max) : 1f;
            Image ghost = Ui.Image(inner, "After", new Color(0.96f, 0.93f, 0.78f, 0.9f));   // 수리 뒤 (옅은 색)
            Ui.Stretch(ghost.rectTransform);
            ghost.rectTransform.anchorMax = new Vector2(Mathf.Max(then, now), 1f);
            ghost.enabled = then > now;
            Image fill = Ui.Image(inner, "Now", RepairWallLook.Tint(RepairWallLook.StateOf(hp, max)));
            Ui.Stretch(fill.rectTransform);
            fill.rectTransform.anchorMax = new Vector2(now, 1f);
        }
    }
}
