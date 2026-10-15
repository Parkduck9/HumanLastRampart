using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 성벽·포탑 3D 배치 목업 (카드 K-001, D-062 3단계 앞부분)
    // - 성벽 윗면 바깥 모서리에 낮은 난간 벽 + 그 위 돌출부(총안 사이 돌), 대포 자리는 난간을 낮추고 벌린 사격구
    // - 네 모서리 포탑: 통로와 같은 높이의 발판(주인공이 그대로 지나감) + 바깥쪽에 높이 솟은 탑
    // - 크기는 병사 키(0.35m)에 맞춘 비율로 정한다. 시안 번호(Draft)로 비율을 바꿔 비교한다 (확정 전 목업)
    // - 질감: 재질 공용 코드(BattleWorldMaterials, K-007)가 Art/Battle/Resources/BattleArt/World의 그림을 읽는다. 그림이 없으면 단색 돌
    // - 확정 D-068: 시안 1(도트 비율), 모서리 포탑은 바깥쪽에 솟음. 시안 2는 비교용으로만 남김
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

        const float WallThickness = 2f;   // 성벽 몸통 두께 (BattleView 상자와 같게)
        const float SkinLift = 0.01f;     // 덮는 판 두께 — 몸통 면과 겹쳐 깜빡이지 않게 1cm 띄운다
        // 난간 그림(벽돌 8줄, 한 줄 0.25m)에서 줄눈 바로 위 높이(m). 난간+돌출부(0.21m)가 벽돌 한 줄 안에 들어가
        // 가로 줄눈이 난간을 가로지르지 않는다. 그림을 바꾸면 이 값을 다시 맞춘다 (D-070)
        const float BrickRowStart = 40f / 32f;   // 아래에서 40화소 (줄눈 39화소 바로 위, 위 줄눈 47화소 아래)

        readonly GameObject root;
        readonly BattleWorldMaterials materials;
        readonly List<Renderer>[] sideSkins = new List<Renderer>[4];

        public int PieceCount { get; private set; }

        public BattleWorldView(BattleSim sim, Transform parent)
        {
            root = new GameObject("Battle World 3D (목업)");
            root.transform.SetParent(parent, false);
            if (!Active) return;

            materials = new BattleWorldMaterials();

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

                // 통로 바닥·성벽 바깥면: 성벽 몸통(BattleView의 상자 하나)은 면마다 질감을 못 나누므로 얇은 판을 덮는다 (그림 있을 때만)
                var tinted = new List<Renderer>();
                if (materials.HasArt(BattleWorldMaterials.Surface.WalkwayStone))
                {
                    float walkLength = half * 2f - WallThickness * 2f;   // 모서리 네모는 발판이 덮는다 — 두 판이 겹쳐 깜빡이지 않게
                    tinted.Add(Box("Walkway", n * (half - WallThickness * 0.5f) + Vector3.up * (top + SkinLift * 0.5f), face,
                        new Vector3(walkLength, SkinLift, WallThickness), BattleWorldMaterials.Surface.WalkwayStone,
                        new Vector2(walkLength, WallThickness), new Vector2(-walkLength * 0.5f, 0f)));
                }
                if (materials.HasArt(BattleWorldMaterials.Surface.WallStone))
                {
                    float faceLength = half * 2f + WallThickness;
                    tinted.Add(Box("Wall Face", n * (half + SkinLift * 0.5f) + Vector3.up * (top * 0.5f), face,
                        new Vector3(faceLength, top, SkinLift), BattleWorldMaterials.Surface.WallStone));
                }
                sideSkins[side] = tinted;

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
                    // 돌출부도 난간과 같은 벽돌 줄에서 이어진다 (위치 = 성벽 따라 m, 높이 = 난간 위)
                    Box("Merlon", n * (half - p.ParapetDepth * 0.5f) + r * m + Vector3.up * (top + p.ParapetHeight + p.MerlonHeight * 0.5f),
                        face, new Vector3(p.MerlonWidth, p.MerlonHeight, p.ParapetDepth), BattleWorldMaterials.Surface.ParapetSide,
                        new Vector2(p.MerlonWidth, p.MerlonHeight), new Vector2(m - p.MerlonWidth * 0.5f, BrickRowStart + p.ParapetHeight));
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
                Box("Corner Platform", platformCenter + Vector3.up * (top * 0.5f), Quaternion.identity, new Vector3(p.TowerPlatform, top, p.TowerPlatform), BattleWorldMaterials.Surface.TowerWall);
                // 솟은 탑: 발판 바깥쪽 끝 (통로 밖이라 길을 막지 않음)
                var towerCenter = corner + outward * (p.TowerSize * 0.5f * 0.9f);
                Box("Corner Tower", towerCenter + Vector3.up * ((top + p.TowerRise) * 0.5f), Quaternion.LookRotation(outward), new Vector3(p.TowerSize, top + p.TowerRise, p.TowerSize), BattleWorldMaterials.Surface.TowerWall);
                // 탑 위 돌출부 4면
                float towerTop = top + p.TowerRise;
                for (int k = 0; k < 4; k++)
                {
                    Quaternion q = Quaternion.LookRotation(outward) * Quaternion.Euler(0f, 90f * k, 0f);
                    Vector3 f = q * Vector3.forward, rr = q * Vector3.right;
                    for (int j = -1; j <= 1; j++)
                        Box("Tower Merlon", towerCenter + f * (p.TowerSize * 0.5f - p.ParapetDepth * 0.5f) + rr * (j * p.TowerSize * 0.32f) + Vector3.up * (towerTop + p.MerlonHeight * 0.5f),
                            q, new Vector3(p.MerlonWidth * 1.1f, p.MerlonHeight, p.ParapetDepth), BattleWorldMaterials.Surface.ParapetSide);
                }

                // 탑 지붕 (낮은 피라미드 대신 납작한 상자 — 목업)
                Box("Tower Roof", towerCenter + Vector3.up * (towerTop + p.MerlonHeight + 0.15f), Quaternion.LookRotation(outward), new Vector3(p.TowerSize * 0.7f, 0.3f, p.TowerSize * 0.7f), BattleWorldMaterials.Surface.TowerRoof);
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
            // 그림 시작을 성벽 따라 위치(from)에 맞춰 이웃 조각과 끊김 없이, 높이는 벽돌 한 줄 안쪽에서 시작
            Box("Parapet", n * (half - p.ParapetDepth * 0.5f) + r * mid + Vector3.up * (top + height * 0.5f), face, new Vector3(to - from, height, p.ParapetDepth), BattleWorldMaterials.Surface.ParapetSide,
                new Vector2(to - from, height), new Vector2(from, BrickRowStart));
        }

        // 크기만 주면 면 크기 = 가로·세로 (그림 시작은 0)
        Renderer Box(string name, Vector3 position, Quaternion rotation, Vector3 size, BattleWorldMaterials.Surface surface)
            => Box(name, position, rotation, size, surface, new Vector2(size.x, size.y), Vector2.zero);

        // uvSize: 그림이 붙는 면의 가로·세로(m), uvOffset: 그림을 밀어 시작할 거리(m)
        // 유니티 기본 상자에서 통로 쪽(안쪽) 면은 가로 그림 방향이 성벽 따라 + 방향과 같다 — 위치를 넣으면 조각끼리 이어진다
        Renderer Box(string name, Vector3 position, Quaternion rotation, Vector3 size, BattleWorldMaterials.Surface surface, Vector2 uvSize, Vector2 uvOffset)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = materials.Get(surface);
            materials.Apply(renderer, surface, uvSize, uvOffset);   // 1m당 32화소로 반복 (그림이 있을 때만)
            PieceCount++;
            return renderer;
        }

        // 성벽 체력 색(BattleView): 덮은 판에도 같은 붉은 기를 곱한다. ratio 1 = 멀쩡
        public void SetWallHealth(int side, float ratio)
        {
            if (sideSkins == null || side < 0 || side >= sideSkins.Length || sideSkins[side] == null) return;
            Color tint = Color.Lerp(new Color(1f, 0.3f, 0.25f), Color.white, ratio);
            foreach (Renderer skin in sideSkins[side]) materials.Tint(skin, tint);
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            materials?.Dispose();
        }
    }
}
