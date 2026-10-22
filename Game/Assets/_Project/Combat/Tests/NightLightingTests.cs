using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Combat.Render;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 밤 빛 (W15): 셰이더 빛 목록 상한·칸 나누기, 땅 웅덩이 상한, 새 셰이더 세 개가 오류 없이 컴파일되는지
    public sealed class NightLightingTests
    {
        [TearDown]
        public void Restore()
        {
            HlrLights.ForceLow = false;
            HlrLights.Begin();
            HlrLights.Clear();
            GroundGlow.Flush(true, true);   // 남은 웅덩이 비우기
        }

        [Test]
        public void LightList_CapsAre14OnPcAnd6OnMobile()
        {
            HlrLights.ForceLow = false;
            HlrLights.Begin();
            Assert.AreEqual(14, HlrLights.Max);
            int added = 0;
            for (int i = 0; i < 30; i++) if (HlrLights.Add(new Vector3(i, 1f, 0f), 5f, Color.white, 1f, 0f)) added++;
            Assert.AreEqual(14, added);
            Assert.AreEqual(14, HlrLights.Count);

            HlrLights.ForceLow = true;
            HlrLights.Begin();
            Assert.AreEqual(6, HlrLights.Max);
            added = 0;
            for (int i = 0; i < 30; i++) if (HlrLights.Add(new Vector3(i, 1f, 0f), 5f, Color.white, 1f, 0f)) added++;
            Assert.AreEqual(6, added);
        }

        [Test]
        public void SlotSplit_TowerThenTorchesThenFlashes()
        {
            // PC: 탑 1 + 횃불 4 + 번쩍 9 = 14
            BattleLights.SplitSlots(14, 1, true, out int torch, out int flash);
            Assert.AreEqual(4, torch);
            Assert.AreEqual(9, flash);
            Assert.AreEqual(14, 1 + torch + flash);

            // 모바일: 탑 1 + 횃불 2 + 번쩍 3 = 6
            BattleLights.SplitSlots(6, 1, true, out torch, out flash);
            Assert.AreEqual(2, torch);
            Assert.AreEqual(3, flash);
            Assert.AreEqual(6, 1 + torch + flash);

            // 탑이 없으면 번쩍이 한 칸 늘고, 횃불이 꺼져 있으면 횃불 몫이 없다
            BattleLights.SplitSlots(6, 0, true, out torch, out flash);
            Assert.AreEqual(4, flash);
            BattleLights.SplitSlots(6, 1, false, out torch, out flash);
            Assert.AreEqual(0, torch);
            Assert.AreEqual(5, flash);
        }

        [Test]
        public void TowerLight_OnMobileFlagDefaultsOn()
        {
            Assert.IsTrue(NightTuning.TowerLightOnMobile, "사용자 결정 2026-10-22: 휴대폰에서도 탑 불빛을 켠다");
            Assert.IsTrue(NightTuning.TowerLightAllowed);
        }

        [Test]
        public void GroundGlow_CapsAre96OnPcAnd24OnMobile()
        {
            HlrLights.ForceLow = false;
            HlrLights.Begin();   // 상한을 다시 정한다
            Assert.AreEqual(96, GroundGlow.Max);
            for (int i = 0; i < 200; i++) GroundGlow.Add(new Vector3(i, 0f, 0f), 2f, Color.white, 0.5f);
            Assert.AreEqual(96, GroundGlow.Count);
            GroundGlow.Flush(true, true);
            Assert.AreEqual(0, GroundGlow.Count);

            HlrLights.ForceLow = true;
            HlrLights.Begin();
            Assert.AreEqual(24, GroundGlow.Max);
            for (int i = 0; i < 200; i++) GroundGlow.Add(new Vector3(i, 0f, 0f), 2f, Color.white, 0.5f);
            Assert.AreEqual(24, GroundGlow.Count);
        }

        [TestCase("FieldGround")]
        [TestCase("GroundGlow")]
        [TestCase("BeamCone")]
        public void NewShaders_LoadAndCompileWithoutErrors(string name)
        {
            Shader shader = Resources.Load<Shader>(name);
            Assert.IsNotNull(shader, name + " 셰이더를 불러오지 못함");
            Assert.IsTrue(shader.isSupported, name + " 이 이 기기에서 지원되지 않음");
            int count = ShaderUtil.GetShaderMessageCount(shader);
            for (int i = 0; i < count; i++)
            {
                var message = ShaderUtil.GetShaderMessages(shader)[i];
                Assert.AreNotEqual(UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, message.severity, name + " 컴파일 오류: " + message.message);
            }
        }

        [Test]
        public void FieldGround_HasDepthNormalsPass()
        {
            Shader shader = Resources.Load<Shader>("FieldGround");
            Assert.IsNotNull(shader);
            var material = new Material(shader);
            try
            {
                Assert.GreaterOrEqual(material.FindPass("DepthNormals"), 0, "화면 공간 차폐 그림자용 깊이·노멀 패스");
                Assert.GreaterOrEqual(material.FindPass("DepthOnly"), 0);
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
