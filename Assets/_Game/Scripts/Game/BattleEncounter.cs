using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A world trigger that runs a playable battle through the real Battle module. On interaction
/// it spins up a BattleManager, a hero and some enemies, then drives the turn loop:
/// on the hero's turn an IMGUI action menu offers the wired <see cref="AbilityData"/> actions
/// (attack / fireball / heal, resolved by <see cref="CombatUnit.UseAbility"/>), while enemy
/// turns are played by a simple attack-the-hero AI after a short delay. An IMGUI overlay shows
/// the live battle state and the outcome.
///
/// Composition-root glue (NorthStar.Game): input UI + enemy AI live here; rules, turn order,
/// damage, statuses and win/lose stay in the Battle module.
/// </summary>
public class BattleEncounter : MonoBehaviour, IInteractable
{
    [SerializeField] private float _turnDelay = 0.6f;

    [Tooltip("Actions offered on the hero's turn (BasicAttack / Fireball / Heal…). All damage " +
             "and MP costs resolve through CombatUnit.UseAbility with this data.")]
    [SerializeField] private AbilityData[] _heroAbilities;

    private BattleManager _manager;
    private readonly List<CombatUnit> _allies = new();
    private readonly List<CombatUnit> _enemies = new();
    private bool _running;
    private bool _awaitingPlayer;
    private CombatUnit _activeUnit;
    private string _result;
    private float _resultUntil;

    /// <inheritdoc />
    public string InteractionPrompt => "Fight! (E)";

    /// <inheritdoc />
    public void Interact(GameObject interactor)
    {
        if (_running) return;
        StartEncounter();
    }

    private void StartEncounter()
    {
        var mgrGo = new GameObject("BattleManager (encounter)");
        _manager = mgrGo.AddComponent<BattleManager>();
        _manager.OnTurnStart += HandleTurnStart;
        _manager.OnBattleEnd += HandleBattleEnd;

        _allies.Clear();
        _enemies.Clear();
        _allies.Add(MakeUnit("Hero", true, hp: 60, mp: 30, atk: 14, def: 6, spd: 9));
        _enemies.Add(MakeUnit("Goblin", false, hp: 28, mp: 0, atk: 9, def: 3, spd: 7));
        _enemies.Add(MakeUnit("Goblin Brute", false, hp: 44, mp: 0, atk: 11, def: 5, spd: 4));

        _running = true;
        _awaitingPlayer = false;
        _manager.StartBattle(_allies.ToArray(), _enemies.ToArray());
    }

    private CombatUnit MakeUnit(string unitName, bool ally, int hp, int mp, int atk, int def, int spd)
    {
        var go = new GameObject($"Unit_{unitName}");
        var u = go.AddComponent<CombatUnit>();
        u.unitName = unitName;
        u.isPlayerControlled = ally;
        u.baseMaxHP = hp;
        u.baseMaxMP = mp;
        u.baseAttack = atk;
        u.baseDefense = def;
        u.baseSpeed = spd;
        u.ResetRuntimeStats(); // re-sync CurrentHP/MP to the stats we just set (Awake ran with defaults)
        return u;
    }

    // ── turn flow ─────────────────────────────────────────────────────────────

    private void HandleTurnStart(CombatUnit active)
    {
        if (!_running || active == null) return;
        _activeUnit = active;

        if (active.isPlayerControlled)
        {
            if (active.IsStunned())
            {
                StartCoroutine(CoSkipTurn($"{active.unitName} is stunned!"));
                return;
            }
            _awaitingPlayer = true; // OnGUI shows the action menu until an action resolves
        }
        else
        {
            StartCoroutine(CoEnemyTurn(active));
        }
    }

    private IEnumerator CoEnemyTurn(CombatUnit active)
    {
        yield return new WaitForSeconds(_turnDelay);
        if (!_running || _manager == null || !_manager.IsBattleActive) yield break;

        var target = FirstLiving(_allies);
        if (target != null && !active.IsStunned())
            target.TakeDamage(active.Attack, DamageType.Physical);

        yield return new WaitForSeconds(_turnDelay * 0.5f);
        if (_manager != null && _manager.IsBattleActive) _manager.AdvanceTurn();
    }

    private IEnumerator CoSkipTurn(string message)
    {
        _result = message;
        _resultUntil = Time.time + _turnDelay * 2f;
        yield return new WaitForSeconds(_turnDelay);
        if (_manager != null && _manager.IsBattleActive) _manager.AdvanceTurn();
    }

