using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A world trigger that runs a self-contained demo battle through the real Battle module.
/// On interaction it spins up a BattleManager, a small player party and some enemies, then
/// auto-drives the turn loop (each unit basic-attacks an opposing unit) until BattleManager
/// resolves Victory/Defeat. An IMGUI overlay shows the live battle state and the outcome.
///
/// This is composition-root glue (NorthStar.Game): the "auto driver" stands in for the player's
/// BattleUI input + enemy AI, neither of which is wired in this slice. It demonstrates the Battle
/// system (turn order, damage, win/lose, GameState Battle↔Exploring) without authoring a combat UI.
/// </summary>
public class BattleEncounter : MonoBehaviour, IInteractable
{
    [SerializeField] private float _turnDelay = 0.6f;

    private BattleManager _manager;
    private readonly List<CombatUnit> _allies = new();
    private readonly List<CombatUnit> _enemies = new();
    private bool _running;
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
        _allies.Add(MakeUnit("Hero", true, hp: 60, atk: 14, def: 6, spd: 9));
        _enemies.Add(MakeUnit("Goblin", false, hp: 28, atk: 9, def: 3, spd: 7));
        _enemies.Add(MakeUnit("Goblin Brute", false, hp: 44, atk: 11, def: 5, spd: 4));

        _running = true;
        _manager.StartBattle(_allies.ToArray(), _enemies.ToArray());
    }

    private CombatUnit MakeUnit(string unitName, bool ally, int hp, int atk, int def, int spd)
    {
        var go = new GameObject($"Unit_{unitName}");
        var u = go.AddComponent<CombatUnit>();
        u.unitName = unitName;
        u.isPlayerControlled = ally;
        u.baseMaxHP = hp;
        u.baseAttack = atk;
        u.baseDefense = def;
        u.baseSpeed = spd;
        u.ResetRuntimeStats(); // re-sync CurrentHP to the stats we just set (Awake ran with defaults)
        return u;
    }

    private void HandleTurnStart(CombatUnit active)
    {
        if (_running && active != null) StartCoroutine(CoTakeTurn(active));
    }

    private IEnumerator CoTakeTurn(CombatUnit active)
    {
        yield return new WaitForSeconds(_turnDelay);
        if (!_running || _manager == null || !_manager.IsBattleActive) yield break;

        var target = FirstLiving(active.isPlayerControlled ? _enemies : _allies);
        if (target != null)
            target.TakeDamage(active.Attack, DamageType.Physical); // basic attack

        yield return new WaitForSeconds(_turnDelay * 0.5f);
        if (_manager != null && _manager.IsBattleActive) _manager.AdvanceTurn();
    }

    private void HandleBattleEnd(BattleResult result)
    {
        _running = false;
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

    private static CombatUnit FirstLiving(List<CombatUnit> units)
    {
        foreach (var u in units) if (u != null && u.IsAlive) return u;
        return null;
    }

    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };

        if (_running && _manager != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"⚔ BATTLE — round {_manager.RoundNumber}");
            var active = _manager.ActiveUnit;
            sb.AppendLine($"Turn: {(active != null ? active.unitName : "—")}");
            sb.AppendLine("Allies:");
            foreach (var u in _allies) if (u != null) sb.AppendLine($"  {u.unitName}  {u.CurrentHP}/{u.MaxHP}");
            sb.AppendLine("Enemies:");
            foreach (var e in _enemies) if (e != null) sb.Append($"  {e.unitName}  {e.CurrentHP}/{e.MaxHP}\n");
            GUI.Box(new Rect(Screen.width - 280f, 36f, 270f, 200f), sb.ToString(), style);
        }
        else if (Time.time < _resultUntil && !string.IsNullOrEmpty(_result))
        {
            GUI.Box(new Rect(Screen.width / 2f - 160f, 170f, 320f, 40f), _result, style);
        }
    }

    private void OnDestroy()
    {
        if (_manager != null)
        {
            _manager.OnTurnStart -= HandleTurnStart;
            _manager.OnBattleEnd -= HandleBattleEnd;
        }
    }
}
