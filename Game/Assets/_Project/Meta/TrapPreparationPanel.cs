using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Traps;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using HumanLastRampart.UI;

namespace HumanLastRampart.Meta
{
    // 정비 함정 공방 (K-042 사용자 요청): 성을 가운데 둔 지도 한 장에 4면 들판·언덕과 설치 칸을 바로 보이고,
    // 여러 칸을 눌러 고른 뒤 [한 번에 구매·설치]. 지형 그림은 Codex K-029 (5m = 32화소 타일, 격자 겉칠).
    // 거래는 TrapPreparationTransactions의 견적·확정을 칸마다 그대로 쓴다 (골드 차감·진행 데이터 기록)
    internal sealed class TrapPreparationPanel
    {
        const float MapPx = 880f;          // 지도 한 변 (화면 1920×1080 기준)
        const float TileMeters = 5f;       // K-029 타일 한 장 = 5m
        const int TilePx = 32;
        const string BuyLabel = "한 번에 구매·설치";   // 시험이 이 글자로 시작하는 단추를 누른다

        readonly Button source;
        readonly CanvasGroup behind;
        readonly IScreenContext context;
        readonly TrapPreparationTransactions session;
        readonly PreparationTransactions preparation;
        readonly RectTransform shade, side;
        readonly Font font;
        readonly List<string> picked = new List<string>();
        readonly Dictionary<string, Image> tiles = new Dictionary<string, Image>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();   // 닫을 때 지울 텍스처·스프라이트
        int kind;
        bool storage, marked = true;
        string message = "지도에서 설치할 칸을 여러 개 눌러 고른 뒤 한 번에 구매하세요. 환불은 불가능합니다.";
        Sprite gridAllowed, gridBlocked, gridSelected, gridPlaced, trapBuried;

        public static void Show(Button source, IScreenContext context, TrapPreparationTransactions session, PreparationTransactions preparation)
        { if (source != null && source.IsInteractable()) new TrapPreparationPanel(source, context, session, preparation); }

        TrapPreparationPanel(Button source, IScreenContext context, TrapPreparationTransactions session, PreparationTransactions preparation)
        {
            this.source = source; this.context = context; this.session = session; this.preparation = preparation;
            behind = source.transform.parent.GetComponent<CanvasGroup>(); behind.interactable = false;
            font = source.GetComponentInChildren<Text>().font;
            shade = Make(source.transform.parent.parent, "Trap preparation", typeof(Image)); Stretch(shade);
            shade.GetComponent<Image>().color = UiStyle.Shade;

            // 오른쪽: 함정 고르기·선택 합계·구매·닫기 (세로 목록)
            side = Make(shade, "Trap side", typeof(Image), typeof(VerticalLayoutGroup));
            side.anchorMin = side.anchorMax = side.pivot = new Vector2(1f, 0.5f);
            side.anchoredPosition = new Vector2(-40f, 0f); side.sizeDelta = new Vector2(900f, 980f);
            side.GetComponent<Image>().color = UiStyle.PanelSolid; UiStyle.Border(side.GetComponent<Image>());
            var layout = side.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(28, 28, 24, 24); layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;

            if (!session.HasNextBattle)
            {
                Label(side, "회차 종료: 다음 전투가 없어 함정을 설치할 수 없습니다.", 90, UiStyle.Body);
                AddButton(side, "닫기", Close);
                return;
            }

            LoadSprites();
            BuildMap(session.Terrain);
            Render();
        }

