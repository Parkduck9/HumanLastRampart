using System.Collections.Generic;
using System.Reflection;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using UnityEngine;

namespace HumanLastRampart.App.Tests
{
    // 시험용 최소 정의 묶음: 성벽 4개, 대포 지점 4×4, 분대 2개, 웨이브 2개
    static class AppTestData
    {
        public static T Make<T>(string id, params (string field, object value)[] fields) where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            Set(instance, typeof(DefinitionBase), "id", id);
            foreach ((string field, object value) in fields)
            {
                Set(instance, typeof(T), field, value);
            }

            return instance;
        }

        public static void Set(object target, System.Type type, string field, object value)
        {
            FieldInfo info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info == null) throw new System.ArgumentException($"{type.Name}.{field} 필드가 없습니다");
            info.SetValue(target, value);
        }

        public static DefinitionCatalog Catalog(IEnumerable<DefinitionBase> entries)
        {
            var catalog = ScriptableObject.CreateInstance<DefinitionCatalog>();
            Set(catalog, typeof(DefinitionCatalog), "entries", new List<DefinitionBase>(entries));
            return catalog;
        }

        public static List<DefinitionBase> ValidSet()
        {
            var list = new List<DefinitionBase>
            {
                Make<WeaponDef>("weapon_bow"),
                Make<UnitDef>("unit_archer", ("kind", UnitKind.Archer), ("defaultWeaponId", "weapon_bow")),
                Make<UnitDef>("unit_hero", ("kind", UnitKind.Hero)),
                Make<EnemyDef>("enemy_goblin"),
                Make<TerrainDef>("terrain_1"),
                Make<BattleRulesDef>("battle_rules"),
                Make<WaveDef>("wave_1", ("groups", new List<WaveGroup> { new WaveGroup { Side = WallSide.North, EnemyDefId = "enemy_goblin", Count = 100 } })),
                Make<WaveDef>("wave_2", ("groups", new List<WaveGroup> { new WaveGroup { Side = WallSide.South, EnemyDefId = "enemy_goblin", Count = 301 } })),
            };

            foreach (WallSide side in new[] { WallSide.North, WallSide.East, WallSide.South, WallSide.West })
            {
                for (int i = 1; i <= 4; i++)
                {
                    list.Add(Make<PostDef>($"post_{side}_cannon_{i}", ("wall", side), ("kind", PostKind.Cannon)));
                }

                list.Add(Make<PostDef>($"post_{side}_squad_1", ("wall", side), ("kind", PostKind.Squad)));
            }

            list.Add(Make<CampaignStartDef>("campaign_start",
                ("startGold", 100),
                ("firstStoryId", "story_intro"),
                ("walls", new List<WallStart>
                {
                    new WallStart { Side = WallSide.North, MaxHp = 1000 },
                    new WallStart { Side = WallSide.East, MaxHp = 1000 },
                    new WallStart { Side = WallSide.South, MaxHp = 1000 },
                    new WallStart { Side = WallSide.West, MaxHp = 1000 },
                }),
                ("heroes", new List<HeroStart> { new HeroStart { UnitDefId = "unit_hero" } }),
                ("squads", new List<SquadStart>
                {
                    new SquadStart { UnitDefId = "unit_archer", Count = 20, PostId = "post_North_squad_1" },
                    new SquadStart { UnitDefId = "unit_archer", Count = 15, PostId = "post_South_squad_1" },
                }),
                ("waveByBattle", new List<string> { "wave_1", "wave_2" }),
                ("terrainOrder", new List<string> { "terrain_1" })));
            return list;
        }
    }
}