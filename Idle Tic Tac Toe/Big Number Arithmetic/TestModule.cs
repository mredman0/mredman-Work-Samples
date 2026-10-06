using UnityEngine;

public abstract class TestModule : ScriptableObject
{
	public abstract bool OnRun();

	#region Logging
	protected static void LogError(string message, string testName = "Unknown Test", int lineNumber = 0) =>
		Debug.LogError($"{testName} (line {lineNumber}) - {message}\n\n{System.Environment.StackTrace}");
	#endregion

	#region Assertion
	protected static bool AssertEqual(object a, object b, [System.Runtime.CompilerServices.CallerMemberName] string testName = "", [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
	{
		if(a is null && b is null || a != null && a.Equals(b) || b != null && b.Equals(a))
		{
			return true;
		}
		LogError($"{a} is not equal to {b}", testName, lineNumber);
		return false;
	}
	protected static bool AssertNotEqual(object a, object b, [System.Runtime.CompilerServices.CallerMemberName] string testName = "", [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
	{
		if (a is null && b is null || a != null && a.Equals(b) || b != null && b.Equals(a))
		{
			LogError($"{a} is equal to {b}", testName, lineNumber);
			return false;
		}
		return true;
	}
	protected static bool AssertTrue(bool b, [System.Runtime.CompilerServices.CallerMemberName] string testName = "", [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
	{
		if(!b)
		{
			LogError($"expression is not true", testName, lineNumber);
			return false;
		}
		return true;
	}
	protected static bool AssertFalse(bool b, [System.Runtime.CompilerServices.CallerMemberName] string testName = "", [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
	{
		if (b)
		{
			LogError($"expression is not false", testName, lineNumber);
			return false;
		}
		return true;
	}
	#endregion
}
