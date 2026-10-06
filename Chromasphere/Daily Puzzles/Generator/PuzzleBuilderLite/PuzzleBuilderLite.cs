using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class PuzzleBuilderLite
{
    public PuzzleGridLite Grid;

    public int[] GridCellsPerRow = new int[2] { 4, 4 };

    public PuzzleBuilderPaintMode PaintMode = PuzzleBuilderPaintMode.Node;
    public int PaintNodeColor = 0;
    public Dictionary<GridCellLite, PuzzleObjectLite> PlacedObjects = new Dictionary<GridCellLite, PuzzleObjectLite>();
    public List<PuzzleObjectNodeLite> Nodes = new List<PuzzleObjectNodeLite>();
    public List<PuzzleObjectWaypointLite> Waypoints = new List<PuzzleObjectWaypointLite>();
    public List<PuzzleObjectWarpLite> Warps = new List<PuzzleObjectWarpLite>();
    public PuzzleObjectWarpLite LastPlacedWarp;
    public List<PuzzleObjectWallLite> Walls = new List<PuzzleObjectWallLite>();

    public bool OpaqueSphere;
    public MyQuaternion CameraArmStart;

    public float CameraDistance;
    public float CameraFoV;

    public string GeneratorSeed = "";
    public string PreviousSeed;
    public PuzzleGenerationMode GenerationMode;

    public int TargetWarpPairs = 0;
    public int MinimumWarpDistance = 3;

    public float InitialWallAmount = 0f;
    public float InitialWallNormalness = 0f;
    public float InitialWallClustering = 0f;
    public float InitialWallNoodling = 0f;

    public int TargetNodePairs = 6;
    public int TargetWaypoints = 0;

    public float AdditionalWallAmount = 0f;

    public int PreferredDistanceBetweenNodes = 8;
    public int MicroWaywardness = 4;
    public bool WallUnusedPlaceholders = false;

    public string Pack = "Pack";
    public string Id = "#";

    public PuzzleBuilderLite()
    {
        Grid = new PuzzleGridLite();
        RebuildGrid();
    }

	#region Operations
	public void Paint(GridCellLite cell)
    {
        if(PlacedObjects.ContainsKey(cell))
        {
            EraseObject(cell);
        }
        if(PaintMode == PuzzleBuilderPaintMode.Erase)
        {
            return;
        }

        if(PaintMode == PuzzleBuilderPaintMode.Node)
        {
            var newNode = new PuzzleObjectNodeLite();
            newNode.SetColor(PaintNodeColor);
            newNode.Cell = cell;

            if(cell.Color != PaintNodeColor)
            {
                ColorCell(cell);
            }

            Nodes.Add(newNode);
            PlacedObjects[cell] = newNode;
        }
        else if(PaintMode == PuzzleBuilderPaintMode.Waypoint)
        {
            var newWaypoint = new PuzzleObjectWaypointLite();
            newWaypoint.SetColor(PaintNodeColor);
            newWaypoint.Cell = cell;

            if (cell.Color != PaintNodeColor)
            {
                ColorCell(cell);
            }

            Waypoints.Add(newWaypoint);
            PlacedObjects[cell] = newWaypoint;
        }
        else if(PaintMode == PuzzleBuilderPaintMode.Warp)
        {
            var newWarp = new PuzzleObjectWarpLite();
            newWarp.Cell = cell;

            if(LastPlacedWarp != null && LastPlacedWarp.PairedWarp is null)
            {
                LastPlacedWarp.SetPairedWarp(newWarp);
                newWarp.SetPairedWarp(LastPlacedWarp);
            }
            LastPlacedWarp = newWarp;

            Warps.Add(newWarp);
            PlacedObjects[cell] = newWarp;
        }
        else if (PaintMode == PuzzleBuilderPaintMode.Wall)
        {
            var newWall = new PuzzleObjectWallLite();
            newWall.Cell = cell;
            ColorCell(cell, -1);

            Walls.Add(newWall);
            PlacedObjects[cell] = newWall;
        }
    }

    private void EraseObject(GridCellLite cell)
    {
        var objectToDelete = PlacedObjects[cell];

        if(objectToDelete is PuzzleObjectNodeLite node)
        {
            Nodes.Remove(node);
            ColorCell(cell, -1);
        }
        else if (objectToDelete is PuzzleObjectWaypointLite waypoint)
        {
            Waypoints.Remove(waypoint);
        }
        else if (objectToDelete is PuzzleObjectWarpLite warp)
        {
            Warps.Remove(warp);
            if(warp.PairedWarp != null)
            {
                warp.Unpair();
            }
        }
        else if (objectToDelete is PuzzleObjectWallLite wall)
        {
            Walls.Remove(wall);
        }

        PlacedObjects.Remove(cell);
    }

    private void ColorCell(GridCellLite cell)
    {
        if(cell.Color == PaintNodeColor)
        {
            ColorCell(cell, -1);
        }
        else
        {
            ColorCell(cell, PaintNodeColor);
        }
    }

    private void ColorCell(GridCellLite cell, int? color)
    {
        cell.Color = color;
    }
	#endregion

	#region Generator

	public void GeneratePuzzle()
    {
        SetRandomSeed();

        switch (GenerationMode)
        {
            case PuzzleGenerationMode.Hemispheres:
                GenerateHemispheresWall();
                GenerateHemisphereWarps();
                GenerateStandardInitialWalls();
                GenerateStandardNodesPathsAndWaypoints();
                RemoveUnusedWarps();
                GenerateStandardAdditionalWalls();
                break;
            default:
                GenerateStandardWarps();
                GenerateStandardInitialWalls();
                GenerateStandardNodesPathsAndWaypoints();
                RemoveUnusedWarps();
                GenerateStandardAdditionalWalls();
                break;
        }
    }

    private void SetRandomSeed()
    {
        int seed = string.IsNullOrEmpty(GeneratorSeed) ? (int)DateTime.Now.Ticks : GeneratorSeed.GetHashCode();
        if (int.TryParse(GeneratorSeed, out int seedNumber))
        {
            seed = seedNumber;
        }
        PreviousSeed = string.IsNullOrEmpty(GeneratorSeed) ? seed.ToString() : GeneratorSeed;
        Random.InitState(seed);
    }

	#region Hemispheres
    private const int HemisphereWallStepDegrees = 1;
    private void GenerateHemispheresWall()
    {
        PaintMode = PuzzleBuilderPaintMode.Wall;

        var normal = Random.onUnitSphere;
        var start = Vector3.Cross(normal, Mathf.Abs(normal.x) < Mathf.Abs(normal.z) ? Vector3.right : Vector3.up).normalized;
        var offset = Random.Range(-0.4f, 0.4f);

        for(float degrees = 0; degrees < 360f; degrees += HemisphereWallStepDegrees)
        {
            var rotation = Quaternion.AngleAxis(degrees, normal);
            var lookAt = ((rotation * start) + normal * offset).normalized;
            var cell = Grid.GetLookingAtCell(lookAt.ToPolar());
            Paint(cell);
        }
    }
    private void GenerateHemisphereWarps()
    {
        var contiguousGroups = GetContiguousGroupsOfCells();
        if(contiguousGroups.Count != 2)
        {
            Console.WriteLine($"ERROR: Hemisphere wall generation did not create exactly 2 contiguous groups of cells. Generating standard warps");
            GenerateStandardWarps();
            return;
        }
        var warpCells = new List<GridCellLite>();
        var availableCells1 = GetOpenCells(contiguousGroups[0]).ToList();
        var availableCells2 = GetOpenCells(contiguousGroups[1]).ToList();
        for (int pair = 0; pair < TargetWarpPairs; pair++)
        {
            var warp1Cell = availableCells1[Random.Range(0, availableCells1.Count)];
            availableCells1.Remove(warp1Cell);

            var warp2Cell = availableCells2[Random.Range(0, availableCells2.Count)];
            availableCells2.Remove(warp2Cell);

            warpCells.Add(warp1Cell);
            warpCells.Add(warp2Cell);
        }

        PaintMode = PuzzleBuilderPaintMode.Warp;
        for (int i = 0; i < warpCells.Count; i += 2)
        {
            Paint(warpCells[i]);
            Paint(warpCells[i + 1]);
        }

        foreach (var warp in Warps)
        {
            if (warp.PairedWarp is null)
            {
                Console.WriteLine($"WARNING: Warp {warp} does not have a paired warp!");
            }
        }
    }
	#endregion

	#region Standard
    private void GenerateStandardWarps()
    {
        var warpCells = new List<GridCellLite>();
        var availableCells = GetOpenCells(Grid.Cells).ToList();
        for (int pair = 0; pair < TargetWarpPairs; pair++)
        {
            var warp1Cell = availableCells[Random.Range(0, availableCells.Count)];
            availableCells.Remove(warp1Cell);

            var cellsFarEnoughAway = availableCells.Where(cell => Grid.DistanceBetween(warp1Cell, cell) >= MinimumWarpDistance).ToList();
            var warp2Cell = cellsFarEnoughAway[Random.Range(0, cellsFarEnoughAway.Count)];
            availableCells.Remove(warp2Cell);

            warpCells.Add(warp1Cell);
            warpCells.Add(warp2Cell);
        }

        PaintMode = PuzzleBuilderPaintMode.Warp;
        for (int i = 0; i < warpCells.Count; i += 2)
        {
            Paint(warpCells[i]);
            Paint(warpCells[i + 1]);
        }

        foreach (var warp in Warps)
        {
            if (warp.PairedWarp is null)
            {
                Console.WriteLine($"WARNING: Warp {warp} does not have a paired warp!");
            }
        }
    }

    private void GenerateStandardInitialWalls()
    {
        PaintMode = PuzzleBuilderPaintMode.Wall;
        var targetWalls = Mathf.RoundToInt(Grid.Cells.Count * InitialWallAmount);

        if (targetWalls < 1)
        {
            return;
        }

        var placementTypeProbabilityTotal = InitialWallNormalness + InitialWallClustering + InitialWallNoodling;
        if (placementTypeProbabilityTotal == 0)
        {
            placementTypeProbabilityTotal = 1;
        }
        var cluster = InitialWallClustering / placementTypeProbabilityTotal;
        var noodle = cluster + InitialWallNoodling / placementTypeProbabilityTotal;

        for(int wallsGenerated = 0; wallsGenerated < targetWalls; wallsGenerated++)
        {
            var rand = Random.value;
            if(rand < cluster)
            {
                AddWallCluster();
            }
            else if(rand < noodle)
            {
                AddWallNoodle();
            }
            else
            {
                AddWallRandom();
            }
        }
    }

    private Dictionary<int, List<GridCellLite>> GeneratedSolutionPaths = new Dictionary<int, List<GridCellLite>>();
    private void GenerateStandardNodesPathsAndWaypoints()
    {
        PaintMode = PuzzleBuilderPaintMode.Node;
        List<List<GridCellLite>> contiguousGroupsOfCells;
        var colors = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            colors.Add(i);
        }
        var colorOrder = new int[colors.Count];
        for (int i = 0; i < colorOrder.Length; i++)
        {
            var randInd = Random.Range(0, colors.Count);
            colorOrder[i] = colors[randInd];
            colors.RemoveAt(randInd);
        }

        var waypointsLeftToGenerate = TargetWaypoints;
        var preferredNodeCells = new List<GridCellLite>();
        var placedColors = 0;
        var allPlaceholderCells = new List<GridCellLite>();

        for (int i = 0; i < TargetNodePairs; i++)
        {
            PaintNodeColor = colorOrder[i];
            contiguousGroupsOfCells = GetContiguousGroupsOfCells();

            List<GridCellLite> biggestGroup = null;
            int biggestGroupSize = 0;
            foreach (var group in contiguousGroupsOfCells)
            {
                if (group.Count > biggestGroupSize)
                {
                    biggestGroup = group;
                    biggestGroupSize = group.Count;
                }
            }

            if (biggestGroupSize < 4)
            {
                break; // Can't place any more node pairs
            }

            // Place nodes
            var possibleNodes = biggestGroup.Where(cell => !PlacedObjects.ContainsKey(cell)).ToList();
            var preferredNodes = possibleNodes.Intersect(preferredNodeCells).ToList();
            GridCellLite firstNode = null;
            if(preferredNodes.Any())
            {
                firstNode = preferredNodes[Random.Range(0, preferredNodes.Count)];
            }
            else
            {
                firstNode = possibleNodes[Random.Range(0, possibleNodes.Count)];
            }
            possibleNodes.Remove(firstNode);
            preferredNodes.Remove(firstNode);
            var distanceMap = new Dictionary<GridCellLite, int>();
            foreach(var cell in possibleNodes)
            {
                distanceMap.Add(cell, Grid.DistanceBetween(firstNode, cell));
            }
            List<GridCellLite> cellsFarEnoughAway = null;
            for(int distanceThreshold = PreferredDistanceBetweenNodes; distanceThreshold > 2; distanceThreshold--)
            {
                var matches = preferredNodes.Where(cell => distanceMap[cell] >= distanceThreshold);
                if(!matches.Any())
                {
                    matches = possibleNodes.Where(cell => distanceMap[cell] >= distanceThreshold);
                }
                if(matches.Any())
                {
                    cellsFarEnoughAway = matches.ToList();
                    break;
                }
            }
            if(cellsFarEnoughAway is null)
            {
                break; // Can't place any more node pairs
            }
            var secondNode = cellsFarEnoughAway[Random.Range(0, cellsFarEnoughAway.Count)];

            // "Micro" waywardness (waywardness initially choosing a segmentLength of 3
            //      then only continuing to modify the segment within that
            var placeholderCells = new List<GridCellLite>();
            var pathIncludingNodes = Grid.GetShortestPathPreferringWarps(firstNode, secondNode, Warps);
            pathIncludingNodes.Insert(0, firstNode);
            pathIncludingNodes.Add(secondNode);

            int microSegmentLength = 3;
            int maxOffsetVal = pathIncludingNodes.Count - microSegmentLength;
            int offsetMin = 0;
            int offsetMax = maxOffsetVal;

            // Randomly offset our starting point so it's not almost-always at one of the endpoints
            int offsetOffset = Random.Range(0, maxOffsetVal+1);
            offsetMin += offsetOffset;
            offsetMax += offsetOffset;

            for (int wayward = 0; wayward < MicroWaywardness; wayward++)
            {
                var adjustmentMade = false;
                for (int rawOffset = offsetMin; rawOffset <= offsetMax; rawOffset++)
                {
                    if (adjustmentMade)
                    {
                        break;
                    }
                    var offset = rawOffset % (maxOffsetVal + 1);

                    var start = pathIncludingNodes[offset];
                    var end = pathIncludingNodes[offset + microSegmentLength - 1];
                    for (int pathIndex = offset + 1; pathIndex < offset + microSegmentLength - 1; pathIndex++)
                    {
                        ColorCell(pathIncludingNodes[pathIndex], -1);
                        placeholderCells.Add(pathIncludingNodes[pathIndex]);
                        allPlaceholderCells.Add(pathIncludingNodes[pathIndex]);
                    }
                    Predicate<GridCellLite> obstructed = cell =>
                        cell.Color != null || cell.Neighbors.Any(n => {
                            var indexInPath = pathIncludingNodes.IndexOf(n);
                            return indexInPath >= 0 && (indexInPath < offset || indexInPath > offset + microSegmentLength - 1);
                        }) || pathIncludingNodes.Any(pathCell => cell.Neighbors.Intersect(pathCell.Neighbors.Where(pathNeighborCell => pathIncludingNodes.IndexOf(pathNeighborCell) < 0)).Count() > 1);
                    var newSubPath = Grid.GetShortestPathPreferringWarps(start, end, Warps, obstructed);
                    if (newSubPath != null && newSubPath.Count > 1)
                    {
                        pathIncludingNodes.RemoveRange(offset + 1, microSegmentLength - 2);
                        pathIncludingNodes.InsertRange(offset + 1, newSubPath);
                        offsetMin = offset + 1;
                        offsetMax = offsetMin + newSubPath.Count - microSegmentLength;
                        if(offsetMax < offsetMin)
                        {
                            offsetMin = offsetMax;
                            offsetMax = offsetMin + 2;
                        }
                        offsetMin = Mathf.Min(offsetMin, pathIncludingNodes.Count - microSegmentLength);
                        offsetMax = Mathf.Min(offsetMax, pathIncludingNodes.Count - microSegmentLength);
                        adjustmentMade = true;
                    }
                    else
                    {
                        for (int pathIndex = offset + 1; pathIndex < offset + microSegmentLength - 1; pathIndex++)
                        {
                            ColorCell(pathIncludingNodes[pathIndex], null);
                            placeholderCells.Remove(pathIncludingNodes[pathIndex]);
                            allPlaceholderCells.Remove(pathIncludingNodes[pathIndex]);
                        }
                    }
                }

                if (!adjustmentMade)
                {
                    break;
                }
            }

            foreach (var cell in placeholderCells)
            {
                ColorCell(cell, null);
            }

            var solutionPath = pathIncludingNodes.Except(new GridCellLite[]{ firstNode, secondNode }).ToList();
            foreach(var cell in solutionPath)
            {
                ColorCell(cell);
            }

            GeneratedSolutionPaths[colorOrder[i]] = solutionPath;

            PaintMode = PuzzleBuilderPaintMode.Node;
            Paint(firstNode);
            Paint(secondNode);

            preferredNodeCells = Grid.Cells.Where(cell => cell.IsDeadEnd()).ToList();

            placedColors++;
        }

        if(WallUnusedPlaceholders)
        {
            PaintMode = PuzzleBuilderPaintMode.Wall;
            foreach (var cell in allPlaceholderCells.Where(c => !PlacedObjects.ContainsKey(c)))
            {
                if (cell.Color is null)
                {
                    Paint(cell);
                }
            }

            for (int i = 0; i < placedColors; i++)
            {
                foreach (var cell in Grid.Cells.Where(c => !PlacedObjects.ContainsKey(c)))
                {
                    if (cell.Color is null && cell.Neighbors.All(n => n.Color == i || allPlaceholderCells.Contains(n)))
                    {
                        Paint(cell);
                    }
                }
            }
        }

        PaintMode = PuzzleBuilderPaintMode.Waypoint;
        for (int i = 0; i < placedColors && waypointsLeftToGenerate > 0; i++)
        {
            PaintNodeColor = colorOrder[i];
            var solution = GeneratedSolutionPaths[PaintNodeColor];

            bool isChokepoint(GridCellLite cell) =>
                cell.Neighbors.All(n => n.Color == PaintNodeColor || PlacedObjects.ContainsKey(n)) &&
                cell.Neighbors.Any(n => PlacedObjects.ContainsKey(n));

            var solutionChokepoints = solution.Where(cell => isChokepoint(cell) &&
                !PlacedObjects.ContainsKey(cell) &&
                !cell.Neighbors.Any(n => PlacedObjects.ContainsKey(n) && PlacedObjects[n] is PuzzleObjectNodeLite neighborNode && neighborNode.Color == PaintNodeColor)).ToList();

            // If we have chokepoints, prefer one of those
            if (solutionChokepoints.Any())
            {
                Paint(solutionChokepoints[Random.Range(0, solutionChokepoints.Count - 1)]);
            }
            else
            {
                var solutionCellsToAcceptWaypoint = solution.Where(cell => !PlacedObjects.ContainsKey(cell)).ToList();
                if(solutionCellsToAcceptWaypoint.Count < 1)
                {
                    Console.WriteLine($"ERROR: Could not add waypoint to solution for color {PaintNodeColor} because it is already occupied by other objects");
                    continue;
                }
                // Paint a waypoint to a part of the solution between 40% and 60% of the way through
                var min = Mathf.CeilToInt((solutionCellsToAcceptWaypoint.Count + 1) * 0.4f);
                var max = Mathf.FloorToInt((solutionCellsToAcceptWaypoint.Count + 1) * 0.6f);
                var rand = Random.Range(min, max + 1);

                Paint(solutionCellsToAcceptWaypoint[rand - 1]);
            }


            waypointsLeftToGenerate--;
        }
    }

    private void GenerateStandardAdditionalWalls()
    {
        PaintMode = PuzzleBuilderPaintMode.Wall;

        var availableCells = GetOpenCells(Grid.Cells).ToList();
        var targetWalls = Mathf.RoundToInt(availableCells.Count * AdditionalWallAmount);

        for (int i = 0; i < targetWalls; i++)
        {
            var randomIndex = Random.Range(0, availableCells.Count);
            Paint(availableCells[randomIndex]);
            availableCells.RemoveAt(randomIndex);
        }
        return;
    }
	#endregion

	#region Common
    private void RemoveUnusedWarps()
    {
        var warpsToRemove = new List<PuzzleObjectWarpLite>();
        foreach (var kvp in GeneratedSolutionPaths)
        {
            var path = kvp.Value;

            for (int i = 0; i < path.Count; i++)
            {
                if (PlacedObjects.ContainsKey(path[i]) && PlacedObjects[path[i]] is PuzzleObjectWarpLite warp)
                {
                    if (i > 0 && PlacedObjects.ContainsKey(path[i - 1]) && PlacedObjects[path[i - 1]] is PuzzleObjectWarpLite otherWarpPrev && warp.PairedWarp == otherWarpPrev)
                    {
                        continue;
                    }
                    if (i < path.Count - 1 && PlacedObjects.ContainsKey(path[i + 1]) && PlacedObjects[path[i + 1]] is PuzzleObjectWarpLite otherWarpNext && warp.PairedWarp == otherWarpNext)
                    {
                        continue;
                    }
                    warpsToRemove.Add(warp);
                    warpsToRemove.Add(warp.PairedWarp);
                }
            }
        }

        foreach(var warp in Warps)
        {
            if(warp.Cell.Color is null || warp.Cell.Color < 0)
            {
                warpsToRemove.Add(warp);
                warpsToRemove.Add(warp.PairedWarp);
            }
        }

        warpsToRemove = warpsToRemove.Where(w => w != null).Distinct().ToList();

        foreach (var warp in warpsToRemove)
        {
            EraseObject(warp.Cell);
        }
    }
	#endregion

	#region Generation Util
    private void AddWallRandom()
    {
        PaintMode = PuzzleBuilderPaintMode.Wall;
        var availableCells = GetOpenCells(Grid.Cells).ToList();
        if(!availableCells.Any())
        {
            return;
        }
        Paint(availableCells[Random.Range(0, availableCells.Count)]);
    }
    private void AddWallCluster()
    {
        PaintMode = PuzzleBuilderPaintMode.Wall;
        var availableCellGroups = GetOpenCells(Grid.Cells)
            .GroupBy(c => c.Neighbors.Count(n => PlacedObjects.ContainsKey(n) && PlacedObjects[n] is PuzzleObjectWallLite)).ToList();
        if(!availableCellGroups.Any())
        {
            return;
        }
        IGrouping<int, GridCellLite> bestGroup = null;
        var bestGroupNeighborCount = -1;
        foreach(var group in availableCellGroups)
        {
            if(group.Key > bestGroupNeighborCount)
            {
                bestGroupNeighborCount = group.Key;
                bestGroup = group;
            }
        }
        var cellsToChooseFrom = bestGroup.ToList();
        Paint(cellsToChooseFrom[Random.Range(0, cellsToChooseFrom.Count)]);
    }
    private void AddWallNoodle()
    {
        PaintMode = PuzzleBuilderPaintMode.Wall;
        var availableCellGroups = GetOpenCells(Grid.Cells)
            .GroupBy(c => c.Neighbors.Count(n => PlacedObjects.ContainsKey(n) && PlacedObjects[n] is PuzzleObjectWallLite)).ToList();
        if (!availableCellGroups.Any())
        {
            return;
        }
        IGrouping<int, GridCellLite> bestGroup = null;
        foreach (var group in availableCellGroups)
        {
            if(group.Key == 1)
            {
                bestGroup = group;
                break;
            }
            if(group.Key == 0)
            {
                bestGroup = group;

            }
            else if(bestGroup is null || bestGroup.Key > 0 && group.Key < bestGroup.Key)
            {
                bestGroup = group;
            }
        }
        var cellsToChooseFrom = bestGroup.ToList();
        Paint(cellsToChooseFrom[Random.Range(0, cellsToChooseFrom.Count)]);
    }

    private List<List<GridCellLite>> GetContiguousGroupsOfCells()
    {
        var groups = new List<List<GridCellLite>>();
        var ungroupedCells = GetOpenCells(Grid.Cells).ToList();
        while (ungroupedCells.Any())
        {
            var group = new List<GridCellLite>();
            groups.Add(group);
            group.AddRange(Grid.GetContiguousCells(ungroupedCells.First()));
            foreach (var cell in group)
            {
                ungroupedCells.Remove(cell);
            }
        }
        return groups;
    }

    private IEnumerable<GridCellLite> GetOpenCells(IEnumerable<GridCellLite> toConsider) => toConsider.Where(c => c.Color is null && !PlacedObjects.ContainsKey(c));
	#endregion

	#endregion

	#region Reset Functions
	public void Clear()
    {
        ClearNodes();
        ClearWaypoints();
        ClearWarps();
        GeneratedSolutionPaths.Clear();
        ClearObstacles();
        ResetGridCellLiteColors();
    }

    public void ClearNodes()
    {
        foreach(var node in Nodes)
        {
            PlacedObjects.Remove(node.Cell);
        }
        Nodes.Clear();
    }
    public void ClearWaypoints()
    {
        foreach (var waypoint in Waypoints)
        {
            PlacedObjects.Remove(waypoint.Cell);
        }
        Waypoints.Clear();
    }
    public void ClearWarps()
    {
        foreach(var warp in Warps)
        {
            PlacedObjects.Remove(warp.Cell);
            warp.Unpair();
        }
        Warps.Clear();
        LastPlacedWarp = null;
    }

    public void ClearObstacles()
    {
        foreach (var wall in Walls)
        {
            PlacedObjects.Remove(wall.Cell);
        }
        Walls.Clear();
    }

    private void ResetGridCellLiteColors()
    {
        foreach(var cell in Grid.Cells)
        {
            ColorCell(cell, null);
        }
    }

    public void RebuildGrid()
    {
        Grid.Clear();
        Grid.Initialize(GridCellsPerRow);
    }
	#endregion

	#region Saving/Loading
    public PuzzleConfig GetPuzzleConfig()
    {
        if(string.IsNullOrWhiteSpace(Id))
        {
            Console.WriteLine("ERROR: Cannot save puzzle with empty name");
            return null;
        }

        var newPuzzleConfig = new PuzzleConfig();

        // Metadata
        newPuzzleConfig.Pack = Pack;
        newPuzzleConfig.Id = Id;

        // Grid
        newPuzzleConfig.GridCellsPerRow = GridCellsPerRow;

        // Nodes
        newPuzzleConfig.NodePositions = new Vector2Int[Nodes.Count];
        newPuzzleConfig.NodeColors = new int[Nodes.Count];
        int i = 0;
        for(int color = 0; color < 6; color++)
        {
            foreach(var node in Nodes.Where(n => n.Color == color))
            {
                newPuzzleConfig.NodePositions[i] = new Vector2Int(node.Cell.Row, node.Cell.Cell);
                newPuzzleConfig.NodeColors[i] = node.Color;
                i++;
            }
        }

        // Waypoints
        newPuzzleConfig.WaypointPositions = new Vector2Int[Waypoints.Count];
        newPuzzleConfig.WaypointColors = new int[Waypoints.Count];
        for (i = 0; i < Waypoints.Count; i++)
        {
            newPuzzleConfig.WaypointPositions[i] = new Vector2Int(Waypoints[i].Cell.Row, Waypoints[i].Cell.Cell);
            newPuzzleConfig.WaypointColors[i] = Waypoints[i].Color;
        }

        // Warps
        var warpsInPairedOrder = new List<PuzzleObjectWarpLite>();
        foreach(var warp in Warps)
        {
            if(warp.PairedWarp is null)
            {
                Console.WriteLine($"WARNING: {warp} does not have a paired warp and will not be saved");
                continue;
            }
            if(!warpsInPairedOrder.Contains(warp))
            {
                warpsInPairedOrder.Add(warp);
                warpsInPairedOrder.Add(warp.PairedWarp);
            }
        }
        newPuzzleConfig.WarpPositions = new Vector2Int[warpsInPairedOrder.Count];
        for(i = 0; i < warpsInPairedOrder.Count; i++)
        {
            newPuzzleConfig.WarpPositions[i] = new Vector2Int(warpsInPairedOrder[i].Cell.Row, warpsInPairedOrder[i].Cell.Cell);
        }

        // Walls
        newPuzzleConfig.WallPositions = new Vector2Int[Walls.Count];
        for (i = 0; i < Walls.Count; i++)
        {
            newPuzzleConfig.WallPositions[i] = new Vector2Int(Walls[i].Cell.Row, Walls[i].Cell.Cell);
        }

        // Solutions
        var solutions = new List<GridCellLite>();
        var solutionLengths = new int[6];
        for(int color = 0; color < 6; color++)
        {
            if(GeneratedSolutionPaths.ContainsKey(color))
            {
                solutions.AddRange(GeneratedSolutionPaths[color]);
                solutionLengths[color] = GeneratedSolutionPaths[color].Count;
            }
            else
            {
                // If we don't have a generated path to store, try figuring it out... dumbly
                var steps = new List<GridCellLite>();
                var nodes = Nodes.Where(n => n.Color == color).ToList();
                if (nodes.Count == 2)
                {
                    var start = nodes[0].Cell;
                    var end = nodes[1].Cell;
                    var current = start;
                    while (current != end)
                    {
                        var next = current.Neighbors.FirstOrDefault(n => n.Color == color && n != start && !steps.Contains(n));
                        if (next is null)
                        {
                            Console.WriteLine($"WARNING: Could not follow solution path for color {color}");
                            break;
                        }
                        if (next != end)
                        {
                            steps.Add(next);
                        }
                        current = next;
                    }
                }
                else if (nodes.Any())
                {
                    Console.WriteLine($"WARNING: Color {color} does not have exactly 2 nodes, not storing solution.");
                }
                solutions.AddRange(steps);
                solutionLengths[color] = steps.Count;
            }
        }
        newPuzzleConfig.SolutionLengths = solutionLengths;
        newPuzzleConfig.Solutions = solutions.Select(c => new Vector2Int(c.Row, c.Cell)).ToArray();

        // View Settings
        newPuzzleConfig.OpaqueSphere = OpaqueSphere;
        newPuzzleConfig.CameraArmStart = CameraArmStart;
        newPuzzleConfig.CameraDistance = CameraDistance;
        newPuzzleConfig.CameraFoV = CameraFoV;

        return newPuzzleConfig;
    }

    public void Load(PuzzleConfig cfg)
    {
        Clear();

        // Metadata
        Pack = cfg.Pack;
        Id = cfg.Id;

        // Grid
        GridCellsPerRow = new int[cfg.GridCellsPerRow.Length];
        cfg.GridCellsPerRow.CopyTo(GridCellsPerRow, 0);
        RebuildGrid();

        // Nodes
        if(cfg.NodePositions != null && cfg.NodeColors != null)
        {
            PaintMode = PuzzleBuilderPaintMode.Node;
            for (int i = 0; i < cfg.NodePositions.Length; i++)
            {
                var row = cfg.NodePositions[i].x;
                var rowCell = cfg.NodePositions[i].y;
                PaintNodeColor = cfg.NodeColors.Length > i ? cfg.NodeColors[i] : Mathf.FloorToInt(i / 2f);
                Paint(Grid.CellsByRow[row][rowCell]);
            }
        }

        // Waypoints
        if(cfg.WaypointPositions != null)
        {
            PaintMode = PuzzleBuilderPaintMode.Waypoint;
            for (int i = 0; i < cfg.WaypointPositions.Length; i++)
            {
                PaintNodeColor = cfg.WaypointColors[i];
                var row = cfg.WaypointPositions[i].x;
                var rowCell = cfg.WaypointPositions[i].y;
                Paint(Grid.CellsByRow[row][rowCell]);
            }
        }

        // Warps
        if(cfg.WarpPositions != null)
        {
            PaintMode = PuzzleBuilderPaintMode.Warp;
            for(int i = 0; i < cfg.WarpPositions.Length; i++)
            {
                var row = cfg.WarpPositions[i].x;
                var rowCell = cfg.WarpPositions[i].y;
                Paint(Grid.CellsByRow[row][rowCell]);
            }
        }

        // Walls
        if(cfg.WallPositions != null)
        {
            PaintMode = PuzzleBuilderPaintMode.Wall;
            for (int i = 0; i < cfg.WallPositions.Length; i++)
            {
                var row = cfg.WallPositions[i].x;
                var rowCell = cfg.WallPositions[i].y;
                Paint(Grid.CellsByRow[row][rowCell]);
            }
        }

        // Solutions
        if(cfg.SolutionLengths != null && cfg.Solutions != null)
        {
            PaintMode = PuzzleBuilderPaintMode.Node;
            var currentStep = 0;
            for (int i = 0; i < 6; i++)
            {
                var solution = new List<GridCellLite>();
                var lengthOfSolution = cfg.SolutionLengths[i];
                var end = currentStep + lengthOfSolution;
                PaintNodeColor = i;
                for (; currentStep < end; currentStep++)
                {
                    var row = cfg.Solutions[currentStep].x;
                    var rowCell = cfg.Solutions[currentStep].y;

                    if (Grid.CellsByRow[row][rowCell].Color != PaintNodeColor)
                    {
                        ColorCell(Grid.CellsByRow[row][rowCell]);
                    }
                    solution.Add(Grid.CellsByRow[row][rowCell]);
                }
                GeneratedSolutionPaths[i] = solution;
            }
        }

        // View Settings
        OpaqueSphere = cfg.OpaqueSphere;
        CameraArmStart = cfg.CameraArmStart;
        CameraDistance = cfg.CameraDistance;
        CameraFoV = cfg.CameraFoV;
    }
	#endregion
}

public enum PuzzleBuilderPaintMode
{
    Erase,
    Node,
    Waypoint,
    Warp,
    Wall,
}

public enum PuzzleGenerationMode
{
    Standard,
    Hemispheres
}