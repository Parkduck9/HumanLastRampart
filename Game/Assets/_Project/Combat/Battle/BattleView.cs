using System;
using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.Definitions;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 전장 그리기: 바닥·성벽 4면(임시 상자)·빛, 적·병사·투사체는 GPU 인스턴싱 (D-024)
    public sealed class BattleView : IDisposable
    {
        static readonly int InstancesId = Shader.PropertyToID("_Instances");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int GridId = Shader.PropertyToID("_Grid");
        static readonly int PivotId = Shader.PropertyToID("_Pivot");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");

        readonly BattleSim sim;
        readonly Material material;
        readonly Mesh quad;
        readonly Texture2D placeholderSheet;
        readonly Texture2D shotTexture;
        readonly List<(GraphicsBuffer buffer, MaterialPropertyBlock block, int start, int count)> enemyDraws =
            new List<(GraphicsBuffer, MaterialPropertyBlock, int, int)>();
        readonly GraphicsBuffer soldierBuffer;
        readonly MaterialPropertyBlock soldierBlock;
        readonly GraphicsBuffer shotBuffer;
        readonly MaterialPropertyBlock shotBlock;
        readonly Renderer[] walls = new Renderer[4];
        readonly Bounds bounds;
        readonly GameObject root;

        public BattleView(BattleSim sim, Shader shader, Transform parent)
        {
            this.sim = sim;
            quad = PerfLabArt.CreateQuad();
            placeholderSheet = PerfLabArt.CreatePlaceholderSheet();
            shotTexture = PerfLabArt.CreateArrowTexture();
            material = new Material(shader) { name = "BattleSprite (runtime)" };
            float extent = sim.FieldSize + 20f;
            bounds = new Bounds(Vector3.zero, new Vector3(extent * 2f, 200f, extent * 2f));

            foreach (var (def, start, count) in sim.EnemyGroups)
            {
                var block = new MaterialPropertyBlock();
                if (def.Sheet != null)
                {
                    Apply(block, def.Sheet, PerfLabArt.GoblinLayout);   // 4열×2행 고블린 규격 (EnemyDef 주석)
                }
                else
                {
                    Apply(block, placeholderSheet, PerfLabArt.PlaceholderLayout);
                }

                var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, count), PerfLabInstance.Stride);
                block.SetBuffer(InstancesId, buffer);
                enemyDraws.Add((buffer, block, start, count));
            }

            soldierBlock = new MaterialPropertyBlock();
            Apply(soldierBlock, placeholderSheet, PerfLabArt.PlaceholderLayout);   // 병사 그림은 Codex 아트 이후
            soldierBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, sim.SoldierCount), PerfLabInstance.Stride);
            soldierBlock.SetBuffer(InstancesId, soldierBuffer);

            shotBlock = new MaterialPropertyBlock();
            Apply(shotBlock, shotTexture, PerfLabArt.ArrowLayout);
            shotBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sim.ShotCapacity, PerfLabInstance.Stride);
            shotBlock.SetBuffer(InstancesId, shotBuffer);

            root = new GameObject("Battlefield");
            root.transform.SetParent(parent, false);
            BuildEnvironment();
        }

        static void Apply(MaterialPropertyBlock block, Texture texture, PerfLabArt.SheetLayout layout)
        {
            block.SetTexture(MainTexId, texture);
            block.SetVector(GridId, layout.Grid);
            block.SetVector(PivotId, layout.Pivot);
            block.SetFloat(AspectId, layout.Aspect);
        }

        void BuildEnvironment()
        {
            float half = sim.CastleHalf;
            float field = sim.FieldSize;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = new Vector3((field + 20f) * 2f / 10f, 1f, (field + 20f) * 2f / 10f);
            Tint(ground.GetComponent<Renderer>(), new Color(0.42f, 0.5f, 0.3f));

            var yard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            yard.name = "Castle Yard";
            yard.transform.SetParent(root.transform, false);
            yard.transform.localPosition = new Vector3(0f, BattleSim.WallHeight * 0.5f - 0.05f, 0f);
            yard.transform.localScale = new Vector3(half * 2f - 3f, BattleSim.WallHeight - 0.1f, half * 2f - 3f);
            Tint(yard.GetComponent<Renderer>(), new Color(0.45f, 0.42f, 0.38f));

            for (int side = 0; side < 4; side++)
            {
                Vector3 n = ToVector(WallGeometry.Outward(side));
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall " + sim.Sides[side].WallId;
                wall.transform.SetParent(root.transform, false);
                wall.transform.localPosition = n * (half - 1f) + Vector3.up * BattleSim.WallHeight * 0.5f;
                wall.transform.localRotation = Quaternion.LookRotation(n);
                wall.transform.localScale = new Vector3(half * 2f + 2f, BattleSim.WallHeight, 2f);
                walls[side] = wall.GetComponent<Renderer>();
            }

            var sun = new GameObject("Sun", typeof(Light));
            sun.transform.SetParent(root.transform, false);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.GetComponent<Light>().type = LightType.Directional;
        }

        static void Tint(Renderer renderer, Color color)
        {
            renderer.material.color = color;
        }

        static Vector3 ToVector(Unity.Mathematics.float3 v) => new Vector3(v.x, v.y, v.z);

        // 작업이 끝난 뒤(sim.Complete 후) 부른다
        public void Draw()
        {
            foreach (var (buffer, block, start, count) in enemyDraws)
            {
                if (count <= 0) continue;
                buffer.SetData(sim.EnemyInstances, start, 0, count);
                Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, count, block);
            }

            if (sim.SoldiersDirty)
            {
                sim.RefreshSoldierInstances();
                soldierBuffer.SetData(sim.SoldierInstances);
                sim.SoldiersDirty = false;
            }

            if (sim.SoldierCount > 0)
            {
                Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, sim.SoldierCount, soldierBlock);
            }

            shotBuffer.SetData(sim.ShotInstances);
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, sim.ShotCapacity, shotBlock);

            // 성벽 색: 체력이 줄수록 붉게
            for (int side = 0; side < 4; side++)
            {
                BattleSim.SideStatus status = sim.Sides[side];
                float ratio = status.WallMaxHp > 0 ? Mathf.Clamp01((float)status.WallHp / status.WallMaxHp) : 1f;
                walls[side].material.color = Color.Lerp(new Color(0.75f, 0.2f, 0.15f), new Color(0.72f, 0.7f, 0.66f), ratio);
            }
        }

        public void Dispose()
        {
            foreach (var draw in enemyDraws)
            {
                draw.buffer.Release();
            }

            enemyDraws.Clear();
            soldierBuffer?.Release();
            shotBuffer?.Release();
            if (material != null) UnityEngine.Object.Destroy(material);
            if (quad != null) UnityEngine.Object.Destroy(quad);
            if (placeholderSheet != null) UnityEngine.Object.Destroy(placeholderSheet);
            if (shotTexture != null) UnityEngine.Object.Destroy(shotTexture);
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
