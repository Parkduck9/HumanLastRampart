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
    // K-049 '지형' 탭 (D-077 ④): 같은 지도·칸에서 토벽·언덕을 골드로 짓는다 (TerrainWorkTransactions). 무작위 지형(언덕·나무·바위)도 지도에 보인다.
    //   기능만 넣었고 화면 모양은 K-047(모든 화면 UI 다시 만들기)이 다듬는다
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
        // 지형 탭 (K-049)
        readonly TerrainWorkTransactions works;
        bool terrainTab;
        TerrainAction action = TerrainAction.EarthWall;   // K-051: 토벽·언덕·길 내기·옮기기·철거
        Sprite earthwallIcon, raisedHillIcon, treesIcon, rocksIcon, grassIcon;
        readonly Sprite[] pathIcons = new Sprite[16];     // K-029 흙길: 북1·동2·남4·서8 연결 비트

        public static void Show(Button source, IScreenContext context, TrapPreparationTransactions session, PreparationTransactions preparation)
        { if (source != null && source.IsInteractable()) new TrapPreparationPanel(source, context, session, preparation); }

        TrapPreparationPanel(Button source, IScreenContext context, TrapPreparationTransactions session, PreparationTransactions preparation)
        {
            this.source = source; this.context = context; this.session = session; this.preparation = preparation;
            works = new TerrainWorkTransactions(session);
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
                TerrainLayout shape = session.Layout;
                foreach (var h in shape.Hills) Box(map, "Hill", new Vector2(h.x, h.z) * scale, Vector2.one * h.y * 2f * scale, new Color(.33f, .43f, .25f));
                foreach (int c in shape.TreeCells) Box(map, "Trees", shape.Center(c) * scale, Vector2.one * shape.CellSize * scale, new Color(.14f, .30f, .14f));
                foreach (int c in shape.RockCells) Box(map, "Rocks", shape.Center(c) * scale, Vector2.one * shape.CellSize * scale, new Color(.45f, .45f, .42f));
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
                        byte kind = shape.KindAt(x, z);
                        Color32[] decor = kind == TerrainCell.Trees ? treePx : kind == TerrainCell.Rocks ? rockPx : null;
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
            message = !terrainTab ? "지도에서 칸을 더 고르거나 한 번에 구매하세요."
                : action == TerrainAction.Move ? (picked.Count % 2 == 1 ? "이제 놓을 빈 칸을 고르세요." : "옮길 나무·바위 덩이 칸을 더 고르거나 [적용]을 누르세요.")
                : "칸을 더 고르거나 [적용]을 누르세요.";
            Render();
        }

        void PaintTiles()
        {
            foreach (var pair in tiles)
            {
                string id = pair.Key; Image image = pair.Value;
                // 함정 탭: 함정을 놓을 수 있는 칸 / 지형 탭(K-049): 토벽·언덕을 지을 수 있는 칸
                bool allowed = terrainTab ? works.CanTarget(action, id, picked) : session.IsPlaceable(id);
                bool occupied = !terrainTab && session.Occupied(id), chosen = picked.Contains(id);
                Sprite sprite = !allowed ? gridBlocked : occupied ? gridPlaced : chosen ? gridSelected : gridAllowed;
                if (sprite != null) { image.sprite = sprite; image.color = Color.white; }
                else image.color = !allowed ? new Color(.30f, .24f, .18f, .7f) : occupied ? new Color(.55f, .32f, .26f, .9f) : chosen ? new Color(.24f, .45f, .70f, .9f) : new Color(.2f, .28f, .22f, .45f);
                image.GetComponent<Button>().interactable = allowed && !occupied;
                PaintWork(image, id);
                occupied = session.Occupied(id);
                // 설치된 칸에는 묻힌 함정 표시
                Transform mark = image.transform.Find("Buried");
                if (occupied && mark == null && trapBuried != null)
                {
                    var icon = Make(image.transform, "Buried", typeof(Image)); Stretch(icon);
                    icon.GetComponent<Image>().sprite = trapBuried; icon.GetComponent<Image>().raycastTarget = false;
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
                var r = Make(image.transform, "Work", typeof(Image)); Stretch(r);
                r.GetComponent<Image>().raycastTarget = false;
                mark = r;
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
            if (!TrapGrid.TryParse(id, out int side, out int column, out int row)) return 0;
            Vector2 c = TrapGrid.Center(session.Terrain, side, column, row);
            float step = shape.CellSize;
            int bits = 0;
            if (shape.KindAt(c.x, c.y + step) == TerrainCell.Path) bits |= 1;
            if (shape.KindAt(c.x + step, c.y) == TerrainCell.Path) bits |= 2;
            if (shape.KindAt(c.x, c.y - step) == TerrainCell.Path) bits |= 4;
            if (shape.KindAt(c.x - step, c.y) == TerrainCell.Path) bits |= 8;
            return bits;
        }

        // ── 오른쪽 목록 (고를 때마다 다시 그린다 — 지도는 칸 색만 바꾼다) ──
        void Render()
        {
            foreach (Transform child in side) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            PaintTiles();
            AddButton(side, terrainTab ? "탭: 함정 ▷ [지형 손질]" : "탭: [함정] ◁ 지형 손질", () =>
            {
                terrainTab = !terrainTab; picked.Clear();
                message = terrainTab ? "할 일을 고르고 지도에서 칸을 고르세요. 골드만 듭니다 (철거하면 절반 환불)." : "지도에서 설치할 칸을 여러 개 눌러 고른 뒤 한 번에 구매하세요. 환불은 불가능합니다.";
                Render();
            });
            if (terrainTab) { RenderTerrainTab(); return; }
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

        // 지형 손질 탭 (D-077, K-049): 토벽(길을 막음·뒤는 총·화살 엄폐) / 언덕(막음·넘으면 느려짐). 함정 칸 격자 위에 골드로
        void RenderTerrainTab()
        {
            var terrain = session.Terrain;
            Label(side, $"지형 손질 · 다음 지형 {terrain.DisplayName} · 골드 {context.Campaign.Gold:N0}", 56, UiStyle.Brass, 28);
            int price = TerrainWorkTransactions.Price(action);
            string priceText = action == TerrainAction.Demolish ? $"낸 골드의 {TerrainRules.RefundRatio * 100f:0}% 환불"
                : action == TerrainAction.Move ? $"한 칸 {price}골드" : $"칸당 {price}골드";
            AddButton(side, $"할 일: {TerrainWorkTransactions.NameOf(action)} · {priceText} (눌러 바꾸기)", () =>
            {
                action = (TerrainAction)(((int)action + 1) % 5); picked.Clear(); Render();
            });
            Label(side, Help(action) + "\n성 둘레 언덕·나무·바위는 새 게임마다 무작위입니다.", 110, UiStyle.TextDim, 20);

            bool ok = works.CanApply(action, picked, out int cost, out string why);
            string sum = picked.Count == 0 ? "고른 칸 없음"
                : cost < 0 ? $"고른 칸 {picked.Count}개 · 환불 {-cost:N0}골드 · 보유 {context.Campaign.Gold:N0}골드"
                : $"고른 칸 {picked.Count}개 · 합계 {cost:N0}골드 · 보유 {context.Campaign.Gold:N0}골드";
            Label(side, sum + (picked.Count > 0 && !ok ? "\n" + why : ""), 76, picked.Count > 0 && !ok ? UiStyle.Danger : UiStyle.Body, 24);
            string costText = cost < 0 ? $"환불 {-cost:N0}골드" : $"{cost:N0}골드";
            var build = AddButton(side, picked.Count > 0 ? $"적용: {TerrainWorkTransactions.NameOf(action)} ({picked.Count}칸 · {costText})" : $"적용: {TerrainWorkTransactions.NameOf(action)}", Build);
            build.interactable = ok;
            if (ok) UiStyle.SetPrimary(build);
            var clear = AddButton(side, "선택 지우기", () => { picked.Clear(); message = "선택을 지웠습니다."; Render(); });
            clear.interactable = picked.Count > 0;
            Label(side, message, 96, UiStyle.Body, 22);
            AddButton(side, "닫기", Close);
        }

        static string Help(TerrainAction action)
        {
            switch (action)
            {
                case TerrainAction.EarthWall: return "토벽: 적이 돌아가거나(50%) 넘거나(30%, 느려짐) 몇 초 때려 허문다(20%).\n토벽 뒤에 숨은 적은 총·화살을 맞지 않는다 — 대포·마법은 맞는다.";
                case TerrainAction.Hill: return "언덕: 적이 돌아가거나(70%) 넘는다(30%, 느려짐). 함정을 놓을 수 없다.";
                case TerrainAction.Path: return "길 내기: 빈 땅·언덕 칸을 흙길로. 적이 이 길을 더 좋아하고, 길 위에는 함정을 놓을 수 있다.";
                case TerrainAction.Move: return "나무·바위 옮기기: 옮길 덩이 칸 → 놓을 빈 칸 순서로 짝을 골라 [적용]. 길을 막거나 연다.";
                default: return "철거: 내가 지은 토벽·언덕·길을 없앤다. 낸 골드의 절반을 돌려받는다.";
            }
        }

        void Build()
        {
            var cells = new List<string>(picked);
            TerrainAction done = action;
            bool ok = works.TryApply(done, cells, out List<string> changed, out int delta, out string reason);
            message = !ok ? reason
                : delta >= 0 ? $"{TerrainWorkTransactions.NameOf(done)} {changed.Count}칸 · 환불 {delta:N0}골드"
                : $"{TerrainWorkTransactions.NameOf(done)} {changed.Count}칸 완료 · {-delta:N0}골드 사용 · 철거하면 절반 환불";
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
