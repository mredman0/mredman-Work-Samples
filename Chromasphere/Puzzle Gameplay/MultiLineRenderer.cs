using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MultiLineRenderer : MonoBehaviour
{
    public GameObject LinePrefab;

    private Color color;
    public Color Color
    {
        get => color;
        set
        {
            color = value;
            PropogateColorChange();
        }
    }

    private int positionCount = 0;
    public int PositionCount
    {
        get => positionCount;
        set
        {
            var difference = value - positionCount;
            if (difference == 0)
            {
                return;
            }
            if (difference > 0)
            {
                Lines.Last().positionCount += difference;
            }
            while(difference < 0)
            {
                var lastLine = Lines.LastOrDefault();
                if(!lastLine)
                {
                    break;
                }
                if(lastLine.positionCount == 0)
                {
                    Lines.Remove(lastLine);
                    Destroy(lastLine.gameObject);
                    continue;
                }
                lastLine.positionCount--;
                difference++;
            }
            positionCount = value;
        }
    }
    private float startWidth = 1;
    public float StartWidth
    {
        get => startWidth;
        set
        {
            startWidth = value;
            PropogateWidthChange();
        }
    }

    private float endWidth = 1;
    public float EndWidth
    {
        get => endWidth;
        set
        {
            endWidth = value;
            PropogateWidthChange();
        }
    }


    private List<LineRenderer> Lines = new List<LineRenderer>();

    // Start is called before the first frame update
    void Start()
    {

    }

    public void StartNewLine()
    {
        var newLineGO = Instantiate(LinePrefab, transform);
        var newLine = newLineGO.GetComponent<LineRenderer>();
        newLine.positionCount = 0;

        newLine.material.SetColor("_Color", Color);
        newLine.startWidth = StartWidth;
        newLine.endWidth = EndWidth;

        Lines.Add(newLine);
    }

	#region Get
    public Vector3 GetPosition(int index)
    {
        if (index < 0 || index >= PositionCount)
        {
            throw new IndexOutOfRangeException();
        }

        foreach (var line in Lines)
        {
            if (index >= line.positionCount)
            {
                index -= line.positionCount;
                continue;
            }
            return line.GetPosition(index);
        }

        throw new IndexOutOfRangeException();
    }

    public int GetPositions(Vector3[] positions)
    {
        int offset = 0;
        foreach(var line in Lines)
        {
            var linePoints = new Vector3[line.positionCount];
            line.GetPositions(linePoints);
            Array.Copy(linePoints, 0, positions, offset, linePoints.Length);
            offset += linePoints.Length;
        }
        return PositionCount;
    }

    public List<Vector3[]> GetPositionsInLines()
    {
        var result = new List<Vector3[]>();
        foreach(var line in Lines)
        {
            var points = new Vector3[line.positionCount];
            line.GetPositions(points);
            result.Add(points);
        }
        return result;
    }
	#endregion

	#region Set
    public void SetPosition(int index, Vector3 position)
    {
        if(index < 0 || index >= PositionCount)
        {
            throw new IndexOutOfRangeException();
        }

        foreach(var line in Lines)
        {
            if(index >= line.positionCount)
            {
                index -= line.positionCount;
                continue;
            }
            line.SetPosition(index, position);
            return;
        }

        throw new IndexOutOfRangeException();
    }

    public void Clear()
    {
        foreach(var line in Lines)
        {
            Destroy(line.gameObject);
        }
        Lines.Clear();
        positionCount = 0;
    }
	#endregion

	#region Point Cleanup
    public List<Vector3> CleanupPoints()
    {
        var pointsRemoved = new List<Vector3>();
        foreach(var line in Lines)
        {
            pointsRemoved.AddRange(CleanupPoints(line));
        }
        return pointsRemoved;
    }
    public List<Vector3> CleanupCurrentLinePoints()
    {
        return CleanupPoints(Lines.Last());
    }

	private List<Vector3> CleanupPoints(LineRenderer line)
    {
        if(!Lines.Contains(line))
        {
            Debug.LogError($"Cannot clean up points... MultiLineRenderer \"{name}\" does not contain LineRenderer \"{line.name}\"!");
            return null;
        }
        var tryAgain = true;
        var pointsRemoved = new List<Vector3>();
        while (tryAgain)
        {
            Vector3[] positions = new Vector3[line.positionCount];
            line.GetPositions(positions);

            var pointsToRemove = new List<Vector3>();

            for (int i = 0; i < positions.Length - 2; i++)
            {
                float distanceToNext = Vector3.Distance(positions[i], positions[i + 1]);
                float distanceToTwoAhead = Vector3.Distance(positions[i], positions[i + 2]);

                if (distanceToTwoAhead < distanceToNext)
                {
                    pointsToRemove.Add(positions[i + 1]);
                }
            }

            if (pointsToRemove.Any())
            {
                var positionsList = positions.ToList();
                var trimmedPositions = positionsList.Except(pointsToRemove).ToArray();
                line.positionCount = trimmedPositions.Length;
                for (int i = 0; i < trimmedPositions.Length; i++)
                {
                    line.SetPosition(i, trimmedPositions[i]);
                }

                // Set positionCount internally which won't make additional changes
                positionCount -= pointsToRemove.Count;
                pointsRemoved.AddRange(pointsToRemove);
            }
            tryAgain = pointsToRemove.Any();
        }
        return pointsRemoved;
    }
	#endregion

	#region Internal Update
	private void PropogateWidthChange()
    {
        foreach (var line in Lines)
        {
            line.startWidth = StartWidth;
            line.endWidth = endWidth;
        }
    }

    private void PropogateColorChange()
    {
        foreach (var line in Lines)
        {
            line.material.SetColor("_Color", color);
        }
    }
    #endregion
}