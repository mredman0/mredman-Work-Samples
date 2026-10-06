using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

public class BonusManager : MonoBehaviour
{
    public static BonusManager Instance;

	public event Action<BonusType, Big> BigBonusChanged;
	public event Action<BonusType, float> FloatBonusChanged;
	public event Action<BonusType, bool> BoolBonusChanged;

	public Dictionary<BonusType, Big> BigBonusTotals = new Dictionary<BonusType, Big>();
	public Dictionary<BonusType, float> FloatBonusTotals = new Dictionary<BonusType, float>();
	public Dictionary<BonusType, bool> BoolBonusTotals = new Dictionary<BonusType, bool>();

	public Dictionary<BonusType, Dictionary<BonusSource, Big>> BigBonusDictionaries = new Dictionary<BonusType, Dictionary<BonusSource, Big>>();
	public Dictionary<BonusType, Dictionary<BonusSource, float>> FloatBonusDictionaries = new Dictionary<BonusType, Dictionary<BonusSource, float>>();
	public Dictionary<BonusType, Dictionary<BonusSource, bool>> BoolBonusDictionaries = new Dictionary<BonusType, Dictionary<BonusSource, bool>>();

	public Dictionary<BonusType, Big> BigBonusDefaults = new Dictionary<BonusType, Big>()
	{
		{ BonusType.PrimaryCurrencyEarnings, Big.ONE },
		{ BonusType.PrestigeCurrencyEarnings, Big.ONE },
		{ BonusType.BoardRewardsMultiplier, Big.ONE },
        { BonusType.StartingGum, Big.ZERO },

        { BonusType.CommendationsOnGrade1Unlock, Big.ZERO },
        { BonusType.CommendationsOnGrade2Unlock, Big.ZERO },
        { BonusType.CommendationsOnGrade3Unlock, Big.ZERO },
        { BonusType.CommendationsOnGrade4Unlock, Big.ZERO },
        { BonusType.CommendationsOnGrade5Unlock, Big.ZERO },
        { BonusType.CommendationsOnGrade6Unlock, Big.ZERO },
        { BonusType.CommendationsOnGrade7Unlock, Big.ZERO },
        { BonusType.CommendationsOnGrade8Unlock, Big.ZERO },
        { BonusType.CommendationsOnGrade9Unlock, Big.ZERO },
        { BonusType.CommendationsOnGrade10Unlock, Big.ZERO },
        { BonusType.CommendationsOnGrade11Unlock, Big.ZERO },
        { BonusType.CommendationsOnGrade12Unlock, Big.ZERO },

        { BonusType.CommendationsOnCollegeUnlock, Big.ZERO },

        { BonusType.GumMultiplierWhileSponsored, Big.ONE },
        { BonusType.PassiveIncome, Big.ZERO },
        { BonusType.PotAddMultiplier, Big.ONE },

        { BonusType.ManualStreakMultiplier, Big.ONE },

        { BonusType.CompoundingPrestigeMultiplier, Big.ONE },

		{ BonusType.OfflineBoardRewardsMultiplier, Big.ONE },
    };
	public Dictionary<BonusType, float> FloatBonusDefaults = new Dictionary<BonusType, float>()
	{
		{ BonusType.BoardUnlockCost, 1 },
		{ BonusType.RewardUpgradeCost, 1 },
		{ BonusType.SpeedUpgradeCost, 1 },
		{ BonusType.IntelligenceUpgradeCost, 1 },
		{ BonusType.GoldenPlayUpgradeCost, 1 },
		{ BonusType.DoublePlayChance, 0 },
		{ BonusType.GoldenPlayEffectiveness, 0 },
		{ BonusType.GlobalGumCostMultiplier, 1 },
		{ BonusType.TurnIntervalMultiplier, 1 },
		{ BonusType.CurrencyEarningsOverGainMultiplier, 1 },
        { BonusType.PlayFasterDurationMultiplier, 1 },
        { BonusType.SkillTrickleDownGum, 1 },
        { BonusType.PrestigeExponent, 1 },
        { BonusType.PotTakeIncrease, 0 },
        { BonusType.PlayFasterAutoRenewChance, 0 },
        { BonusType.PresidentsBoardCost, 1 },
        { BonusType.KeepGumOnSpendChance, 0 },
        { BonusType.ManualStreakMaxIncrease, 0 },
    };

