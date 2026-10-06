using System;
using System.Linq;
using UnityEngine;

public abstract class BoardUpgrade : MonoBehaviour
{
    public event Action<int> LevelChanged;
    public event Action EffectChanged;
    public event Action PurchaseCostsChanged;
    public event Action<int, int> PurchaseMaxTargetLevelChanged;

    [Header("Required References")]
    public Board Board;

    [Header("Settings")]
    public int MinLevel = 0;
    public int MaxLevel = int.MaxValue;

    [Header("State")]
    public int Level = 0;

    public Big Cost1;
    public Big Cost5;
    public int Actual5TargetLevel;
    public Big CostMax;
    public int ActualMaxTargetLevel;
    public Big CostMaxPlus1;

    private int PreviousMaxTargetLevel;

    protected void Start()
    {
    }

    public virtual void Init()
    {
        CurrencyManager.Instance.AvailableCurrencyChanged += OnAvailableCurrencyChanged;
        BonusManager.Instance.FloatBonusChanged += OnFloatBonusChanged;
        BonusManager.Instance.BigBonusChanged += OnBigBonusChanged;
    }

    protected void OnDestroy()
    {
        Cleanup();
    }

    protected virtual void Cleanup()
    {
        CurrencyManager.Instance.AvailableCurrencyChanged -= OnAvailableCurrencyChanged;
        BonusManager.Instance.FloatBonusChanged -= OnFloatBonusChanged;
        BonusManager.Instance.BigBonusChanged -= OnBigBonusChanged;
    }

    protected void OnValidate()
    {
        Apply();
        UpdateCost1AndCost5();
        UpdateCostMax();
    }

    public bool Purchase1()
    {
        var targetLevel = Mathf.Min(Level + 1, MaxLevel);
        var toPurchase = targetLevel - Level;
        if(toPurchase < 1)
        {
            return false;
        }
        var cost = Cost1;
        if(!CurrencyManager.Instance.CanPay(cost))
        {
            return false;
        }
        SetLevelUnchecked(targetLevel);
        CurrencyManager.Instance.Pay(cost);
        return true;
    }

    public bool Purchase5()
    {
        if(Actual5TargetLevel <= Level)
        {
            return false;
        }
        var cost = Cost5;
        if (!CurrencyManager.Instance.CanPay(cost))
        {
            return false;
        }
        SetLevelUnchecked(Actual5TargetLevel);
        CurrencyManager.Instance.Pay(cost);
        return true;
    }

    public bool PurchaseMax()
    {
        if (ActualMaxTargetLevel <= Level)
        {
            return false;
        }
        var cost = CostMax;
        if (!CurrencyManager.Instance.CanPay(cost))
        {
            return false;
        }
        SetLevelUnchecked(ActualMaxTargetLevel);
        CurrencyManager.Instance.Pay(cost);
        return true;
    }

    public void SetLevelUnchecked(int targetLevel)
    {
        Level = targetLevel;
        Apply();
        LevelChanged?.Invoke(Level);

        UpdateCost1AndCost5();
        UpdateCostMax();
        PurchaseCostsChanged?.Invoke();
    }

    public abstract void Apply();

    public virtual void OnAvailableCurrencyChanged(Big availableCurrency, Big change)
    {
        if(availableCurrency >= CostMaxPlus1 || change.Coefficient < 0)
        {
            UpdateCostMax();
            PurchaseCostsChanged?.Invoke();
        }
    }

    public abstract string EffectValueForDisplay();

    // Determines the cost scaling of upgrading by 1 level
    protected virtual double CostFormulaCoefficient => 1;
    protected virtual double CostFormulaBase => 1.5;
    protected virtual Big CostFormula(int level) =>
        Big.FromExponential(CostFormulaCoefficient, CostFormulaBase, level-1).Normalized;

    protected Big CalculateCost(int level) => CostFormula(level) * Board.UpgradeCostMultiplier * GetCostReductionFromBonuses();

    public virtual float GetCostReductionFromBonuses() => BonusManager.Instance ?
		BonusManager.Instance.TotalBonusFloat(CostBonusType) *
        BonusManager.Instance.TotalBonusFloat(BonusType.GlobalGumCostMultiplier) : 1f;

	private void UpdateCost1AndCost5()
    {
        Cost1 = CalculateCost(Level + 1);
        Cost5 = Cost1;
        Actual5TargetLevel = Mathf.Min(Level + 5, MaxLevel);
        for(int l = Level + 2; l <= Actual5TargetLevel; l++)
        {
            Cost5 += CalculateCost(l);
        }
    }

    private void UpdateCostMax()
    {
        var availableCurrency = CurrencyManager.Instance ? CurrencyManager.Instance.Currency : Big.ZERO;

        (var max2Lvl, var max2Cost, var canBuy) = GetMaxV2(availableCurrency, Level);
        ActualMaxTargetLevel = Level + max2Lvl;
        CostMax = max2Cost;
        CostMaxPlus1 = CostMax + (canBuy ? CalculateCost(Level + max2Lvl + 1) : Big.ZERO);

        if (ActualMaxTargetLevel != PreviousMaxTargetLevel)
        {
            PurchaseMaxTargetLevelChanged?.Invoke(ActualMaxTargetLevel, PreviousMaxTargetLevel);
        }
        PreviousMaxTargetLevel = ActualMaxTargetLevel;
    }

    private (int levels, Big totalCost, bool canBuy) GetMaxV2(Big currency, int currentLevel)
    {
        // If no money, can't buy anything
        if (currency <= Big.ZERO)
            return (1, CalculateCost(currentLevel+1), false);

        // Calculate denominator: A × r^(currentLevel - 1)
        Big growthTerm = Big.FromExponential(CostFormulaCoefficient, CostFormulaBase, currentLevel).Normalized;
        Big denom = growthTerm * Board.UpgradeCostMultiplier * GetCostReductionFromBonuses();

        // (C × (r - 1)) / denom + 1
        double rMinus1 = CostFormulaBase - 1;
        Big numerator = currency * new Big(rMinus1, 0);

        Big inside = numerator / denom + new Big(1, 0);

        // N = log_base(inside)
        Big logResult = Big.Log(inside, CostFormulaBase);

        int levels = (int)Math.Floor((double)logResult);

        if (levels <= 0)
            return (1, CalculateCost(currentLevel + 1), false);

        // Total cost: A × r^(L - 1) × (r^N - 1) / (r - 1)
        Big rToN = Big.FromExponential(1, CostFormulaBase, levels).Normalized;
        Big totalCost = denom * (rToN - Big.ONE) / new Big(rMinus1, 0);

        // Floor to get the maximum whole number of levels you can afford
        return (levels, totalCost, true);
    }

    public abstract BonusType CostBonusType { get; }
    protected virtual void OnFloatBonusChanged(BonusType bonus, float _)
    {
        if(bonus == CostBonusType || bonus == BonusType.GlobalGumCostMultiplier)
		{
			UpdateCost1AndCost5();
			UpdateCostMax();
			PurchaseCostsChanged?.Invoke();
		}
	}

    protected virtual void OnBigBonusChanged(BonusType bonus, Big _) { }

    public void EffectChangedExternally() => EffectChanged?.Invoke();
    protected void EffectChangedInternally() => EffectChanged?.Invoke();
}
