using UnityEngine;

namespace HumanLastRampart.Combat.Render
{
    // 땅 위 따뜻한 빛 웅덩이 모음 (W15, 사용자 2026-10-22 "불 아래 웅덩이가 확실히 보이게"): 불 조각·불바다·횃불·폭발이 Add로 자리를 남기면
    // 프레임 끝(Flush)에 인스턴싱 한 번으로 눕힌 부드러운 덧칠 판을 그린다. 빛 목록(HlrLights)은 그림 쪽 빛에, 이쪽은 땅 쪽 웅덩이에 쓴다
    // 상한 PC 96 / 모바일 24. 낮에는 아주 약하게(0.25), 지도 모드에서는 그리지 않는다
    public static class GroundGlow
    {
        public const int Capacity = 128;
        public static readonly Color Fire = new Color(1f, 0.52f, 0.2f);
        public static readonly Color Torch = new Color(1f, 0.6f, 0.28f);

        static readonly int PosId = Shader.PropertyToID("_GlowPos");
        static readonly int ColId = Shader.PropertyToID("_GlowCol");
        static readonly int NightId = Shader.PropertyToID("_Night");

        static readonly Vector4[] pos = new Vector4[Capacity];
        static readonly Vector4[] col = new Vector4[Capacity];
        static int count;
        static Material material;
        static Mesh quad;
        static GraphicsBuffer posBuffer, colBuffer;
        static bool failed;
        static readonly Bounds bounds = new Bounds(Vector3.zero, new Vector3(2000f, 20f, 2000f));

        // 저사양 여부는 한 번 확인해 두고(Flush 때 갱신), 추가할 때마다 다시 보지 않는다
        static int max = 96;
        public static int Max => max;
        public static bool Full => count >= max;
        public static int Count => count;

        // 프레임 시작(또는 시험)에서 상한을 다시 정한다
        public static void RefreshCap() { max = HlrLights.Low ? 24 : 96; }

        // 땅 위 웅덩이 하나. radius = 반지름(m), intensity = 색에 곱할 세기 (불 조각 0.3 안팎: 겹치면 더해진다)
        public static void Add(Vector3 position, float radius, Color color, float intensity)
        {
            if (failed || count >= Max || radius <= 0.01f || intensity <= 0.001f) return;
            pos[count] = new Vector4(position.x, position.y, position.z, radius);
            col[count] = new Vector4(color.r * intensity, color.g * intensity, color.b * intensity, Mathf.Abs(position.x * 0.37f + position.z * 0.61f) % 1f);
            count++;
        }

        // 프레임 끝: 모인 웅덩이를 그리고 목록을 비운다. 지도 모드·낮이면 버리기만
        public static void Flush(bool mapMode, bool night)
        {
            int n = count;
            count = 0;
            if (n <= 0 || mapMode || failed) return;
            if (!Ensure()) return;
            posBuffer.SetData(pos, 0, 0, n);
            colBuffer.SetData(col, 0, 0, n);
            material.SetFloat(NightId, night ? 1f : 0.25f);
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, n, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }

        static bool Ensure()
        {
            if (material != null) return true;
            Shader shader = Resources.Load<Shader>("GroundGlow");
            if (shader == null) { failed = true; return false; }
            material = new Material(shader) { name = "GroundGlow (runtime)" };
            quad = new Mesh { name = "GroundGlow quad (runtime)" };
            quad.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quad.bounds = bounds;
            posBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Capacity, 16);
            colBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Capacity, 16);
            material.SetBuffer(PosId, posBuffer);
            material.SetBuffer(ColId, colBuffer);
            return true;
        }

        public static void Release()
        {
            count = 0;
            posBuffer?.Release(); colBuffer?.Release();
            posBuffer = null; colBuffer = null;
            if (material != null) Object.Destroy(material);
            if (quad != null) Object.Destroy(quad);
            material = null; quad = null;
        }
    }
}