	private void Awake()
	{
		if (Instance)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
		DontDestroyOnLoad(gameObject);

		foreach (var enumVal in Enum.GetValues(typeof(BonusType)))
		{
			var type = (BonusType)enumVal;
			var memberInfo = typeof(BonusType).GetMember(enumVal.ToString())[0];
			if (Attribute.IsDefined(memberInfo, typeof(BigBonusAttribute)))
			{
				BigBonusDictionaries.Add(type, new Dictionary<BonusSource, Big>());
				RecalculateBonusBig(type);
			}
			if (Attribute.IsDefined(memberInfo, typeof(FloatBonusAttribute)))
			{
				FloatBonusDictionaries.Add(type, new Dictionary<BonusSource, float>());
				RecalculateBonusFloat(type);
			}
			if (Attribute.IsDefined(memberInfo, typeof(BoolBonusAttribute)))
			{
				BoolBonusDictionaries.Add(type, new Dictionary<BonusSource, bool>());
				RecalculateBonusBool(type);
			}
		}
	}

    #region API
    public Big CurrentBonusBig(BonusType type, BonusSource source) =>
        BigBonusDictionaries[type].ContainsKey(source) ?
            BigBonusDictionaries[type][source] :
            BigBonusDefaults[type];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Big TotalBonusBig(BonusType type) => BigBonusTotals[type];
	public void UpdateBonusBig(BonusType type, BonusSource source, Big val)
	{
		if(!BigBonusDictionaries.ContainsKey(type))
		{
			Debug.LogError($"No bonus dictionary of Big values given BonusType {type}");
			return;
		}
		var bonuses = BigBonusDictionaries[type];
		if(!bonuses.ContainsKey(source))
		{
			bonuses.Add(source, val);
		}
		else
        {
            if (bonuses[source] == val)
            {
                // This will be a NOP, don't recalulate or propagate events
                return;
            }
            bonuses[source] = val;
		}
		RecalculateBonusBig(type);
		Debug.Log($"{type} bonus from {source} changed to {val.ToStringShorthand()}... new total: {BigBonusTotals[type].ToStringShorthand()}");
	}

	public void RemoveBonusBig(BonusType type, BonusSource source)
	{
		if (!BigBonusDictionaries.ContainsKey(type))
		{
			Debug.LogError($"No bonus dictionary of Big values given BonusType {type}");
			return;
		}
		var bonuses = BigBonusDictionaries[type];
		if (!bonuses.ContainsKey(source))
		{
			return;
		}
		bonuses.Remove(source);
		RecalculateBonusBig(type);
		Debug.Log($"{type} bonus from {source} removed... new total: {BigBonusTotals[type].ToStringShorthand()}");
	}

    public float CurrentBonusFloat(BonusType type, BonusSource source) =>
        FloatBonusDictionaries[type].ContainsKey(source) ?
            FloatBonusDictionaries[type][source] :
            FloatBonusDefaults[type];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	public float TotalBonusFloat(BonusType type) => FloatBonusTotals[type];
	public void UpdateBonusFloat(BonusType type, BonusSource source, float val)
	{
		if (!FloatBonusDictionaries.ContainsKey(type))
		{
			Debug.LogError($"No bonus dictionary of Float values given BonusType {type}");
			return;
		}
		var bonuses = FloatBonusDictionaries[type];
		if (!bonuses.ContainsKey(source))
		{
			bonuses.Add(source, val);
		}
		else
		{
            if(bonuses[source] == val)
            {
                // This will be a NOP, don't recalulate or propagate events
                return;
            }
			bonuses[source] = val;
		}
		RecalculateBonusFloat(type);
		Debug.Log($"{type} bonus from {source} changed to {val}... new total: {FloatBonusTotals[type]}");
	}

