using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator PrepYardDepthAndCollision()
    {
        Directory.CreateDirectory(Out); log = new StringBuilder();
        bool keep = KeepPreparation; KeepPreparation = true;
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
            Assert.IsNotNull(yard); Assert.AreEqual(6, yard.StationCount); Assert.AreEqual(7, yard.ObstacleCount);   // K-120 그림 속 건물 6 + 등대 탑
            for (int i = 0; i < 15; i++) yield return null;
            Assert.IsTrue(yard.CanMove, "마당이 열린 뒤 입력 허용");
            foreach (Vector2 pixel in new[] { new Vector2(1500, 990), new Vector2(1500, 600) })
            {
                Assert.IsTrue(yard.SetGround(PrepYardProjection.Ground(pixel)), "가까운/먼 바닥 이동");
                Vector3 world = new Vector3(yard.HeroGround.x, -1000f, yard.HeroGround.y);
                Vector3 p = yard.WorldCamera.WorldToViewportPoint(world);
                Assert.AreEqual(pixel.x, p.x * 3840f, 0.5f, "실제 3D 사진기 가로 투영 일치");
                Assert.AreEqual(pixel.y, (1f - p.y) * 1080f, 0.5f, "실제 3D 사진기 깊이 투영 일치");
                for (int i = 0; i < 3; i++) yield return null;
                yield return Capture(pixel.y > 900 ? "yard_00_near" : "yard_01_far", 1920, 1080);
                var rt = yard.WorldCamera.targetTexture;
                var previous = RenderTexture.active;
                var pixels = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                int shadowPixels = 0;
                try
                {
                    RenderTexture.active = rt;
                    pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    foreach (var color in pixels.GetPixels32()) if (color.a > 20 && color.a < 200) shadowPixels++;
                }
                finally { RenderTexture.active = previous; Object.Destroy(pixels); }
                Assert.Greater(shadowPixels, 50, "실제 바닥 그림자가 반투명 픽셀로 그려짐");
                Note($"깊이 {yard.HeroGround.y:0.00}m: 그림자 {shadowPixels}화소");
            }
            Assert.IsFalse(yard.SetGround(new Vector2(0, -1)), "앞쪽 깊이 경계");
            Assert.IsFalse(yard.SetGround(new Vector2(0, 9)), "뒤쪽 깊이 경계");
            Assert.IsFalse(yard.SetGround(PrepYardProjection.Ground(new Vector2(500, 900))), "공방 건물 안으로 진입 불가");
            Assert.IsTrue(yard.SetGround(PrepYardProjection.Ground(new Vector2(1400, 760))));
            Vector2 before = yard.HeroGround;
            Assert.IsTrue(yard.Move(Vector2.up, 0.2f));
            Assert.Greater(yard.HeroGround.y, before.y, "W 입력은 깊이 증가");
            Assert.IsTrue(yard.SetGround(PrepYardProjection.Ground(new Vector2(1400, 950))));
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("yard_02_foreground", 1920, 1080);
            // 각 장소로 실제 이동 가능한지 검사한다. 바로가기 순간이동 검사는 아래에서 별도로 한다.
            // 앞쪽 건물(공방·상점)은 문 앞 줄(y≈1040)로 먼저 내려가 돌아 간다 — 대각선으로 모서리를 곧장 향하면 둥근 모서리에 걸린다 (사람도 방향키를 나눠 누른다)
            foreach (var (place, spot, via) in new[] { ("성벽 수리", new Vector2(1095, 730), (Vector2?)null), ("병영", new Vector2(3045, 680), null), ("함정 공방", new Vector2(525, 1034), new Vector2(1000, 1045)),
                ("상점", new Vector2(3165, 1042), new Vector2(2700, 1045)), ("등대", new Vector2(1917, 830), null), ("성벽 계단", new Vector2(1755, 570), null) })
            {
                Assert.IsTrue(yard.SetGround(PrepYardProjection.Ground(new Vector2(1920, 880))));
                foreach (Vector2 stop in via.HasValue ? new[] { via.Value, spot } : new[] { spot })
                {
                    Vector2 goal = PrepYardProjection.Ground(stop);
                    int steps = 0;
                    while ((goal - yard.HeroGround).magnitude > 0.11f && steps++ < 900)
                        if (!yard.Move((goal - yard.HeroGround).normalized, 0.02f)) break;
                    Assert.Less((goal - yard.HeroGround).magnitude, 0.12f, $"실제 걷기 접근: {place} {stop} (발 {yard.HeroPixel})");
                }

                Assert.AreEqual(place, yard.Nearest()?.Name, "실제 걷기로 장소 도달: " + place);
            }            // 등대 탑(W9): 마당 한가운데 장애물, 곁에 서면 장소로 잡히고 창이 열리며 Esc로 닫힌다
            Assert.IsTrue(yard.SetGround(PrepYardProjection.Ground(new Vector2(1900, 830))), "등대 앞 바닥은 걸을 수 있다");
            Assert.AreEqual("등대", yard.Nearest()?.Name, "탑 곁에서 등대 장소");
            Assert.IsTrue(yard.SetGround(PrepYardProjection.Ground(new Vector2(1920, 1010))));
            for (int i = 0; i < 400; i++) yard.Move(Vector2.up, 0.02f);   // 탑을 향해 계속 걷는다
            Assert.Greater(yard.HeroPixel.y, 776f, "탑 몸체 앞에서 막힌다 (뒤로 지나가지 못한다)");
            yield return Capture("yard_04_beacon_tower", 1920, 1080);
            Assert.IsTrue(yard.Use("등대"));
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(yard.ModalOpen);
            yield return Capture("yard_05_beacon_panel", 1920, 1080);
            yard.CloseModal();
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsFalse(yard.ModalOpen);
            foreach (string place in new[] { "성벽 수리", "병영", "함정 공방", "상점" })
            {
                Assert.IsTrue(yard.Use(place), "도달 가능한 장소: " + place);
                for (int i = 0; i < 4; i++) yield return null;
                Vector2 held = yard.HeroGround;
                Assert.IsFalse(yard.Move(Vector2.right, 0.2f), "창 열린 동안 이동 중단");
                Assert.AreEqual(held, yard.HeroGround);
                yield return Press("닫기", 120);
                for (int i = 0; i < 6; i++) yield return null;
            }
            Assert.IsTrue(yard.Use("성벽 계단"));
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(yard.Move(Vector2.left, 0.2f), "출정 확인 중 이동 중단");
            yield return Capture("yard_03_stairs", 1920, 1080);
            yield return Press("더 둘러보기", 120);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(yard.Use("성벽 계단"));
            yield return Press("다음 전투 시작", 120);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsNull(GameObject.Find("Prep Yard 3D"), "다음 화면에서 3D 공간·카메라 정리");
            Note("실제 3D 바닥 투영, 앞뒤 이동, 충돌, 6장소, 팝업 입력 중단과 화면 종료 정리 통과");
        }
        finally
        {
            KeepPreparation = keep;
            File.WriteAllText(Path.Combine(Out, "prep_yard_depth.txt"), log.ToString());
        }
    }
}
