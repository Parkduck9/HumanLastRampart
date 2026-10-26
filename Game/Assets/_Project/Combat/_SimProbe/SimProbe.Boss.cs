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

// 우두머리 성벽손 오우거 균형 측정 (W43): 요새 1·2 점화의 밤 × 쉬움·현실 × 우두머리 대응(빛줄기 없이 / 가까워지면 빛줄기 / 빛줄기 + 대포를 오르는 자리에 조준) × 여러 씨앗
// 정비는 시험용 정책(정원 60% 모집 → 강화 → 남은 골드로 모집). 결과는 HLR_PROBE_OUT 파일에 표로 (씨앗마다 한 줄이 아니라 설정마다 합계·평균)
public sealed partial class SimProbe
{
    const int BossSeeds = 8;

    sealed class BossRow
    {
        public bool Killed, Reached, Climbed, Won, Timeout;
        public float AppearAt, KillSeconds, ReachSeconds, EndAt;
        public int Falls, BossHpLeft, BossMax, WallLoss, SoldiersLost;
        public int KilledPhase = -1;   // 0 걷는 중 1 오르는 중 2 통로 위 3 쓰러져 있는 중
    }

    [Test, Timeout(3600000)]
    public void BossBalance()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        string[] responses = { "빛줄기 안 씀", "55m 안이면 빛줄기", "빛줄기 + 대포를 오르는 자리에" };
        var log = new StringBuilder();
        log.AppendLine($"씨앗 {BossSeeds}개씩. 처치 = 우두머리를 쓰러뜨린 판 수, 오름 = 한 번이라도 오르기 시작, 통로 = 통로 위 도착, 오르다 = 오르는 중에 처치, 걷다 = 오르기 전에 처치, 통로에서 = 통로 위에서 처치");
        log.AppendLine("| 설정 | 승 | 처치 | 걷다 | 오름 | 오르다 | 통로 | 통로에서 | 처치 시간(평균) | 떨어뜨림(평균) | 성벽 손실(평균) | 잃은 병사(평균) |");
        log.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        bool quick = Environment.GetEnvironmentVariable("HLR_BOSS_QUICK") == "1";   // 빠른 측정: 요새 1 40% 병력, 요새 2 정원 병력만
        foreach (float invest in new[] { 0.4f, 1f })
            foreach (int fortress in new[] { 1, 2 })
                foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Realistic })
                    for (int mode = 0; mode < 3; mode++)
                    {
                        if (quick && (invest < 1f) != (fortress == 1)) continue;
                        var rows = new List<BossRow>();
                        for (int seed = 0; seed < BossSeeds; seed++) rows.Add(RunBossNight(lookup, fortress, difficulty, mode, invest, 1234 + seed * 17));
                        int n = rows.Count, killed = rows.Count(r => r.Killed);
                        float killTime = killed > 0 ? rows.Where(r => r.Killed).Average(r => r.KillSeconds) : 0f;
                        string setup = $"{(invest < 1f ? "병력 40%" : "병력 정원·강화")} · 요새 {fortress} · {(difficulty == Difficulty.Easy ? "쉬움" : "현실")} · {responses[mode]}";
                        log.AppendLine($"| {setup} | {rows.Count(r => r.Won)}/{n} | {killed}/{n} | {rows.Count(r => r.KilledPhase == 0)} | {rows.Count(r => r.Climbed)} | {rows.Count(r => r.KilledPhase == 1)} | {rows.Count(r => r.Reached)} | {rows.Count(r => r.KilledPhase == 2)} | {(killed > 0 ? killTime.ToString("0") + "s" : "-")} | {rows.Average(r => r.Falls):0.0} | {rows.Average(r => r.WallLoss):0} | {rows.Average(r => r.SoldiersLost):0} |");
                    }

        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe_boss.txt", log.ToString(), new UTF8Encoding(false));
    }

    static BossRow RunBossNight(DefinitionLookup lookup, int fortress, Difficulty difficulty, int mode, float invest, int seed)
    {
        CampaignState c = CampaignFactory.Create(lookup, new NewGameOptions(difficulty), seed);
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
        BattleSetup setup = CreateFitted(c, lookup, $"boss_{fortress}_{difficulty}_{mode}_{seed}", out string error, new StringBuilder());
        Assert.IsNotNull(setup, error);
        Assert.IsTrue(setup.IsIgnitionNight, "점화의 밤이어야 한다");
        var row = new BossRow();
        using (var sim = new BattleSim(setup, lookup))
        {
            Reinforce(sim, 0.34f, new StringBuilder());
            int soldiers0 = sim.Sides.Sum(s => s.GarrisonStart);
            int wall0 = sim.Sides.Sum(s => s.WallHp);
            float nextAssist = 0f;
            int phase = 0;
            while (!sim.Ended && sim.Elapsed < 1500f)
            {
                if (sim.Elapsed >= nextAssist)
                {
                    Assist(sim);
                    if (!sim.BossActive) SteerBeam(sim);
                    nextAssist = sim.Elapsed + 2f;
                }

                // 빛줄기 안 씀: 우두머리가 나온 뒤에는 빛줄기를 우두머리 반대쪽에 둔다 (우두머리는 빛 밖)
                if (mode == 0 && sim.BossActive && sim.BeamSide == sim.BossSide && !sim.BeamRotating) sim.RotateBeam((sim.BossSide + 2) % 4);

                if (mode >= 1 && sim.BossActive)
                {
                    Vector3 p = sim.BossPosition;
                    float fromWall = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) - sim.CastleHalf;
                    if (fromWall <= 55f && sim.BeamSide != sim.BossSide) sim.RotateBeam(sim.BossSide);
                    if (mode == 2)
                    {
                        // 대포: 우두머리가 사정에 들면 따라가고, 성벽 가까이(25m 안)부터는 오르는 자리에 고정
                        Vector3 aim = fromWall <= 25f ? sim.BossTargetPoint : p;
                        aim.y = 0f;
                        for (int i = 0; i < sim.CannonCount; i++)
                        {
                            var cannon = sim.GetCannon(i);
                            if (cannon.Alive && Vector3.Distance(cannon.Position, aim) <= cannon.Range - 4f) sim.SetCannonAim(i, aim);
                        }
                    }
                }

                sim.BeginFrame(0.05f);
                if (sim.Ended) break;
                sim.Schedule(0.05f, sim.Elapsed);
                sim.Complete();
                if (sim.BossActive) phase = sim.BossDownLeft > 0f ? 3 : sim.BossOnWalkway ? 2 : sim.BossClimbing ? 1 : 0;
                if (sim.BossClimbing) row.Climbed = true;
                if (sim.BossReachedWalkway && !row.Reached) { row.Reached = true; row.ReachSeconds = sim.BossReachedAt - sim.BossAppearedAt; }
            }

            row.Timeout = !sim.Ended;
            row.EndAt = sim.Elapsed;
            row.Won = sim.Ended && sim.Outcome == BattleOutcome.Victory;
            row.AppearAt = sim.BossAppearedAt;
            row.Killed = sim.BossKilled;
            if (row.Killed) row.KilledPhase = phase;
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