	public void RemoveBonusFloat(BonusType type, BonusSource source)
	{
		if (!FloatBonusDictionaries.ContainsKey(type))
		{
			Debug.LogError($"No bonus dictionary of Float values given BonusType {type}");
			return;
		}
		var bonuses = FloatBonusDictionaries[type];
		if (!bonuses.ContainsKey(source))
		{
			return;
		}
		bonuses.Remove(source);
		RecalculateBonusFloat(type);
		Debug.Log($"{type} bonus from {source} removed... new total: {FloatBonusTotals[type]}");
	}

    public bool CurrentBonusBool(BonusType type, BonusSource source) =>
        BoolBonusDictionaries[type].ContainsKey(source) && BoolBonusDictionaries[type][source];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TotalBonusBool(BonusType type) => BoolBonusTotals[type];
	public void UpdateBonusBool(BonusType type, BonusSource source, bool val)
	{
		if (!BoolBonusDictionaries.ContainsKey(type))
		{
			Debug.LogError($"No bonus dictionary of bool values given BonusType {type}");
			return;
		}
		var bonuses = BoolBonusDictionaries[type];
		if (!bonuses.ContainsKey(source))
		{
			bonuses.Add(source, val);
		}
		else
        {
            if (bonuses[source] == val)
            {
                // This will be a NOP, don't recalulate or propagate events
                return;
            }
            bonuses[source] = val;
		}
		RecalculateBonusBool(type);
		Debug.Log($"{type} bonus from {source} changed to {val}... new total: {BoolBonusTotals[type]}");
	}

	public void RemoveBonusBool(BonusType type, BonusSource source)
	{
		if (!BoolBonusDictionaries.ContainsKey(type))
		{
			Debug.LogError($"No bonus dictionary of bool values given BonusType {type}");
			return;
		}
		var bonuses = BoolBonusDictionaries[type];
		if (!bonuses.ContainsKey(source))
		{
			return;
		}
		bonuses.Remove(source);
		RecalculateBonusBool(type);
		Debug.Log($"{type} bonus from {source} removed... new total: {BoolBonusTotals[type]}");
	}

    // Thought this might be needed but not right now
    //public void Prestige()
    //{
    //    // For each source that is marked as "RemoveOnPrestige"
    //    // remove all bonuses that it may currently be applying
    //    foreach (var sourceVal in Enum.GetValues(typeof(BonusSource)))
    //    {
    //        var source = (BonusSource)sourceVal;
    //        var sourceMemberInfo = typeof(BonusSource).GetMember(sourceVal.ToString())[0];
    //        if (Attribute.IsDefined(sourceMemberInfo, typeof(RemoveOnPrestigeAttribute)))
    //        {
    //            foreach(var kvp in BigBonusDictionaries)
    //            {
    //                kvp.Value.Remove(source);
    //                RecalculateBonusBig(kvp.Key);
    //            }
    //            foreach(var kvp in FloatBonusDictionaries)
    //            {
    //                kvp.Value.Remove(source);
    //                RecalculateBonusFloat(kvp.Key);
    //            }
    //            foreach(var kvp in BoolBonusDictionaries)
    //            {
    //                kvp.Value.Remove(source);
    //                RecalculateBonusBool(kvp.Key);
    //            }
    //        }
    //    }
    //}
	#endregion

