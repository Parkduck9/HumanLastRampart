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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 게임 한 바퀴 통과 시험 (K-053, FirstRead 완료 기준): 메인(0) → 스토리(5) → 전투(1·2: 준비·지도·시작·승리) → 결과(4)
// → 정비 마당(3: 수리·병영 모집·함정 공방·지형 탭) → 계단 → 두 번째 전투 시작. 패배 경로: 패배 → 패배 엔딩 → 메인.
// 화면 모양이 바뀌어도(K-047) 덜 깨지게: 화면 일은 공개 함수(StoryPlayer.Advance·Skip, BattleEntry.StartBattle·ToggleMap,
// PrepYard.Use)로 부르고, 단추는 게임오브젝트 이름 → 글자 순으로 찾는다. 단추 이름·장소 이름은 아래 한곳에 모았다
public sealed partial class PlayProbe
{
    static class LoopKeys
    {
        public const string NewGame = "게임 시작", Easy = "쉬움";
        public const string Speed4 = "×4", TestWin = "[시험] 승리", TestLoseWall = "[시험] 패배 — 성벽 붕괴";
        public const string ResultNext = "다음";
        public const string Repair = "성벽 수리", Barracks = "병영", Workshop = "함정 공방", Stairs = "성벽 계단";   // PrepEntry 장소 이름 (K-047 간판)
        public const string RepairQuote = "수리:100", RepairConfirm = "확정 수리";   // K-047: 수리 카드 (값은 바로 보인다)
        public const string PostPrefix = "지점:", PlusTen = "인원 +10", RecruitConfirm = "확정 모집";   // K-047: 견적 단추 없음
        public const string CellPrefix = "cell_", TrapBuyPrefix = "한 번에 구매·설치", TabPrefix = "탭:지형", BuildPrefix = "한 번에 짓기";
        public const string Close = "닫기", StartNextBattle = "다음 전투 시작";
    }

    [UnityTest]
    public IEnumerator FullLoop()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;   // 전투 준비 단계도 직접 지난다
        try
        {
            // ── 0 메인 ──
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Capture("loop_00_main");
            yield return Press(LoopKeys.NewGame, 600);
            yield return Press(LoopKeys.Easy, 300);

            // ── 5 스토리 ──
            yield return WaitScreen(ScreenId.Story, 600);
            CampaignState c = GameFlow.Instance.Campaign;
            Assert.IsNotNull(c, "새 게임 진행 데이터");
            Note($"새 게임: 쉬움, 전투 {c.BattleNumber}, 골드 {c.Gold}, 병력 {c.Squads.Sum(s => s.Count)}, 이야기 {c.NextStory?.StoryId}");
            HumanLastRampart.Story.StoryPlayer story = null;
            for (int i = 0; i < 300 && story == null; i++) { story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); if (story == null) yield return null; }
            Assert.IsNotNull(story, "스토리 재생기");
            Assert.Greater(story.LineTotal, 0, "이야기 줄이 있다");
            for (int i = 0; i < 400 && StoryText().Length < 2; i++) yield return null;
            yield return Capture("loop_01_story");
            story.Advance();   // 나오는 중 → 끝까지
            yield return null;
            story.Advance();   // 다음 줄
            for (int i = 0; i < 30; i++) yield return null;
            Note($"스토리 {story.LineIndex + 1}/{story.LineTotal}줄까지 넘김 → 넘기기");
            yield return Capture("loop_02_story_next");
            story.Skip();

            // ── 1·2 전투: 준비(지도) → 시작 → 지도 → 승리 ──
            yield return WaitScreen(ScreenId.Battle, 900);
            BattleSetup setup1 = c.ActiveBattleSetup;
            Assert.IsNotNull(setup1, "전투 준비 정보");
            Assert.AreEqual(1, setup1.BattleNumber);
            var entry = Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
            Assert.IsTrue(entry.Preparing, "전투 준비 단계(지도가 열린 채 멈춤)로 시작");
            yield return Capture("loop_03_battle_prepare");
            entry.StartBattle();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(entry.Preparing, "[전투 시작] 뒤 준비 끝");
            yield return Press(LoopKeys.Speed4, 300);
            for (int i = 0; i < 150; i++) yield return null;
            yield return Capture("loop_04_battle");
            entry.ToggleMap();
            for (int i = 0; i < 5; i++) yield return null;
            yield return Capture("loop_05_battle_map");
            entry.ToggleMap();
            for (int i = 0; i < 3; i++) yield return null;
            int gold0 = c.Gold, soldiers0 = c.Squads.Sum(s => s.Count);
            yield return Press(LoopKeys.TestWin, 300);

