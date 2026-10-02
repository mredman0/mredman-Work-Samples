using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PuzzleGridLite
{
    public List<GridCellLite> Cells = new List<GridCellLite>();
    public List<List<GridCellLite>> CellsByRow = new List<List<GridCellLite>>();

    public float ClosestDistanceBetweenNeighbors = float.MaxValue;

    #region Initialization
    public void Initialize(int[] cellsPerRow)
    {
        AddCells(cellsPerRow);
        SetCellNeighbors();
    }

    private void AddCells(int[] cellsPerRow)
    {
        int totalRows = cellsPerRow.Length;

        for (int row = 0; row < totalRows; row++)
        {
            CellsByRow.Add(new List<GridCellLite>());
            var cells = cellsPerRow[row];
            for (int cell = 0; cell < cells; cell++)
            {
                var newCell = new GridCellLite();
                newCell.Initialize(totalRows, cells, row, cell);
                CellsByRow.Last().Add(newCell);
                Cells.Add(newCell);
            }
        }
    }

    private void SetCellNeighbors()
    {
        foreach (var cell in Cells)
        {
            AddCellHorizontalNeighbors(cell);
            AddCellVerticalNeighbors(cell);
            cell.DistanceToClosestNeighbor = cell.Neighbors.Min(n => Vector3.Distance(cell.Position, n.Position));
            if (cell.DistanceToClosestNeighbor < ClosestDistanceBetweenNeighbors)
            {
                ClosestDistanceBetweenNeighbors = cell.DistanceToClosestNeighbor;
            }
        }
    }

    private void AddCellHorizontalNeighbors(GridCellLite cell)
    {
        var cellsInRow = CellsByRow[cell.Row].Count;
        if (cellsInRow < 2)
        {
            return;
        }

        var leftNeighborIndex = (cell.Cell - 1 + cellsInRow) % cellsInRow;
        cell.Neighbors.Add(CellsByRow[cell.Row][leftNeighborIndex]);

        if (cellsInRow > 2)
        {
            var rightNeighborIndex = (cell.Cell + 1) % cellsInRow;
            cell.Neighbors.Add(CellsByRow[cell.Row][rightNeighborIndex]);
        }
    }

    private const float EQUATOR_SHARED_DEGREES_TO_BE_VERTICAL_NEIGHBORS = 10f;
    private void AddCellVerticalNeighbors(GridCellLite cell)
    {
        var rowAbove = cell.Row - 1;
        var rowBelow = cell.Row + 1;

        void AddneighborsForRow(int row, bool isAbove)
        {
            if (row < 0 || row >= CellsByRow.Count)
            {
                return;
            }

            var sharedLatitude = isAbove ? cell.LatitudeMin : cell.LatitudeMax;
            var requiredSharedDegrees = EQUATOR_SHARED_DEGREES_TO_BE_VERTICAL_NEIGHBORS / Mathf.Cos(Mathf.Deg2Rad * sharedLatitude);
            foreach (var otherCell in CellsByRow[row])
            {
                var paddedStart = otherCell.LongitudeMin + requiredSharedDegrees;
                var paddedEnd = otherCell.LongitudeMax - requiredSharedDegrees;
                if (cell.LongitudeMin <= paddedEnd && cell.LongitudeMax >= paddedStart)
                {
                    cell.Neighbors.Add(otherCell);
                }
            }
        }

        AddneighborsForRow(rowAbove, isAbove: true);
        AddneighborsForRow(rowBelow, isAbove: false);
    }
    #endregion

    public GridCellLite GetLookingAtCell(PolarVector3 pov)
    {
        int totalRows = CellsByRow.Count;
        float rowHeight = 180f / totalRows;
        var lookingAtRow = Mathf.Clamp(Mathf.FloorToInt((90f - pov.Latitude) / rowHeight), 0, totalRows - 1);

        var cellsInRow = CellsByRow[lookingAtRow].Count;
        float cellWidth = 360f / cellsInRow;
        var longitude = pov.Longitude < 0 ? pov.Longitude + 360f : pov.Longitude;
        var lookingAtCell = Mathf.Clamp(Mathf.FloorToInt(longitude / cellWidth), 0, cellsInRow - 1);

        return CellsByRow[lookingAtRow][lookingAtCell];
    }

    public void Clear()
    {
        Cells.Clear();
        CellsByRow.Clear();
        ClosestDistanceBetweenNeighbors = float.MaxValue;
    }

    #region Pathfinding
    public List<GridCellLite> GetContiguousCells(GridCellLite from, Predicate<GridCellLite> obstructed = null)
    {
        obstructed ??= cell => cell.Color != null;

        // Queue for BFS
        Queue<GridCellLite> queue = new Queue<GridCellLite>();
        // Set for tracking visited cells
        HashSet<GridCellLite> visited = new HashSet<GridCellLite>();

        // Initialize
        queue.Enqueue(from);
        visited.Add(from);

        while (queue.Count > 0)
        {
            GridCellLite current = queue.Dequeue();

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
    public int DistanceBetween(GridCellLite from, GridCellLite to, Predicate<GridCellLite> obstructed = null) =>
        GetShortestPath(from, to, obstructed).Count + 1;
    public List<GridCellLite> GetShortestPathPreferringWarps(GridCellLite from, GridCellLite to, List<PuzzleObjectWarpLite> warps, Predicate<GridCellLite> obstructed = null)
    {
        var placedWarps = new Dictionary<GridCellLite, PuzzleObjectWarpLite>();
        foreach (var warp in warps)
        {
            placedWarps.Add(warp.Cell, warp);
        }
        obstructed ??= cell => cell.Color != null;

        var path1 = GetShortestPath(from, to, obstructed);
        if (path1 is null)
        {
            return null;
        }
        bool ignoredWarp = false;
        for (int i = 0; i < path1.Count; i++)
        {
            if (placedWarps.ContainsKey(path1[i]) && placedWarps[path1[i]] is PuzzleObjectWarpLite warp)
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

        if (!ignoredWarp)
        {
            return path1;
        }

        Predicate<GridCellLite> newObstructed = (cell) => obstructed(cell) || placedWarps.ContainsKey(cell);
        return GetShortestPath(from, to, newObstructed) ?? path1;
    }
    public List<GridCellLite> GetShortestPath(GridCellLite from, GridCellLite to, Predicate<GridCellLite> obstructed = null)
    {
        obstructed ??= cell => cell.Color != null;

        // Queue for BFS
        Queue<GridCellLite> queue = new Queue<GridCellLite>();
        // Dictionary to track the previous cell in the path
        Dictionary<GridCellLite, GridCellLite> cameFrom = new Dictionary<GridCellLite, GridCellLite>();
        // Set for tracking visited cells
        HashSet<GridCellLite> visited = new HashSet<GridCellLite>();

        // Initialize
        queue.Enqueue(from);
        visited.Add(from);

        while (queue.Count > 0)
        {
            GridCellLite current = queue.Dequeue();

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
                    if (neighbor == to || !obstructed(neighbor))
                    {
                        cameFrom[neighbor] = current; // Track the path
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return null; // No path found
    }
    private List<GridCellLite> ReconstructPath(Dictionary<GridCellLite, GridCellLite> cameFrom, GridCellLite start, GridCellLite goal)
    {
        var path = new List<GridCellLite>();
        GridCellLite current = goal;

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