	#region Calculation and Update
	private void RecalculateBonusBig(BonusType type)
	{
		switch (type)
		{
            case BonusType.StartingGum:
            case BonusType.CommendationsOnGrade1Unlock:
            case BonusType.CommendationsOnGrade2Unlock:
            case BonusType.CommendationsOnGrade3Unlock:
            case BonusType.CommendationsOnGrade4Unlock:
            case BonusType.CommendationsOnGrade5Unlock:
            case BonusType.CommendationsOnGrade6Unlock:
            case BonusType.CommendationsOnGrade7Unlock:
            case BonusType.CommendationsOnGrade8Unlock:
            case BonusType.CommendationsOnGrade9Unlock:
            case BonusType.CommendationsOnGrade10Unlock:
            case BonusType.CommendationsOnGrade11Unlock:
            case BonusType.CommendationsOnGrade12Unlock:
            case BonusType.CommendationsOnCollegeUnlock:
            case BonusType.PassiveIncome:
                RecalculateBigBonusAdditive(type);
                break;
			default:
				RecalculateBigBonusMultiplicative(type);
				break;
		}
	}

	private void RecalculateBonusFloat(BonusType type)
	{
		switch (type)
		{
			case BonusType.GoldenPlayEffectiveness:
            case BonusType.PrestigeExponent:
            case BonusType.PotTakeIncrease:
            case BonusType.PlayFasterAutoRenewChance:
            case BonusType.KeepGumOnSpendChance:
            case BonusType.ManualStreakMaxIncrease:
                RecalculateFloatBonusAdditive(type);
				break;
			case BonusType.DoublePlayChance:
				RecalculateTotalDoublePlayChanceBonus();
				break;
			default:
				RecalculateFloatBonusMultiplicative(type);
				break;
		}
	}

	private void RecalculateBonusBool(BonusType type)
	{
		switch (type)
		{
			default:
				RecalculateBoolBonusAny(type);
				break;
		}
	}

    private void RecalculateBigBonusAdditive(BonusType type)
    {
        var total = BigBonusDefaults[type];
        foreach (var kvp in BigBonusDictionaries[type])
        {
            total += kvp.Value;
        }
        BigBonusTotals[type] = total;
        BigBonusChanged?.Invoke(type, total);
    }
    private void RecalculateBigBonusMultiplicative(BonusType type)
	{
		var total = BigBonusDefaults[type];
		foreach(var kvp in BigBonusDictionaries[type])
		{
			total *= kvp.Value;
		}
		BigBonusTotals[type] = total;
		BigBonusChanged?.Invoke(type, total);
	}

	private void RecalculateFloatBonusMultiplicative(BonusType type)
	{
		var total = FloatBonusDefaults[type];
		foreach (var kvp in FloatBonusDictionaries[type])
		{
			total *= kvp.Value;
		}
		FloatBonusTotals[type] = total;
		FloatBonusChanged?.Invoke(type, total);
	}

	private void RecalculateFloatBonusAdditive(BonusType type)
	{
		var total = FloatBonusDefaults[type];
		foreach (var kvp in FloatBonusDictionaries[type])
		{
			total += kvp.Value;
		}
		FloatBonusTotals[type] = total;
		FloatBonusChanged?.Invoke(type, total);
	}

	private void RecalculateBoolBonusAny(BonusType type)
	{
		var active = BoolBonusDictionaries[type].Values.Any(b => b);
		BoolBonusTotals[type] = active;
		BoolBonusChanged?.Invoke(type, active);
	}
	private void RecalculateBoolBonusAll(BonusType type)
	{
		var active = BoolBonusDictionaries[type].Values.All(b => b);
		BoolBonusTotals[type] = active;
		BoolBonusChanged?.Invoke(type, active);
	}
	
	// Special math involved
	private void RecalculateTotalDoublePlayChanceBonus()
	{
		// Individual values in the dictionary should represent
		// % chance to proc. Joining them together involves
		// inverting those values, multiplying, then inverting again
		var total = 1f;
		foreach (var kvp in FloatBonusDictionaries[BonusType.DoublePlayChance])
		{
			total *= (1 - kvp.Value);
		}
		FloatBonusTotals[BonusType.DoublePlayChance] = 1 - total;
		FloatBonusChanged?.Invoke(BonusType.DoublePlayChance, FloatBonusTotals[BonusType.DoublePlayChance]);
	}
	#endregion
}

