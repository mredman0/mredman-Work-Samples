using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Skill Tree")]
public class SkillTree : ScriptableObject
{
    public List<Skill> Skills;

    public void Init()
    {
        foreach(var skill in Skills)
        {
            skill.LevelChanged += (int level) => OnSkillLevelChanged(skill, level);
        }
    }

    private void OnSkillLevelChanged(Skill skill, int level)
    {
        HandleSkillAllocationAchievements(skill);
    }

    private void HandleSkillAllocationAchievements(Skill skill)
    {
        if(Skills.All(s => s.MaxedOut))
        {
            AchievementManager.Instance.SetCompleted(AchievementManager.Achievement.MaxAllSkills);
        }
        if(skill.Id == "AutoBuy" && skill.MaxedOut)
        {
            AchievementManager.Instance.SetCompleted(AchievementManager.Achievement.MaxAutoBuySkill);
        }
        if(skill.Id == "EndgameGumAndPrestigeMultiplier" && skill.Owned)
        {
            AchievementManager.Instance.SetCompleted(AchievementManager.Achievement.AllocateTicTacToeStocksSkill);
        }
    }

    public Skill GetSkillById(string skillId) => Skills.FirstOrDefault(s => s.Id == skillId);

    public void Respec()
    {
        foreach(var skill in Skills)
        {
            skill.SetLevel(0);
        }
    }
}
