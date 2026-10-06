using UnityEngine;

public class WinValueUpgrade : BoardUpgrade
{
	public override BonusType CostBonusType => BonusType.RewardUpgradeCost;

    public override void Init()
    {
        base.Init();
        SponsorshipManager.Instance.ActiveSponsorshipChanged += OnActiveSponsorshipChanged;
        PremiumInventoryManager.Instance.MegaMergerActiveChanged += OnMegaMergerActiveChanged;
    }

    protected override void Cleanup()
    {
        base.Cleanup();
        SponsorshipManager.Instance.ActiveSponsorshipChanged -= OnActiveSponsorshipChanged;
        PremiumInventoryManager.Instance.MegaMergerActiveChanged -= OnMegaMergerActiveChanged;
    }

    public override void Apply()
	{
		Board.WinValue = Board.BaseWinValue * WinValueMultiplierFormula(Level);
	}

    private Big WinValueMultiplierFormula(int level) => Big.FromExponential(1, 1.25, level).Normalized;

	public override string EffectValueForDisplay() =>
        (Board.WinValue *
        Board.TrickleDownGumSkillMultiplier *
        BonusManager.Instance.TotalBonusBig(BonusType.BoardRewardsMultiplier) *
        BonusManager.Instance.TotalBonusBig(BonusType.PrimaryCurrencyEarnings) *
        (SponsorshipManager.Instance.NoneActive ? Big.ONE : BonusManager.Instance.TotalBonusBig(BonusType.GumMultiplierWhileSponsored)))
        .ToStringShorthand();

    protected override void OnBigBonusChanged(BonusType bonus, Big _)
    {
        base.OnBigBonusChanged(bonus, _);
        if (bonus != BonusType.BoardRewardsMultiplier &&
            bonus != BonusType.PrimaryCurrencyEarnings &&
            bonus != BonusType.GumMultiplierWhileSponsored) return;
        EffectChangedInternally();
    }

    private void OnActiveSponsorshipChanged(Sponsorship from, Sponsorship to)
    {
        EffectChangedInternally();
    }

    private void OnMegaMergerActiveChanged(bool active, double _)
    {
        EffectChangedInternally();
    }
}
