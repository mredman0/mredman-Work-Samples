using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleObjectWarpLite : PuzzleObjectLite
{
    public PuzzleObjectWarpLite PairedWarp;

    public void SetPairedWarp(PuzzleObjectWarpLite other)
    {
        Unpair();
        PairedWarp = other;

        // Meet your new neighbor!
        Cell.Neighbors.Add(PairedWarp.Cell);
    }

    public void Unpair()
    {
        if (PairedWarp != null)
        {
            Cell.Neighbors.Remove(PairedWarp.Cell);
            PairedWarp.Cell.Neighbors.Remove(Cell);

            PairedWarp.PairedWarp = null;
            PairedWarp = null;
        }
    }
}