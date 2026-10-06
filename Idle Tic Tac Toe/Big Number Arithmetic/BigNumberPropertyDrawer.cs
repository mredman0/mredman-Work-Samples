using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(Big))]
public class BigNumberPropertyDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
        var coefficientProperty = property.FindPropertyRelative("Coefficient");
        var exponentProperty = property.FindPropertyRelative("Exponent");
        Big bigNumber = new Big(coefficientProperty.doubleValue, exponentProperty.longValue);

        EditorGUI.BeginProperty(position, label, property);
		{
			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
			if (GUI.Button(new Rect(position.x, position.y, 21, position.height), new GUIContent("▼", "Normalize numerical value. For example: \"1000\" will become \"1E3\".")))
			{
				bigNumber.Normalize();
				coefficientProperty.doubleValue = bigNumber.Coefficient;
				exponentProperty.longValue = bigNumber.Exponent;
				property.serializedObject.ApplyModifiedProperties();
			}
			position.x += 21;
			position.width -= 21;
			float width3 = position.width * 1f / 3f;
			EditorGUI.PropertyField(new Rect(position.x, position.y, width3, position.height), coefficientProperty, GUIContent.none);
			EditorGUI.LabelField(new Rect(position.x + width3, position.y, width3, position.height), $" [{bigNumber.GetExponentName()}]");
			EditorGUI.LabelField(new Rect(position.x + width3 * 2f, position.y, 21, position.height), new GUIContent("E", "Exponent. For example: \"3\" means \"1000\" (also \"1E3\")."));
			EditorGUI.PropertyField(new Rect(position.x + width3 * 2f + 21, position.y, width3 - 21, position.height), exponentProperty, GUIContent.none);
		}
		EditorGUI.EndProperty();
	}
}