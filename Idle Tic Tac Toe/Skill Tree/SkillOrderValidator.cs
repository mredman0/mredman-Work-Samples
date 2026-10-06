using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SkillOrderValidator
{
    [MenuItem("Tools/Validate Skill Orders")]
    public static void ValidateSkillOrders()
    {
        // Find all ScriptableObjects of type Skill in the project
        string[] guids = AssetDatabase.FindAssets("t:Skill");
        List<Skill> skills = new List<Skill>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Skill skill = AssetDatabase.LoadAssetAtPath<Skill>(path);
            if (skill != null)
                skills.Add(skill);
        }

        if (skills.Count == 0)
        {
            Debug.LogWarning("No Skill ScriptableObjects found in the project.");
            return;
        }

        // Sort by order
        var orderedSkills = skills.OrderBy(s => s.Order).ToList();

        // Validate
        var highestOrder = 0;
        bool valid = true;
        HashSet<int> seenOrders = new HashSet<int>();

        for (int i = 0; i < orderedSkills.Count; i++)
        {
            var skill = orderedSkills[i];
            int order = skill.Order;
            highestOrder = Mathf.Max(highestOrder, order);

            // Negative check
            if (order < 0)
            {
                Debug.LogError($"❌ {skill.name} has a negative Order value ({order}).");
                valid = false;
            }

            // Duplicate check
            if (!seenOrders.Add(order))
            {
                Debug.LogError($"❌ Duplicate Order value {order} found in {skill.name}.");
                valid = false;
            }
        }

        // Gap check (only if there were no negatives or duplicates)
        if (valid)
        {
            var orders = orderedSkills.Select(s => s.Order).ToList();
            int minOrder = orders.Min();
            int maxOrder = orders.Max();

            for (int i = minOrder; i <= maxOrder; i++)
            {
                if (!orders.Contains(i))
                {
                    Debug.LogError($"❌ Missing Order value {i} (gap detected).");
                    valid = false;
                }
            }
        }

        // Summary
        Debug.Log($"Highest existing Skill.Order value: {highestOrder}");

        if (valid)
        {
            Debug.Log($"✅ Skill order validation passed. Found {skills.Count} skills with sequential, non-negative, unique Order values.");
        }
        else
        {
            Debug.LogWarning("⚠️ Skill order validation failed. See errors above for details.");
        }
    }
}