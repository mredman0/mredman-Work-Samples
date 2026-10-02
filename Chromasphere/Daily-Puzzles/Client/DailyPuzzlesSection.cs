using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

public class DailyPuzzlesSection : MonoBehaviour
{
    [Header("Required References")]
    public GameObject PuzzleButtonPrefab;
    public RectTransform PuzzleButtonsContainer;
    public TMP_Text TitleText;
    public LocalizeStringEvent TitleLoc;

    public void Init(PackInfo packInfo, IEnumerable<int> puzzles)
    {
        var tint = packInfo ? packInfo.Tint : Color.white;
        TitleText.color = tint;
        TitleLoc.StringReference = packInfo ? packInfo.Name : null;
        TitleLoc.RefreshString();

        foreach(var id in puzzles)
        {
            var buttonGO = Instantiate(PuzzleButtonPrefab, PuzzleButtonsContainer);
            var loadPuzzleButton = buttonGO.GetComponent<LoadPuzzleButton>();

            buttonGO.GetComponent<Image>().color = tint;

            var idString = id.ToString();
            loadPuzzleButton.ButtonText.text = idString;
            loadPuzzleButton.PuzzlePack = "Daily";
            loadPuzzleButton.PuzzleIdInPack = idString;
            loadPuzzleButton.PuzzleLoader.PuzzlePack = "Daily";
            loadPuzzleButton.PuzzleLoader.PuzzleIdInPack = idString;

            loadPuzzleButton.SetButtonSpriteBasedOnCompletion();
        }
    }
}
