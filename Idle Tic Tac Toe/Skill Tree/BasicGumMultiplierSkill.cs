using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Skills/Basic Gum Multiplier")]
public class BasicGumMultiplierSkill : Skill
{
    [Space]
    [Header("Gum Multiplier Balance")]
    public SkillBonusSource SkillBonusSource;
    public List<Big> Multipliers = new List<Big>();

    public override void Apply()
    {
        if(CurrentLevel == 0)
        {
            BonusManager.Instance.RemoveBonusBig(BonusType.PrimaryCurrencyEarnings, GetBonusSource(SkillBonusSource));
            return;
        }
        BonusManager.Instance.UpdateBonusBig(BonusType.PrimaryCurrencyEarnings, GetBonusSource(SkillBonusSource), Multipliers[CurrentLevel-1]);
    }
}
