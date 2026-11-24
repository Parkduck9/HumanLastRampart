using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// K-086: 검토 그림을 메모리에만 올려 실제 전투 사진기에서 여섯 칸을 정지 촬영한다.
public sealed partial class PlayProbe
{
    const BindingFlags IgniteProbeFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    static FieldInfo IgniteField(System.Type type, string name)
    {
        var field = type.GetField(name, IgniteProbeFlags);
        Assert.IsNotNull(field, "점화 확인 필드: " + name);
        return field;
    }

    static Texture2D IgniteTexture(string path, string name)
    {
        Assert.IsTrue(File.Exists(path), "검토 그림이 먼저 완성되어야 한다: " + path);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(File.ReadAllBytes(path)))
        {
            Object.Destroy(texture);
            Assert.Fail("검토 PNG 읽기 실패: " + path);
        }
        texture.name = name;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    // 현재 두 그리기는 화면 평면 빌보드다. 셰이더의 발·칸·배율을 읽어 같은 점을 투영한다.
    static Vector2 IgniteScreenPoint(Camera camera, Material material, MaterialPropertyBlock block,
        PerfLabInstance instance, Vector2 pixel, Vector2 cell)
    {
        Assert.Greater(material.GetFloat("_ScreenUpright"), 0.5f, "화면 평면 빌보드 설정");
        Assert.Less(block.GetVector("_FixedRight").w, 0.5f, "고정 축을 쓰지 않는 점화·대포 그리기");
        float scale = block.GetFloat("_SheetScale");
        if (scale <= 0f) scale = material.GetFloat("_SheetScale");
        float size = instance.TintSize.w * scale;
        Vector4 pivot = block.GetVector("_Pivot");
        float x = (pixel.x / cell.x - pivot.x) * block.GetFloat("_Aspect") * size;
        float y = (1f - pixel.y / cell.y - pivot.y) * size;
        var origin = new Vector3(instance.PosFrame.x, instance.PosFrame.y, instance.PosFrame.z);
        Vector3 point = camera.WorldToViewportPoint(origin + camera.transform.right * x + camera.transform.up * y);
        Assert.Greater(point.z, 0f, "접점이 사진기 앞에 있다");
        return new Vector2(point.x * 1920f, point.y * 1080f);
    }

    void AssertIgniteContactProjection(Camera camera, Material material, MaterialPropertyBlock crewBlock,
        MaterialPropertyBlock cannonBlock, BattleSim sim, int soldier, BattlePropView props, string label)
    {
        var cannonInstances = (PerfLabInstance[])IgniteField(typeof(BattlePropView), "cannonInstances").GetValue(props);
        PerfLabInstance cannon = cannonInstances[0];
        int frame = (int)cannon.PosFrame.w;
        float recoilY = frame == 1 ? 8f : frame == 2 ? 4f : 0f;
        float aspect = camera.aspect;
        camera.aspect = 1920f / 1080f;
        try
        {
            // v5 실제 밝은 불꽃의 연결 성분 경계(native7,54~19,69)를 게임3배 좌표로 읽는다.
            Vector2 a = IgniteScreenPoint(camera, material, crewBlock, sim.SoldierInstances[soldier],
                new Vector2(7f * 3f, 54f * 3f), new Vector2(432f, 384f));
            Vector2 b = IgniteScreenPoint(camera, material, crewBlock, sim.SoldierInstances[soldier],
                new Vector2(19f * 3f, 69f * 3f), new Vector2(432f, 384f));
            var bounds = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            Vector2 ring = IgniteScreenPoint(camera, material, cannonBlock, cannon,
                new Vector2(128f, 134f + recoilY), new Vector2(256f, 256f));
            Vector2 tip = IgniteScreenPoint(camera, material, cannonBlock, cannon,
                new Vector2(134f, 124f + recoilY), new Vector2(256f, 256f));
            Note($"{label}: 불꽃 화면범위 {bounds}, 고리 {ring}, 심지끝 {tip}, 대포칸 {frame}");
            Assert.IsTrue(bounds.Contains(ring), label + " 고리가 밝은 불꽃 범위 안");
            Assert.IsTrue(bounds.Contains(tip), label + " 심지 끝이 밝은 불꽃 범위 안");
        }
        finally { camera.aspect = aspect; }
    }

    [UnityTest]
    public IEnumerator IgniteContactSixFrames()
        => IgniteContactFrames(false);

    [UnityTest]
    public IEnumerator InstalledIgniteContactSixFrames()
        => IgniteContactFrames(true);

