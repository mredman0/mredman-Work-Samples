using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Skills/Play Faster All At Once")]
public class PlayFasterAllAtOnceSkill : Skill
{
    public override void Apply()
    {
        if (!Owned)
        {
            BonusManager.Instance.RemoveBonusBool(BonusType.PlayFasterAllAtOnce, BonusSource.SkillT0);
        }
        else
        {
            BonusManager.Instance.UpdateBonusBool(BonusType.PlayFasterAllAtOnce, BonusSource.SkillT0, true);
        }
    }
}