            // ── 4 결과 ──
            yield return WaitScreen(ScreenId.Result, 900);
            yield return WaitPressable(LoopKeys.ResultNext, 300);
            BattleResult r = c.LastBattle;
            Assert.IsNotNull(r, "전투 결과");
            Assert.AreEqual(BattleOutcome.Victory, r.Outcome);
            Assert.AreEqual(gold0 + r.EarnedGold, c.Gold, "결과 골드 한 번 반영");
            Assert.AreEqual(soldiers0 - r.SquadLosses.Sum(l => l.LostCount), c.Squads.Sum(s => s.Count), "잃은 병사 반영");
            foreach (WallOutcome w in r.WallOutcomes)
                Assert.AreEqual(w.HpAfter, c.Wall.First(x => x.SegmentId == w.SegmentId).Hp, "성벽 피해 반영");
            Assert.AreEqual(2, c.BattleNumber, "승리 뒤 다음 전투 번호");
            Note($"결과: 골드 {gold0}→{c.Gold}, 병력 {soldiers0}→{c.Squads.Sum(s => s.Count)}, 성벽 {string.Join("/", c.Wall.Select(w => w.Hp))}");
            yield return Capture("loop_06_result");
            yield return Press(LoopKeys.ResultNext, 300);

            // ── 3 정비 마당 ──
            yield return WaitScreen(ScreenId.Prep, 900);
            var yard = Object.FindAnyObjectByType<HumanLastRampart.Meta.PrepYard>();
            Assert.IsNotNull(yard, "정비 마당");
            Assert.AreEqual(5, yard.StationCount, "장소 5곳 (수리·병영·함정 공방·상점·계단, K-058)");
            yield return Capture("loop_07_prep_yard");

            // 수리 1번: 북쪽 성벽 55HP 손상 → 100HP 견적 → 확정 (55HP, 6골드 올림)
            WallSegmentState wall = c.Wall[0];
            wall.Hp = wall.MaxHp - 55;
            string wallId = wall.SegmentId;
            int goldRepair = c.Gold;
            Assert.IsTrue(yard.Use(LoopKeys.Repair), "수리 장소");
            yield return Press(LoopKeys.RepairQuote, 120);
            yield return Press(LoopKeys.RepairConfirm, 60);
            Assert.AreEqual(c.Wall.First(w => w.SegmentId == wallId).MaxHp, c.Wall.First(w => w.SegmentId == wallId).Hp, "수리 반영");
            Assert.Less(c.Gold, goldRepair, "수리 비용");
            yield return Capture("loop_08_repair");
            yield return Press(LoopKeys.Close, 60);

            // 모집 1번: 북쪽 첫 분대 지점(대포 지점 건너뜀)에 +10 → 견적 → 확정
            int goldRecruit = c.Gold, total0 = c.Squads.Sum(s => s.Count);
            Assert.IsTrue(yard.Use(LoopKeys.Barracks), "병영 장소");
            // K-047 병영 장부: 지점은 칸으로 늘어서 있다 — 이름(btn:지점:<지점 ID>)에 _squad_가 든 첫 칸을 바로 누른다
            Button post = null;
            for (int k = 0; k < 120 && post == null; k++)
            {
                post = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .Where(b => b.isActiveAndEnabled && b.IsInteractable() && b.name.StartsWith(KeyPrefix + LoopKeys.PostPrefix) && b.name.Contains("_squad_"))
                    .OrderBy(b => b.name, System.StringComparer.Ordinal).FirstOrDefault();
                if (post == null) yield return null;
            }
            Assert.IsNotNull(post, "분대 지점 칸");
            post.onClick.Invoke();
            yield return null;
            yield return Press(LoopKeys.PlusTen, 60);
            yield return Press(LoopKeys.RecruitConfirm, 60);
            int recruited = c.Squads.Sum(s => s.Count) - total0;
            Assert.Greater(recruited, 0, "모집 반영");
            Assert.Less(c.Gold, goldRecruit, "모집 비용");
            yield return Capture("loop_09_recruit");
            yield return Press(LoopKeys.Close, 60);

