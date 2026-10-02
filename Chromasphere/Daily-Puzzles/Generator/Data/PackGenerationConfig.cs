using System.Collections;
using System.Collections.Generic;

[Serializable]
public class PackGenerationConfig
{
    public string PackId;
    public int NumPuzzles;

    public PuzzleGenerationMode Mode;

    public int[] GridCellsPerRow;

    public int MinNodePairs;
    public int MaxNodePairs;
    public MyAnimationCurve TargetNodePairs;

    public int TargetColorsPlusWaypoints;

    public int MinWarpPairs;
    public int MaxWarpPairs;
    public MyAnimationCurve TargetWarpPairs;

    public MyAnimationCurve PreWalling;
    public float PreWallNormalness = 1;
    public MyAnimationCurve PreWallClustering;
    public MyAnimationCurve PreWallNoodling;

    public MyAnimationCurve PostWalling;

    public float EnforceMinColors = 0f;
    public float EnforceMinWarpPairs = 0f;
    public float EnforceMinWaypoints = 0f;
}