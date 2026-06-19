using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NorthStar.Battle.Tests
{
    /// <summary>
    /// EditMode tests for status-effect application, duration ticking/expiry, per-turn damage,
    /// stat-multiplier effects and the stun (preventsAction) gate.
    /// </summary>
    public class StatusEffectTests
    {
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => EventBus.ClearAll();

        [TearDown]
        public void TearDown()
        {
            EventBus.ClearAll();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private CombatUnit MakeUnit(int hp = 100, int attack = 20, int defense = 0, int speed = 8)
        {
            var go = new GameObject("StatusTestUnit");
            _spawned.Add(go);
            var unit = go.AddComponent<CombatUnit>();
            unit.unitName = "Dummy";
            unit.baseMaxHP = hp;
            unit.baseAttack = attack;
            unit.baseDefense = defense;
            unit.baseSpeed = speed;
            unit.ResetRuntimeStats();
            return unit;
        }

        private StatusEffectData MakeStatus(string id, int duration, int dmgPerTurn = 0,
            bool preventsAction = false, float statMult = 0f, string affectedStat = null,
            DamageType type = DamageType.Physical)
        {
            var s = ScriptableObject.CreateInstance<StatusEffectData>();
            _spawned.Add(s);
            s.statusId = id;
            s.displayName = id;
            s.durationTurns = duration;
            s.damagePerTurn = dmgPerTurn;
            s.damageType = type;
            s.preventsAction = preventsAction;
            s.statMultiplier = statMult;
            s.affectedStat = affectedStat;
            return s;
        }

        [Test]
        public void ApplyStatus_AddsToActiveStatuses_AndFiresEvent()
        {
            var unit = MakeUnit();
            StatusEffectData applied = null;
            unit.OnStatusApplied += s => applied = s;

            var poison = MakeStatus("poison", duration: 3);
            unit.ApplyStatus(poison);

            Assert.AreEqual(1, unit.ActiveStatuses.Count);
            Assert.AreSame(poison, applied);
        }

        [Test]
        public void ApplyStatus_SameId_RefreshesInsteadOfStacking()
        {
            var unit = MakeUnit();
            unit.ApplyStatus(MakeStatus("poison", duration: 2));
            unit.ApplyStatus(MakeStatus("poison", duration: 5));

            Assert.AreEqual(1, unit.ActiveStatuses.Count,
                "Re-applying the same statusId should refresh, not stack.");
        }

        [Test]
        public void RemoveStatus_RemovesAndFiresEvent()
        {
            var unit = MakeUnit();
            StatusEffectData removed = null;
            unit.OnStatusRemoved += s => removed = s;

            var stun = MakeStatus("stun", duration: 2, preventsAction: true);
            unit.ApplyStatus(stun);
            unit.RemoveStatus("stun");

            Assert.AreEqual(0, unit.ActiveStatuses.Count);
            Assert.AreSame(stun, removed);
        }

        [Test]
        public void TickStatuses_ExpiresStatusAfterDurationTurns()
        {
            var unit = MakeUnit();
            unit.ApplyStatus(MakeStatus("buff", duration: 2));

            unit.TickStatuses(); // 2 → 1, still active
            Assert.AreEqual(1, unit.ActiveStatuses.Count, "Still active after first tick.");

            unit.TickStatuses(); // 1 → 0, expires
            Assert.AreEqual(0, unit.ActiveStatuses.Count, "Should expire after its duration elapses.");
        }

        [Test]
        public void TickStatuses_AppliesPerTurnDamage()
        {
            var unit = MakeUnit(hp: 100, defense: 0);
            unit.ApplyStatus(MakeStatus("poison", duration: 3, dmgPerTurn: 7, type: DamageType.Dark));

            unit.TickStatuses(); // 7 dmg → 93
            Assert.AreEqual(93, unit.CurrentHP);

            unit.TickStatuses(); // 7 dmg → 86
            Assert.AreEqual(86, unit.CurrentHP);
        }

        [Test]
        public void TickStatuses_PerTurnDamage_CanKill_AndExpiresEffect()
        {
            var unit = MakeUnit(hp: 10, defense: 0);
            unit.ApplyStatus(MakeStatus("burn", duration: 5, dmgPerTurn: 20, type: DamageType.Fire));

            unit.TickStatuses();
            Assert.IsFalse(unit.IsAlive, "Per-turn damage should be able to kill the unit.");
        }

        [Test]
        public void StatMultiplierStatus_ReducesAffectedStat()
        {
            var unit = MakeUnit(attack: 20);
            Assert.AreEqual(20, unit.Attack);

            unit.ApplyStatus(MakeStatus("weaken", duration: 2, statMult: 0.5f, affectedStat: "Attack"));
            Assert.AreEqual(10, unit.Attack, "Attack should be halved while the debuff is active.");

            unit.RemoveStatus("weaken");
            Assert.AreEqual(20, unit.Attack, "Stat returns to base once the debuff is removed.");
        }

        [Test]
        public void PreventsActionStatus_MarksUnitStunned()
        {
            var unit = MakeUnit();
            Assert.IsFalse(unit.IsStunned());

            unit.ApplyStatus(MakeStatus("stun", duration: 1, preventsAction: true));
            Assert.IsTrue(unit.IsStunned());
        }
    }
}