    IEnumerator IgniteContactFrames(bool installed)
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Texture2D ignite = null, cannon = null, originalSheet = null;
        UnitDef unit = null;
        BattleEntry entry = null;
        BattleCameraRig rig = null;
        MaterialPropertyBlock cannonBlock = null;
        Texture originalCannon = null;
        NativeArray<float> fuse = default;
        int soldier = -1, oldSide = 0, oldPreset = -1;
        float oldFuse = 0f, oldAlong = 0f, oldCaptureDelta = Time.captureDeltaTime;
        bool oldPaused = false, oldMap = true, stateSaved = false;
        bool oldKeepPreparation = KeepPreparation;
        KeepPreparation = true;
        try
        {
            Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
                "실제 카메라 검토에는 그래픽 장치가 필요하다");
            var source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/References/codex_k086_v5/game"));
            if (!installed)
            {
            ignite = IgniteTexture(Path.Combine(source, "cannon_crew_ignite6.png"), HumanLastRampart.Combat.PerfLab.CannonArt.IgniteSheetName);
            cannon = IgniteTexture(Path.Combine(source, "cannon_recoil_fuse.png"), "cannon_recoil_fuse_probe");
            Assert.AreEqual(2592, ignite.width);
            Assert.AreEqual(384, ignite.height);
            Assert.AreEqual(768, cannon.width, "기존 반동 대포 256칸 세 개와 같은 규격");
            Assert.AreEqual(256, cannon.height);
            var layout = PerfLabArt.FromSheet(ignite);
            Assert.AreEqual(6, PerfLabArt.FrameCount(ignite));
            Assert.AreEqual(2f / 3f, layout.Pivot.x, 1e-6f);
            Assert.AreEqual(1f / 16f, layout.Pivot.y, 1e-6f);
            }

            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600);
            yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            unit = GameFlow.Instance.Definitions.All<UnitDef>().FirstOrDefault(u => u.Kind == UnitKind.CannonCrew);
            Assert.IsNotNull(unit, "대포병 정의");
            originalSheet = unit.IgniteSheet;
            if (installed)
            {
                ignite = originalSheet;
                Assert.IsNotNull(ignite, "설치된 대포병 점화 에셋");
                Assert.AreEqual(2592, ignite.width);
                Assert.AreEqual(384, ignite.height);
                Assert.AreEqual(6, PerfLabArt.FrameCount(ignite));
            }
            else IgniteField(typeof(UnitDef), "igniteSheet").SetValue(unit, ignite);
            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            Assert.IsNotNull(story);
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);
            entry = Object.FindAnyObjectByType<BattleEntry>();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Preparing, "실제 전투 진행 전 준비 상태");
            var sim = (BattleSim)IgniteField(typeof(BattleEntry), "sim").GetValue(entry);
            var view = (BattleView)IgniteField(typeof(BattleEntry), "view").GetValue(entry);
            rig = (BattleCameraRig)IgniteField(typeof(BattleEntry), "rig").GetValue(entry);
            sim.Complete();
            Assert.Greater(sim.CannonCount, 0);
            var cannons = (List<(int soldier, int side, int number, string postId)>)
                IgniteField(typeof(BattleSim), "cannons").GetValue(sim);
            soldier = cannons[0].soldier;
            Assert.IsTrue(sim.SoldierGroups.Any(g => g.unit == unit && soldier >= g.start && soldier < g.start + g.count),
                "선택한 대포의 사격병이 교체한 대포병 정의를 사용한다");
            var poseDraws = (List<(MaterialPropertyBlock block, int count)>)
                IgniteField(typeof(BattleView), "soldierPoseDraws").GetValue(view);
            Assert.IsTrue(poseDraws.Any(d => d.count > 0 && d.block.GetTexture("_MainTex") == ignite),
                "실제 동작 그리기 블록에 새 점화 텍스처가 연결되었다");
            var crewBlock = poseDraws.First(d => d.count > 0 && d.block.GetTexture("_MainTex") == ignite).block;
            var material = (Material)IgniteField(typeof(BattleView), "material").GetValue(view);
            fuse = (NativeArray<float>)IgniteField(typeof(BattleSim), "sFuse").GetValue(sim);
            oldFuse = fuse[soldier];
            oldPaused = (bool)IgniteField(typeof(BattleEntry), "paused").GetValue(entry);
            oldMap = entry.MapOpen;
            oldSide = rig.Side;
            oldAlong = rig.Along;
            oldPreset = rig.PresetIndex;
            stateSaved = true;
            IgniteField(typeof(BattleEntry), "paused").SetValue(entry, true);
            if (entry.MapOpen) entry.ToggleMap();
            var info = sim.GetCannon(0);
            rig.ShowSide(info.Side);
            IgniteField(typeof(BattleCameraRig), "along").SetValue(rig,
                math.dot(new float3(info.Position.x, info.Position.y, info.Position.z), WallGeometry.Right(info.Side)));
            rig.SetPreset(0); // 실제 A 시점의 거리·높이·각도·40도 화각을 유지한다.
            var props = (BattlePropView)IgniteField(typeof(BattleView), "props").GetValue(view);
            cannonBlock = (MaterialPropertyBlock)IgniteField(typeof(BattlePropView), "cannonBlock").GetValue(props);
            Assert.IsNotNull(cannonBlock);
            originalCannon = cannonBlock.GetTexture("_MainTex");
            if (installed)
            {
                cannon = originalCannon as Texture2D;
                Assert.IsNotNull(cannon, "설치된 대포 그림");
                Assert.AreEqual(768, cannon.width);
                Assert.AreEqual(256, cannon.height);
            }
            else cannonBlock.SetTexture("_MainTex", cannon);
            float elapsed = sim.Elapsed;
            var camera = (Camera)IgniteField(typeof(BattleEntry), "battleCamera").GetValue(entry);
            Note($"{(installed ? "게임 설치 에셋 그대로" : "검토용 메모리 교체")}: {ignite.width}×{ignite.height}, 432×384 여섯 칸, 발 288·360");
            Note($"실제 A 사진기: 위치 {camera.transform.position}, 각도 {camera.transform.eulerAngles}, 화각 {camera.fieldOfView}");
            Note("시점 변경: 주인공의 성벽 방향·가로 자리만 첫 대포로 옮김. 거리·높이·각도·화각 확대 없음.");
            for (int frame = 0; frame < 6; frame++)
            {
                fuse[soldier] = BattleSim.CannonIgniteTotal - (frame + 0.05f) / BattleSim.IgniteFps;
                sim.RefreshSoldierInstances();
                Assert.AreEqual(PerfLabArt.IgniteFrame0 + frame, sim.SoldierInstances[soldier].PosFrame.w,
                    "실제 병사 그리기 버퍼 칸 " + (frame + 1));
                Assert.Greater(sim.SoldierInstances[soldier].TintSize.w, 0f, "점화병이 그려지는 상태");
                yield return null;
                yield return Capture("ignite_contact_frame_" + (frame + 1), 1920, 1080, true);
                string imagePath = Path.Combine(Out, $"{shot:00}_ignite_contact_frame_{frame + 1}.png");
                Assert.IsTrue(File.Exists(imagePath), "실제 카메라 사진 저장: " + imagePath);
                Assert.Greater(new FileInfo(imagePath).Length, 1024, "빈 PNG 파일이 아니다");
                Assert.AreEqual(elapsed, sim.Elapsed, "사진 촬영 중 전투 시간 정지");
                Assert.AreSame(cannon, cannonBlock.GetTexture("_MainTex"));
                Note($"{frame + 1}번 칸: 실제 점화 구간 {sim.SoldierInstances[soldier].PosFrame.w}, 전투 시간 {sim.Elapsed}");
                if (frame == 3)
                    AssertIgniteContactProjection(camera, material, crewBlock, cannonBlock, sim, soldier, props, "A 접촉칸");
            }
            fuse[soldier] = BattleSim.CannonIgniteTotal - 3.05f / BattleSim.IgniteFps;
            sim.RefreshSoldierInstances();
            for (int preset = 1; preset < 3; preset++)
            {
                rig.SetPreset(preset);
                for (int settle = 0; settle < 4; settle++) yield return null;
                yield return Capture("ignite_contact_preset_" + preset, 1920, 1080, true);
                AssertIgniteContactProjection(camera, material, crewBlock, cannonBlock, sim, soldier, props,
                    "사진기 " + BattleCameraRig.Presets[preset].Name);
            }
            Note("검사: 실제 그리기 배치의 A/B/C 접점 투영 범위 포함. 사진으로 가림·자연스러움은 별도 확인한다.");
        }
        finally
        {
            if (cannonBlock != null) cannonBlock.SetTexture("_MainTex", originalCannon);
            if (stateSaved && entry != null)
            {
                if (fuse.IsCreated && soldier >= 0) fuse[soldier] = oldFuse;
                if (entry.MapOpen != oldMap) entry.ToggleMap();
                if (rig != null && !oldMap)
                {
                    rig.ShowSide(oldSide);
                    IgniteField(typeof(BattleCameraRig), "along").SetValue(rig, oldAlong);
                    rig.SetPreset(oldPreset);
                }
                IgniteField(typeof(BattleEntry), "paused").SetValue(entry, oldPaused);
            }
            if (unit != null) IgniteField(typeof(UnitDef), "igniteSheet").SetValue(unit, originalSheet);
            // 전투 뷰의 참조가 사라진 뒤 임시 텍스처를 해제한다. 원본 에셋에는 저장하지 않는다.
            if (entry != null) SceneManager.LoadScene("Boot");
            if (!installed && ignite != null) Object.Destroy(ignite);
            if (!installed && cannon != null) Object.Destroy(cannon);
            Time.captureDeltaTime = oldCaptureDelta;
            KeepPreparation = oldKeepPreparation;
            File.WriteAllText(Path.Combine(Out, "ignite_contact_six_frames.txt"), log.ToString(), new UTF8Encoding(false));
        }
    }
}
