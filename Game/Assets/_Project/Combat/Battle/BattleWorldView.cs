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
        // 성벽 손상 3단계 (K-024): 면마다 바깥면·난간·돌출부 조각. 체력 비율로 손상 그림 재질을 바꿔 끼운다
        readonly List<(Renderer renderer, BattleWorldMaterials.Surface surface)>[] damageParts = new List<(Renderer, BattleWorldMaterials.Surface)>[4];
        readonly int[] damageStage = new int[4];
        List<(Renderer renderer, BattleWorldMaterials.Surface surface)> collecting;   // 지금 짓는 면의 손상 조각 목록 (Box가 채운다)
        // 단계 기준(66%·33%·0)은 WallDamageRules.StageOf
        // W39 (Codex K-126): 0~3단계(3 = 파공, 체력 0) 그림을 얹는다. 기준은 WallDamageRules (66%·33%는 옛 값 그대로). 돌출부 3D 조각은 단계가 오르면 그림으로 바뀐다
        readonly List<(Renderer renderer, float along)>[] merlonPieces = new List<(Renderer, float)>[4];
        readonly bool[] stageKnown = new bool[4];
        BattleWallDamageView damage;

        public int PieceCount { get; private set; }

        // 마당 계단 등이 같은 돌 재질을 나눠 쓴다 (W19). 3D 목업이 꺼져 있으면 null
        public BattleWorldMaterials Materials => materials;

        // 들판 바닥 판에 풀 질감 (반복 크기는 재질 공용 코드의 그림 한 장 미터)
        public void DressGround(Renderer ground, Vector2 sizeMeters)
        {
            if (materials == null || ground == null || !materials.HasArt(BattleWorldMaterials.Surface.FieldGrass)) return;
            ground.sharedMaterial = materials.Get(BattleWorldMaterials.Surface.FieldGrass);
            materials.Apply(ground, BattleWorldMaterials.Surface.FieldGrass, sizeMeters);
        }

        public BattleWorldView(BattleSim sim, Transform parent)
        {
            root = new GameObject("Battle World 3D (목업)");
            root.transform.SetParent(parent, false);
            if (!Active) return;

            materials = new BattleWorldMaterials();

            Proportion p = Current;
            float half = sim.CastleHalf;
            float top = BattleSim.WallHeight;

            // 성벽마다 대포 자리(성벽 따라 m) — 병사 자리(BattleSim.Slots)와 같은 목록 (K-045)
            var bays = new IReadOnlyList<float>[4];
            for (int s = 0; s < 4; s++) bays[s] = sim.CannonBays(s);

            var merlons = new List<float>();
            for (int side = 0; side < 4; side++)
            {
                float3 n3 = WallGeometry.Outward(side);
                float3 r3 = WallGeometry.Right(side);
                var n = new Vector3(n3.x, 0f, n3.z);
                var r = new Vector3(r3.x, 0f, r3.z);
                Quaternion face = Quaternion.LookRotation(n);
                // 모서리 발판이 차지하는 끝은 비워 둔다
                float from = BattleWallLayout.From(half), to = BattleWallLayout.To(half);

                damageParts[side] = new List<(Renderer, BattleWorldMaterials.Surface)>();
                collecting = damageParts[side];

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

                // 돌출부 (사격구 안에는 없음). 자리 계산은 병사 총안 자리·횃불과 공용 (BattleWallLayout, K-045)
                BattleWallLayout.Merlons(half, bays[side], merlons);
                merlonPieces[side] = new List<(Renderer, float)>();
                foreach (float m in merlons)
                {
                    // 돌출부도 난간과 같은 벽돌 줄에서 이어진다 (위치 = 성벽 따라 m, 높이 = 난간 위)
                    Renderer merlonBox = Box("Merlon", n * (half - p.ParapetDepth * 0.5f) + r * m + Vector3.up * (top + p.ParapetHeight + p.MerlonHeight * 0.5f),
                        face, new Vector3(p.MerlonWidth, p.MerlonHeight, p.ParapetDepth), BattleWorldMaterials.Surface.ParapetSide,
                        new Vector2(p.MerlonWidth, p.MerlonHeight), new Vector2(m - p.MerlonWidth * 0.5f, BrickRowStart + p.ParapetHeight));
                    merlonPieces[side].Add((merlonBox, m));
                }

                collecting = null;   // 모서리 포탑은 손상 그림을 바꾸지 않는다
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
                // 발판 윗면에 통로와 같은 돌 바닥을 같은 높이로 (사용자 '코너 돌 때 땅이 위아래로' 2026-10-20 — 윗면이 탑 벽 질감을 늘인 민무늬라 통로와 확 달라 보였다)
                if (materials.HasArt(BattleWorldMaterials.Surface.WalkwayStone))
                    Box("Corner Walkway", platformCenter + Vector3.up * (top + SkinLift * 0.5f), Quaternion.identity,
                        new Vector3(p.TowerPlatform, SkinLift, p.TowerPlatform), BattleWorldMaterials.Surface.WalkwayStone,
                        new Vector2(p.TowerPlatform, p.TowerPlatform), Vector2.zero);
                // 솟은 탑: 발판 바깥쪽 끝 (통로 밖이라 길을 막지 않음)
                var towerCenter = corner + outward * (p.TowerSize * 0.5f * 0.9f);
                towerCenters[side] = towerCenter;
                collectingTower = towerParts[side] = new List<Renderer>();
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
                collectingTower = null;
            }

            damage = new BattleWallDamageView(root.transform, sim.BattleSeed, half, p, merlonPieces);   // 부서짐 그림 (처음 부서질 때 그림을 읽는다)
        }

        // 모서리 탑이 사진기 바로 옆이면 숨긴다 — 모서리를 돌 때 탑 벽이 화면 절반을 덮었다 (2026-10-20)
        const float TowerHideDistance = 4.5f;
        readonly Vector3[] towerCenters = new Vector3[4];
        readonly List<Renderer>[] towerParts = new List<Renderer>[4];
        List<Renderer> collectingTower;
        readonly bool[] towerHidden = new bool[4];

        public void UpdateView(Vector3 cameraPosition, bool mapMode)
        {
            damage?.Tick(Time.unscaledTime, cameraPosition);   // 먼지·연기·불꽃 칸 (켜진 것만)
            for (int side = 0; side < 4; side++)
            {
                List<Renderer> parts = towerParts[side];
                if (parts == null) continue;
                Vector3 d = cameraPosition - towerCenters[side];
                bool hide = !mapMode && d.x * d.x + d.z * d.z < TowerHideDistance * TowerHideDistance;
                if (hide == towerHidden[side]) continue;
                towerHidden[side] = hide;
                foreach (Renderer r in parts) if (r != null) r.enabled = !hide;
            }
        }

        static List<float> Sorted(IReadOnlyList<float> list)
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
            Discard(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = materials.Get(surface);
            materials.Apply(renderer, surface, uvSize, uvOffset);   // 1m당 32화소로 반복 (그림이 있을 때만)
            if (collecting != null && (surface == BattleWorldMaterials.Surface.WallStone || surface == BattleWorldMaterials.Surface.ParapetSide || surface == BattleWorldMaterials.Surface.ParapetTop))
                collecting.Add((renderer, surface));
            collectingTower?.Add(renderer);
            PieceCount++;
            return renderer;
        }

        // 성벽 체력 색(BattleView): 덮은 판에도 같은 붉은 기를 곱한다. ratio 1 = 멀쩡
        // 손상 그림(K-024)이 있으면 체력 비율에 따라 멀쩡 → 금감 → 부서짐 재질로 바꿔 끼운다 (반복 값은 조각마다 그대로)
        public void SetWallHealth(int side, float ratio)
        {
            if (sideSkins == null || side < 0 || side >= sideSkins.Length || sideSkins[side] == null) return;
            // 붉은 기는 뺐다(W39 사진 검토): 손상은 돌 그림(균열·검댕·빠진 돌)으로 읽히게, 체력이 줄수록 조금 어둡게만
            float shade = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(ratio));
            Color tint = new Color(shade, shade * 0.98f, shade * 0.96f);
            foreach (Renderer skin in sideSkins[side]) materials.Tint(skin, tint);

            int stage = WallDamageRules.StageOf(ratio);
            bool first = !stageKnown[side];   // 처음 받은 값은 먼지를 피우지 않는다 (이미 부서진 채로 시작)
            stageKnown[side] = true;
            if (stage == damageStage[side] || damageParts[side] == null) return;
            damageStage[side] = stage;
            foreach (var (renderer, surface) in damageParts[side])
            {
                if (renderer == null) continue;
                renderer.sharedMaterial = materials.GetDamaged(surface, WallDamageRules.MaterialStage(stage)) ?? materials.Get(surface);
            }

            damage?.SetStage(side, stage, first);
        }

        // 확인용: 면 side의 지금 손상 단계 (0 멀쩡, 1 금감, 2 반파, 3 파공)
        public int DamageStage(int side) => side >= 0 && side < damageStage.Length ? damageStage[side] : 0;

        // 밤이면 심하게 부서진 곳에 작은 연기·불꽃 (BattleView.ApplyAtmosphere)
        public void SetNight(bool night) => damage?.SetNight(night);

        // 확인용: 부서짐 그림 (그림이 없거나 3D 목업이 꺼져 있으면 null)
        public BattleWallDamageView Damage => damage;

        // 편집 모드 시험에서는 Destroy를 못 쓴다 (W39)
        static void Discard(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        public void Dispose()
        {
            damage?.Dispose();
            if (root != null) Discard(root);
            materials?.Dispose();
        }
    }
}