            // 함정 자유 위치 구매 → 지형 탭에서 짓기
            int goldTrap = c.Gold, traps0 = c.Traps.Count;
            int works0 = c.TerrainWorks?.Count ?? 0;
            Assert.IsTrue(yard.Use(LoopKeys.Workshop), "함정 공방 장소");
            for (int i = 0; i < 3; i++) yield return null;
            var trapSession = new HumanLastRampart.Meta.TrapPreparationTransactions(c, GameFlow.Instance.Definitions);
            Vector2 trapPoint = FreeTrapTestSupport.Point(trapSession, "trap_bomb");
            string trapCell = HumanLastRampart.Core.Traps.TrapGrid.FreeId(trapPoint.x, trapPoint.y);
            var trapPlacer = Object.FindAnyObjectByType<HumanLastRampart.Meta.FreePlacementInput>();
            Assert.IsNotNull(trapPlacer, "함정 자유 배치 입력");
            trapPlacer.PlaceAt(FreeTrapTestSupport.MapPoint(trapSession, trapPoint));
            yield return null;
            yield return Press(LoopKeys.TrapBuyPrefix, 60, true);
            Assert.AreEqual(traps0 + 1, c.Traps.Count, "함정 1개 설치");
            Assert.Less(c.Gold, goldTrap, "함정 비용");
            yield return Capture("loop_10_trap");
            yield return Press(LoopKeys.TabPrefix, 60, true);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("loop_11_terrain_tab");

            // 자유 배치 (사용자 2026-10-20): 토벽은 칸이 아니라 지도 위 아무 곳에 — 놓을 수 있는 자리를 찾아 하나는 돌려서 둘 놓는다
            var placer = Object.FindAnyObjectByType<HumanLastRampart.Meta.FreePlacementInput>();
            Assert.IsNotNull(placer, "토벽을 고르면 자유 배치 입력이 켜진다");
            Vector2[] spots = { new Vector2(-150f, 250f), new Vector2(160f, 260f), new Vector2(260f, -140f), new Vector2(-260f, 150f), new Vector2(-140f, -260f), new Vector2(120f, 300f), new Vector2(-300f, -100f), new Vector2(300f, 120f) };
            int placed = 0;
            foreach (Vector2 spot in spots)
            {
                if (placed == 1) placer.Rotate();   // 두 번째는 45도 돌려서
                placer.PlaceAt(spot);
                yield return null;
                Button pick = FindPressable(LoopKeys.BuildPrefix, true);
                string label = pick != null ? ButtonLabel(pick) : "";
                placed = label.StartsWith("2") ? 2 : label.StartsWith("1") ? 1 : placed;
                if (placed >= 2) break;
            }

            placer.MoveTo(new Vector2(-60f, -300f));   // 놓일 모습(초록·빨강)이 사진에 보이게
            yield return null;
            yield return Capture("loop_11b_free_pick");
            if (placed > 0)
            {
                yield return Press(LoopKeys.BuildPrefix, 60, true);
                Note((c.TerrainWorks?.Count ?? 0) > works0 ? $"자유 배치 토벽 {placed}개, 남은 골드 {c.Gold}" : $"자유 배치 짓기 실패 (골드 {c.Gold})");
                yield return null;
                yield return Capture("loop_11c_free_built");
                works0 = c.TerrainWorks?.Count ?? 0;
            }
            else Note("자유 배치: 놓을 자리를 찾지 못함");

