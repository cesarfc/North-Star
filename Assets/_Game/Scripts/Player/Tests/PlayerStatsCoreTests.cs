using NorthStar.Player.Stats;
using NUnit.Framework;

namespace NorthStar.Player.Tests
{
    /// <summary>
    /// EditMode unit tests for <see cref="PlayerStatsCore"/> — the pure,
    /// engine-free stat logic behind the PlayerStats MonoBehaviour. Covers
    /// HP/MP/gold modification, exp/level-up thresholds, and the death event.
    /// </summary>
    public class PlayerStatsCoreTests
    {
        // Deterministic test subject: 100 HP, 50 MP, +20 HP/+10 MP per level,
        // 100 exp to L2, 1.0 growth (so each level needs a flat 100 more).
        private static PlayerStatsCore MakeCore() =>
            new PlayerStatsCore(
                baseMaxHP: 100, baseMaxMP: 50,
                hpPerLevel: 20, mpPerLevel: 10,
                baseExpToLevel: 100, expGrowth: 1.0f,
                startingGold: 50);

        // ── Construction ──────────────────────────────────────────────────

        [Test]
        public void NewCore_StartsFullAtLevelOne()
        {
            var c = MakeCore();
            Assert.AreEqual(1, c.Level);
            Assert.AreEqual(100, c.MaxHP);
            Assert.AreEqual(100, c.CurrentHP);
            Assert.AreEqual(50, c.MaxMP);
            Assert.AreEqual(50, c.CurrentMP);
            Assert.AreEqual(50, c.Gold);
            Assert.AreEqual(0, c.Exp);
            Assert.IsFalse(c.IsDead);
        }

        // ── ModifyHP ──────────────────────────────────────────────────────

        [Test]
        public void ModifyHP_Damage_ReducesAndReturnsNewHP()
        {
            var c = MakeCore();
            int hp = c.ModifyHP(-30);
            Assert.AreEqual(70, hp);
            Assert.AreEqual(70, c.CurrentHP);
        }

        [Test]
        public void ModifyHP_Heal_ClampsToMax()
        {
            var c = MakeCore();
            c.ModifyHP(-30);
            int hp = c.ModifyHP(+1000);
            Assert.AreEqual(c.MaxHP, hp);
            Assert.AreEqual(100, c.CurrentHP);
        }

        [Test]
        public void ModifyHP_Overkill_FloorsAtZero()
        {
            var c = MakeCore();
            int hp = c.ModifyHP(-9999);
            Assert.AreEqual(0, hp);
        }

        [Test]
        public void ModifyHP_RaisesHPChangedWithCurrentAndMax()
        {
            var c = MakeCore();
            int observedCurrent = -1, observedMax = -1;
            c.HPChanged += (cur, max) => { observedCurrent = cur; observedMax = max; };
            c.ModifyHP(-25);
            Assert.AreEqual(75, observedCurrent);
            Assert.AreEqual(100, observedMax);
        }

