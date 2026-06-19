using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NorthStar.Battle.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="CombatUnit"/>: damage formula (defense + elemental
    /// resistance), heal clamping, ability resolution/targeting, and the HP-event bug fix
    /// (PlayerHPChangedEvent reserved for player units; UnitHPChangedEvent for everyone).
    /// </summary>
    public class CombatUnitTests
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

        private CombatUnit MakeUnit(bool player, int hp = 100, int attack = 10, int defense = 5, int speed = 8)
        {
            var go = new GameObject("TestUnit");
            _spawned.Add(go);
            var unit = go.AddComponent<CombatUnit>();
            unit.isPlayerControlled = player;
            unit.unitName = player ? "Hero" : "Goblin";
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

        // ---- Damage formula -------------------------------------------------

        [Test]
        public void ComputeDamage_SubtractsDefense_NeutralResistance()
        {
            Assert.AreEqual(15, CombatUnit.ComputeDamage(20, 5, 1f));
        }

        [Test]
        public void ComputeDamage_FloorsAtOne_BeforeResistance()
        {
            // 3 - 10 = -7 → floored to 1, then *1 = 1.
            Assert.AreEqual(1, CombatUnit.ComputeDamage(3, 10, 1f));
        }

        [Test]
        public void ComputeDamage_AppliesResistanceMultiplier()
        {
            // (20 - 5) = 15, *0.5 resistance = 7.5 → rounds to 8.
            Assert.AreEqual(8, CombatUnit.ComputeDamage(20, 5, 0.5f));
        }

        [Test]
        public void ComputeDamage_ImmuneMultiplierZero_DealsNothing()
        {
            Assert.AreEqual(0, CombatUnit.ComputeDamage(50, 5, 0f));
        }

        [Test]
        public void TakeDamage_ReducesHP_ByFormulaResult()
        {
            var unit = MakeUnit(player: false, hp: 100, defense: 5);
            int dealt = unit.TakeDamage(20, DamageType.Physical); // 20-5 = 15
            Assert.AreEqual(15, dealt);
            Assert.AreEqual(85, unit.CurrentHP);
        }

        [Test]
        public void TakeDamage_RespectsElementalWeakness()
        {
            var unit = MakeUnit(player: false, hp: 100, defense: 0);
            unit.resistances = new[]
            {
                new ElementalResistance { type = DamageType.Fire, multiplier = 2f }
            };
            int dealt = unit.TakeDamage(10, DamageType.Fire); // (10-0)=10, *2 = 20
            Assert.AreEqual(20, dealt);
            Assert.AreEqual(80, unit.CurrentHP);
        }

        [Test]
        public void TakeDamage_HealType_RestoresInsteadOfDamaging()
        {
            var unit = MakeUnit(player: true, hp: 100, defense: 5);
            unit.TakeDamage(40, DamageType.Physical); // 35 dmg → 65
            int delta = unit.TakeDamage(20, DamageType.Heal); // heals 20 → 85
            Assert.AreEqual(-20, delta, "Heal-type TakeDamage returns a negative delta.");
            Assert.AreEqual(85, unit.CurrentHP);
        }

        [Test]
        public void TakeDamage_LethalDamage_FiresDeathAndUnitDiedEvent()
        {
            var unit = MakeUnit(player: false, hp: 10, defense: 0);
            bool deathFired = false;
            unit.OnDeath += _ => deathFired = true;

            UnitDiedEvent? captured = null;
            void Handler(UnitDiedEvent e) => captured = e;
            EventBus.Subscribe<UnitDiedEvent>(Handler);

            unit.TakeDamage(100, DamageType.Physical);

            EventBus.Unsubscribe<UnitDiedEvent>(Handler);
            Assert.IsFalse(unit.IsAlive);
            Assert.IsTrue(deathFired, "OnDeath should fire when HP reaches zero.");
            Assert.IsTrue(captured.HasValue, "UnitDiedEvent should be published on death.");
            Assert.IsFalse(captured.Value.wasAlly, "Enemy death should report wasAlly = false.");
        }

        // ---- HP event bug fix ----------------------------------------------

        [Test]
        public void TakeDamage_Enemy_DoesNotPublishPlayerHPChanged()
        {
            var enemy = MakeUnit(player: false);
            bool playerEvent = false;
            void PlayerHandler(PlayerHPChangedEvent e) => playerEvent = true;
            EventBus.Subscribe<PlayerHPChangedEvent>(PlayerHandler);

            enemy.TakeDamage(20, DamageType.Physical);

            EventBus.Unsubscribe<PlayerHPChangedEvent>(PlayerHandler);
            Assert.IsFalse(playerEvent,
                "Bug fix: enemy damage must NOT publish PlayerHPChangedEvent.");
        }

        [Test]
        public void TakeDamage_Player_PublishesPlayerHPChanged()
        {
            var player = MakeUnit(player: true);
            bool playerEvent = false;
            void PlayerHandler(PlayerHPChangedEvent e) => playerEvent = true;
            EventBus.Subscribe<PlayerHPChangedEvent>(PlayerHandler);

            player.TakeDamage(20, DamageType.Physical);

            EventBus.Unsubscribe<PlayerHPChangedEvent>(PlayerHandler);
            Assert.IsTrue(playerEvent, "Player damage should publish PlayerHPChangedEvent.");
        }

        [Test]
        public void TakeDamage_AnyUnit_PublishesUnitHPChanged()
        {
            var enemy = MakeUnit(player: false);
            UnitHPChangedEvent? captured = null;
            void Handler(UnitHPChangedEvent e) => captured = e;
            EventBus.Subscribe<UnitHPChangedEvent>(Handler);

            enemy.TakeDamage(20, DamageType.Physical); // 15 dmg → 85

            EventBus.Unsubscribe<UnitHPChangedEvent>(Handler);
            Assert.IsTrue(captured.HasValue, "UnitHPChangedEvent should fire for every unit.");
            Assert.AreEqual(85, captured.Value.current);
            Assert.AreSame(enemy, captured.Value.unit);
        }

        // ---- Heal -----------------------------------------------------------

        [Test]
        public void Heal_ClampsToMaxHP()
        {
            var unit = MakeUnit(player: true, hp: 100);
            unit.TakeDamage(30, DamageType.Physical); // 25 dmg → 75
            int healed = unit.Heal(999);
            Assert.AreEqual(25, healed);
            Assert.AreEqual(100, unit.CurrentHP);
        }

        // ---- UseAbility -----------------------------------------------------

        [Test]
        public void UseAbility_InsufficientMP_ReturnsFalse()
        {
            var caster = MakeUnit(player: true);
            caster.baseMaxMP = 5;
            caster.ResetRuntimeStats();

            var ability = ScriptableObject.CreateInstance<AbilityData>();
            _spawned.Add(ability);
            ability.abilityId = "fireball";
            ability.mpCost = 20;
            ability.damageType = DamageType.Fire;
            ability.damageMultiplier = 1f;

            var target = MakeUnit(player: false);
            Assert.IsFalse(caster.UseAbility(ability, new[] { target }));
            Assert.AreEqual(5, caster.CurrentMP, "MP must not be spent on a failed cast.");
        }

        [Test]
        public void UseAbility_DamagingAbility_SpendsMPAndDamagesTarget()
        {
            var caster = MakeUnit(player: true, attack: 10);
            var ability = ScriptableObject.CreateInstance<AbilityData>();
            _spawned.Add(ability);
            ability.abilityId = "fireball";
            ability.mpCost = 10;
            ability.damageType = DamageType.Fire;
            ability.damageMultiplier = 2f; // 10 atk * 2 = 20 raw

            var target = MakeUnit(player: false, hp: 100, defense: 0); // takes full 20
            int mpBefore = caster.CurrentMP;

            bool ok = caster.UseAbility(ability, new[] { target });

            Assert.IsTrue(ok);
            Assert.AreEqual(mpBefore - 10, caster.CurrentMP);
            Assert.AreEqual(80, target.CurrentHP);
        }

        [Test]
        public void UseAbility_HealAbility_RestoresTarget()
        {
            var caster = MakeUnit(player: true, attack: 10);
            var ability = ScriptableObject.CreateInstance<AbilityData>();
            _spawned.Add(ability);
            ability.abilityId = "heal";
            ability.mpCost = 5;
            ability.damageType = DamageType.Heal;
            ability.damageMultiplier = 3f; // 10 atk * 3 = 30 heal

            var ally = MakeUnit(player: true, hp: 100, defense: 0);
            ally.TakeDamage(50, DamageType.Physical); // → 50

            caster.UseAbility(ability, new[] { ally });
            Assert.AreEqual(80, ally.CurrentHP);
        }

        [Test]
        public void UseAbility_SingleTarget_OnlyAffectsFirstTarget()
        {
            var caster = MakeUnit(player: true, attack: 10);
            var ability = ScriptableObject.CreateInstance<AbilityData>();
            _spawned.Add(ability);
            ability.abilityId = "strike";
            ability.mpCost = 0;
            ability.damageType = DamageType.Physical;
            ability.damageMultiplier = 1f; // 10 raw
            ability.isMultiTarget = false;

            var t1 = MakeUnit(player: false, hp: 100, defense: 0);
            var t2 = MakeUnit(player: false, hp: 100, defense: 0);

            caster.UseAbility(ability, new[] { t1, t2 });
            Assert.AreEqual(90, t1.CurrentHP, "First target should be hit.");
            Assert.AreEqual(100, t2.CurrentHP, "Single-target ability must not hit the second target.");
        }

        [Test]
        public void UseAbility_MultiTarget_AffectsAllTargets()
        {
            var caster = MakeUnit(player: true, attack: 10);
            var ability = ScriptableObject.CreateInstance<AbilityData>();
            _spawned.Add(ability);
            ability.abilityId = "fireball";
            ability.mpCost = 0;
            ability.damageType = DamageType.Fire;
            ability.damageMultiplier = 1f;
            ability.isMultiTarget = true;

            var t1 = MakeUnit(player: false, hp: 100, defense: 0);
            var t2 = MakeUnit(player: false, hp: 100, defense: 0);

            caster.UseAbility(ability, new[] { t1, t2 });
            Assert.AreEqual(90, t1.CurrentHP);
            Assert.AreEqual(90, t2.CurrentHP);
        }

        [Test]
        public void UseAbility_WhileStunned_ReturnsFalse()
        {
            var caster = MakeUnit(player: true);
            caster.ApplyStatus(MakeStatus("stun", duration: 1, preventsAction: true));

            var ability = ScriptableObject.CreateInstance<AbilityData>();
            _spawned.Add(ability);
            ability.abilityId = "strike";
            ability.mpCost = 0;
            ability.damageType = DamageType.Physical;
            ability.damageMultiplier = 1f;

            var target = MakeUnit(player: false);
            Assert.IsFalse(caster.UseAbility(ability, new[] { target }),
                "A stunned unit cannot act.");
        }
    }
}
