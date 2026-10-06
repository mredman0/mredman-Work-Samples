using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PuzzleGrid : MonoBehaviour
{
    public GameObject IntersectionBall;
    public GameObject CellPrefab;
    public GameObject CellWallPrefab;

    public List<GridCell> Cells = new List<GridCell>();
    public List<List<GridCell>> CellsByRow = new List<List<GridCell>>();
    private List<LineRenderer> CellConnections = new List<LineRenderer>();
    private List<MeshRenderer> IntersectionBalls = new List<MeshRenderer>();

    public Dictionary<LineRenderer, (GridCell, GridCell)> CellConnectionDetails = new Dictionary<LineRenderer, (GridCell, GridCell)>();
    public Dictionary<GridCell, MeshRenderer> IntersectionBallsByCell = new Dictionary<GridCell, MeshRenderer>();

    public float ClosestDistanceBetweenNeighbors = float.MaxValue;

    // Start is called before the first frame update
    void Start()
    {

    }

	#region Initialization
	public void Initialize(int[] cellsPerRow, bool gridVisible = true)
    {
        AddCells(cellsPerRow);
        SetCellNeighbors();
        SetCellNeighborhoods();

        DrawCellConnections();

        SetVisible(gridVisible);
    }

    private void AddCells(int[] cellsPerRow)
    {
        int totalRows = cellsPerRow.Length;

        for (int row = 0; row < totalRows; row++)
        {
            CellsByRow.Add(new List<GridCell>());
            var cells = cellsPerRow[row];
            for (int cell = 0; cell < cells; cell++)
            {
                var cellGO = Instantiate(CellPrefab, transform);
                cellGO.name = $"Cell r{row}c{cell}";
                var newCell = cellGO.GetComponent<GridCell>();
                newCell.Initialize(totalRows, cells, row, cell);
                CellsByRow.Last().Add(newCell);
                Cells.Add(newCell);
                var intersectionBall = Instantiate(IntersectionBall, newCell.transform).GetComponent<MeshRenderer>();
                IntersectionBalls.Add(intersectionBall);
                IntersectionBallsByCell.Add(newCell, intersectionBall);
            }
        }
    }

    private void DrawCellConnections()
    {
        float connectionLineStep = 1f; // Degrees
        var drawn = new Dictionary<GridCell, List<GridCell>>();
        foreach(var cell in Cells)
        {
            foreach(var neighbor in cell.Neighbors.Where(c => !drawn.ContainsKey(c) || !drawn[c].Contains(cell)))
            {
                var newLineGO = Instantiate(CellWallPrefab, transform);
                newLineGO.name = $"Cell Connection r{cell.Row}c{cell.Cell} to r{neighbor.Row}c{neighbor.Cell}";
                var newLine = newLineGO.GetComponent<LineRenderer>();
                newLine.loop = false;

                var startPos = cell.transform.position.ToPolar();
                var endPos = neighbor.transform.position.ToPolar();

                var startLong = startPos.Longitude;
                var endLong = endPos.Longitude;

                if (startPos.Latitude == 90f || startPos.Latitude == -90f)
                {
                    startLong = endLong;
                }
                else if (endPos.Latitude == 90f || endPos.Latitude == -90f)
                {
                    endLong = startLong;
                }

                var newPoints = new List<Vector3>();
                var latDiff = endPos.Latitude - startPos.Latitude;
                var longDiff = endLong - startLong;
                if (longDiff > 180f)
                {
                    longDiff -= 360f;
                }
                else if (longDiff < -180f)
                {
                    longDiff += 360f;
                }
                var step = new Vector2(latDiff, longDiff).normalized * connectionLineStep;
                var numSteps = Mathf.FloorToInt(new Vector2(latDiff, longDiff).magnitude / step.magnitude) - 1;
                var latStep = step.x;
                var longStep = step.y;
                for (int i = 1; i < numSteps; i++)
                {
                    var currentLat = startPos.Latitude + latStep * i;
                    var currentLong = startLong + longStep * i;
                    newPoints.Add(PolarVector3.ToCartesian(currentLat, currentLong));
                }
                newPoints.Add(neighbor.transform.position);

                newLine.positionCount += newPoints.Count;
                newLine.SetPositions(newPoints.ToArray());
                CellConnections.Add(newLine);
                CellConnectionDetails.Add(newLine, (cell, neighbor));

                if (!drawn.ContainsKey(cell))
                {
                    drawn.Add(cell, new List<GridCell>());
                }
                drawn[cell].Add(neighbor);

                if (!drawn.ContainsKey(neighbor))
                {
                    drawn.Add(neighbor, new List<GridCell>());
                }
                drawn[neighbor].Add(cell);
            }
        }

    }

    private void SetCellNeighbors()
    {
        foreach(var cell in Cells)
        {
            AddCellHorizontalNeighbors(cell);
            AddCellVerticalNeighbors(cell);
            cell.DistanceToClosestNeighbor = cell.Neighbors.Min(n => Vector3.Distance(cell.transform.position, n.transform.position));
            if(cell.DistanceToClosestNeighbor < ClosestDistanceBetweenNeighbors)
            {
                ClosestDistanceBetweenNeighbors = cell.DistanceToClosestNeighbor;
            }
        }
    }

    private void AddCellHorizontalNeighbors(GridCell cell)
    {
        var cellsInRow = CellsByRow[cell.Row].Count;
        if (cellsInRow < 2)
        {
            return;
        }

        var leftNeighborIndex = (cell.Cell - 1 + cellsInRow) % cellsInRow;
        cell.Neighbors.Add(CellsByRow[cell.Row][leftNeighborIndex]);

        if(cellsInRow > 2)
        {
            var rightNeighborIndex = (cell.Cell + 1) % cellsInRow;
            cell.Neighbors.Add(CellsByRow[cell.Row][rightNeighborIndex]);
        }
    }

    private const float EQUATOR_SHARED_DEGREES_TO_BE_VERTICAL_NEIGHBORS = 10f;
    private void AddCellVerticalNeighbors(GridCell cell)
    {
        var rowAbove = cell.Row - 1;
        var rowBelow = cell.Row + 1;

        void AddneighborsForRow(int row, bool isAbove)
        {
            if(row < 0 || row >= CellsByRow.Count)
            {
                return;
            }

            var sharedLatitude = isAbove ? cell.LatitudeMin : cell.LatitudeMax;
            var requiredSharedDegrees = EQUATOR_SHARED_DEGREES_TO_BE_VERTICAL_NEIGHBORS / Mathf.Cos(Mathf.Deg2Rad * sharedLatitude);
            foreach (var otherCell in CellsByRow[row])
            {
                var paddedStart = otherCell.LongitudeMin + requiredSharedDegrees;
                var paddedEnd = otherCell.LongitudeMax - requiredSharedDegrees;
                if(cell.LongitudeMin <= paddedEnd && cell.LongitudeMax >= paddedStart)
                {
                    cell.Neighbors.Add(otherCell);
                }
            }
        }

        AddneighborsForRow(rowAbove, isAbove: true);
        AddneighborsForRow(rowBelow, isAbove: false);
    }

    private void SetCellNeighborhoods()
    {
        foreach (var cell in Cells)
        {
            SetCellNeighborhood(cell);
        }
    }

    private void SetCellNeighborhood(GridCell cell)
    {
        var nearbyCells = new List<GridCell>();
        nearbyCells.Add(cell);
        nearbyCells.AddRange(cell.Neighbors);
        cell.Neighborhood = nearbyCells.SelectMany(c => c.Neighbors).Distinct().ToList();
    }
	#endregion

	public GridCell GetLookingAtCell(PolarVector3 pov)
    {
        int totalRows = CellsByRow.Count;
        float rowHeight = 180f / totalRows;
        var lookingAtRow = Mathf.Clamp(Mathf.FloorToInt((90f - pov.Latitude) / rowHeight), 0, totalRows-1);

        var cellsInRow = CellsByRow[lookingAtRow].Count;
        float cellWidth = 360f / cellsInRow;
        var longitude = pov.Longitude < 0 ? pov.Longitude + 360f : pov.Longitude;
        var lookingAtCell = Mathf.Clamp(Mathf.FloorToInt(longitude / cellWidth), 0, cellsInRow-1);

        return CellsByRow[lookingAtRow][lookingAtCell];
    }

    public void SetVisible(bool visible)
    {
        foreach (var line in CellConnections)
        {
            line.gameObject.SetActive(visible);
        }
        foreach (var ball in IntersectionBalls)
        {
            ball.gameObject.SetActive(visible);
        }
    }

    public void Clear()
    {
        foreach(var cell in Cells)
        {
            Destroy(cell.gameObject);
        }
        Cells.Clear();
        CellsByRow.Clear();
        ClosestDistanceBetweenNeighbors = float.MaxValue;

        foreach (var line in CellConnections)
        {
            Destroy(line.gameObject);
        }
        CellConnections.Clear();
        CellConnectionDetails.Clear();

        foreach (var ball in IntersectionBalls)
        {
            Destroy(ball.gameObject);
        }
        IntersectionBalls.Clear();
        IntersectionBallsByCell.Clear();
    }

    #region Pathfinding
    public List<GridCell> GetContiguousCells(GridCell from, Predicate<GridCell> obstructed = null)
    {
        obstructed ??= cell => cell.Color != null;

        // Queue for BFS
        Queue<GridCell> queue = new Queue<GridCell>();
        // Set for tracking visited cells
        HashSet<GridCell> visited = new HashSet<GridCell>();

        // Initialize
        queue.Enqueue(from);
        visited.Add(from);

        while (queue.Count > 0)
        {
            GridCell current = queue.Dequeue();

            // Explore neighbors
            foreach (var neighbor in current.Neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    if (!obstructed(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return visited.ToList();
    }
    public int DistanceBetween(GridCell from, GridCell to, Predicate<GridCell> obstructed = null) =>
        GetShortestPath(from, to, obstructed).Count + 1;
    public List<GridCell> GetShortestPathPreferringWarps(GridCell from, GridCell to, List<PuzzleObjectWarp> warps, Predicate<GridCell> obstructed = null)
    {
        var placedWarps = new Dictionary<GridCell, PuzzleObjectWarp>();
        foreach(var warp in warps)
        {
            placedWarps.Add(warp.Cell, warp);
        }
        obstructed ??= cell => cell.Color != null;

        var path1 = GetShortestPath(from, to, obstructed);
        if(path1 is null)
        {
            return null;
        }
        bool ignoredWarp = false;
        for (int i = 0; i < path1.Count; i++)
        {
            if (placedWarps.ContainsKey(path1[i]) && placedWarps[path1[i]] is PuzzleObjectWarp warp)
            {
                if (i > 0 && placedWarps.ContainsKey(path1[i - 1]) && warp.PairedWarp == placedWarps[path1[i - 1]])
                {
                    continue;
                }
                if (i < path1.Count - 1 && placedWarps.ContainsKey(path1[i + 1]) && warp.PairedWarp == placedWarps[path1[i + 1]])
                {
                    continue;
                }
                ignoredWarp = true;
            }
        }

        if(!ignoredWarp)
        {
            return path1;
        }

        Predicate<GridCell> newObstructed = (cell) => obstructed(cell) || placedWarps.ContainsKey(cell);
        return GetShortestPath(from, to, newObstructed) ?? path1;
    }
    public List<GridCell> GetShortestPath(GridCell from, GridCell to, Predicate<GridCell> obstructed = null)
    {
        obstructed ??= cell => cell.Color != null;

        // Queue for BFS
        Queue<GridCell> queue = new Queue<GridCell>();
        // Dictionary to track the previous cell in the path
        Dictionary<GridCell, GridCell> cameFrom = new Dictionary<GridCell, GridCell>();
        // Set for tracking visited cells
        HashSet<GridCell> visited = new HashSet<GridCell>();

        // Initialize
        queue.Enqueue(from);
        visited.Add(from);

        while (queue.Count > 0)
        {
            GridCell current = queue.Dequeue();

            // Check if we reached the goal
            if (current == to)
            {
                return ReconstructPath(cameFrom, from, to);
            }

            // Explore neighbors
            foreach (var neighbor in current.Neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    if(neighbor == to || !obstructed(neighbor))
                    {
                        cameFrom[neighbor] = current; // Track the path
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return null; // No path found
    }
    private List<GridCell> ReconstructPath(Dictionary<GridCell, GridCell> cameFrom, GridCell start, GridCell goal)
    {
        var path = new List<GridCell>();
        GridCell current = goal;

        while (current != start)
        {
            path.Add(current);
            current = cameFrom[current];
        }
        path.Remove(goal);
        path.Reverse(); // Reverse to get the path from start to goal
        return path;
    }
    #endregion
}
