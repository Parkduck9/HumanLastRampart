using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    // 정비 마당 (D-076): 성벽 안 작은 마당을 옆에서 본 한 줄 길. 주인공이 A/D(←/→)로 좌우로 걷고,
    // 장소 가까이에서 E(또는 스페이스)를 누르면 그 장소 일을 연다. 장소: 성벽 수리 · 병영 · 함정 공방 · 성벽 계단.
    // 장소 일은 기존 창(PreparationPanel·TrapPreparationPanel)을 그대로 쓴다 — 마당은 길과 입구만.
    // 그림은 Codex K-028 확정 전이라 지금은 색 도형 + 주인공 시트 (확정되면 바꿔 끼움)
    public sealed class PrepYard : MonoBehaviour
    {
        public sealed class Station
        {
            public string Name;
            public float X;               // 마당 띠에서 가로 위치
            public Color Color;
            public Action Use;
            public RectTransform Sign;
        }

        const float YardWidth = 3600f;     // 마당 띠 길이 (화면 1920 기준 단위)
        const float GroundY = 250f;        // 바닥 높이 (화면 아래에서)
        const float WalkSpeed = 520f;      // 초당 (화면 단위) — 마당 끝에서 끝까지 약 7초
        const float Reach = 170f;          // 이 거리 안이면 상호작용
        const float HeroHeight = 300f;
        const float WalkFps = 8f;

        readonly List<Station> stations = new List<Station>();
        RectTransform strip, hero, prompt;
        RawImage heroImage;
        Texture2D heroSheet;
        int sheetColumns = 1, sheetRows = 1;
        float heroX = 900f;
        bool facingRight = true;
        CanvasGroup blocker;               // 창(팝업)이 열려 있으면 비활성 — 그때는 움직이지 않는다
        GameObject confirm;

        public int StationCount => stations.Count;
        public float HeroX => heroX;

        // backdrop = 화면에 K-027 정비 배경 그림이 깔려 있음 → 하늘·성벽 면·바닥 색 띠를 그리지 않고 걷는 길만 옅게
        public static PrepYard Build(ScreenView view, UnitDef heroUnit, bool backdrop = false)
        {
            RectTransform layer = view.AddBackdropLayer("Prep Yard");
            var yard = layer.gameObject.AddComponent<PrepYard>();
            yard.blocker = view.PanelGroup;
            yard.heroSheet = heroUnit != null ? heroUnit.Sheet : null;
            yard.hasBackdrop = backdrop;
            yard.BuildScene(layer);
            return yard;
        }

        bool hasBackdrop;

        public void AddStation(string name, float x, Color color, Action use)
        {
            var station = new Station { Name = name, X = x, Color = color, Use = use };
            if (!hasBackdrop)
            {
                // 건물 자리 (배경 그림이 없을 때만 임시 도형): 몸체 + 지붕 띠
                RectTransform body = Block(strip, name, new Vector2(x, GroundY + 140f), new Vector2(260f, 280f), color);
                Block(body, "Roof", new Vector2(0f, 150f), new Vector2(300f, 30f), Color.Lerp(color, Color.black, 0.35f), true);
            }

            // K-047: 장소마다 나무 간판 (Ui Plate — 도트 계단 모서리) + 이름은 세계 글꼴. 바닥에는 서는 자리 황동 빛
            Block(strip, name + " Spot", new Vector2(x, GroundY - 20f), new Vector2(192f, UiTokens.Dot * 3f), UiTokens.Alpha(UiTokens.Brass, 0.55f));
            Image plate = Ui.Panel(strip, name + " Plate", UiPanel.Plate);
            plate.raycastTarget = false;
            Ui.Place(plate.rectTransform, Vector2.zero, new Vector2(x, GroundY + 330f), new Vector2(304f, 64f), new Vector2(0.5f, 0.5f));
            Text sign = Ui.Label(plate.transform, "Name", name, UiTokens.H2, UiTokens.BrassLight, TextAnchor.MiddleCenter, true);
            sign.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Stretch(sign.rectTransform);
            station.Sign = plate.rectTransform;
            stations.Add(station);
            hero.SetAsLastSibling();   // 주인공은 건물 앞에
            prompt.SetAsLastSibling();
        }

        void BuildScene(RectTransform layer)
        {
            strip = new GameObject("Strip", typeof(RectTransform)).GetComponent<RectTransform>();
            strip.SetParent(layer, false);
            strip.anchorMin = strip.anchorMax = strip.pivot = Vector2.zero;
            strip.sizeDelta = new Vector2(YardWidth, 1080f);
            // 하늘 → 안쪽 성벽 면 → 바닥 (임시 색). 배경 그림이 있으면 그림이 대신하고 걷는 길만 옅게 남긴다
            if (!hasBackdrop)
            {
                Block(strip, "Sky", new Vector2(YardWidth / 2f, 820f), new Vector2(YardWidth, 520f), new Color(0.32f, 0.40f, 0.48f));
                Block(strip, "Inner Wall", new Vector2(YardWidth / 2f, GroundY + 230f), new Vector2(YardWidth, 460f), new Color(0.36f, 0.35f, 0.33f));
                for (float x = 60f; x < YardWidth; x += 180f)   // 성벽 돌 줄눈 느낌
                    Block(strip, "Joint", new Vector2(x, GroundY + 230f), new Vector2(4f, 460f), new Color(0f, 0f, 0f, 0.18f));
                Block(strip, "Ground", new Vector2(YardWidth / 2f, GroundY / 2f), new Vector2(YardWidth, GroundY), new Color(0.38f, 0.31f, 0.22f));
            }

            if (!hasBackdrop) Block(strip, "Path", new Vector2(YardWidth / 2f, GroundY - 20f), new Vector2(YardWidth, 40f), new Color(0.50f, 0.45f, 0.38f));   // 배경 그림이 있으면 그림 속 길을 쓴다

            var heroObj = new GameObject("Hero", typeof(RectTransform), typeof(RawImage));
            heroObj.transform.SetParent(strip, false);
            hero = (RectTransform)heroObj.transform;
            hero.anchorMin = hero.anchorMax = Vector2.zero;
            hero.pivot = new Vector2(0.5f, 0f);
            hero.sizeDelta = new Vector2(HeroHeight * 2f / 3f, HeroHeight);
            heroImage = heroObj.GetComponent<RawImage>();
            heroImage.raycastTarget = false;
            if (heroSheet != null)
            {
                heroImage.texture = heroSheet;
                sheetColumns = Mathf.Max(1, Mathf.RoundToInt(heroSheet.width / 256f));
                sheetRows = Mathf.Max(1, Mathf.RoundToInt(heroSheet.height / 384f));
            }
            else heroImage.color = new Color(0.8f, 0.7f, 0.5f);

            // 가까이 가면 간판 위에 [E] 키 표시 (S-15: 지금 할 수 있는 행동만)
            prompt = Ui.KeyHint(strip, "E", "들어가기");
            prompt.anchorMin = prompt.anchorMax = Vector2.zero;
            prompt.pivot = new Vector2(0.5f, 0f);
            prompt.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            HorizontalLayoutGroup hints = Ui.Row(layer, "Key Hints", UiTokens.S24);
            Ui.Place((RectTransform)hints.transform, Vector2.zero, new Vector2(UiTokens.S32, UiTokens.S32), new Vector2(400f, 33f), Vector2.zero);
            Ui.KeyHint(hints.transform, "A · D", "걷기");
            Place();
        }

        void Update()
        {
            bool free = blocker == null || blocker.interactable;
            if (confirm != null) free = false;
            var keyboard = Keyboard.current;
            float move = 0f;
            if (free && keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move += 1f;
            }

            if (move != 0f)
            {
                heroX = Mathf.Clamp(heroX + move * WalkSpeed * Time.unscaledDeltaTime, 120f, YardWidth - 120f);
                facingRight = move > 0f;
            }

            Station near = Nearest();
            if (free && near != null && keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                near.Use?.Invoke();

            Animate(move != 0f);
            Place();
            bool show = free && near != null;
            if (prompt.gameObject.activeSelf != show)
            {
                if (show) UiAppear.Show(prompt.gameObject);   // 자리는 매 프레임 따라가므로 밀지 않고 나타나기만
                else prompt.gameObject.SetActive(false);
            }

            if (near != null) prompt.anchoredPosition = new Vector2(near.X, GroundY + 378f);
            foreach (Station s in stations)
                if (s.Sign != null) s.Sign.localScale = Vector3.one * Mathf.MoveTowards(s.Sign.localScale.x, s == near ? 1.08f : 1f, Time.unscaledDeltaTime * 1.2f);   // 가까운 간판이 살짝 커진다
        }

        // 시험·마우스용: 장소 이름으로 바로 들어가기 (E와 같은 길)
        public bool Use(string stationName)
        {
            Station station = stations.Find(s => s.Name == stationName);
            if (station == null) return false;
            heroX = station.X;
            station.Use?.Invoke();
            return true;
        }

        public Station Nearest()
        {
            Station best = null;
            float bestDistance = Reach;
            foreach (Station s in stations)
            {
                float d = Mathf.Abs(s.X - heroX);
                if (d <= bestDistance) { bestDistance = d; best = s; }
            }

            return best;
        }

        // 계단: '다음 전투를 시작할까요?' 확인 창 (D-076)
        public void AskNextBattle(Action start)
        {
            if (confirm != null) return;
            // 화면 맨 위 층 (Ui.Confirm: 어둠막 + 양피지 확인 창, 출정 = 황동)
            confirm = Ui.Confirm(transform.parent, "성벽 위로 올라갈까요?", "다음 전투가 바로 시작됩니다",
                "다음 전투 시작", "출정", () => { confirm = null; start?.Invoke(); },
                "더 둘러보기", "더 둘러보기", () => confirm = null);
        }

        // 주인공: 걸으면 옆모습 걷기 행(1), 서 있으면 앞모습 행(3) 첫 칸. 시트 옆모습은 왼쪽을 보므로 오른쪽이면 뒤집는다
        void Animate(bool walking)
        {
            if (heroSheet == null) return;
            int row = walking ? 1 : Mathf.Min(3, sheetRows - 1);
            int column = walking ? (int)(Time.unscaledTime * WalkFps) % sheetColumns : 0;
            float w = 1f / sheetColumns, h = 1f / sheetRows;
            bool flip = walking && facingRight;
            heroImage.uvRect = new UnityEngine.Rect(flip ? (column + 1) * w : column * w, 1f - (row + 1) * h, flip ? -w : w, h);
        }

        // 마당 띠를 옮겨 주인공이 화면 가운데쯤 오게 (끝에서는 멈춤)
        void Place()
        {
            hero.anchoredPosition = new Vector2(heroX, GroundY - 30f);
            var parent = (RectTransform)strip.parent;
            float view = parent.rect.width > 1f ? parent.rect.width : 1920f;
            float offset = Mathf.Clamp(view / 2f - heroX, view - YardWidth, 0f);
            strip.anchoredPosition = new Vector2(offset, 0f);
        }

        static RectTransform Block(RectTransform parent, string name, Vector2 center, Vector2 size, Color color, bool centered = false)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = centered ? new Vector2(0.5f, 0.5f) : Vector2.zero;
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            var image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }
    }
}