            yield return Press("할 일:" + "철거", 60, true);   // 길 내기·옮기기는 없앰 (사용자 2026-10-20 #7) — 칸 방식 할 일은 철거만 남음
            yield return null;
            Button ground = FindPressable(LoopKeys.CellPrefix, true);
            if (ground != null)
            {
                ground.onClick.Invoke();
                yield return null;
                if (FindPressable(LoopKeys.BuildPrefix, true) != null)
                {
                    yield return Press(LoopKeys.BuildPrefix, 60, true);
                    // 짓기 실패(골드 부족 등)는 흐름이 끊긴 것이 아니므로 기록만 — 성공하면 다음 전투 준비에 넘어가는지 아래에서 본다
                    Note((c.TerrainWorks?.Count ?? 0) > works0 ? $"지형 손질: {ground.name}, 남은 골드 {c.Gold}" : $"지형 손질 실패 ({ground.name}, 골드 {c.Gold})");
                }
                else Note($"지형 탭: {ground.name}을 골랐지만 짓기 단추가 꺼져 있음 (골드 {c.Gold})");
            }
            else Note("지형 탭: 지을 수 있는 칸이 없음");
            yield return Press(LoopKeys.Close, 60);
            Note($"정비: 수리 {wallId} −{goldRepair - goldRecruit}골드, 모집 {recruited}명 −{goldRecruit - goldTrap}골드, 함정 {trapCell}, 남은 골드 {c.Gold}");

            // 계단 → 확인 → 다음 전투
            int totalNext = c.Squads.Sum(s => s.Count), trapsNext = c.Traps.Count, worksNext = c.TerrainWorks?.Count ?? 0;
            Assert.IsTrue(yard.Use(LoopKeys.Stairs), "계단 장소");
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("loop_12_stairs_confirm");
            yield return Press(LoopKeys.StartNextBattle, 60);

