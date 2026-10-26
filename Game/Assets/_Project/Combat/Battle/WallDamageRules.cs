using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 성벽 부서짐 단계·배치 규칙 (W39, Codex K-126). 순수 계산 — 장면·그림 없이 시험할 수 있다
    // - 단계: 0 멀쩡(체력 66% 이상) · 1 금감(66% 미만) · 2 반파(33% 미만) · 3 파공(0) — 기존 재질 바꿈 기준(K-024)을 그대로 쓴다.
    //   K-123 결과 화면 요새 그림(UiArt.FortressWall)은 70%·35%·0 경계라 조금 다르다: 파공(3) = 그림의 broken(체력 0)과 같고, 70→66·35→33은 전투 재질 기준을 유지한 것
    // - 배치는 전투 씨앗(BattleSim.BattleSeed)과 면·번호로만 정한다 (System.Random·시간 없음). 같은 씨앗 = 같은 자리
    // - 모든 조각은 '이 단계부터 보임(MinStage)'을 갖는다 — 단계가 오르면 이미 있던 것은 그대로 두고 더 얹는다 (줄어들지 않음)
    public static class WallDamageRules
    {
        public const float CrackedBelow = 0.66f;   // 체력 비율이 이보다 낮으면 1단계
        public const float BrokenBelow = 0.33f;    // 이보다 낮으면 2단계
        public const int MaxStage = 3;
        public const float TileMeters = 4f;        // 외벽 데칼 한 장 = 가로 4m (K-126 4m × 4m)

        public enum MerlonState { Intact, Chipped, Half, Gone }
        public enum DecalKind { None, Cracks, Gouges }

        public struct RubblePiece
        {
            public float Along;     // 성벽 따라 m (WallGeometry.Right 방향, 성벽 가운데 0)
            public bool Field;      // true = 성벽 바깥 발치, false = 통로 위
            public int Size;        // 0 소 · 1 중 · 2 대
            public int Variant;     // 0~2
            public float Depth;     // 성벽 면에서 떨어진 거리 m (바깥 = 밖으로, 통로 = 난간 안쪽으로)
            public int MinStage;
        }

        public struct Spot
        {
            public float Along;     // 통로 위 자리
            public bool Fire;       // true = 불꽃, false = 연기
            public int MinStage;
        }

        public static int StageOf(float ratio) => ratio <= 0f ? 3 : ratio < BrokenBelow ? 2 : ratio < CrackedBelow ? 1 : 0;

        // 정수 체력으로 (BattleSim.SideStatus) — 최대 체력이 0이면 멀쩡
        public static int StageOf(int hp, int maxHp) => maxHp > 0 ? StageOf(Mathf.Clamp01((float)hp / maxHp)) : 0;

        // 재질 바꿈(K-024)은 손상 그림이 2종뿐이라 3단계도 2번 그림을 쓴다
        public static int MaterialStage(int stage) => Mathf.Min(stage, 2);

        static uint Mix(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16;
            return x;
        }

        // 0 이상 1 미만, 같은 (씨앗, 면, 번호, 소금)이면 늘 같은 값
        public static float Hash01(uint seed, int side, int key, int salt)
        {
            uint a = Mix(seed ^ 0x9E3779B9U);
            a = Mix(a ^ (uint)(side * 7919 + 17));
            a = Mix(a ^ (uint)(key * 104729 + 31));
            a = Mix(a ^ (uint)(salt * 15485863 + 7));
            return (a >> 8) * (1f / 16777216f);
        }

        // 돌출부 한 개의 상태: h = 그 돌출부의 고유 값(0~1). 단계가 오를수록 나빠지기만 한다
        public static MerlonState MerlonAt(float h, int stage)
        {
            switch (stage)
            {
                case 1: return h < 0.5f ? MerlonState.Chipped : MerlonState.Intact;
                case 2: return h < 0.35f ? MerlonState.Half : h < 0.8f ? MerlonState.Chipped : MerlonState.Intact;
                case 3: return h < 0.5f ? MerlonState.Gone : h < 0.85f ? MerlonState.Half : MerlonState.Chipped;
                default: return MerlonState.Intact;
            }
        }

        public static float MerlonHash(uint seed, int side, int index) => Hash01(seed, side, index, 3);

        // 외벽 4m 칸 한 개의 데칼: h = 그 칸의 고유 값
        public static DecalKind DecalAt(float h, int stage)
        {
            switch (stage)
            {
                case 1: return h < 0.35f ? DecalKind.Cracks : DecalKind.None;
                case 2: return h < 0.25f ? DecalKind.Gouges : h < 0.6f ? DecalKind.Cracks : DecalKind.None;
                case 3: return h < 0.4f ? DecalKind.Gouges : h < 0.8f ? DecalKind.Cracks : DecalKind.None;
                default: return DecalKind.None;
            }
        }

        public static float TileHash(uint seed, int side, int tile) => Hash01(seed, side, tile, 1);

        // 칸 배치: 모서리 발판 사이 [from, to]를 4m 칸으로 나눠 가운데 맞춤
        public static int TileCount(float from, float to) => Mathf.Max(1, Mathf.FloorToInt((to - from) / TileMeters));
        public static float TileCenter(float from, float to, int tile)
        {
            int count = TileCount(from, to);
            float start = from + ((to - from) - count * TileMeters) * 0.5f;
            return start + (tile + 0.5f) * TileMeters;
        }

        // 파공이 나는 칸 (양 끝 칸은 피한다)
        public static int BreachTile(uint seed, int side, int tileCount)
        {
            if (tileCount < 3) return tileCount / 2;
            return Mathf.Clamp(1 + (int)(Hash01(seed, side, 0, 4) * (tileCount - 2)), 1, tileCount - 2);
        }

        // 잔해·불꽃·연기 자리 전부 (단계와 무관한 고정 목록. 보이는 것은 MinStage로 거른다)
        public static void Place(uint seed, int side, float from, float to, IReadOnlyList<float> merlons, List<RubblePiece> rubble, List<Spot> spots)
        {
            rubble.Clear();
            spots.Clear();
            int tiles = TileCount(from, to);
            int breach = BreachTile(seed, side, tiles);
            for (int t = 0; t < tiles; t++)
            {
                if (t == breach) continue;
                float center = TileCenter(from, to, t);
                float h = TileHash(seed, side, t);
                float jitter = (Hash01(seed, side, t, 5) - 0.5f) * TileMeters * 0.6f;
                int variant = Mathf.Min(2, (int)(Hash01(seed, side, t, 6) * 3f));
                float depth = 0.05f + Hash01(seed, side, t, 7) * 0.35f;
                if (DecalAt(h, 1) == DecalKind.Cracks && Hash01(seed, side, t, 2) < 0.5f)
                    rubble.Add(new RubblePiece { Along = center + jitter, Field = true, Size = 0, Variant = variant, Depth = depth, MinStage = 1 });
                if (DecalAt(h, 2) == DecalKind.Gouges)
                    rubble.Add(new RubblePiece { Along = center + jitter, Field = true, Size = 1, Variant = variant, Depth = depth + 0.3f, MinStage = 2 });
                else if (DecalAt(h, 3) == DecalKind.Gouges)
                    rubble.Add(new RubblePiece { Along = center + jitter, Field = true, Size = 1, Variant = variant, Depth = depth + 0.3f, MinStage = 3 });
            }

            float bc = TileCenter(from, to, breach);
            rubble.Add(new RubblePiece { Along = bc - 0.7f, Field = true, Size = 2, Variant = 0, Depth = 0.25f, MinStage = 3 });
            rubble.Add(new RubblePiece { Along = bc + 0.7f, Field = true, Size = 2, Variant = 1, Depth = 0.9f, MinStage = 3 });
            rubble.Add(new RubblePiece { Along = bc + 0.1f, Field = true, Size = 1, Variant = 2, Depth = 1.5f, MinStage = 3 });
            rubble.Add(new RubblePiece { Along = bc, Field = false, Size = 2, Variant = 2, Depth = 0.2f, MinStage = 3 });
            rubble.Add(new RubblePiece { Along = bc - 0.3f, Field = true, Size = 2, Variant = 2, Depth = 0.1f, MinStage = 3 });   // 파공 발치에 더 쌓인 돌
            rubble.Add(new RubblePiece { Along = bc + 0.35f, Field = true, Size = 1, Variant = 0, Depth = 0.05f, MinStage = 3 });
            rubble.Add(new RubblePiece { Along = bc - 1.1f, Field = true, Size = 1, Variant = 1, Depth = 0.6f, MinStage = 3 });

            int smokeAt = -1, fires = 0;
            for (int k = 0; k < merlons.Count; k++)
            {
                float h = MerlonHash(seed, side, k);
                int variant = Mathf.Min(2, (int)(Hash01(seed, side, k, 8) * 3f));
                MerlonState s2 = MerlonAt(h, 2), s3 = MerlonAt(h, 3);
                if (s2 == MerlonState.Half)
                {
                    rubble.Add(new RubblePiece { Along = merlons[k], Field = false, Size = 0, Variant = variant, Depth = 0.12f, MinStage = 2 });
                    if (smokeAt < 0) { smokeAt = k; spots.Add(new Spot { Along = merlons[k], Fire = false, MinStage = 2 }); }
                    else if (s3 == MerlonState.Gone) { rubble.Add(new RubblePiece { Along = merlons[k] + 0.04f, Field = false, Size = 1, Variant = variant, Depth = 0.2f, MinStage = 3 }); if (fires < 2) { fires++; spots.Add(new Spot { Along = merlons[k], Fire = true, MinStage = 3 }); } }
                }
                else if (s3 == MerlonState.Gone)
                {
                    rubble.Add(new RubblePiece { Along = merlons[k], Field = false, Size = 1, Variant = variant, Depth = 0.12f, MinStage = 3 });
                    if (fires < 2) { fires++; spots.Add(new Spot { Along = merlons[k], Fire = true, MinStage = 3 }); }
                }
                else if (s3 == MerlonState.Half)
                    rubble.Add(new RubblePiece { Along = merlons[k], Field = false, Size = 0, Variant = variant, Depth = 0.12f, MinStage = 3 });
            }

            spots.Add(new Spot { Along = bc, Fire = false, MinStage = 3 });
        }
    }
}
