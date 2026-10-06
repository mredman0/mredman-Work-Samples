using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

public class DailyPuzzlesPage : MonoBehaviour
{
    [Header("Required References")]
    public GameObject SectionPrefab;
    public Transform SectionContainer;
    public GameObject UnlockMorePuzzlesButton;

    [Header("Localization Lookup")]
    public PackInfo[] PackInfo;

    // Start is called before the first frame update
    void Start()
    {
        if(DailyPuzzleManager.Instance.PuzzlesAreReady)
        {
            OnDailyPuzzlesReady();
        }
        DailyPuzzleManager.Instance.DailyPuzzlesReady += OnDailyPuzzlesReady;

#if ENABLE_MARKETING_TOOLSET || UNITY_EDITOR
        DefaultControls.Resources uiResources = new DefaultControls.Resources();
        GameObject uiInputField = DefaultControls.CreateInputField(uiResources);
        uiInputField.transform.SetParent(transform, false);
        uiInputField.transform.GetChild(0).GetComponent<Text>().font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf");
        var dayInput = uiInputField.GetComponent<InputField>();
        dayInput.textComponent.fontSize = 38;
        dayInput.text = DateTime.Today.ToString(DailyPuzzleManager.REQUEST_DATE_FORMAT);

        GameObject uiButton = DefaultControls.CreateButton(uiResources);
        uiButton.transform.SetParent(transform, false);
        var onDemandButton = uiButton.GetComponent<Button>();
        onDemandButton.onClick.AddListener(() =>
        {
            var dateParsed = DateTime.TryParseExact(dayInput.text, DailyPuzzleManager.REQUEST_DATE_FORMAT, null, System.Globalization.DateTimeStyles.None, out DateTime requestDate);
            if (!dateParsed)
            {
                Debug.LogError($"Could not parse date {dayInput.text} to request daily puzzles for");
                return;
            }
            DailyPuzzleManager.Instance.OnDemandFetchPuzzles(requestDate);
        });

        // Position the UI
        RectTransform inputFieldRect = uiInputField.GetComponent<RectTransform>();
        RectTransform buttonRect = uiButton.GetComponent<RectTransform>();

        // Set the anchors to the top-right
        inputFieldRect.anchorMin = new Vector2(1, 1);
        inputFieldRect.anchorMax = new Vector2(1, 1);
        buttonRect.anchorMin = new Vector2(1, 1);
        buttonRect.anchorMax = new Vector2(1, 1);

        // Set the pivot to the top-right for easier positioning
        inputFieldRect.pivot = new Vector2(1, 1);
        buttonRect.pivot = new Vector2(1, 1);

        // Set size
        inputFieldRect.sizeDelta = new Vector2(250, 60);
        buttonRect.sizeDelta = new Vector2(250, 60);

        // Set positions
        inputFieldRect.anchoredPosition = new Vector2(-50, -50); // 50px left, 50px down
        buttonRect.anchoredPosition = new Vector2(-50, -120); // 50px left, 120px down
#endif

        //var onDemandButtonGO = new GameObject("On Demand Button");
        //onDemandButtonGO.transform.parent = transform;

    }

    private void OnDestroy()
    {
        DailyPuzzleManager.Instance.DailyPuzzlesReady -= OnDailyPuzzlesReady;
    }

    private void OnDailyPuzzlesReady()
    {
        foreach(var section in SectionContainer.GetComponentsInChildren<DailyPuzzlesSection>())
        {
            Destroy(section.gameObject);
        }

        foreach(var kvp in DailyPuzzleManager.Instance.PuzzleGroups.OrderBy(kvp => kvp.Value.First()))
        {
            var sectionGO = Instantiate(SectionPrefab, SectionContainer);
            var section = sectionGO.GetComponent<DailyPuzzlesSection>();
            var packInfo = PackInfo.FirstOrDefault(p => p.Id == kvp.Key);
            section.Init(packInfo, kvp.Value);
        }
        UnlockMorePuzzlesButton.SetActive(DailyPuzzleManager.Instance.AnyPuzzlesNotUnlocked());
    }
}
