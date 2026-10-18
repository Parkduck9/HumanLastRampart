using HumanLastRampart.Core.Definitions;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 3등신 시험 그림 바꿔 끼우기 (K-057, Codex K-056 시안 — 사용자 확정 전 비교용)
    // - Enabled를 켜면 궁수(서기·사격)와 주인공(뒷모습 서기·옆 달리기·앞모습 서기)만 시험 그림으로 그린다. 다른 병종·적은 그대로
    // - 전투 중에 켜고 꺼도 다음 그리기부터 바뀐다(BattleView가 블록 그림만 바꿔 끼움) → 같은 순간 old/new 사진을 찍을 수 있다
    // - 기본은 꺼짐: 꺼져 있으면 그림을 불러오지도 않고 지금 게임과 똑같다
    // - 그림: Art/Battle/Resources/BattleArt/ProportionTrial/trial3_*.png (Docs 원본의 사본, 점 그대로·압축 없음).
    //   인물 키 192px → PerfLabArt.FigurePx가 앞글자 trial3_로 알아보고 기존 그림과 같은 월드 키로 맞춘다
    // - 주인공 시안에 없는 걷기 칸은 시트 합성 때 서기(뒷모습·앞모습)·달리기(옆 걷기)로 채웠다
    public static class ProportionTrial
    {
        public static bool Enabled;

        public const string ArcherId = "unit_archer";
        const string Folder = "BattleArt/ProportionTrial/";

        static Texture2D archerStatic, archerFire, heroGrid, heroIdle;
        static bool loaded;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            archerStatic = Resources.Load<Texture2D>(Folder + "trial3_archer_static");
            archerFire = Resources.Load<Texture2D>(Folder + "trial3_archer_fire");
            heroGrid = Resources.Load<Texture2D>(Folder + "trial3_hero_grid");
            heroIdle = Resources.Load<Texture2D>(Folder + "trial3_hero_back_idle");
        }

        public static bool IsArcher(UnitDef unit) => unit != null && unit.Id == ArcherId;

        // 시험 그림이 있으면 그것, 없으면 원래 그림 (꺼져 있으면 늘 원래 그림)
        public static Texture2D ArcherStatic(Texture2D original) { if (!Enabled) return original; Load(); return archerStatic != null ? archerStatic : original; }
        public static Texture2D ArcherFire(Texture2D original) { if (!Enabled) return original; Load(); return archerFire != null ? archerFire : original; }
        public static Texture2D HeroGrid(Texture2D original) { if (!Enabled) return original; Load(); return heroGrid != null ? heroGrid : original; }
        public static Texture2D HeroIdle(Texture2D original) { if (!Enabled) return original; Load(); return heroIdle != null ? heroIdle : original; }

        // 시험 그림으로 바뀌면 원래 노멀맵은 칸이 안 맞으니 뺀다
        public static Texture Normal(Texture2D shown, Texture2D original, Texture normal) => shown == original ? normal : null;
    }
}
