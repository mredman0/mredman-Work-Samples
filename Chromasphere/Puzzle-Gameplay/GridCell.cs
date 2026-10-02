using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GridCell : MonoBehaviour
{
    public int Row;
    public int Cell;

    public List<GridCell> Neighbors = new List<GridCell>();
    public List<GridCell> Neighborhood = new List<GridCell>();
    public float DistanceToClosestNeighbor = float.MaxValue;

    public float LatitudeMin;
    public float LatitudeMax;
    public float LongitudeMin;
    public float LongitudeMax;

    public int? Color = null;

    // Start is called before the first frame update
    void Start()
    {

    }

    public void Initialize(int numRows, int numCellsInRow, int row, int cell)
    {
        Row = row;
        Cell = cell;

        float height = 180f / numRows;
        float width = 360f / numCellsInRow;

        float heightOffset = 90f - height * Row;
        float widthOffset = width * Cell;

        LatitudeMin = heightOffset;
        LatitudeMax = heightOffset - height;

        LongitudeMin = widthOffset;
        LongitudeMax = widthOffset + width;

        var middleLat = (LatitudeMin + LatitudeMax) / 2;
        var middleLong = (LongitudeMin + LongitudeMax) / 2;

        // Special case (single-node poles)
        if(LongitudeMax - LongitudeMin == 360f)
        {
            if (Mathf.Abs(LatitudeMin - 90f) < 0.01f)
            {
                transform.localPosition = PolarVector3.ToCartesian(90f, 0);
            }
            else if(Mathf.Abs(LatitudeMax + 90f) < 0.01f)
            {
                transform.localPosition = PolarVector3.ToCartesian(-90f, 0);
            }
        }
        else
        {
            transform.localPosition = PolarVector3.ToCartesian(middleLat, middleLong);
        }
    }

    public int NumFreeNeighbors(Predicate<GridCell> obstructed = null)
    {
        obstructed ??= cell => cell.Color != null;
        return Neighbors.Count(n => !obstructed(n));
    }
    public bool IsDeadEnd(Predicate<GridCell> obstructed = null)
    {
        obstructed ??= cell => cell.Color != null;
        return NumFreeNeighbors(obstructed) < 2;
    }
}
