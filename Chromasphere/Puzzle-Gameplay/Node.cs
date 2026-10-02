using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Node : MonoBehaviour
{
    public List<Material> ColorIconMaterials;
    public Material HintedColorIconMaterial;

    public Puzzle Puzzle;
    public int Color;
    public bool Connected = false;
    public bool Active = false;

    public GameObject PathPrefab;
    public MultiLineRenderer Path;

    public GameObject ColorIconArm;
    public MeshRenderer ColorIconRenderer;

    public GridCell GridCell;

    public Node PairedNode { get; private set; }

    // Start is called before the first frame update
    void Start()
    {
        ColorIconArm.SetActive(SettingsManager.Instance.GetBool(SHOW_COLOR_ICONS_KEY));
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
            ColorIconArm.SetActive(value || IsSolvedByHint());
        }
    }

    public void StartPath()
    {
        Path = Instantiate(PathPrefab).GetComponent<MultiLineRenderer>();

        var pathColor = ColorManager.Instance.GetColor(Color);
        Path.Color = pathColor;
        if (Puzzle)
        {
            Path.transform.parent = Puzzle.transform;
        }

        Path.StartNewLine();
        Path.PositionCount++;
        Path.SetPosition(0, transform.position);
    }

    public void Initialize(Puzzle puzzle, int color)
    {
        Puzzle = puzzle;
        SetColor(color);
    }

    public void SetColor(int color)
    {
        Color = color;
        var mappedColor = ColorManager.Instance.GetColor(color);
        GetComponent<Renderer>().material.SetColor("_Color", mappedColor);
        if(Path)
        {
            Path.Color = mappedColor;
        }
        ColorIconRenderer.material = IsSolvedByHint() ? HintedColorIconMaterial : ColorIconMaterials[Color];
    }

    public void SetPairedNode(Node other)
    {
        PairedNode = other;
    }

    public void SetSelected()
    {
        GetComponent<Animator>().SetBool("Selected", true);
    }
    public void SetDeselected()
    {
        GetComponent<Animator>().SetBool("Selected", false);
    }


	#region Hint Display
	public bool IsSolvedByHint() => Puzzle != null && Puzzle.HintedColors.Contains(Color);

    public void SetSolvedByHint()
    {
        ColorIconRenderer.material = HintedColorIconMaterial;
        ColorIconArm.SetActive(true);
    }

    public void SetHintSolutionRemoved()
    {
        var showIcon = SettingsManager.Instance.GetBool(SHOW_COLOR_ICONS_KEY);
        ColorIconRenderer.material = ColorIconMaterials[Color];
        ColorIconArm.SetActive(showIcon);
    }
	#endregion
}
