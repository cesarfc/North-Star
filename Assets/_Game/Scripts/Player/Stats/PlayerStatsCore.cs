using System;

namespace NorthStar.Player.Stats
{
    /// <summary>
    /// Pure, MonoBehaviour-free implementation of the player's stat logic
    /// (HP, MP, gold, exp, level). Holds no Unity dependencies so it can be
    /// exercised directly in EditMode unit tests. The <see cref="PlayerStats"/>
    /// MonoBehaviour owns one of these and forwards its callbacks onto the
    /// EventBus — keeping the rules separable from the engine glue.
    /// </summary>
    public class PlayerStatsCore
    {
        /// <summary>Maximum hit points at the current level.</summary>
        public int MaxHP { get; private set; }

        /// <summary>Current hit points. Clamped to [0, MaxHP].</summary>
        public int CurrentHP { get; private set; }

        /// <summary>Maximum magic points at the current level.</summary>
        public int MaxMP { get; private set; }

        /// <summary>Current magic points. Clamped to [0, MaxMP].</summary>
        public int CurrentMP { get; private set; }

        /// <summary>Carried gold. Never negative.</summary>
        public int Gold { get; private set; }

        /// <summary>Total accumulated experience points.</summary>
        public int Exp { get; private set; }

        /// <summary>Current level (1-based).</summary>
        public int Level { get; private set; }

        /// <summary>True once HP has reached zero and the death callback has fired.</summary>
        public bool IsDead { get; private set; }

        // ── Per-level growth ──────────────────────────────────────────────
        private readonly int _baseMaxHP;
        private readonly int _baseMaxMP;
        private readonly int _hpPerLevel;
        private readonly int _mpPerLevel;
        private readonly int _baseExpToLevel;
        private readonly float _expGrowth;

        // ── Decoupled callbacks (the MonoBehaviour wires these to EventBus) ─
        /// <summary>Raised whenever current HP changes. Args: (current, max).</summary>
        public event Action<int, int> HPChanged;

        /// <summary>Raised whenever current MP changes. Args: (current, max).</summary>
        public event Action<int, int> MPChanged;

        /// <summary>Raised whenever gold changes. Args: (newTotal, delta).</summary>
        public event Action<int, int> GoldChanged;

        /// <summary>Raised when a level is gained. Args: (oldLevel, newLevel).</summary>
        public event Action<int, int> LeveledUp;

        /// <summary>Raised exactly once, when HP first reaches zero.</summary>
        public event Action Died;

        /// <summary>
        /// Create a stat block. Defaults model a typical action-RPG curve; the
        /// MonoBehaviour passes Inspector-configured values from a ScriptableObject.
        /// </summary>
        /// <param name="baseMaxHP">HP cap at level 1.</param>
        /// <param name="baseMaxMP">MP cap at level 1.</param>
        /// <param name="hpPerLevel">HP added to the cap per level gained.</param>
        /// <param name="mpPerLevel">MP added to the cap per level gained.</param>
        /// <param name="baseExpToLevel">Exp required to go from level 1 to level 2.</param>
        /// <param name="expGrowth">Multiplier applied to the threshold each level (>= 1).</param>
        /// <param name="startingGold">Gold the player starts with.</param>
        public PlayerStatsCore(
            int baseMaxHP = 100,
            int baseMaxMP = 50,
            int hpPerLevel = 20,
            int mpPerLevel = 10,
            int baseExpToLevel = 100,
            float expGrowth = 1.5f,
            int startingGold = 0)
        {
            _baseMaxHP = Math.Max(1, baseMaxHP);
            _baseMaxMP = Math.Max(0, baseMaxMP);
            _hpPerLevel = Math.Max(0, hpPerLevel);
            _mpPerLevel = Math.Max(0, mpPerLevel);
            _baseExpToLevel = Math.Max(1, baseExpToLevel);
            _expGrowth = expGrowth < 1f ? 1f : expGrowth;

            Level = 1;
            MaxHP = _baseMaxHP;
            MaxMP = _baseMaxMP;
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
            Gold = Math.Max(0, startingGold);
            Exp = 0;
            IsDead = false;
        }

        /// <summary>
        /// Total cumulative exp required to reach <paramref name="level"/> from level 1.
        /// Level 1 requires 0; each subsequent threshold scales by <c>expGrowth</c>.
        /// </summary>
        public int ExpRequiredForLevel(int level)
        {
            if (level <= 1) return 0;

            double total = 0;
            double step = _baseExpToLevel;
            for (int l = 1; l < level; l++)
            {
                total += step;
                step *= _expGrowth;
            }
            return (int)total;
        }

        /// <summary>
        /// Apply a signed change to HP (negative damages, positive heals).
        /// Clamps to [0, MaxHP], raises <see cref="HPChanged"/>, and raises
        /// <see cref="Died"/> exactly once if HP hits zero. Returns the new HP.
        /// Healing has no effect once dead.
        /// </summary>
        public int ModifyHP(int delta)
        {
            if (IsDead && delta > 0) return CurrentHP;

            int previous = CurrentHP;
            CurrentHP = Clamp(CurrentHP + delta, 0, MaxHP);

            if (CurrentHP != previous)
                HPChanged?.Invoke(CurrentHP, MaxHP);

            if (CurrentHP == 0 && !IsDead)
            {
                IsDead = true;
                Died?.Invoke();
            }

            return CurrentHP;
        }

        /// <summary>
        /// Apply a signed change to MP. Clamps to [0, MaxMP], raises
        /// <see cref="MPChanged"/>, and returns the new MP.
        /// </summary>
        public int ModifyMP(int delta)
        {
            int previous = CurrentMP;
            CurrentMP = Clamp(CurrentMP + delta, 0, MaxMP);

            if (CurrentMP != previous)
                MPChanged?.Invoke(CurrentMP, MaxMP);

            return CurrentMP;
        }

        /// <summary>
        /// Apply a signed change to gold. Floors at zero, raises
        /// <see cref="GoldChanged"/> with the actual applied delta, and returns
        /// the new total.
        /// </summary>
        public int ModifyGold(int delta)
        {
            int previous = Gold;
            Gold = Math.Max(0, Gold + delta);

            int applied = Gold - previous;
            if (applied != 0)
                GoldChanged?.Invoke(Gold, applied);

            return Gold;
        }

        /// <summary>
        /// Award experience and resolve any resulting level-ups. May cross
        /// several thresholds in one call; <see cref="LeveledUp"/> fires once
        /// per level gained. Negative or zero amounts are ignored.
        /// </summary>
        public void AddExp(int amount)
        {
            if (amount <= 0) return;

            Exp += amount;

            int oldLevel = Level;
            while (Exp >= ExpRequiredForLevel(Level + 1))
            {
                Level++;
                ApplyLevelGrowth();
            }

            if (Level != oldLevel)
                LeveledUp?.Invoke(oldLevel, Level);
        }

        /// <summary>Returns the current level. Mirrors <see cref="Level"/> for the public API.</summary>
        public int GetLevel() => Level;

        /// <summary>
        /// Recompute caps for the new level and top both pools off. Called once
        /// per level gained inside <see cref="AddExp"/>.
        /// </summary>
        private void ApplyLevelGrowth()
        {
            int levelsGained = Level - 1;
            MaxHP = _baseMaxHP + (_hpPerLevel * levelsGained);
            MaxMP = _baseMaxMP + (_mpPerLevel * levelsGained);
            // Leveling up fully restores the player.
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
            HPChanged?.Invoke(CurrentHP, MaxHP);
            MPChanged?.Invoke(CurrentMP, MaxMP);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
