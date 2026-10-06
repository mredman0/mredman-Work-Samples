using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;

public abstract class Skill : ScriptableObject
{
    public event Action<int> LevelChanged;

    [Header("Settings")]
    public string Id;
    public int Order;
    public LocalizedString DisplayName;
    public LocalizedString Description;
    public Sprite Icon;
    public Color IconTint = Color.white;

    public int MaxLevel;
    public List<long> CostPerLevel;
    public long CostFormulaBaseCost = 0;
    public long CostFormulaCostIncreasePerLevel = 0;
    public List<Skill> Dependencies;

    public int CurrentLevel { get; set; }

    public void SetLevel(int level)
    {
        level = Mathf.Clamp(level, 0, MaxLevel);
        CurrentLevel = level;
        Apply();
        LevelChanged?.Invoke(CurrentLevel);
    }

    public bool Owned => CurrentLevel > 0;
    public bool MaxedOut => CurrentLevel >= MaxLevel;
    public bool DependenciesMet => Dependencies.All(s => s.Owned);
    public long CostToUpgrade => UpgradeCostAtLevel(CurrentLevel);
    public long UpgradeCostAtLevel(int level) => CostFormulaBaseCost > 0 ?
        CostFormulaBaseCost + (CostFormulaCostIncreasePerLevel * level) :
        level >= MaxLevel ? long.MaxValue : CostPerLevel[level];

    public abstract void Apply();

    public BonusSource GetBonusSource(SkillBonusSource skillBonusSource) =>
        skillBonusSource switch
        {
            SkillBonusSource.T1 => BonusSource.SkillT1,
            SkillBonusSource.T2 => BonusSource.SkillT2,
            SkillBonusSource.TInf => BonusSource.SkillTInf,
            _ => BonusSource.SkillT0,
        };
}

public enum SkillBonusSource
{
    T0,
    T1,
    T2,

    TInf = int.MaxValue
}