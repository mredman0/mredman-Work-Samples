using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/GriddedPuzzleConfig", order = 1)]
[Serializable]
public class PuzzleConfig : ScriptableObject
{
	[Header("Metadata")]
	public string Pack;
	public string Id;

	[Header("Grid")]
	public int[] GridCellsPerRow;

	[Header("Obstacles")]
	public Vector2Int[] WallPositions;

	[Header("Nodes")]
	public Vector2Int[] NodePositions;
	public int[] NodeColors;

	[Header("Waypoints")]
	public Vector2Int[] WaypointPositions;
	public int[] WaypointColors;

	[Header("Warps")]
	public Vector2Int[] WarpPositions;

	[Header("Solutions")]
	public int[] SolutionLengths;
	public Vector2Int[] Solutions;

	[Header("View")]
	public bool OpaqueSphere;
	public Quaternion CameraArmStart;
	public float CameraDistance;
	public float CameraFoV;
}
