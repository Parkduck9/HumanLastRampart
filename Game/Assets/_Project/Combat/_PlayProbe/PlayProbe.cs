using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 임시 자동 Play 확인 도구 (커밋하지 않음): Boot부터 버튼을 눌러 1회차를 끝까지 진행한다
public sealed class PlayProbe
{
    static readonly string Out = Environment.GetEnvironmentVariable("HLR_PLAY_OUT") ?? "playprobe";
    StringBuilder log;
    int shot;

    [UnityTest]
    public IEnumerator EasyFullRun() { yield return Run(Difficulty.Easy, "easy"); }

    [UnityTest]
    public IEnumerator RealisticRun() { yield return Run(Difficulty.Realistic, "realistic"); }

    IEnumerator Run(Difficulty difficulty, string tag)
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;   // 프레임마다 게임 시간 0.05초 (실제 시간과 무관하게 빠르게)
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click(difficulty == Difficulty.Easy ? "쉬움" : "현실", 300);
        Note($"[{tag}] 새 게임 시작");
        yield return WaitScreen(ScreenId.Story, 600);
        Note($"스토리: {GameFlow.Instance.Campaign?.NextStory?.StoryId}");
        yield return Click("다음", 600);

        for (int round = 0; round < 6; round++)
        {
            yield return WaitScreen(ScreenId.Battle, 900);
            CampaignState c = GameFlow.Instance.Campaign;
            int gold0 = c.Gold;
            int number = c.BattleNumber;
            int soldiers0 = c.Squads.Sum(s => s.Count);
            Note($"-- 전투 {number} 시작: 골드 {gold0}, 병사 {soldiers0}, 성벽 {string.Join("/", c.Wall.Select(w => w.Hp))}, 적 배율 {c.ActiveBattleSetup.EnemyCountMultiplier}");
            yield return Click("×4", 300);
            int frames = 0;
            bool shotTaken = false;
            while (FindReady("결과로") == null && frames < 40000)
            {
                frames++;
                if (!shotTaken && frames == 200) { yield return Capture($"{tag}_battle{number}_early"); shotTaken = true; }
                if (frames == 260 && number == 1) yield return Capture($"{tag}_battle{number}_mid");
                yield return null;
            }
            Assert.IsNotNull(FindReady("결과로"), $"전투 {number}가 {frames}프레임 안에 끝나지 않음");
            yield return Capture($"{tag}_battle{number}_end");
            Note($"전투 {number} 끝 ({frames}프레임 ≈ 게임 {frames * 0.2f:0}초): {HudText()}");
            yield return Click("결과로", 60);
            BattleResult r = c.LastBattle;
            Note($"결과: {r.Outcome} {r.DefeatReason} 처치 {r.DefeatedEnemyCount} 골드 {r.EarnedGold} 손실 분대 {r.SquadLosses.Count}(합 {r.SquadLosses.Sum(l => l.LostCount)}) 성벽 피해 {r.WallOutcomes.Count} 영웅 {r.HeroOutcomes.Count}");
            // C-005 v1.3 (M7): 배치 지점 없는 주인공·마법사도 참가 → 영웅 결과 2개
            Assert.AreEqual(2, c.ActiveBattleSetup?.ParticipatingHeroIds.Count ?? r.HeroOutcomes.Count, "참가 영웅 2명");
            Assert.AreEqual(2, r.HeroOutcomes.Count, "영웅 결과 2개 (주인공·마법사)");

            if (r.Outcome == BattleOutcome.Defeat)
            {
                yield return WaitScreen(ScreenId.Story, 900);
                Assert.AreEqual(StoryKind.DefeatEnding, GameFlow.Instance.Campaign.NextStory.Kind, "패배 뒤 패배 엔딩");
                Note("패배 엔딩 화면 (결과 화면 건너뜀)");
                yield return Click("메인으로", 600);
                yield return WaitScreen(ScreenId.MainMenu, 900);
                Assert.IsNull(GameFlow.Instance.Campaign, "메인 복귀 시 진행 데이터 폐기");
                Note("메인 복귀, 진행 데이터 폐기 확인");
                break;
            }

            yield return WaitScreen(ScreenId.Result, 900);
            yield return WaitReady("다음", 300);
            Assert.AreEqual(gold0 + r.EarnedGold, c.Gold, "결과 화면에서 골드 한 번 반영");
            Assert.AreEqual(r.BattleRunId, c.LastAppliedBattleRunId, "반영 표시 기록");
            int lost = r.SquadLosses.Sum(l => l.LostCount);
            Assert.AreEqual(soldiers0 - lost, c.Squads.Sum(s => s.Count), "잃은 병사 반영");
            foreach (WallOutcome w in r.WallOutcomes)
                Assert.AreEqual(w.HpAfter, c.Wall.First(x => x.SegmentId == w.SegmentId).Hp, "성벽 피해 반영");
            Note($"결과 반영 확인: 골드 {gold0}→{c.Gold}, 병사 {soldiers0}→{c.Squads.Sum(s => s.Count)}, 성벽 {string.Join("/", c.Wall.Select(w => w.Hp))}, 다음 번호 {c.BattleNumber}");
            yield return Capture($"{tag}_result{number}");
            // Codex 결과 상세 (G-CX-017): 버튼 3개 → 팝업 → 닫기
            foreach (string detail in new[] { "성벽 피해", "병력 손실", "영웅 상태" })
            {
                yield return Click(detail, 120);
                for (int i = 0; i < 3; i++) yield return null;
                yield return Capture($"{tag}_result{number}_{detail.Replace(" ", "")}");
                yield return ClickPrefix("닫기", 120);
                for (int i = 0; i < 3; i++) yield return null;
            }
            Note($"결과 상세 3개 열고 닫기 확인 (전투 {number})");
            yield return Click("다음", 300);
            yield return WaitScreen(ScreenId.Prep, 900);
            yield return Click("전투 준비 완료", 300);
            yield return null;
            yield return null;
            for (int i = 0; i < 900 && GameFlow.Instance.CurrentScreen == ScreenId.Prep; i++) yield return null;
            if (GameFlow.Instance.CurrentScreen == ScreenId.MainMenu)
            {
                Note("마지막 전투 뒤 메인 복귀");
                break;
            }
        }

