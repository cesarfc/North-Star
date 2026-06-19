using UnityEngine;

namespace NorthStar.Player
{
    /// <summary>
    /// Designer-authored configuration for the player's stat curve. Keeps all
    /// tunable numbers out of the MonoBehaviour (CLAUDE.md rule 2: data lives in
    /// ScriptableObjects). Create instances via the asset menu and assign one to
    /// <see cref="PlayerStats"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_PlayerStats_Default", menuName = "North-Star/Player/Player Stats Config")]
    public class PlayerStatsConfig : ScriptableObject
    {
        [Header("Base Pools (level 1)")]
        [SerializeField] private int _baseMaxHP = 100;
        [SerializeField] private int _baseMaxMP = 50;

        [Header("Per-Level Growth")]
        [SerializeField] private int _hpPerLevel = 20;
        [SerializeField] private int _mpPerLevel = 10;

        [Header("Experience Curve")]
        [Tooltip("Exp required to go from level 1 to level 2.")]
        [SerializeField] private int _baseExpToLevel = 100;
        [Tooltip("Threshold multiplier applied each level (>= 1).")]
        [SerializeField] private float _expGrowth = 1.5f;

        [Header("Economy")]
        [SerializeField] private int _startingGold = 0;

        /// <summary>HP cap at level 1.</summary>
        public int BaseMaxHP => _baseMaxHP;

        /// <summary>MP cap at level 1.</summary>
        public int BaseMaxMP => _baseMaxMP;

        /// <summary>HP added to the cap per level gained.</summary>
        public int HpPerLevel => _hpPerLevel;

        /// <summary>MP added to the cap per level gained.</summary>
        public int MpPerLevel => _mpPerLevel;

        /// <summary>Exp required for the first level-up.</summary>
        public int BaseExpToLevel => _baseExpToLevel;

        /// <summary>Per-level exp threshold multiplier.</summary>
        public float ExpGrowth => _expGrowth;

        /// <summary>Gold the player starts a new game with.</summary>
        public int StartingGold => _startingGold;
    }
}
