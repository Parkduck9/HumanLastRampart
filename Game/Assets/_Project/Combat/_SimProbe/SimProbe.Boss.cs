using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// 우두머리 성벽손 오우거 균형 측정 (W43): 요새 1·2 점화의 밤 × 쉬움·현실 × 우두머리 대응(빛줄기 없이 / 가까워지면 빛줄기 / 빛줄기 + 대포 조준)
// 정비는 시험용 정책(정원 60% 모집 → 강화 → 남은 골드로 모집). 결과는 HLR_PROBE_OUT 파일에 표로
public sealed partial class SimProbe
{
    sealed class BossRow
    {
        public string Setup;
        public bool Killed, Reached, Won, Timeout;
        public float AppearAt, KillSeconds, ReachSeconds, EndAt;
        public int Falls, BossHpLeft, BossMax, WallLoss, SoldiersLost;
        public int ReachSide;
    }

    [Test, Timeout(3600000)]
    public void BossBalance()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        string[] responses = { "빛줄기 안 씀(위협 기준만)", "우두머리가 55m 안이면 빛줄기", "빛줄기 + 대포 조준" };
        var rows = new List<BossRow>();
        foreach (float invest in new[] { 0.4f, 1f })
            foreach (int fortress in new[] { 1, 2 })
                foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Realistic })
                    for (int mode = 0; mode < 3; mode++)
                    {
                        BossRow row = RunBossNight(lookup, fortress, difficulty, mode, invest);
                        row.Setup = $"{(invest < 1f ? "병력 40%" : "병력 정원·강화")} · 요새 {fortress} · {(difficulty == Difficulty.Easy ? "쉬움" : "현실")} · {responses[mode]}";
                        rows.Add(row);
                    }

        var log = new StringBuilder();
        log.AppendLine("| 설정 | 결과 | 나옴 | 처치 | 통로 도착 | 떨어뜨림 | 우두머리 체력 | 성벽 손실 | 잃은 병사 |");
        log.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (BossRow r in rows)
            log.AppendLine($"| {r.Setup} | {(r.Timeout ? "시간초과" : r.Won ? "승리" : "패배")} {r.EndAt:0}s | {r.AppearAt:0}s | {(r.Killed ? $"{r.KillSeconds:0}s 걸림" : "못 잡음")} | {(r.Reached ? $"{r.ReachSeconds:0}s에 {r.ReachSide}면" : "아니오")} | {r.Falls} | {r.BossHpLeft}/{r.BossMax} | {r.WallLoss} | {r.SoldiersLost} |");
        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe_boss.txt", log.ToString(), new UTF8Encoding(false));
    }

    static BossRow RunBossNight(DefinitionLookup lookup, int fortress, Difficulty difficulty, int mode, float invest)
    {
        CampaignState c = CampaignFactory.Create(lookup, new NewGameOptions(difficulty), 1234);
        c.FortressNumber = fortress;
        c.NightNumber = BeaconRules.NightsIn(fortress);
        c.BattleNumber = fortress == 1 ? 2 : 5;
        c.BeaconRepair = 100f;
        c.PlannedLamp = LampBrightness.Normal;
        c.Gold = 30000;
        int globalCap = lookup.All<PreparationEconomyDef>()[0].GlobalCapacity;
        if (invest < 1f) HireToCaps(c, lookup, Mathf.RoundToInt(globalCap * invest));   // 강화 없이 일부만
        else
        {
            HireToCaps(c, lookup, globalCap * 6 / 10);
            SpendOnUpgrades(c, lookup, TroopsFull(c, lookup));
            HireToCaps(c, lookup, globalCap);
        }
        BattleSetup setup = CreateFitted(c, lookup, $"boss_{fortress}_{difficulty}_{mode}", out string error, new StringBuilder());
        Assert.IsNotNull(setup, error);
        Assert.IsTrue(setup.IsIgnitionNight, "점화의 밤이어야 한다");
        var row = new BossRow();
        using (var sim = new BattleSim(setup, lookup))
        {
            Reinforce(sim, 0.34f, new StringBuilder());
            int soldiers0 = sim.Sides.Sum(s => s.GarrisonStart);
            int wall0 = sim.Sides.Sum(s => s.WallHp);
            float nextAssist = 0f;
            while (!sim.Ended && sim.Elapsed < 1500f)
            {
                if (sim.Elapsed >= nextAssist)
                {
                    Assist(sim);
                    if (mode == 0 || !sim.BossActive) SteerBeam(sim);
                    nextAssist = sim.Elapsed + 2f;
                }

                if (mode >= 1 && sim.BossActive)
                {
                    Vector3 p = sim.BossPosition;
                    float fromWall = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) - sim.CastleHalf;
                    if (fromWall <= 55f && sim.BeamSide != sim.BossSide) sim.RotateBeam(sim.BossSide);
                    if (mode == 2)
                        for (int i = 0; i < sim.CannonCount; i++)
                        {
                            var cannon = sim.GetCannon(i);
                            if (cannon.Alive && Vector3.Distance(cannon.Position, new Vector3(p.x, 0f, p.z)) <= cannon.Range - 4f) sim.SetCannonAim(i, new Vector3(p.x, 0f, p.z));
                        }
                }

                sim.BeginFrame(0.05f);
                if (sim.Ended) break;
                sim.Schedule(0.05f, sim.Elapsed);
                sim.Complete();
                if (sim.BossReachedWalkway && !row.Reached) { row.Reached = true; row.ReachSeconds = sim.BossReachedAt - sim.BossAppearedAt; row.ReachSide = sim.BossSide; }
            }

            row.Timeout = !sim.Ended;
            row.EndAt = sim.Elapsed;
            row.Won = sim.Ended && sim.Outcome == BattleOutcome.Victory;
            row.AppearAt = sim.BossAppearedAt;
            row.Killed = sim.BossKilled;
            row.KillSeconds = sim.BossKilled ? sim.BossKilledAt - sim.BossAppearedAt : 0f;
            row.Falls = sim.BossFalls;
            row.BossMax = sim.BossMaxHp;
            row.BossHpLeft = sim.BossKilled ? 0 : sim.BossHp;
            row.WallLoss = wall0 - sim.Sides.Sum(s => s.WallHp);
            row.SoldiersLost = soldiers0 - sim.Sides.Sum(s => s.GarrisonAlive);
        }

        return row;
    }
}
