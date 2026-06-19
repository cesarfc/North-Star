using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle HUD: action menu, turn-order display, floating damage numbers and per-unit HP bars.
/// Listens to EventBus battle events (start/end, HP change, death) for decoupled updates, and is
/// wired to a <see cref="BattleManager"/>'s turn events for the active-unit action menu.
/// </summary>
public class BattleUI : MonoBehaviour
{
    [Header("Roots")]
    [Tooltip("Container shown while a battle is active and hidden otherwise.")]
    [SerializeField] private GameObject _battleHudRoot;
    [Tooltip("Action menu shown on a player unit's turn.")]
    [SerializeField] private GameObject _actionMenuRoot;

    [Header("Turn Order")]
    [Tooltip("Text listing the upcoming turn order / active unit.")]
    [SerializeField] private TMP_Text _turnOrderLabel;
    [Tooltip("Text naming the unit whose turn it currently is.")]
    [SerializeField] private TMP_Text _activeUnitLabel;

    [Header("HP Bars")]
    [Tooltip("Prefab with a Slider (and optional TMP_Text) used to display one unit's HP.")]
    [SerializeField] private HpBar _hpBarPrefab;
    [Tooltip("Parent the HP bars are instantiated under.")]
    [SerializeField] private RectTransform _hpBarContainer;

    [Header("Damage Numbers")]
    [Tooltip("Prefab for a floating damage/heal number.")]
    [SerializeField] private DamageNumber _damageNumberPrefab;
    [Tooltip("Parent for spawned damage numbers (usually a world-space or screen-space canvas).")]
    [SerializeField] private RectTransform _damageNumberContainer;

    private BattleManager _battleManager;
    private readonly Dictionary<ICombatant, HpBar> _hpBars = new();

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
        EventBus.Subscribe<UnitHPChangedEvent>(OnUnitHPChanged);
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
        EventBus.Unsubscribe<UnitHPChangedEvent>(OnUnitHPChanged);
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
        UnbindManager();
    }

    /// <summary>
    /// Bind this HUD to a <see cref="BattleManager"/> so the action menu and turn labels follow
    /// its turn events. Safe to call again to rebind; unbinds the previous manager first.
    /// </summary>
    /// <param name="manager">The active battle manager.</param>
    public void Bind(BattleManager manager)
    {
        UnbindManager();
        _battleManager = manager;
        if (_battleManager == null) return;

        _battleManager.OnTurnStart += HandleTurnStart;
        _battleManager.OnTurnEnd   += HandleTurnEnd;
    }

    private void UnbindManager()
    {
        if (_battleManager == null) return;
        _battleManager.OnTurnStart -= HandleTurnStart;
        _battleManager.OnTurnEnd   -= HandleTurnEnd;
        _battleManager = null;
    }

    private void OnBattleStarted(BattleStartedEvent e)
    {
        SetVisible(_battleHudRoot, true);
        ClearHpBars();

        BuildHpBars(e.allies);
        BuildHpBars(e.enemies);
        RefreshTurnOrderLabel(e.allies, e.enemies);
        SetVisible(_actionMenuRoot, false);
    }

    private void OnBattleEnded(BattleEndedEvent e)
    {
        SetVisible(_actionMenuRoot, false);
        SetVisible(_battleHudRoot, false);
        ClearHpBars();
        if (_activeUnitLabel != null) _activeUnitLabel.text = string.Empty;
    }

    private void OnUnitHPChanged(UnitHPChangedEvent e)
    {
        if (e.unit != null && _hpBars.TryGetValue(e.unit, out var bar) && bar != null)
            bar.SetValue(e.current, e.max);
    }

    private void OnUnitDied(UnitDiedEvent e)
    {
        if (e.unit != null && _hpBars.TryGetValue(e.unit, out var bar) && bar != null)
            bar.SetDead();
    }

    private void HandleTurnStart(CombatUnit unit)
    {
        if (_activeUnitLabel != null)
            _activeUnitLabel.text = unit != null ? $"{unit.UnitName}'s turn" : string.Empty;

        // Only show the action menu for player-controlled units; enemies are AI-driven.
        SetVisible(_actionMenuRoot, unit != null && unit.IsPlayerControlled);
    }

    private void HandleTurnEnd(CombatUnit unit)
    {
        SetVisible(_actionMenuRoot, false);
    }

    private void BuildHpBars(ICombatant[] units)
    {
        if (units == null || _hpBarPrefab == null || _hpBarContainer == null) return;

        foreach (var unit in units)
        {
            if (unit == null || _hpBars.ContainsKey(unit)) continue;

            var bar = Instantiate(_hpBarPrefab, _hpBarContainer);
            bar.Initialize(unit.UnitName, unit.CurrentHP, unit.MaxHP);
            _hpBars[unit] = bar;
        }
    }

    private void RefreshTurnOrderLabel(ICombatant[] allies, ICombatant[] enemies)
    {
        if (_turnOrderLabel == null) return;

        var names = new List<string>();
        AppendNames(allies, names);
        AppendNames(enemies, names);
        _turnOrderLabel.text = string.Join("  >  ", names);
    }

    private static void AppendNames(ICombatant[] units, List<string> into)
    {
        if (units == null) return;
        foreach (var u in units)
            if (u != null) into.Add(u.UnitName);
    }

    /// <summary>
    /// Spawn a floating damage (or heal) number at the given screen/world anchor position.
    /// </summary>
    /// <param name="anchoredPosition">Position in the damage-number container's space.</param>
    /// <param name="amount">Magnitude to display.</param>
    /// <param name="isHeal">True to style as a heal rather than damage.</param>
    public void ShowDamageNumber(Vector2 anchoredPosition, int amount, bool isHeal)
    {
        if (_damageNumberPrefab == null || _damageNumberContainer == null) return;

        var number = Instantiate(_damageNumberPrefab, _damageNumberContainer);
        number.Play(anchoredPosition, amount, isHeal);
    }

    private void ClearHpBars()
    {
        foreach (var bar in _hpBars.Values)
            if (bar != null) Destroy(bar.gameObject);
        _hpBars.Clear();
    }

    private static void SetVisible(GameObject go, bool visible)
    {
        if (go != null) go.SetActive(visible);
    }
}
