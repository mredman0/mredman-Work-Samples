using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Skills/Trickle-Down Gumenomics")]
public class TrickleDownGumenomicsSkill : Skill
{
    [Space]
    [Header("Trickle-Down Balance")]
    public List<float> Multipliers = new List<float>();

    public override void Apply()
    {
        if(CurrentLevel == 0)
        {
            BonusManager.Instance.RemoveBonusFloat(BonusType.SkillTrickleDownGum, BonusSource.SkillT0);
            return;
        }
        BonusManager.Instance.UpdateBonusFloat(BonusType.SkillTrickleDownGum, BonusSource.SkillT0, Multipliers[CurrentLevel-1]);
    }
}
