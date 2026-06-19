using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NorthStar.Battle.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="BattleManager"/>: initiative ordering (Speed + d6 with
    /// ties re-rolled), battle start/end EventBus publishing and game-state transitions, and
    /// win/lose resolution.
    /// </summary>
    public class BattleManagerTests
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

        private CombatUnit MakeUnit(string name, bool player, int speed, int hp = 100)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var unit = go.AddComponent<CombatUnit>();
            unit.unitName = name;
            unit.isPlayerControlled = player;
            unit.baseMaxHP = hp;
            unit.baseSpeed = speed;
            unit.baseDefense = 0;
            unit.ResetRuntimeStats();
            return unit;
        }

        private BattleManager MakeManager()
        {
            var go = new GameObject("BattleManager");
            _spawned.Add(go);
            return go.AddComponent<BattleManager>();
        }

        // ---- Initiative ordering (pure, deterministic die) ------------------

        [Test]
        public void OrderByInitiative_OrdersBySpeedPlusDie_HighestFirst()
        {
            var fast = MakeUnit("Fast", true, speed: 12);
            var slow = MakeUnit("Slow", false, speed: 3);

            // Constant die of 1 → totals 13 vs 4. Fast acts first.
            var ordered = BattleManager.OrderByInitiative(new[] { slow, fast }, () => 1);

            Assert.AreSame(fast, ordered[0]);
            Assert.AreSame(slow, ordered[1]);
        }

        [Test]
        public void OrderByInitiative_DieCanFlipOrder()
        {
            var a = MakeUnit("A", true, speed: 10);
            var b = MakeUnit("B", false, speed: 8);

            // Feed a die sequence so B's total (8+6=14) beats A's (10+1=11).
            var rolls = new Queue<int>(new[] { 1, 6 });
            var ordered = BattleManager.OrderByInitiative(new[] { a, b }, () => rolls.Dequeue());

            Assert.AreSame(b, ordered[0], "Die roll should let the slower unit outrace the faster one.");
        }

        [Test]
        public void OrderByInitiative_ResolvesTies_NoDuplicateTotals()
        {
            // Two units with identical speed. First pass gives a tie (both 5+3=8);
            // re-roll feeds distinct values so the tie is broken.
            var u1 = MakeUnit("U1", true, speed: 5);
            var u2 = MakeUnit("U2", false, speed: 5);

            // Pass 1: both roll 3 (tie). Re-roll: 4 and 2 → 9 vs 7, distinct.
            var rolls = new Queue<int>(new[] { 3, 3, 4, 2 });
            var ordered = BattleManager.OrderByInitiative(new[] { u1, u2 }, () => rolls.Dequeue());

            Assert.AreEqual(2, ordered.Count);
            CollectionAssert.AreEquivalent(new[] { u1, u2 }, ordered,
                "Both units must appear exactly once after tie resolution.");
            Assert.AreSame(u1, ordered[0], "Higher re-rolled total (5+4) should be first.");
        }

        // ---- Battle lifecycle ----------------------------------------------

        [Test]
        public void StartBattle_PublishesBattleStartedEvent_WithCombatants()
        {
            var manager = MakeManager();
            var ally = MakeUnit("Hero", true, speed: 8);
            var enemy = MakeUnit("Goblin", false, speed: 6);

            BattleStartedEvent? captured = null;
            void Handler(BattleStartedEvent e) => captured = e;
            EventBus.Subscribe<BattleStartedEvent>(Handler);

            manager.StartBattle(new[] { ally }, new[] { enemy });

            EventBus.Unsubscribe<BattleStartedEvent>(Handler);
            Assert.IsTrue(captured.HasValue, "BattleStartedEvent should be published.");
            Assert.AreEqual(1, captured.Value.allies.Length);
            Assert.AreEqual(1, captured.Value.enemies.Length);
            Assert.IsTrue(manager.IsBattleActive);
        }

        [Test]
        public void StartBattle_BeginsFirstTurn_OnFastestUnit()
        {
            var manager = MakeManager();
            var fast = MakeUnit("Fast", true, speed: 50);   // dominates regardless of d6
            var slow = MakeUnit("Slow", false, speed: 1);

            CombatUnit firstTurn = null;
            manager.OnTurnStart += u => { if (firstTurn == null) firstTurn = u; };

            manager.StartBattle(new[] { fast }, new[] { slow });

            Assert.AreSame(fast, firstTurn, "The fastest unit should take the first turn.");
            Assert.AreSame(fast, manager.ActiveUnit);
        }

        [Test]
        public void EndBattle_PublishesBattleEndedEvent_AndDeactivates()
        {
            var manager = MakeManager();
            manager.StartBattle(
                new[] { MakeUnit("Hero", true, 8) },
                new[] { MakeUnit("Goblin", false, 6) });

            BattleEndedEvent? captured = null;
            void Handler(BattleEndedEvent e) => captured = e;
            EventBus.Subscribe<BattleEndedEvent>(Handler);

            manager.EndBattle(new BattleResult { outcome = BattleOutcome.Fled });

            EventBus.Unsubscribe<BattleEndedEvent>(Handler);
            Assert.IsTrue(captured.HasValue, "BattleEndedEvent should be published.");
            Assert.AreEqual(BattleOutcome.Fled, captured.Value.result.outcome);
            Assert.IsFalse(manager.IsBattleActive);
        }

        [Test]
        public void StartBattle_TransitionsGameState_ToBattle()
        {
            using var gm = new GameManagerScope();
            var manager = MakeManager();

            manager.StartBattle(
                new[] { MakeUnit("Hero", true, 8) },
                new[] { MakeUnit("Goblin", false, 6) });

            Assert.AreEqual(GameState.Battle, GameManager.Instance.CurrentState);
        }

        [Test]
        public void EndBattle_RestoresGameState_ToExploring()
        {
            using var gm = new GameManagerScope();
            var manager = MakeManager();

            manager.StartBattle(
                new[] { MakeUnit("Hero", true, 8) },
                new[] { MakeUnit("Goblin", false, 6) });
            manager.EndBattle(new BattleResult { outcome = BattleOutcome.Victory });

            Assert.AreEqual(GameState.Exploring, GameManager.Instance.CurrentState);
        }

        [Test]
        public void Battle_EndsInVictory_WhenAllEnemiesDie()
        {
            var manager = MakeManager();
            var hero = MakeUnit("Hero", true, speed: 50, hp: 100);
            var goblin = MakeUnit("Goblin", false, speed: 1, hp: 10);

            BattleEndedEvent? captured = null;
            void Handler(BattleEndedEvent e) => captured = e;
            EventBus.Subscribe<BattleEndedEvent>(Handler);

            manager.StartBattle(new[] { hero }, new[] { goblin });
            goblin.TakeDamage(999, DamageType.Physical); // enemy down
            manager.AdvanceTurn();                        // manager detects the wipe

            EventBus.Unsubscribe<BattleEndedEvent>(Handler);
            Assert.IsTrue(captured.HasValue, "Battle should end when the enemy side is wiped.");
            Assert.AreEqual(BattleOutcome.Victory, captured.Value.result.outcome);
        }

        [Test]
        public void Battle_EndsInDefeat_WhenAllAlliesDie()
        {
            var manager = MakeManager();
            var hero = MakeUnit("Hero", true, speed: 50, hp: 10);
            var goblin = MakeUnit("Goblin", false, speed: 1, hp: 100);

            BattleEndedEvent? captured = null;
            void Handler(BattleEndedEvent e) => captured = e;
            EventBus.Subscribe<BattleEndedEvent>(Handler);

            manager.StartBattle(new[] { hero }, new[] { goblin });
            hero.TakeDamage(999, DamageType.Physical); // ally down
            manager.AdvanceTurn();

            EventBus.Unsubscribe<BattleEndedEvent>(Handler);
            Assert.IsTrue(captured.HasValue, "Battle should end when the ally side is wiped.");
            Assert.AreEqual(BattleOutcome.Defeat, captured.Value.result.outcome);
        }

        /// <summary>
        /// Spins up a real <see cref="GameManager"/> for state-transition tests and tears it down
        /// (Awake assigns the static Instance; OnDestroy clears it).
        /// </summary>
        private sealed class GameManagerScope : System.IDisposable
        {
            private readonly GameObject _go;
            public GameManagerScope()
            {
                _go = new GameObject("GameManager");
                var gm = _go.AddComponent<GameManager>();
                // Assign the singleton directly. We deliberately do NOT call Awake: it invokes
                // DontDestroyOnLoad and Destroy, which are play-mode-only and throw in EditMode.
                // ChangeState is pure state + EventBus, so it works fine without Awake.
                SetInstance(gm);
                GameManager.Instance.ChangeState(GameState.Exploring);
            }
            public void Dispose()
            {
                SetInstance(null);
                Object.DestroyImmediate(_go);
            }
            private static void SetInstance(GameManager value) =>
                typeof(GameManager)
                    .GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .SetValue(null, value);
        }
    }
}
