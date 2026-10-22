using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.CombatContracts;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 등불 밝기와 등대 빛줄기 (Concept_Beacon_v1 9.3·9.4)
    // - 밝기는 적 수(시작 때만)와 빛줄기 폭·효과를 정한다. 전투 중 바꿔도 적 수는 그대로
    // - 빛줄기는 성 가운데에서 한 방향으로 뻗는 부채꼴. 방향을 바꾸면 3초에 걸쳐 돌아간다 (도는 중에도 그 순간의 각도로 적용)
    // - 빛 안 적: 이동 느려짐 + 받는 피해 증가 (모든 피해가 지나는 ApplyBattleHitsJob에서 한 번에 곱한다)
    // - 밝기별 켜 둔 시간(LampSeconds)을 쌓아 결과에 넣는다 (등대 복구 계산용)
    public sealed partial class BattleSim
    {
        public const float BeamRotateSeconds = 3f;

        public static readonly float[] BeamWidthDegrees = { 60f, 40f, 80f };      // 인덱스 = (int)LampBrightness (보통·약·강)
        public static readonly float[] BeamSlowAmount = { 0.25f, 0.15f, 0.35f };   // 이동 감소
        public static readonly float[] BeamDamageBonus = { 0.20f, 0.10f, 0.30f };  // 받는 피해 증가
        public static readonly float[] LampCountMultiplier = { 1f, 0.8f, 1.3f };   // 전체 적 수 (시작 때만)

        public LampBrightness Lamp { get; private set; }
        public int BeamSide { get; private set; }
        public bool BeamRotating => beamRotateLeft > 0f;
        public float BeamAngleDegrees { get; private set; }   // 수평 각도: +x = 0°, +z(북) = 90°
        public float BeamWidth => BeamWidthDegrees[(int)Lamp];
        public bool BeamSuppressesTiers => Lamp == LampBrightness.High;   // 강 밝기 빛 안에서는 변형 단계 효과를 끈다 (9.4·9.5, UpdateTierBeam)

        readonly float[] lampSeconds = new float[3];
        float beamFrom, beamTo, beamRotateLeft;

        public float GetLampSeconds(LampBrightness b) => lampSeconds[(int)b];

        public static float LampCountMul(LampBrightness b) => LampCountMultiplier[Mathf.Clamp((int)b, 0, 2)];

        // 면 번호 → 수평 각도 (0 북 90° · 1 동 0° · 2 남 -90° · 3 서 180°)
        public static float SideAngle(int side)
        {
            switch (((side % 4) + 4) % 4)
            {
                case 0: return 90f;
                case 1: return 0f;
                case 2: return -90f;
                default: return 180f;
            }
        }

        void InitLamp()
        {
            Lamp = setup.LampBrightness;
            BeamSide = ((setup.BeamSide % 4) + 4) % 4;
            BeamAngleDegrees = beamFrom = beamTo = SideAngle(BeamSide);
            beamRotateLeft = 0f;
        }

        public void SetLamp(LampBrightness brightness)
        {
            if (Ended) return;
            Lamp = brightness;
        }

        // 빛줄기 방향 바꾸기: 지금 각도에서 가까운 쪽으로 3초에 걸쳐 돈다
        public void RotateBeam(int side)
        {
            if (Ended) return;
            side = ((side % 4) + 4) % 4;
            if (side == BeamSide && !BeamRotating) return;
            BeamSide = side;
            beamFrom = BeamAngleDegrees;
            beamTo = beamFrom + Mathf.DeltaAngle(beamFrom, SideAngle(side));
            beamRotateLeft = BeamRotateSeconds;
        }

        // 매 프레임 (BeginFrame): 켜 둔 시간 쌓기 + 빛줄기 각도 갱신
        void UpdateLamp(float dt)
        {
            lampSeconds[(int)Lamp] += dt;
            if (beamRotateLeft > 0f)
            {
                beamRotateLeft = Mathf.Max(0f, beamRotateLeft - dt);
                float t = 1f - beamRotateLeft / BeamRotateSeconds;
                BeamAngleDegrees = Mathf.Lerp(beamFrom, beamTo, t);
            }
        }

        // 작업에 넘기는 빛줄기 값
        BeamField CurrentBeam()
        {
            int k = (int)Lamp;
            float rad = math.radians(BeamAngleDegrees);
            return new BeamField
            {
                Dir = new float2(math.cos(rad), math.sin(rad)),
                CosHalf = math.cos(math.radians(BeamWidthDegrees[k] * 0.5f)),
                SlowMul = 1f - BeamSlowAmount[k],
                DamageMul = 1f + BeamDamageBonus[k],
            };
        }

        // 시험·훅용: 이 적이 지금 빛 안에 있는가 (살아 있는 적만)
        public bool InBeam(int enemyIndex)
        {
            if (enemyIndex < 0 || enemyIndex >= enemyCount || !EnemyState.IsAlive(eState[enemyIndex])) return false;
            return CurrentBeam().Contains(ePos[enemyIndex]);
        }

        // 시험용: 적 위치를 직접 놓는다
        public void DebugPlaceEnemy(int enemyIndex, float3 position)
        {
            ePos[enemyIndex] = position;
        }

        // 시험용: 적 체력 읽기·피해 넣기 (작업이 끝난 뒤에만). 피해는 다음 Schedule의 사건 처리에서 빛 배율이 곱해져 들어간다
        public bool DebugAlive(int enemyIndex) => enemyIndex >= 0 && enemyIndex < enemyCount && EnemyState.IsAlive(eState[enemyIndex]);

        public int DebugSide(int enemyIndex) => eSide[enemyIndex];

        public int DebugEnemyHp(int enemyIndex) => eHp[enemyIndex];

        public void DebugHit(int enemyIndex, int damage)
        {
            hitQueue.Enqueue(new HitEvent { Enemy = new EnemyHandle { Index = enemyIndex, Generation = eGen[enemyIndex] }, Damage = damage });
        }

        // Complete 뒤 매 프레임: 변형된 적이 강 밝기 빛 안이면 변형 효과(체력·속도·크기·색)를 끄고, 빛을 벗어나면 다시 켠다.
        // 변형 없는 전투(1단계)에서는 아무것도 하지 않는다
        void UpdateTierBeam()
        {
            if (EnemyTier <= 0) return;
            bool suppress = BeamSuppressesTiers;
            BeamField beam = CurrentBeam();
            for (int i = 0; i < enemyCount; i++)
            {
                if (!EnemyState.IsAlive(eState[i])) continue;
                SetTierBonusActive(i, !(suppress && beam.Contains(ePos[i])));
            }
        }

        void CopyLampSeconds(BattleResult result)
        {
            for (int i = 0; i < 3; i++) result.LampSeconds[i] = lampSeconds[i];
        }
    }

    // 빛줄기 부채꼴 (성 가운데 기준, 길이는 들판 전체). 각도 검사는 내적 한 번
    public struct BeamField
    {
        public float2 Dir;
        public float CosHalf;
        public float SlowMul;
        public float DamageMul;

        public bool Contains(float3 p)
        {
            float2 xz = new float2(p.x, p.z);
            float len = math.length(xz);
            if (len < 1e-3f) return true;   // 성 한가운데는 빛 안
            return math.dot(xz, Dir) >= CosHalf * len;
        }
    }
}
