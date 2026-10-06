using UnityEngine;

public class PlaySpeedUpgrade : BoardUpgrade
{
	public override BonusType CostBonusType => BonusType.SpeedUpgradeCost;

	public override void Apply()
	{
        Board.SetTurnInterval(Board.BaseTurnInterval *
            TurnIntervalMultiplierFormula(Level) *
            (BonusManager.Instance ? BonusManager.Instance.TotalBonusFloat(BonusType.TurnIntervalMultiplier) : 1));

		// This should prevent a scenario where TimeSinceLastTurn is large and then a 
		// significant drop in TurnInterval causes a large number of rounds to occur instantly.
		Board.TimeSinceLastTurn = Mathf.Min(Board.TimeSinceLastTurn, Board.TurnInterval);
	}

	private float TurnIntervalMultiplierFormula(int level) => 7f / (level + 7f);

	public override string EffectValueForDisplay() => $"{Board.TurnInterval.ToString("G3")}s";

	protected override void OnFloatBonusChanged(BonusType bonus, float _)
	{
		base.OnFloatBonusChanged(bonus, _);
		if (bonus != BonusType.TurnIntervalMultiplier) return;
		Apply();
		EffectChangedInternally();
	}
}