    /// <summary>Resolve a chosen hero action through the Battle module, then advance the turn.</summary>
    private void PlayHeroAction(AbilityData ability, CombatUnit target)
    {
        if (!_awaitingPlayer || _activeUnit == null) return;

        CombatUnit[] targets = ability.isMultiTarget
            ? LivingUnits(_enemies).ToArray()
            : new[] { target };
        if (!_activeUnit.UseAbility(ability, targets))
        {
            _result = $"Not enough MP for {ability.displayName}!";
            _resultUntil = Time.time + 1.5f;
            return; // keep the menu open; the player picks something else
        }

        _awaitingPlayer = false;
        StartCoroutine(CoFinishHeroTurn());
    }

    private IEnumerator CoFinishHeroTurn()
    {
        yield return new WaitForSeconds(_turnDelay * 0.5f);
        if (_manager != null && _manager.IsBattleActive) _manager.AdvanceTurn();
    }

    private void HandleBattleEnd(BattleResult result)
    {
        _running = false;
        _awaitingPlayer = false;
        _result = $"Battle over — {result.outcome}!";
        _resultUntil = Time.time + 4f;

        if (_manager != null)
        {
            _manager.OnTurnStart -= HandleTurnStart;
            _manager.OnBattleEnd -= HandleBattleEnd;
            Destroy(_manager.gameObject);
            _manager = null;
        }
        foreach (var u in _allies) if (u != null) Destroy(u.gameObject);
        foreach (var e in _enemies) if (e != null) Destroy(e.gameObject);
        _allies.Clear();
        _enemies.Clear();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static CombatUnit FirstLiving(List<CombatUnit> units)
    {
        foreach (var u in units) if (u != null && u.IsAlive) return u;
        return null;
    }

    private static List<CombatUnit> LivingUnits(List<CombatUnit> units)
    {
        var living = new List<CombatUnit>();
        foreach (var u in units) if (u != null && u.IsAlive) living.Add(u);
        return living;
    }

    // ── IMGUI ─────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };

        if (_running && _manager != null)
        {
            DrawStatusPanel(style);
            if (_awaitingPlayer) DrawActionMenu();
        }

        if (Time.time < _resultUntil && !string.IsNullOrEmpty(_result))
            GUI.Box(new Rect(Screen.width / 2f - 160f, 170f, 320f, 40f), _result, style);
    }

    private void DrawStatusPanel(GUIStyle style)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"⚔ BATTLE — round {_manager.RoundNumber}");
        var active = _manager.ActiveUnit;
        sb.AppendLine($"Turn: {(active != null ? active.unitName : "—")}");
        sb.AppendLine("Allies:");
        foreach (var u in _allies)
            if (u != null) sb.AppendLine($"  {u.unitName}  HP {u.CurrentHP}/{u.MaxHP}  MP {u.CurrentMP}/{u.MaxMP}");
        sb.AppendLine("Enemies:");
        foreach (var e in _enemies)
            if (e != null) sb.Append($"  {e.unitName}  HP {e.CurrentHP}/{e.MaxHP}\n");
        GUI.Box(new Rect(Screen.width - 300f, 36f, 290f, 210f), sb.ToString(), style);
    }

    private void DrawActionMenu()
    {
        List<CombatUnit> targets = LivingUnits(_enemies);
        float h = 40f;
        foreach (AbilityData ability in _heroAbilities ?? System.Array.Empty<AbilityData>())
            h += (ability == null ? 0f : (AbilityTargetsEnemies(ability) && !ability.isMultiTarget ? targets.Count : 1) * 30f);

        GUILayout.BeginArea(new Rect(Screen.width - 300f, 256f, 290f, h), GUI.skin.box);
        GUILayout.Label("Choose an action:");
        foreach (AbilityData ability in _heroAbilities ?? System.Array.Empty<AbilityData>())
        {
            if (ability == null) continue;
            string cost = ability.mpCost > 0 ? $" ({ability.mpCost} MP)" : string.Empty;

            if (!AbilityTargetsEnemies(ability))
            {
                if (GUILayout.Button($"{ability.displayName}{cost} → self", GUILayout.Height(26f)))
                    PlayHeroAction(ability, _activeUnit);
            }
            else if (ability.isMultiTarget)
            {
                if (GUILayout.Button($"{ability.displayName}{cost} → all enemies", GUILayout.Height(26f)))
                    PlayHeroAction(ability, null);
            }
            else
            {
                foreach (CombatUnit target in targets)
                    if (GUILayout.Button($"{ability.displayName}{cost} → {target.unitName}", GUILayout.Height(26f)))
                        PlayHeroAction(ability, target);
            }
        }
        GUILayout.EndArea();
    }

    /// <summary>Heals/buffs target the caster; every other damage type targets enemies.</summary>
    private static bool AbilityTargetsEnemies(AbilityData ability) =>
        ability.damageType != DamageType.Heal && ability.damageType != DamageType.Buff;

    private void OnDestroy()
    {
        if (_manager != null)
        {
            _manager.OnTurnStart -= HandleTurnStart;
            _manager.OnBattleEnd -= HandleBattleEnd;
        }
    }
}