        // ── 지도 (왼쪽): 땅 그림 한 장 + 칸마다 누를 수 있는 겉칠 ──
        void BuildMap(TerrainDef terrain)
        {
            float extent = Mathf.Ceil((terrain.CastleHalfSize + terrain.TrapFar + 5f) / TileMeters) * TileMeters;   // 성 가운데에서 지도 끝까지(m)
            float scale = MapPx / (extent * 2f);

            var frame = Make(shade, "Trap map", typeof(Image));
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0f, 0.5f);
            frame.anchoredPosition = new Vector2(40f, -20f); frame.sizeDelta = new Vector2(MapPx + 16f, MapPx + 16f);
            frame.GetComponent<Image>().color = UiStyle.PanelDeep; UiStyle.Border(frame.GetComponent<Image>());
            var map = Make(frame, "Map", typeof(RawImage));
            map.anchorMin = map.anchorMax = map.pivot = new Vector2(0.5f, 0.5f); map.sizeDelta = new Vector2(MapPx, MapPx);
            var ground = map.GetComponent<RawImage>(); ground.raycastTarget = false;
            Texture2D picture = ComposeGround(terrain, extent);
            if (picture != null) ground.texture = picture;
            else
            {
                // 그림이 없으면 예전처럼 색 도형: 들판 + 언덕 + 성
                ground.color = new Color(.20f, .27f, .19f);
                foreach (var h in terrain.Hills) Box(map, "Hill", new Vector2(h.x, h.z) * scale, Vector2.one * h.y * 2f * scale, new Color(.33f, .43f, .25f));
                Box(map, "Castle", Vector2.zero, Vector2.one * terrain.CastleHalfSize * 2f * scale, new Color(.46f, .43f, .37f));
            }

            // 설치 칸: 이름 = 칸 ID (시험이 이름으로 찾는다)
            float cellPx = terrain.TrapCellSize * scale;
            int cols = TrapGrid.Columns(terrain), rows = TrapGrid.Rows(terrain);
            for (int s = 0; s < 4; s++)
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < cols; col++)
                    {
                        string id = TrapGrid.CellId(s, col, row);
                        var r = Make(map, id, typeof(Image), typeof(Button));
                        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                        r.anchoredPosition = TrapGrid.Center(terrain, s, col, row) * scale;
                        r.sizeDelta = new Vector2(cellPx, cellPx);
                        var button = r.GetComponent<Button>(); button.targetGraphic = r.GetComponent<Image>();
                        UiStyle.StyleTintButton(button);
                        button.onClick.AddListener(() => Toggle(id));
                        tiles[id] = r.GetComponent<Image>();
                    }

            // 동서남북 글자 + 주 공격 방향
            string[] names = { "북", "동", "남", "서" };
            Vector2[] dirs = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            for (int s = 0; s < 4; s++)
            {
                var letter = Make(map, "Side " + names[s], typeof(Text), typeof(Outline));
                letter.anchorMin = letter.anchorMax = new Vector2(0.5f, 0.5f); letter.sizeDelta = new Vector2(48f, 40f);
                letter.anchoredPosition = dirs[s] * (MapPx * 0.5f - 22f);
                Text(letter, names[s], 30, UiStyle.Brass);
                letter.GetComponent<Text>().fontStyle = FontStyle.Bold;
                letter.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            }

