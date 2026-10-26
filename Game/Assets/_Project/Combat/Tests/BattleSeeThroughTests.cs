using HumanLastRampart.Combat.Battle;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 주인공 둘레 투시 구멍 (W42): 가리는 것이 사진기와 주인공 사이에 있을 때만 켜지고, 재질이 투시 셰이더로 바뀌고, 끄기 옵션이 듣는다
    // (화면에 실제로 뚫리는지는 PlayProbe.SeeThrough가 사진 화소로 확인한다)
    public sealed class BattleSeeThroughTests
    {
        const float HeroHeight = 0.375f;
        GameObject cameraObject, cube;
        Camera camera;
        BattleSeeThrough seeThrough;

        [SetUp]
        public void SetUp()
        {
            BattleSeeThrough.Enabled = true;
            cameraObject = new GameObject("SeeThrough Test Camera", typeof(Camera));
            camera = cameraObject.GetComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);   // +z를 본다
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.position = new Vector3(0f, 1f, 3f);
            cube.transform.localScale = new Vector3(2f, 2f, 1f);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(lit, "URP Lit");
            cube.GetComponent<Renderer>().sharedMaterial = new Material(lit);
            seeThrough = new BattleSeeThrough(camera);
        }

        [TearDown]
        public void TearDown()
        {
            seeThrough.Release();
            BattleSeeThrough.Enabled = true;
            Object.DestroyImmediate(cube.GetComponent<Renderer>().sharedMaterial);
            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(cameraObject);
        }

        static Vector3 Feet => new Vector3(0f, 0.7f, 8f);

        [Test]
        public void Occluder_BetweenCameraAndHero_TurnsOnAndSwapsMaterialToSeeThroughShader()
        {
            seeThrough.Update(Feet, HeroHeight, 1f, true);
            Assert.IsTrue(seeThrough.Occluded, "큐브가 사진기와 주인공 사이");
            Assert.AreEqual(1f, seeThrough.Strength, 1e-4f);
            Assert.GreaterOrEqual(seeThrough.OccluderCount, 1);
            Assert.AreEqual("HLR/SeeThroughLit", cube.GetComponent<Renderer>().sharedMaterial.shader.name, "URP Lit이 같은 속성의 투시 셰이더로 바뀐다");

            Vector4 a = Shader.GetGlobalVector("_HlrSeeThroughA"), b = Shader.GetGlobalVector("_HlrSeeThroughB");
            Assert.AreEqual(1f, b.x, 1e-4f, "켜짐");
            Assert.AreEqual(Feet.y + BattleSeeThrough.FloorMargin, b.z, 1e-4f, "바닥 높이 이하는 안 뚫는다");
            float depth = Feet.z - camera.transform.position.z;   // 사진기가 정면을 보니 보기 깊이 = z 차이
            Assert.AreEqual(depth, a.w, 0.01f, "주인공 깊이");
            Assert.AreEqual(BattleSeeThrough.RadiusPerHeight * HeroHeight / depth, a.z, 1e-4f, "반지름 = 키×1.2 (탄젠트)");
        }

        [Test]
        public void Occluder_NotInTheWay_StaysOff()
        {
            cube.transform.position = new Vector3(6f, 1f, 3f);   // 옆으로 비킨다
            seeThrough.Update(Feet, HeroHeight, 1f, true);
            Assert.IsFalse(seeThrough.Occluded);
            Assert.AreEqual(0f, seeThrough.Strength, 1e-4f);
            Assert.AreEqual(0f, Shader.GetGlobalVector("_HlrSeeThroughB").x, 1e-4f, "아무것도 안 뚫는다");
        }

        [Test]
        public void Occluder_BehindTheHero_StaysOff()
        {
            cube.transform.position = new Vector3(0f, 1f, 11f);   // 주인공 뒤
            seeThrough.Update(Feet, HeroHeight, 1f, true);
            Assert.IsFalse(seeThrough.Occluded, "주인공 뒤의 것은 뚫지 않는다");
        }

        [Test]
        public void Fade_TakesAboutAFifthOfASecond_AndDisableOptionTurnsItOff()
        {
            seeThrough.Update(Feet, HeroHeight, 1f, false);   // 지도 화면 등: 끔
            Assert.AreEqual(0f, Shader.GetGlobalVector("_HlrSeeThroughB").x, 1e-4f);
            seeThrough.Update(Feet, HeroHeight, 0.05f, true);
            Assert.That(seeThrough.Strength, Is.InRange(0.2f, 0.5f), "0.15초에 걸쳐 켜진다 (0.05초면 약 1/3)");
            seeThrough.Update(Feet, HeroHeight, 0.2f, true);
            Assert.AreEqual(1f, seeThrough.Strength, 1e-4f);

            BattleSeeThrough.Enabled = false;
            seeThrough.Update(Feet, HeroHeight, 0.2f, true);
            Assert.AreEqual(0f, seeThrough.Strength, 1e-4f, "코드 옵션으로 끈다");
            Assert.AreEqual(0f, Shader.GetGlobalVector("_HlrSeeThroughB").x, 1e-4f);
        }

        [Test]
        public void TransparentMaterial_IsLeftAlone()
        {
            Material m = cube.GetComponent<Renderer>().sharedMaterial;
            m.SetFloat("_Surface", 1f);   // 투명 (빛무리 같은 것)
            seeThrough.Refresh();
            Assert.AreEqual("Universal Render Pipeline/Lit", m.shader.name);
            Assert.AreEqual(0, seeThrough.OccluderCount);
        }
    }
}
