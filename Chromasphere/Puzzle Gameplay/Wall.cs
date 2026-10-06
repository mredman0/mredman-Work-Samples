using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wall : MonoBehaviour
{
    [Header("Required References")]
    public MeshFilter MeshFilter;
    public MeshRenderer MeshRenderer;

    [Header("Settings")]
    public float VerticesPerDegree = 0.5f;

    public GridCell GridCell { get; set; }

    // Start is called before the first frame update
    void Start()
    {

    }

    public void SetGridCell(GridCell cell)
    {
        GridCell = cell;
        var latitudeSegments = Mathf.CeilToInt(VerticesPerDegree * Mathf.Abs(cell.LatitudeMin - cell.LatitudeMax));
        var longitudeSegments = Mathf.CeilToInt(VerticesPerDegree * Mathf.Abs(cell.LongitudeMin - cell.LongitudeMax));
        MeshFilter.sharedMesh = MeshGenerator.GenerateSphereSector(cell.LatitudeMin, cell.LatitudeMax, cell.LongitudeMin, cell.LongitudeMax, 0.95f, latitudeSegments, longitudeSegments);
    }
}
