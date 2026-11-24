using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    public enum WeaponKind
    {
        Bow,
        Crossbow,
        Gun,
        Cannon,
        Magic,
    }

    // 궤적 (D-028 C7, D-029 6.4)
    public enum Trajectory
    {
        Arc,       // 화살: 거리에 따라 아래/살짝 위/높이
        Straight,  // 총: 곡사 없이 직선
        HighArc,   // 대포: 높은 포물선
    }

    [CreateAssetMenu(menuName = "HLR/정의/무기", fileName = "WeaponDef")]
    public sealed class WeaponDef : DefinitionBase
    {
        [SerializeField] WeaponKind kind;
        [SerializeField] Trajectory trajectory;
        [SerializeField] int damage = 10;
        [SerializeField] float range = 50f;
        [SerializeField] float interval = 1f;          // 공격 간격(초)
        [SerializeField] float areaRadius;             // 0이면 단일 대상
        [SerializeField] float projectileSpeed = 45f;

        [Header("거리별 명중 (0~1)과 흩어짐(m) — 시작값, 시험 플레이로 조정")]
        [SerializeField] float nearDistance = 15f;     // 이하 = 가까움
        [SerializeField] float farDistance = 35f;      // 이상 = 멀다
        [SerializeField] float accuracyNear = 0.95f;
        [SerializeField] float accuracyMid = 0.75f;
        [SerializeField] float accuracyFar = 0.45f;
        [SerializeField] float spreadNear = 0.3f;
        [SerializeField] float spreadMid = 1f;
        [SerializeField] float spreadFar = 3f;

        public WeaponKind Kind => kind;
        public Trajectory Trajectory => trajectory;
        public int Damage => damage;
        public float Range => range;
        public float Interval => interval;
        public float AreaRadius => areaRadius;
        public float ProjectileSpeed => projectileSpeed;
        public float NearDistance => nearDistance;
        public float FarDistance => farDistance;
        public float AccuracyNear => accuracyNear;
        public float AccuracyMid => accuracyMid;
        public float AccuracyFar => accuracyFar;
        public float SpreadNear => spreadNear;
        public float SpreadMid => spreadMid;
        public float SpreadFar => spreadFar;
    }
}