        File.WriteAllText(Path.Combine(Out, $"{tag}.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    [UnityTest]
    public IEnumerator CameraCompare()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("쉬움", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        yield return Click("×4", 300);
        for (int i = 0; i < 300; i++) yield return null;   // 북쪽 고블린이 사거리 안으로 들어올 때
        Time.captureDeltaTime = 0.0001f;                     // 찍는 동안 거의 멈춤
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry)
            .GetField("rig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
        // 자동(D-040): 멈춤 → A, 움직이는 중 → B, 다시 멈추면 0.6초 뒤 A
        rig.SetPreset(-1);
        Time.captureDeltaTime = 1f / 20f;
        for (int i = 0; i < 30; i++) yield return null;                       // 멈춰 있음
        yield return Capture($"auto_stopped_blend{rig.MoveBlend:0.00}");
        for (int i = 0; i < 4; i++) { rig.Step(true, 0.05f); yield return null; }
        yield return Capture($"auto_moving_early_blend{rig.MoveBlend:0.00}");
        for (int i = 0; i < 10; i++) { rig.Step(true, 0.05f); yield return null; }
        yield return Capture($"auto_moving_blend{rig.MoveBlend:0.00}");
        for (int i = 0; i < 8; i++) { rig.Step(false, 0.05f); yield return null; }   // 멈춘 직후 (0.4초, 아직 B 유지)
        yield return Capture($"auto_just_stopped_blend{rig.MoveBlend:0.00}");
        for (int i = 0; i < 20; i++) { rig.Step(false, 0.05f); yield return null; }
        yield return Capture($"auto_stopped_again_blend{rig.MoveBlend:0.00}");
        Time.captureDeltaTime = 0f;
    }

    // 전투 중 함정 (CY4-C1, D-050): 북쪽 앞 칸 3곳(성벽에서 약 72m, 궁수 사거리 밖)에 표시한 함정 → 지도에서 눌러 점화 → 폭발·구덩이·지도 표시
    [UnityTest]
    public IEnumerator TrapFlow()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("쉬움", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        // 정비 함정 설치 화면(Codex)이 생기기 전이라 진행 데이터에 직접 넣는다 — 전투 준비는 스토리 '다음'에서 만들어진다
        CampaignState c = GameFlow.Instance.Campaign;
        var terrain = GameFlow.Instance.Definitions.Get<HumanLastRampart.Core.Definitions.TerrainDef>("terrain_1");
        foreach (int column in new[] { 2, 4, 6 })   // D-061 성벽 40m → 가로 8칸
        {
            Vector2 p = HumanLastRampart.Core.Traps.TrapGrid.Center(terrain, 0, column, 12);
            c.Traps.Add(new TrapPlacement
            {
                TrapId = c.NewInstanceId("trap"), TrapDefId = "trap_bomb", TerrainId = "terrain_1",
                SlotId = HumanLastRampart.Core.Traps.TrapGrid.CellId(0, column, 12), ActualX = p.x, ActualZ = p.y, Marked = true, MarkX = p.x, MarkZ = p.y,
            });
        }

        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", flags).GetValue(entry);
        var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("rig", flags).GetValue(entry);
        Assert.AreEqual(3, sim.TrapCount, "이 지형 함정 3개");
        yield return Click("×4", 300);
        int near = 0;
        // 코루틴은 작업이 도는 중에 불리므로 먼저 끝내고 읽는다 (Complete는 여러 번 불러도 된다)
        for (int i = 0; i < 3000 && near < 4 && !sim.Ended; i++) { yield return null; sim.Complete(); near = sim.EnemiesInTrapRange(1); }
        Note($"가운데 함정 근처 적 {near}마리 (시간 {sim.Elapsed:0.0})");
        yield return Click("×1", 60);
        entry.ToggleMap();
        for (int i = 0; i < 3; i++) yield return null;
        entry.MapCamera.SetLevel(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Close);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("trap_0_map_armed");
        Assert.IsTrue(entry.DetonateTrapAt(sim.GetTrap(1).Mark), "지도 표시를 눌러 점화");
        sim.Complete();
        int kills0 = sim.Kills;
        Note($"점화 → 상태 {sim.GetTrap(1).State}");
        // 쉬움은 지도에서 시간이 멈춘다 — 닫아야 터진다
        entry.ToggleMap();
        for (int i = 0; i < 30; i++) yield return null;
        rig.ShowSide(0);
        rig.SetPreset(2);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("trap_1_crater_C");
        sim.Complete();
        Note($"폭발 뒤 상태 {sim.GetTrap(1).State}, 처치 {kills0} → {sim.Kills}");
        Assert.AreEqual(HumanLastRampart.Combat.Battle.BattleSim.TrapState.Exploded, sim.GetTrap(1).State);
        entry.ToggleMap();
        for (int i = 0; i < 3; i++) yield return null;
        entry.MapCamera.SetLevel(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Close);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("trap_2_map_exploded");
        entry.ToggleMap();

        // 연동 (Codex T-CX-035 결과 반영 C-007 v1.1): 전투를 끝까지 이기고 결과 화면에서 터진 함정만 사라지는지
        string exploded = sim.GetTrap(1).TrapId;
        var survivors = new[] { sim.GetTrap(0).TrapId, sim.GetTrap(2).TrapId };
        yield return Click("×4", 60);
        int waited = 0;
        while (FindReady("결과로") == null && waited < 40000) { waited++; yield return null; }
        Assert.IsNotNull(FindReady("결과로"), "전투가 끝나야 함");
        sim.Complete();
        BattleResult result = sim.BuildResult();
        Note($"전투 끝 {result.Outcome}, 터진 함정 {string.Join(",", result.ConsumedTrapIds)}, 밀린 함정 {result.TrapMoves.Count}");
        yield return Click("결과로", 60);
        if (result.Outcome == BattleOutcome.Victory)
        {
            yield return WaitScreen(ScreenId.Result, 900);
            yield return WaitReady("다음", 300);
            Assert.IsFalse(c.Traps.Any(t => t.TrapId == exploded), "터진 함정은 진행 데이터에서 사라진다");
            foreach (string id in survivors)
            {
                TrapPlacement t = c.Traps.FirstOrDefault(x => x.TrapId == id);
                Assert.IsNotNull(t, "안 터진 함정은 남는다");
                Assert.IsTrue(t.Marked && t.SlotId.StartsWith("cell_0_"), "지도 표시·처음 칸은 그대로");
            }

            // 결과 상세를 열고 닫아도(재진입 역할) 다시 반영되지 않는다
            int gold = c.Gold, traps = c.Traps.Count;
            yield return Click("성벽 피해", 120);
            yield return ClickPrefix("닫기", 120);
            Assert.AreEqual(gold, c.Gold); Assert.AreEqual(traps, c.Traps.Count);
            Note($"결과 반영: 함정 {c.Traps.Count}개 남음(터진 {exploded} 제거), 재진입 무변경");
            yield return Capture("trap_3_result");
            yield return Click("다음", 300);
            yield return WaitScreen(ScreenId.Prep, 900);
            Note("정비 화면 진입 정상");
        }
        File.WriteAllText(Path.Combine(Out, "trap_flow.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // 정비 함정 연동 (Codex T-CX-036 정비 함정 + T-CX-035 결과 반영, D-050·054): 정비에서 사서 묻기 → 다음 전투에 들어감 → 점화 → 결과에서 사라짐
    [UnityTest]
    public IEnumerator PrepTrapFlow()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("쉬움", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        yield return Click("[시험] 승리", 300);
        yield return WaitScreen(ScreenId.Result, 900);
        yield return Click("다음", 300);
        yield return WaitScreen(ScreenId.Prep, 900);
        CampaignState c = GameFlow.Instance.Campaign;
        var shop = c.TrapShop.First(s => s.TrapDefId == "trap_bomb");
        Note($"정비 진입: 골드 {c.Gold}, 상점 재고 {shop.Count}, 보충 기록 {c.TrapShopRestockedForBattle}, 다음 전투 {c.BattleNumber}");
        Assert.AreEqual(12, shop.Count, "첫 정비는 시작 재고 그대로 (D-054 12개)");
        int gold0 = c.Gold;

        yield return Click("함정 구매·설치", 300);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("preptrap_0_panel");
        const string cellId = "cell_0_4_12";   // 북쪽 가운데, 성벽에서 약 72m (D-061 가로 8칸)
        Button tile = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == cellId && b.isActiveAndEnabled);
        Assert.IsNotNull(tile, "칸 버튼");
        Assert.IsTrue(tile.interactable);
        tile.onClick.Invoke();
        yield return null;
        yield return Click("설치 견적 확인", 60);
        yield return Click("확정 설치", 60);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("preptrap_1_installed");
        TrapPlacement placed = c.Traps.FirstOrDefault(t => t.SlotId == cellId);
        Assert.IsNotNull(placed, "진행 데이터에 함정");
        Assert.AreEqual(gold0 - 25, c.Gold, "25골드");
        Assert.AreEqual(11, c.TrapShop.First(s => s.TrapDefId == "trap_bomb").Count, "상점 재고 −1");
        Assert.IsTrue(placed.Marked, "지도 표시 기본 켜짐");
        Button again = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == cellId && b.isActiveAndEnabled);
        Assert.IsTrue(again == null || !again.interactable, "설치된 칸은 다시 못 고른다");
        Note($"설치: {placed.TrapId} {placed.TerrainId} {cellId} ({placed.ActualX:0.0},{placed.ActualZ:0.0}) 골드 {gold0}→{c.Gold}");
        yield return Click("닫기", 60);
        yield return Click("전투 준비 완료", 300);
        yield return WaitScreen(ScreenId.Battle, 900);
        BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
        Assert.IsTrue(setup.Traps.Any(t => t.TrapId == placed.TrapId), "다음 전투 준비에 함정");

        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
        Assert.AreEqual(1, sim.TrapCount);
        yield return Click("×4", 300);
        int near = 0;
        for (int i = 0; i < 4000 && near < 4 && !sim.Ended; i++) { yield return null; sim.Complete(); near = sim.EnemiesInTrapRange(0); }
        Note($"전투 2: 함정 근처 적 {near} (시간 {sim.Elapsed:0.0})");
        entry.ToggleMap();
        for (int i = 0; i < 3; i++) yield return null;
        Assert.IsTrue(entry.DetonateTrapAt(sim.GetTrap(0).Mark), "지도에서 점화");
        entry.ToggleMap();
        int waited = 0;
        while (FindReady("결과로") == null && waited < 40000) { waited++; yield return null; }
        sim.Complete();
        BattleResult result = sim.BuildResult();
        Note($"전투 2 끝: {result.Outcome}, 터진 함정 {string.Join(",", result.ConsumedTrapIds)}, 함정 상태 {sim.GetTrap(0).State}");
        yield return Click("결과로", 60);
        if (result.Outcome == BattleOutcome.Victory)
        {
            yield return WaitScreen(ScreenId.Result, 900);
            yield return WaitReady("다음", 300);
            bool exploded = result.ConsumedTrapIds.Contains(placed.TrapId);
            Assert.AreEqual(!exploded, c.Traps.Any(t => t.TrapId == placed.TrapId), "터졌으면 사라지고, 불발이면 남는다");
            Note($"결과 반영: 함정 {(exploded ? "사라짐" : "남음(불발)")} · 남은 함정 {c.Traps.Count}");
            yield return Click("다음", 300);
            yield return WaitScreen(ScreenId.Prep, 900);
            var shop2 = c.TrapShop.First(s => s.TrapDefId == "trap_bomb");
            Note($"두 번째 정비: 상점 재고 {shop2.Count} (11 + 6 = 17 기대), 보충 기록 {c.TrapShopRestockedForBattle}");
            Assert.AreEqual(17, shop2.Count, "정비마다 +6");
            yield return Click("함정 구매·설치", 300);
            yield return Click("닫기", 60);
            Assert.AreEqual(17, c.TrapShop.First(s => s.TrapDefId == "trap_bomb").Count, "같은 정비에서 다시 열어도 보충은 한 번");
        }

        File.WriteAllText(Path.Combine(Out, "prep_trap_flow.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // 정비 모집·수리 (Codex T-CX-028, C-017): 실제 버튼으로 견적 → 확정, 일회 견적, 다음 전투 준비에 반영
    [UnityTest]
    public IEnumerator PrepFlow()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("쉬움", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        yield return Click("[시험] 승리", 300);
        yield return WaitScreen(ScreenId.Result, 900);
        yield return Click("다음", 300);
        yield return WaitScreen(ScreenId.Prep, 900);
        CampaignState c = GameFlow.Instance.Campaign;
        WallSegmentState wall = c.Wall[0];
        wall.Hp = wall.MaxHp - 55;   // 수리 시험용 손상
        int gold0 = c.Gold, total0 = c.Squads.Sum(s => s.Count);
        Note($"정비 시작: 골드 {gold0}, 병력 {total0}, {wall.SegmentId} {wall.Hp}/{wall.MaxHp}");
        yield return Capture("prep_0_screen");

        // 모집: 북쪽 성벽 지점을 4번 넘기면 분대 1 (대포 1~4 다음), 기존 궁수 분대에 11명 보충 → 300명 기준 5골드 × 11
        yield return Click("병력 모집·보충", 300);
        for (int i = 0; i < 4; i++) yield return ClickPrefix("지점:", 60);
        yield return Click("인원 +10", 60);
        yield return Click("모집 견적 확인", 60);
        yield return Capture("prep_1_recruit_quote");
        int squad0 = c.Squads.First(s => s.PostId == "post_north_squad_1").Count;
        yield return Click("확정 모집", 60);
        Assert.AreEqual(gold0 - 55, c.Gold, "궁수 11명 = 55골드");
        // 정비는 목록을 한 번에 바꿔 끼운다(원자적 반영) — 반영 뒤 다시 찾는다
        Assert.AreEqual(squad0 + 11, c.Squads.First(s => s.PostId == "post_north_squad_1").Count, "분대에 11명");
        Assert.AreEqual(total0 + 11, c.Squads.Sum(s => s.Count));
        for (int i = 0; i < 3; i++) yield return null;
        Assert.IsNull(FindReady("확정 모집"), "같은 견적은 다시 확정할 수 없다");
        yield return Capture("prep_2_recruit_done");
        yield return Click("닫기", 60);

        // 수리: 55HP 손상, 100HP 요청 → 실제 55HP, 6골드(올림)
        int gold1 = c.Gold;
        yield return Click("성벽 수리", 300);
        yield return Click("선택 성벽 100HP 수리 견적", 60);
        yield return Click("확정 수리", 60);
        wall = c.Wall.First(w => w.SegmentId == wall.SegmentId);
        Assert.AreEqual(wall.MaxHp, wall.Hp, "55HP 수리");
        Assert.AreEqual(gold1 - 6, c.Gold, "55HP = 6골드 (올림)");
        yield return Capture("prep_3_repair_done");
        yield return Click("닫기", 60);
        Note($"모집 11명 −55골드, 수리 55HP −6골드 → 골드 {c.Gold}, 병력 {c.Squads.Sum(s => s.Count)}");

        yield return Click("전투 준비 완료", 300);
        yield return WaitScreen(ScreenId.Battle, 900);
        BattleSetup next = GameFlow.Instance.Campaign.ActiveBattleSetup;
        Assert.AreEqual(2, next.BattleNumber);
        Assert.AreEqual(total0 + 11, next.Squads.Sum(s => s.Count), "다음 전투 준비에 새 병력");
        Assert.AreEqual(wall.MaxHp, next.Wall.First(w => w.SegmentId == wall.SegmentId).Hp, "다음 전투 준비에 수리한 성벽");
        Note("다음 전투 준비에 모집·수리 반영 확인");
        yield return Capture("prep_4_next_battle");
        File.WriteAllText(Path.Combine(Out, "prep_flow.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // 주인공 표시 (P3-C02 준비, M-CX-045 시트): 서 있기 뒷모습 A/B/C, 걷기 오른쪽·왼쪽, Q/E 달리기
    [UnityTest]
    public IEnumerator HeroPose()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("쉬움", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        for (int i = 0; i < 200; i++) yield return null;   // 적이 조금 다가오게
        Time.captureDeltaTime = 0.0001f;                    // 전투는 거의 멈춤 (카메라·주인공은 실제 시간)
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("rig", flags).GetValue(entry);
        rig.ShowSide(0);
        for (int p = 0; p < 3; p++)
        {
            rig.SetPreset(p);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(HumanLastRampart.Combat.Battle.BattleCameraRig.HeroPose.Idle, rig.Pose);
            yield return Capture($"hero_idle_{"ABC"[p]}");
        }

        rig.SetPreset(0);
        rig.WalkInput = 1f;
        for (int i = 0; i < 8; i++) yield return null;
        Assert.AreEqual(HumanLastRampart.Combat.Battle.BattleCameraRig.HeroPose.Run, rig.Pose, "D = 늘 달리기 (D-061)");
        Assert.IsTrue(rig.FacingRight, "오른쪽으로 가면 오른쪽 보기");
        yield return Capture("hero_run_right_A");
        rig.WalkInput = -1f;
        for (int i = 0; i < 8; i++) yield return null;
        Assert.IsFalse(rig.FacingRight, "왼쪽으로 가면 왼쪽 보기");
        yield return Capture("hero_run_left_A");
        rig.WalkInput = 0f;
        rig.DepthInput = -1f;
        for (int i = 0; i < 6; i++) yield return null;
        Assert.AreEqual(HumanLastRampart.Combat.Battle.BattleCameraRig.HeroPose.Walk, rig.Pose, "S = 통로 안쪽 걷기");
        Assert.IsTrue(rig.FacingFront, "안쪽으로 걸으면 앞모습");
        yield return Capture("hero_walk_in_A");
        rig.DepthInput = 1f;
        for (int i = 0; i < 6; i++) yield return null;
        Assert.IsFalse(rig.FacingFront, "바깥쪽으로 걸으면 뒷모습");
        yield return Capture("hero_walk_out_A");
        rig.DepthInput = 0f;
        // 모서리 돌기: 오른쪽으로 계속 달리면 동쪽 성벽으로 넘어간다
        rig.ShowSide(0);
        rig.WalkInput = 1f;
        var cornerWatch = System.Diagnostics.Stopwatch.StartNew();
        while (rig.Side == 0 && cornerWatch.Elapsed.TotalSeconds < 10) yield return null;
        for (int i = 0; i < 2; i++) yield return null;
        Note($"모서리: 북 → {rig.Side}면, 카메라 방향 {rig.Yaw:0}도 (돌아가는 중)");
        yield return Capture("hero_corner_turning_A");
        rig.WalkInput = 0f;
        var turnWatch = System.Diagnostics.Stopwatch.StartNew();
        while (turnWatch.Elapsed.TotalSeconds < 0.6) yield return null;   // 카메라가 도는 시간(실제 시간 약 0.3초)
        Assert.AreEqual(1, rig.Side, "오른쪽으로 계속 달리면 동쪽 성벽");
        Assert.AreEqual(90f, Mathf.DeltaAngle(0f, rig.Yaw), 1f, "카메라가 동쪽으로 다 돌았다");
        Note($"모서리 뒤: 카메라 방향 {rig.Yaw:0}도 (동쪽 90도)");
        yield return Capture("hero_corner_after_A");
        rig.ShowSide(0);
        rig.SetPreset(2);
        rig.RunToNextWall(1);
        for (int i = 0; i < 10; i++) yield return null;
        Assert.AreEqual(HumanLastRampart.Combat.Battle.BattleCameraRig.HeroPose.Run, rig.Pose, "E 달리기");
        yield return Capture("hero_run_right_C");
        rig.WalkInput = 0f;
        Note($"주인공: 서 있기 A/B/C, 걷기 오른쪽·왼쪽, 달리기 확인 (along {rig.Along:0.0})");
        File.WriteAllText(Path.Combine(Out, "hero_pose.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // D-058: 마법사 밤색 뒷모습 — 정면 가운데 A/B/C, 동쪽 마법 뒤 동쪽 가운데로 이동
    [UnityTest]
    public IEnumerator WizardPose()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("쉬움", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        for (int i = 0; i < 100; i++) yield return null;
        Time.captureDeltaTime = 0.0001f;
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("rig", flags).GetValue(entry);
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", flags).GetValue(entry);
        Camera cam = Camera.allCameras.OrderBy(c => c.depth).First();

        void Measure(string name)
        {
            sim.Complete();
            Vector3 feet = sim.WizardFeet;
            Vector3 hero = rig.HeroFeet;
            float size = HumanLastRampart.Combat.Battle.BattleView.HeroDrawSize;
            float Px(Vector3 f) => (cam.WorldToScreenPoint(f + Vector3.up * size).y - cam.WorldToScreenPoint(f).y) * 900f / Screen.height;
            Vector3 s = cam.WorldToScreenPoint(feet);
            Note($"{name}: 마법사 발 ({feet.x:0.0},{feet.y:0.0},{feet.z:0.0}) 화면 ({s.x / Screen.width:0.00},{s.y / Screen.height:0.00}) 키 {Px(feet):0}px · 주인공 키 {Px(hero):0}px (1600×900 기준)");
        }

        rig.ShowSide(0);
        for (int p = 0; p < 3; p++)
        {
            rig.SetPreset(p);
            for (int i = 0; i < 5; i++) yield return null;
            Measure($"정면 {"ABC"[p]}");
            yield return Capture($"wizard_front_{"ABC"[p]}");
        }

        sim.Complete();
        Assert.IsTrue(sim.CastSpell(0, new Vector3(sim.CastleHalf + 40f, 0f, 0f)), "동쪽 마법");
        Note($"동쪽 마법: 이동 {sim.WizardMoving}, 목표 {sim.WizardSide}");
        Time.captureDeltaTime = 1f / 20f;
        rig.SetPreset(2);
        for (int i = 0; i < 120; i++) yield return null;   // 약 6초 — 가는 중
        Time.captureDeltaTime = 0.0001f;
        rig.ShowSide(1);
        for (int i = 0; i < 5; i++) yield return null;
        Measure("이동 중");
        yield return Capture("wizard_moving_C");
        Time.captureDeltaTime = 1f / 20f;
        for (int i = 0; i < 400 && sim.WizardMoving; i++) yield return null;
        Time.captureDeltaTime = 0.0001f;
        Assert.IsFalse(sim.WizardMoving, "도착");
        rig.SetPreset(0);
        for (int i = 0; i < 5; i++) yield return null;
        Measure("동쪽 도착 A");
        yield return Capture("wizard_east_A");
        File.WriteAllText(Path.Combine(Out, "wizard_pose.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // K-001 성벽·포탑 3D 목업: 시안별로 정면 가운데 A·B·C, 대포 사격구, 모서리 포탑, 전체 보기
    [UnityTest] public IEnumerator WorldMock1() { yield return WorldMock(1); }
    [UnityTest] public IEnumerator WorldMock2() { yield return WorldMock(2); }

    IEnumerator WorldMock(int draft)
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        HumanLastRampart.Combat.Battle.BattleWorldView.Draft = draft;
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("쉬움", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        for (int i = 0; i < 300; i++) yield return null;
        Time.captureDeltaTime = 0.0001f;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("rig", flags).GetValue(entry);
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", flags).GetValue(entry);
        var alongField = typeof(HumanLastRampart.Combat.Battle.BattleCameraRig).GetField("along", flags);
        var view = typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("view", flags).GetValue(entry);
        var world = (HumanLastRampart.Combat.Battle.BattleWorldView)view.GetType().GetField("world", flags).GetValue(view);
        Note($"{HumanLastRampart.Combat.Battle.BattleWorldView.Current.Name}: 조각 {world.PieceCount}개 · 날씨 {sim.CurrentWeather} {sim.TimeOfDay}");
        string tag = "draft" + draft;

        rig.ShowSide(0);
        for (int p = 0; p < 3; p++)
        {
            rig.SetPreset(p);
            for (int i = 0; i < 4; i++) yield return null;
            yield return Capture($"{tag}_front_{"ABC"[p]}");
        }

        // 대포 사격구 앞
        sim.Complete();
        for (int i = 0; i < sim.CannonCount; i++)
        {
            var c = sim.GetCannon(i);
            if (c.Side != 0) continue;
            alongField.SetValue(rig, c.Position.x);
            break;
        }

        rig.SetPreset(1);
        for (int i = 0; i < 4; i++) yield return null;
        yield return Capture($"{tag}_cannon_B");

        // 모서리 포탑 쪽 (북쪽 동쪽 끝 가까이)
        alongField.SetValue(rig, sim.CastleHalf - 4f);
        foreach (int p in new[] { 1, 2 })
        {
            rig.SetPreset(p);
            for (int i = 0; i < 4; i++) yield return null;
            yield return Capture($"{tag}_corner_{"ABC"[p]}");
        }

        // 전체 보기 (Tab)
        typeof(HumanLastRampart.Combat.Battle.BattleCameraRig).GetField("overview", flags).SetValue(rig, true);
        rig.SetPreset(1);
        for (int i = 0; i < 4; i++) yield return null;
        yield return Capture($"{tag}_overview");

        File.WriteAllText(Path.Combine(Out, $"world_{tag}.txt"), log.ToString(), new UTF8Encoding(false));
        HumanLastRampart.Combat.Battle.BattleWorldView.Draft = 1;
        Time.captureDeltaTime = 0f;
    }

    // D-062 1단계: 화면 효과 켜기/끄기 비교 (카메라 A·B, 서 있는 주인공)
    [UnityTest]
    public IEnumerator LookCompare()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("쉬움", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        for (int i = 0; i < 400; i++) yield return null;   // 적이 다가오게
        Time.captureDeltaTime = 0.0001f;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("rig", flags).GetValue(entry);
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", flags).GetValue(entry);
        rig.ShowSide(0);
        Note($"날씨 {sim.CurrentWeather} · {sim.TimeOfDay}");
        foreach (int preset in new[] { 0, 1 })
        {
            rig.SetPreset(preset);
            foreach (bool on in new[] { false, true })
            {
                HumanLastRampart.Combat.Render.BattleLook.Enabled = on;
                for (int i = 0; i < 4; i++) yield return null;
                yield return Capture($"look_{"AB"[preset]}_{(on ? "on" : "off")}");
            }
        }

        HumanLastRampart.Combat.Render.BattleLook.Enabled = true;
        File.WriteAllText(Path.Combine(Out, "look_compare.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // 미리 연결(사용자 검토 전): 성벽 난간·사격구·대포 3방향·달리는 병사
    [UnityTest]
    public IEnumerator ArtPreview()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("쉬움", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        for (int i = 0; i < 200; i++) yield return null;
        Time.captureDeltaTime = 0.0001f;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("rig", flags).GetValue(entry);
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", flags).GetValue(entry);
        var view = typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("view", flags).GetValue(entry);
        var props = (HumanLastRampart.Combat.Battle.BattlePropView)view.GetType().GetField("props", flags).GetValue(view);
        var alongField = typeof(HumanLastRampart.Combat.Battle.BattleCameraRig).GetField("along", flags);
        Note($"난간 판 {props.WallPieceCount}개, 대포 {sim.CannonCount}문");

        rig.ShowSide(0);
        for (int p = 0; p < 3; p++)
        {
            rig.SetPreset(p);
            for (int i = 0; i < 5; i++) yield return null;
            yield return Capture($"art_front_{"ABC"[p]}");
        }

        // 북쪽 첫 대포 앞으로
        sim.Complete();
        int cannon = -1;
        for (int i = 0; i < sim.CannonCount; i++) if (sim.GetCannon(i).Side == 0) { cannon = i; break; }
        Assert.GreaterOrEqual(cannon, 0, "북쪽 대포");
        var info = sim.GetCannon(cannon);
        alongField.SetValue(rig, info.Position.x);   // 북쪽: 오른쪽 = +x
        rig.SetPreset(1);
        string[] names = { "왼쪽", "가운데", "오른쪽" };
        var aims = new (string name, Vector3? point)[]
        {
            ("left", info.Position + new Vector3(-45f, 0f, 50f)),
            ("center", null),
            ("right", info.Position + new Vector3(45f, 0f, 50f)),
        };
        foreach (var (name, point) in aims)
        {
            sim.Complete();
            if (point.HasValue) sim.SetCannonAim(cannon, point.Value); else sim.ClearCannonAim(cannon);
            for (int i = 0; i < 5; i++) yield return null;
            int frame = props.CannonFrame(cannon);
            Note($"대포 조준 {name}: 그림 칸 {frame} ({(frame >= 0 && frame < 3 ? names[frame] : "?")})");
            yield return Capture($"art_cannon_{name}_B");
        }

        rig.SetPreset(0);
        for (int i = 0; i < 5; i++) yield return null;
        yield return Capture("art_cannon_center_A");

        // 전령 — 북쪽에서 동쪽으로 달림
        sim.Complete();
        alongField.SetValue(rig, 20f);
        rig.SetPreset(1);
        sim.IssueMessenger(0, 20f, 1, HumanLastRampart.Core.Campaign.TargetPriority.HighestHpRatio);
        Time.captureDeltaTime = 1f / 20f;
        for (int i = 0; i < 20; i++) yield return null;
        Time.captureDeltaTime = 0.0001f;
        sim.Complete();
        int running = sim.RunnerPictureCount();
        Note($"달리는 병사 그림 {running}명");
        if (sim.TryFirstRunner(out Vector3 runnerAt)) { alongField.SetValue(rig, runnerAt.x - 0.6f); Note($"달리는 병사 위치 ({runnerAt.x:0.0},{runnerAt.z:0.0})"); }
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("art_runner_B");
        rig.SetPreset(0);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("art_runner_A");
        File.WriteAllText(Path.Combine(Out, "art_preview.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // D-046: 병사 1줄·작게, 적은 성벽 가까이서 줄어듦, 카메라 A/B/C, Q/E 자동 달리기(옆 성벽 가운데까지 약 12초)
    [UnityTest]
    public IEnumerator WallScale()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("쉬움", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("rig", flags).GetValue(entry);
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", flags).GetValue(entry);

        // Q/E 자동 달리기: 북쪽 가운데 → 동쪽 가운데 (120m)
        rig.ShowSide(0);
        // 카메라 이동은 실제 시간(unscaledDeltaTime) 기준이라 실제 시계로 잰다
        Time.captureDeltaTime = 0.0001f;   // 재는 동안 전투 시간은 거의 멈춤
        var watch = System.Diagnostics.Stopwatch.StartNew();
        rig.RunToNextWall(1);
        while (rig.AutoRunning && watch.Elapsed.TotalSeconds < 30) yield return null;
        Time.captureDeltaTime = 1f / 20f;
        float seconds = (float)watch.Elapsed.TotalSeconds;
        Note($"E 자동 달리기: 북 가운데 → {rig.Side}면 along {rig.Along:0.0}, 실제 {seconds:0.0}초 (기대 12초)");
        Assert.AreEqual(1, rig.Side, "E = 오른쪽(동쪽) 성벽");
        Assert.AreEqual(2f * (sim.CastleHalf - HumanLastRampart.Combat.Battle.BattleCameraRig.HeroInset) / HumanLastRampart.Combat.Battle.BattleSim.HeroSpeed, seconds, 0.8f, "옆 성벽 가운데까지 (D-061 성벽 40m)");
        rig.ShowSide(0);
        rig.SetPreset(0);
        for (int i = 0; i < 5; i++) yield return null;
        yield return Capture("scale_0_start_A");

        yield return Click("×4", 300);
        int climbSide = -1;
        for (int i = 0; i < 8000 && climbSide < 0; i++)
        {
            yield return null;
            for (int s = 0; s < 4; s++) if (sim.Sides[s].AtWall + sim.Sides[s].Climbing + sim.Sides[s].OnWall >= 1) { climbSide = s; break; }
        }

        Note($"성벽 앞에 적이 붙은 면 {climbSide}, 시간 {sim.Elapsed:0.0}초");
        if (climbSide < 0)
        {
            File.WriteAllText(Path.Combine(Out, "wall_scale.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
            Assert.Ignore("D-061 성벽 40m: 전투 1에서 성벽까지 온 적이 없음 (병력이 촘촘해 다 막힘) — 크기 확인 건너뜀");
        }
        yield return Click("×1", 60);
        rig.ShowSide(climbSide);
        for (int p = 0; p < 3; p++)
        {
            rig.SetPreset(p);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"scale_{p + 1}_climb_{"ABC"[p]}");
        }

        rig.SetPreset(-1);
        File.WriteAllText(Path.Combine(Out, "wall_scale.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    void Note(string line) { log.AppendLine(line); Debug.Log("[PlayProbe] " + line); }

    static string HudText()
    {
        var t = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "Walls");
        return t != null ? t.text.Replace("\n", " | ") : "(HUD 없음)";
    }

    static bool InputOpen() => UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Any(e => e.isActiveAndEnabled);

    static Button FindReady(string label)
    {
        if (!InputOpen()) return null;
        foreach (Button b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if (!b.isActiveAndEnabled || !b.IsInteractable()) continue;
            Text t = b.GetComponentInChildren<Text>();
            if (t != null && t.text == label) return b;
        }
        return null;
    }

    static IEnumerator WaitReady(string label, int frames)
    {
        for (int i = 0; i < frames && FindReady(label) == null; i++) yield return null;
        Assert.IsNotNull(FindReady(label), $"'{label}' 버튼이 {frames}프레임 안에 준비되지 않음 (화면 {GameFlow.Instance?.CurrentScreen})");
    }

    static IEnumerator Click(string label, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            Button b = FindReady(label);
            if (b != null) { b.onClick.Invoke(); yield return null; yield break; }
            yield return null;
        }
        Assert.Fail($"'{label}' 버튼이 {frames}프레임 안에 준비되지 않음 (화면 {GameFlow.Instance?.CurrentScreen})");
    }

    static IEnumerator ClickPrefix(string prefix, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            foreach (Button b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (!b.isActiveAndEnabled || !b.IsInteractable()) continue;
                Text t = b.GetComponentInChildren<Text>();
                if (t != null && t.text.StartsWith(prefix)) { b.onClick.Invoke(); yield return null; yield break; }
            }
            yield return null;
        }
        Assert.Fail($"'{prefix}…' 버튼 없음");
    }

    // 명령 (CY2-C3): 메뉴를 실제 HUD 경로로 열고 고른다 (휠 버튼 대신 같은 함수 호출)
    [UnityTest]
    public IEnumerator CommandFlow()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("현실", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        yield return Click("×4", 300);
        for (int i = 0; i < 150; i++) yield return null;
        var entryType = typeof(HumanLastRampart.Combat.Battle.BattleEntry);
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var hud = (HumanLastRampart.Combat.Battle.BattleHud)entryType.GetField("hud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)entryType.GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
        var campaign = GameFlow.Instance.Campaign;

        // ① 명령하기: 북쪽 → 오르는 적부터
        hud.ToggleMenu(0);
        yield return null;
        yield return Capture("cmd_menu_root");
        hud.Choose(0);
        yield return null;
        yield return Capture("cmd_menu_words");
        hud.Choose(3);
        yield return null;
        Assert.AreEqual(HumanLastRampart.Core.Campaign.TargetPriority.ClimbingFirst, sim.PriorityOf(0), "명령하기 즉시 적용");
        Assert.AreEqual(HumanLastRampart.Core.Campaign.TargetPriority.ClimbingFirst, campaign.Targeting.PriorityFor("wall_north"), "진행 데이터 기록");
        Note("명령하기: 북쪽 오르는 적부터 — 즉시 적용·진행 데이터 기록 확인");

        // ② 전령: 서쪽으로 특수 적 먼저
        hud.ToggleMenu(0); yield return null; hud.Choose(1); yield return null; hud.Choose(4); yield return null;
        yield return Capture("cmd_menu_target");
        hud.Choose(0);   // 왼쪽 = 서
        yield return null;
        Assert.AreEqual(1, sim.MessengersRunning, "전령 출발");
        float issued = sim.Elapsed;
        for (int i = 0; i < 20; i++) yield return null;
        yield return Capture("cmd_messenger_running");

        // ③ 전령이 가는 동안 같은 서쪽에 소리 지르기(더 나중 명령) → 전령 도착 시 밀려야 함
        hud.ToggleMenu(0); yield return null; hud.Choose(2); yield return null; hud.Choose(5); yield return null; hud.Choose(0); yield return null;
        var westAfterShout = sim.PriorityOf(3);
        Note($"소리 지르기 서쪽 밀집한 곳: 서쪽 현재 {westAfterShout} (못 들었을 수도 있음)");
        int guard = 0;
        while (sim.MessengersRunning > 0 && guard++ < 4000) yield return null;
        Note($"전령 왕복 끝 ({sim.Elapsed - issued:0}초 뒤), 서쪽 최종 {sim.PriorityOf(3)}, 진행 데이터 {campaign.Targeting.PriorityFor("wall_west")}");
        if (westAfterShout == HumanLastRampart.Core.Campaign.TargetPriority.DensestArea)
            Assert.AreEqual(HumanLastRampart.Core.Campaign.TargetPriority.DensestArea, sim.PriorityOf(3), "늦게 도착한 옛 전령 명령은 무시 (C16-1)");
        else
            Assert.AreEqual(HumanLastRampart.Core.Campaign.TargetPriority.SpecialFirst, sim.PriorityOf(3), "외침을 못 들었으면 전령 명령 적용");
        yield return Capture("cmd_after");
        File.WriteAllText(Path.Combine(Out, "command.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // 날씨 (CY2-C4): 같은 전투에서 날씨·밤낮을 바꿔 찍고, 전투 중 소나기 변화를 확인
    [UnityTest]
    public IEnumerator WeatherShots()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("현실", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        yield return Click("×4", 300);
        for (int i = 0; i < 260; i++) yield return null;
        var entryType = typeof(HumanLastRampart.Combat.Battle.BattleEntry);
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)entryType.GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
        var setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
        Time.captureDeltaTime = 0.0005f;
        var combos = new[] {
            (HumanLastRampart.Core.CombatContracts.Weather.Clear, HumanLastRampart.Core.CombatContracts.TimeOfDay.Day),
            (HumanLastRampart.Core.CombatContracts.Weather.Cloudy, HumanLastRampart.Core.CombatContracts.TimeOfDay.Day),
            (HumanLastRampart.Core.CombatContracts.Weather.Rain, HumanLastRampart.Core.CombatContracts.TimeOfDay.Day),
            (HumanLastRampart.Core.CombatContracts.Weather.Fog, HumanLastRampart.Core.CombatContracts.TimeOfDay.Day),
            (HumanLastRampart.Core.CombatContracts.Weather.Clear, HumanLastRampart.Core.CombatContracts.TimeOfDay.Night),
            (HumanLastRampart.Core.CombatContracts.Weather.Rain, HumanLastRampart.Core.CombatContracts.TimeOfDay.Night),
        };
        foreach (var (w, t) in combos)
        {
            var f = setup.Forecast; f.InitialWeather = w; f.TimeOfDay = t; f.Changes = false; setup.Forecast = f;
            for (int i = 0; i < 3; i++) yield return null;
            sim.WeatherDirty = true;
            for (int i = 0; i < 3; i++) yield return null;
            Note($"{w}·{t}: 현재 {sim.CurrentWeather}, 시야 {sim.VisibleDistance:0}, 밝기 {sim.Brightness:0.00}, 낮은 시야 {sim.LowVisibility}, 비 {sim.IsRaining}");
            // 횃불: 켜진 수와 화면 안에 들어온 수 (화면 좌표 0~1)
            int torchOn = 0, torchSeen = 0;
            Camera view = Camera.allCameras.OrderBy(c => c.depth).FirstOrDefault();
            foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.name != "Torch" || !l.enabled) continue;
                torchOn++;
                if (torchOn <= 4) Note($"  횃불 위치 {l.transform.position.ToString("0.0")} 세기 {l.intensity:0.0} 범위 {l.range:0.0}");
                if (view == null) continue;
                Vector3 vp = view.WorldToViewportPoint(l.transform.position);
                if (vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f) { torchSeen++; Note($"  횃불 화면 {vp.x:0.00},{vp.y:0.00} 거리 {vp.z:0.0}"); }
            }
            Note($"  횃불 켜짐 {torchOn}, 화면 안 {torchSeen}, 사진기 {(view != null ? view.transform.position.ToString("0.0") : "없음")}");
            yield return Capture($"weather_{w}_{t}");
        }

        // 전투 중 변화: 맑음·낮 → 1초 뒤 소나기
        Time.captureDeltaTime = 1f / 20f;
        var g = setup.Forecast; g.InitialWeather = HumanLastRampart.Core.CombatContracts.Weather.Clear; g.TimeOfDay = HumanLastRampart.Core.CombatContracts.TimeOfDay.Day;
        g.Changes = true; g.ChangedWeather = HumanLastRampart.Core.CombatContracts.Weather.Rain; g.IsShower = true; g.ChangeAtSeconds = sim.Elapsed + 1f; g.Reverts = false; setup.Forecast = g;
        for (int i = 0; i < 20; i++) yield return null;
        Assert.AreEqual(HumanLastRampart.Core.CombatContracts.Weather.Rain, sim.CurrentWeather, "예보 시각에 소나기로 바뀜");
        Note($"전투 중 변화: {sim.CurrentWeather}, 불 피해 배율 {sim.FireDamageMultiplier} (소나기 0.5 기대)");
        Assert.AreEqual(0.5f, sim.FireDamageMultiplier, 0.001f, "소나기 불 피해 50%");
        yield return Capture("weather_shower_change");
        File.WriteAllText(Path.Combine(Out, "weather.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // 2번 지도 (MAP-1): 3단계 사진, 쉬움 시간 정지, 현실 시간 흐름
    [UnityTest]
    public IEnumerator MapEasy() { yield return MapCheck(Difficulty.Easy, "map_easy"); }

    [UnityTest]
    public IEnumerator MapRealistic() { yield return MapCheck(Difficulty.Realistic, "map_real"); }

    IEnumerator MapCheck(Difficulty difficulty, string tag)
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click(difficulty == Difficulty.Easy ? "쉬움" : "현실", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        yield return Click("×4", 300);
        for (int i = 0; i < 260; i++) yield return null;
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
        yield return Capture($"{tag}_0_battle");
        entry.ToggleMap();
        for (int i = 0; i < 3; i++) yield return null;
        float t0 = sim.Elapsed;
        yield return Capture($"{tag}_1_side");
        entry.MapCamera.SetLevel(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Full);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture($"{tag}_2_full");
        entry.MapCamera.SetLevel(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Close);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture($"{tag}_3_close");
        // MAP-2 전체 명령: 4면 모두 바뀌고 진행 데이터는 SetGlobal(성벽 명령 지움). 쉬움 정지 중에도 바로 적용 (M8)
        yield return Click("오르는 적부터", 60);
        for (int i = 0; i < 2; i++) yield return null;
        for (int side = 0; side < 4; side++) Assert.AreEqual(HumanLastRampart.Core.Campaign.TargetPriority.ClimbingFirst, sim.PriorityOf(side), "전체 명령 4면 적용");
        var targeting = GameFlow.Instance.Campaign.Targeting;
        Assert.AreEqual(HumanLastRampart.Core.Campaign.TargetPriority.ClimbingFirst, targeting.GlobalPriority, "진행 데이터 전체 명령");
        Assert.AreEqual(0, targeting.WallRules.Count, "성벽 명령 지워짐");
        Note($"전체 명령 [오르는 적부터] 4면 적용, 진행 데이터 기록 (시간 {sim.Elapsed:0.0})");
        yield return Capture($"{tag}_5_global");
        for (int i = 0; i < 40; i++) yield return null;
        float t1 = sim.Elapsed;
        Note($"지도 연 동안 경과 {t0:0.0} → {t1:0.0}");
        if (difficulty == Difficulty.Easy) Assert.AreEqual(t0, t1, 0.001f, "쉬움: 지도 연 동안 시간 정지");
        else Assert.Greater(t1, t0 + 1f, "현실: 지도 연 동안 시간 흐름");
        entry.ToggleMap();
        for (int i = 0; i < 20; i++) yield return null;
        Assert.Greater(sim.Elapsed, t1, "지도를 닫으면 다시 흐름");
        Assert.IsFalse(Camera.allCameras.Any(c => c.orthographic), "닫으면 원근 카메라로 돌아옴");
        yield return Capture($"{tag}_4_closed");
        File.WriteAllText(Path.Combine(Out, $"{tag}.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // 대포 조준 (MAP-3): 북쪽 대포 1~3은 적이 오는 길, 4는 빈 성 안마당 → 4번은 '대기'
    [UnityTest]
    public IEnumerator CannonAim()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("현실", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        yield return Click("×4", 300);
        for (int i = 0; i < 240; i++) yield return null;
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
        entry.ToggleMap();
        entry.SelectCannonSide(0);
        yield return null;
        Assert.AreEqual(16, sim.CannonCount, "대포 4면 × 4문");
        float[] xs = { -12f, -3f, 8f };   // D-061 성벽 40m
        for (int n = 1; n <= 3; n++)
        {
            entry.SelectCannon(n);
            Assert.IsTrue(entry.AimSelectedCannonAt(new Vector3(xs[n - 1], 0f, 80f)), $"대포 {n} 조준 (사거리 안)");
        }
        entry.SelectCannon(4);
        Assert.IsFalse(entry.AimSelectedCannonAt(new Vector3(0f, 0f, 250f)), "사거리 밖은 거부");
        Assert.IsTrue(entry.AimSelectedCannonAt(new Vector3(30f, 0f, 20f)), "성 안마당 조준");
        for (int i = 0; i < 60; i++) yield return null;
        yield return Capture("cannon_aim_side");
        var labels = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text).Where(t => t.Contains("번:")).ToList();
        Note("대포 표시: " + string.Join(" | ", labels));
        Assert.IsTrue(labels.Any(t => t.Contains("4번: 조준 — 대기")), "적 없는 곳을 노린 대포는 대기");
        Assert.IsTrue(labels.Any(t => t.Contains("포격 중")), "적이 오는 길을 노린 대포는 포격");
        entry.MapCamera.SetLevel(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Full);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("cannon_aim_full");
        File.WriteAllText(Path.Combine(Out, "cannon.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // 마법 (MAP-4): 마법사 참가, 폭발 1초 뒤·피해, 불바다, 재사용 대기, 150m 제한
    [UnityTest]
    public IEnumerator SpellCast()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("현실", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        // M7 참가 목록 변경은 Codex 결과 반영기 갱신 뒤 (C-005 v1.3 적용 대기)
        yield return Click("×4", 300);
        for (int i = 0; i < 240; i++) yield return null;
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
        Assert.IsTrue(sim.WizardPresent, "마법사 있음");
        entry.ToggleMap();
        yield return null;
        Time.captureDeltaTime = 1f / 60f;
        entry.SelectSpell(0);
        yield return null;
        Assert.IsFalse(entry.CastSelectedSpellAt(new Vector3(0f, 0f, 60f + 160f)), "150m 밖 거부");
        Assert.IsTrue(entry.CastSelectedSpellAt(new Vector3(0f, 0f, 110f)), "폭발 쓰기");
        for (int i = 0; i < 3; i++) yield return null;
        Assert.IsTrue(sim.Effects.Any(e => e.Pending), "1초 뒤 터질 폭발 대기");
        yield return Capture("spell_explosion_pending");
        entry.SelectSpell(0);
        Assert.IsFalse(entry.CastSelectedSpellAt(new Vector3(10f, 0f, 110f)), "재사용 대기 중 거부");
        for (int i = 0; i < 20; i++) yield return null;   // 4배속 × 1/60초 × 20 ≈ 1.3초
        var notes = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Where(t => t.name == "Notices").Select(t => t.text).FirstOrDefault() ?? "";
        Note("알림: " + notes.Replace("\n", " | "));
        Assert.IsTrue(notes.Contains("폭발!"), "폭발이 터짐");
        entry.SelectSpell(1);
        Assert.IsTrue(entry.CastSelectedSpellAt(new Vector3(-20f, 0f, 100f)), "불바다 쓰기");
        for (int i = 0; i < 10; i++) yield return null;
        Assert.IsTrue(sim.Effects.Any(e => e.Fire), "불바다 진행");
        yield return Capture("spell_fire_map");
        entry.ToggleMap();
        for (int i = 0; i < 6; i++) yield return null;
        yield return Capture("spell_fire_battle");
        File.WriteAllText(Path.Combine(Out, "spell.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // 망원경 (MAP-5): 안개·밤에 북쪽 무리를 렌즈로 확인 → 무리 표시·시야 밖 보임
    [UnityTest]
    public IEnumerator Telescope()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("현실", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("다음", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        var setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
        var f = setup.Forecast; f.InitialWeather = HumanLastRampart.Core.CombatContracts.Weather.Fog; f.TimeOfDay = HumanLastRampart.Core.CombatContracts.TimeOfDay.Night; f.Changes = false; setup.Forecast = f;
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
        yield return Click("×4", 300);
        for (int i = 0; i < 200; i++) yield return null;
        sim.WeatherDirty = true;
        entry.ToggleMap();
        entry.MapCamera.SetLevel(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Full);
        Time.captureDeltaTime = 1f / 120f;
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("telescope_before");
        Assert.AreEqual(0, sim.SpottedGroupCount, "처음엔 확인한 무리 없음");
        for (int i = 0; i < 6; i++) { entry.UseTelescope(new Vector3(0f, 0f, 140f)); yield return null; }
        for (int i = 0; i < 12; i++) { entry.UseTelescope(new Vector3(0f, 0f, 140f)); yield return null; }
        Note($"확인한 무리 {sim.SpottedGroupCount}");
        Assert.Greater(sim.SpottedGroupCount, 0, "렌즈로 북쪽 무리 확인");
        System.Collections.Generic.List<string> labels = null;
        var started = System.Diagnostics.Stopwatch.StartNew();
        while (started.ElapsedMilliseconds < 3000)   // 무리 표시는 실제 시간 0.25초마다 갱신
        {
            labels = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Where(t => t.name == "Group Label" && t.gameObject.activeInHierarchy).Select(t => t.text).ToList();
            if (labels.Count > 0) break;
            yield return null;
        }
        Note("무리 표시: " + string.Join(" | ", labels));
        Assert.IsTrue(labels.Any(t => t.StartsWith("북쪽 약")), "지도에 무리 표시");
        yield return Capture("telescope_after");
        entry.ToggleMap();
        for (int i = 0; i < 4; i++) yield return null;
        Assert.IsFalse(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => t.name == "Group Label" && t.gameObject.activeInHierarchy), "지도 닫으면 표시 숨김");
        yield return Capture("telescope_battle_view");
        File.WriteAllText(Path.Combine(Out, "telescope.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    static IEnumerator WaitScreen(ScreenId screen, int frames)
    {
        for (int i = 0; i < frames && (GameFlow.Instance == null || GameFlow.Instance.CurrentScreen != screen || !InputOpen()); i++) yield return null;
        Assert.AreEqual(screen, GameFlow.Instance.CurrentScreen, "화면 전환");
        for (int i = 0; i < 5; i++) yield return null;
    }

    // 배치 모드에는 화면 그리기 단계(WaitForEndOfFrame)가 없다. 전투의 LateUpdate(인스턴싱 그리기 예약) 뒤에
    // 도는 찍개 컴포넌트가 같은 프레임 안에서 카메라를 직접 그려 찍는다. 화면 덮개 UI는 잠시 카메라 공간으로 바꾼다.
    IEnumerator Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
        var go = new GameObject("PlayProbe Shooter");
        var shooter = go.AddComponent<ProbeShooter>();
        shooter.Path = Path.Combine(Out, $"{++shot:00}_{name}.png");
        for (int i = 0; i < 10 && !shooter.Done; i++) yield return null;
        UnityEngine.Object.Destroy(go);
    }
}

[DefaultExecutionOrder(int.MaxValue)]
public sealed class ProbeShooter : MonoBehaviour
{
    public string Path;
    public bool Done;

    const int ProbeLayer = 31;   // 사진 찍는 동안만 표시판을 옮겨 두는 층 (게임에서 안 쓴다)

    static Color[] RenderUi(Camera ui, RenderTexture rt, Texture2D tex, Color background)
    {
        ui.backgroundColor = background;
        ui.targetTexture = rt;
        ui.Render();
        ui.targetTexture = null;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        RenderTexture.active = null;
        return tex.GetPixels();
    }

    void LateUpdate()
    {
        if (Done || Path == null) return;
        Done = true;
        const int w = 1600, h = 900;
        var rt = new RenderTexture(w, h, 24);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var cams = Camera.allCameras.OrderBy(c => c.depth).ToArray();
        var changed = new System.Collections.Generic.List<Canvas>();
        var layers = new System.Collections.Generic.List<(GameObject go, int layer)>();
        GameObject uiCamObject = null;
        try
        {
            // 1) 장면만 찍는다 (화면 위 표시판은 사진기에 안 그려진다)
            foreach (Camera cam in cams) { var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old; }
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = null;
            Color[] scene = tex.GetPixels();

            // 2) 표시판은 화면 효과·안개 없는 전용 사진기로 검은 바탕·흰 바탕에 한 번씩 찍어 덧그린다
            //    (예전에는 표시판을 장면 사진기 앞에 붙여 찍어서, 사진에서만 글씨가 흐림·번짐·어둡게 효과를 받았다)
            uiCamObject = new GameObject("PlayProbe UI Camera");
            var ui = uiCamObject.AddComponent<Camera>();
            ui.enabled = false;
            ui.cullingMask = 1 << ProbeLayer;
            ui.clearFlags = CameraClearFlags.SolidColor;
            ui.orthographic = true;
            ui.nearClipPlane = 0.1f; ui.farClipPlane = 10f;
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay || !canvas.isActiveAndEnabled) continue;
                foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true)) { layers.Add((t.gameObject, t.gameObject.layer)); t.gameObject.layer = ProbeLayer; }
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = ui;
                canvas.planeDistance = 1f;
                changed.Add(canvas);
            }
            if (changed.Count > 0)
            {
                Canvas.ForceUpdateCanvases();
                Color[] black = RenderUi(ui, rt, tex, Color.black);
                Color[] onWhite = RenderUi(ui, rt, tex, Color.white);
                for (int i = 0; i < scene.Length; i++)
                {
                    Color b = black[i], k = onWhite[i], s = scene[i];
                    // 흰 바탕과 검은 바탕의 차이 = 표시판이 비치는 정도
                    scene[i] = new Color(b.r + s.r * (k.r - b.r), b.g + s.g * (k.g - b.g), b.b + s.b * (k.b - b.b), 1f);
                }
            }
            tex.SetPixels(scene);
            tex.Apply();
            File.WriteAllBytes(Path, tex.EncodeToPNG());
        }
        catch (Exception e) { Debug.LogWarning("[PlayProbe] 캡처 실패: " + e.Message); }
        finally
        {
            foreach (Canvas canvas in changed) { if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; } }
            foreach (var (go, layer) in layers) if (go != null) go.layer = layer;
            if (uiCamObject != null) Destroy(uiCamObject);
            Destroy(rt);
            Destroy(tex);
        }
    }
}
