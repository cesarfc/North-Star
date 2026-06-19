using System;
using NorthStar.Player.Stats;
using UnityEngine;

namespace NorthStar.Player
{
    /// <summary>
    /// Player health, magic, gold, experience and level. The actual rules live
    /// in the pure, testable <see cref="PlayerStatsCore"/>; this MonoBehaviour is
    /// the engine glue that exposes the frozen INTERFACE.md API and republishes
    /// changes onto the EventBus (<see cref="PlayerHPChangedEvent"/>,
    /// <see cref="PlayerDiedEvent"/>, etc.) for the rest of the game.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Stat curve definition. If left empty, sensible defaults are used.")]
        [SerializeField] private PlayerStatsConfig _config;

        private PlayerStatsCore _core;

        /// <summary>Raised when the player dies (HP reaches zero).</summary>
        public event Action OnDeath;

        /// <summary>Raised when a new level is reached. Arg: new level.</summary>
        public event Action<int> OnLevelUp;

        /// <summary>Raised when HP changes. Args: (current, max).</summary>
        public event Action<int, int> OnHPChanged;

        /// <summary>Maximum hit points at the current level.</summary>
        public int MaxHP => _core.MaxHP;

        /// <summary>Current hit points.</summary>
        public int CurrentHP => _core.CurrentHP;

        /// <summary>Maximum magic points at the current level.</summary>
        public int MaxMP => _core.MaxMP;

        /// <summary>Current magic points.</summary>
        public int CurrentMP => _core.CurrentMP;

        /// <summary>Carried gold.</summary>
        public int Gold => _core.Gold;

        /// <summary>Total accumulated experience.</summary>
        public int Exp => _core.Exp;

        /// <summary>Current level.</summary>
        public int Level => _core.Level;

        private void Awake()
        {
            _core = BuildCore();
            WireCore();
        }

        private void OnDestroy()
        {
            UnwireCore();
        }

        /// <summary>
        /// Construct the pure stat core from the assigned config (or defaults
        /// when none is set). Kept separate so it can be reused by save/load.
        /// </summary>
        private PlayerStatsCore BuildCore()
        {
            if (_config != null)
            {
                return new PlayerStatsCore(
                    _config.BaseMaxHP,
                    _config.BaseMaxMP,
                    _config.HpPerLevel,
                    _config.MpPerLevel,
                    _config.BaseExpToLevel,
                    _config.ExpGrowth,
                    _config.StartingGold);
            }
            return new PlayerStatsCore();
        }

        private void WireCore()
        {
            _core.HPChanged += HandleHPChanged;
            _core.GoldChanged += HandleGoldChanged;
            _core.LeveledUp += HandleLeveledUp;
            _core.Died += HandleDied;
        }

        private void UnwireCore()
        {
            if (_core == null) return;
            _core.HPChanged -= HandleHPChanged;
            _core.GoldChanged -= HandleGoldChanged;
            _core.LeveledUp -= HandleLeveledUp;
            _core.Died -= HandleDied;
        }

        /// <summary>Apply a signed HP change (negative damages). Returns the new HP.</summary>
        public int ModifyHP(int delta) => _core.ModifyHP(delta);

        /// <summary>Apply a signed MP change. Returns the new MP.</summary>
        public int ModifyMP(int delta) => _core.ModifyMP(delta);

        /// <summary>Apply a signed gold change (floored at zero). Returns the new total.</summary>
        public int ModifyGold(int delta) => _core.ModifyGold(delta);

        /// <summary>Award experience and resolve any resulting level-ups.</summary>
        public void AddExp(int amount) => _core.AddExp(amount);

        /// <summary>Returns the current level.</summary>
        public int GetLevel() => _core.GetLevel();

        // ── Core → C# events + EventBus bridge ────────────────────────────

        private void HandleHPChanged(int current, int max)
        {
            OnHPChanged?.Invoke(current, max);
            EventBus.Publish(new PlayerHPChangedEvent { current = current, max = max });
        }

        private void HandleGoldChanged(int newTotal, int delta)
        {
            EventBus.Publish(new PlayerGoldChangedEvent { newTotal = newTotal, delta = delta });
        }

        private void HandleLeveledUp(int oldLevel, int newLevel)
        {
            OnLevelUp?.Invoke(newLevel);
            EventBus.Publish(new PlayerLeveledUpEvent { oldLevel = oldLevel, newLevel = newLevel });
        }

        private void HandleDied()
        {
            OnDeath?.Invoke();
            EventBus.Publish(new PlayerDiedEvent { position = transform.position });
        }
    }
}