            var note = Make(shade, "Map note", typeof(Text));
            note.anchorMin = note.anchorMax = note.pivot = new Vector2(0f, 1f);
            note.anchoredPosition = new Vector2(40f, -14f); note.sizeDelta = new Vector2(MapPx + 16f, 36f);
            Text(note, "▲ 북쪽이 정면 — 주 공격 방향 · 일부 적은 옆·뒤 성벽으로 돌아올 수 있습니다", 20, UiStyle.TextDim);
        }

        // K-029 타일로 땅 그림 한 장 만들기 (5m = 32화소): 풀 2종 섞기, 언덕 3단계, 빈 모서리에 나무·바위, 성 안 흙 + 성 둘레 그림
        Texture2D ComposeGround(TerrainDef terrain, float extent)
        {
            Texture2D grassA = Tile("grass_a"), grassB = Tile("grass_b"), dirt = Tile("dirt"), castle = Tile("castle_perimeter");
            Texture2D[] hills = { Tile("hill_1"), Tile("hill_2"), Tile("hill_3") };
            Texture2D trees = Tile("trees"), rocks = Tile("rocks");
            if (grassA == null || grassB == null) return null;
            try
            {
                int n = Mathf.RoundToInt(extent * 2f / TileMeters);
                int size = n * TilePx;
                var pixels = new Color32[size * size];
                var a = grassA.GetPixels32(); var b = grassB.GetPixels32();
                Color32[] dirtPx = dirt != null ? dirt.GetPixels32() : null;
                var hillPx = new Color32[3][];
                for (int k = 0; k < 3; k++) hillPx[k] = hills[k] != null ? hills[k].GetPixels32() : null;
                Color32[] treePx = trees != null ? trees.GetPixels32() : null, rockPx = rocks != null ? rocks.GetPixels32() : null;
                float half = terrain.CastleHalfSize, zoneFar = half + terrain.TrapFar;
                int seed = terrain.Id != null ? terrain.Id.GetHashCode() : 0;
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        float x = -extent + (i + 0.5f) * TileMeters, z = -extent + (j + 0.5f) * TileMeters;
                        uint h = Hash(i, j, seed);
                        bool inCastle = Mathf.Abs(x) < half && Mathf.Abs(z) < half;
                        Color32[] tile = inCastle && dirtPx != null ? dirtPx : (h & 1) == 0 ? a : b;
                        foreach (var hill in terrain.Hills)
                        {
                            float d = new Vector2(x - hill.x, z - hill.z).magnitude / Mathf.Max(0.1f, hill.y);
                            if (d > 1f) continue;
                            int level = d < 0.35f ? 2 : d < 0.7f ? 1 : 0;
                            if (hillPx[level] != null) tile = hillPx[level];
                        }
                        Paste(pixels, size, tile, i * TilePx, j * TilePx, TilePx, false);
                        // 설치 구역이 아닌 대각선 모서리·바깥에만 가끔 나무·바위 (설치 칸을 가리지 않게)
                        bool inZone = (Mathf.Abs(x) < half && Mathf.Abs(z) < zoneFar) || (Mathf.Abs(z) < half && Mathf.Abs(x) < zoneFar);
                        if (!inZone && !inCastle && (h >> 8) % 100 < 9)
                        {
                            Color32[] decor = (h >> 16) % 2 == 0 ? treePx : rockPx;
                            if (decor != null) Paste(pixels, size, decor, i * TilePx, j * TilePx, TilePx, true);
                        }
                    }

                if (castle != null)
                {
                    int castlePx = Mathf.RoundToInt(half * 2f / TileMeters * TilePx);
                    if (castle.width == castlePx && castle.height == castlePx)
                        Paste(pixels, size, castle.GetPixels32(), size / 2 - castlePx / 2, size / 2 - castlePx / 2, castlePx, true);
                }

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Trap map ground", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                owned.Add(texture);
                return texture;
            }
            catch (UnityException)
            {
                return null;   // 읽기 불가로 가져온 그림 — 색 도형으로
            }
        }

        static void Paste(Color32[] target, int size, Color32[] tile, int x0, int y0, int tileSize, bool blend)
        {
            for (int y = 0; y < tileSize; y++)
            {
                int ty = y0 + y;
                if (ty < 0 || ty >= size) continue;
                for (int x = 0; x < tileSize; x++)
                {
                    int tx = x0 + x;
                    if (tx < 0 || tx >= size) continue;
                    Color32 src = tile[y * tileSize + x];
                    int index = ty * size + tx;
                    if (!blend || src.a == 255) { if (!blend || src.a > 0) target[index] = src; continue; }
                    if (src.a == 0) continue;
                    Color32 dst = target[index];
                    float t = src.a / 255f;
                    target[index] = new Color32((byte)(dst.r + (src.r - dst.r) * t), (byte)(dst.g + (src.g - dst.g) * t), (byte)(dst.b + (src.b - dst.b) * t), 255);
                }
            }
        }

        static uint Hash(int i, int j, int seed)
        {
            unchecked
            {
                uint h = (uint)(i * 73856093) ^ (uint)(j * 19349663) ^ (uint)seed;
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return h;
            }
        }

        static Texture2D Tile(string name) => Resources.Load<Texture2D>("TrapMap/" + name);

        void LoadSprites()
        {
            gridAllowed = MakeSprite("grid_allowed"); gridBlocked = MakeSprite("grid_blocked");
            gridSelected = MakeSprite("grid_selected"); gridPlaced = MakeSprite("grid_placed"); trapBuried = MakeSprite("trap_buried");
        }

        Sprite MakeSprite(string name)
        {
            Texture2D texture = Tile(name);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            owned.Add(sprite);
            return sprite;
        }

        // ── 칸 고르기 ──
        void Toggle(string id)
        {
            if (picked.Contains(id)) picked.Remove(id); else picked.Add(id);
            message = "지도에서 칸을 더 고르거나 한 번에 구매하세요.";
            Render();
        }

        void PaintTiles()
        {
            TerrainDef terrain = session.Terrain;
            foreach (var pair in tiles)
            {
                string id = pair.Key; Image image = pair.Value;
                bool allowed = TrapGrid.IsPlaceable(terrain, id), occupied = session.Occupied(id), chosen = picked.Contains(id);
                Sprite sprite = !allowed ? gridBlocked : occupied ? gridPlaced : chosen ? gridSelected : gridAllowed;
                if (sprite != null) { image.sprite = sprite; image.color = Color.white; }
                else image.color = !allowed ? new Color(.30f, .24f, .18f, .7f) : occupied ? new Color(.55f, .32f, .26f, .9f) : chosen ? new Color(.24f, .45f, .70f, .9f) : new Color(.2f, .28f, .22f, .45f);
                image.GetComponent<Button>().interactable = allowed && !occupied;
                // 설치된 칸에는 묻힌 함정 표시
                Transform mark = image.transform.Find("Buried");
                if (occupied && mark == null && trapBuried != null)
                {
                    var icon = Make(image.transform, "Buried", typeof(Image)); Stretch(icon);
                    icon.GetComponent<Image>().sprite = trapBuried; icon.GetComponent<Image>().raycastTarget = false;
                }
            }
        }

        // ── 오른쪽 목록 (고를 때마다 다시 그린다 — 지도는 칸 색만 바꾼다) ──
        void Render()
        {
            foreach (Transform child in side) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            PaintTiles();
            var terrain = session.Terrain; var defs = context.Definitions.All<TrapDef>(); kind %= defs.Count; var def = defs[kind];
            Label(side, $"함정 공방 · 다음 지형 {terrain.DisplayName} · 골드 {context.Campaign.Gold:N0}", 56, UiStyle.Brass, 28);
            AddButton(side, $"함정: {def.DisplayName} · 상점 {session.ShopCount(def.Id)}개 · 보관 {session.StoredCount(def.Id)}개", () => { kind = (kind + 1) % defs.Count; Render(); });
            AddButton(side, storage ? "보관품 재설치 · 골드 없음" : "상점 구매·설치 · 1개 " + def.Price + "골드", () => { storage = !storage; Render(); });
            AddButton(side, marked ? "지도 표시: 켜짐 (기본)" : "지도 표시: 없음 — 지도에서 점화 불가", () => { marked = !marked; Render(); });
            Label(side, $"칸 {terrain.TrapCellSize:0.#}m · 성벽에서 {terrain.TrapNear:0}~{terrain.TrapFar:0}m\n밝은 칸 = 설치 가능 · 어두운 칸 = 언덕/불가 · 파란 칸 = 고름 · 함정 표시 = 설치됨", 70, UiStyle.TextDim, 20);

            bool ok = session.CanBuyMany(def.Id, picked, storage, out int cost, out string why);
            int each = storage ? 0 : def.Price;
            string sum = picked.Count == 0 ? "고른 칸 없음"
                : $"고른 칸 {picked.Count}개 · 합계 {each * picked.Count:N0}골드 · 보유 {context.Campaign.Gold:N0}골드";
            Label(side, sum + (picked.Count > 0 && !ok ? "\n" + why : ""), 76, picked.Count > 0 && !ok ? UiStyle.Danger : UiStyle.Body, 24);

            var buy = AddButton(side, picked.Count > 0 ? $"{BuyLabel} ({picked.Count}칸 · {cost:N0}골드)" : BuyLabel, Buy);
            buy.interactable = ok;
            if (ok) UiStyle.SetPrimary(buy);
            var clear = AddButton(side, "선택 지우기", () => { picked.Clear(); message = "선택을 지웠습니다."; Render(); });
            clear.interactable = picked.Count > 0;
            Label(side, message, 96, UiStyle.Body, 22);
            Label(side, "회수 기능은 판정 약속 보완 후 연결합니다.", 40, UiStyle.TextDim, 18);
            AddButton(side, "닫기", Close);
        }

        void Buy()
        {
            var def = context.Definitions.All<TrapDef>()[kind];
            var cells = new List<string>(picked);
            bool ok = session.TryBuyMany(def.Id, cells, storage, marked, out int placed, out int spent, out string reason, out bool error);
            if (error) { DataError(reason); return; }
            message = ok ? $"설치 완료 · {placed}칸 · {spent:N0}골드 사용 · 환불 불가"
                : placed > 0 ? $"{placed}칸만 설치됨 ({spent:N0}골드) · {reason}" : reason;
            if (ok || placed > 0) picked.Clear();
            preparation.CancelQuote();
            PreparationPanel.RefreshSummary(source, context, preparation);
            Render();
        }

        void DataError(string reason) { session.Close(); context.ReportDataError(reason); foreach (var b in shade.GetComponentsInChildren<Button>()) b.interactable = false; }

        void Close()
        {
            session.CancelQuote(); behind.interactable = true; shade.gameObject.SetActive(false); UnityEngine.Object.Destroy(shade.gameObject);
            foreach (var item in owned) if (item != null) UnityEngine.Object.Destroy(item);   // 지도 그림 텍스처·스프라이트 (불러온 원본 그림은 지우지 않는다)
            owned.Clear();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(source.gameObject);
        }

        void Box(RectTransform map, string name, Vector2 center, Vector2 size, Color color)
        {
            var b = Make(map, name, typeof(Image)); b.anchorMin = b.anchorMax = new Vector2(.5f, .5f); b.sizeDelta = size; b.anchoredPosition = center;
            var image = b.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        }

        Button AddButton(Transform parent, string text, Action action)
        {
            var r = Make(parent, text, typeof(Image), typeof(Button)); var b = r.GetComponent<Button>(); b.targetGraphic = r.GetComponent<Image>(); UiStyle.StyleButton(b);
            var l = Make(r, "Text", typeof(Text)); Stretch(l); Text(l, text, 22, UiStyle.Body); b.onClick.AddListener(() => action());
            r.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
            return b;
        }

        void Label(Transform parent, string value, float height, Color color, int size = 22)
        {
            var r = Make(parent, "Info", typeof(Text), typeof(LayoutElement)); Text(r, value, size, color);
            r.GetComponent<LayoutElement>().preferredHeight = height;
        }

        void Text(RectTransform r, string value, int size, Color color)
        {
            var t = r.GetComponent<Text>(); t.font = font; t.fontSize = size; t.text = value; t.alignment = TextAnchor.MiddleCenter;
            t.color = color; t.supportRichText = false; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        static RectTransform Make(Transform parent, string name, params Type[] components) { var o = new GameObject(name, typeof(RectTransform)); o.transform.SetParent(parent, false); foreach (var type in components) o.AddComponent(type); return o.GetComponent<RectTransform>(); }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    }
}
