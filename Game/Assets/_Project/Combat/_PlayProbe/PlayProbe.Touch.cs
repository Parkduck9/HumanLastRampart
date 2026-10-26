using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Meta;
using HumanLastRampart.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// W40 휴대전화 터치 조작 (K-125): 진짜 Touchscreen 장치에 손가락 입력을 넣어 이동 패드·상호작용·성벽 선택판·지도 끌어 고르기·정비 마당을 확인한다.
// 사진 1920×1080 (터치 층 강제 켬): HLR_PLAY_OUT/NN_touch_*.png, touch_probe.txt
public sealed partial class PlayProbe
{
    static Touchscreen touchDevice;
    static int touchSerial = 100;

    static Vector2 CenterOf(TouchWidget widget) => widget.Rt.TransformPoint(widget.Rt.rect.center);

    static void TouchBegin(int id, Vector2 p) => InputSystem.QueueStateEvent(touchDevice, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Began, position = p, pressure = 1f });
    static void TouchMove(int id, Vector2 p) => InputSystem.QueueStateEvent(touchDevice, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = p, pressure = 1f });
    static void TouchEnd(int id, Vector2 p) => InputSystem.QueueStateEvent(touchDevice, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = p });

    // 짧게 눌렀다 뗀다 (프레임 2칸씩 사이에 둔다)
    static IEnumerator TapAt(Vector2 p)
    {
        int id = ++touchSerial;
        TouchBegin(id, p);
        yield return null;
        yield return null;
        TouchEnd(id, p);
        yield return null;
        yield return null;
    }

    sealed class TouchTestScope
    {
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorBehavior;
        public bool Tutorial;

        public void Begin()
        {
            background = InputSystem.settings.backgroundBehavior;
            editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;   // 창에 초점이 없어도 가상 터치를 받는다
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Tutorial = HumanLastRampart.Tutorial.TutorialState.Suppressed;
            HumanLastRampart.Tutorial.TutorialState.Suppressed = true;
            TouchInput.ForceOn = false;
            TouchInput.SetModeInMemory(TouchMode.Auto);   // 저장된 설정은 건드리지 않는다
            TouchInput.ResetHistory();
            PointerInput.Reset();
            touchDevice = InputSystem.AddDevice<Touchscreen>();   // 터치 화면이 있어도 터치하기 전에는 층이 꺼져 있어야 한다
            Time.captureDeltaTime = 1f / 20f;
        }

        public void End()
        {
            TouchInput.ForceOn = false;
            TouchInput.ReloadMode();
            if (touchDevice != null) InputSystem.RemoveDevice(touchDevice);
            touchDevice = null;
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorBehavior;
            HumanLastRampart.Tutorial.TutorialState.Suppressed = Tutorial;
            Time.captureDeltaTime = 0f;
            PointerInput.Reset();
        }
    }

    [UnityTest]
    public IEnumerator TouchControlsBattle()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType, "시각 촬영에는 그래픽 장치가 필요");
        KeepPreparation = true;
        var scope = new TouchTestScope();
        scope.Begin();
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
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);

            var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            BattleSim sim = entry.Sim;
            BattleCameraRig rig = entry.Rig;
            BattleTouch touch = entry.Touch;
            Assert.IsNotNull(touch, "터치 조작이 만들어진다");
            BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
            var forecast = setup.Forecast; forecast.InitialWeather = Weather.Clear; forecast.TimeOfDay = TimeOfDay.Day; forecast.Changes = false; setup.Forecast = forecast;
            sim.WeatherDirty = true;
            for (int i = 0; i < 10; i++) yield return null;

            // 0) PC 기본: 터치 층은 보이지 않는다
            Assert.IsTrue(entry.Preparing && entry.MapOpen, "전투 준비 지도");
            Assert.IsFalse(TouchInput.Active, "터치하기 전·강제 없음 = 꺼짐");
            Assert.IsFalse(touch.Layer.Shown, "PC에서는 터치 층이 숨겨져 있다");
            TouchInput.ForceOn = true;
            for (int i = 0; i < 8; i++) yield return null;
            Assert.IsTrue(touch.Layer.Shown, "강제로 켜면 보인다");
            Assert.IsTrue(touch.BoxMode.Visible && touch.Confirm.Visible && touch.Cancel.Visible, "지도 단추");
            Assert.IsFalse(touch.Slider.Visible, "지도에는 이동 패드가 없다");
            yield return Capture("touch_00_map_prep", PfWidth, PfHeight);

            // 1) 지도: 끌어 고르기 모드 → 한 손가락 끌기로 상자 → 분대 고르기 → 취소 단추
            yield return TapAt(CenterOf(touch.BoxMode));
            Assert.IsTrue(entry.BoxSelectMode, "끌어 고르기 단추");
            float w = Screen.width, h = Screen.height;
            Vector2 a = new Vector2(w * 0.3f, h * 0.2f), b = new Vector2(w * 0.7f, h * 0.78f);
            int id = ++touchSerial;
            TouchBegin(id, a);
            yield return null;
            for (int i = 1; i <= 4; i++) { TouchMove(id, Vector2.Lerp(a, b, i / 4f)); yield return null; }
            yield return null;
            yield return Capture("touch_01_map_selecting", PfWidth, PfHeight);
            TouchEnd(id, b);
            yield return null;
            yield return null;
            yield return null;
            Assert.Greater(entry.SelectedGroupCount, 0, $"한 손가락 끌기로 상자 고르기 (분대 {entry.SelectedGroupCount})");
            Note($"끌어 고르기: 분대 {entry.SelectedGroupCount}개");
            yield return Capture("touch_02_map_selected", PfWidth, PfHeight);
            yield return TapAt(CenterOf(touch.Cancel));
            Assert.AreEqual(0, entry.SelectedGroupCount, "취소 단추가 고르기를 푼다");
            yield return TapAt(CenterOf(touch.BoxMode));
            Assert.IsFalse(entry.BoxSelectMode, "같은 단추를 다시 누르면 명령 모드");

            // 2) 길게 누르기: 고리 → 오른쪽 버튼(망원경)
            Vector2 pressAt = new Vector2(w * 0.5f, h * 0.15f);
            id = ++touchSerial;
            TouchBegin(id, pressAt);
            yield return new WaitForSecondsRealtime(0.32f);   // 눌림 시간은 실제 시간 (고리가 자라는 중)
            yield return null;
            Assert.Greater(PointerInput.RingProgress, 0f, "길게 누르는 동안 고리가 자란다");
            yield return Capture("touch_03_longpress_ring", PfWidth, PfHeight);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return null;
            Assert.IsTrue(PointerInput.SecondaryHeld, "0.5초 넘게 누르면 오른쪽 버튼");
            Assert.IsTrue(entry.TelescopeActive, "길게 누르는 동안 망원경 렌즈");
            TouchEnd(id, pressAt);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(entry.TelescopeActive, "떼면 렌즈 끝");

            // 3) 확인 단추 = 전투 시작
            yield return TapAt(CenterOf(touch.Confirm));
            Assert.IsFalse(entry.Preparing, "확인 단추로 전투 시작");
            for (int i = 0; i < 20; i++) yield return null;
            Assert.IsFalse(entry.MapOpen);
            Assert.IsTrue(touch.Slider.Visible && touch.Interact.Visible, "성벽 위 단추");
            Assert.IsFalse(touch.Compass.Visible);
            yield return Capture("touch_04_wall", PfWidth, PfHeight);

            // 4) 이동 패드: 오른쪽으로 달리기
            rig.ShowSide(0);
            for (int i = 0; i < 4; i++) yield return null;
            Vector2 c = CenterOf(touch.Slider);
            float s = touch.Slider.Rt.lossyScale.x;
            int side0 = rig.Side;
            float along0 = rig.Along;
            id = ++touchSerial;
            TouchBegin(id, c);
            yield return null;
            TouchMove(id, c + new Vector2(95f * s, 0f));
            yield return WaitFor(() => rig.Along - along0 > 2f, 6f);   // 달리기는 실제 시간
            Assert.IsTrue(touch.Slider.Held);
            Assert.Greater(touch.Slider.Value.x, 0.9f);
            Assert.AreEqual(side0, rig.Side);
            // 왼손으로 패드를 쥔 채 오른손으로 빠르기 단추 (손가락마다 따로 쥔다)
            var speedButton = (TouchButton)touch.Layer.Find("speed");
            int speed0 = entry.Speed;
            yield return TapAt(CenterOf(speedButton));
            Assert.AreNotEqual(speed0, entry.Speed, "다른 손가락의 단추도 눌린다");
            Assert.IsTrue(touch.Slider.Held, "패드는 그대로 쥐고 있다");
            while (entry.Speed != speed0) entry.CycleSpeed();
            Assert.Greater(rig.Along - along0, 2f, $"패드를 오른쪽으로 밀면 달린다 (along {along0:0.0} → {rig.Along:0.0})");
            TouchEnd(id, c);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(0f, rig.WalkInput, "뗐으면 멈춘다");
            Note($"이동 패드 달리기: along {along0:0.0} → {rig.Along:0.0}");

            // 5) 이동 패드: 아래로 밀어 통로 안쪽(계단)까지
            rig.ShowSide(0);
            for (int i = 0; i < 4; i++) yield return null;
            id = ++touchSerial;
            TouchBegin(id, c);
            yield return null;
            TouchMove(id, c + new Vector2(0f, -30f * s));
            yield return WaitFor(() => rig.NearStairs, 8f);
            Assert.IsTrue(rig.NearStairs, "패드를 아래로 밀면 통로 안쪽 계단에 닿는다");
            TouchEnd(id, c);
            for (int i = 0; i < 6; i++) yield return null;
            Assert.AreEqual("glyph_stairs_down", touch.Interact.GlyphName, "계단 가까이서는 큰 단추가 계단 기호");
            Assert.IsTrue(touch.Interact.Enabled && touch.Interact.Highlight);
            yield return Capture("touch_05_near_stairs", PfWidth, PfHeight);

            // 6) 상호작용 단추 → 계단 내려가기 → 뒤돌기 → 다시 내려가기(계단 단추) → 탑 도착
            yield return TapAt(CenterOf(touch.Interact));
            Assert.IsTrue(rig.TowerTarget && rig.InTransition, "상호작용 단추로 계단을 내려간다");
            for (int i = 0; i < 6; i++) yield return null;
            Assert.IsTrue(touch.TurnBack.Visible && !touch.Compass.Visible, "내려가는 중에는 뒤돌기 단추");
            yield return Capture("touch_06_descending", PfWidth, PfHeight);
            yield return TapAt(CenterOf(touch.TurnBack));
            Assert.IsFalse(rig.TowerTarget, "뒤돌기 단추: 올라간다");
            yield return WaitFor(() => rig.OnWall, 20f);
            Assert.IsTrue(rig.OnWall && rig.NearStairs);
            for (int i = 0; i < 6; i++) yield return null;
            yield return TapAt(CenterOf(touch.Interact));
            Assert.IsTrue(rig.TowerTarget, "다시 상호작용 단추로 내려간다");
            yield return WaitFor(() => rig.AtTower, 40f);
            Assert.IsTrue(rig.AtTower, "탑 앞 도착");
            for (int i = 0; i < 20; i++) yield return null;
            entry.TutorialTimeScale = 0f;
            yield return new WaitForSecondsRealtime(1.2f);
            entry.TutorialTimeScale = 1f;
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(touch.Compass.Visible && touch.Beam.Visible && touch.StairsUp.Visible && !touch.Slider.Visible, "탑 앞 단추");
            yield return Capture("touch_07_tower", PfWidth, PfHeight);

            // 7) 성벽 선택판: 서쪽 조각 → 서쪽 성벽 가운데로 올라간다
            float cs = touch.Compass.Rt.lossyScale.x;
            yield return TapAt(CenterOf(touch.Compass) + new Vector2(-62f * cs, 0f));
            yield return WaitFor(() => rig.OnWall, 40f);
            Assert.IsTrue(rig.OnWall, "성벽으로 올라왔다");
            Assert.AreEqual(3, rig.Side, "성벽 선택판 서쪽 = 서쪽 성벽");
            Assert.AreEqual(0f, rig.Along, 1e-3f);
            for (int i = 0; i < 20; i++) yield return null;
            yield return Capture("touch_08_after_compass", PfWidth, PfHeight);

            // 도움말: 열리면 터치 층이 꺼지고(판 위로 새지 않게), 판 안 닫기 단추로 닫는다
            yield return TapAt(CenterOf(touch.Help));
            for (int i = 0; i < 6; i++) yield return null;
            Assert.IsTrue(entry.TouchBlocked && !touch.Layer.Shown, "도움말이 열린 동안 터치 층은 꺼진다");
            yield return Capture("touch_11_help", PfWidth, PfHeight);
            yield return Press("도움말 닫기", 120);
            for (int i = 0; i < 8; i++) yield return null;
            Assert.IsFalse(entry.TouchBlocked);
            Assert.IsTrue(touch.Layer.Shown, "닫으면 터치 층이 다시 켜진다");

            // 8) 지도 단추 → 지도 (분대 상자 고르기는 위 1에서 확인했다)
            yield return TapAt(CenterOf(touch.WallMap));
            for (int i = 0; i < 6; i++) yield return null;
            Assert.IsTrue(entry.MapOpen, "지도 단추");
            yield return TapAt(CenterOf(touch.Confirm));
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsFalse(entry.MapOpen, "확인 단추로 지도 접기");
            Note("터치: 지도 상자 고르기·길게 누르기·이동 패드·계단·뒤돌기·성벽 선택판 통과");
        }
        finally
        {
            scope.End();
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "touch_probe.txt"), log.ToString(), new UTF8Encoding(false));
        }
    }

    [UnityTest]
    public IEnumerator TouchControlsPrepYard()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        bool keep = KeepPreparation;
        KeepPreparation = true;
        var scope = new TouchTestScope();
        scope.Begin();
        try
        {
            SceneManager.LoadScene("Boot"); yield return null; yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600); yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>().Skip();
            yield return WaitScreen(ScreenId.Battle, 900);
            yield return Press("[시험] 승리", 300);
            yield return WaitScreen(ScreenId.Result, 900); yield return Press("다음", 300);
            yield return WaitScreen(ScreenId.Prep, 900);
            var yard = Object.FindAnyObjectByType<PrepYard>();
            Assert.IsNotNull(yard);
            for (int i = 0; i < 15; i++) yield return null;
            Assert.IsNull(yard.GetComponent<PrepYardTouch>(), "PC에서는 터치 층을 만들지 않는다");
            TouchInput.ForceOn = true;
            for (int i = 0; i < 8; i++) yield return null;
            var touch = yard.GetComponent<PrepYardTouch>();
            Assert.IsNotNull(touch, "터치가 켜지면 정비 마당 터치 층");
            Assert.IsTrue(touch.Layer.Shown);
            yield return Capture("touch_09_prep_yard", PfWidth, PfHeight);

            // 이동 패드로 걷기 (오른쪽)
            Vector2 before = yard.HeroGround;
            Vector2 c = CenterOf(touch.Slider);
            float s = touch.Slider.Rt.lossyScale.x;
            int id = ++touchSerial;
            TouchBegin(id, c);
            yield return null;
            TouchMove(id, c + new Vector2(95f * s, 0f));
            yield return WaitFor(() => yard.HeroGround.x > before.x + 0.5f, 5f);
            TouchEnd(id, c);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.Greater(yard.HeroGround.x, before.x + 0.5f, "패드를 오른쪽으로 밀면 마당에서 걷는다");
            Note($"정비 마당 터치 걷기: x {before.x:0.00} → {yard.HeroGround.x:0.00}");

            // 장소 앞에서 상호작용 단추
            Assert.IsFalse(touch.Interact.Enabled && yard.Nearest() == null, "장소가 없으면 꺼짐");
            Assert.IsTrue(yard.SetGround(PrepYardProjection.Ground(new Vector2(1900, 830))), "등대 앞");
            for (int i = 0; i < 4; i++) yield return null;
            Assert.AreEqual("등대", yard.Nearest()?.Name);
            Assert.IsTrue(touch.Interact.Enabled && touch.Interact.Highlight, "장소 앞에서 상호작용 단추가 켜진다");
            yield return Capture("touch_10_prep_station", PfWidth, PfHeight);
            yield return TapAt(CenterOf(touch.Interact));
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(yard.ModalOpen, "상호작용 단추로 등대 창");
            yard.CloseModal();
            for (int i = 0; i < 4; i++) yield return null;
            Note("정비 마당 터치: 이동 패드·상호작용 통과");
        }
        finally
        {
            scope.End();
            KeepPreparation = keep;
            File.WriteAllText(Path.Combine(Out, "touch_prep_probe.txt"), log.ToString(), new UTF8Encoding(false));
        }
    }
}

