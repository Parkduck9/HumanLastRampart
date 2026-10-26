using System.Collections.Generic;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 등대 일꾼 K-119 시트 (W31): 시트 파일·방향 고르기·상태마다 맞는 시트
    public sealed class BattleWorkerSheetTests
    {
        static readonly Vector3 CamRight = Vector3.right, CamToward = Vector3.back;   // 사진기가 북쪽을 본다 (사진기 쪽 = 남쪽)

        [Test]
        public void Sheets_Exist_ForEveryProvidedMotionAndDirection()
        {
            int found = 0;
            for (int v = 0; v < 2; v++)
                for (int m = 0; m < 5; m++)
                    for (int d = 0; d < 4; d++)
                    {
                        var motion = (BattleBeaconView.WorkMotion)m; var dir = (BattleBeaconView.WorkDir)d;
                        if (!BattleBeaconView.SheetProvided(motion, dir)) continue;
                        string name = BattleBeaconView.SheetName(v, motion, dir);
                        var tex = Resources.Load<Texture2D>("BattleArt/Beacon/" + name);
                        Assert.IsNotNull(tex, name);
                        int frames = motion == BattleBeaconView.WorkMotion.Walk || motion == BattleBeaconView.WorkMotion.Carry ? 8 : 6;
                        Assert.AreEqual(frames * 96, tex.width, name + " 폭 = 칸 수 × 96");
                        Assert.AreEqual(128, tex.height, name);
                        Assert.AreEqual(FilterMode.Point, tex.filterMode, name);
                        Assert.AreEqual(TextureWrapMode.Clamp, tex.wrapMode, name);
                        found++;
                    }

            Assert.AreEqual(34, found, "K-119 시트 34개 (외형 둘 × 걷기 4 + 운반 4 + 오르기 3 + 내려오기 3 + 화로 3)");
        }

        [Test]
        public void SheetName_FollowsK119Naming()
        {
            Assert.AreEqual("worker_a_walk_front", BattleBeaconView.SheetName(0, BattleBeaconView.WorkMotion.Walk, BattleBeaconView.WorkDir.Front));
            Assert.AreEqual("worker_b_climb_up_back", BattleBeaconView.SheetName(1, BattleBeaconView.WorkMotion.ClimbUp, BattleBeaconView.WorkDir.Back));
            Assert.AreEqual("worker_b_climb_down_left", BattleBeaconView.SheetName(1, BattleBeaconView.WorkMotion.ClimbDown, BattleBeaconView.WorkDir.Left));
            Assert.AreEqual("worker_a_tend_right", BattleBeaconView.SheetName(0, BattleBeaconView.WorkMotion.Tend, BattleBeaconView.WorkDir.Right));
            Assert.AreEqual("worker_a_carry_back", BattleBeaconView.SheetName(0, BattleBeaconView.WorkMotion.Carry, BattleBeaconView.WorkDir.Back));
        }

        [Test]
        public void PickDirection_FollowsCameraAxes()
        {
            var walk = BattleBeaconView.WorkMotion.Walk;
            Assert.AreEqual(BattleBeaconView.WorkDir.Right, BattleBeaconView.PickDirection(Vector3.right, CamRight, CamToward, BattleBeaconView.WorkDir.Front, walk));
            Assert.AreEqual(BattleBeaconView.WorkDir.Left, BattleBeaconView.PickDirection(Vector3.left, CamRight, CamToward, BattleBeaconView.WorkDir.Front, walk));
            Assert.AreEqual(BattleBeaconView.WorkDir.Front, BattleBeaconView.PickDirection(Vector3.back, CamRight, CamToward, BattleBeaconView.WorkDir.Right, walk), "사진기 쪽으로 오면 앞모습");
            Assert.AreEqual(BattleBeaconView.WorkDir.Back, BattleBeaconView.PickDirection(Vector3.forward, CamRight, CamToward, BattleBeaconView.WorkDir.Right, walk), "멀어지면 뒷모습");
            // 사진기를 돌려도 같다: 사진기가 동쪽을 본다 (오른쪽 = 남쪽, 사진기 쪽 = 서쪽)
            Assert.AreEqual(BattleBeaconView.WorkDir.Front, BattleBeaconView.PickDirection(Vector3.left, Vector3.back, Vector3.left, BattleBeaconView.WorkDir.Right, walk));
        }

        [Test]
        public void PickDirection_NeverPicksMissingSheets()
        {
            var up = BattleBeaconView.WorkMotion.ClimbUp;
            var down = BattleBeaconView.WorkMotion.ClimbDown;
            var tend = BattleBeaconView.WorkMotion.Tend;
            // 계단은 앞모습이 없다 → 사진기 쪽으로 오르내려도 옆모습
            var d1 = BattleBeaconView.PickDirection(Vector3.back + Vector3.right * 0.1f, CamRight, CamToward, BattleBeaconView.WorkDir.Back, up);
            Assert.AreEqual(BattleBeaconView.WorkDir.Right, d1);
            Assert.AreEqual(BattleBeaconView.WorkDir.Left, BattleBeaconView.PickDirection(Vector3.back + Vector3.left * 0.1f, CamRight, CamToward, BattleBeaconView.WorkDir.Back, down));
            Assert.AreEqual(BattleBeaconView.WorkDir.Back, BattleBeaconView.PickDirection(Vector3.forward, CamRight, CamToward, BattleBeaconView.WorkDir.Left, up), "멀어지는 계단은 뒷모습이 있다");
            // 화로 관리는 뒷모습이 없다
            Assert.AreEqual(BattleBeaconView.WorkDir.Right, BattleBeaconView.PickDirection(Vector3.forward + Vector3.right * 0.1f, CamRight, CamToward, BattleBeaconView.WorkDir.Front, tend));
            Assert.AreEqual(BattleBeaconView.WorkDir.Front, BattleBeaconView.PickDirection(Vector3.back, CamRight, CamToward, BattleBeaconView.WorkDir.Left, tend));
            // 가는 쪽이 없으면 이전 방향 (있을 때만)
            Assert.AreEqual(BattleBeaconView.WorkDir.Left, BattleBeaconView.PickDirection(Vector3.zero, CamRight, CamToward, BattleBeaconView.WorkDir.Left, up));
            Assert.AreEqual(BattleBeaconView.WorkDir.Right, BattleBeaconView.PickDirection(Vector3.zero, CamRight, CamToward, BattleBeaconView.WorkDir.Front, up));
        }

        [Test]
        public void PickDirection_Hysteresis_DoesNotFlickerOnDiagonal()
        {
            var walk = BattleBeaconView.WorkMotion.Walk;
            Vector3 nearDiagonal = new Vector3(1f, 0f, -1.1f);   // 사진기 쪽 성분이 아주 조금 더 크다
            Assert.AreEqual(BattleBeaconView.WorkDir.Right, BattleBeaconView.PickDirection(nearDiagonal, CamRight, CamToward, BattleBeaconView.WorkDir.Right, walk), "옆모습이었으면 조금 더 유지");
            Assert.AreEqual(BattleBeaconView.WorkDir.Front, BattleBeaconView.PickDirection(nearDiagonal, CamRight, CamToward, BattleBeaconView.WorkDir.Front, walk), "앞모습이었으면 조금 더 유지");
        }

        [Test]
        public void ClimberMotion_FollowsCycle_CarryUp_TendInLamp_EmptyDown()
        {
            Vector3 station = BattleTowerInterior.WorkerStation(BattleTowerInterior.ClimberIndex, 0f);
            var order = new List<BattleTowerInterior.ClimbMotion>();
            for (float t = 0f; t < BattleTowerInterior.ClimbCycleSeconds; t += 0.05f)
            {
                var phase = BattleTowerInterior.Climber(t, station, out Vector3 feet, out var motion);
                if (order.Count == 0 || order[order.Count - 1] != motion) order.Add(motion);
                Assert.AreEqual(phase == BattleTowerInterior.ClimbPhase.Hidden, motion == BattleTowerInterior.ClimbMotion.Hidden, "Hidden 일치");
                Assert.AreEqual(phase == BattleTowerInterior.ClimbPhase.Tend, motion == BattleTowerInterior.ClimbMotion.Tend, "Tend 일치");
                Assert.AreEqual(phase == BattleTowerInterior.ClimbPhase.Wait, motion == BattleTowerInterior.ClimbMotion.Wait, "Wait 일치");
            }

            var expected = new[]
            {
                BattleTowerInterior.ClimbMotion.Wait, BattleTowerInterior.ClimbMotion.WalkCarry, BattleTowerInterior.ClimbMotion.ClimbUp, BattleTowerInterior.ClimbMotion.Hidden,
                BattleTowerInterior.ClimbMotion.WalkCarry, BattleTowerInterior.ClimbMotion.Tend, BattleTowerInterior.ClimbMotion.WalkEmpty, BattleTowerInterior.ClimbMotion.Hidden,
                BattleTowerInterior.ClimbMotion.ClimbDown, BattleTowerInterior.ClimbMotion.WalkEmpty,
            };
            CollectionAssert.AreEqual(expected, order, "기다림 → 운반 걷기 → 오르기 → 벽 속 → 불방 입구 운반 → 화로 관리 → 빈손 → 벽 속 → 내려오기 → 빈손 제자리");
        }

        [Test]
        public void Workers_ChooseSheetPerState()
        {
            using (var sim = MakeSim())
            {
                var quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var mat = new Material(Shader.Find("Sprites/Default"));
                try
                {
                    var view = new BattleBeaconView(sim, mat, quadObj.GetComponent<MeshFilter>().sharedMesh, new Bounds(Vector3.zero, Vector3.one * 2000f));
                    try
                    {
                        Assert.IsTrue(view.HasMotionArt, "K-119 시트가 불러와졌다");
                        var seen = new HashSet<BattleBeaconView.WorkMotion>();
                        float t = 0f;
                        view.Draw();
                        for (int i = 0; i < 900 && seen.Count < 5; i++)
                        {
                            t += 0.05f; sim.BeginFrame(0.05f); sim.Schedule(0.05f, t); sim.Complete(); view.Draw();
                            // 쉬는 동안 돌림대 일꾼 둘은 옛 서기·쉬기 그림, 계단 일꾼만 새 시트
                            Assert.IsFalse(view.WorkerUsesMotionSheet(0), "0번은 쉬는 동안 새 시트를 쓰지 않는다");
                            Assert.IsFalse(view.WorkerUsesMotionSheet(1), "1번도");
                            if (!view.WorkerUsesMotionSheet(2)) continue;
                            var m = view.WorkerMotionKind(2);
                            seen.Add(m);
                            string expectedPrefix = "worker_b_" + (m == BattleBeaconView.WorkMotion.ClimbUp ? "climb_up" : m == BattleBeaconView.WorkMotion.ClimbDown ? "climb_down"
                                : m == BattleBeaconView.WorkMotion.Tend ? "tend" : m == BattleBeaconView.WorkMotion.Carry ? "carry" : "walk") + "_";
                            StringAssert.StartsWith(expectedPrefix, view.WorkerSheetName(2), "동작에 맞는 시트");
                            Assert.IsTrue(BattleBeaconView.SheetProvided(m, view.WorkerFacing(2)), "없는 방향 시트는 고르지 않는다");
                        }

                        Assert.IsTrue(seen.Contains(BattleBeaconView.WorkMotion.Carry), "올라갈 때 운반 걷기");
                        Assert.IsTrue(seen.Contains(BattleBeaconView.WorkMotion.ClimbUp), "계단 오르기");
                        Assert.IsTrue(seen.Contains(BattleBeaconView.WorkMotion.Tend), "불방 화로 관리");
                        Assert.IsTrue(seen.Contains(BattleBeaconView.WorkMotion.ClimbDown), "계단 내려오기");
                        Assert.IsTrue(seen.Contains(BattleBeaconView.WorkMotion.Walk), "내려와 빈손으로 걷기");

                        // 돌림대를 밀 때는 세 명 모두 옛 미는 그림 (돌아오는 일꾼 포함 — 걷는 중이면 새 시트로 돌아오다가 도착하면 민다)
                        sim.RotateBeam(1);
                        view.Draw();
                        Assert.IsTrue(view.WorkersPushing);
                        Assert.IsFalse(view.WorkerUsesMotionSheet(0), "밀 때 0번은 미는 그림");
                        Assert.IsFalse(view.WorkerUsesMotionSheet(1), "밀 때 1번은 미는 그림");
                    }
                    finally { view.Dispose(); }
                }
                finally { Object.DestroyImmediate(quadObj); Object.DestroyImmediate(mat); }
            }
        }

        [Test]
        public void Workers_FacingFollowsGivenCamera_AndFallBackToOldSheetWithoutMotionSheets()
        {
            using (var sim = MakeSim())
            {
                var quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var mat = new Material(Shader.Find("Sprites/Default"));
                var camObject = new GameObject("Battle Camera (not MainCamera)", typeof(Camera));
                try
                {
                    var cam = camObject.GetComponent<Camera>();
                    var view = new BattleBeaconView(sim, mat, quadObj.GetComponent<MeshFilter>().sharedMesh, new Bounds(Vector3.zero, Vector3.one * 2000f));
                    try
                    {
                        float t = 0f;
                        camObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);   // 북쪽을 본다: 서쪽 벽 계단을 북으로 오르면 멀어진다
                        view.Draw(cam);
                        for (int i = 0; i < 400 && !(view.WorkerUsesMotionSheet(2) && view.WorkerMotionKind(2) == BattleBeaconView.WorkMotion.ClimbUp); i++)
                        { t += 0.05f; sim.BeginFrame(0.05f); sim.Schedule(0.05f, t); sim.Complete(); view.Draw(cam); }
                        Assert.AreEqual(BattleBeaconView.WorkMotion.ClimbUp, view.WorkerMotionKind(2));
                        Assert.AreEqual(BattleBeaconView.WorkDir.Back, view.WorkerFacing(2), "멀어지며 오르면 뒷모습");
                        camObject.transform.rotation = Quaternion.LookRotation(Vector3.right);   // 다른 성벽으로 사진기를 돌린다
                        t += 0.05f; sim.BeginFrame(0.05f); sim.Schedule(0.05f, t); sim.Complete(); view.Draw(cam);
                        Assert.AreEqual(BattleBeaconView.WorkDir.Left, view.WorkerFacing(2), "사진기를 따라 옆모습으로");

                        // 새 시트가 없으면 옛 미는 그림으로 되돌아간다 (사라지지 않는다)
                        view.DropMotionSheets();
                        Assert.IsFalse(view.HasMotionArt);
                        for (int i = 0; i < 1200 && !(view.WorkerVisible(2) && view.WorkerMotionKind(2) == BattleBeaconView.WorkMotion.Carry && view.WorkerFeet(2).y < BattleSim.WallHeight + 0.3f && t > 20f); i++)
                        { t += 0.05f; sim.BeginFrame(0.05f); sim.Schedule(0.05f, t); sim.Complete(); view.Draw(cam); }
                        Assert.IsTrue(view.WorkerVisible(2));
                        Assert.IsFalse(view.WorkerUsesMotionSheet(2), "새 시트 없이 옛 그림으로 그린다");
                    }
                    finally { view.Dispose(); }
                }
                finally { Object.DestroyImmediate(quadObj); Object.DestroyImmediate(mat); Object.DestroyImmediate(camObject); }
            }
        }

        // ── W39: Codex K-127 일꾼 바퀴 밀기 시트 ──
        [Test]
        public void PushSheets_Exist_ForEveryProvidedKindAndDirection()
        {
            int found = 0;
            int[] frames = { 8, 3, 3, 8 };
            for (int v = 0; v < 2; v++)
                for (int k = 0; k < 4; k++)
                    for (int d = 0; d < 4; d++)
                    {
                        var kind = (BattleBeaconView.PushKind)k; var dir = (BattleBeaconView.WorkDir)d;
                        if (!BattleBeaconView.PushSheetProvided(kind, dir)) continue;
                        string name = BattleBeaconView.PushSheetName(v, kind, dir);
                        var tex = Resources.Load<Texture2D>("BattleArt/Beacon/" + name);
                        Assert.IsNotNull(tex, name);
                        Assert.AreEqual(frames[k] * 96, tex.width, name + " 폭 = 칸 수 × 96");
                        Assert.AreEqual(128, tex.height, name);
                        Assert.AreEqual(FilterMode.Point, tex.filterMode, name);
                        Assert.AreEqual(TextureWrapMode.Clamp, tex.wrapMode, name);
                        found++;
                    }

            Assert.AreEqual(28, found, "K-127 시트 28개 (외형 둘 × (밀기 4 + 시작 4 + 멈춤 4 + 힘줌 2))");
            // 옛 K-110 미는 그림은 그대로 남아 있다 (대비책)
            Assert.IsNotNull(Resources.Load<Texture2D>("BattleArt/Beacon/worker_a_push"));
            Assert.IsNotNull(Resources.Load<Texture2D>("BattleArt/Beacon/worker_b_push"));
        }

        [Test]
        public void PushSheetNames_AndKindRules()
        {
            Assert.AreEqual("worker_a_push_front", BattleBeaconView.PushSheetName(0, BattleBeaconView.PushKind.Push, BattleBeaconView.WorkDir.Front));
            Assert.AreEqual("worker_b_push_start_back", BattleBeaconView.PushSheetName(1, BattleBeaconView.PushKind.Start, BattleBeaconView.WorkDir.Back));
            Assert.AreEqual("worker_a_push_stop_left", BattleBeaconView.PushSheetName(0, BattleBeaconView.PushKind.Stop, BattleBeaconView.WorkDir.Left));
            Assert.AreEqual("worker_b_push_strain_right", BattleBeaconView.PushSheetName(1, BattleBeaconView.PushKind.Strain, BattleBeaconView.WorkDir.Right));
            Assert.IsFalse(BattleBeaconView.PushSheetProvided(BattleBeaconView.PushKind.Strain, BattleBeaconView.WorkDir.Front), "힘줌은 옆모습만");
            Assert.IsFalse(BattleBeaconView.PushSheetProvided(BattleBeaconView.PushKind.Strain, BattleBeaconView.WorkDir.Back));
            Assert.IsTrue(BattleBeaconView.PushSheetProvided(BattleBeaconView.PushKind.Start, BattleBeaconView.WorkDir.Back));

            var right = BattleBeaconView.WorkDir.Right; var front = BattleBeaconView.WorkDir.Front;
            Assert.AreEqual(BattleBeaconView.PushKind.Start, BattleBeaconView.PushKindFor(true, 0.1f, false, right), "막 시작하면 시작");
            Assert.AreEqual(BattleBeaconView.PushKind.Push, BattleBeaconView.PushKindFor(true, 2f, false, right));
            Assert.AreEqual(BattleBeaconView.PushKind.Strain, BattleBeaconView.PushKindFor(true, 2f, true, right), "빨리 돌면 옆모습은 힘줌");
            Assert.AreEqual(BattleBeaconView.PushKind.Push, BattleBeaconView.PushKindFor(true, 2f, true, front), "앞모습은 힘줌 시트가 없어 밀기");
            Assert.AreEqual(BattleBeaconView.PushKind.Stop, BattleBeaconView.PushKindFor(false, 5f, true, right), "멈추면 멈춤");

            float nominal = 90f / BattleSim.BeamRotateSeconds;
            Assert.IsFalse(BattleBeaconView.IsStrain(nominal), "평소 속도는 힘줌이 아니다");
            Assert.IsTrue(BattleBeaconView.IsStrain(nominal * 1.5f));
            Assert.IsFalse(BattleBeaconView.IsStrain(0f));
        }

        [Test]
        public void Workers_PushSheetFollowsFacing_StartPushStop_AndFallsBack()
        {
            using (var sim = MakeSim())
            {
                var quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var mat = new Material(Shader.Find("Sprites/Default"));
                var camObject = new GameObject("Battle Camera (not MainCamera)", typeof(Camera));
                try
                {
                    var cam = camObject.GetComponent<Camera>();
                    camObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);   // 북쪽을 본다
                    var view = new BattleBeaconView(sim, mat, quadObj.GetComponent<MeshFilter>().sharedMesh, new Bounds(Vector3.zero, Vector3.one * 2000f));
                    try
                    {
                        float t = 0f;
                        view.Draw(cam);
                        Assert.AreEqual(0, view.PushSheetsLoaded, "서 있을 때는 밀기 시트를 읽지 않는다");
                        Assert.IsFalse(view.WorkerUsesPushSheet(0));

                        sim.RotateBeam(1);
                        t += 0.05f; sim.BeginFrame(0.05f); sim.Schedule(0.05f, t); sim.Complete(); view.Draw(cam);
                        Assert.IsTrue(view.WorkersPushing);
                        Assert.IsTrue(view.WorkerUsesPushSheet(0), "0번은 K-127 시트");
                        Assert.IsTrue(view.WorkerUsesPushSheet(1));
                        Assert.IsFalse(view.WorkerUsesMotionSheet(0), "K-119 걷기 시트가 아니다");
                        Assert.AreEqual(BattleBeaconView.PushKind.Start, view.WorkerPushKind(0), "시작 먼저");
                        StringAssert.StartsWith("worker_a_push_start_", view.WorkerPushSheetName(0));
                        StringAssert.StartsWith("worker_b_push_start_", view.WorkerPushSheetName(1), "1번은 b 외형");

                        var dirs = new HashSet<BattleBeaconView.WorkDir>();
                        var kinds = new HashSet<BattleBeaconView.PushKind>();
                        var last = view.WorkerFacing(0);
                        int steps = 0;
                        for (; steps < 300; steps++)
                        {
                            t += 0.05f; sim.BeginFrame(0.05f); sim.Schedule(0.05f, t); sim.Complete(); view.Draw(cam);
                            if (!view.WorkersPushing) break;
                            var kind = view.WorkerPushKind(0);
                            var dir = view.WorkerFacing(0);
                            kinds.Add(kind);
                            dirs.Add(dir);
                            last = dir;
                            Assert.IsTrue(view.WorkerUsesPushSheet(0));
                            Assert.AreEqual(BattleBeaconView.PushSheetName(0, kind, dir), view.WorkerPushSheetName(0), "종류·방향에 맞는 시트 이름");
                            Assert.IsTrue(BattleBeaconView.PushSheetProvided(kind, dir), "없는 시트를 고르지 않는다");
                            // 방향은 걷기와 같은 규칙: 지금 방향을 이전 방향으로 다시 골라도 같다
                            float a = (view.CapstanDegrees + 60f) * Mathf.Deg2Rad;
                            Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * (view.FacingLeft ? 1f : -1f);
                            Assert.AreEqual(dir, BattleBeaconView.PickDirection(tangent, CamRight, CamToward, dir, BattleBeaconView.WorkMotion.Walk), "접선 방향을 본다");
                        }

                        Assert.Less(steps, 300, "빛줄기가 끝까지 돈다");
                        Assert.IsTrue(kinds.Contains(BattleBeaconView.PushKind.Start) && kinds.Contains(BattleBeaconView.PushKind.Push), "시작 뒤 밀기");
                        Assert.IsFalse(view.BeamStraining, "평소 속도는 힘줌이 아니다");
                        Assert.GreaterOrEqual(dirs.Count, 2, "돌림대를 한 바퀴 가까이 돌며 방향이 바뀐다");
                        Assert.Less(view.PushSheetsLoaded, 28, "쓰는 시트만 읽는다");

                        // 멈춤: 같은 방향 그대로, 멈춤 시트 한 번 + 마지막 칸 유지
                        Assert.AreEqual(BattleBeaconView.PushKind.Stop, view.WorkerPushKind(0));
                        Assert.AreEqual(last, view.WorkerFacing(0), "멈출 때 향하던 쪽 그대로");
                        StringAssert.StartsWith("worker_a_push_stop_", view.WorkerPushSheetName(0));
                        Assert.IsTrue(view.WorkerUsesPushSheet(0));

                        // 시트가 없으면 옛 미는·쉬는 그림으로 (사라지지 않는다)
                        view.DropPushSheets();
                        Assert.AreEqual(0, view.PushSheetsLoaded);
                        sim.RotateBeam(2);
                        t += 0.05f; sim.BeginFrame(0.05f); sim.Schedule(0.05f, t); sim.Complete(); view.Draw(cam);
                        Assert.IsTrue(view.WorkersPushing);
                        Assert.IsFalse(view.WorkerUsesPushSheet(0), "K-127 시트 없이 옛 그림으로 그린다");
                        Assert.IsFalse(view.WorkerUsesPushSheet(1));
                    }
                    finally { view.Dispose(); }
                }
                finally { Object.DestroyImmediate(quadObj); Object.DestroyImmediate(mat); Object.DestroyImmediate(camObject); }
            }
        }
        static BattleSim MakeSim()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_worker_sheets_w31", out string error);
            Assert.IsNull(error);
            return new BattleSim(setup, lookup);
        }
    }
}
