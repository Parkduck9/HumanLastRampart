using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.UI
{
    // K-125 터치 조작 그림 (Resources/UiKit/Touch/<이름>.png, 3배 그림 = 화면 1px). Point · Clamp · 크기 그대로.
    // 9칸 늘이기는 move_slider_track 하나뿐 (왼쪽·아래·오른쪽·위 24px, 가로만 늘임). 둥근 것들은 늘이지 않는다 (manifest)
    public static class TouchArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out Sprite cached) && cached != null) return cached;
            var texture = Resources.Load<Texture2D>("UiKit/Touch/" + name);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Vector4 border = name == "move_slider_track" ? new Vector4(24f, 24f, 24f, 24f) : Vector4.zero;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            cache[name] = sprite;
            return sprite;
        }

        // 한 그림 안의 일부 칸 (이동 패드 그림 위·아래 표식용). 원점은 왼쪽 아래 화소
        public static Sprite Crop(string name, string key, Rect pixels)
        {
            string id = name + "#" + key;
            if (cache.TryGetValue(id, out Sprite cached) && cached != null) return cached;
            Sprite whole = Get(name);
            if (whole == null) return null;
            Sprite sprite = Sprite.Create(whole.texture, pixels, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = id;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            cache[id] = sprite;
            return sprite;
        }

        // 행동 기호 (48×48)
        public static Sprite Glyph(string id) => Get("glyph_" + id);

        // 파문 4칸 · 길게 누르기 고리 8칸: 한 번 불러 모아 두고 이름 글자를 매 프레임 만들지 않는다
        static Sprite[] ripples, rings;

        public static Sprite Ripple(int frame)
        {
            if (ripples == null || ripples[0] == null)
            {
                ripples = new Sprite[4];
                for (int i = 0; i < 4; i++) ripples[i] = Get("tap_ripple_" + (i + 1));
            }

            return ripples[Mathf.Clamp(frame, 0, 3)];
        }

        public static Sprite Ring(int step)
        {
            if (rings == null || rings[0] == null)
            {
                rings = new Sprite[8];
                for (int i = 0; i < 8; i++) rings[i] = Get("long_press_" + (i + 1));
            }

            return rings[Mathf.Clamp(step - 1, 0, 7)];
        }
    }
}
