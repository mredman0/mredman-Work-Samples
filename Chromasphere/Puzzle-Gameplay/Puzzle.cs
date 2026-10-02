using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class Puzzle : MonoBehaviour
{
    public static Puzzle Current { get; set; }

    public event Action PuzzleInitialized;
    public event Action<Node> NodeSelected;
    public event Action NodeDeselected;
    public event Action<Node, Node> NodesConnected;
    public event Action<Node, Node> NodesDisconnected;
    public event Action<Waypoint> WaypointReached;
    public event Action<Waypoint> WaypointUnreached;
    public event Action<Warp, Warp> WarpTaken;
    public event Action<Warp, Warp> WarpUntaken;
    public event Action UndoAvailable;
    public event Action UndoUnavailable;
    public event Action UndoUsed;
    public event Action ResetUsed;
    public event Action PuzzleCompleted;

    [Header("Prefabs")]
    public GameObject NodePrefab;
    public GameObject WaypointPrefab;
    public GameObject WarpPrefab;
    public GameObject WallPrefab;

    [Header("Effects")]
    public GameObject ClickPositiveEffect;
    public GameObject ClickNegativeEffect;
    public List<GameObject> NodesConnectedEffects;
    public List<GameObject> WaypointColoredEffects;
    public GameObject WarpTakenEffect;
    public GameObject PuzzleCompleteEffect;

    [Header("Required References")]
    public PuzzleGrid Grid;
    public Camera PuzzleViewCamera;
    public CameraController CameraController;
    public MeshRenderer MainSphere;

    [Header("Settings")]
    public float PanInsteadOfSelectionThreshold = 1f;
    public List<RectTransform> IgnoreInputRegions = new List<RectTransform>();

    [Header("State")]
    public bool Initialized = false;
    public Node ActiveNode;
    public List<Node> Nodes;
    public Dictionary<int, List<Node>> NodesByColor = new Dictionary<int, List<Node>>();
    public List<Waypoint> Waypoints;
    public Dictionary<GridCell, Waypoint> WaypointsByGridCell = new Dictionary<GridCell, Waypoint>();
    public List<Warp> Warps;
    public Dictionary<GridCell, Warp> WarpsByGridCell = new Dictionary<GridCell, Warp>();
    public Dictionary<int, MultiLineRenderer> Paths = new Dictionary<int, MultiLineRenderer>();
    public Dictionary<GridCell, List<Vector3>[]> DrawnPointsByGridCell = new Dictionary<GridCell, List<Vector3>[]>();
    public List<Wall> Walls;
    public List<int> HintedColors = new List<int>();

    public int LastModifiedColor = -1;
    public bool LastModifiedConnected;
    public List<Vector3[]> LastModifiedPathState;
    public bool LastModifiedColorWasHintSolved;

    public int InputLocks = 0;
    public bool Completed;

    private float PathConnectToNodeDistance = 0.1f;
    private float PathCollisionDistance = 0.07f;
    private float NodeCollisionDistance = 0.15f;

    private PuzzleConfig PuzzleConfig;

    [Header("Debug")]
    public bool GridVisible = false;

    private void Awake()
    {
        Current = this;
    }

    public const string TUTORIAL_SHOWN_KEY = "TutorialShown";

    void Start()
    {
        InitializePuzzle(manual: false);

        StatsManager.Instance.OnPuzzleStarted();

        if (PuzzleConfig.Pack != "Tutorial")
        {
            // Handle a bit of ad related stuff
            AdManager.Instance.PuzzleOpened();
            AdManager.Instance.LoadInterstitial();
            AdManager.Instance.LoadRewardedHint();
        }

        InputManager.Instance.Tap += OnTap;
        ColorManager.Instance.ColorSchemeChanged += OnColorSchemeChanged;

        if (PuzzleConfig.Pack == "Tutorial" && TutorialInstructionsProvider.Instance)
        {
            var tutorialInstructions = TutorialInstructionsProvider.Instance.GetTutorialInstructionsPrefab(PuzzleConfig.Id);
            if(tutorialInstructions)
            {
                Instantiate(tutorialInstructions);
            }

            // Disable interstitial ads for the tutorial
            foreach (var sceneLoader in FindObjectsOfType<SceneLoader>(includeInactive: true))
            {
                sceneLoader.AllowInterstitial = false;
            }
            foreach (var puzzleLoader in FindObjectsOfType<PuzzleLoader>(includeInactive: true))
            {
                puzzleLoader.AllowInterstitial = false;
            }
        }
    }

    private void OnDestroy()
    {
        StatsManager.Instance.OnPuzzleClosed();
        ColorManager.Instance.ColorSchemeChanged -= OnColorSchemeChanged;
        InputManager.Instance.Tap -= OnTap;
    }

    private void OnColorSchemeChanged()
    {
        foreach(var node in Nodes)
        {
            node.SetColor(node.Color);
        }
        foreach(var waypoint in Waypoints)
        {
            waypoint.SetColor(waypoint.Color);
        }
        foreach(var warp in Warps)
        {
            if(warp.Role != Warp.WarpRole.Open)
            {
                warp.SetSurfaceColor(warp.Color);
                warp.SetLineColor(warp.Color);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(ActiveNode)
        {
            HandleDrawingForActiveNode();
        }
    }

    private Vector3 PreviousDrawPoint;
    private void HandleDrawingForActiveNode()
    {
        if(InputLocks > 0)
        {
            return;
        }
        var point = PuzzleViewCamera.transform.position.normalized;
        if (point == PreviousDrawPoint)
        {
            return;
        }
        PreviousDrawPoint = point;

        Draw(ActiveNode, point);
        SmoothEndOfLine(ActiveNode.Path, ActiveNode.Color);
        DejitterEndOfLine(ActiveNode.Path, ActiveNode.Color);

        if (Vector3.Distance(ActiveNode.PairedNode.transform.position, point) < PathConnectToNodeDistance)
        {
            Draw(ActiveNode, ActiveNode.PairedNode.transform.position);
            SmoothEndOfLine(ActiveNode.Path, ActiveNode.Color);
            DejitterEndOfLine(ActiveNode.Path, ActiveNode.Color);
            //Paths.RemoveAll(p => !p.Item2);
            ActiveNode.PairedNode.Path = ActiveNode.Path;
            SetConnected(ActiveNode, ActiveNode.PairedNode);
        }
    }

    private const float MINIMUM_DRAW_STEP = 0.01f;
    private void Draw(Node node, Vector3 point)
    {
        // TODO Remove after investigation of disappearing lines over
        if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z))
        {
            var errorsDir = Path.Combine(Application.persistentDataPath, "err");
            if (!Directory.Exists(errorsDir))
            {
                Directory.CreateDirectory(errorsDir);
            }
            File.AppendAllText(Path.Combine(errorsDir, "drawing_errors.txt"),
                $"Drawing resulted in bad point {point}\n{Environment.StackTrace}\n\n\n");
        }

        var path = node.Path;
        var loopMergeDistance = path.EndWidth * 0.8f;
        int mergeLoop = 0;
        int mergeIgnoreMostRecent = Mathf.FloorToInt(loopMergeDistance / MINIMUM_DRAW_STEP);
        bool merged = false;
        bool mergedOutOfWarp = false;
        var lines = path.GetPositionsInLines();
        foreach(var line in lines)
        {
            for(int i = 0; i < line.Length; i++)
            {
                if (mergeLoop >= path.PositionCount - mergeIgnoreMostRecent)
                {
                    break;
                }

                if(i < line.Length - 5)
                {
                    var mergeOrigin = line[i];
                    if ((point - mergeOrigin).magnitude < loopMergeDistance)
                    {
                        for (int j = mergeLoop + 1; j < path.PositionCount; j++)
                        {
                            InternalHandleLinePointRemoved(path.GetPosition(j), node.Color);
                        }
                        mergedOutOfWarp = NotifyWarpsOfLineMerge(path, node.Color, point, mergeOrigin);
                        if (!mergedOutOfWarp)
                        {
                            path.PositionCount = mergeLoop + 1;
                        }
                        merged = true;
                        break;
                    }
                }

                mergeLoop++;
            }
            if(merged)
            {
                break;
            }
        }

        if (!mergedOutOfWarp && (point - path.GetPosition(path.PositionCount - 1)).magnitude > MINIMUM_DRAW_STEP)
        {
            path.PositionCount++;
            path.SetPosition(path.PositionCount - 1, point);
            InternalHandleLinePointDrawn(path, point, node.Color);
        }

        if(!mergedOutOfWarp)
        {
            var pointsRemoved = path.CleanupCurrentLinePoints();
            foreach(var removedPoint in pointsRemoved)
            {
                InternalHandleLinePointRemoved(removedPoint, node.Color);
            }
        }
    }

    private void InternalHandleLinePointDrawn(MultiLineRenderer path, Vector3 point, int color, bool notifyWaypoints = true, bool notifyWarps = true, bool applyWarpToPath = true, bool moveCamera = true)
    {
        DrawnPointsByGridCell[Grid.GetLookingAtCell(point.ToPolar())][color].Add(point);
        if(notifyWaypoints)
        {
            NotifyWaypointsOfLinePointDrawn(point, color);
        }
        if(notifyWarps)
        {
            NotifyWarpsOfLinePointDrawn(path, point, color, applyWarpToPath, moveCamera);
        }
    }

    private void NotifyWaypointsOfLinePointDrawn(Vector3 point, int color)
    {
        var cell = Grid.GetLookingAtCell(point.ToPolar());
        if(!WaypointsByGridCell.ContainsKey(cell))
        {
            return;
        }
        var waypoint = WaypointsByGridCell[cell];
        var previouslyReached = waypoint.Reached;
        waypoint.LinePointDrawnInCell(point, color);
        var nowReached = waypoint.Reached;
        if (nowReached && !previouslyReached)
        {
            WaypointReached?.Invoke(waypoint);
            var reachedWaypoints = Waypoints.Count(w => w.Reached);
            var effectToUse = WaypointColoredEffects[Mathf.Clamp(reachedWaypoints - 1, 0, WaypointColoredEffects.Count - 1)];
            Instantiate(effectToUse);
        }
    }

    private bool NotifyWarpsOfLinePointDrawn(MultiLineRenderer path, Vector3 point, int color, bool applyWarpToPath = true, bool moveCamera = true)
    {
        var cell = Grid.GetLookingAtCell(point.ToPolar());
        if (!WarpsByGridCell.ContainsKey(cell))
        {
            return false;
        }
        var warp = WarpsByGridCell[cell];
        if(warp.Role == Warp.WarpRole.Open)
        {
            warp.TakeWarp(path, point, color, applyWarpToPath);
            WarpTaken?.Invoke(warp, warp.PairedWarp);
            if(WarpTakenEffect)
            {
                Instantiate(WarpTakenEffect);
            }
            if(moveCamera)
            {
                CameraController.GradualSnapToGridCell(warp.PairedWarp.GridCell);
            }
            return true;
        }
        return false;
        //if (previousWaypointColor != newWaypointColor)
        //{
        //    var coloredWaypoints = Waypoints.Count(w => w.Color >= 0);
        //    var effectToUse = WaypointColoredEffects[Mathf.Clamp(coloredWaypoints - 1, 0, WaypointColoredEffects.Count - 1)];
        //    Instantiate(effectToUse);
        //}
    }

    private bool NotifyWarpsOfLineMerge(MultiLineRenderer path, int color, Vector3 drawnPoint, Vector3 mergeOrigin)
    {
        // Both the drawn point and the merge origin have to be in the warp cell to go back
        var drawnPointCell = Grid.GetLookingAtCell(drawnPoint.ToPolar());
        if (!WarpsByGridCell.ContainsKey(drawnPointCell))
        {
            return false;
        }
        var mergeOriginCell = Grid.GetLookingAtCell(mergeOrigin.ToPolar());
        if (mergeOriginCell != drawnPointCell || !WarpsByGridCell.ContainsKey(mergeOriginCell))
        {
            return false;
        }
        var warp = WarpsByGridCell[drawnPointCell];
        if(warp.Role != Warp.WarpRole.Destination)
        {
            return false;
        }

        // The distance to the warp must be pretty close
        if(Vector3.Distance(drawnPoint, mergeOriginCell.transform.position) > 0.1f)
        {
            return false;
        }


        // Additionally, the path must have at least 1 point between drawnPoint and mergeOrigin that is OUTSIDE the warp cell
        //var hasLeftDestinationWarp = false;
        //int i = 0;
        //for(;i < path.PositionCount; i++)
        //{
        //    if(path.GetPosition(i) == mergeOrigin)
        //    {
        //        break;
        //    }
        //}
        //i++;
        //for(; i < path.PositionCount; i++)
        //{
        //    var pos = path.GetPosition(i);
        //    if(pos == drawnPoint)
        //    {
        //        break;
        //    }
        //    var cell = Grid.GetLookingAtCell(pos.ToPolar());
        //    if(cell != warp.GridCell)
        //    {
        //        hasLeftDestinationWarp = true;
        //        break;
        //    }
        //}
        //if(!hasLeftDestinationWarp)
        //{
        //    return false;
        //}

        var source = warp.PairedWarp;
        var pathTrimPoint = source.PointDrawnInCell.Value;
        TrimPathToPoint(path, color, pathTrimPoint, includePoint: false);

        if (path.PositionCount > 0)
        {
            CameraController.GradualSnapToLookAt(path.GetPosition(path.PositionCount - 1));
        }

        return true;
    }

    private void InternalHandleLinePointRemoved(Vector3 point, int color, bool notifyWaypoints = true, bool notifyWarps = true)
    {
        DrawnPointsByGridCell[Grid.GetLookingAtCell(point.ToPolar())][color].Remove(point);
        if (notifyWaypoints)
        {
            NotifyWaypointsOfLinePointRemoved(point);
        }
        if(notifyWarps)
        {
            NotifyWarpsOfLinePointRemoved(point);
        }
    }

    public void NotifyWaypointsOfLinePointRemoved(Vector3 point)
    {
        var cell = Grid.GetLookingAtCell(point.ToPolar());
        if (!WaypointsByGridCell.ContainsKey(cell))
        {
            return;
        }
        var waypoint = WaypointsByGridCell[cell];
        var previouslyReached = waypoint.Reached;
        waypoint.LinePointRemovedFromCell(point);
        var nowReached = waypoint.Reached;
        if (previouslyReached && !nowReached)
        {
            WaypointUnreached?.Invoke(waypoint);
        }
    }

    private void NotifyWarpsOfLinePointRemoved(Vector3 point)
    {
        var cell = Grid.GetLookingAtCell(point.ToPolar());
        if (!WarpsByGridCell.ContainsKey(cell))
        {
            return;
        }
        var warp = WarpsByGridCell[cell];
        bool warpUntaken = warp.LinePointRemovedFromCell(point);
        if(warpUntaken)
        {
            WarpUntaken?.Invoke(warp, warp.PairedWarp);
        }
    }

    public void InitializePuzzle(bool manual)
    {
        Initialized = false;
        ResetPuzzle(manual);

        var puzzleProvider = PuzzleProvider.Instance;
        if (!puzzleProvider)
        {
            Debug.LogError("No puzzle provider found!");
            return;
        }
        if (!puzzleProvider.PuzzleConfig)
        {
            Debug.LogError("Puzzle provider has no puzzle config!");
            return;
        }
        PuzzleConfig = puzzleProvider.PuzzleConfig;

        Grid.Initialize(PuzzleConfig.GridCellsPerRow, gridVisible: GridVisible);

        PathConnectToNodeDistance = Grid.ClosestDistanceBetweenNeighbors * 0.9f;
        PathCollisionDistance = Grid.ClosestDistanceBetweenNeighbors * 0.4f;
        NodeCollisionDistance = Grid.ClosestDistanceBetweenNeighbors * 0.5f;

        if (PuzzleConfig != null)
        {
            SetupPuzzle(PuzzleConfig);
        }
        Initialized = true;
        PuzzleInitialized?.Invoke();
    }

    private void OnTap(Vector2 tapPosition)
    {
        if(Completed || InputLocks > 0 || ShouldIgnoreInputPosition(tapPosition))
        {
            return;
        }
        var nodeLayerMask = LayerMask.GetMask("Node", "InputCatch");
        var ray = CameraController.Camera.ScreenPointToRay(tapPosition);
        var anyHit = Physics.Raycast(ray, out RaycastHit hitInfo, float.MaxValue, nodeLayerMask);
        if(anyHit)
        {
            var node = hitInfo.collider.GetComponent<Node>();
            if(node)
            {
                OnNodeTapped(node);
            }
            else
            {
                // Selection by line
                Node selectedNodeByLine = null;
                var tappedColor = -1;
                MultiLineRenderer tappedPath = null;
                Vector3? tappedPoint = null;
                var closestPathDistance = float.MaxValue;
                var maximumDistanceForPathTap = Paths.Values.FirstOrDefault()?.EndWidth ?? 0;

                foreach (var kvp in Paths)
                {
                    var numPoints = kvp.Value.PositionCount;
                    var points = new Vector3[numPoints];
                    kvp.Value.GetPositions(points);

                    for(int i = 0; i < numPoints; i++)
                    {
                        var dist = Vector3.Distance(points[i], hitInfo.point);
                        if (dist < closestPathDistance)
                        {
                            tappedPoint = points[i];
                            closestPathDistance = dist;
                            tappedColor = kvp.Key;
                            tappedPath = kvp.Value;
                        }
                    }
                }
                if (tappedPoint.HasValue && closestPathDistance < maximumDistanceForPathTap)
                {
                    var nodesOfColor = Nodes.Where(n => n.Color == tappedColor);
                    foreach (var nodeOfColor in nodesOfColor)
                    {
                        if (nodeOfColor.Path == tappedPath && nodeOfColor.transform.position == tappedPath.GetPosition(0))
                        {
                            selectedNodeByLine = nodeOfColor;
                            break;
                        }
                    }
                }

                if (selectedNodeByLine)
                {
                    // Move tapped point away from source warp if needed
                    var tappedPointGridCell = Grid.GetLookingAtCell(tappedPoint.Value.ToPolar());
                    if(WarpsByGridCell.ContainsKey(tappedPointGridCell) && WarpsByGridCell[tappedPointGridCell].Role == Warp.WarpRole.Source)
                    {
                        var warp = WarpsByGridCell[tappedPointGridCell];
                        if(warp.EntrancePath.Contains(tappedPoint.Value))
                        {
                            tappedPoint = warp.PointDrawnInCell;
                        }
                    }

                    OnPathTapped(selectedNodeByLine, tappedPoint.Value);
                }
                else if (ActiveNode)
                {
                    SetActiveNode(null, fromExistingLine: false);
                }
            }
        }
        else if(ActiveNode)
        {
            SetActiveNode(null, fromExistingLine: false);
        }
    }

    private bool ShouldIgnoreInputPosition(Vector2 position)
    {
        if(IgnoreInputRegions is null)
        {
            return false;
        }
        foreach(var rect in IgnoreInputRegions)
        {
            if(RectTransformUtility.RectangleContainsScreenPoint(rect, position))
            {
                return true;
            }
        }
        return false;
    }
    public void OnPathTapped(Node n, Vector3 tappedPoint)
    {
        SetUndoState(n.Color);

        if (n.Connected)
        {
            SetDisconnected(n);
        }

        TrimPathToPoint(n.Path, n.Color, tappedPoint, includePoint: true);

        SetActiveNode(n, fromExistingLine: true);
    }

    public void OnNodeTapped(Node n)
    {
        SetUndoState(n.Color);
        SetActiveNode(n, fromExistingLine: false);
    }

    public void LockInput()
    {
        if(InputLocks == 0)
        {
            StatsManager.Instance.OnPuzzlePaused();
        }
        InputLocks++;
    }
    public void FreeInput()
    {
        if (InputLocks == 1)
        {
            StatsManager.Instance.OnPuzzleUnpaused();
        }
        InputLocks = Mathf.Max(InputLocks - 1, 0);
    }

    public bool IsComplete()
    {
        // Make sure all pairs of nodes are connected
        var nodesAllConnected = Nodes.TrueForAll(n => n.Connected);
        if(!nodesAllConnected)
        {
            return false;
        }
        // Make sure all waypoints have been hit
        var waypointsAllReached = Waypoints.TrueForAll(w => w.Reached);
        if(!waypointsAllReached)
        {
            return false;
        }

        return true;
    }

    private const float PATH_SIZE_RELATIVE_TO_NODE_SIZE = 0.6f;
    public void SetActiveNode(Node n, bool fromExistingLine, bool triggerEffects = true)
    {
        if(ActiveNode)
        {
            ActiveNode.SetDeselected();
            ActiveNode.PairedNode.SetDeselected();
        }

        if(!n)
        {
            if(ActiveNode)
            {
                ActiveNode = null;
                if(triggerEffects)
                {
                    Instantiate(ClickNegativeEffect);
                }
            }
            NodeDeselected?.Invoke();
            return;
        }

        if(fromExistingLine && n.Path && n.Path.PositionCount > 0)
        {
            CameraController.GradualSnapToLookAt(n.Path.GetPosition(n.Path.PositionCount-1));
        }
        else
        {
            CameraController.GradualSnapToGridCell(n.GridCell);
        }

        ActiveNode = n;
        ActiveNode.SetSelected();
        ActiveNode.PairedNode.SetSelected();

        if (!fromExistingLine)
        {
            StartPathFromNode(n);
        }

        NodeSelected?.Invoke(n);
        if(triggerEffects)
        {
            Instantiate(ClickPositiveEffect);
        }
    }

    private void StartPathFromNode(Node n)
    {
        if (n.Path)
        {
            DeleteNodePath(n);
        }
        if (n.PairedNode.Path)
        {
            DeleteNodePath(n.PairedNode);
        }

        n.StartPath();

        Paths[n.Color] = n.Path;
        n.Path.StartWidth = n.transform.localScale.x * PATH_SIZE_RELATIVE_TO_NODE_SIZE;
        n.Path.EndWidth = n.transform.localScale.x * PATH_SIZE_RELATIVE_TO_NODE_SIZE;
    }

    private void TrimPathToPoint(MultiLineRenderer path, int color, Vector3 point, bool includePoint)
    {
        var numPoints = path.PositionCount;
        var points = new Vector3[numPoints];
        path.GetPositions(points);
        int i = 0;
        for(; i < numPoints; i++)
        {
            if(points[i] == point)
            {
                break;
            }
        }

        if(includePoint)
        {
            i++;
        }

        path.PositionCount = i;
        for(; i< numPoints; i++)
        {
            InternalHandleLinePointRemoved(points[i], color);
        }
    }

    private void DeleteNodePath(Node node)
    {
        SetDisconnected(node);

        if (node.Path)
        {
            for (int i = 0; i < node.Path.PositionCount; i++)
            {
                InternalHandleLinePointRemoved(node.Path.GetPosition(i), node.Color);
            }
            Destroy(node.Path.gameObject);
            Paths.Remove(node.Color);
        }
    }

    public void SetConnected(Node a, Node b)
    {
        a.Connected = true;
        b.Connected = true;
        SetActiveNode(null, fromExistingLine: false, triggerEffects: false);

        NodesConnected?.Invoke(a, b);

        if(IsComplete())
        {
            if(PuzzleConfig.Pack != "Tutorial")
            {
                StatsManager.Instance.OnPuzzleSolved();
                ReviewRequestManager.Instance.OnPuzzleSolved();
            }
            PuzzleCompleted?.Invoke();
            if(PuzzleCompletionManager.Instance)
            {
                PuzzleCompletionManager.Instance.SetPuzzleCompleted(PuzzleConfig.Pack, PuzzleConfig.Id);
            }
            Completed = true;

            CheckForAchievementsOnCompletion();

            if(PuzzleCompleteEffect)
            {
                Instantiate(PuzzleCompleteEffect);
            }
        }
        else if(NodesConnectedEffects.Any())
        {
            var numConnectedPairs = NodesByColor.Count(kvp => kvp.Value.First().Connected);
            var totalPairs = NodesByColor.Count;
            var percentDone = 0f;
            if(totalPairs > 1)
            {
                percentDone = (float)(numConnectedPairs - 1) / (totalPairs - 1);
            }
            var effectToUse = NodesConnectedEffects[Mathf.CeilToInt(Mathf.Lerp(0, NodesConnectedEffects.Count-1, percentDone))];
            Instantiate(effectToUse);
        }
    }

    public void SetDisconnected(Node a)
    {
        var wasConnected = a.Connected;
        a.Connected = false;
        a.PairedNode.Connected = false;
        HintedColors.Remove(a.Color);
        a.SetHintSolutionRemoved();
        a.PairedNode.SetHintSolutionRemoved();

        if(wasConnected)
        {
            NodesDisconnected?.Invoke(a, a.PairedNode);
        }
    }

    private void ResetPuzzle(bool manual)
    {
        if (Nodes != null)
        {
            foreach (var node in Nodes)
            {
                Destroy(node.gameObject);
            }
            Nodes.Clear();
            NodesByColor.Clear();
        }
        ActiveNode = null;
        if (Paths != null)
        {
            foreach (var kvp in Paths)
            {
                Destroy(kvp.Value.gameObject);
            }
            Paths.Clear();
        }
        if(DrawnPointsByGridCell != null)
        {
            DrawnPointsByGridCell.Clear();
        }
        if(Waypoints != null)
        {
            foreach(var waypoint in Waypoints)
            {
                Destroy(waypoint.gameObject);
            }
            Waypoints.Clear();
            WaypointsByGridCell.Clear();
        }
        if(Warps != null)
        {
            foreach(var warp in Warps)
            {
                if(warp.WarpPreviewLine)
                {
                    Destroy(warp.WarpPreviewLine.gameObject);
                }
            }
            foreach(var warp in Warps)
            {
                Destroy(warp.gameObject);
            }
            Warps.Clear();
            WarpsByGridCell.Clear();
        }
        if (Walls != null)
        {
            foreach (var wall in Walls)
            {
                Destroy(wall.gameObject);
            }
            Walls.Clear();
        }

        HintedColors.Clear();
        SetUndoState(-1, false, null);

        Grid.Clear();
        Completed = false;
        if(manual)
        {
            ResetUsed?.Invoke();
        }
    }

	#region Undo
	public void Undo()
    {
        if(LastModifiedColor < 0)
        {
            return;
        }

        if(ActiveNode && ActiveNode.Color == LastModifiedColor)
        {
            SetActiveNode(null, fromExistingLine: false);
        }

        if(Paths[LastModifiedColor])
        {
            foreach (var node in NodesByColor[LastModifiedColor])
            {
                DeleteNodePath(node);
            }
        }

        if(LastModifiedPathState != null && LastModifiedPathState.Count > 0 && LastModifiedPathState[0].Length > 0)
        {
            var firstPathPoint = LastModifiedPathState.First().First();
            var nodeToStartFrom = NodesByColor[LastModifiedColor].OrderBy(n => Vector3.Distance(n.transform.position, firstPathPoint)).First();
            StartPathFromNode(nodeToStartFrom);
            var path = nodeToStartFrom.Path;
            path.Clear();
            foreach(var points in LastModifiedPathState)
            {
                path.StartNewLine();
                foreach(var point in points)
                {
                    path.PositionCount++;
                    path.SetPosition(path.PositionCount - 1, point);

                    InternalHandleLinePointDrawn(nodeToStartFrom.Path, point, LastModifiedColor, applyWarpToPath: false, moveCamera: false);
                }
            }
        }

        if(LastModifiedConnected)
        {
            var nodeA = NodesByColor[LastModifiedColor][0];
            var nodeB = nodeA.PairedNode;

            SetConnected(nodeA, nodeB);
            if(LastModifiedColorWasHintSolved)
            {
                HintedColors.Add(LastModifiedColor);
                nodeA.SetSolvedByHint();
                nodeB.SetSolvedByHint();
            }
        }


        SetUndoState(-1, false, null);
        UndoUsed?.Invoke();
    }

    public void SetUndoState(int color)
    {
        MultiLineRenderer existingPath = Paths.ContainsKey(color) ? Paths[color] : null;
        List<Vector3[]> existingPoints = null;
        if (existingPath)
        {
            existingPoints = existingPath.GetPositionsInLines();
        }
        SetUndoState(color, NodesByColor[color][0].Connected, existingPoints);
    }
    private void SetUndoState(int color, bool connected, List<Vector3[]> points)
    {
        var undoWasAvailable = LastModifiedColor >= 0;
        LastModifiedColor = color;
        LastModifiedConnected = connected;
        LastModifiedPathState = points;
        LastModifiedColorWasHintSolved = HintedColors.Contains(color);
        var undoIsAvailable = LastModifiedColor >= 0;
        if(!undoWasAvailable && undoIsAvailable)
        {
            UndoAvailable?.Invoke();
        }
        else if(undoWasAvailable && !undoIsAvailable)
        {
            UndoUnavailable?.Invoke();
        }
    }
	#endregion

	#region Hints
	public void RevealHint()
    {
        var colorToSolve = PickHintColor();
        if(colorToSolve < 0)
        {
            Debug.Log("No valid colors to solve for a hint");
            return;
        }
        if(!HintManager.Instance.UseHint())
        {
            Debug.Log("No hints remaining");
            return;
        }
        var success = SolveColor(colorToSolve);
        if(!success)
        {
            Debug.LogWarning($"Something went wrong solving for color {colorToSolve}, refunding hint");
            _ = HintManager.Instance.GainHints(1);
            return;
        }

        // Make undo unavailable, as adding this solution may have invalidated whatever state the undo button could try to force
        SetUndoState(-1, false, null);
    }

    private int PickHintColor()
    {
        // Allow player to have control over what the hint reveals, if they want
        if (ActiveNode)
        {
            return ActiveNode.Color;
        }

        // Determine colors to consider at all
        var colorsToConsider = new List<int>();
        foreach (var kvp in NodesByColor)
        {
            if (!HintedColors.Contains(kvp.Key))
            {
                colorsToConsider.Add(kvp.Key);
            }
        }

        if(!colorsToConsider.Any())
        {
            return -1;
        }

        var cellsOccupiedByPlayerPaths = new Dictionary<int, List<GridCell>>();
        foreach(var kvp in Paths)
        {
            var path = kvp.Value;
            var cells = new List<GridCell>();
            for(int i = 0; i < path.PositionCount; i++)
            {
                var cell = Grid.GetLookingAtCell(path.GetPosition(i).ToPolar());
                if(!cells.Contains(cell))
                {
                    cells.Add(cell);
                }
            }

            cellsOccupiedByPlayerPaths.Add(kvp.Key, cells);
        }

        colorsToConsider.Sort((a, b) =>
        {
            // Primary ordering: disconnected nodes come first
            if(!NodesByColor[a][0].Connected && NodesByColor[b][0].Connected)
            {
                return -1;
            }
            if(NodesByColor[a][0].Connected && !NodesByColor[b][0].Connected)
            {
                return 1;
            }

            // Secondary ordering: solutions that would not occupy cells occupied by player paths come first
            var aSolution = GetSolutionForColor(a);
            var bSolution = GetSolutionForColor(b);
            var aMightCut = 0;
            var bMightCut = 0;
            foreach(var kvp in cellsOccupiedByPlayerPaths)
            {
                if(kvp.Key != a)
                {
                    foreach(var cell in kvp.Value)
                    {
                        if(aSolution.Contains(cell))
                        {
                            aMightCut++;
                        }
                    }
                }
                if (kvp.Key != b)
                {
                    foreach (var cell in kvp.Value)
                    {
                        if (bSolution.Contains(cell))
                        {
                            bMightCut++;
                        }
                    }
                }
            }
            if(aMightCut < bMightCut)
            {
                return -1;
            }
            if(bMightCut < aMightCut)
            {
                return 1;
            }

            // Final ordering: longer solutions come first
            return PuzzleConfig.SolutionLengths[b].CompareTo(PuzzleConfig.SolutionLengths[a]);
        });

        return colorsToConsider.First();
    }

    public bool SolveColor(int color)
    {
        var solution = GetSolutionForColor(color);
        return DrawPathForColor(color, solution);
    }

    public bool DrawPathForColor(int color, List<GridCell> cells, bool isHint = true)
    {
        // Reset the state for this color
        foreach (var node in NodesByColor[color])
        {
            DeleteNodePath(node);
        }
        var nodeA = NodesByColor[color][0];
        var nodeB = NodesByColor[color][1];

        if (cells is null || cells.Count == 0)
        {
            // Special case which is technically possible if the nodes are adjacent
            StartPathFromNode(nodeA);
            DrawPointsDetectingWarp(nodeA.GridCell, nodeB.GridCell, color, nodeA.Path);
            SetConnected(nodeA, nodeB);
            if(isHint)
            {
                HintedColors.Add(color);
                nodeA.SetSolvedByHint();
                nodeB.SetSolvedByHint();
            }

            return true;
        }

        // Determine which node is our starting point
        var solutionStart = cells.First();
        var solutionEnd = cells.Last();
        var startA = solutionStart.Neighbors.Contains(nodeA.GridCell);
        var startB = solutionStart.Neighbors.Contains(nodeB.GridCell);
        var endA = solutionEnd.Neighbors.Contains(nodeA.GridCell);
        var endB = solutionEnd.Neighbors.Contains(nodeB.GridCell);

        Node startNode;
        GridCell start;
        GridCell end;

        if (startA && endB)
        {
            startNode = nodeA;
            start = nodeA.GridCell;
            end = nodeB.GridCell;
        }
        else if (startB && endA)
        {
            startNode = nodeB;
            start = nodeB.GridCell;
            end = nodeA.GridCell;
        }
        else
        {
            Debug.LogError($"No valid way to connect solution for color {color} to its nodes");
            return false;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (WarpsByGridCell.ContainsKey(cell))
            {
                var warp = WarpsByGridCell[cell];
                if (warp.Role != Warp.WarpRole.Open && warp.Color != color)
                {
                    var source = warp.Role == Warp.WarpRole.Source ? warp : warp.PairedWarp;
                    var pathTrimPoint = source.PointDrawnInCell.Value;

                    SetDisconnected(NodesByColor[warp.Color][0]);
                    TrimPathToPoint(Paths[warp.Color], warp.Color, pathTrimPoint, includePoint: false);
                }
            }
        }

        StartPathFromNode(startNode);
        var path = startNode.Path;

        var current = start;
        var next = 0;
        while (next < cells.Count)
        {
            bool tookWarp = DrawPointsDetectingWarp(current, cells[next], color, path);
            if (tookWarp)
            {
                path.StartNewLine();
                next++;
            }
            current = cells[next];
            next++;
        }

        DrawPointsDetectingWarp(cells.Last(), end, color, path);

        var toTrim = GetPathColorsToTrimAfterHint(color);
        foreach (var tuple in toTrim)
        {
            SetDisconnected(NodesByColor[tuple.color][0]);
            TrimPathToPoint(Paths[tuple.color], tuple.color, tuple.point, includePoint: true);
        }

        SetConnected(nodeA, nodeB);
        if (isHint)
        {
            HintedColors.Add(color);
            nodeA.SetSolvedByHint();
            nodeB.SetSolvedByHint();
        }

        return true;
    }

    private List<GridCell> GetSolutionForColor(int color)
    {
        int solutionStartIndex = 0;
        for(int i = 0; i < color; i++)
        {
            solutionStartIndex += PuzzleConfig.SolutionLengths[i];
        }
        int solutionLength = PuzzleConfig.SolutionLengths[color];
        var solutionCoordinates = new Vector2Int[solutionLength];
        Array.Copy(PuzzleConfig.Solutions, solutionStartIndex, solutionCoordinates, 0, solutionLength);
        return solutionCoordinates.Select(coords => Grid.CellsByRow[coords.x][coords.y]).ToList();
    }

    private bool DrawPointsDetectingWarp(GridCell from, GridCell to, int color, MultiLineRenderer path)
    {
        var points = GetPointsBetweenCells(from, to, 8);
        foreach(var point in points)
        {
            path.PositionCount++;
            path.SetPosition(path.PositionCount - 1, point);
            InternalHandleLinePointDrawn(path, point, color, notifyWaypoints: false, notifyWarps: false);
            NotifyWaypointsOfLinePointDrawn(point, color);
            bool takingWarp = NotifyWarpsOfLinePointDrawn(path, point, color, applyWarpToPath: true, moveCamera: false);
            if(takingWarp)
            {
                return true;
            }
        }
        return false;
    }

    private List<Vector3> GetPointsBetweenCells(GridCell a, GridCell b, int points)
    {
        var result = new List<Vector3>();

        // points + 2 means points are all BETWEEN a and b
        var tStep = 1f / (points + 2);
        for(float t = tStep; t < 1; t += tStep)
        {
            var raw = Vector3.Lerp(a.transform.position, b.transform.position, t);
            result.Add(raw.normalized);
        }
        return result;
    }

    private List<(int color, Vector3 point)> GetPathColorsToTrimAfterHint(int hintColor)
    {
        var result = new List<(int, Vector3)>();

        var pathPathCollisionDistance = PathCollisionDistance * 2;
        var hintPath = Paths[hintColor];
        var hintPoints = new Vector3[hintPath.PositionCount];
        hintPath.GetPositions(hintPoints);

        foreach(var kvp in Paths)
        {
            if(kvp.Key == hintColor)
            {
                continue;
            }
            var path = kvp.Value;
            Vector3? collisionPoint = null;
            for(int i = 0; i < path.PositionCount; i++)
            {
                var point = path.GetPosition(i);
                foreach (var hintPoint in hintPoints)
                {
                    if(Vector3.Distance(hintPoint, point) < pathPathCollisionDistance)
                    {
                        collisionPoint = point;
                    }
                }
                if(collisionPoint != null)
                {
                    break;
                }
            }

            if(collisionPoint != null)
            {
                result.Add((kvp.Key, collisionPoint.Value));
            }
        }

        return result;
    }

#if ENABLE_MARKETING_TOOLSET || UNITY_EDITOR
    public void StartSolutionSpin()
    {
        CameraController.DoMarketingSpin = true;
        CameraController.MarketingSpinStartTime = Time.time;
        CameraController.AllowRoll = false;
        CameraController.LockInput();
        // Camera distance moves the sphere and nodes entirely within view which is good for making 1:1 aspect content
        CameraController.SnapTo(PuzzleConfig.CameraArmStart, 6.05f, PuzzleConfig.CameraFoV);

        SolveAllColors();
    }
    public void SolveAllColors()
    {
        foreach(var color in NodesByColor.Keys)
        {
            SolveColor(color);
        }
    }
#endif
    #endregion

    #region Setup
	public void SetupPuzzle(PuzzleConfig cfg)
    {
        SetupNodes(cfg);
        SetupWaypoints(cfg);
        SetupWarps(cfg);
        SetupObstacles(cfg);
        SetupView(cfg);
    }

    private const float NODE_VISUAL_SCALE_FACTOR = 2.24f;
    private void SetupNodes(PuzzleConfig cfg)
    {
        ActiveNode = null;
        var children = new List<GameObject>();
        foreach (Transform child in transform) children.Add(child.gameObject);
        children.ForEach(child => Destroy(child));

        Nodes = new List<Node>();
        NodesByColor = new Dictionary<int, List<Node>>();
        Paths = new Dictionary<int, MultiLineRenderer>();

        foreach(var cell in Grid.Cells)
        {
            DrawnPointsByGridCell.Add(cell, new List<Vector3>[6]
            {
                new List<Vector3>(),new List<Vector3>(),new List<Vector3>(),new List<Vector3>(),new List<Vector3>(),new List<Vector3>(),
            });
        }

        float nodeVisualScale = NodeCollisionDistance * NODE_VISUAL_SCALE_FACTOR;

        Func<int, int> colorGetter = (i) => Mathf.FloorToInt(i / 2f);
        if (cfg.NodeColors != null && cfg.NodeColors.Length >= cfg.NodePositions.Length)
        {
            colorGetter = (i) => cfg.NodeColors[i];
        }

        for (int i = 0; i < cfg.NodePositions.Length; i++)
        {
            var newNodeGO = Instantiate(NodePrefab);
            newNodeGO.transform.parent = transform;
            var newNode = newNodeGO.GetComponent<Node>();
            var colorIndex = colorGetter(i);
            newNodeGO.name = $"C{colorIndex} Node";
            newNode.Initialize(this, colorIndex);

            var nodePosition = cfg.NodePositions[i];

            var cell = Grid.CellsByRow[nodePosition.x][nodePosition.y];
            newNode.GridCell = cell;
            newNode.transform.position = cell.transform.position;

            newNode.transform.localScale = new Vector3(nodeVisualScale, nodeVisualScale, nodeVisualScale);

            Nodes.Add(newNode);
            if (NodesByColor.ContainsKey(colorIndex))
            {
                NodesByColor[colorIndex].Add(newNode);
            }
            else
            {
                NodesByColor.Add(colorIndex, new List<Node>() { newNode });
            }
        }

        foreach (var pair in Nodes.GroupBy(n => n.Color))
        {
            if (pair.Count() != 2)
            {
                Debug.LogError($"There should be 2 nodes of color {pair.Key}, but there are {pair.Count()}!");
            }
            var first = pair.ElementAt(0);
            var second = pair.ElementAt(1);
            first.SetPairedNode(second);
            second.SetPairedNode(first);
        }
    }

    private void SetupWaypoints(PuzzleConfig cfg)
    {
        if(cfg.WaypointPositions is null)
        {
            return;
        }
        for (int i = 0; i < cfg.WaypointPositions.Length; i++)
        {
            var color = cfg.WaypointColors[i];

            var row = cfg.WaypointPositions[i].x;
            var rowCell = cfg.WaypointPositions[i].y;

            var newWaypointGO = Instantiate(WaypointPrefab);
            newWaypointGO.transform.parent = transform;
            newWaypointGO.name = $"Waypoint r{row}c{rowCell}";

            var cell = Grid.CellsByRow[row][rowCell];

            var newWaypoint = newWaypointGO.GetComponent<Waypoint>();
            newWaypoint.SetGridCell(cell);
            newWaypoint.SetColor(color);

            Waypoints.Add(newWaypoint);
            WaypointsByGridCell.Add(cell, newWaypoint);
        }
    }

    private void SetupWarps(PuzzleConfig cfg)
    {
        if (cfg.WarpPositions is null)
        {
            return;
        }

        Warp makeWarp(int row, int rowCell)
        {
            var newWarpGO = Instantiate(WarpPrefab);
            newWarpGO.transform.parent = transform;
            newWarpGO.name = $"Warp r{row}c{rowCell}";

            var cell = Grid.CellsByRow[row][rowCell];

            var newWarp = newWarpGO.GetComponent<Warp>();
            newWarp.SetGridCell(cell);

            Warps.Add(newWarp);
            WarpsByGridCell.Add(cell, newWarp);

            return newWarp;
        }

        for(int i = 0; i < cfg.WarpPositions.Length-1; i += 2)
        {
            var row1 = cfg.WarpPositions[i].x;
            var rowCell1 = cfg.WarpPositions[i].y;
            var row2 = cfg.WarpPositions[i + 1].x;
            var rowCell2 = cfg.WarpPositions[i + 1].y;

            var warp1 = makeWarp(row1, rowCell1);
            var warp2 = makeWarp(row2, rowCell2);

            warp1.SetPairedWarp(warp2);
        }
    }

    private void SetupObstacles(PuzzleConfig cfg)
    {
        SetupWalls(cfg);
    }

    private void SetupWalls(PuzzleConfig cfg)
    {
        if (cfg.WallPositions is null)
        {
            return;
        }
        for (int i = 0; i < cfg.WallPositions.Length; i++)
        {
            var row = cfg.WallPositions[i].x;
            var rowCell = cfg.WallPositions[i].y;

            var newWallGO = Instantiate(WallPrefab);
            newWallGO.transform.parent = transform;
            newWallGO.name = $"Wall r{row}c{rowCell}";

            var wallPosition = cfg.WallPositions[i];

            var cell = Grid.CellsByRow[wallPosition.x][wallPosition.y];

            var newWall = newWallGO.GetComponent<Wall>();
            newWall.SetGridCell(cell);

            Walls.Add(newWall);
        }
    }

    private void SetupView(PuzzleConfig cfg)
    {
        if(cfg.OpaqueSphere)
        {
            MainSphere.material.SetFloat("_Opacity", 1f);
        }

        // Camera setup
        CameraController.SnapTo(cfg.CameraArmStart, cfg.CameraDistance, cfg.CameraFoV);
    }
    #endregion

    #region Camera Functionality
	public bool IsCameraPositionValid()
    {
        if(!ActiveNode)
        {
            return true;
        }
        var position = (PuzzleViewCamera.transform.position - transform.position).normalized;
        return IsPositionFree(position, ActiveNode.Color);
    }

    private PolarVector3[] PositionsToTest = new PolarVector3[2]
    {
        new PolarVector3(),
        new PolarVector3(),
    };
    public bool IsPositionFree(Vector3 position, int? excludeColor = null)
    {
        var pointPolar = position.ToPolar();
        var latAdjust = pointPolar.Latitude * 0.96f;
        var paddingLatFactor = 0.92f / Mathf.Cos(latAdjust * Mathf.Deg2Rad);

        var cellPathCollisionPadding = PathCollisionDistance * Mathf.Rad2Deg * 0.9f;
        var lonCellPathCollisionPadding = cellPathCollisionPadding * paddingLatFactor;

        PositionsToTest[0] = pointPolar;
        // Test a version of the point +/- 360 longitude to handle the vertical seam at 0/360
        PositionsToTest[1].Radius = pointPolar.Radius;
        PositionsToTest[1].Latitude = pointPolar.Latitude;
        PositionsToTest[1].Longitude = pointPolar.Longitude + (pointPolar.Longitude < 180 ? 360f : -360f);

        var nodePathCollisionDistance = NodeCollisionDistance + PathCollisionDistance;
        foreach (var node in Nodes.Where(n => n.Color != excludeColor))
        {
            if (Vector3.Distance(position, node.transform.position) < nodePathCollisionDistance)
            {
                return false;
            }
        }

        var pathPathCollisionDistance = PathCollisionDistance * 2;
        // Get current cell + neighbors + neighbors of neighbors
        var nearbyCells = new List<GridCell>();
        var currentCell = Grid.GetLookingAtCell(pointPolar);
        var neighborhood = currentCell.Neighborhood;

        foreach(var cell in neighborhood)
        {
            for(int i = 0; i < 6; i++)
            {
                if(i == excludeColor)
                {
                    continue;
                }
                foreach(var drawnPoint in DrawnPointsByGridCell[cell][i])
                {
                    if (Vector3.Distance(position, drawnPoint) < pathPathCollisionDistance)
                    {
                        return false;
                    }
                }
            }
        }

        foreach (var wall in Walls)
        {
            foreach (var point in PositionsToTest)
            {
                // Latitude "minimum" is the top, so it's actually the max
                if (point.Latitude < wall.GridCell.LatitudeMin + cellPathCollisionPadding && point.Latitude > wall.GridCell.LatitudeMax - cellPathCollisionPadding &&
                    point.Longitude > wall.GridCell.LongitudeMin - lonCellPathCollisionPadding && point.Longitude < wall.GridCell.LongitudeMax + lonCellPathCollisionPadding)
                {
                    return false;
                }
            }
        }

        foreach (var waypoint in Waypoints)
        {
            if(waypoint.Color == excludeColor)
            {
                continue;
            }

            foreach (var point in PositionsToTest)
            {
                // Latitude "minimum" is the top, so it's actually the max
                if (point.Latitude < waypoint.GridCell.LatitudeMin + cellPathCollisionPadding && point.Latitude > waypoint.GridCell.LatitudeMax - cellPathCollisionPadding &&
                    point.Longitude > waypoint.GridCell.LongitudeMin - lonCellPathCollisionPadding && point.Longitude < waypoint.GridCell.LongitudeMax + lonCellPathCollisionPadding)
                {
                    return false;
                }
            }
        }

        var nodeWarpCollisionDistance = NodeCollisionDistance + PathCollisionDistance;
        foreach (var warp in Warps.Where(w => w.Color >= 0 && w.Color != excludeColor))
        {
            if (Vector3.Distance(position, warp.transform.position) < nodePathCollisionDistance)
            {
                return false;
            }
        }

        return true;
    }
    #endregion

    #region Line Smoothing
	public float DISTANCE_TO_ASSUME_WARP = 0.1f;

	[Header("Line Smoothing")]
    public float LineSmoothDistanceWeight = 1f;
    public float LineSmoothAngleWeight = 1f;
    public float MaxSharpness = 10f;
    public bool SmoothLineSegment(Vector3 p1, Vector3 p2, Vector3 p3, out Vector3 resultingP2)
    {
        var p1p2 = p2 - p1;
        var p2p3 = p3 - p2;
        var p1p2Len = Vector3.Magnitude(p1p2);
        var p2p3Len = Vector3.Magnitude(p2p3);

        var distance = p1p2Len + p2p3Len;
        var angle = Vector3.Angle(p2 - p1, p3 - p2);

        var sharpness = (angle * LineSmoothAngleWeight) / (distance * LineSmoothDistanceWeight);

        if(sharpness <= MaxSharpness)
        {
            resultingP2 = p2;
            return false;
        }

        var maxAngle = MaxSharpness * distance * LineSmoothDistanceWeight / LineSmoothAngleWeight;
        var angleOutside = angle - maxAngle;

        var straightP2 = Vector3.Lerp(p1, p3, p1p2Len / (distance));
        var p1p2Straight = straightP2 - p1;
        var p1p2Alt = Vector3.RotateTowards(p1p2, p1p2Straight, angleOutside, float.MaxValue);
        resultingP2 = p1 + p1p2Alt;

        return true;
    }

    public void SmoothEndOfLine(MultiLineRenderer renderer, int lineColor)
    {
        var positionCount = renderer.PositionCount;
        if(positionCount < 3)
        {
            return;
        }

        var p1 = renderer.GetPosition(positionCount - 3);
        var p2 = renderer.GetPosition(positionCount - 2);
        var p3 = renderer.GetPosition(positionCount - 1);

        if(Vector3.Distance(p1, p2) > DISTANCE_TO_ASSUME_WARP ||
            Vector3.Distance(p2, p3) > DISTANCE_TO_ASSUME_WARP)
        {
            return;
        }

        if(SmoothLineSegment(p1, p2, p3, out Vector3 resultingP2))
        {
            // TODO Remove after investigation of disappearing lines over
            if (float.IsNaN(resultingP2.x) || float.IsNaN(resultingP2.y) || float.IsNaN(resultingP2.z))
            {
                var errorsDir = Path.Combine(Application.persistentDataPath, "err");
                if (!Directory.Exists(errorsDir))
                {
                    Directory.CreateDirectory(errorsDir);
                }
                File.AppendAllText(Path.Combine(errorsDir, "drawing_errors.txt"),
                    $"Line smoothing resulted in bad point {resultingP2}\nLine smoothing input that resulted in bad output:\n{p1}\n{p2}\n{p3}\n{Environment.StackTrace}\n\n\n");
            }

            InternalHandleLinePointRemoved(p2, lineColor, notifyWarps: false);
            renderer.SetPosition(positionCount - 2, resultingP2);
            InternalHandleLinePointDrawn(renderer, resultingP2, lineColor, notifyWarps: false);
        }
    }

    public bool DejitterLineSegment(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, out Vector3 resultingP2, out Vector3 resultingP3)
    {
        var p1p2 = p2 - p1;
        var p2p3 = p3 - p2;
        var p3p4 = p4 - p3;
        var p1p4 = p4 - p1;

        var p1p2Flat = Vector3.ProjectOnPlane(p1p2, p1p4);
        var p2p3Flat = Vector3.ProjectOnPlane(p2p3, p1p4);
        var p3p4Flat = Vector3.ProjectOnPlane(p3p4, p1p4);

        var isZigZag = Vector3.Dot(p1p2Flat, p2p3Flat) < 0 &&
            Vector3.Dot(p3p4Flat, p2p3Flat) < 0;

        //var p1p2Cross = Vector3.Cross(p1p2, p1p4);
        //var p2p3Cross = Vector3.Cross(p2p3, p1p4);
        //var p3p4Cross = Vector3.Cross(p3p4, p1p4);

        //var isZigZag = Vector3.Dot(p1p2Cross, p2p3Cross) < 0 &&
        //    Vector3.Dot(p3p4Cross, p2p3Cross) < 0;

        if (!isZigZag)
        {
            resultingP2 = p2;
            resultingP3 = p3;
            return false;
        }

        var p1p3 = p3 - p1;

        var p1p2Alt = Vector3.Project(p1p2, p1p4);
        var p1p3Alt = Vector3.Project(p1p3, p1p4);
        resultingP2 = p1 + p1p2Alt;
        resultingP3 = p1 + p1p3Alt;

        return true;
    }

    public void DejitterEndOfLine(MultiLineRenderer renderer, int lineColor)
    {
        var positionCount = renderer.PositionCount;
        if (positionCount < 4)
        {
            return;
        }

        var p1 = renderer.GetPosition(positionCount - 4);
        var p2 = renderer.GetPosition(positionCount - 3);
        var p3 = renderer.GetPosition(positionCount - 2);
        var p4 = renderer.GetPosition(positionCount - 1);

        if (Vector3.Distance(p1, p2) > DISTANCE_TO_ASSUME_WARP ||
            Vector3.Distance(p2, p3) > DISTANCE_TO_ASSUME_WARP ||
            Vector3.Distance(p3, p4) > DISTANCE_TO_ASSUME_WARP)
        {
            return;
        }

        if (DejitterLineSegment(p1, p2, p3, p4, out Vector3 resultingP2, out Vector3 resultingP3))
        {
            // TODO Remove after investigation of disappearing lines over
            if (float.IsNaN(resultingP2.x) || float.IsNaN(resultingP2.y) || float.IsNaN(resultingP2.z) ||
                float.IsNaN(resultingP3.x) || float.IsNaN(resultingP3.y) || float.IsNaN(resultingP3.z))
            {
                var errorsDir = Path.Combine(Application.persistentDataPath, "err");
                if (!Directory.Exists(errorsDir))
                {
                    Directory.CreateDirectory(errorsDir);
                }
                File.AppendAllText(Path.Combine(errorsDir, "drawing_errors.txt"),
                    $"Line dejittering resulted in bad output {resultingP2}, {resultingP3}\nLine dejittering input that resulted in bad output:\n{p1}\n{p2}\n{p3}\n{p4}\n{Environment.StackTrace}\n\n\n");
            }

            InternalHandleLinePointRemoved(p2, lineColor, notifyWarps: false);
            renderer.SetPosition(positionCount - 3, resultingP2);
            InternalHandleLinePointDrawn(renderer, resultingP2, lineColor, notifyWarps: false);
            InternalHandleLinePointRemoved(p3, lineColor, notifyWarps: false);
            renderer.SetPosition(positionCount - 2, resultingP3);
            InternalHandleLinePointDrawn(renderer, resultingP3, lineColor, notifyWarps: false);
        }
    }
    #endregion

    #region Achievements
    private void CheckForAchievementsOnCompletion()
    {
        var totalTunnels = Warps.Count / 2;
        var tunnelCountsByColor = new Dictionary<int, int>();
        foreach(var color in NodesByColor.Keys)
        {
            tunnelCountsByColor.Add(color, Warps.Count(w => w.Color == color) / 2);
        }

        var manyTunnels = tunnelCountsByColor.Values.Sum() >= 4;
        var multiTunnelLine = tunnelCountsByColor.Values.Any(num => num > 1);
        var tunnelAvoider = (totalTunnels > 0) && tunnelCountsByColor.Values.Sum() == 0;

        if(manyTunnels)
        {
            AchievementManager.Instance.SetCompleted(AchievementManager.Achievement.ManyTunnels);
        }
        if (multiTunnelLine)
        {
            AchievementManager.Instance.SetCompleted(AchievementManager.Achievement.MultiTunnelLine);
        }
        if (tunnelAvoider)
        {
            AchievementManager.Instance.SetCompleted(AchievementManager.Achievement.TunnelAvoider);
        }
    }
    #endregion
}
