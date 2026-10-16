using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.Definitions;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 마법사 특수 공격 (MAP-4, Battle.md 5.3, D-027 Q3, D-044 M3·M4·M7·M8)
    // - 폭발: 찍고 1초 뒤 반경 안 모든 적에게 한 번 큰 피해 / 불바다: 찍은 범위가 한동안 불타 지속 피해(비 0.1·소나기 0.5배)
    // - 성벽에서 150m 안까지. 쓰면 재사용 대기. 마법사가 참가해야 쓸 수 있다
    // - 피해는 일반 사건 줄(HitEvent)에 넣어 화살·포탄과 같은 방식으로 처리된다(보상·시체 더미 포함)
    public sealed partial class BattleSim
    {
        public const float SpellCastRange = 150f;   // 성벽에서 (M3)
        const float ExplosionDelay = 1f;            // M4
        const float FireTickSeconds = 0.5f;

        public struct SpellInfo
        {
            public string Name;
            public float Radius;
            public float Cooldown;
            public float CooldownLeft;
            public bool IsFire;
            public bool Ready => CooldownLeft <= 0f;
        }

        // 화면 효과용
        public struct SpellEffect
        {
            public bool Fire;          // false = 폭발(대기 중이면 Pending)
            public bool Pending;
            public Vector3 Position;
            public float Radius;
            public float TimeLeft;
        }

        readonly List<SpellDef> spells = new List<SpellDef>();
        readonly List<float> spellCooldown = new List<float>();
        readonly List<SpellEffect> effects = new List<SpellEffect>();
        readonly List<float> fireTick = new List<float>();
        readonly List<int> effectDamage = new List<int>();

        public bool WizardPresent { get; private set; }
        public int SpellCount => spells.Count;
        public IReadOnlyList<SpellEffect> Effects => effects;

        void InitSpells(IDefinitionLookup defs)
        {
            // D-044 M7: 마법사는 배치 지점 없이도 항상 참가. 참가 목록 규칙(C-005 v1.3)은 Codex 결과 반영기 갱신 뒤 App에 적용 —
            // 그 전까지는 살아 있는 마법사가 있으면 쓸 수 있게 한다
            foreach (var hero in setup.Heroes)
            {
                UnitDef unit = defs.Get<UnitDef>(hero.UnitDefId);
                if (hero.IsAlive && unit != null && unit.Kind == UnitKind.Wizard)
                {
                    WizardPresent = true;
                    InitWizard(unit);
                }
            }

            // 정의 순서: 즉시(폭발) 먼저, 지속(불바다) 다음
            var all = new List<SpellDef>(defs.All<SpellDef>());
            all.Sort((a, b) => a.Duration.CompareTo(b.Duration));
            foreach (SpellDef spell in all)
            {
                spells.Add(spell);
                spellCooldown.Add(0f);
            }
        }

        public SpellInfo GetSpell(int index) => new SpellInfo
        {
            Name = spells[index].DisplayName,
            Radius = spells[index].Radius,
            Cooldown = spells[index].Cooldown,
            CooldownLeft = spellCooldown[index],
            IsFire = spells[index].IsFire,
        };

        // 성벽에서 떨어진 거리 (성 안이면 0 이하)
        float DistanceFromWalls(Vector3 p) => math.max(math.abs(p.x), math.abs(p.z)) - CastleHalf;

        public bool CastSpell(int index, Vector3 point)
        {
            if (Ended || index < 0 || index >= spells.Count) return false;
            SpellDef spell = spells[index];
            if (!WizardPresent)
            {
                Notice?.Invoke("마법사가 이번 전투에 없습니다");
                return false;
            }

            if (spellCooldown[index] > 0f)
            {
                Notice?.Invoke($"{spell.DisplayName}: 재사용 대기 {spellCooldown[index]:0}초");
                return false;
            }

            if (DistanceFromWalls(point) > SpellCastRange)
            {
                Notice?.Invoke($"{spell.DisplayName}: 성벽에서 {SpellCastRange:0}m 안에만 쓸 수 있습니다");
                return false;
            }

            if (WizardMoving)
            {
                Notice?.Invoke($"마법사가 {SideNames[WizardSide]} 성벽으로 이동 중입니다");
                return false;
            }

            var at = new Vector3(point.x, 0f, point.z);
            spellCooldown[index] = spell.Cooldown;   // 바로 대기 시작(같은 프레임 두 번 방지)
            // D-058: 다른 성벽 쪽이면 마법사가 그 성벽 가운데로 가서 도착하면 쏜다
            if (!WizardReadyAt(SideOfPoint(at), index, at))
            {
                Notice?.Invoke($"마법사가 {SideNames[WizardSide]} 성벽으로 이동 (약 {wizardDistanceLeft / WizardSpeed:0}초) — 도착하면 {spell.DisplayName}");
                return true;
            }

            StartSpellEffect(index, at);
            return true;
        }

        // 지금까지 시작된 마법 효과 수 (줄지 않음) — 같은 순간 다른 효과가 끝나도 시험이 '새 효과'를 셀 수 있게
        public int EffectsStarted { get; private set; }

        void StartSpellEffect(int index, Vector3 at)
        {
            SpellDef spell = spells[index];
            MarkWizardCast();   // 마법사 시전 그림 (K-032)
            RunOrQueue(() =>
            {
                bool fire = spell.Duration > 0f;
                effects.Add(new SpellEffect { Fire = fire, Pending = !fire, Position = at, Radius = spell.Radius, TimeLeft = fire ? spell.Duration : ExplosionDelay });
                EffectsStarted++;
                fireTick.Add(0f);
                effectDamage.Add(spell.Damage);
            });
            Notice?.Invoke(spell.Duration > 0f ? $"불바다! ({spell.Duration:0}초)" : "폭발 준비 — 1초 뒤 터집니다");
        }

        // BeginFrame에서 (작업이 끝난 뒤) — 재사용 대기, 폭발 대기, 불바다 피해
        void UpdateSpells(float dt)
        {
            for (int i = 0; i < spellCooldown.Count; i++)
            {
                spellCooldown[i] = math.max(0f, spellCooldown[i] - dt);
            }

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                SpellEffect e = effects[i];
                e.TimeLeft -= dt;
                if (e.Pending)
                {
                    if (e.TimeLeft <= 0f)
                    {
                        int hit = DamageArea(e.Position, e.Radius, effectDamage[i]);
                        Notice?.Invoke($"폭발! 적 {hit}마리에 피해");
                        e.Pending = false;
                        e.TimeLeft = 0.4f;   // 터지는 모습 잠깐
                    }
                }
                else if (e.Fire)
                {
                    float tick = fireTick[i] + dt;
                    while (tick >= FireTickSeconds && e.TimeLeft > -FireTickSeconds)
                    {
                        tick -= FireTickSeconds;
                        int damage = math.max(1, (int)math.round(effectDamage[i] * FireTickSeconds * FireDamageMultiplier));
                        DamageArea(e.Position, e.Radius, damage, effectDamage[i]);   // 맞은 적은 화상 확률 (K-035)
                    }

                    fireTick[i] = tick;
                }

                if (e.TimeLeft <= 0f && !e.Pending)
                {
                    effects.RemoveAt(i);
                    fireTick.RemoveAt(i);
                    effectDamage.RemoveAt(i);
                }
                else
                {
                    effects[i] = e;
                }
            }

            UpdateBurns(dt);   // 불바다 화상 (K-035, BattleSim.Burn.cs)
        }

        // 반경 안 살아 있는 적마다 사건 하나 (지난 프레임 격자 사용 — 작업이 끝난 뒤라 읽어도 안전)
        // burnFieldDps > 0이면 불바다 틱: 맞은 적마다 화상 확률 (K-035)
        int DamageArea(Vector3 center, float radius, int damage, float burnFieldDps = 0f)
        {
            var c = new float3(center.x, 0f, center.z);
            int2 minCell = grid.CellOf(c - new float3(radius, 0f, radius));
            int2 maxCell = grid.CellOf(c + new float3(radius, 0f, radius));
            float radiusSq = radius * radius;
            int count = 0;
            for (int z = minCell.y; z <= maxCell.y; z++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int cell = grid.IndexOf(new int2(x, z));
                    int start = cellStart[cell];
                    int end = start + cellCount[cell];
                    for (int k = start; k < end; k++)
                    {
                        int e = sorted[k];
                        if (!EnemyState.IsAlive(eState[e])) continue;
                        float3 d = ePos[e] - c;
                        if (d.x * d.x + d.z * d.z > radiusSq) continue;
                        hitQueue.Enqueue(new HitEvent { Enemy = new EnemyHandle { Index = e, Generation = eGen[e] }, Damage = damage });
                        if (burnFieldDps > 0f) TryIgnite(e, burnFieldDps);
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
