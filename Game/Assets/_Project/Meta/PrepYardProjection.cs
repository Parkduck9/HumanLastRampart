using UnityEngine;

namespace HumanLastRampart.Meta
{
    // K-089 고정 그림과 3D 바닥의 공통 좌표. 화면 y는 위에서 아래로 센다.
    public static class PrepYardProjection
    {
        public const float Width = 3840f, Height = 1080f, Depth = 8f;
        public const float MinDepth = -0.3f;   // K-120: 앞쪽 건물(공방·상점) 문 앞까지 닿도록 화면 맨 아래(y≈1060)까지 허용
        public const float PixelsPerMetre = 219f / 1.7f;

        public static Vector2 Project(Vector2 ground)
        {
            float q = 1f + ground.y / Depth;
            return new Vector2(1920f + PixelsPerMetre * ground.x / q, -60f + 1080f / q);
        }

        public static Vector2 Ground(Vector2 pixel)
        {
            float q = 1080f / (pixel.y + 60f);
            return new Vector2((pixel.x - 1920f) * q / PixelsPerMetre, Depth * (q - 1f));
        }

        public static bool Walkable(Texture2D mask, Vector2 ground)
        {
            if (mask == null || ground.y < MinDepth || ground.y > Depth) return false;
            Vector2 p = Project(ground);
            if (p.x < 0f || p.x >= Width || p.y < 0f || p.y >= Height) return false;
            int x = Mathf.FloorToInt(p.x * mask.width / Width);
            int y = mask.height - 1 - Mathf.FloorToInt(p.y * mask.height / Height);
            return mask.GetPixel(x, y).r > 0.5f;
        }
    }
}
