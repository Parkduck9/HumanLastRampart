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
public sealed partial class PlayProbe   // 한 바퀴 통과 시험은 PlayProbe.FullLoop.cs (K-053)
{
    static readonly string Out = Environment.GetEnvironmentVariable("HLR_PLAY_OUT") ?? "playprobe";
    StringBuilder log;
    int shot;

    [UnityTest]
    public IEnumerator EasyFullRun() { yield return Run(Difficulty.Easy, "easy"); }

    [UnityTest]
    public IEnumerator RealisticRun() { yield return Run(Difficulty.Realistic, "realistic"); }

    // K-050 스토리 화면: 대사 상자·이름표·인물 사진, 누르면 나오던 글이 끝까지 → 한 번 더 누르면 다음 줄, 마지막 줄 다음 전투
    [UnityTest]
    public IEnumerator StoryFlow()
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
        Button advance = null;
        for (int i = 0; i < 300 && advance == null; i++)
        {
            advance = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "Story Advance" && b.isActiveAndEnabled);
            if (advance == null) yield return null;
        }

        Assert.IsNotNull(advance, "스토리 누르기 판");
        Assert.IsNotNull(FindReady("넘기기"), "[넘기기] 단추");
        Assert.IsNotNull(FindReady("자동"), "[자동] 단추");

        int lines = 0, presses = 0;
        bool sawSpeaker = false;
        string last = null;
        while (GameFlow.Instance.CurrentScreen == ScreenId.Story && presses < 60)
        {
            // 암전이 열리고 글이 나오기 시작할 때까지
            for (int i = 0; i < 400 && StoryText().Length < 2 && GameFlow.Instance.CurrentScreen == ScreenId.Story; i++) yield return null;
            if (GameFlow.Instance.CurrentScreen != ScreenId.Story) break;
            if (lines == 0) yield return Capture("story_0_typing");
            string partial = StoryText();
            advance = FindAdvance() ?? advance;   // W12: 이야기가 이어지면 화면이 다시 열려 단추가 새로 생긴다
            advance.onClick.Invoke();   // 나오는 중이면 끝까지
            presses++;
            yield return null;
            string full = StoryText();
            for (int i = 0; i < 4; i++) yield return null;
            // W12: "……"처럼 짧은 줄은 이미 다 나온 상태라 누르면 다음 줄로 넘어간다 (글이 비었다가 새로 나온다) — 그때는 확인하지 않는다
            bool nextLineStarted = full.Length < partial.Length || !full.StartsWith(partial) || full.Length < 2;
            if (!nextLineStarted)
            {
                Assert.AreEqual(full, StoryText(), "누른 뒤에는 글이 더 늘지 않는다 (끝까지 보임)");
                Assert.IsTrue(full.StartsWith(partial), "한 자씩 나오던 글의 앞부분");
            }
            if (full != last) { lines++; last = full; Note($"스토리 {lines}줄: {SpeakerText()} — {full}"); }
            if (!sawSpeaker && SpeakerText().Length > 0) { sawSpeaker = true; yield return Capture("story_1_speaker"); }
            advance = FindAdvance() ?? advance;
            advance.onClick.Invoke();   // 다음 줄 (마지막 줄이면 끝 → 다음 이야기 또는 전투)
            presses++;
            yield return null;
        }

        Note($"스토리: {lines}줄, 누른 횟수 {presses}");
        Assert.GreaterOrEqual(lines, 3, "임시 첫 이야기는 3줄 이상");
        Assert.IsTrue(sawSpeaker, "이름표가 보인 줄이 있다");
        yield return WaitScreen(ScreenId.Battle, 900);
        File.WriteAllText(Path.Combine(Out, "story_flow.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    static Button FindAdvance() => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "Story Advance" && b.isActiveAndEnabled);
    static string StoryText() => UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Story Text")?.text ?? "";
    static string SpeakerText() => UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Story Speaker" && t.gameObject.activeInHierarchy)?.text ?? "";

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
        yield return Click("넘기기", 600);

        for (int round = 0; round < 6; round++)
        {
            yield return WaitScreen(ScreenId.Battle, 900);
            CampaignState c = GameFlow.Instance.Campaign;
            int gold0 = c.Gold;
            int number = c.BattleNumber;
            int lossesBefore = c.ConsecutiveNightLosses;   // W8: 진 밤이 연속 두 번째면 패배 엔딩
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

            if (r.Outcome == BattleOutcome.Defeat && lossesBefore == 0)
            {
                // W8: 첫 패배는 엔딩이 아니라 같은 요새의 다음 낮 (등대 복구 절반). 정비를 거쳐 같은 밤을 다시 치른다
                yield return WaitScreen(ScreenId.Result, 900);   // W11: 첫 패배도 결과 화면(잃은 병력 보고)을 거친다
                yield return WaitReady("다음", 300);
                Assert.AreEqual(r.BattleRunId, c.LastAppliedBattleRunId, "진 밤의 결과 반영");
                yield return Click("다음", 60);
                yield return WaitScreen(ScreenId.Prep, 900);
                Note($"첫 패배 → 낮 (복구 {c.BeaconRepair}, 연속 패배 {c.ConsecutiveNightLosses})");
                Assert.AreEqual(1, c.ConsecutiveNightLosses, "연속 패배 +1");
                yield return Click("전투 준비 완료", 300);
                yield return null;
                yield return null;
                continue;
            }

            if (r.Outcome == BattleOutcome.Defeat)
            {
                yield return WaitScreen(ScreenId.Result, 900);   // W11: 연속 두 번째 패배도 결과 화면 뒤에 엔딩
                yield return WaitReady("다음", 300);
                yield return Click("다음", 60);
                yield return WaitScreen(ScreenId.Story, 900);
                Assert.AreEqual(StoryKind.DefeatEnding, GameFlow.Instance.Campaign.NextStory.Kind, "패배 뒤 패배 엔딩");
                Note("패배 엔딩 화면 (결과 화면 건너뜀)");
                yield return Click("넘기기", 600);   // K-050 스토리 화면: 패배 엔딩도 [넘기기]
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
            float recoverRatio = c.ActiveBattleSetup.Boosts != null ? Mathf.Clamp01(c.ActiveBattleSetup.Boosts.RecoverLostRatio) : 0f;   // 군의관 천막: 잃은 병사 일부가 돌아온다
            int recovered = r.SquadLosses.Sum(l => Mathf.FloorToInt(l.LostCount * recoverRatio + 0.5f));
            Assert.AreEqual(soldiers0 - lost + recovered - c.LastNightGarrison, c.Squads.Sum(s => s.Count), "잃은 병사 반영 (돌아온 병사 포함, 점화한 밤은 수비대로 남긴 인원도 빠진다)");
            // 요새를 점화하고 다음 요새로 넘어가면 성벽이 가득 채워진다 (BeaconRules.Relight) — 마지막 요새는 그대로
            bool wallsRestored = c.LastNightRelit && !(c.NextStory != null && c.NextStory.Kind == StoryKind.RunComplete);
            foreach (WallOutcome w in r.WallOutcomes)
                if (!wallsRestored) Assert.AreEqual(w.HpAfter, c.Wall.First(x => x.SegmentId == w.SegmentId).Hp, "성벽 피해 반영");
            Note($"결과 반영 확인: 골드 {gold0}→{c.Gold}, 병사 {soldiers0}→{c.Squads.Sum(s => s.Count)}, 성벽 {string.Join("/", c.Wall.Select(w => w.Hp))}, 다음 번호 {c.BattleNumber}");
            yield return Capture($"{tag}_result{number}");
            // D-076 결과 그림판: 성벽·병력·영웅 세 칸이 한 화면에 있다 (예전 상세 버튼·팝업 없음)
            foreach (string column in new[] { "Walls", "Squads", "Heroes" })
                Assert.IsNotNull(GameObject.Find(column), $"결과 그림판 {column} 칸");
            Note($"결과 그림판 세 칸 확인 (전투 {number})");
            yield return Click("다음", 300);
            if (c.LastNightRelit)
            {
                // W10: 요새를 점화한 밤 뒤에는 봉화선 지도가 먼저 (마지막 요새도)
                yield return WaitScreen(ScreenId.BeaconMap, 900);
                yield return Click("계속", 300);
            }

            if (c.NextStory != null && c.NextStory.Kind == StoryKind.RunComplete)
            {
                // W8: 마지막 요새 점화 → 1회차 완료 이야기 → 메인
                yield return WaitScreen(ScreenId.Story, 900);
                yield return Click("넘기기", 600);
                yield return WaitScreen(ScreenId.MainMenu, 900);
                Note("1회차 완료 이야기 뒤 메인 복귀");
                break;
            }
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
        yield return Click("넘기기", 600);
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

        // 자동 폭발(1%)은 그대로 둔다. 전투 밖 대조 함정으로 보존 검사가 빈 검사가 되지 않게 한다.
        string controlTrapId = c.NewInstanceId("trap");
        c.Traps.Add(new TrapPlacement { TrapId = controlTrapId, TrapDefId = "trap_bomb", TerrainId = "terrain_2",
            SlotId = "cell_0_2_12", Marked = true });
        var originalTraps = c.Traps.Select(t => (t.TrapId, t.SlotId, t.TerrainId)).ToArray();
        yield return Click("넘기기", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", flags).GetValue(entry);
        var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("rig", flags).GetValue(entry);
        Assert.AreEqual(3, sim.TrapCount, "이 지형 함정 3개, 다른 지형 대조 함정은 전투에서 제외");
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
        yield return VerifyTrapVisualCleanup(entry, sim, 1);
        entry.ToggleMap();
        for (int i = 0; i < 3; i++) yield return null;
        entry.MapCamera.SetLevel(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Close);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("trap_2_map_exploded");
        entry.ToggleMap();

        // 연동 (Codex T-CX-035 결과 반영 C-007 v1.1): 전투를 끝까지 이기고 결과 화면에서 터진 함정만 사라지는지
        string exploded = sim.GetTrap(1).TrapId;
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
            Assert.Contains(exploded, result.ConsumedTrapIds, "수동 점화한 함정이 실제 폭발 목록에 포함");
            foreach (var original in originalTraps)
            {
                if (result.ConsumedTrapIds.Contains(original.TrapId))
                {
                    Assert.IsFalse(c.Traps.Any(t => t.TrapId == original.TrapId), "수동·자동 폭발 함정 모두 제거");
                    continue;
                }
                TrapPlacement t = c.Traps.FirstOrDefault(x => x.TrapId == original.TrapId);
                Assert.IsNotNull(t, "안 터진 함정은 남는다");
                Assert.IsTrue(t.Marked && t.SlotId == original.SlotId && t.TerrainId == original.TerrainId,
                    "안 터진 함정의 표시·처음 칸·지형은 그대로");
            }
            Assert.IsTrue(c.Traps.Any(t => t.TrapId == controlTrapId), "전투 밖 대조 함정이 반드시 남아 보존 검사를 수행");
            Assert.AreEqual(originalTraps.Length - result.ConsumedTrapIds.Count(id => originalTraps.Any(t => t.TrapId == id)),
                c.Traps.Count, "실제 폭발한 함정 수만큼만 줄어든다");

            // 결과 화면에 몇 프레임 머물러도(재진입 역할) 다시 반영되지 않는다 (D-076 그림판 — 상세 팝업 없음)
            int gold = c.Gold, traps = c.Traps.Count;
            for (int i = 0; i < 10; i++) yield return null;
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
        yield return Click("넘기기", 600);
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
        var session = new HumanLastRampart.Meta.TrapPreparationTransactions(c, GameFlow.Instance.Definitions);
        Vector2 point = FreeTrapTestSupport.Point(session, "trap_bomb");
        string cellId = HumanLastRampart.Core.Traps.TrapGrid.FreeId(point.x, point.y);
        var placer = UnityEngine.Object.FindAnyObjectByType<HumanLastRampart.Meta.FreePlacementInput>();
        Assert.IsNotNull(placer, "함정 자유 배치 입력");
        placer.MoveTo(FreeTrapTestSupport.MapPoint(session, point));
        var ghost = placer.transform.Find("Ghost").GetComponent<Image>();
        var ring = placer.transform.Find("Trap Blast Radius").GetComponent<Image>();
        Assert.IsTrue(ghost.gameObject.activeSelf && ghost.color.g > ghost.color.r, "가능한 땅은 초록 고스트");
        Assert.IsTrue(ring.gameObject.activeSelf);
        float pixelsPerMeter = FreeTrapTestSupport.MapPoint(session, Vector2.right).x;
        Assert.AreEqual(session.Radius("trap_bomb") * 2f * pixelsPerMeter, ring.rectTransform.sizeDelta.x, .01f, "실제 폭발 반경 원");
        yield return Capture("preptrap_0a_free_valid");
        placer.MoveTo(Vector2.zero);
        Assert.IsTrue(ghost.color.r > ghost.color.g, "성 안은 빨강 고스트");
        yield return Capture("preptrap_0b_free_invalid");
        placer.MoveTo(FreeTrapTestSupport.MapPoint(session, point));
        placer.PlaceAt(FreeTrapTestSupport.MapPoint(session, point));
        yield return null;
        Assert.IsTrue(FreeTrapTestSupport.BuyButton().interactable, "자유 위치 선택");
        placer.PlaceAt(FreeTrapTestSupport.MapPoint(session, point));
        yield return null;
        Assert.IsFalse(FreeTrapTestSupport.BuyButton().interactable, "같은 위치를 다시 누르면 선택 해제");
        placer.PlaceAt(FreeTrapTestSupport.MapPoint(session, point));
        yield return null;
        // K-042: 칸을 여러 개 고른 뒤 한 번에 구매 (견적·확정 두 단계 → 단추 하나, 글자 뒤에 칸 수·합계가 붙는다)
        yield return ClickPrefix("한 번에 구매·설치", 60);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("preptrap_1_installed");
        TrapPlacement placed = c.Traps.FirstOrDefault(t => t.SlotId.StartsWith("free:") && Vector2.Distance(new Vector2(t.ActualX, t.ActualZ), point) < .001f);
        Assert.IsNotNull(placed, "진행 데이터에 함정");
        Assert.AreEqual(gold0 - 25, c.Gold, "25골드");
        Assert.AreEqual(11, c.TrapShop.First(s => s.TrapDefId == "trap_bomb").Count, "상점 재고 −1");
        Assert.IsTrue(placed.Marked, "지도 표시 기본 켜짐");
        placer.PlaceAt(FreeTrapTestSupport.MapPoint(session, point));
        yield return null;
        Assert.IsFalse(FreeTrapTestSupport.BuyButton().interactable, "설치된 위치는 다시 못 고른다");
        Note($"설치: {placed.TrapId} {placed.TerrainId} {cellId} ({placed.ActualX:0.0},{placed.ActualZ:0.0}) 골드 {gold0}→{c.Gold}");
        yield return Click("닫기", 60);
        // W18: 2번째 밤이 점화의 밤이면 결과 뒤 길(지도·이야기·새 요새)과 함정 정리가 달라진다 — 이 시험은 함정 보충을 보므로 복구를 0으로 두어 늘 평범한 밤
        c.BeaconRepair = 0f;
        yield return Click("전투 준비 완료", 300);
        yield return WaitScreen(ScreenId.Battle, 900);
        BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
        Assert.IsFalse(setup.IsIgnitionNight, "2번째 밤은 점화의 밤이 아니다");
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
        Assert.AreEqual(BattleOutcome.Victory, result.Outcome, "전투 2 승리");
        {
            yield return WaitScreen(ScreenId.Result, 900);
            yield return WaitReady("다음", 300);
            bool exploded = result.ConsumedTrapIds.Contains(placed.TrapId);
            Assert.AreEqual(!exploded, c.Traps.Any(t => t.TrapId == placed.TrapId), "터졌으면 사라지고, 불발이면 남는다");
            Note($"결과 반영: 함정 {(exploded ? "사라짐" : "남음(불발)")} · 남은 함정 {c.Traps.Count}");
            yield return Click("다음", 300);
            Assert.IsFalse(c.LastNightRelit, "평범한 밤이라 지도 없이 정비로");
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
        yield return Click("넘기기", 600);
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

        // 모집: 북쪽 성벽 분대 1 지점, 기존 궁수 분대에 11명 보충 → 300명 기준 5골드 × 11
        // K-047: 지점은 칸을 바로 누르고, 값은 고를 때마다 바로 보인다 (견적 단추 없음)
        yield return Click("병력 모집·보충", 300);
        yield return Click("지점:post_north_squad_1", 60);
        yield return Click("인원 +10", 60);
        yield return Capture("prep_1_recruit_quote");
        int squad0 = c.Squads.First(s => s.PostId == "post_north_squad_1").Count;
        yield return Click("확정 모집", 60);
        Assert.AreEqual(gold0 - 55, c.Gold, "궁수 11명 = 55골드");
        // 정비는 목록을 한 번에 바꿔 끼운다(원자적 반영) — 반영 뒤 다시 찾는다
        Assert.AreEqual(squad0 + 11, c.Squads.First(s => s.PostId == "post_north_squad_1").Count, "분대에 11명");
        Assert.AreEqual(total0 + 11, c.Squads.Sum(s => s.Count));
        for (int i = 0; i < 3; i++) yield return null;
        // 사용자 2026-10-20 #15: 확정 뒤에도 단추가 잠기지 않고, 인원은 1명으로 돌아가 새 견적으로 바로 또 모집할 수 있다
        Assert.IsNotNull(FindReady("확정 모집"), "모집 뒤에도 바로 또 모집할 수 있다");
        Assert.AreEqual(gold0 - 55, c.Gold, "다시 누르기 전에는 골드가 그대로");
        yield return Capture("prep_2_recruit_done");
        yield return Click("닫기", 60);

        // 수리: 55HP 손상, 100HP 요청 → 실제 55HP, 6골드(올림)
        int gold1 = c.Gold;
        yield return Click("성벽 수리", 300);
        yield return Click("수리:100", 60);
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
        yield return Click("넘기기", 600);
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
        yield return Click("넘기기", 600);
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
        yield return Click("넘기기", 600);
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
        yield return Click("넘기기", 600);
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
        yield return Click("넘기기", 600);
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

    // K-060: 설치 대포 크기 (보이는 폭 = 병사 키 × 1.15, 납작함 보정) — 뒤(A·B)와 들판 쪽 정면 사진, 대포 묶음 이동 중 사진
    [UnityTest]
    public IEnumerator CannonArt()
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
        yield return Click("넘기기", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        for (int i = 0; i < 60; i++) yield return null;
        Time.captureDeltaTime = 0.0001f;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("rig", flags).GetValue(entry);
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", flags).GetValue(entry);
        var alongField = typeof(HumanLastRampart.Combat.Battle.BattleCameraRig).GetField("along", flags);
        Note($"설치 대포 칸 높이 {HumanLastRampart.Combat.Battle.BattlePropView.InstalledCannonSize:0.000}m · 보이는 폭 {HumanLastRampart.Combat.Battle.BattlePropView.InstalledCannonVisibleWidth:0.000}m (병사 키 0.35m × 1.15 = 0.403m)");

        sim.Complete();
        int cannon = -1;
        for (int i = 0; i < sim.CannonCount; i++) if (sim.GetCannon(i).Side == 0) { cannon = i; break; }
        Assert.GreaterOrEqual(cannon, 0, "북쪽 대포");
        var info = sim.GetCannon(cannon);
        rig.ShowSide(0);
        alongField.SetValue(rig, info.Position.x);   // 북쪽: 오른쪽 = +x
        for (int p = 0; p < 2; p++)
        {
            rig.SetPreset(p);
            for (int i = 0; i < 5; i++) yield return null;
            yield return Capture($"cannon_installed_back_{"AB"[p]}");
        }

        // 정면: 들판 쪽에서 성벽을 바라보는 임시 사진기 (전투 사진기 위에 그린다)
        var main = (Camera)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("battleCamera", flags).GetValue(entry);
        var front = new GameObject("PlayProbe Front Camera").AddComponent<Camera>();
        front.CopyFrom(main);
        front.depth = main.depth + 10f;
        Vector3 at = info.Position + new Vector3(0f, 0.2f, 0f);
        front.transform.position = at + new Vector3(0f, 0.35f, 2.2f);   // 북쪽 바깥 2.2m, 조금 위
        front.transform.LookAt(at);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("cannon_installed_front");
        front.enabled = false;

        // 대포 옮기기: 북쪽 대포를 동쪽 빈 사격구로 끌고 간다 (K-060)
        sim.Complete();
        Note($"동쪽 빈 사격구 {sim.FreeCannonBays(1)}곳");
        Assert.IsTrue(sim.MoveCannon(cannon, 1, false), "대포 옮기기 출발");
        Time.captureDeltaTime = 1f / 20f;
        for (int i = 0; i < 40; i++) yield return null;
        Time.captureDeltaTime = 0.0001f;
        sim.Complete();
        Assert.Greater(sim.CannonHauls.Count, 0, "끄는 대포");
        Vector3 haul = sim.CannonHauls[0].Position;
        Note($"끄는 대포 위치 ({haul.x:0.0},{haul.z:0.0}) · 끈 거리 {sim.CannonHauls[0].Travelled:0.0}m · 속도 {HumanLastRampart.Combat.Battle.BattleSim.HaulSpeed:0.00}m/초");
        rig.ShowSide(0);
        alongField.SetValue(rig, haul.x);
        for (int p = 0; p < 2; p++)
        {
            rig.SetPreset(p);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"cannon_haul_back_{"AB"[p]}");
        }

        // 끄는 모습 옆에서 (통로 안쪽에서 바깥을 보며 대포 옆쪽으로)
        haul = sim.CannonHauls.Count > 0 ? sim.CannonHauls[0].Position : haul;
        front.transform.position = haul + new Vector3(0f, 0.45f, -1.8f);
        front.transform.LookAt(haul + new Vector3(0f, 0.2f, 0f));
        front.enabled = true;
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture("cannon_haul_side");
        UnityEngine.Object.Destroy(front.gameObject);

        File.WriteAllText(Path.Combine(Out, "cannon_art.txt"), log.ToString(), new UTF8Encoding(false));
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
        yield return Click("넘기기", 600);
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

    const string KeyPrefix = "btn:";   // HumanLastRampart.UI.Ui.KeyPrefix (이 시험 어셈블리는 UI를 참조하지 않는다)

    static bool InputOpen() => UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Any(e => e.isActiveAndEnabled);

    static Button FindReady(string label)
    {
        if (!InputOpen()) return null;
        foreach (Button b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if (!b.isActiveAndEnabled || !b.IsInteractable()) continue;
            if (b.name == KeyPrefix + label) return b;   // K-047: 단추 열쇠 (보이는 글자와 따로)
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
                if (b.name.StartsWith(KeyPrefix + prefix) || (t != null && t.text.StartsWith(prefix))) { b.onClick.Invoke(); yield return null; yield break; }
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
        yield return Click("넘기기", 600);
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
        yield return Click("넘기기", 600);
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

    // 총안 자리 (K-045): 정면 가까이 앞줄이 돌출부 사이 총안에 선 모습(낮), 밤 횃불이 돌 뒤에서 병사와 안 겹치는지
    [UnityTest]
    public IEnumerator WallSlots()
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
        yield return Click("넘기기", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        for (int i = 0; i < 60; i++) yield return null;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", flags).GetValue(entry);
        var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("rig", flags).GetValue(entry);
        var setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
        Time.captureDeltaTime = 0.0005f;

        for (int side = 0; side < 4; side++)
            Note($"{side}면: 총안 {sim.SlotCount(side, 0)}칸, 돌 뒤 {sim.SlotCount(side, 1)}칸, 횃불 {sim.TorchSpots(side).Count}개, 대포 사격구 {sim.CannonBays(side).Count}곳");
        var rows = new int[6];
        for (int i = 0; i < sim.SoldierCount; i++)
        {
            int row = sim.SoldierRow(i, out _, out _);
            if (row >= 0) rows[row]++;
        }
        Note($"줄별 병사: 총안 {rows[0]} · 돌 뒤 {rows[1]} · 둘째 총안 줄 {rows[2]} · 그 뒤 {rows[3] + rows[4]}");

        rig.ShowSide(0);
        foreach (var (w, t, name) in new[] {
            (HumanLastRampart.Core.CombatContracts.Weather.Clear, HumanLastRampart.Core.CombatContracts.TimeOfDay.Day, "day"),
            (HumanLastRampart.Core.CombatContracts.Weather.Clear, HumanLastRampart.Core.CombatContracts.TimeOfDay.Night, "night") })
        {
            var f = setup.Forecast; f.InitialWeather = w; f.TimeOfDay = t; f.Changes = false; setup.Forecast = f;
            sim.WeatherDirty = true;
            for (int p = 0; p < 2; p++)
            {
                rig.SetPreset(p);
                for (int i = 0; i < 5; i++) yield return null;
                yield return Capture($"wall_slots_{name}_{"AB"[p]}");
            }
        }

        File.WriteAllText(Path.Combine(Out, "wall_slots.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }

    // 3등신 시험 그림 비교 (K-057, Codex K-056): 같은 장면·같은 카메라(A, 정면 성벽)에서 스위치 끔(old)/켬(new) 사진 쌍.
    // 낮·밤, 그리고 주인공 달리기 순간. 궁수·주인공만 바뀌고 다른 병종·적은 그대로인 게 정상
    [UnityTest]
    public IEnumerator ProportionTrial()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        HumanLastRampart.Combat.Battle.ProportionTrial.Enabled = false;
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("현실", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("넘기기", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        for (int i = 0; i < 80; i++) yield return null;   // 적이 다가오고 궁수가 쏘기 시작하게
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", flags).GetValue(entry);
        var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("rig", flags).GetValue(entry);
        var setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
        Time.captureDeltaTime = 0.0005f;   // 전투는 거의 멈춤 — old/new가 같은 순간
        try
        {
            // 정면(북) 궁수 구간으로 주인공·카메라를 옮긴다 — 성벽 가운데에는 총병만 있어 궁수가 안 보였다 (K-057 1차 시험)
            int archerStart = 0, archerCount = 0;
            foreach (var (unit, start, count) in sim.SoldierGroups)
                if (HumanLastRampart.Combat.Battle.ProportionTrial.IsArcher(unit)) { archerStart = start; archerCount = count; }
            Assert.Greater(archerCount, 0, "궁수 병종이 전투에 있다");
            float archerAlong = float.NaN;
            for (int i = archerStart; i < archerStart + archerCount; i++)
            {
                int row = sim.SoldierRow(i, out int side, out float lateral);
                if (row < 0 || side != 0) continue;
                if (float.IsNaN(archerAlong) || Mathf.Abs(lateral) < Mathf.Abs(archerAlong)) archerAlong = lateral;   // 가운데에서 가장 가까운 정면 궁수
            }

            Assert.IsFalse(float.IsNaN(archerAlong), "정면 성벽에 궁수가 서 있다");
            Note($"정면 궁수 구간: 성벽 따라 {archerAlong:0.0}m");
            var alongField = typeof(HumanLastRampart.Combat.Battle.BattleCameraRig).GetField("along", flags);
            rig.ShowSide(0);
            alongField.SetValue(rig, archerAlong);   // 북쪽: 오른쪽 = +x
            rig.SetPreset(0);
            for (int i = 0; i < 5; i++) yield return null;
            foreach (var (w, t, name) in new[] {
                (HumanLastRampart.Core.CombatContracts.Weather.Clear, HumanLastRampart.Core.CombatContracts.TimeOfDay.Day, "day"),
                (HumanLastRampart.Core.CombatContracts.Weather.Clear, HumanLastRampart.Core.CombatContracts.TimeOfDay.Night, "night") })
            {
                var f = setup.Forecast; f.InitialWeather = w; f.TimeOfDay = t; f.Changes = false; setup.Forecast = f;
                sim.WeatherDirty = true;
                foreach (bool trial in new[] { false, true })
                {
                    HumanLastRampart.Combat.Battle.ProportionTrial.Enabled = trial;
                    for (int i = 0; i < 5; i++) yield return null;
                    yield return Capture($"proportion_{name}_A_{(trial ? "new" : "old")}");
                }
            }

            // 궁수 사격 순간 — 낮. 전투를 돌리며 카메라 근처(±4m) 정면 궁수가 사격 칸일 때 멈추고 old/new
            var g = setup.Forecast; g.InitialWeather = HumanLastRampart.Core.CombatContracts.Weather.Clear; g.TimeOfDay = HumanLastRampart.Core.CombatContracts.TimeOfDay.Day; g.Changes = false; setup.Forecast = g;
            sim.WeatherDirty = true;
            HumanLastRampart.Combat.Battle.ProportionTrial.Enabled = false;
            Time.captureDeltaTime = 1f / 20f;
            int firing = -1;
            for (int k = 0; k < 400 && firing < 0; k++)
            {
                yield return null;
                sim.Complete();
                for (int i = archerStart; i < archerStart + archerCount && firing < 0; i++)
                {
                    if (sim.SoldierRow(i, out int side, out float lateral) < 0 || side != 0 || Mathf.Abs(lateral - archerAlong) > 4f) continue;
                    var inst = sim.SoldierInstances[i];
                    float frame = inst.PosFrame.w;
                    if (inst.TintSize.w > 0f && frame >= HumanLastRampart.Combat.PerfLab.PerfLabArt.FireFrame0 && frame < HumanLastRampart.Combat.PerfLab.PerfLabArt.FireFrame0 + HumanLastRampart.Combat.PerfLab.PerfLabArt.PoseWindow)
                        firing = i;
                }
            }

            Time.captureDeltaTime = 0.0005f;
            if (firing >= 0)
            {
                Note($"궁수 사격 순간: 병사 {firing}");
                yield return Capture("proportion_archer_fire_A_old");
                HumanLastRampart.Combat.Battle.ProportionTrial.Enabled = true;
                for (int i = 0; i < 3; i++) yield return null;
                yield return Capture("proportion_archer_fire_A_new");
                HumanLastRampart.Combat.Battle.ProportionTrial.Enabled = false;
                for (int i = 0; i < 3; i++) yield return null;
            }
            else
            {
                Note("궁수 사격 순간을 400프레임 안에 찾지 못함 (사진 없음)");
            }

            // 주인공 달리기 (오른쪽, 옆 달리기 칸) — 낮
            rig.WalkInput = 1f;
            for (int i = 0; i < 8; i++) yield return null;
            Assert.AreEqual(HumanLastRampart.Combat.Battle.BattleCameraRig.HeroPose.Run, rig.Pose, "오른쪽으로 달리는 중");
            yield return Capture("proportion_hero_run_A_old");
            HumanLastRampart.Combat.Battle.ProportionTrial.Enabled = true;
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("proportion_hero_run_A_new");
            rig.WalkInput = 0f;
            for (int i = 0; i < 8; i++) yield return null;
            yield return Capture("proportion_hero_idle_A_new");
            HumanLastRampart.Combat.Battle.ProportionTrial.Enabled = false;
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("proportion_hero_idle_A_old");
            Note("3등신 시험: 궁수(서기·사격)와 주인공만 바뀜. 시험 그림 인물 키 192px → 기존(서기 300px)과 같은 월드 키로 맞춤");
        }
        finally
        {
            HumanLastRampart.Combat.Battle.ProportionTrial.Enabled = false;   // 다른 시험에 남지 않게
            File.WriteAllText(Path.Combine(Out, "proportion_trial.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }

    // 메인 설정 창·이어하기 (K-062): 저장은 임시 폴더에서만(사용자 저장을 건드리지 않게), 설정은 끝나면 원래 값으로
    [UnityTest]
    public IEnumerator SettingsAndContinue()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        string previousFolder = HumanLastRampart.Core.Save.CampaignSave.Folder;
        string folder = Path.Combine(Path.GetTempPath(), "hlr_probe_save_" + Guid.NewGuid().ToString("N"));
        HumanLastRampart.Core.Save.CampaignSave.Folder = folder;
        HumanLastRampart.Core.Settings.GameSettings.Load();
        bool effects = HumanLastRampart.Core.Settings.GameSettings.ScreenEffects;
        try
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            Assert.IsNull(FindReadyPrefix("이어하기"), "저장이 없으면 이어하기 버튼 없음");
            yield return Capture("main_menu_no_save");

            yield return Click("설정", 300);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("settings_popup");
            yield return ClickPrefix("화면 효과", 60);
            Assert.AreEqual(!effects, HumanLastRampart.Core.Settings.GameSettings.ScreenEffects, "누르면 바로 바뀐다");
            yield return Capture("settings_popup_toggled");
            HumanLastRampart.Core.Settings.GameSettings.SetScreenEffects(effects);
            yield return Click("닫기", 60);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsNotNull(FindReady("게임 시작"), "설정을 닫으면 메인 메뉴 입력이 돌아온다");

            // 저장 하나를 만들고 메인을 다시 열면 이어하기 → 정비 화면
            var campaign = HumanLastRampart.App.Data.CampaignFactory.Create(GameFlow.Instance.Definitions, new NewGameOptions(Difficulty.Realistic), 4321);
            campaign.BattleNumber = 2;
            Assert.IsTrue(HumanLastRampart.Core.Save.CampaignSave.Save(campaign, ScreenId.Prep, out string error), error);
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            Assert.IsNotNull(FindReadyPrefix("이어하기"), "저장이 있으면 이어하기 버튼");
            yield return Capture("main_menu_continue");
            yield return ClickPrefix("이어하기", 120);
            yield return WaitScreen(ScreenId.Prep, 900);
            Assert.AreEqual(2, GameFlow.Instance.Campaign.BattleNumber, "저장한 진행을 불러옴");
            yield return Capture("prep_after_continue");
            Note($"이어하기: {GameFlow.Instance.Campaign.BattleNumber}번째 전투 앞 정비, 골드 {GameFlow.Instance.Campaign.Gold}");
        }
        finally
        {
            HumanLastRampart.Core.Settings.GameSettings.SetScreenEffects(effects);
            HumanLastRampart.Core.Save.CampaignSave.Folder = previousFolder;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            File.WriteAllText(Path.Combine(Out, "settings_continue.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }

    static Button FindReadyPrefix(string prefix)
    {
        if (!InputOpen()) return null;
        foreach (Button b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if (!b.isActiveAndEnabled || !b.IsInteractable()) continue;
            Text t = b.GetComponentInChildren<Text>();
            if (t != null && t.text.StartsWith(prefix)) return b;
        }
        return null;
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
        yield return Click("넘기기", 600);
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
        yield return Click("넘기기", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);

        // 선봉이 45~55m에서 나와(2026-10-22) 4배속이면 확인 전에 성벽 앞에 닿는다 → '대기' 확인은 보통 속도로 먼저, 4배속은 그다음
        // '대기' 확인은 적이 성벽에 닿기 전에 (사용자 2026-10-20 #10: 성 안마당은 이제 칠 수 없어 성벽 바로 앞을 노린다)
        // 북쪽은 선봉이 곧바로 사거리에 들어오므로, 첫 무리가 오지 않는 남쪽 대포로 '대기'를 확인한다
        for (int i = 0; i < 10; i++) yield return null;
        entry.ToggleMap();
        // 같은 프레임에 4면의 4번 대포가 각자 성벽 바로 앞을 노린다. 어느 한 면에는 선봉이 일찍 닿을 수 있다 (게임 안 시계가 실행마다 조금 달라 어느 면인지 정해져 있지 않다)
        // → 하나라도 '대기'면 통과. 적이 오는 길을 노린 대포는 대기가 아니므로 이 시험이 느슨해지지 않는다 (면마다 따로 조준)
        var sideOf4 = new int[4];
        var dirs = new[] { new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(-1f, 0f) };   // 북·동·남·서
        foreach (int side in new[] { 2, 1, 3, 0 })
        {
            entry.SelectCannonSide(side);
            yield return null;
            entry.SelectCannon(4);
            int index = -1;
            for (int c = 0; c < sim.CannonCount; c++) { var ci = sim.GetCannon(c); if (ci.Side == side && ci.Number == 4) index = c; }
            Assert.GreaterOrEqual(index, 0, $"{side}면 4번 대포");
            sideOf4[side] = index;
            Vector2 p = new Vector2(sim.GetCannon(index).Position.x, sim.GetCannon(index).Position.z), d = dirs[side];
            Vector2 aim2 = p - d * Vector2.Dot(p, d) + d * (sim.CastleHalf + 5f);
            Assert.IsTrue(entry.AimSelectedCannonAt(new Vector3(aim2.x, 0f, aim2.y)), $"{side}면 성벽 바로 앞 조준");
        }
        // '대기'는 대포가 장전을 마치고 조준점을 한 번 살펴본 뒤에야 정해진다 → 최대 100프레임(약 5초) 기다린다
        int idleSide = -1;
        var trace = new StringBuilder();
        for (int i = 0; i < 100 && idleSide < 0; i++)
        {
            yield return null;
            sim.Complete();
            if (i % 20 == 0) trace.Append($"[{i}:{sim.Elapsed:0.0}s " + string.Join("", new[] { 0, 1, 2, 3 }.Select(s => sim.GetCannon(sideOf4[s]).Idle ? "I" : "F")) + "] ");
            if (i >= 20) foreach (int side in new[] { 2, 1, 3, 0 }) if (idleSide < 0 && sim.GetCannon(sideOf4[side]).Idle) idleSide = side;
        }
        Note($"초반 4번 대포 대기: {idleSide}면 (게임 {sim.Elapsed:0}초) {trace}");
        Assert.GreaterOrEqual(idleSide, 0, "적 없는 곳을 노린 대포는 대기");
        entry.SelectCannonSide(idleSide);
        yield return null;
        var early = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Where(t => t.name == "Cannon 4 State" && t.gameObject.activeInHierarchy).ToList();
        Assert.IsTrue(early.Any(t => t.text == "대기"), "화면에도 대기로 보인다");
        entry.ToggleMap();
        yield return Click("×4", 300);

        for (int i = 0; i < 210; i++) yield return null;
        entry.ToggleMap();
        entry.SelectCannonSide(0);
        yield return null;
        Assert.AreEqual(16, sim.CannonCount, "대포 4면 × 4문");
        // K-037 사거리를 절반으로 줄임 — 고정 좌표(성벽 앞 60m) 대신 대포마다 사거리의 60% 앞을 노린다
        for (int n = 1; n <= 3; n++)
        {
            entry.SelectCannon(n);
            HumanLastRampart.Combat.Battle.BattleSim.CannonInfo info = default;
            for (int c = 0; c < sim.CannonCount; c++) { var ci = sim.GetCannon(c); if (ci.Side == 0 && ci.Number == n) info = ci; }
            var aim = new Vector3(info.Position.x * 0.5f, 0f, info.Position.z + info.Range * 0.6f);   // 적이 오는 가운데 쪽으로 조금
            Assert.IsTrue(entry.AimSelectedCannonAt(aim), $"대포 {n} 조준 (사거리 안, 사거리 {info.Range:0}m)");
            Note($"대포 {n}: 사거리 {info.Range:0}m, 조준 {aim}");
        }
        entry.SelectCannon(4);
        Assert.IsFalse(entry.AimSelectedCannonAt(new Vector3(0f, 0f, 250f)), "사거리 밖은 거부");
        // 진짜 성 안마당(북쪽 면 안, 성벽 반폭 20m 안). 예전 (30,20)은 동쪽 성벽 밖이라 우회 적이 오면 포격해 시험이 들쑥날쑥했다
        // 사용자 2026-10-20 #10: 대포는 자기 성벽선 앞만 — 성 안마당은 이제 거부. 대기 확인은 성벽 바로 앞(적이 아직 안 온 곳)으로
        Assert.IsFalse(entry.AimSelectedCannonAt(new Vector3(2f, 0f, 10f)), "성 안마당(성벽 뒤)은 거부");
        for (int i = 0; i < 60; i++) yield return null;
        yield return Capture("cannon_aim_side");
        // K-047 포병판: 줄마다 상태 낱말 칸 "Cannon N State" (포격·대기·자동·병사 없음)
        var states = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Cannon ") && t.name.EndsWith(" State") && t.gameObject.activeInHierarchy).ToList();
        Note("대포 표시: " + string.Join(" | ", states.OrderBy(t => t.name).Select(t => t.name.Substring(7, 1) + "번 " + t.text)));
        Assert.IsTrue(states.Any(t => t.text == "포격"), "적이 오는 길을 노린 대포는 포격");
        // K-046 불붙이기: 쏘기 전 1.5초(막대 뻗기 0.5 + 도화선 1) 동안 '불붙이는 중' — 간격 6초라 몇 초 안에 한 번은 보인다
        int igniting = -1;
        for (int i = 0; i < 200 && igniting < 0; i++)
        {
            yield return null;
            sim.Complete();
            for (int c = 0; c < sim.CannonCount && igniting < 0; c++) if (sim.GetCannon(c).Igniting) igniting = c;
        }

        Assert.GreaterOrEqual(igniting, 0, "대포병이 불붙이는 중인 대포가 있다");
        Note($"불붙이는 대포: {sim.GetCannon(igniting).Side}면 {sim.GetCannon(igniting).Number}번");
        yield return Capture("cannon_ignite_map");
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
        yield return Click("넘기기", 600);
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
        yield return Click("넘기기", 600);
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
        Assert.IsTrue(labels.Any(t => t.StartsWith("약 ")), "지도에 무리 표시");   // K-047: 방향은 지도 자리로 보이므로 "약 320"만
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
        for (int i = 0; i < frames && (GameFlow.Instance == null || GameFlow.Instance.CurrentScreen != screen || !InputOpen()); i++)
        {
            // W12: 이야기가 사슬로 이어진다 (첫 이야기 → 요새 도착, 점화 → 도착). 이야기 아닌 화면을 기다리는 중이면 이야기는 건너뛴다
            if (screen != ScreenId.Story && GameFlow.Instance != null && GameFlow.Instance.CurrentScreen == ScreenId.Story && GameFlow.Instance.Campaign?.NextStory?.Kind == StoryKind.Normal && InputOpen())
                UnityEngine.Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>()?.Skip();
            yield return null;
        }

        Assert.AreEqual(screen, GameFlow.Instance.CurrentScreen, "화면 전환");
        for (int i = 0; i < 5; i++) yield return null;
        // 전투 준비(D-071) 지도는 기본으로 바로 넘긴다 — 준비 화면 자체는 PrepPhase에서 찍는다
        if (screen == ScreenId.Battle && !KeepPreparation) StartPreparedBattle();
    }

    static bool KeepPreparation;

    static void StartPreparedBattle()
    {
        foreach (var entry in UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None))
            if (entry.Preparing) entry.StartBattle();
    }

    // D-071 전투 준비: 전투 시작 전 지도가 열리고 시간이 멈춤 → 성벽 하나에 명령 → [전투 시작]으로 1번 화면
    [UnityTest]
    public IEnumerator PrepPhase()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("현실", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("넘기기", 600);
        KeepPreparation = true;
        try
        {
            yield return WaitScreen(ScreenId.Battle, 900);
        }
        finally { KeepPreparation = false; }

        var entry = UnityEngine.Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
        var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
        Assert.IsTrue(entry.Preparing, "전투 시작 전 준비 상태");
        float before = sim.Elapsed;
        for (int i = 0; i < 30; i++) yield return null;
        Assert.AreEqual(before, sim.Elapsed, 0.0001f, "준비 중에는 시간이 멈춘다 (현실도)");
        yield return Click("동쪽", 60);   // 받을 곳: 동쪽 성벽
        yield return Click("특수 적 먼저", 60);
        Note($"준비 중 동쪽 명령: {BattleSim_PriorityName(sim, 1)}");
        Assert.AreEqual(HumanLastRampart.Core.Campaign.TargetPriority.SpecialFirst, sim.PriorityOf(1), "준비 중 성벽 명령은 바로 적용");
        // D-072 병력 묶음: 준비 중 북쪽 묶음 하나를 동쪽으로 → 바로 옮겨지고 병력 수가 따라간다
        int northStart = sim.Sides[0].GarrisonStart, eastStart = sim.Sides[1].GarrisonStart;
        int moved = -1;
        for (int g = 0; g < sim.GroupCount && moved < 0; g++)
        {
            var info = sim.GetGroup(g);
            if (info.Movable && info.Side == 0 && info.Alive > 0) moved = g;
        }
        Assert.GreaterOrEqual(moved, 0, "북쪽에 옮길 수 있는 묶음");
        int movedCount = sim.GetGroup(moved).Alive;
        Assert.LessOrEqual(movedCount, HumanLastRampart.Combat.Battle.BattleSim.GroupSize, "10명 묶음");
        entry.OrderGroupMove(moved, 1);
        yield return null;
        Assert.AreEqual(1, sim.GetGroup(moved).Side, "준비 중 묶음 이동은 바로");
        Assert.AreEqual(northStart - movedCount, sim.Sides[0].GarrisonStart, "북쪽 처음 병력에서 빠짐 (떠났다고 패배 아님)");
        Assert.AreEqual(eastStart + movedCount, sim.Sides[1].GarrisonStart, "동쪽에 더해짐");
        Note($"준비 중 묶음 이동: {movedCount}명 북→동, 북 {northStart}→{sim.Sides[0].GarrisonStart}, 동 {eastStart}→{sim.Sides[1].GarrisonStart}");

        // 2차 피드백 11: 남쪽 묶음을 한 번에 서쪽으로 — 남쪽에 옮길 수 있는 병사가 20명 이상 남는다
        int southGroup = -1;
        for (int g = 0; g < sim.GroupCount && southGroup < 0; g++)
        {
            var info = sim.GetGroup(g);
            if (info.Movable && info.Side == 2 && info.Alive > 0) southGroup = g;
        }
        if (southGroup >= 0)
        {
            entry.OrderWallMove(southGroup, 3);
            yield return null;
            int southLeft = 0;
            for (int g = 0; g < sim.GroupCount; g++) { var info = sim.GetGroup(g); if (info.Movable && info.Side == 2) southLeft += info.Alive; }
            Assert.GreaterOrEqual(southLeft, HumanLastRampart.Combat.Battle.BattleSim.KeepPerWall, "한 번에 옮겨도 20명은 남는다");
            Note($"한 번에 옮기기 남→서: 남쪽 옮길 수 있는 병사 {southLeft}명 남음, 서쪽 처음 병력 {sim.Sides[3].GarrisonStart}");
        }
        yield return Capture("prep_map");
        yield return Click("전투 시작", 60);
        Assert.IsFalse(entry.Preparing, "전투 시작 버튼");
        for (int i = 0; i < 30; i++) yield return null;
        Assert.Greater(sim.Elapsed, before, "시작 뒤 시간이 흐른다");
        yield return Capture("prep_started");

        // 전투 중: 주인공(북쪽) 성벽의 다른 묶음을 서쪽으로 → 달려서 간다
        int running = -1;
        for (int g = 0; g < sim.GroupCount && running < 0; g++)
        {
            var info = sim.GetGroup(g);
            if (info.Movable && info.Side == 0 && info.Alive > 0) running = g;
        }
        if (running >= 0)
        {
            Time.captureDeltaTime = 1f / 20f;
            entry.OrderGroupMove(running, 3);
            yield return null;
            yield return null;
            Assert.IsTrue(sim.GetGroup(running).Moving, "전투 중 묶음은 달려서 간다");
            float started = sim.Elapsed;
            for (int i = 0; i < 1200 && sim.GetGroup(running).Moving; i++) yield return null;
            Time.captureDeltaTime = 0f;
            Assert.AreEqual(3, sim.GetGroup(running).Side, "서쪽 도착");
            Note($"전투 중 묶음 이동 북→서: {sim.Elapsed - started:0.0}초");
        }
        File.WriteAllText(Path.Combine(Out, "prep_phase.txt"), log.ToString(), new UTF8Encoding(false));
    }

    static string BattleSim_PriorityName(HumanLastRampart.Combat.Battle.BattleSim sim, int side) => HumanLastRampart.Combat.Battle.BattleSim.PriorityNames[(int)sim.PriorityOf(side)];

    // 배치 모드에는 화면 그리기 단계(WaitForEndOfFrame)가 없다. 전투의 LateUpdate(인스턴싱 그리기 예약) 뒤에
    // 도는 찍개 컴포넌트가 같은 프레임 안에서 카메라를 직접 그려 찍는다. 화면 덮개 UI는 잠시 카메라 공간으로 바꾼다.
    IEnumerator Capture(string name, int width = 1600, int height = 900, bool sceneOnly = false)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
        // K-047: 덜 열린 반투명 판이 찍히지 않게 열리는 연출을 바로 끝낸다 (프레임을 기다리면 전투가 흘러 시점 시험이 어긋난다)
        HumanLastRampart.UI.UiAppear.FinishAll();   // 닫히던 판도 끝낸다 (K-047: 앞 창이 뒤에 비쳐 찍히던 것)
        var go = new GameObject("PlayProbe Shooter");
        var shooter = go.AddComponent<ProbeShooter>();
        shooter.Width = width;
        shooter.Height = height;
        shooter.SceneOnly = sceneOnly;
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
    public int Width = 1600, Height = 900;   // 발표 자료용 사진은 1920×1080 (PlayProbe.Portfolio.cs)
    public bool SceneOnly;                    // 화면 위 표시판 없이 장면만

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
        int w = Width, h = Height;
        var rt = new RenderTexture(w, h, 24);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var cams = Camera.allCameras.OrderBy(c => c.depth).ToArray();
        var changed = new System.Collections.Generic.List<Canvas>();
        var layers = new System.Collections.Generic.List<(GameObject go, int layer)>();
        GameObject uiCamObject = null;
        try
        {
            // 1) 장면만 찍는다 (화면 위 표시판은 사진기에 안 그려진다)
            foreach (Camera cam in cams)
            {
                // 정비 마당처럼 UI에 넣을 전용 렌더 텍스처는 원래 크기·비율로 갱신한다.
                if (cam.targetTexture != null) { cam.Render(); continue; }
                cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            }
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
            if (changed.Count > 0 && !SceneOnly)
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
