using System;
using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using UnityEngine;
using UnityEngine.Rendering;

namespace HumanLastRampart.Combat.Render
{
    // 접촉 그림자 (W3, K-098): 병사·적·대포 발밑 부드러운 타원 한 장.
    // - 새 개체 GameObject·CPU 반복 없음: 이미 올라가 있는 개체 버퍼(적 그룹·병사 그룹·주인공·마법사·대포)를 그림자 셰이더가 그대로 읽는다.
    //   그리기 호출은 버퍼 하나당 한 번(약 10회), 그림 한 장(병사 타원)을 폭·깊이만 바꿔 쓴다 — 오우거·대포 타원은 같은 식(170×(1−r²)²)이라 늘려도 같다
    // - 폭은 K-098 manifest 권장값을 개체 크기(그림 키)에 대한 비율로: 병사 0.38m/0.35m, 오우거 0.85m/2.0m, 대포 0.75m (실제 카메라에서 조정)
    public sealed class ContactShadows : IDisposable
    {
        public const string ShaderName = "ContactShadow";
        public const string TexturePath = "BattleArt/Shadow/contact_shadow_soldier_2x";
        public const float Opacity = 0.65f;            // K-098 권장 재질 불투명도

        // 개체 크기(그림 키) 대비 타원 폭 — manifest 권장 폭 ÷ 그 개체의 표준 그림 키
        public const float SoldierWidthPerSize = 0.38f / 0.35f;
        public const float OgreWidthPerSize = 0.85f / 2.0f;
        public const float GoblinWidthPerSize = 0.38f;   // 일반 적은 manifest에 없어 오우거 비율에 맞춘 시작값
        public const float CannonWidthScale = 1.2f;      // 대포 타원 폭 = 보이는 폭 × 이 값 (manifest 0.75m ≈ 보이는 폭 약 0.6m의 1.2배, BattlePropView가 계산)
        public const float LadderFadeHeight = 1.5f;      // 사다리 오르는 적: 발이 땅에서 이만큼 뜨면 그림자 사라짐

        // 켜고 끄기 (확인용 사진 비교). 기본 켬
        public static bool Enabled = true;

        sealed class Kind
        {
            public GraphicsBuffer Buffer;
            public MaterialPropertyBlock Block;
            public int Count;
            public bool Hidden;
        }

        static readonly int InstancesId = Shader.PropertyToID("_Instances");
        static readonly int WidthPerSizeId = Shader.PropertyToID("_WidthPerSize");
        static readonly int DepthRatioId = Shader.PropertyToID("_DepthRatio");
        static readonly int GroundYId = Shader.PropertyToID("_GroundY");
        static readonly int FadeHeightId = Shader.PropertyToID("_FadeHeight");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        readonly Mesh quad;
        readonly Bounds bounds;
        readonly Material material;
        readonly Texture2D blob;
        readonly List<Kind> kinds = new List<Kind>();

        public ContactShadows(Mesh quad, Bounds bounds)
        {
            this.quad = quad;
            this.bounds = bounds;
            Shader shader = Resources.Load<Shader>(ShaderName);
            blob = Resources.Load<Texture2D>(TexturePath);
            if (shader != null && blob != null)
            {
                material = new Material(shader) { name = "ContactShadow (runtime)" };
                material.SetTexture("_MainTex", blob);
            }
        }

        // 그림자 셰이더와 타원 그림이 모두 있을 때만 그린다 (없으면 조용히 건너뜀)
        public bool Available => material != null;
        public int KindCount => kinds.Count;
        public int DrawCount { get; private set; }   // 확인용: 마지막 Draw에서 낸 그리기 호출 수

        // 개체 버퍼 하나 등록. buffer는 PerfLabInstance 배치(발 위치·크기), count = 그 버퍼의 칸 수.
        // fadeHeight > 0이면 발이 groundY보다 높을수록 옅어진다 (사다리 오르는 적). 0이면 항상 같은 진하기 (성벽 위 병사)
        public void Add(GraphicsBuffer buffer, int count, float widthPerSize, float depthRatio = 0.5f, float groundY = 0f, float fadeHeight = 0f)
        {
            if (material == null || buffer == null || count <= 0) return;
            var block = new MaterialPropertyBlock();
            block.SetBuffer(InstancesId, buffer);
            block.SetFloat(WidthPerSizeId, widthPerSize);
            block.SetFloat(DepthRatioId, depthRatio);
            block.SetFloat(GroundYId, groundY);
            block.SetFloat(FadeHeightId, fadeHeight);
            block.SetFloat(OpacityId, Opacity);
            kinds.Add(new Kind { Buffer = buffer, Block = block, Count = count });
        }

        // 숨겨진 개체(그리지 않는 주인공 등)는 버퍼에 옛 값이 남아 있으므로 그 버퍼의 그림자를 끈다
        public void SetVisible(GraphicsBuffer buffer, bool visible)
        {
            foreach (Kind kind in kinds) if (kind.Buffer == buffer) kind.Hidden = !visible;
        }

        // 지도 모드가 아닐 때, 개체 버퍼가 올라간 뒤에 부른다
        public void Draw()
        {
            DrawCount = 0;
            if (material == null || !Enabled) return;
            foreach (Kind kind in kinds)
            {
                if (kind.Hidden) continue;
                Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, kind.Count, kind.Block, ShadowCastingMode.Off, false);
                DrawCount++;
            }
        }

        public void Dispose()
        {
            kinds.Clear();
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}
