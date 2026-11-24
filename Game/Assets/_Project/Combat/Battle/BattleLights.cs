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
    public sealed partial class BattleLights
    {
        public const float TorchSpacing = 3f;          // 성벽 따라 간격 (m). 가까운 사진기에 보이는 성벽 폭이 몇 m뿐이라 8m 간격이면 한 개도 안 잡혔다
        // 성벽 바깥 모서리에서 안쪽으로 — 난간(두께 0.3m) 바로 뒤. 자리는 간격마다 가장 가까운 돌출부 뒤(BattleSim.TorchSpots, K-045):
        // 총안에 선 병사(0.5m)와 옆으로 비켜 있고, 횃불 돌 뒤에는 병사 자리를 두지 않는다
        public const float TorchInset = 0.35f;
        public const float TorchHeight = 0.24f;        // 성벽 위 높이 (돌출부 끝쯤 — 병사 키 0.35m 기준)
        const float TorchRange = 2f;            // 처음 값. 실제 세기·반경은 NightTuning (밤 밝기 단계, 2026-10-20)
        const float RainTorchMultiplier = 0.75f;
        const float FlameSize = 0.035f;         // 눈에 보이는 불꽃 지름 (m) — 병사 머리보다 작게
        static readonly Color TorchColor = new Color(1f, 0.58f, 0.26f);
        static readonly Color FlameColor = new Color(2.2f, 1.1f, 0.35f);   // 조금 밝게(HDR) 칠해 은은히 번지게

        const int FlashCapacity = 28;
        public static Vector3 ViewPosition;   // 전투 사진기 자리 (BattleView가 매 장면 넣는다) — 총구 빛은 여기서 가까운 총병만
        // 쏠 때·터질 때 빛 (2026-10-20)
        public const float CannonImpactIntensity = 14f;
        const float MuzzleSeconds = 0.12f;
        const float CannonMuzzleIntensity = 3f;
        const float GunMuzzleIntensity = 0.45f;   // 사용자 '총구 빛은 조금 약하게' (2026-10-20)
        const int GunFlashMax = 6;
        const float CastSeconds = 0.8f;
        const float CastIntensity = 1.5f;
        const float DayFlashMultiplier = 1.8f;   // 낮에는 햇빛에 묻히지 않게
        public static readonly Color ImpactColor = new Color(1f, 0.7f, 0.4f);
        static readonly Color MuzzleColor = new Color(1f, 0.78f, 0.45f);
        static readonly Color CastColor = new Color(0.55f, 0.65f, 1f);
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
        bool torchesDecided, torchNight;
        int torchTuning = -1;

        public BattleLights(BattleSim sim, Transform parent)
        {
            this.sim = sim;
            pending.Clear();   // 앞 전투에서 남은 번쩍 지우기
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
                // 간격마다 가장 가까운 돌출부 뒤 (BattleWallLayout.Torches — 그림 불꽃·받침도 같은 목록, K-045)
                foreach (float along in sim.TorchSpots(side))
                {
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
                AgeTimed(Time.deltaTime);
                HumanLastRampart.Combat.Render.HlrLights.Clear();
                UpdateTowerLight(true);
                return;
            }

            AgeTimed(Time.deltaTime);

            // 횃불: 밤낮은 전투 중 안 바뀌고, 날씨가 바뀌면 다시 판단
            if (!torchesDecided || torchWeather != sim.CurrentWeather || torchNight != (sim.TimeOfDay == TimeOfDay.Night)
                || torchTuning != HumanLastRampart.Combat.Render.NightTuning.Version)
            {
                torchesDecided = true;
                torchWeather = sim.CurrentWeather;
                torchNight = sim.TimeOfDay == TimeOfDay.Night;
                torchTuning = HumanLastRampart.Combat.Render.NightTuning.Version;
                float intensity = HumanLastRampart.Combat.Render.NightTuning.TorchIntensity;
                foreach (Light torch in torches) torch.range = HumanLastRampart.Combat.Render.NightTuning.TorchRange;
                SetTorches(false, 0f);   // 세기를 다시 넣으려고 한 번 끈다
                SetTorches(torchNight, sim.IsRaining ? intensity * RainTorchMultiplier : intensity);
            }

            if (torchesOn) FlickerTorches();
            TrackTraps(Time.deltaTime);
            UpdateFlashes();
            UpdateTowerLight(false);
            GatherShaderLights();   // 셰이더 점광원 목록 (BattleLights.Hlr.cs) — 탑 불빛 포함
        }

        // 마당 가운데 등대 탑 불빛 (W9): 밤에 탑 꼭대기에서 마당을 비춘다. 세기는 등불 밝기(약·보통·강)를 따르고, 점화의 밤이면 더 환하다. 낮·지도에서는 끈다
        Light towerLight;
        const float TowerTopRangeFactor = 1.1f;   // 불방이 높아진 만큼(약 9m) 닿는 거리를 더한다
        const float TowerTopBoost = 2.2f;         // 멀어진 만큼 세기를 보태 마당이 예전처럼 밝게 (그림 확인 후 조정)

        void UpdateTowerLight(bool mapMode)
        {
            bool on = !mapMode && sim.TimeOfDay == TimeOfDay.Night && HumanLastRampart.Combat.Render.NightTuning.TowerLightAllowed;
            if (!on)
            {
                if (towerLight != null) towerLight.enabled = false;
                UpdateCourtyardLights(false, 0f, 1f);
                return;
            }

            if (towerLight == null)
            {
                // W20: 높은 탑 꼭대기 불방에서 비춘다. 마당 윗면까지 멀어졌으므로 닿는 거리를 높이만큼 늘린다 (PC·모바일 같은 자리)
                towerLight = NewLight("Beacon Tower", new Vector3(0f, BattleSim.WallHeight + BattleTowerView.LampHeight, 0f));
                towerLight.color = HumanLastRampart.Combat.Render.NightTuning.TowerLightColor;
                towerLight.range = HumanLastRampart.Combat.Render.NightTuning.TowerLightRange + BattleTowerView.LampHeight * TowerTopRangeFactor;
            }

            float level = BattleSim.GlowLevel(sim.Lamp);
            float flicker = 0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 4f, 7.3f);
            towerLight.intensity = HumanLastRampart.Combat.Render.NightTuning.TowerLightIntensity * TowerTopBoost * level * flicker * (sim.IsIgnitionNight ? 1.3f : 1f) * (sim.IsRaining ? RainTorchMultiplier : 1f);
            towerLight.enabled = true;
            UpdateCourtyardLights(true, level, flicker);
        }

        // 마당 아래 빛 (W21, 밤 마당·탑 밑동이 거의 까맣던 문제): 탑 꼭대기 빛은 9m 위라 바닥에 거의 안 닿는다.
        // - 탑 둘레 따뜻한 빛 웅덩이(GroundGlow, 가장자리로 어두워짐) + 문 앞 작은 웅덩이 (모바일도 같다, 싸다)
        // - PC만: 탑 윗몸에 따뜻한 빛을 비스듬히 얹는 보조 빛 하나, 문등 점광원 하나 (밑동 병사·일꾼이 보이게)
        public const float CourtyardPoolRadius = 9f;
        public const float DoorPoolRadius = 2.4f;
        public const float DoorLanternHeight = 1.3f;
        public const float DoorLanternOut = 0.45f;   // 문 앞으로 나온 거리
        const float TowerFillIntensity = 3.6f;   // 윗몸이 램프 빛을 더 받게 (사진 확인 후 2.2에서 올림)
        const float DoorLanternIntensity = 1f;
        const float DoorLanternRange = 3.6f;
        static readonly Color DoorLanternColor = new Color(1f, 0.55f, 0.22f);   // 호박빛 (흰빛으로 날아가지 않게 붉은 쪽)
        static readonly Color TowerFillColor = new Color(1f, 0.6f, 0.26f);
        Light towerFill, doorLantern;
        float courtyardLevel;   // 마당 빛 세기 하나 (등불 밝기 최소 0.5 × 깜박임 × 비)

        // 문등 점광원이 셰이더 빛 목록 칸을 쓰는가: 모바일(6칸)은 칸을 아끼려고 땅 웅덩이만
        public static bool DoorLanternUsesSlot(bool low) => !low;

        void UpdateCourtyardLights(bool on, float level, float flicker)
        {
            bool real = !HumanLastRampart.Combat.Render.HlrLights.Low;
            if (!on)
            {
                if (towerFill != null) towerFill.enabled = false;
                if (doorLantern != null) doorLantern.enabled = false;
                courtyardLevel = 0f;
                return;
            }

            float rain = sim.IsRaining ? RainTorchMultiplier : 1f;
            courtyardLevel = Mathf.Max(0.5f, level) * flicker * rain;
            if (!real)
            {
                if (towerFill != null) towerFill.enabled = false;
                if (doorLantern != null) doorLantern.enabled = false;
                return;
            }

            if (towerFill == null)
            {
                // 남쪽(사진기 쪽)으로 비켜 서서 윗몸 앞면을 쓸듯이 비춘다
                towerFill = NewLight("Tower Fill", new Vector3(0f, BattleSim.WallHeight + BattleTowerView.LampHeight - 2.5f, BattleTowerView.DoorZ - 1.8f));
                towerFill.color = TowerFillColor;
                towerFill.range = 7f;
            }

            if (doorLantern == null)
            {
                doorLantern = NewLight("Door Lantern", new Vector3(0f, BattleSim.WallHeight + DoorLanternHeight, BattleTowerView.DoorZ - DoorLanternOut));
                doorLantern.color = DoorLanternColor;
                doorLantern.range = DoorLanternRange;
            }

            towerFill.intensity = TowerFillIntensity * level * flicker * rain * (sim.IsIgnitionNight ? 1.3f : 1f);
            towerFill.enabled = true;
            doorLantern.intensity = DoorLanternIntensity * courtyardLevel;
            doorLantern.enabled = true;
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

            // ── 쏠 때·터질 때 빛 (사용자 2026-10-20 '효과만 터지고 빛이 없어 어색하다') ──
            float flashDay = sim.TimeOfDay == TimeOfDay.Night ? 1f : DayFlashMultiplier;

            // 포탄 착탄 (BattleEffectsView가 떨어지는 순간 알린다)
            for (int i = 0; i < timed.Count && used < FlashCapacity; i++)
            {
                var f = timed[i];
                float k = Mathf.Clamp01(f.timeLeft / f.seconds);
                Use(used++, f.position, f.radius, f.color, f.intensity * flashDay * k * k, FlashHeight, f.radius * 2.5f + 5f);
            }

            // 대포 발사: 포구 번쩍
            for (int i = 0; i < sim.CannonCount && used < FlashCapacity; i++)
            {
                float age = sim.CannonFireAge(i);
                if (age >= MuzzleSeconds * 1.5f) continue;
                float k = 1f - Mathf.Clamp01(age / (MuzzleSeconds * 1.5f));
                Use(used++, sim.CannonMuzzle(i), 1f, MuzzleColor, CannonMuzzleIntensity * flashDay * k * k, 0.1f, 4f);   // 포구 바로 위 (사용자 2026-10-20 '포탑 총구에서')
            }

            // 총 쏘기: 주인공 가까운 총병 몇 명만 (수백 명 모두 빛을 켜면 무겁다)
            gunFlashes.Clear();
            sim.RecentGunshots(gunFlashes, MuzzleSeconds, ViewPosition, GunFlashMax);
            for (int i = 0; i < gunFlashes.Count && used < FlashCapacity; i++)
                Use(used++, gunFlashes[i], 0.5f, MuzzleColor, GunMuzzleIntensity * flashDay, 0.02f, 1.1f);   // 총구 자리 (RecentGunshots가 총구 좌표를 준다), 대포보다 약하게

            // 마법 쓰기: 마법사 지팡이에 푸른 빛이 차올랐다 사라진다
            float cast = sim.WizardCastAge;
            if (sim.WizardPresent && cast >= 0f && cast < CastSeconds && used < FlashCapacity)
            {
                float k = Mathf.Sin(Mathf.Clamp01(cast / CastSeconds) * Mathf.PI);
                Use(used++, sim.WizardFeet, 0.5f, CastColor, CastIntensity * k, 0.4f, 1.8f);   // 성벽 위 가까이라 약하고 좁게 (처음 값은 통로가 하얗게 날아갔다)
            }

            for (int i = used; i < flashes.Count; i++) flashes[i].enabled = false;
        }

        // 잠깐 번쩍이는 빛을 남긴다 (포탄 착탄 등 — 그리기 쪽이 사건을 알 때 부른다)
        public static void Flash(Vector3 at, float radius, Color color, float intensity, float seconds) =>
            pending.Add((at, radius, color, intensity, seconds, seconds));

        static readonly List<(Vector3 position, float radius, Color color, float intensity, float seconds, float timeLeft)> pending =
            new List<(Vector3, float, Color, float, float, float)>();
        readonly List<(Vector3 position, float radius, Color color, float intensity, float seconds, float timeLeft)> timed =
            new List<(Vector3, float, Color, float, float, float)>();
        readonly List<Vector3> gunFlashes = new List<Vector3>();

        void AgeTimed(float dt)
        {
            timed.AddRange(pending);
            pending.Clear();
            for (int i = timed.Count - 1; i >= 0; i--)
            {
                var f = timed[i];
                f.timeLeft -= dt;
                if (f.timeLeft <= 0f) timed.RemoveAt(i);
                else timed[i] = f;
            }
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
            HumanLastRampart.Combat.Render.HlrLights.Clear();
            torches.Clear();
            flames.Clear();
            if (flameMaterial != null) Object.Destroy(flameMaterial);
            flashes.Clear();
            towerLight = null;
            towerFill = null;
            doorLantern = null;
            trapBlasts.Clear();
        }
    }
}
