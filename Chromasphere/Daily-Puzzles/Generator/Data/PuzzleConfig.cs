using UnityEngine;

[Serializable]
public class PuzzleConfig
{
	public string Pack;
	public string Id;

	public int[] GridCellsPerRow;

	public Vector2Int[] WallPositions;

	public Vector2Int[] NodePositions;
	public int[] NodeColors;

	public Vector2Int[] WaypointPositions;
	public int[] WaypointColors;

	public Vector2Int[] WarpPositions;

	public int[] SolutionLengths;
	public Vector2Int[] Solutions;

	public bool OpaqueSphere;
	public MyQuaternion CameraArmStart;
	public float CameraDistance;
	public float CameraFoV;
}