            // ── 두 번째 전투 시작 ──
            yield return WaitScreen(ScreenId.Battle, 900);
            BattleSetup setup2 = GameFlow.Instance.Campaign.ActiveBattleSetup;
            Assert.AreSame(c, GameFlow.Instance.Campaign, "같은 진행 데이터");
            Assert.AreEqual(2, setup2.BattleNumber, "두 번째 전투");
            Assert.AreNotEqual(setup1.BattleRunId, setup2.BattleRunId, "새 전투 실행 ID");
            Assert.AreEqual(totalNext, setup2.Squads.Sum(s => s.Count), "모집한 병력이 전투 준비에");
            Assert.AreEqual(c.Wall.First(w => w.SegmentId == wallId).MaxHp, setup2.Wall.First(w => w.SegmentId == wallId).Hp, "수리한 성벽이 전투 준비에");
            Assert.AreEqual(trapsNext, setup2.Traps.Count(t => c.Traps.Any(x => x.TrapId == t.TrapId)), "다음 지형 함정이 전투 준비에");
            Assert.AreEqual(worksNext, setup2.TerrainWorks.Count, "지형 손질이 전투 준비에");
            var entry2 = Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
            Assert.IsTrue(entry2.Preparing, "두 번째 전투도 준비 단계부터");
            yield return Capture("loop_13_battle2_prepare");
            entry2.StartBattle();
            for (int i = 0; i < 60; i++) yield return null;
            yield return Capture("loop_14_battle2");
            Note($"두 번째 전투 시작: {setup2.TerrainId}, 병력 {setup2.Squads.Sum(s => s.Count)}, 함정 {setup2.Traps.Count}, 지형 손질 {setup2.TerrainWorks.Count}");
        }
        finally
        {
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "full_loop.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }

    // 패배 경로: 전투 패배 → (결과 화면 건너뜀) 패배 엔딩 스토리 → 메인, 진행 데이터 폐기
    [UnityTest]
    public IEnumerator FullLoopDefeat()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        try
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press(LoopKeys.NewGame, 600);
            yield return Press(LoopKeys.Easy, 300);
            yield return WaitScreen(ScreenId.Story, 600);
            HumanLastRampart.Story.StoryPlayer story = null;
            for (int i = 0; i < 300 && story == null; i++) { story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); if (story == null) yield return null; }
            Assert.IsNotNull(story, "스토리 재생기");
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);   // 준비 단계는 WaitScreen이 바로 넘긴다
            yield return Press(LoopKeys.TestLoseWall, 300);

            // W8: 첫 패배는 엔딩이 아니다 — 등대 복구 절반·연속 패배 1, 같은 요새의 다음 낮. 한 번 더 지면 패배 엔딩
            yield return WaitScreen(ScreenId.Prep, 900);
            Assert.AreEqual(1, GameFlow.Instance.Campaign.ConsecutiveNightLosses, "첫 패배: 연속 패배 1");
            Assert.AreEqual(1, GameFlow.Instance.Campaign.BattleNumber, "진 밤은 밤 번호가 오르지 않는다");
            var lossYard = Object.FindAnyObjectByType<HumanLastRampart.Meta.PrepYard>();
            Assert.IsNotNull(lossYard, "정비 마당");
            Assert.IsTrue(lossYard.Use(LoopKeys.Stairs), "계단 장소");
            for (int i = 0; i < 3; i++) yield return null;
            yield return Press(LoopKeys.StartNextBattle, 60);
            yield return WaitScreen(ScreenId.Battle, 900);
            yield return Press(LoopKeys.TestLoseWall, 300);
            yield return WaitScreen(ScreenId.Story, 900);
            Assert.AreEqual(StoryKind.DefeatEnding, GameFlow.Instance.Campaign.NextStory.Kind, "패배 뒤 패배 엔딩 (결과 화면 건너뜀)");
            HumanLastRampart.Story.StoryPlayer ending = null;
            for (int i = 0; i < 300 && (ending == null || ending == story); i++) { ending = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); if (ending == null) yield return null; }
            Assert.IsNotNull(ending, "패배 엔딩 재생기");
            for (int i = 0; i < 400 && StoryText().Length < 2; i++) yield return null;
            yield return Capture("defeat_00_ending");
            Note($"패배 엔딩 {ending.LineTotal}줄: {StoryText()}");
            ending.Skip();
            yield return WaitScreen(ScreenId.MainMenu, 900);
            Assert.IsNull(GameFlow.Instance.Campaign, "메인 복귀 시 진행 데이터 폐기");
            yield return Capture("defeat_01_main");
            Note("패배 → 패배 엔딩 → 메인, 진행 데이터 폐기");
        }
        finally
        {
            File.WriteAllText(Path.Combine(Out, "full_loop_defeat.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }

    // ── 단추 찾기: 이름 → 글자 (K-047 모양 바뀜에도 덜 깨지게) ──

    static string ButtonLabel(Button b) => b == null ? "" : b.GetComponentInChildren<Text>()?.text ?? "";

    static Button FindPressable(string key, bool prefix = false)
    {
        if (!InputOpen()) return null;
        Button byText = null;
        foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if (!b.isActiveAndEnabled || !b.IsInteractable()) continue;
            if (prefix ? b.name.StartsWith(key) || b.name.StartsWith(KeyPrefix + key) : b.name == key || b.name == KeyPrefix + key) return b;   // K-047 열쇠 이름
            string label = ButtonLabel(b);
            if (byText == null && (prefix ? label.StartsWith(key) : label == key)) byText = b;
        }

        return byText;
    }

    static IEnumerator WaitPressable(string key, int frames, bool prefix = false)
    {
        for (int i = 0; i < frames && FindPressable(key, prefix) == null; i++) yield return null;
        Assert.IsNotNull(FindPressable(key, prefix), $"'{key}' 단추가 {frames}프레임 안에 준비되지 않음 (화면 {GameFlow.Instance?.CurrentScreen}) — 지금 단추: {PressableList()}");
    }

    static IEnumerator Press(string key, int frames, bool prefix = false)
    {
        for (int i = 0; i < frames; i++)
        {
            Button b = FindPressable(key, prefix);
            if (b != null) { b.onClick.Invoke(); yield return null; yield break; }
            yield return null;
        }

        Assert.Fail($"'{key}' 단추가 {frames}프레임 안에 준비되지 않음 (화면 {GameFlow.Instance?.CurrentScreen}) — 지금 단추: {PressableList()}");
    }

    static string PressableList() => string.Join(" / ", Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
        .Where(b => b.isActiveAndEnabled && b.IsInteractable()).Select(b => b.name).Distinct().Take(30));
}
