using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Waypoint : MonoBehaviour
{
    public List<Material> ColorIconMaterials;

    [Header("Required References")]
    public MeshFilter MeshFilter;
    public MeshRenderer MeshRenderer;
    public Animator Animator;
    public MeshRenderer ColorIconRenderer;

    [Header("Settings")]
    public int Color;
    public float VerticesPerDegree = 0.5f;

    [Header("State")]
    public bool Reached;

    public GridCell GridCell { get; set; }

    private List<Vector3> DrawnPointsInCell = new List<Vector3>();

    // Start is called before the first frame update
    void Start()
    {
        ColorIconRenderer.gameObject.SetActive(SettingsManager.Instance.GetBool(SHOW_COLOR_ICONS_KEY));
        SettingsManager.Instance.BoolSettingChanged += OnBoolSettingChanged;
    }

    private void OnDestroy()
    {
        SettingsManager.Instance.BoolSettingChanged -= OnBoolSettingChanged;
    }

    private const string SHOW_COLOR_ICONS_KEY = "AccessibilityShowColorIcons";
    private void OnBoolSettingChanged(string setting, bool value)
    {
        if (setting == SHOW_COLOR_ICONS_KEY)
        {
            ColorIconRenderer.gameObject.SetActive(value);
        }
    }

    public void SetGridCell(GridCell cell)
    {
        var radiusMultiplier = 0.95f;
        GridCell = cell;
        var latitudeSegments = Mathf.CeilToInt(VerticesPerDegree * Mathf.Abs(cell.LatitudeMin - cell.LatitudeMax));
        var longitudeSegments = Mathf.CeilToInt(VerticesPerDegree * Mathf.Abs(cell.LongitudeMin - cell.LongitudeMax));
        MeshFilter.sharedMesh = MeshGenerator.GenerateSphereSectorRounded(cell.LatitudeMin, cell.LatitudeMax, cell.LongitudeMin, cell.LongitudeMax, radiusMultiplier, latitudeSegments, longitudeSegments, 0.25f);
        ColorIconRenderer.transform.position = cell.transform.position * radiusMultiplier;
    }
    public void SetColor(int colorIndex)
    {
        Color = colorIndex;
        MeshRenderer.material.SetColor("_Color", ColorManager.Instance.GetColor(colorIndex));
        ColorIconRenderer.material = ColorIconMaterials[Color];
    }

    public void LinePointDrawnInCell(Vector3 point, int color)
    {
        if(!DrawnPointsInCell.Any())
        {
            SetReached(true);
        }
        DrawnPointsInCell.Add(point);
    }

    public void LinePointRemovedFromCell(Vector3 point)
    {
        DrawnPointsInCell.Remove(point);
        if(!DrawnPointsInCell.Any())
        {
            SetReached(false);
        }
    }

    private void SetReached(bool reached)
    {
        Reached = reached;
        Animator.SetBool("Reached", reached);
    }
}
