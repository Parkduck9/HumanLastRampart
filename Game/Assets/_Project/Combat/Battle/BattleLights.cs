using System.Collections.Generic;
using HumanLastRampart.Core.CombatContracts;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 전투 빛 효과 (카드 K-010, D-062 4단계)
    // - 밤 횃불: 네 성벽 바깥 난간 바로 안쪽을 따라 약 3m마다 따뜻한 점광원 + 보이는 작은 불꽃. 낮이면 꺼 둔다. 비가 오면 조금 약하게
    // - 폭발 빛: 마법 폭발(0.4초 번쩍)·불바다(지속 중 흔들리는 주황빛)·함정 폭발(터지는 순간 0.4초) 자리에 잠깐 밝은 빛
    // - 폭발 빛은 풀에서 다시 쓴다 (동시에 최대 FlashCapacity개). 지도 모드면 모두 끈다
    // 캐릭터(PerfLabSprite)는 추가 조명을 감싸는 빛으로 받고, 성벽·땅(기본 Lit)도 함께 밝아진다
    public sealed class BattleLights
    {
        public const float TorchSpacing = 3f;          // 성벽 따라 간격 (m). 가까운 사진기에 보이는 성벽 폭이 몇 m뿐이라 8m 간격이면 한 개도 안 잡혔다
        public const float TorchInset = 0.5f;          // 성벽 바깥 모서리에서 안쪽으로 — 바깥 난간 바로 안쪽 (예전 1.9m 안쪽 끝은 사진기 뒤라 안 보였다)
        public const float TorchHeight = 0.24f;        // 성벽 위 높이 (돌출부 끝쯤 — 병사 키 0.35m 기준)
        const float TorchRange = 2f;            // 통로와 난간을 비춘다 (이웃 횃불과 조금 겹치게)
        const float TorchIntensity = 0.8f;      // 바로 옆 병사가 하얗게 날아가지 않게
        const float RainTorchMultiplier = 0.75f;
        const float FlameSize = 0.035f;         // 눈에 보이는 불꽃 지름 (m) — 병사 머리보다 작게
        static readonly Color TorchColor = new Color(1f, 0.58f, 0.26f);
        static readonly Color FlameColor = new Color(2.2f, 1.1f, 0.35f);   // 조금 밝게(HDR) 칠해 은은히 번지게

        const int FlashCapacity = 16;
        const float BlastSeconds = 0.4f;        // 폭발 번쩍 (BattleSim 폭발 표시 시간과 같다)
        const float BlastIntensity = 9f;        // 함정 폭발
        const float MagicBlastIntensity = 24f;  // 마법 폭발은 크게 번쩍 (K-041: 대포·함정과 확실히 다르게)
        const float FireIntensity = 10f;        // 불바다 (K-041: 4는 낮에 안 보였다 — 사용자 '불에서 빛도 안 나옴')
        const float DayFireMultiplier = 1.6f;   // 낮에는 햇빛에 묻히지 않게 더
        const float FlashHeight = 1.5f;         // 땅 위
        const float FireHeight = 0.9f;          // 불바다 빛은 낮게 — 땅에 주황빛 웅덩이가 지게
        static readonly Color BlastColor = new Color(1f, 0.82f, 0.55f);
        static readonly Color FireColor = new Color(1f, 0.5f, 0.18f);

        readonly BattleSim sim;
        readonly GameObject root;
        readonly List<Light> torches = new List<Light>();
        readonly List<GameObject> flames = new List<GameObject>();   // 횃불마다 보이는 불꽃
        Material flameMaterial;
        float torchBase;
        readonly List<Light> flashes = new List<Light>();
        readonly BattleSim.TrapState[] trapStates;   // 지난 프레임 함정 상태 (터지는 순간 찾기)
        readonly List<(Vector3 position, float radius, float timeLeft)> trapBlasts = new List<(Vector3, float, float)>();

        bool torchesOn;
        Weather torchWeather;
        bool torchesDecided;

        public BattleLights(BattleSim sim, Transform parent)
        {
            this.sim = sim;
            root = new GameObject("Battle Lights");
            root.transform.SetParent(parent, false);
            BuildTorches();

            trapStates = new BattleSim.TrapState[sim.TrapCount];
            for (int i = 0; i < trapStates.Length; i++) trapStates[i] = sim.GetTrap(i).State;
        }

        void BuildTorches()
        {
            float half = sim.CastleHalf;
            float inner = half - TorchInset;
            // 한 면을 간격으로 나눠 가운데 맞춤 (모서리 발판 근처는 비운다)
            int count = Mathf.Max(1, Mathf.FloorToInt((inner * 2f) / TorchSpacing));
            float start = -(count - 1) * TorchSpacing * 0.5f;
            // 보이는 불꽃은 Codex 도트 그림(torch_flame, BattleEffectsView)이 같은 자리에 그린다. 그림이 없을 때만 임시 공
            Shader unlit = Resources.Load<Texture2D>("BattleArt/Effects/torch_flame") == null ? Shader.Find("Universal Render Pipeline/Unlit") : null;
            if (unlit != null)
            {
                flameMaterial = new Material(unlit);
                flameMaterial.SetColor("_BaseColor", FlameColor);
            }
            for (int side = 0; side < 4; side++)
            {
                float3 n = WallGeometry.Outward(side);
                float3 r = WallGeometry.Right(side);
                for (int k = 0; k < count; k++)
                {
                    float along = start + k * TorchSpacing;
                    var position = new Vector3(n.x * inner + r.x * along, BattleSim.WallHeight + TorchHeight, n.z * inner + r.z * along);
                    Light light = NewLight("Torch", position);
                    light.color = TorchColor;
                    light.range = TorchRange;
                    light.enabled = false;
                    torches.Add(light);

                    if (flameMaterial == null) continue;
                    var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    flame.name = "Torch Flame";
                    Object.Destroy(flame.GetComponent<Collider>());
                    flame.transform.SetParent(light.transform, false);
                    flame.transform.localScale = new Vector3(FlameSize, FlameSize * 1.4f, FlameSize);
                    var renderer = flame.GetComponent<Renderer>();
                    renderer.sharedMaterial = flameMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    flame.SetActive(false);
                    flames.Add(flame);
                }
            }
        }

        Light NewLight(string name, Vector3 position)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            Light light = go.GetComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;   // 그림자는 해·달만
            return light;
        }

        public void Update(bool mapMode)
        {
            if (mapMode)
            {
                SetTorches(false, 0f);
                torchesDecided = false;   // 지도에서 돌아오면 다시 판단
                for (int i = 0; i < flashes.Count; i++) flashes[i].enabled = false;
                TrackTraps(Time.deltaTime);
                return;
            }

            // 횃불: 밤낮은 전투 중 안 바뀌고, 날씨가 바뀌면 다시 판단
            if (!torchesDecided || torchWeather != sim.CurrentWeather)
            {
                torchesDecided = true;
                torchWeather = sim.CurrentWeather;
                bool night = sim.TimeOfDay == TimeOfDay.Night;
                SetTorches(night, sim.IsRaining ? TorchIntensity * RainTorchMultiplier : TorchIntensity);
            }

            if (torchesOn) FlickerTorches();
            TrackTraps(Time.deltaTime);
            UpdateFlashes();
        }

        void SetTorches(bool on, float intensity)
        {
            if (!on && !torchesOn) return;
            torchesOn = on;
            torchBase = intensity;
            foreach (Light torch in torches)
            {
                torch.enabled = on;
                torch.intensity = intensity;
            }
            foreach (GameObject flame in flames) flame.SetActive(on);
        }

        // 횃불마다 조금씩 다르게 흔들린다
        void FlickerTorches()
        {
            float t = Time.time;
            for (int i = 0; i < torches.Count; i++)
                torches[i].intensity = torchBase * (0.85f + 0.15f * Mathf.PerlinNoise(t * 5f, i * 1.7f));
        }

        // 함정이 터지는 순간(Exploded로 바뀔 때) 번쩍 하나를 남기고, 남은 시간을 줄인다
        void TrackTraps(float dt)
        {
            for (int i = trapBlasts.Count - 1; i >= 0; i--)
            {
                var blast = trapBlasts[i];
                blast.timeLeft -= dt;
                if (blast.timeLeft <= 0f) trapBlasts.RemoveAt(i);
                else trapBlasts[i] = blast;
            }

            int count = Mathf.Min(trapStates.Length, sim.TrapCount);
            for (int i = 0; i < count; i++)
            {
                BattleSim.TrapInfo trap = sim.GetTrap(i);
                BattleSim.TrapState state = trap.State;
                if (state == BattleSim.TrapState.Exploded && trapStates[i] != BattleSim.TrapState.Exploded)
                    trapBlasts.Add((trap.Position, trap.DamageRadius, BlastSeconds));
                trapStates[i] = state;
            }
        }

        void UpdateFlashes()
        {
            int used = 0;
            float t = Time.time;

            IReadOnlyList<BattleSim.SpellEffect> effects = sim.Effects;
            for (int i = 0; i < effects.Count && used < FlashCapacity; i++)
            {
                BattleSim.SpellEffect e = effects[i];
                if (e.Pending) continue;   // 찍고 터지기 전에는 빛 없음
                if (e.Fire)
                {
                    // 불바다: 흔들리는 주황빛, 끝날 때 1초 동안 사그라든다
                    float fade = Mathf.Clamp01(e.TimeLeft);
                    float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(t * 7f, i * 3.1f);
                    float day = sim.TimeOfDay == TimeOfDay.Night ? 1f : DayFireMultiplier;
                    Use(used++, e.Position, e.Radius, FireColor, FireIntensity * day * flicker * fade, FireHeight, e.Radius * 1.6f + 3f);
                }
                else
                {
                    // 마법 폭발: 크고 넓게 번쩍 (함정 폭발은 아래 trapBlasts로 예전 세기)
                    float k = Mathf.Clamp01(e.TimeLeft / BlastSeconds);
                    Use(used++, e.Position, e.Radius, BlastColor, MagicBlastIntensity * k * k, FlashHeight, e.Radius * 3f + 8f);
                }
            }

            for (int i = 0; i < trapBlasts.Count && used < FlashCapacity; i++)
            {
                var blast = trapBlasts[i];
                Use(used++, blast.position, blast.radius, BlastColor, BlastIntensity * Mathf.Clamp01(blast.timeLeft / BlastSeconds));
            }

            for (int i = used; i < flashes.Count; i++) flashes[i].enabled = false;
        }

        // 풀의 index번 빛을 켠다 (없으면 만든다). 범위는 효과 반경보다 넓게
        void Use(int index, Vector3 ground, float radius, Color color, float intensity) =>
            Use(index, ground, radius, color, intensity, FlashHeight, Mathf.Max(4f, radius * 2f + 3f));

        void Use(int index, Vector3 ground, float radius, Color color, float intensity, float height, float range)
        {
            while (flashes.Count <= index)
            {
                Light created = NewLight("Flash", Vector3.zero);
                created.enabled = false;
                flashes.Add(created);
            }

            Light light = flashes[index];
            light.transform.localPosition = new Vector3(ground.x, ground.y + height, ground.z);
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.enabled = intensity > 0.01f;
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            torches.Clear();
            flames.Clear();
            if (flameMaterial != null) Object.Destroy(flameMaterial);
            flashes.Clear();
            trapBlasts.Clear();
        }
    }
}
