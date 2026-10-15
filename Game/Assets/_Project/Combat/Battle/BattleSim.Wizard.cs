using HumanLastRampart.Core.Definitions;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 마법사 자리와 이동 (D-058)
    // - 평소에는 정면(북) 성벽 가운데, 주인공 시작 자리 바로 왼쪽에 바깥을 보고 서 있다
    // - 다른 성벽 쪽에 마법을 쓰면 성벽 위 둘레를 따라 그 성벽 가운데로 가서, 도착하면 마법이 나간다
    // - 쓴 뒤에는 다음 마법 전까지 그 성벽에 머문다. 이동 중에는 다른 마법을 쓸 수 없다
    public sealed partial class BattleSim
    {
        public const float WizardSpeed = HeroSpeed;   // 주인공과 같은 속도 (D-071 초당 5m)
        const float WizardAlong = -0.4f;               // 가운데에서 왼쪽으로 — 카메라 A/B/C 모두 화면 안, 오른쪽 아래 버튼과 안 겹침
        const float WizardInset = BattleCameraRig.HeroInset;   // 주인공과 같은 줄 (가까워 보여 커지지 않게)

        float wizardU;
        float wizardDirection;
        float wizardDistanceLeft;
        int wizardPendingSpell = -1;
        Vector3 wizardPendingPoint;

        public UnitDef WizardUnit { get; private set; }
        public int WizardSide { get; private set; }
        public bool WizardMoving => wizardPendingSpell >= 0;

        // 발 위치 (성벽 위)
        public Vector3 WizardFeet
        {
            get
            {
                float perimeter = CastleHalf * 8f;
                float u = Mod(wizardU, perimeter);
                int side = math.min(3, (int)(u / (2f * CastleHalf)));
                float along = u - side * 2f * CastleHalf - CastleHalf;
                float3 p = WallGeometry.Outward(side) * (CastleHalf - WizardInset) + WallGeometry.Right(side) * along + new float3(0f, WallHeight, 0f);
                return new Vector3(p.x, p.y, p.z);
            }
        }

        void InitWizard(UnitDef unit)
        {
            WizardUnit = unit;
            WizardSide = 0;
            wizardU = RingU(0, WizardAlong);
        }

        // 마법을 찍은 곳이 어느 성벽 쪽인지 (바깥 방향과 가장 가까운 면)
        static int SideOfPoint(Vector3 point)
        {
            int best = 0;
            float bestDot = float.MinValue;
            for (int side = 0; side < 4; side++)
            {
                float3 n = WallGeometry.Outward(side);
                float d = n.x * point.x + n.z * point.z;
                if (d > bestDot)
                {
                    bestDot = d;
                    best = side;
                }
            }

            return best;
        }

        // 그 성벽에 이미 있으면 true. 아니면 이동을 시작한다
        bool WizardReadyAt(int side, int spellIndex, Vector3 point)
        {
            if (side == WizardSide) return true;
            float perimeter = CastleHalf * 8f;
            float forward = Mod(RingU(side, WizardAlong) - wizardU, perimeter);
            float backward = perimeter - forward;
            wizardDirection = forward <= backward ? 1f : -1f;
            wizardDistanceLeft = math.min(forward, backward);
            wizardPendingSpell = spellIndex;
            wizardPendingPoint = point;
            WizardSide = side;
            return false;
        }

        // BeginFrame에서 (작업이 끝난 뒤)
        void UpdateWizard(float dt)
        {
            if (wizardPendingSpell < 0) return;
            float step = math.min(WizardSpeed * dt, wizardDistanceLeft);
            wizardU += wizardDirection * step;
            wizardDistanceLeft -= step;
            if (wizardDistanceLeft > 0f) return;

            wizardU = RingU(WizardSide, WizardAlong);
            int spell = wizardPendingSpell;
            wizardPendingSpell = -1;
            Notice?.Invoke($"마법사가 {SideNames[WizardSide]} 성벽에 도착");
            StartSpellEffect(spell, wizardPendingPoint);
        }
    }
}
