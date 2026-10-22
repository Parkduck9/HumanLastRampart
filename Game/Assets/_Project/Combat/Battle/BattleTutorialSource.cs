using System.Collections.Generic;
using HumanLastRampart.Tutorial;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 전투 화면 튜토리얼이 게임 상태를 읽는 곳 (W2). BattleEntry의 공개 상태와 BattleSim의 공개 값만 읽고 바꾸는 것은 시간 배율뿐이다.
    // - 걷기: 주인공 발 위치 변화량 합 / 지도: BattleEntry.MapOpen
    // - 분대: 묶음이 움직이기 시작·성벽이 바뀜·전령이 새로 떠남 / 마법: 재사용 대기가 0 → 양수로 바뀜
    public sealed class BattleTutorialSource : ITutorialSource
    {
        readonly BattleEntry entry;
        bool started;
        Vector3? lastFeet;
        float walked;
        int spellCasts;
        readonly List<bool> lastCooling = new List<bool>();

        BattleTutorialSource(BattleEntry entry) { this.entry = entry; }

        // BattleEntry.OnEnter 끝에서 한 줄로 부른다. 대상(첫 전투·튜토리얼 안 끝남)이 아니면 아무것도 하지 않는다
        public static TutorialDirector TryBegin(BattleEntry entry, Transform parent, int battleNumber)
        {
            if (!TutorialState.ShouldStartBattle(battleNumber)) return null;
            // 저장이 없으면 0으로 적어 둔다 — 전투가 중간에 끝나도 정비 구간이 이어지게
            if (!TutorialState.Store.TryLoad(out _)) TutorialState.Store.Save(0);
            return TutorialDirector.Begin(parent, new BattleTutorialSource(entry), TutorialStep.Walk, TutorialStep.Magic);
        }

        public bool Alive => entry != null && entry.Sim != null;

        public void Tick(float dt)
        {
            BattleSim sim = entry.Sim;
            if (sim == null) return;
            // 튜토리얼은 준비 지도 없이 성벽 위에서 시작한다 (걷기부터 가르치므로)
            if (!started)
            {
                started = true;
                if (entry.Preparing) entry.StartBattle();
            }

            if (entry.Rig != null)
            {
                Vector3 feet = entry.Rig.HeroFeet;
                if (lastFeet.HasValue) walked += Vector3.Distance(feet, lastFeet.Value);
                lastFeet = feet;
            }

            for (int i = 0; i < sim.SpellCount; i++)
            {
                bool cooling = sim.GetSpell(i).CooldownLeft > 0f;
                if (i >= lastCooling.Count) { lastCooling.Add(cooling); continue; }
                if (cooling && !lastCooling[i]) spellCasts++;
                lastCooling[i] = cooling;
            }
        }

        public float WalkedMeters => walked;
        public bool MapOpen => entry.MapOpen;
        public int SquadSends => entry.SquadOrders;   // 분대 이동 명령만 (OrderGroupMove·OrderWallMoveCount)
        public int SpellCasts => spellCasts;
        public int TrapCount => entry.Sim != null ? entry.Sim.TrapCount : 0;

        // 마법사가 없으면 마법 단계는 건너뛴다 (쓸 수 없는 일을 시키지 않는다)
        public bool IsAvailable(TutorialStep step) =>
            step != TutorialStep.Magic || (entry.Sim != null && entry.Sim.WizardPresent && entry.Sim.SpellCount > 0);

        // 주인공은 성벽 위에서 키 0.375m(HeroDrawSize)로 그려진다 — 몸 한가운데는 발에서 그 절반 위 (1.2m로 잡으면 고리가 머리 위 허공에 뜬다)
        public const float HeroCenterHeight = BattleView.HeroDrawSize * 0.5f;

        public bool TryGetTargetScreen(TutorialStep step, out Vector2 screen)
        {
            screen = default;
            Camera camera = entry.BattleCamera;
            if (camera == null || entry.Sim == null) return false;
            Vector3 world;
            if (step == TutorialStep.Walk && entry.Rig != null) world = entry.Rig.HeroFeet + Vector3.up * HeroCenterHeight;
            else if (step == TutorialStep.Squad && entry.MapOpen && TryFindGroup(out Vector3 center)) world = center;
            else return false;
            // 전투 사진기는 낮은 해상도 그림판에 그릴 때가 있어(PerfLabResolution) 화소가 아닌 비율로 구해 화면 크기에 곱한다
            Vector3 p = camera.WorldToViewportPoint(world);
            if (p.z <= 0f) return false;
            screen = new Vector2(p.x * Screen.width, p.y * Screen.height);
            return true;
        }

        // 보낼 수 있는 분대 가운데 가장 큰 묶음의 자리
        bool TryFindGroup(out Vector3 center)
        {
            center = default;
            BattleSim sim = entry.Sim;
            int best = 0;
            for (int g = 0; g < sim.GroupCount; g++)
            {
                BattleSim.GroupInfo info = sim.GetGroup(g);
                if (!info.Movable || info.Moving || info.Alive <= best) continue;
                best = info.Alive;
                center = new Vector3(info.Center.x, info.Center.y, info.Center.z);
            }

            return best > 0;
        }

        public void SetTimeScale(float scale) => entry.TutorialTimeScale = scale;
    }
}
