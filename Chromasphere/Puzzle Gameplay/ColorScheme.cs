using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/ColorScheme", order = 3)]
public class ColorScheme : ScriptableObject
{
	public string Id;
	public List<Color> Colors;
}
