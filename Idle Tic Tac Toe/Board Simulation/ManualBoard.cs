using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ManualBoard : Board
{
    public override bool SupportsPassiveSimulation => false;

    public event Action<Big, Big> PotChanged;
    public event Action<int, Big> StreakIncremented;
    public event Action<int, Big> StreakHalved;
    public event Action StreakLost;

    [Space]
    [Header("Manual Board Config")]
    [Header("Required References")]
    public GameObject PotDisplay;
    public TMP_Text PotAmountText;
    public TMP_Text PotTakePercentText;
    public GameObject WinEffect;
    public GameObject StreakDisplay;
    public TMP_Text StreakText;
    public TMP_Text StreakBonusText;

    [Header("Settings")]
    public float SmartOpponentChance;
    [Tooltip("Opponent intelligence multiplier reduction while the player has never won a manual game.")]
    public float NoManualWinsIntelligenceModifier = -0.4f;
    [Tooltip("Opponent intelligence multiplier reduction while no automatic boards are unlocked.")]
    public float NoAutoBoardsIntelligenceModifier = -0.1f;
    public float BasePotTakeAmount = 0.25f;
    public Big BasePotAddAmount = Big.ONE;
    public float BaseMaxStreakBonus = 5;

    [Header("State")]
    public int Streak = 0;
    public Big Pot = Big.ZERO;
    public Big PotAddMultiplier = Big.ONE;
    public float PotTakeAmount = 0.25f;

    protected override float TotalDumbPlayChance =>
        base.TotalDumbPlayChance - SmartOpponentChance -
        FirstTimePlayerSmartOpponentChance() -
        NoAutoBoardsSmartOpponentChance();

    private float FirstTimePlayerSmartOpponentChance() => StatsManager.Instance.ManualGamesWon == 0 ?
        NoManualWinsIntelligenceModifier : 0;

    private float NoAutoBoardsSmartOpponentChance() => !Engine.MainBoards.Any(b => b.Unlocked) ?
        NoAutoBoardsIntelligenceModifier : 0;

    public override void Init()
    {
        base.Init();

        RecalculatePotAddBonus();
        RecalculatePotTakeAmount();
        SettingsManager.Instance.BoolSettingChanged += OnBoolSettingChanged;
        BonusManager.Instance.BigBonusChanged += OnBigBonusChanged;
        BonusManager.Instance.FloatBonusChanged += OnFloatBonusChanged;
    }

    private void OnBigBonusChanged(BonusType bonus, Big total)
    {
        if (bonus == BonusType.PotAddMultiplier)
        {
            RecalculatePotAddBonus();
            return;
        }
        if(bonus == BonusType.ManualStreakMultiplier)
        {
            UpdateStreakDisplay();
            return;
        }
    }

    private void RecalculatePotAddBonus()
    {
        PotAddMultiplier = BasePotAddAmount * BonusManager.Instance.TotalBonusBig(BonusType.PotAddMultiplier);
    }

    private void OnBoolSettingChanged(string key, bool value)
    {
        if (key == Big.SCIENTIFIC_NOTATION_KEY)
        {
            UpdatePotText();
        }
    }

    private new void OnFloatBonusChanged(BonusType bonus, float total)
    {
        if (bonus == BonusType.PotTakeIncrease)
        {
            RecalculatePotTakeAmount();
            return;
        }
        if(bonus == BonusType.ManualStreakMaxIncrease)
        {
            UpdateStreakDisplay();
            return;
        }
    }

    private void RecalculatePotTakeAmount()
    {
        PotTakeAmount = Mathf.Clamp01(BasePotTakeAmount + BonusManager.Instance.TotalBonusFloat(BonusType.PotTakeIncrease));
        PotTakePercentText.text = $"{(int)(PotTakeAmount*100)}%";
    }

    protected override void InitTile(int row, int col)
    {
        base.InitTile(row, col);
        var button = Tiles[row, col].GetComponentInChildren<Button>();
        button.onClick.AddListener(() => {
            ManualPlay(row, col);
        });
    }

    protected override void InitializeStartingPlayer()
    {
        if (BonusManager.Instance.TotalBonusBool(BonusType.ManualGoFirst))
        {
            CurrentTurnOwner = Owner.Player;
            UpdateActive();
            return;
        }
        base.InitializeStartingPlayer();
    }

    public override void MakePlay(Tile tile, bool goldenPlay)
    {
        base.MakePlay(tile, goldenPlay);
        UpdateActive();
    }

    public void UpdateActive()
    {
        Active = CurrentTurnOwner == Owner.Opponent ||
            State == BoardState.Loss ||
            State == BoardState.Win ||
            State == BoardState.Draw;
    }

    public void ManualPlay(int row, int col)
    {
        if (State != BoardState.InProgress) return;
        if (CurrentTurnOwner != Owner.Player) return;
        if (Tiles[row, col].Owner != Owner.None) return;

        var goldenPlay = PremiumInventoryManager.Instance.GoldenHourActive || Rng.PassFail(GoldenTileChance);
        if (BonusManager.Instance.TotalBonusBool(BonusType.LuckyGoldenPlayChance))
        {
            goldenPlay = goldenPlay || Rng.PassFail(GoldenTileChance);
        }

        var tile = Tiles[row, col];
        MakePlay(tile, goldenPlay);
    }

    protected override void OnWon(Tile winningPlay)
    {
        if (IsPrestigeFromManualWinsActive())
        {
            // No Gum or pot take — grant streak-multiplied commendations, then update streak.
            ShowWinLines(true, GoldenTiles > 0);
            Engine.OnBoardWon(this, Big.ZERO);

            var spec = WinningManager.Instance.CurrentSpecialization as PrestigeFromManualWinsSpecialization;
            spec?.GrantCommendationsForManualWin(this);

            RaiseWin(Big.ZERO);
            Instantiate(WinEffect);
            IncrementStreak();
            AchievementManager.Instance.SetCompleted(AchievementManager.Achievement.WinFirstGame);
            return;
        }

        base.OnWon(winningPlay);

        Instantiate(WinEffect);

        var fromPot = Pot * PotTakeAmount;
        TakeFromPot(fromPot);
        var winnings = fromPot * CalculateStreakBonus();
        CurrencyManager.Instance.Earn(winnings);

        IncrementStreak();

        var floater = Instantiate(EarningsFloaterPrefab, PotDisplay.transform);
        if (GoldenTiles > 0) floater.GetComponentInChildren<Image>().color = EarningsFloaterGoldenColor;
        floater.GetComponentInChildren<TMP_Text>().text = $"<sprite name=Gum>+{winnings.ToStringShorthand()}";
        ((RectTransform)floater.transform).anchoredPosition = new Vector2(UnityEngine.Random.Range(-10, 10), UnityEngine.Random.Range(-10, 10));

        AchievementManager.Instance.SetCompleted(AchievementManager.Achievement.WinFirstGame);
    }

    protected override void OnDraw()
    {
        base.OnDraw();
        HalveStreak();
    }

    protected override void OnLoss(Tile losingPlay)
    {
        base.OnLoss(losingPlay);
        LoseStreak();

        if (IsPrestigeFromManualWinsActive())
        {
            var spec = WinningManager.Instance.CurrentSpecialization as PrestigeFromManualWinsSpecialization;
            spec?.WipeUnspentCommendations();
        }
    }

    protected override Big CalculateWinValue() =>
        WinValue *
        Math.Pow(GetGoldenPlayRewardMultiplierPerTile(), GoldenTiles) *
        TrickleDownGumSkillMultiplier *
        BonusManager.Instance.TotalBonusBig(BonusType.BoardRewardsMultiplier) *
        BonusManager.Instance.TotalBonusBig(BonusType.PrimaryCurrencyEarnings) *
        CalculateStreakBonus();

    public Big CalculateStreakBonus()
    {
        if (Streak == 0) return Big.ONE;

        var rawMultiplier = 1f;
        rawMultiplier += Streak;
        var maxStreakBonus = BaseMaxStreakBonus + BonusManager.Instance.TotalBonusFloat(BonusType.ManualStreakMaxIncrease);
        rawMultiplier = Mathf.Clamp(rawMultiplier, 1f, maxStreakBonus);

        var multiplier = new Big(rawMultiplier, 0);
        multiplier *= BonusManager.Instance.TotalBonusBig(BonusType.ManualStreakMultiplier);

        return multiplier;
    }

    public void OnBoardWon(Board board, Big reward)
    {
        if (board == this) return;
        if (IsPrestigeFromManualWinsActive()) return;
        AddToPot(reward * PotAddMultiplier);
    }

    public void SetStreak(int streak)
    {
        Streak = streak;
        StreakIncremented?.Invoke(Streak, CalculateStreakBonus());

        UpdateStreakDisplay();
    }

    public void IncrementStreak()
    {
        Streak++;
        StreakIncremented?.Invoke(Streak, CalculateStreakBonus());

        UpdateStreakDisplay();
    }

    public void HalveStreak()
    {
        Streak /= 2;
        StreakHalved?.Invoke(Streak, CalculateStreakBonus());

        UpdateStreakDisplay();
    }

    public void LoseStreak()
    {
        Streak = 0;
        StreakLost?.Invoke();

        UpdateStreakDisplay();
    }

    public void UpdateStreakDisplay()
    {
        StreakDisplay.SetActive(Streak > 0);
        StreakText.text = $"Streak\n<b>{Streak}</b>";
        StreakBonusText.text = $"x{CalculateStreakBonus().ToStringShortInt()}";
    }

    /// <summary>
    /// Hides pot UI and clears the pot while PrestigeFromManualWins is active.
    /// </summary>
    public void RefreshPrestigeFromManualWinsPresentation(bool active)
    {
        if (PotDisplay) PotDisplay.SetActive(!active);
        if (active) SetPot(Big.ZERO);
    }

    private static bool IsPrestigeFromManualWinsActive() =>
        WinningManager.Instance != null &&
        WinningManager.Instance.IsSpecializationSelected<PrestigeFromManualWinsSpecialization>();

    public void SetPot(Big total)
    {
        Pot = total;
        UpdatePotText();
        PotChanged?.Invoke(Pot, Big.ZERO);
    }

    public void AddToPot(Big amount)
    {
        if (IsPrestigeFromManualWinsActive()) return;
        Pot += amount;
        UpdatePotText();
        PotChanged?.Invoke(Pot, amount);
    }

    public void TakeFromPot(Big amount)
    {
        Pot -= amount;
        UpdatePotText();
        PotChanged?.Invoke(Pot, amount * -1);
    }

    private void UpdatePotText()
    {
        PotAmountText.text = $"{Pot.ToStringShorthand()} <sprite name=Gum>";
    }

    public string SerializeBoardState()
    {
        if(State != BoardState.InProgress)
        {
            // Special case to prevent restoring an already-done game later
            return "end";
        }

        var output = "";
        output += (int)CurrentTurnOwner;
        for(int row = 0; row < Size; row++)
        {
            for(int col = 0; col < Size; col++)
            {
                output += (int)(Tiles[row, col].Owner);
            }
        }
        return output;
    }

    public void DeserializeBoardState(string serialized)
    {
        if(serialized is null)
        {
            return;
        }

        if(serialized == "end")
        {
            // Special case, indicating the serialized board state was a completed game, do nothing
            return;
        }
        if (serialized.Length < Size*Size + 1)
        {
            Debug.LogError($"Could not deserialize manual board state from string: {serialized}");
            return;
        }
        CurrentTurnOwner = (Owner)(int.Parse(serialized.Substring(0, 1)));
        for (int row = 0; row < Size; row++)
        {
            for(int col = 0; col < Size; col++)
            {
                int t = row * Size + col + 1;
                var tileOwner = (Owner)(int.Parse(serialized.Substring(t, 1)));
                if(tileOwner != Owner.None)
                {
                    Tiles[row, col].AssignOwner(tileOwner, false);
                }
            }
        }

        FreeTiles.Clear();
        foreach(var tile in Tiles)
        {
            if(tile.Owner == Owner.None)
            {
                FreeTiles.Add(tile);
            }
        }
    }
}
