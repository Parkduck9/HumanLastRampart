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
    // 정비 함정 공방 (K-042 사용자 요청 → K-047 모양): 왼쪽 = 양피지 테두리 안의 작전 지도(성 가운데, 4면 들판·언덕·설치 칸),
    // 오른쪽 = 공방 장부(양피지): [함정 | 지형 손질] 탭 · 종류·할 일 칩 · 칸 그림 범례 · 값 줄 · 황동 [N칸 묻기 / N칸 짓기].
    // 여러 칸을 눌러 고른 뒤 한 번에 한다. 지형 그림은 Codex K-029·K-030 (5m = 32화소 타일, 격자 겉칠).
    // 함정 거래 = TrapPreparationTransactions (칸마다 견적·확정), 지형 손질 = TerrainWorkTransactions (K-049·K-051: 토벽·언덕·길·옮기기·철거)
    internal sealed class TrapPreparationPanel
    {
        const float MapPx = 880f;          // 지도 한 변 (화면 1920×1080 기준)
        const float TileMeters = 5f;       // K-029 타일 한 장 = 5m
        const int TilePx = 32;
        const string BuyLabel = "한 번에 구매·설치";   // 시험이 이 열쇠로 누른다 (보이는 글은 "N칸 묻기")
        const string BuildLabel = "한 번에 짓기";      // 지형 탭 적용 열쇠 (보이는 글은 "N칸 토벽 쌓기" 등)

        readonly Button source;
        readonly CanvasGroup behind;
        readonly IScreenContext context;
        readonly TrapPreparationTransactions session;
        readonly PreparationTransactions preparation;
        readonly GameObject shade;
        readonly RectTransform side;
        readonly List<string> picked = new List<string>();
        readonly Dictionary<string, Image> tiles = new Dictionary<string, Image>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();   // 닫을 때 지울 텍스처·스프라이트
        int kind;
        bool storage, marked = true;
        string message;   // 방금 한 일의 결과 (설명문이 아니라 결과만)
        bool messageBad;
        Sprite gridAllowed, gridBlocked, gridSelected, gridPlaced, trapBuried;
        // 지형 탭 (K-049·K-051)
        readonly TerrainWorkTransactions works;
        bool terrainTab;
        TerrainAction action = TerrainAction.EarthWall;
        Sprite earthwallIcon, raisedHillIcon, treesIcon, rocksIcon, grassIcon;
        readonly Sprite[] pathIcons = new Sprite[16];     // K-029 흙길: 북1·동2·남4·서8 연결 비트

        public static void Show(Button source, IScreenContext context, TrapPreparationTransactions session, PreparationTransactions preparation)
        { if (source != null && source.IsInteractable()) new TrapPreparationPanel(source, context, session, preparation); }

        TrapPreparationPanel(Button source, IScreenContext context, TrapPreparationTransactions session, PreparationTransactions preparation)
        {
            this.source = source; this.context = context; this.session = session; this.preparation = preparation;
            works = new TerrainWorkTransactions(session);
            behind = source.transform.parent.GetComponentInParent<CanvasGroup>();   // 정비 띠 판 (숨긴 단추는 "Actions" 줄 안에 있다)
            if (behind != null) behind.interactable = false;
            shade = Ui.Shade(source.GetComponentInParent<Canvas>().transform, "Trap preparation").gameObject;

            // 오른쪽: 공방 장부
            Image sheet = Ui.Panel(shade.transform, "Trap side", UiPanel.Parchment);
            side = sheet.rectTransform;
            Ui.Place(side, new Vector2(1f, 0.5f), new Vector2(-UiTokens.S48, 0f), new Vector2(840f, 0f), new Vector2(1f, 0.5f));
            var layout = sheet.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 32, 48);
            layout.spacing = UiTokens.S24;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            sheet.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiAppear.Show(sheet.gameObject, new Vector2(UiTokens.S24, 0f));

            if (!session.HasNextBattle)
            {
                Head();
                Text line = Ui.Label(side, "Info", "다음 전투가 없어 함정을 묻을 곳이 없습니다", UiTokens.Text, UiTokens.InkSoft);
                Ui.Size(line, -1f, 36f);
                return;
            }

            LoadSprites();
            BuildMap(session.Terrain);
            Render();
        }

        // ── 지도 (왼쪽): 양피지 테두리 + 땅 그림 한 장 + 칸마다 누를 수 있는 겉칠 ──
        void BuildMap(TerrainDef terrain)
        {
            float extent = Mathf.Ceil((terrain.CastleHalfSize + terrain.TrapFar + 5f) / TileMeters) * TileMeters;   // 성 가운데에서 지도 끝까지(m)
            float scale = MapPx / (extent * 2f);

            Image frameImage = Ui.Panel(shade.transform, "Trap map", UiPanel.Parchment);
            var frame = frameImage.rectTransform;
            Ui.Place(frame, new Vector2(0f, 0.5f), new Vector2(UiTokens.S48, 0f), new Vector2(MapPx + 48f, MapPx + 48f), new Vector2(0f, 0.5f));
            UiAppear.Show(frameImage.gameObject, new Vector2(-UiTokens.S24, 0f));
            var mapObject = new GameObject("Map", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            mapObject.transform.SetParent(frame, false);
            var map = (RectTransform)mapObject.transform;
            map.anchorMin = map.anchorMax = map.pivot = new Vector2(0.5f, 0.5f); map.sizeDelta = new Vector2(MapPx, MapPx);
            var ground = map.GetComponent<RawImage>(); ground.raycastTarget = false;
            Texture2D picture = ComposeGround(terrain, extent);
            if (picture != null) ground.texture = picture;
            else
            {
                // 그림이 없으면 색 도형: 들판 + 언덕 + 나무·바위 덩이 + 성
                ground.color = new Color(.20f, .27f, .19f);
                TerrainLayout shape = session.Layout;
                foreach (var h in shape.Hills) Box(map, "Hill", new Vector2(h.x, h.z) * scale, Vector2.one * h.y * 2f * scale, new Color(.33f, .43f, .25f));
                foreach (int c in shape.TreeCells) Box(map, "Trees", shape.Center(c) * scale, Vector2.one * shape.CellSize * scale, new Color(.14f, .30f, .14f));
                foreach (int c in shape.RockCells) Box(map, "Rocks", shape.Center(c) * scale, Vector2.one * shape.CellSize * scale, new Color(.45f, .45f, .42f));
                Box(map, "Castle", Vector2.zero, Vector2.one * terrain.CastleHalfSize * 2f * scale, new Color(.46f, .43f, .37f));
            }

            // 설치 칸: 이름 = 칸 ID (시험이 이름으로 찾는다). 올리면 따뜻하게 밝아지고 누르면 어두워진다
            float cellPx = terrain.TrapCellSize * scale;
            int cols = TrapGrid.Columns(terrain), rows = TrapGrid.Rows(terrain);
            for (int s = 0; s < 4; s++)
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < cols; col++)
                    {
                        string id = TrapGrid.CellId(s, col, row);
                        var cell = new GameObject(id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                        cell.transform.SetParent(map, false);
                        var r = (RectTransform)cell.transform;
                        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                        r.anchoredPosition = TrapGrid.Center(terrain, s, col, row) * scale;
                        r.sizeDelta = new Vector2(cellPx, cellPx);
                        var button = cell.GetComponent<Button>(); button.targetGraphic = cell.GetComponent<Image>();
                        var colors = ColorBlock.defaultColorBlock;
                        colors.highlightedColor = new Color(1f, 0.93f, 0.72f);   // 황동 쪽으로 따뜻하게
                        colors.selectedColor = Color.white;
                        colors.pressedColor = new Color(0.78f, 0.66f, 0.45f);
                        colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.9f);
                        button.colors = colors;
                        button.onClick.AddListener(() => Toggle(id));
                        tiles[id] = cell.GetComponent<Image>();
                    }

            // 동서남북 글자 (세계 글꼴), 북쪽 아래에 '정면' 꼬리표 — 주 공격 방향
            string[] names = { "북", "동", "남", "서" };
            Vector2[] dirs = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            for (int s = 0; s < 4; s++)
            {
                Text letter = Ui.Outlined(Ui.Label(map, "Side " + names[s], names[s], UiTokens.H2, UiTokens.Brass, TextAnchor.MiddleCenter, true));
                letter.fontStyle = FontStyle.Bold;
                Ui.Place(letter.rectTransform, new Vector2(0.5f, 0.5f), dirs[s] * (MapPx * 0.5f - 24f), new Vector2(48f, 40f));
            }

            Text front = Ui.Outlined(Ui.Label(map, "Front", "정면", UiTokens.Small, UiTokens.BrassLight, TextAnchor.MiddleCenter));
            Ui.Place(front.rectTransform, new Vector2(0.5f, 0.5f), Vector2.up * (MapPx * 0.5f - 60f), new Vector2(80f, 28f));
        }

        // K-029 타일로 땅 그림 한 장 만들기 (5m = 32화소): 풀 2종 섞기, 언덕 3단계, 무작위 지형의 나무·바위 덩이(K-049), 성 안 흙 + 성 둘레 그림
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
                float half = terrain.CastleHalfSize;
                int seed = terrain.Id != null ? terrain.Id.GetHashCode() : 0;
                TerrainLayout shape = session.Layout;   // 무작위 지형 (K-049): 언덕·나무 덩이·바위 덩이가 진짜 위치에
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        float x = -extent + (i + 0.5f) * TileMeters, z = -extent + (j + 0.5f) * TileMeters;
                        uint h = Hash(i, j, seed);
                        bool inCastle = Mathf.Abs(x) < half && Mathf.Abs(z) < half;
                        Color32[] tile = inCastle && dirtPx != null ? dirtPx : (h & 1) == 0 ? a : b;
                        foreach (var hill in shape.Hills)
                        {
                            float d = new Vector2(x - hill.x, z - hill.z).magnitude / Mathf.Max(0.1f, hill.y);
                            if (d > 1f) continue;
                            int level = d < 0.35f ? 2 : d < 0.7f ? 1 : 0;
                            if (hillPx[level] != null) tile = hillPx[level];
                        }
                        Paste(pixels, size, tile, i * TilePx, j * TilePx, TilePx, false);
                        byte cellKind = shape.KindAt(x, z);
                        Color32[] decor = cellKind == TerrainCell.Trees ? treePx : cellKind == TerrainCell.Rocks ? rockPx : null;
                        if (decor != null) Paste(pixels, size, decor, i * TilePx, j * TilePx, TilePx, true);
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
            // K-030 지도 그림: 직선 토벽은 10×5m(64×32) 한 장 — 칸 하나(5m)에는 왼쪽 절반만
            earthwallIcon = MakeSprite("earthwall_map", 32); raisedHillIcon = MakeSprite("raised_hill_map");
            // K-051: 옮긴 나무·바위, 옮겨 간 자리(풀), 낸 길 — 땅 그림은 열 때 한 번만 만들어 칸 위에 겹쳐 보인다
            treesIcon = MakeSprite("trees"); rocksIcon = MakeSprite("rocks"); grassIcon = MakeSprite("grass_a");
            for (int b = 0; b < 16; b++) pathIcons[b] = MakeSprite("path_" + b);
        }

        Sprite MakeSprite(string name, int width = 0)
        {
            Texture2D texture = Tile(name);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            int w = width > 0 ? Mathf.Min(width, texture.width) : texture.width;
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, texture.height), new Vector2(0.5f, 0.5f), 100f);
            owned.Add(sprite);
            return sprite;
        }

        // ── 칸 고르기 ──
        void Toggle(string id)
        {
            if (picked.Contains(id)) picked.Remove(id); else picked.Add(id);
            message = null;
            Render();
        }

        void PaintTiles()
        {
            foreach (var pair in tiles)
            {
                string id = pair.Key; Image image = pair.Value;
                // 함정 탭: 함정을 놓을 수 있는 칸 / 지형 탭: 고른 할 일을 할 수 있는 칸
                bool allowed = terrainTab ? works.CanTarget(action, id, picked) : session.IsPlaceable(id);
                bool occupied = !terrainTab && session.Occupied(id), chosen = picked.Contains(id);
                Sprite sprite = !allowed ? gridBlocked : occupied ? gridPlaced : chosen ? gridSelected : gridAllowed;
                if (sprite != null) { image.sprite = sprite; image.color = Color.white; }
                else image.color = !allowed ? new Color(.30f, .24f, .18f, .7f) : occupied ? UiTokens.Alpha(UiTokens.Danger, .9f) : chosen ? UiTokens.Alpha(UiTokens.BrassLight, .9f) : new Color(.2f, .28f, .22f, .45f);
                image.GetComponent<Button>().interactable = allowed && !occupied;
                PaintWork(image, id);
                // 설치된 칸에는 묻힌 함정 표시
                Transform mark = image.transform.Find("Buried");
                if (session.Occupied(id) && mark == null && trapBuried != null)
                {
                    Image icon = Ui.Image(image.transform, "Buried", Color.white);
                    icon.sprite = trapBuried;
                    Ui.Stretch(icon.rectTransform);
                }
            }
        }

        // 지어 둔 손질 그림 (K-030·K-029 지도 칸). 동·서 성벽 칸의 토벽은 성벽과 나란하게 90도 돌린다
        void PaintWork(Image image, string id)
        {
            TerrainWork work = works.WorkAt(id);
            Transform mark = image.transform.Find("Work");
            Sprite icon = null;
            if (work != null)
            {
                switch (work.Kind)
                {
                    case TerrainWorkKind.EarthWall: icon = earthwallIcon; break;
                    case TerrainWorkKind.Hill: icon = raisedHillIcon; break;
                    case TerrainWorkKind.Path: icon = pathIcons[PathBits(id)]; break;
                    case TerrainWorkKind.PlacedTrees: icon = treesIcon; break;
                    case TerrainWorkKind.PlacedRocks: icon = rocksIcon; break;
                    case TerrainWorkKind.Cleared: icon = grassIcon; break;
                }
            }
            if (icon == null) { if (mark != null) mark.gameObject.SetActive(false); return; }
            if (mark == null)
            {
                Image added = Ui.Image(image.transform, "Work", Color.white);
                Ui.Stretch(added.rectTransform);
                mark = added.transform;
            }

            mark.gameObject.SetActive(true);
            mark.GetComponent<Image>().sprite = icon;
            bool sideways = work.Kind == TerrainWorkKind.EarthWall && TrapGrid.TryParse(id, out int s, out _, out _) && (s == 1 || s == 3);
            mark.localRotation = Quaternion.Euler(0f, 0f, sideways ? 90f : 0f);
        }

        // 길 칸의 이웃 연결 (지도 위 = 북): 같은 지형 격자에서 동서남북 칸도 길이면 비트
        int PathBits(string id)
        {
            TerrainLayout shape = session.Layout;
            if (!TrapGrid.TryParse(id, out int sideIndex, out int column, out int row)) return 0;
            Vector2 c = TrapGrid.Center(session.Terrain, sideIndex, column, row);
            float step = shape.CellSize;
            int bits = 0;
            if (shape.KindAt(c.x, c.y + step) == TerrainCell.Path) bits |= 1;
            if (shape.KindAt(c.x + step, c.y) == TerrainCell.Path) bits |= 2;
            if (shape.KindAt(c.x, c.y - step) == TerrainCell.Path) bits |= 4;
            if (shape.KindAt(c.x - step, c.y) == TerrainCell.Path) bits |= 8;
            return bits;
        }

        // ── 오른쪽 장부 (고를 때마다 다시 그린다 — 지도는 칸 그림만 바꾼다) ──
        void Head()
        {
            HorizontalLayoutGroup head = Ui.Row(side, "Head", UiTokens.S16);
            Ui.Size(head, -1f, 48f);
            Text title = Ui.Label(head.transform, "Title", "함정 공방", UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Size(title, 0f, 48f, true);
            if (Ui.HudIcon(head.transform, "Gold Icon", "Icons/stat_gold", 32f) == null) Ui.Size(Ui.Label(head.transform, "Gold Word", "골드", UiTokens.Small, UiTokens.InkSoft), 44f, 48f);
            Text gold = Ui.Label(head.transform, "Gold", context.Campaign.Gold.ToString("N0"), UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Size(gold, 136f, 48f);
            Ui.IconButton(head.transform, "닫기", UiArt.IconClose, Close, 48f);
        }

        void Render()
        {
            foreach (Transform child in side) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            PaintTiles();
            Head();

            // [함정 | 지형 손질] 탭 + 다음 싸움터
            HorizontalLayoutGroup tabs = Ui.Row(side, "Tabs", UiTokens.S8);
            Button trapTab = Ui.Button(tabs.transform, "탭:함정", "함정", () => SwitchTab(false), UiButtonKind.Tab);
            Ui.Size(trapTab, 176f, 56f);
            Ui.SetOn(trapTab, !terrainTab);
            Button groundTab = Ui.Button(tabs.transform, "탭:지형", "지형 손질", () => SwitchTab(true), UiButtonKind.Tab);
            Ui.Size(groundTab, 176f, 56f);
            Ui.SetOn(groundTab, terrainTab);
            Text where = Ui.Label(tabs.transform, "Terrain", $"다음 싸움터 · {session.Terrain.DisplayName}", UiTokens.Small, UiTokens.InkSoft, TextAnchor.MiddleRight);
            Ui.Size(where, 0f, 56f, true);

            if (terrainTab) RenderTerrainTab();
            else RenderTrapTab();
        }

        void SwitchTab(bool terrain)
        {
            if (terrainTab == terrain) return;
            terrainTab = terrain;
            picked.Clear();
            message = null;
            Render();
        }

        void RenderTrapTab()
        {
            var defs = context.Definitions.All<TrapDef>(); kind %= defs.Count; var def = defs[kind];

            // 함정 종류 (그림 카드 대신 지금은 이름 + 재고)
            HorizontalLayoutGroup kinds = Ui.Row(side, "Kinds", UiTokens.S8);
            for (int i = 0; i < defs.Count; i++)
            {
                int index = i;
                var d = defs[i];
                Button chip = Ui.Button(kinds.transform, "함정:" + d.Id, d.DisplayName, () => { if (kind == index) return; kind = index; message = null; Render(); }, UiButtonKind.Tab);
                Ui.Size(chip, 232f, 72f);
                Text label = Ui.LabelOf(chip);
                label.alignment = TextAnchor.UpperCenter;
                label.rectTransform.offsetMax = new Vector2(-UiTokens.S16, -UiTokens.S8);
                Text stock = Ui.Label(chip.transform.Find("Content"), "Stock", $"상점 {session.ShopCount(d.Id)} · 보관 {session.StoredCount(d.Id)}", UiTokens.Small, UiTokens.BodyDim, TextAnchor.LowerCenter);
                Ui.Stretch(stock.rectTransform, UiTokens.S8);
                Ui.SetOn(chip, i == kind);
            }

            // 어디서: 상점에서 사기 / 보관품 묻기 · 지도 표시 켜고 끄기
            HorizontalLayoutGroup options = Ui.Row(side, "Options", UiTokens.S8);
            Button shop = Ui.Button(options.transform, "출처:상점", $"상점 {def.Price}골드", () => { storage = false; message = null; Render(); }, UiButtonKind.Tab);
            Ui.Size(shop, 232f, 56f);
            Ui.SetOn(shop, !storage);
            Button stored = Ui.Button(options.transform, "출처:보관", "보관품", () => { storage = true; message = null; Render(); }, UiButtonKind.Tab);
            Ui.Size(stored, 168f, 56f);
            Ui.SetOn(stored, storage);
            stored.interactable = session.StoredCount(def.Id) > 0;
            Button mark = Ui.Button(options.transform, "지도 표시", marked ? "지도에 표시" : "표시 안 함", () => { marked = !marked; Render(); }, UiButtonKind.Tab, marked ? UiArt.IconCheck : UiArt.IconClose);
            Ui.Size(mark, 248f, 56f);
            Ui.SetOn(mark, marked);

            LegendRow("묻힘");

            bool ok = session.CanBuyMany(def.Id, picked, storage, out int cost, out string why);
            PriceLine(ok, cost, why, "지도에서 칸을 누르기");

            HorizontalLayoutGroup act = Ui.Row(side, "Actions", UiTokens.S16);
            Ui.Size(act, -1f, 64f);
            var buy = Ui.Button(act.transform, BuyLabel, picked.Count > 0 ? $"{picked.Count}칸 묻기" : "묻기", Buy, UiButtonKind.Primary);
            Ui.Size(buy, 288f, 64f);
            buy.interactable = ok;
            ClearButton(act.transform);
            Ui.Icon(act.transform, "Warn", UiArt.IconWarn, 1f, UiTokens.Danger);
            Text final = Ui.Label(act.transform, "Final", "되돌릴 수 없음", UiTokens.Small, UiTokens.Danger);
            Ui.Size(final, final.preferredWidth + 4f, 64f);
        }

        // 지형 손질 탭 (D-077, K-049·K-051): 할 일 칩 5개(토벽·언덕·길·옮기기·철거) + 한 줄 효과 + 값 + [N칸 짓기]
        void RenderTerrainTab()
        {
            HorizontalLayoutGroup chips = Ui.Row(side, "Actions Kind", UiTokens.S8);
            string[] shortNames = { "토벽", "언덕", "길", "옮기기", "철거" };
            for (int i = 0; i < 5; i++)
            {
                var a = (TerrainAction)i;
                Button chip = Ui.Button(chips.transform, "할 일:" + TerrainWorkTransactions.NameOf(a), shortNames[i], () => { if (action == a) return; action = a; picked.Clear(); message = null; Render(); }, UiButtonKind.Tab);
                Ui.Size(chip, 136f, 72f);
                Text label = Ui.LabelOf(chip);
                label.alignment = TextAnchor.UpperCenter;
                label.rectTransform.offsetMax = new Vector2(-UiTokens.S16, -UiTokens.S8);
                string price = a == TerrainAction.Demolish ? "절반 환불" : $"{TerrainWorkTransactions.Price(a)}골드";
                Text tag = Ui.Label(chip.transform.Find("Content"), "Price", price, UiTokens.Small, UiTokens.BodyDim, TextAnchor.LowerCenter);
                Ui.Stretch(tag.rectTransform, UiTokens.S8);
                Ui.SetOn(chip, a == action);
            }

            Text effect = Ui.Label(side, "Effect", Effect(action), UiTokens.Text, UiTokens.InkSoft);
            Ui.Size(effect, -1f, 32f);
            LegendRow(null);

            bool ok = works.CanApply(action, picked, out int cost, out string why);
            string hint = action == TerrainAction.Move && picked.Count % 2 == 1 ? "이제 놓을 빈 칸을 누르기"
                : action == TerrainAction.Move ? "옮길 나무·바위 칸을 누르기" : "지도에서 칸을 누르기";
            PriceLine(ok, cost, why, hint);

            HorizontalLayoutGroup act = Ui.Row(side, "Actions", UiTokens.S16);
            Ui.Size(act, -1f, 64f);
            string verb = TerrainWorkTransactions.NameOf(action);
            int count = action == TerrainAction.Move ? picked.Count / 2 : picked.Count;
            var build = Ui.Button(act.transform, BuildLabel, count > 0 ? $"{count}칸 {verb}" : verb, Build, UiButtonKind.Primary);
            Ui.Size(build, 288f, 64f);
            build.interactable = ok;
            ClearButton(act.transform);
            if (action != TerrainAction.Demolish)
            {
                Text note = Ui.Label(act.transform, "Refund", "철거하면 절반 환불", UiTokens.Small, UiTokens.InkSoft);
                Ui.Size(note, note.preferredWidth + 4f, 64f);
            }
        }

        // 할 일 한 줄 (확률·규칙 문장 대신 결과만 — S-17)
        static string Effect(TerrainAction action)
        {
            switch (action)
            {
                case TerrainAction.EarthWall: return "적이 돌아가거나 넘거나 허문다 · 뒤에 숨으면 총·화살을 피함";
                case TerrainAction.Hill: return "적이 돌아가거나 넘으며 느려진다 · 함정은 못 놓음";
                case TerrainAction.Path: return "적이 이 길로 몰린다 · 길 위에도 함정을 놓을 수 있음";
                case TerrainAction.Move: return "나무·바위 덩이를 옮겨 길을 막거나 연다";
                default: return "내가 지은 토벽·언덕·길을 없앤다";
            }
        }

        void ClearButton(Transform row)
        {
            var clear = Ui.Button(row, "선택 지우기", "다시 고르기", () => { picked.Clear(); message = null; Render(); });
            Ui.Size(clear, 208f, 64f);
            clear.interactable = picked.Count > 0;
        }

        // 칸 그림 범례 (그림 + 낱말, 설명문 없음). placed = 함정 탭의 '묻힘' 칸
        void LegendRow(string placed)
        {
            HorizontalLayoutGroup legend = Ui.Row(side, "Legend", UiTokens.S8);
            Ui.Size(legend, -1f, 32f);
            Legend(legend.transform, gridAllowed, "할 수 있음");
            Legend(legend.transform, gridSelected, "고름");
            Legend(legend.transform, gridBlocked, "막힘");
            if (placed != null) Legend(legend.transform, gridPlaced, placed);
        }

        // 값 줄: 방금 결과 / 고를 것 안내 / 안 되는 이유(붉게) / [골드] 값 + 남는 골드 (환불이면 돌려받는 골드)
        void PriceLine(bool ok, int cost, string why, string hint)
        {
            HorizontalLayoutGroup price = Ui.Row(side, "Price", UiTokens.S8);
            Ui.Size(price, -1f, 48f);
            if (message != null)
            {
                Ui.Icon(price.transform, "Result", messageBad ? UiArt.IconWarn : UiArt.IconCheck, 1f, messageBad ? UiTokens.Danger : UiTokens.Good);
                Text done = Ui.Label(price.transform, "Text", message, UiTokens.Text, messageBad ? UiTokens.Danger : UiTokens.Ink);
                Ui.Size(done, 680f, 48f);
            }
            else if (picked.Count == 0 || (action == TerrainAction.Move && terrainTab && picked.Count % 2 == 1))
            {
                Text text = Ui.Label(price.transform, "Hint", hint, UiTokens.Text, UiTokens.InkSoft);
                Ui.Size(text, 680f, 48f);
            }
            else if (!ok)
            {
                Text bad = Ui.Label(price.transform, "Reason", why, UiTokens.Text, UiTokens.Danger);
                Ui.Size(bad, 680f, 48f);
            }
            else
            {
                if (Ui.HudIcon(price.transform, "Gold Icon", "Icons/stat_gold", 32f) == null) Ui.Size(Ui.Label(price.transform, "Gold Word", "골드", UiTokens.Small, UiTokens.InkSoft), 44f, 48f);
                Text total = Ui.Label(price.transform, "Cost", cost < 0 ? "+" + (-cost).ToString("N0") : cost.ToString("N0"), UiTokens.H2, cost < 0 ? UiTokens.Good : UiTokens.Ink, TextAnchor.MiddleLeft, true);
                Ui.Size(total, total.preferredWidth + 8f, 48f);
                Text left = Ui.Label(price.transform, "Left", $"남는 골드 {context.Campaign.Gold - cost:N0}", UiTokens.Text, UiTokens.InkSoft);
                Ui.Size(left, 320f, 48f);
            }
        }

        static void Legend(Transform row, Sprite sprite, string word)
        {
            Image swatch = Ui.Image(row, "Swatch", sprite != null ? Color.white : UiTokens.InkSoft);
            swatch.sprite = sprite;
            Ui.Size(swatch, 24f, 24f);
            Text label = Ui.Label(row, "Word", word, UiTokens.Small, UiTokens.Ink);
            Ui.Size(label, label.preferredWidth + UiTokens.S16, 32f);
        }

        void Buy()
        {
            var def = context.Definitions.All<TrapDef>()[kind];
            var cells = new List<string>(picked);
            bool ok = session.TryBuyMany(def.Id, cells, storage, marked, out int placed, out int spent, out string reason, out bool error);
            if (error) { DataError(reason); return; }
            message = ok ? $"{placed}칸에 묻었습니다" + (spent > 0 ? $" · {spent:N0}골드" : "")
                : placed > 0 ? $"{placed}칸만 묻었습니다 · {reason}" : reason;
            messageBad = !ok;
            if (ok || placed > 0) picked.Clear();
            preparation.CancelQuote();
            PreparationPanel.RefreshSummary(source, context, preparation);
            Render();
        }

        void Build()
        {
            var cells = new List<string>(picked);
            TerrainAction done = action;
            bool ok = works.TryApply(done, cells, out List<string> changed, out int delta, out string reason);
            message = !ok ? reason
                : delta >= 0 ? $"{changed.Count}칸 {TerrainWorkTransactions.NameOf(done)} · {delta:N0}골드 돌려받음"
                : $"{changed.Count}칸 {TerrainWorkTransactions.NameOf(done)} · {-delta:N0}골드";
            messageBad = !ok;
            if (ok) picked.Clear();
            preparation.CancelQuote();
            PreparationPanel.RefreshSummary(source, context, preparation);
            Render();
            if (ok && done != TerrainAction.Demolish) PlayWorkers(done, changed);
        }

        // 병사 작업 연출 (K-030 시트): 토벽·길 = 삽질, 언덕 = 흙 나르기, 옮기기 = 통나무 나르기. 궁수·총병을 번갈아
        void PlayWorkers(TerrainAction what, List<string> cells)
        {
            string act = what == TerrainAction.Hill ? "earth" : what == TerrainAction.Move ? "log" : "dig";
            for (int k = 0; k < cells.Count; k++)
            {
                if (!tiles.TryGetValue(cells[k], out Image tile)) continue;
                Texture2D sheet = Tile($"worker_{act}_{(k % 2 == 0 ? "archer" : "gunner")}");
                float height = tile.rectTransform.rect.height * 1.6f;
                TerrainWorkerAnimation.Play(tile.rectTransform, sheet, WorkerSeconds, height);
            }
        }

        const float WorkerSeconds = 2.4f;   // 작업 연출 시간 (게임 시간과 상관없음)

        void DataError(string reason)
        {
            session.Close(); context.ReportDataError(reason);
            foreach (var b in shade.GetComponentsInChildren<Button>()) if (b.name != Ui.KeyPrefix + "닫기") b.interactable = false;
        }

        void Close()
        {
            session.CancelQuote();
            if (behind != null) behind.interactable = true;
            // 다 사라진 뒤에 지도 그림 텍스처·스프라이트를 지운다 (불러온 원본 그림은 지우지 않는다)
            UiAppear.Hide(shade, true, () =>
            {
                foreach (var item in owned) if (item != null) UnityEngine.Object.Destroy(item);
                owned.Clear();
            });
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(source.gameObject);
        }

        void Box(RectTransform map, string name, Vector2 center, Vector2 size, Color color)
        {
            Image image = Ui.Image(map, name, color);
            Ui.Place(image.rectTransform, new Vector2(.5f, .5f), center, size);
        }
    }
}