        [Test]
        public void ModifyHP_NoNetChange_DoesNotRaiseHPChanged()
        {
            var c = MakeCore(); // already at full
            int calls = 0;
            c.HPChanged += (_, __) => calls++;
            c.ModifyHP(+10); // capped, no change
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void ModifyHP_HealAfterDeath_DoesNothing()
        {
            var c = MakeCore();
            c.ModifyHP(-100);
            Assert.IsTrue(c.IsDead);
            int hp = c.ModifyHP(+50);
            Assert.AreEqual(0, hp);
            Assert.AreEqual(0, c.CurrentHP);
        }

        // ── Death event ───────────────────────────────────────────────────

        [Test]
        public void ReachingZeroHP_RaisesDiedExactlyOnce()
        {
            var c = MakeCore();
            int deaths = 0;
            c.Died += () => deaths++;

            c.ModifyHP(-100);
            Assert.IsTrue(c.IsDead);
            Assert.AreEqual(1, deaths);

            // Further damage while already dead must not re-fire.
            c.ModifyHP(-10);
            Assert.AreEqual(1, deaths);
        }

        [Test]
        public void StayingAboveZero_DoesNotRaiseDied()
        {
            var c = MakeCore();
            bool died = false;
            c.Died += () => died = true;
            c.ModifyHP(-99);
            Assert.IsFalse(died);
            Assert.IsFalse(c.IsDead);
        }

        // ── ModifyMP ──────────────────────────────────────────────────────

        [Test]
        public void ModifyMP_SpendAndClamp()
        {
            var c = MakeCore();
            Assert.AreEqual(30, c.ModifyMP(-20));
            Assert.AreEqual(0, c.ModifyMP(-9999));
            Assert.AreEqual(c.MaxMP, c.ModifyMP(+9999));
        }

        [Test]
        public void ModifyMP_RaisesMPChanged()
        {
            var c = MakeCore();
            int observed = -1;
            c.MPChanged += (cur, _) => observed = cur;
            c.ModifyMP(-15);
            Assert.AreEqual(35, observed);
        }

        // ── ModifyGold ────────────────────────────────────────────────────

        [Test]
        public void ModifyGold_AddAndSpend()
        {
            var c = MakeCore(); // 50 gold
            Assert.AreEqual(150, c.ModifyGold(+100));
            Assert.AreEqual(50, c.ModifyGold(-100));
        }

        [Test]
        public void ModifyGold_CannotGoNegative()
        {
            var c = MakeCore(); // 50 gold
            Assert.AreEqual(0, c.ModifyGold(-9999));
        }

        [Test]
        public void ModifyGold_RaisesGoldChangedWithActualDelta()
        {
            var c = MakeCore(); // 50 gold
            int newTotal = -1, delta = 0;
            c.GoldChanged += (total, d) => { newTotal = total; delta = d; };
            c.ModifyGold(-9999); // can only spend 50
            Assert.AreEqual(0, newTotal);
            Assert.AreEqual(-50, delta);
        }

        // ── Exp / level-up thresholds ─────────────────────────────────────

        [Test]
        public void ExpRequiredForLevel_FlatGrowth_IsCumulative()
        {
            var c = MakeCore(); // 100 base, 1.0 growth
            Assert.AreEqual(0, c.ExpRequiredForLevel(1));
            Assert.AreEqual(100, c.ExpRequiredForLevel(2));
            Assert.AreEqual(200, c.ExpRequiredForLevel(3));
            Assert.AreEqual(300, c.ExpRequiredForLevel(4));
        }

        [Test]
        public void AddExp_JustUnderThreshold_DoesNotLevel()
        {
            var c = MakeCore();
            c.AddExp(99);
            Assert.AreEqual(1, c.GetLevel());
            Assert.AreEqual(99, c.Exp);
        }

        [Test]
        public void AddExp_HittingThreshold_LevelsUpOnce()
        {
            var c = MakeCore();
            int newLevel = -1;
            c.LeveledUp += (_, nl) => newLevel = nl;
            c.AddExp(100);
            Assert.AreEqual(2, c.GetLevel());
            Assert.AreEqual(2, newLevel);
        }

        [Test]
        public void AddExp_LargeAmount_CrossesMultipleLevelsAndFiresOnce()
        {
            var c = MakeCore();
            int fireCount = 0, lastOld = -1, lastNew = -1;
            c.LeveledUp += (oldL, newL) => { fireCount++; lastOld = oldL; lastNew = newL; };

            c.AddExp(350); // 0→100(L2)→200(L3)→300(L4), 350 < 400 so stops at L4
            Assert.AreEqual(4, c.GetLevel());
            Assert.AreEqual(1, fireCount);
            Assert.AreEqual(1, lastOld);
            Assert.AreEqual(4, lastNew);
        }

        [Test]
        public void LevelUp_RaisesMaxPoolsAndRefills()
        {
            var c = MakeCore();
            c.ModifyHP(-50);
            c.ModifyMP(-40);
            c.AddExp(100); // → level 2
            Assert.AreEqual(120, c.MaxHP);   // 100 + 20
            Assert.AreEqual(60, c.MaxMP);    // 50 + 10
            Assert.AreEqual(120, c.CurrentHP); // refilled
            Assert.AreEqual(60, c.CurrentMP);  // refilled
        }

        [Test]
        public void AddExp_NonPositive_Ignored()
        {
            var c = MakeCore();
            c.AddExp(0);
            c.AddExp(-50);
            Assert.AreEqual(0, c.Exp);
            Assert.AreEqual(1, c.GetLevel());
        }

        [Test]
        public void ExpGrowth_Multiplier_ScalesThresholds()
        {
            // 100 base, 1.5 growth → L2 needs 100, L3 needs 100 + 150 = 250.
            var c = new PlayerStatsCore(baseExpToLevel: 100, expGrowth: 1.5f);
            Assert.AreEqual(100, c.ExpRequiredForLevel(2));
            Assert.AreEqual(250, c.ExpRequiredForLevel(3));
        }
    }
}
