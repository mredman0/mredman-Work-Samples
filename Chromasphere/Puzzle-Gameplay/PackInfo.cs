using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/PackInfo", order = 2)]
public class PackInfo : ScriptableObject
{
    public string Id;
    public LocalizedString Name;
    public Color Tint;

    public int NumLevels = 50;
    public bool LevelsLocked;
}
