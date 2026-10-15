using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 성벽·포탑 3D 배치 목업 (카드 K-001, D-062 3단계 앞부분)
    // - 성벽 윗면 바깥 모서리에 낮은 난간 벽 + 그 위 돌출부(총안 사이 돌), 대포 자리는 난간을 낮추고 벌린 사격구
    // - 네 모서리 포탑: 통로와 같은 높이의 발판(주인공이 그대로 지나감) + 바깥쪽에 높이 솟은 탑
    // - 크기는 병사 키(0.35m)에 맞춘 비율로 정한다. 시안 번호(Draft)로 비율을 바꿔 비교한다 (확정 전 목업)
    // - 질감은 아직 단색 회색 돌 (Codex 3D 질감 카드 K-005가 오면 입힌다)
    public sealed class BattleWorldView
    {
        // 0 = 끔(예전 난간 그림 판), 1 = 시안 1 '도트 비율'(허리 높이 난간), 2 = 시안 2 '실제 비율'(가슴 높이 난간·큰 탑)
        public static int Draft = 1;

        public struct Proportion
        {
            public string Name;
            public float ParapetHeight;   // 난간 벽 높이 (통로 위)
            public float ParapetDepth;    // 난간 벽 두께
            public float MerlonHeight;    // 돌출부 높이 (난간 위)
            public float MerlonWidth;
            public float MerlonGap;       // 돌출부 사이(총안) 폭
            public float BayWidth;        // 대포 사격구 폭
            public float BayHeight;       // 사격구에서 남는 난간 높이
            public float TowerPlatform;   // 모서리 발판 한 변
            public float TowerSize;       // 솟은 탑 한 변
            public float TowerRise;       // 통로 위로 솟은 높이
        }

        static readonly float S = 1.4f * BattleSim.WallFigureScale;   // 병사 키 (0.35m)

        public static readonly Proportion[] Drafts =
        {
            default,
            new Proportion { Name = "시안 1 도트 비율 (허리 높이 난간)", ParapetHeight = 0.30f * S, ParapetDepth = 0.30f, MerlonHeight = 0.30f * S, MerlonWidth = 0.75f * S, MerlonGap = 0.6f * S, BayWidth = 3.2f * S, BayHeight = 0.15f * S, TowerPlatform = 4.5f, TowerSize = 2.6f, TowerRise = 6f * S },
            new Proportion { Name = "시안 2 실제 비율 (가슴 높이 난간·큰 탑)", ParapetHeight = 0.55f * S, ParapetDepth = 0.40f, MerlonHeight = 0.45f * S, MerlonWidth = 1.1f * S, MerlonGap = 0.7f * S, BayWidth = 3.6f * S, BayHeight = 0.25f * S, TowerPlatform = 5.5f, TowerSize = 3.4f, TowerRise = 11f * S },
        };

        public static bool Active => Draft > 0 && Draft < Drafts.Length;
        public static Proportion Current => Drafts[math.clamp(Draft, 0, Drafts.Length - 1)];

        readonly GameObject root;
        readonly Material stone, stoneDark, roof;

        public int PieceCount { get; private set; }

        public BattleWorldView(BattleSim sim, Transform parent)
        {
            root = new GameObject("Battle World 3D (목업)");
            root.transform.SetParent(parent, false);
            if (!Active) return;

            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material lit = probe.GetComponent<Renderer>().sharedMaterial;
            Object.Destroy(probe);
            stone = new Material(lit) { name = "Stone (목업)", color = new Color(0.62f, 0.61f, 0.58f) };
            stoneDark = new Material(lit) { name = "Stone Dark (목업)", color = new Color(0.48f, 0.47f, 0.45f) };
            roof = new Material(lit) { name = "Roof (목업)", color = new Color(0.36f, 0.30f, 0.27f) };

            Proportion p = Current;
            float half = sim.CastleHalf;
            float top = BattleSim.WallHeight;

            // 성벽마다 대포 자리(성벽 따라 m)
            var bays = new List<float>[4];
            for (int s = 0; s < 4; s++) bays[s] = new List<float>();
            for (int i = 0; i < sim.CannonCount; i++)
            {
                BattleSim.CannonInfo c = sim.GetCannon(i);
                float3 r = WallGeometry.Right(c.Side);
                bays[c.Side].Add(c.Position.x * r.x + c.Position.z * r.z);
            }

            float platformHalf = p.TowerPlatform * 0.5f;
            for (int side = 0; side < 4; side++)
            {
                float3 n3 = WallGeometry.Outward(side);
                float3 r3 = WallGeometry.Right(side);
                var n = new Vector3(n3.x, 0f, n3.z);
                var r = new Vector3(r3.x, 0f, r3.z);
                Quaternion face = Quaternion.LookRotation(n);
                // 모서리 발판이 차지하는 끝은 비워 둔다
                float from = -half + platformHalf, to = half - platformHalf;

                // 난간 벽 (사격구에서는 낮게)
                float along = from;
                foreach (float bay in Sorted(bays[side]))
                {
                    float bayFrom = Mathf.Max(from, bay - p.BayWidth * 0.5f), bayTo = Mathf.Min(to, bay + p.BayWidth * 0.5f);
                    Parapet(n, r, face, half, top, along, bayFrom, p.ParapetHeight, p);
                    Parapet(n, r, face, half, top, bayFrom, bayTo, p.BayHeight, p);
                    along = bayTo;
                }

                Parapet(n, r, face, half, top, along, to, p.ParapetHeight, p);

                // 돌출부 (사격구 안에는 없음)
                float period = p.MerlonWidth + p.MerlonGap;
                for (float m = from + p.MerlonGap + p.MerlonWidth * 0.5f; m < to - p.MerlonWidth * 0.5f; m += period)
                {
                    bool inBay = false;
                    foreach (float bay in bays[side]) if (Mathf.Abs(m - bay) < p.BayWidth * 0.5f + p.MerlonWidth * 0.5f) inBay = true;
                    if (inBay) continue;
                    Box("Merlon", n * (half - p.ParapetDepth * 0.5f) + r * m + Vector3.up * (top + p.ParapetHeight + p.MerlonHeight * 0.5f),
                        face, new Vector3(p.MerlonWidth, p.MerlonHeight, p.ParapetDepth), stone);
                }
            }

            // 모서리 포탑: 통로 높이 발판(주인공이 지나감) + 바깥쪽 솟은 탑
            for (int side = 0; side < 4; side++)
            {
                float3 a = WallGeometry.Outward(side), b = WallGeometry.Outward((side + 1) % 4);
                var corner = new Vector3(a.x + b.x, 0f, a.z + b.z) * half;   // 성벽 바깥 모서리
                var outward = new Vector3(a.x + b.x, 0f, a.z + b.z).normalized;
                // 발판: 모서리를 중심으로, 위가 통로 높이
                var platformCenter = corner - outward * 0.7f;
                Box("Corner Platform", platformCenter + Vector3.up * (top * 0.5f), Quaternion.identity, new Vector3(p.TowerPlatform, top, p.TowerPlatform), stoneDark);
                // 솟은 탑: 발판 바깥쪽 끝 (통로 밖이라 길을 막지 않음)
                var towerCenter = corner + outward * (p.TowerSize * 0.5f * 0.9f);
                Box("Corner Tower", towerCenter + Vector3.up * ((top + p.TowerRise) * 0.5f), Quaternion.LookRotation(outward), new Vector3(p.TowerSize, top + p.TowerRise, p.TowerSize), stone);
                // 탑 위 돌출부 4면
                float towerTop = top + p.TowerRise;
                for (int k = 0; k < 4; k++)
                {
                    Quaternion q = Quaternion.LookRotation(outward) * Quaternion.Euler(0f, 90f * k, 0f);
                    Vector3 f = q * Vector3.forward, rr = q * Vector3.right;
                    for (int j = -1; j <= 1; j++)
                        Box("Tower Merlon", towerCenter + f * (p.TowerSize * 0.5f - p.ParapetDepth * 0.5f) + rr * (j * p.TowerSize * 0.32f) + Vector3.up * (towerTop + p.MerlonHeight * 0.5f),
                            q, new Vector3(p.MerlonWidth * 1.1f, p.MerlonHeight, p.ParapetDepth), stone);
                }

                // 탑 지붕 (낮은 피라미드 대신 납작한 상자 — 목업)
                Box("Tower Roof", towerCenter + Vector3.up * (towerTop + p.MerlonHeight + 0.15f), Quaternion.LookRotation(outward), new Vector3(p.TowerSize * 0.7f, 0.3f, p.TowerSize * 0.7f), roof);
            }
        }

        static List<float> Sorted(List<float> list)
        {
            var copy = new List<float>(list);
            copy.Sort();
            return copy;
        }

        void Parapet(Vector3 n, Vector3 r, Quaternion face, float half, float top, float from, float to, float height, Proportion p)
        {
            if (to - from < 0.05f || height <= 0.001f) return;
            float mid = (from + to) * 0.5f;
            Box("Parapet", n * (half - p.ParapetDepth * 0.5f) + r * mid + Vector3.up * (top + height * 0.5f), face, new Vector3(to - from, height, p.ParapetDepth), stone);
        }

        void Box(string name, Vector3 position, Quaternion rotation, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            PieceCount++;
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            if (stone != null) Object.Destroy(stone);
            if (stoneDark != null) Object.Destroy(stoneDark);
            if (roof != null) Object.Destroy(roof);
        }
    }
}
