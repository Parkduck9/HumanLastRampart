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

        // 그림 자리 이름 (Resources/HudArt/Repair/…) — 없으면 null로 두고 색으로 그린다 (K-096 + K-091 v2)
        // wall_*: 256×256 전체 캔버스 패치 (정상/금/크게/무너짐 = K-091 healthy/damaged/badly_damaged/breached), keep: 성 전체 그림 256×256
        public static string WallSlot(string wallId, RepairWallState s) => "Repair/wall_" + Direction(wallId) + "_" + StateKey(s);
        public static string OutlineSlot(string wallId, bool selected) => "Repair/outline_" + Direction(wallId) + (selected ? "_selected" : "_hover");
        public const string MapSlot = "Repair/map_bg", KeepSlot = "Repair/keep", BarSlot = "Repair/hp_frame", GhostSlot = "Repair/hp_fill_ghost";
        public static readonly Vector4 BarBorder = new Vector4(8f, 8f, 8f, 8f);   // 좌·아래·우·위 (K-096)

        // 체력 채움 그림: 튼튼=초록, 금=노랑, 크게 부서짐·무너짐=빨강
        public static string FillSlot(RepairWallState s) =>
            s == RepairWallState.Normal ? "Repair/hp_fill_green" : s == RepairWallState.Cracked ? "Repair/hp_fill_yellow" : "Repair/hp_fill_red";

        // 256×256 성 그림 안에서 성벽 교체 사각형 (왼쪽 위 기준 x, y, 너비, 높이) — K-096 manifest replace_rect
        public static RectInt WallRect(string wallId) =>
            wallId == WallIds.North ? new RectInt(52, 8, 152, 52) : wallId == WallIds.East ? new RectInt(196, 52, 52, 152)
            : wallId == WallIds.South ? new RectInt(52, 196, 152, 52) : new RectInt(8, 52, 52, 152);
        public static string Direction(string wallId) =>
            wallId == WallIds.North ? "north" : wallId == WallIds.East ? "east" : wallId == WallIds.South ? "south" : "west";
    }

    // 수리 지도: 성을 위에서 본 그림 한 장, 네 성벽이 따로 눌리는 조각. 고른 성벽은 두꺼운 황동 테두리, 올려 둔 성벽은 얇은 테두리.
    // 단추 이름은 "btn:성벽맵:<wallId>" (시험이 같은 열쇠로 누른다)
    internal sealed class RepairMapView
    {
        public const string KeyOf = "성벽맵:";
        const float MapW = 500f, MapH = 360f;

        // 256점 성 그림을 화면에 놓는 크기 (정사각형 한 변)
        const float CastleSize = 360f;
        const float CastleScale = CastleSize / 256f;

        sealed class Piece : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Outline Rim;          // 외곽선 그림이 없을 때만 쓰는 대체 테두리
            public Image HoverOutline;   // K-096 가리킴 외곽선 (전체 캔버스)
            public bool Selected;
            public void OnPointerEnter(PointerEventData e)
            {
                if (HoverOutline != null) { if (!Selected) HoverOutline.enabled = true; return; }
                if (!Selected) Rim.effectDistance = new Vector2(1f, -1f);
                Rim.enabled = true;
            }
            public void OnPointerExit(PointerEventData e)
            {
                if (HoverOutline != null) { HoverOutline.enabled = false; return; }
                if (!Selected) Rim.enabled = false;
            }
        }

        // 성 그림 전체 캔버스 패치에서 해당 성벽 사각형만 잘라 낸 조각 (손상 패치 밖은 투명이라 잘라도 같은 그림)
        static readonly System.Collections.Generic.Dictionary<string, Sprite> crops = new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Sprite WallCrop(string wallId, RepairWallState state)
        {
            string slot = RepairWallLook.WallSlot(wallId, state);
            if (crops.TryGetValue(slot, out Sprite cached) && cached != null) return cached;
            var texture = Resources.Load<Texture2D>("HudArt/" + slot);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            RectInt r = RepairWallLook.WallRect(wallId);
            var sprite = Sprite.Create(texture, new Rect(r.x, texture.height - r.y - r.height, r.width, r.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = slot + "_crop";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            crops[slot] = sprite;
            return sprite;
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

            // 성 전체 그림 (K-096 reference/castle_perimeter와 같은 픽셀). 없으면 나무색 사각형 + "성"
            Vector2 center = new Vector2(0.5f, 0.5f);
            Sprite castle = UiArt.Hud(RepairWallLook.KeepSlot);
            Image keep = Ui.Image(map, "Keep", castle != null ? Color.white : UiTokens.Wood);
            keep.sprite = castle;
            if (castle != null) Ui.Place(keep.rectTransform, center, Vector2.zero, new Vector2(CastleSize, CastleSize));
            else
            {
                Ui.Place(keep.rectTransform, center, Vector2.zero, new Vector2(280f, 190f));
                Text keepLabel = Ui.Label(keep.transform, "Label", "성", UiTokens.H2, UiTokens.BodyDim, TextAnchor.MiddleCenter, true);
                Ui.Stretch(keepLabel.rectTransform);
            }

            // 북·동·남·서 (WallIds.All 순서). 위쪽이 북쪽. 그림이 있으면 256점 성 그림의 교체 사각형을 화면 크기로 옮긴 자리, 없으면 옛 막대 모양
            Vector2[] oldPos = { new Vector2(0f, 150f), new Vector2(184f, 0f), new Vector2(0f, -150f), new Vector2(-184f, 0f) };
            Vector2[] oldSize = { new Vector2(440f, 52f), new Vector2(72f, 248f), new Vector2(440f, 52f), new Vector2(72f, 248f) };
            var pieces = new Piece[4];
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                string id = WallIds.All[i];
                var (hp, max) = wall(i);
                RepairWallState state = RepairWallLook.StateOf(hp, max);
                Sprite crop = castle != null ? WallCrop(id, state) : null;

                Vector2 pos = oldPos[i], size = oldSize[i];
                if (crop != null)
                {
                    RectInt r = RepairWallLook.WallRect(id);
                    pos = new Vector2((r.x + r.width * 0.5f - 128f) * CastleScale, -(r.y + r.height * 0.5f - 128f) * CastleScale);
                    size = new Vector2(r.width, r.height) * CastleScale;
                    // 무너짐 패치는 구멍이 뚫려 있어 아래 성 그림의 옛 성벽이 비친다 → 같은 바닥 색으로 먼저 덮는다
                    Image mask = Ui.Image(map, "Mask:" + id, UiTokens.PaperShade);
                    Ui.Place(mask.rectTransform, center, pos, size);
                }

                var go = new GameObject(Ui.KeyPrefix + KeyOf + id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(Piece));
                go.transform.SetParent(map, false);
                var image = go.GetComponent<Image>();
                image.sprite = crop;
                image.color = crop != null ? Color.white : RepairWallLook.Tint(state);
                image.raycastTarget = true;
                Ui.Place((RectTransform)go.transform, center, pos, size);

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
                rim.enabled = piece.Selected && castle == null;   // 올려 둔 순간에만 얇게, 고른 것은 늘 두껍게 (외곽선 그림이 없을 때만 대체 테두리)
                pieces[i] = piece;

                int pct = max > 0 ? Mathf.RoundToInt(100f * hp / max) : 100;
                string caption = $"{PreparationTransactions.WallName(id)}\n{pct}%";
                if (crop != null)
                {
                    // 성벽 그림 위에 글을 얹으면 돌 무늬에 묻힌다 → 마당 쪽으로 당겨 테두리 글로 쓴다 (누르기는 조각이 받는다)
                    Text inner = Ui.Label(map, "Text:" + id, caption, UiTokens.Text, UiTokens.Ink, TextAnchor.MiddleCenter, true);
                    HumanLastRampart.UI.UiFonts.Bold(inner);
                    inner.horizontalOverflow = HorizontalWrapMode.Overflow;
                    inner.raycastTarget = false;
                    Ui.Place(inner.rectTransform, center, pos * 0.62f, new Vector2(120f, 44f));
                }
                else
                {
                    Text text = Ui.Label(go.transform, "Text", caption, UiTokens.Small, UiTokens.Ink, TextAnchor.MiddleCenter);
                    HumanLastRampart.UI.UiFonts.Bold(text);
                    text.horizontalOverflow = HorizontalWrapMode.Overflow;
                    Ui.Stretch(text.rectTransform);
                }
            }

            // 외곽선 (K-096): 모든 조각 위에 256점 전체 캔버스 그림. 고른 성벽은 늘, 나머지는 가리킬 때만
            for (int i = 0; i < 4; i++)
            {
                string id = WallIds.All[i];
                Sprite selectedSprite = UiArt.Hud(RepairWallLook.OutlineSlot(id, true));
                Sprite hoverSprite = UiArt.Hud(RepairWallLook.OutlineSlot(id, false));
                if (castle == null || selectedSprite == null || hoverSprite == null) continue;
                Image selectedOutline = Ui.Image(map, "Outline:selected:" + id, Color.white);
                selectedOutline.sprite = selectedSprite;
                Ui.Place(selectedOutline.rectTransform, center, Vector2.zero, new Vector2(CastleSize, CastleSize));
                selectedOutline.enabled = pieces[i].Selected;
                Image hoverOutline = Ui.Image(map, "Outline:hover:" + id, Color.white);
                hoverOutline.sprite = hoverSprite;
                Ui.Place(hoverOutline.rectTransform, center, Vector2.zero, new Vector2(CastleSize, CastleSize));
                hoverOutline.enabled = false;
                pieces[i].HoverOutline = hoverOutline;
                pieces[i].Rim.enabled = false;
            }

            return map;
        }

        static void BarFill(Transform bar, string name, Sprite sprite, float width, float ratio, bool visible)
        {
            Image fill = Ui.Image(bar, name, Color.white);
            fill.sprite = sprite;
            fill.type = Image.Type.Sliced;
            RectTransform rt = fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(Mathf.Max(16f, Mathf.Round((width - 12f) * ratio) + 12f), 0f);
            fill.enabled = visible && ratio > 0f;
        }

        // 체력 막대: 지금 체력(단계 색) + 수리 뒤 체력의 옅은 그림자. after <= now 이면 그림자 없음
        public static void HpBar(Transform parent, float width, float height, int hp, int max, int after)
        {
            Image track = Ui.Image(parent, "HpBar", Color.white);
            Sprite frame = UiArt.Hud(RepairWallLook.BarSlot, RepairWallLook.BarBorder);
            Sprite fillSprite = UiArt.Hud(RepairWallLook.FillSlot(RepairWallLook.StateOf(hp, max)), RepairWallLook.BarBorder);
            Sprite ghostSprite = UiArt.Hud(RepairWallLook.GhostSlot, RepairWallLook.BarBorder);
            if (frame != null && fillSprite != null && ghostSprite != null)
            {
                // K-096: 프레임 위에 수리 뒤 채움 → 지금 채움 (왼쪽 정렬). 채움 너비 = max(16, round((W-12)p)+12), p=0이면 숨김
                track.sprite = frame;
                track.type = Image.Type.Sliced;
                Ui.Size(track, width, height);
                float nowP = max > 0 ? Mathf.Clamp01((float)hp / max) : 1f;
                float thenP = max > 0 ? Mathf.Clamp01((float)after / max) : 1f;
                BarFill(track.transform, "After", ghostSprite, width, Mathf.Max(thenP, nowP), thenP > nowP);
                BarFill(track.transform, "Now", fillSprite, width, nowP, nowP > 0f);
                return;
            }
            track.sprite = UiArt.Get(UiArt.BarTrack);
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
