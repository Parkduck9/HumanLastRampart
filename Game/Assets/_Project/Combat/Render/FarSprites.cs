using UnityEngine;

namespace HumanLastRampart.Combat.Render
{
    // 먼 적 저비용 그림 (W4b, K-099): 고블린·사다리 고블린·오우거를 거리에 따라 절반/1/4 크기 시트로 그린다.
    // 같은 개체 버퍼를 시트마다 한 번씩 더 그리고, 셰이더(_LodBand)가 카메라 거리 띠 밖의 개체를 잘라 낸다.
    //   가까움 (< HalfFrom)            : 원래 시트
    //   중간 (HalfFrom ~ QuarterFrom)  : 절반 시트 (칸 배치 같음)
    //   멀리 (>= QuarterFrom)          : 1/4 시트 (걷기 2칸 — 셰이더가 칸 번호를 0·1로 바꿈)
    // 월드 크기·발 위치는 그대로라 전환은 자세한 정도만 바뀐다. 먼 그림이 없으면(또는 Enabled 꺼짐) 예전 그대로 한 번만 그린다.
    public static class FarSprites
    {
        // 꺼 두면 아무것도 더 그리지 않는다 (BattleView가 바뀐 것을 한 번 알아채고 가까운 그림의 거리 띠를 푼다)
        // 2026-10-22 측정: 이 컴퓨터(데스크톱)에서는 켜면 75초 장면이 약 1ms 느렸다(정점 셰이더를 세 번 돈다). 모바일에서 다시 재기 전까지 꺼 둔다
        public static bool Enabled = false;

        public const float HalfFrom = 60f;       // m — 이보다 멀면 절반 시트 (manifest 제안값, Claude 측정 뒤 확정)
        public const float QuarterFrom = 110f;   // m — 이보다 멀면 1/4 시트
        public const float Unbounded = 1e6f;
        public const string Folder = "BattleArt/Far/";
        public const string HalfSuffix = "_half", QuarterSuffix = "_quarter";
        public const int QuarterColumns = 2;     // 1/4 시트: 걷기 두 자세 가로 2칸, 1행

        // 셰이더 _LodBand = (가장 가까운 거리, 가장 먼 거리, 1/4 칸 바꾸기(0/1), 켬(0/1))
        public static readonly Vector4 Off = new Vector4(0f, 0f, 0f, 0f);
        public static readonly Vector4 NearBand = new Vector4(0f, HalfFrom, 0f, 1f);
        public static readonly Vector4 HalfBand = new Vector4(HalfFrom, QuarterFrom, 0f, 1f);
        public static readonly Vector4 QuarterBand = new Vector4(QuarterFrom, Unbounded, 1f, 1f);

        // 원래 이동 시트 이름 → 먼 그림 이름 앞부분 (manifest.json sources)
        public static string SpeciesOf(Texture nearSheet)
        {
            if (nearSheet == null) return null;
            switch (nearSheet.name)
            {
                case "goblin_walk_attack4": return "goblin";
                case "ladder_goblin_walk": return "ladder_goblin";
                case "ladder_goblin_carry_run4": return "ladder_carry";
                case "ogre_walk": return "ogre";
                default: return null;
            }
        }

        // 1/4 시트의 걷기 칸 (원래 걷기 4칸 → 앞 두 칸 = 0, 뒤 두 칸 = 1). 셰이더 Billboard와 같은 식 — 시험용
        public static int QuarterFrame(int walkFrame) => (walkFrame % 4) >= 2 ? 1 : 0;
    }
}
