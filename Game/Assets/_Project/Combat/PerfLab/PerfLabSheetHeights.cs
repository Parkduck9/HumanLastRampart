using System.Collections.Generic;

namespace HumanLastRampart.Combat.PerfLab
{
    // 자동 생성 — 손으로 고치지 말고 Tools/SheetMeasure/measure-sheets.ps1을 다시 돌린다 (K-034)
    // 시트(텍스처 이름) → 칸 384px 안 인물 기준 키(px, 똑바로 선 키로 환산). 발 줄은 모두 칸 위에서 352 (표: Tools/SheetMeasure/sheet-heights.md)
    public static class PerfLabSheetHeights
    {
        public static readonly Dictionary<string, float> FigurePx = new Dictionary<string, float>
        {
            { "archer_fall", 273.3f },
            { "archer_fire", 249f },
            { "archer_run", 226.5f },
            { "archer_static", 300f },
            { "cannon_crew_fall", 253f },
            { "cannon_crew_run", 248.9f },
            { "cannon_crew_static", 300f },
            { "goblin_fall4", 221f },
            { "goblin_walk_attack4", 240f },
            { "gunner_fall", 250.5f },
            { "gunner_fire", 265f },
            { "gunner_run", 262.1f },
            { "gunner_static", 300f },
            { "hero_back_idle", 300f },
            { "hero_command", 291f },
            { "hero_walk_run_grid", 297f },
            { "ladder_goblin_carry_run4", 260f },
            { "ladder_goblin_climb4", 246f },
            { "ladder_goblin_deploy4", 242f },
            { "ladder_goblin_static", 272f },
            { "ladder_goblin_walk", 272f },
            { "ogre_attack4", 201f },
            { "ogre_fall4", 206.8f },
            { "ogre_static", 173f },
            { "ogre_walk", 233f },
            { "wizard_back_static", 298f },
            { "wizard_cast", 257f },
            { "wizard_static", 296f },
        };
    }
}
