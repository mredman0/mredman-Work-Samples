using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DailyPuzzleStreakDisplay : MonoBehaviour
{
    [Header("Optional References")]
    public TMP_Text StreakCountText;
    public TMP_Text PuzzlesCompletedTodayText;

    [Header("Settings")]
    public int PuzzlesToMaintainStreak = 3;
    public Color StreakCountNeutralColor = Color.gray;
    public Color StreakCountPositiveColor = Color.yellow;

    private int OutOfValue;

    // Start is called before the first frame update
    void Start()
    {
        PuzzleCompletionManager.Instance.PackCompletionCountUpdated += OnPackCompletionCountUpdated;
        PuzzleCompletionManager.Instance.DailyPuzzleStreakUpdated += OnDailyPuzzleStreakUpdated;
        PurchaseManager.Instance.DailyPuzzleAccessChanged += OnDailyPuzzleAccessChanged;

        if (PurchaseManager.Instance.IsInitialized)
        {
            OnPurchaseManagerInitialized();
        }
        PurchaseManager.Instance.Initialized += OnPurchaseManagerInitialized;

        UpdateStreakCountText(PuzzleCompletionManager.Instance.DailyPuzzleStreak);
    }

    private void OnDestroy()
    {
        PuzzleCompletionManager.Instance.PackCompletionCountUpdated -= OnPackCompletionCountUpdated;
        PuzzleCompletionManager.Instance.DailyPuzzleStreakUpdated -= OnDailyPuzzleStreakUpdated;
        PurchaseManager.Instance.DailyPuzzleAccessChanged -= OnDailyPuzzleAccessChanged;

        PurchaseManager.Instance.Initialized -= OnPurchaseManagerInitialized;
    }

    private void OnPurchaseManagerInitialized()
    {
        OutOfValue = PuzzlesToMaintainStreak < 0 ? PurchaseManager.Instance.CountAccessibleDailyPuzzles() : PuzzlesToMaintainStreak;
        UpdateCompletionCountText(PuzzleCompletionManager.Instance.GetPackStats("Daily").completed, OutOfValue);
    }

    private void OnPackCompletionCountUpdated(string packId)
    {
        if(packId != "Daily")
        {
            return;
        }

        UpdateCompletionCountText(PuzzleCompletionManager.Instance.GetPackStats("Daily").completed, OutOfValue);
    }

    private void OnDailyPuzzleStreakUpdated(int streak)
    {
        UpdateStreakCountText(streak);
    }

    private void OnDailyPuzzleAccessChanged()
    {
        if (PuzzlesToMaintainStreak < 0)
        {
            OutOfValue = PurchaseManager.Instance.CountAccessibleDailyPuzzles();
            UpdateCompletionCountText(PuzzleCompletionManager.Instance.GetPackStats("Daily").completed, OutOfValue);
        }
    }

    private void UpdateCompletionCountText(int completed, int outOf)
    {
        if (!PuzzlesCompletedTodayText)
        {
            return;
        }

        PuzzlesCompletedTodayText.color = completed >= outOf ? StreakCountPositiveColor : StreakCountNeutralColor;
        PuzzlesCompletedTodayText.text = $"{completed}/{outOf}";
    }

    private void UpdateStreakCountText(int streak)
    {
        if(!StreakCountText)
        {
            return;
        }
        StreakCountText.color = streak > 0 ? StreakCountPositiveColor : StreakCountNeutralColor;
        StreakCountText.text = streak.ToString();
    }

}
