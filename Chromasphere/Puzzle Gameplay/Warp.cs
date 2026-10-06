using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Warp : MonoBehaviour
{
    [Header("Required References")]
    public MeshRenderer MeshRenderer;
    public GameObject WarpPreviewLinePrefab;

    [Header("Settings")]
    public Warp PairedWarp;
    public int InnerLinevertices = 20;
    public float InnerLineMaxLerpTowardsCenter = 0.5f;
    public int EnterCurveVertices = 5;

    [Header("State")]
    public WarpRole Role = WarpRole.Open;
    public int Color = -1;
    public LineRenderer WarpPreviewLine;
    public Vector3? PointDrawnInCell;
    public List<Vector3> EntrancePath = new List<Vector3>();

    public GridCell GridCell { get; set; }

    private Color DefaultSurfaceBaseColor;
    private float DefaultSurfaceWaveSpeed;

    private bool IsStartOfInnerLine;
    private Animator InnerLineAnimator;

    // Start is called before the first frame update
    void Start()
    {
        DefaultSurfaceBaseColor = MeshRenderer.material.GetColor("_BaseColor");
        DefaultSurfaceWaveSpeed = MeshRenderer.material.GetFloat("_WaveSpeed");
    }

    public void SetSurfaceColor(int colorIndex)
    {
        var color = colorIndex < 0 ? DefaultSurfaceBaseColor : ColorManager.Instance.GetColor(colorIndex);
        MeshRenderer.material.SetColor("_BaseColor", color);
    }

    public void SetLineColor(int colorIndex)
    {
        var color = colorIndex < 0 ? DefaultSurfaceBaseColor : ColorManager.Instance.GetColor(colorIndex);
        WarpPreviewLine.material.SetColor("_FillColor", color);
    }

    public void TakeWarp(MultiLineRenderer path, Vector3 point, int color, bool applyWarpToPath)
    {
        PointDrawnInCell = point;
        SetAsSource();
        PairedWarp.SetAsDestination();

        Color = color;
        PairedWarp.Color = color;
        PairedWarp.SetSurfaceColor(color);
        PairedWarp.SetLineColor(color);
        SetSurfaceColor(color);
        SetLineColor(color);

        if (applyWarpToPath)
        {
            ApplyWarpToPath(path, point);
        }
        var animatorParam = "Fill";
        if (!IsStartOfInnerLine)
        {
            animatorParam = "FillBackwards";
        }
        InnerLineAnimator.SetBool("Unfill", false);
        InnerLineAnimator.SetBool(animatorParam, true);
    }

    private void ApplyWarpToPath(MultiLineRenderer path, Vector3 startPoint)
    {
        AddEnterCurve(path, startPoint);

        path.StartNewLine();
        path.PositionCount++;
        path.SetPosition(path.PositionCount - 1, PairedWarp.GridCell.transform.position);
    }

    private void AddEnterCurve(MultiLineRenderer path, Vector3 startPoint)
    {
        EntrancePath.Clear();
        var endPointOuter = GridCell.transform.position;
        var endPointInner = GridCell.transform.position * 0.95f;

        var max = EnterCurveVertices;
        for(int i = 1; i < EnterCurveVertices; i++)
        {
            var t = (float)i / max;
            var lerpedEndpoint = Vector3.Lerp(endPointOuter, endPointInner, t);
            var lerped = Vector3.Lerp(startPoint, lerpedEndpoint, t);
            path.PositionCount++;
            path.SetPosition(path.PositionCount - 1, lerped);
            EntrancePath.Add(lerped);
        }
    }

    public bool LinePointRemovedFromCell(Vector3 point)
    {
        var untakeWarp = false;
        if(PointDrawnInCell == point)
        {
            untakeWarp = true;
        }
        foreach(var entryPoint in EntrancePath)
        {
            if(entryPoint == point)
            {
                untakeWarp = true;
            }
        }

        if(untakeWarp)
        {
            if(Role == WarpRole.Source)
            {
                SetAsOpen();
                PairedWarp.SetAsOpen();
                InnerLineAnimator.SetBool("Fill", false);
                InnerLineAnimator.SetBool("FillBackwards", false);
                InnerLineAnimator.SetBool("Unfill", true);
            }
            PointDrawnInCell = null;

            Color = -1;
            PairedWarp.Color = -1;
            SetSurfaceColor(-1);
            PairedWarp.SetSurfaceColor(-1);

            return true;
        }
        return false;
    }

    public void SetGridCell(GridCell cell)
    {
        GridCell = cell;
        transform.position = cell.transform.position;
        transform.LookAt(transform.parent);
        transform.position *= 0.94f;
        transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
    }

    public void SetPairedWarp(Warp other)
    {
        PairedWarp = other;
        other.PairedWarp = this;

        var previewLineGO = Instantiate(WarpPreviewLinePrefab, transform.parent);
        WarpPreviewLine = previewLineGO.GetComponent<LineRenderer>();
        PairedWarp.WarpPreviewLine = WarpPreviewLine;

        var innerPathPoints = GetInnerPathPoints();
        WarpPreviewLine.positionCount = innerPathPoints.Count;
        WarpPreviewLine.SetPositions(innerPathPoints.ToArray());

        var lineAnimator = previewLineGO.GetComponent<Animator>();
        InnerLineAnimator = lineAnimator;
        PairedWarp.InnerLineAnimator = lineAnimator;
        IsStartOfInnerLine = true;
        PairedWarp.IsStartOfInnerLine = false;
    }

    private List<Vector3> GetInnerPathPoints()
    {
        var curveStart = GridCell.transform.position * 0.9f;
        var curveEnd = PairedWarp.GridCell.transform.position * 0.9f;
        var curveFactor = InnerLineMaxLerpTowardsCenter * (Vector3.Distance(curveStart, curveEnd) / 1.8f);

        var linePoints = new List<Vector3>();
        var maxVertex = InnerLinevertices - 1;
        for (int i = 0; i < InnerLinevertices; i++)
        {
            var t = (float)i / maxVertex;
            if (t < 0.5f)
            {
                t = Mathf.Pow(t, 2f) * 2f;
            }
            else if (t > 0.5f)
            {
                t = 1f - Mathf.Pow(1f - t, 2f) * 2f;
            }
            var raw = Vector3.Lerp(curveStart, curveEnd, t);
            var lerpTowardsCenterAmount = 1f - Mathf.Abs(t - 0.5f) * 2f;
            var lerped = Vector3.Lerp(raw, Vector3.zero, Mathf.Pow(lerpTowardsCenterAmount, 0.25f) * curveFactor);
            linePoints.Add(lerped);
        }

        linePoints.Insert(0, GridCell.transform.position * 0.93f);
        linePoints.Add(PairedWarp.GridCell.transform.position * 0.93f);

        return linePoints;
    }

    private void SetAsOpen()
    {
        Role = WarpRole.Open;
        MeshRenderer.material.SetFloat("_WaveSpeed", DefaultSurfaceWaveSpeed);
    }

    private void SetAsSource()
    {
        Role = WarpRole.Source;
        MeshRenderer.material.SetFloat("_WaveSpeed", DefaultSurfaceWaveSpeed);
    }

    private void SetAsDestination()
    {
        Role = WarpRole.Destination;
        MeshRenderer.material.SetFloat("_WaveSpeed", -1f * DefaultSurfaceWaveSpeed);
    }

    public enum WarpRole
    {
        Open,
        Source,
        Destination
    }
}
