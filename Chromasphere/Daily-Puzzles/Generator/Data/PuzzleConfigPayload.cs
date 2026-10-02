
using UnityEngine;

[Serializable]
public class PuzzleConfigPayload
{
	public string DailyPuzzleGroup;

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

	public PuzzleConfigPayload() { }

	public PuzzleConfigPayload(PuzzleConfig c)
	{
		Pack = c.Pack;
		Id = c.Id;
		GridCellsPerRow = c.GridCellsPerRow;
		WallPositions = c.WallPositions;
		NodePositions = c.NodePositions;
		NodeColors = c.NodeColors;
		WaypointPositions = c.WaypointPositions;
		WaypointColors = c.WaypointColors;
		WarpPositions = c.WarpPositions;
		SolutionLengths = c.SolutionLengths;
		Solutions = c.Solutions;
		OpaqueSphere = c.OpaqueSphere;
		CameraArmStart = c.CameraArmStart;
		CameraDistance = c.CameraDistance;
		CameraFoV = c.CameraFoV;
	}

	public static PuzzleConfig ToPuzzleConfig(PuzzleConfigPayload p)
	{
		var c = new PuzzleConfig();
		c.Pack = p.Pack;
		c.Id = p.Id;
		c.GridCellsPerRow = p.GridCellsPerRow;
		c.WallPositions = p.WallPositions;
		c.NodePositions = p.NodePositions;
		c.NodeColors = p.NodeColors;
		c.WaypointPositions = p.WaypointPositions;
		c.WaypointColors = p.WaypointColors;
		c.WarpPositions = p.WarpPositions;
		c.SolutionLengths = p.SolutionLengths;
		c.Solutions = p.Solutions;
		c.OpaqueSphere = p.OpaqueSphere;
		c.CameraArmStart = p.CameraArmStart;
		c.CameraDistance = p.CameraDistance;
		c.CameraFoV = p.CameraFoV;
		return c;
	}
}
