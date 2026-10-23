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
        public void DoorLantern_TakesSlotOnPcOnly_AndStillFitsSplit()
        {
            Vector3 lantern = new Vector3(0f, BattleSim.WallHeight + BattleLights.DoorLanternHeight, BattleTowerView.DoorZ);

            // PC: 탑이 1칸을 넣은 뒤 마당 빛이 문등 1칸 + 웅덩이 2개 -> 칸 나누기 전에 2칸 사용, 번쩍 8칸
            HlrLights.ForceLow = false;
            HlrLights.Begin();
            HlrLights.Add(new Vector3(0f, 10f, 0f), 20f, Color.white, 1f, 0f);
            BattleLights.EmitCourtyard(false, 1f, true, lantern, 1f);
            Assert.AreEqual(2, HlrLights.Count, "PC: 탑 + 문등");
            Assert.AreEqual(2, GroundGlow.Count, "웅덩이 둘 (탑 둘레·문 앞)");
            BattleLights.SplitSlots(HlrLights.Max, HlrLights.Count, true, out int torch, out int flash);
            Assert.AreEqual(4, torch);
            Assert.AreEqual(8, flash);
            GroundGlow.Flush(true, true);

            // 모바일: 문등은 칸을 안 쓰고 웅덩이만 -> 번쩍 3칸 유지, 마당 웅덩이 반지름은 줄어든다
            HlrLights.ForceLow = true;
            HlrLights.Begin();
            HlrLights.Add(new Vector3(0f, 10f, 0f), 20f, Color.white, 1f, 0f);
            BattleLights.EmitCourtyard(true, 1f, true, lantern, 1f);
            Assert.AreEqual(1, HlrLights.Count, "모바일: 탑만");
            Assert.AreEqual(2, GroundGlow.Count);
            BattleLights.SplitSlots(HlrLights.Max, HlrLights.Count, true, out torch, out flash);
            Assert.AreEqual(3, flash);
            Assert.Greater(BattleLights.CourtyardPoolRadius, BattleTowerView.BaseHalf * 3f, "마당 웅덩이는 탑 밑동보다 훨씬 넓다");
        }

        [Test]
        public void EmitCourtyard_LanternOff_AddsNoSlot()
        {
            HlrLights.ForceLow = false;
            HlrLights.Begin();
            BattleLights.EmitCourtyard(false, 1f, false, Vector3.zero, 0f);
            Assert.AreEqual(0, HlrLights.Count);
            Assert.AreEqual(2, GroundGlow.Count);
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