class BigBonusAttribute : Attribute { }
class FloatBonusAttribute : Attribute { }
class BoolBonusAttribute : Attribute { }

public enum BonusType
{
	[BigBonus]
	PrimaryCurrencyEarnings,
	[FloatBonus]
	BoardUnlockCost,
	[FloatBonus]
	RewardUpgradeCost,
	[FloatBonus]
	SpeedUpgradeCost,
	[FloatBonus]
	IntelligenceUpgradeCost,
	[FloatBonus]
	GoldenPlayUpgradeCost,
	[BigBonus]
	PrestigeCurrencyEarnings,
    [FloatBonus]
    PrestigeExponent,
	[FloatBonus]
	DoublePlayChance,
	[FloatBonus]
	GoldenPlayEffectiveness,
	[FloatBonus]
	GlobalGumCostMultiplier,
	[BigBonus]
	BoardRewardsMultiplier,
	[FloatBonus]
	TurnIntervalMultiplier,
	[FloatBonus]
	CurrencyEarningsOverGainMultiplier,
	[BoolBonus]
	LuckyGoldenPlayChance,
    [BoolBonus]
    PresidentGoFirst,
    [FloatBonus]
    PlayFasterDurationMultiplier,
    [BigBonus]
    StartingGum,

    // Skills
    [FloatBonus]
    SkillTrickleDownGum,

    [BigBonus]
    CommendationsOnGrade1Unlock,
    [BigBonus]
    CommendationsOnGrade2Unlock,
    [BigBonus]
    CommendationsOnGrade3Unlock,
    [BigBonus]
    CommendationsOnGrade4Unlock,
    [BigBonus]
    CommendationsOnGrade5Unlock,
    [BigBonus]
    CommendationsOnGrade6Unlock,
    [BigBonus]
    CommendationsOnGrade7Unlock,
    [BigBonus]
    CommendationsOnGrade8Unlock,
    [BigBonus]
    CommendationsOnGrade9Unlock,
    [BigBonus]
    CommendationsOnGrade10Unlock,
    [BigBonus]
    CommendationsOnGrade11Unlock,
    [BigBonus]
    CommendationsOnGrade12Unlock,

    [BigBonus]
    CommendationsOnCollegeUnlock,

    [BigBonus]
    GumMultiplierWhileSponsored,
    [BoolBonus]
    SponsorshipNoReset,
    [BigBonus]
    PassiveIncome,

    [BigBonus]
    PotAddMultiplier,
    [FloatBonus]
    PotTakeIncrease,

    [FloatBonus]
    PlayFasterAutoRenewChance,

    [BoolBonus]
    PlayFasterAllAtOnce,

    [FloatBonus]
    PresidentsBoardCost,

    [FloatBonus]
    KeepGumOnSpendChance,

    [BoolBonus]
    ManualGoFirst,

    [FloatBonus]
    ManualStreakMaxIncrease,
    [BigBonus]
    ManualStreakMultiplier,

    [BigBonus]
    CompoundingPrestigeMultiplier,

    [BigBonus]
    OfflineBoardRewardsMultiplier,

    [BoolBonus]
    PresidentBoardPersistThroughPrestige,
    [BoolBonus]
    PresidentBoardPersistThroughTimeWarp,
}

class RemoveOnPrestigeAttribute : Attribute { }

public enum BonusSource
{
    Prestige,
	Sponsorship,
	BannerAd,
	RewardedAd,
    [RemoveOnPrestige]
	President,
    SkillT0,
    SkillT1,
    SkillT2,
    SkillTInf,
    SkillPoint,
    Premium,
    Winning,
}
