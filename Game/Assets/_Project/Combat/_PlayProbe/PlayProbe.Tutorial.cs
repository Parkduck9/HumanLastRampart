using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TestTools;

public sealed partial class PlayProbe
{
    // 다른 시험에는 튜토리얼이 끼어들지 않게 끈다 (튜토리얼 시험만 켠다)
    [NUnit.Framework.SetUp]
    public void TutorialOffForOtherProbes()
    {
        TutorialState.Store = new MemoryTutorialStore();
        TutorialState.Suppressed = true;
    }

    [NUnit.Framework.TearDown]
    public void TutorialOffAfterProbe()
    {
        TutorialState.Suppressed = true;
        TutorialState.Store = new PlayerPrefsTutorialStore();
    }

    static IEnumerator WaitTutorialStep(TutorialStep expected, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            var d = Object.FindAnyObjectByType<TutorialDirector>();
            if (d != null && !d.Finished && d.Current == expected) yield break;
            yield return null;
        }
        var last = Object.FindAnyObjectByType<TutorialDirector>();
        Assert.Fail($"튜토리얼 단계 {expected}에 {frames}프레임 안에 닿지 못함 (지금 {(last == null ? "없음" : last.Finished ? "끝" : last.Current.ToString())})");
    }

    static IEnumerator TutorialAdvance(TutorialStep from, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            var d = Object.FindAnyObjectByType<TutorialDirector>();
            if (d == null || d.Finished || d.Current != from) yield break;
            yield return null;
        }
        Assert.Fail($"튜토리얼 단계 {from}이 {frames}프레임 안에 끝나지 않음");
    }

    // 튜토리얼 1~8단계: 걷기 → 망원경(설명) → 지도 → 분대 → 대포(설명) → 마법 → (정비) 함정 → 망원경 업그레이드(설명).
    // 직접 해야 하는 단계는 다른 시험과 같은 공개 함수(BattleEntry.ToggleMap·SelectGroupsInScreenRect·SendSelectedGroups·SelectSpell·CastSelectedSpellAt,
    // FreePlacementInput.PlaceAt)로 하고, 설명 단계는 실제 [다음] 단추를 누른다. 단계마다 사진 한 장
    [UnityTest]
    public IEnumerator TutorialWalkthrough()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        TutorialState.Suppressed = false;
        TutorialState.Store = new MemoryTutorialStore();
        try
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600);
            yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            for (int i = 0; i < 300 && story == null; i++) { yield return null; story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); }
            Assert.IsNotNull(story, "스토리 재생기");
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);

            CampaignState c = GameFlow.Instance.Campaign;
            var entry = Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
            yield return WaitTutorialStep(TutorialStep.Walk, 60);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(entry.Preparing, "튜토리얼은 성벽 위에서 시작");
            Assert.IsFalse(entry.MapOpen, "걷기 단계는 지도가 닫혀 있다");
            Assert.AreEqual(0f, entry.TutorialTimeScale, "읽는 동안 전투 시간 멈춤");
            // 고리가 주인공 위에 있어야 한다 (화면 좌표 80px 안). 고리 위치는 오버레이 캔버스라 RectTransform 월드 좌표 = 화면 화소
            RawImage walkRing = null;
            foreach (var image in Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None))
                if (image.name == "Ring" && image.gameObject.activeInHierarchy) walkRing = image;
            Assert.IsNotNull(walkRing, "걷기 단계 강조 고리");
            Vector3 heroView = entry.BattleCamera.WorldToViewportPoint(entry.Rig.HeroFeet + Vector3.up * HumanLastRampart.Combat.Battle.BattleTutorialSource.HeroCenterHeight);
            Vector3 heroScreen = new Vector3(heroView.x * Screen.width, heroView.y * Screen.height, 0f);
            Note($"고리 확인: 화면 {Screen.width}x{Screen.height}, 사진기 화소 {entry.BattleCamera.pixelWidth}x{entry.BattleCamera.pixelHeight}, 그림판 {(entry.BattleCamera.targetTexture != null ? entry.BattleCamera.targetTexture.width + "x" + entry.BattleCamera.targetTexture.height : "없음")}");
            {
                Camera bc = entry.BattleCamera;
                Vector3 feetView = bc.WorldToViewportPoint(entry.Rig.HeroFeet);
                Note($"고리 진단: 사진기 위치 {bc.transform.position} 회전 {bc.transform.eulerAngles} 시야 {bc.fieldOfView} 비율 {bc.aspect:0.000} 발 {entry.Rig.HeroFeet} 발뷰 {feetView} 가슴뷰 {heroView}");
                Note($"고리 진단: 앵커 {walkRing.rectTransform.anchorMin}~{walkRing.rectTransform.anchorMax} 위치 {walkRing.rectTransform.position} 로컬 {walkRing.rectTransform.anchoredPosition} 캔버스 {((RectTransform)walkRing.canvas.transform).rect} 배율 {walkRing.canvas.scaleFactor}");
            }
            Vector3 ringScreen = walkRing.rectTransform.position;
            Assert.Less(Vector2.Distance(heroScreen, ringScreen), 80f, $"고리가 주인공 위에 있음 (주인공 {heroScreen}, 고리 {ringScreen})");
            yield return Capture("tutorial_1_walk");

            // 1 걷기: 좌우 입력으로 실제로 걸어야 넘어간다
            var rig = entry.Rig;
            // 주인공은 실제 시간(unscaled)으로 걷는다 — 다른 시험처럼 실제 시간으로 최대 15초 기다린다
            rig.WalkInput = 1f;
            var walkWatch = System.Diagnostics.Stopwatch.StartNew();
            while (walkWatch.Elapsed.TotalSeconds < 15)
            {
                var d = Object.FindAnyObjectByType<TutorialDirector>();
                if (d == null || d.Finished || d.Current != TutorialStep.Walk) break;
                yield return null;
            }
            rig.WalkInput = 0f;
            Assert.AreNotEqual(TutorialStep.Walk, Object.FindAnyObjectByType<TutorialDirector>()?.Current, "15초 걸었는데 걷기 단계가 끝나지 않음");
            yield return WaitTutorialStep(TutorialStep.Telescope, 5);

            // 2 망원경(설명): [다음]
            yield return null;
            yield return Capture("tutorial_2_telescope");
            yield return Press("튜토리얼 다음", 60);
            yield return WaitTutorialStep(TutorialStep.Map, 10);

            // 3 지도: 열어야 넘어간다
            yield return null;
            yield return Capture("tutorial_3_map");
            entry.ToggleMap();
            yield return WaitTutorialStep(TutorialStep.Squad, 30);

            // 4 분대: 상자로 고르고 보낸다
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("tutorial_4_squad");
            int picked = entry.SelectGroupsInScreenRect(new Rect(0f, 0f, Screen.width, Screen.height));
            Assert.Greater(picked, 0, "보낼 분대");
            entry.SendSelectedGroups(2);
            yield return TutorialAdvance(TutorialStep.Squad, 120);
            yield return WaitTutorialStep(TutorialStep.Cannon, 5);

            // 5 대포(설명)
            yield return null;
            yield return Capture("tutorial_5_cannon");
            yield return Press("튜토리얼 다음", 60);
            yield return WaitTutorialStep(TutorialStep.Magic, 10);

            // 6 마법: 직접 한 번 써야 넘어간다
            yield return null;
            yield return Capture("tutorial_6_magic");
            entry.SelectSpell(0);
            Assert.IsTrue(entry.CastSelectedSpellAt(new Vector3(0f, 0f, 110f)), "마법 쓰기");
            yield return TutorialAdvance(TutorialStep.Magic, 120);
            Assert.IsTrue(Object.FindAnyObjectByType<TutorialDirector>().Finished, "전투 구간 끝");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1f, entry.TutorialTimeScale, "끝나면 시간이 정상");

            // 결과 → 정비
            yield return Press("[시험] 승리", 300);
            yield return WaitScreen(ScreenId.Result, 900);
            yield return Press("다음", 300);
            yield return WaitScreen(ScreenId.Prep, 900);

            // 7 함정: 직접 하나 놓아야 넘어간다
            yield return WaitTutorialStep(TutorialStep.Trap, 60);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("tutorial_7_trap");
            int traps0 = c.Traps.Count;
            var yard = Object.FindAnyObjectByType<HumanLastRampart.Meta.PrepYard>();
            Assert.IsTrue(yard.Use("함정 공방"), "함정 공방 장소");
            for (int i = 0; i < 3; i++) yield return null;
            var trapSession = new HumanLastRampart.Meta.TrapPreparationTransactions(c, GameFlow.Instance.Definitions);
            Vector2 trapPoint = FreeTrapTestSupport.Point(trapSession, "trap_bomb");
            var placer = Object.FindAnyObjectByType<HumanLastRampart.Meta.FreePlacementInput>();
            Assert.IsNotNull(placer, "함정 자유 배치 입력");
            placer.PlaceAt(FreeTrapTestSupport.MapPoint(trapSession, trapPoint));
            yield return null;
            yield return Press("한 번에 구매·설치", 60, true);
            Assert.AreEqual(traps0 + 1, c.Traps.Count, "함정 1개 설치");
            yield return Press("닫기", 60);
            yield return WaitTutorialStep(TutorialStep.Lens, 30);

            // 8 망원경 업그레이드(설명): [다음]으로 끝
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("tutorial_8_lens");
            yield return Press("튜토리얼 다음", 60);
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<TutorialDirector>().Finished, "정비 구간 끝");
            Assert.IsTrue(TutorialState.Completed, "튜토리얼 끝이 저장됨");
            Note("튜토리얼 8단계 통과");
        }
        finally
        {
            TutorialState.Suppressed = true;
            Time.captureDeltaTime = 0f;
            File.WriteAllText(Path.Combine(Out, "tutorial_steps.txt"), log.ToString(), new UTF8Encoding(false));
        }
    }
}
