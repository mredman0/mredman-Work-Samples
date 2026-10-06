using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleProvider : MonoBehaviour
{
	public static PuzzleProvider Instance;
	
	public PuzzleConfig PuzzleConfig;

	public PuzzleProvider()
	{
		Instance = this;
	}

	private void Start()
	{
		DontDestroyOnLoad(gameObject);
	}
}
