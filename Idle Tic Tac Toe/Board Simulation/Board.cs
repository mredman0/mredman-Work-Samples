using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Board : MonoBehaviour
{
    public event Action Unlock;
    public event Action Reset;
    public event Action<Big> Win;
    public event Action Loss;
    public event Action Draw;

    [Header("Required References")]
    public Engine Engine;
    public RectTransform TileGrid;
    public GameObject LockedPanel;
    public Button UnlockButton;
    public TMP_Text UnlockCostText;
    public Image PlayQuickerProgressImage;
    public GameObject PlayQuickerBaseIcon;
    public GameObject TilePrefab;
    public GameObject EarningsFloaterPrefab;
    public GameObject WinLinePrefab;

    [Header("Passive mode UI")]
    [Tooltip("Shown while the board is in high-speed passive (analytic) mode.")]
    public GameObject PassiveModePanel;
    [Tooltip("Displays expected Gum per second from the passive earnings model.")]
    public TMP_Text PassiveModeGumRateText;

    [Header("Upgrades")]
    public WinValueUpgrade WinValueUpgrade;
    public PlaySpeedUpgrade PlaySpeedUpgrade;
    public PlayIntelligenceUpgrade PlayIntelligenceUpgrade;
    public GoldenTileUpgrade GoldenTileUpgrade;

    [Header("Settings")]
    public bool IncludeInSaveData = true;
    public string Id;
    public int Tier = -1;
    public int Size = 3;
    public bool UnlockedByDefault = false;
    public Big BaseUnlockCost = Big.ZERO;
    public Big BaseWinValue = (Big)1;
    public float BaseTurnInterval = 1f;

    public Big UpgradeCostMultiplier = (Big)1;

    public Owner GoFirst = Owner.Opponent;

    public float BasePlayQuickerDuration = 1f;
    public float BasePlayQuickerFactor = 5f;

    public bool AllowDoublePlay = true;

    public float HiddenSmarts = 0f;
    public float HiddenSmartsPlayFaster = 0f;

    [Header("Visual Settings")]
    public Color EarningsFloaterGoldenColor;
    public float MinTimeBetweenEarningsFloaters = 0.2f;

    [Header("State")]
    public Big UnlockCost = Big.ZERO;
    public bool Active;
    public bool Unlocked;
    public Tile[,] Tiles;
    public BoardState State = BoardState.Reset;

    public Owner CurrentTurnOwner;
    public int TurnsTaken = 0;
    public float TimeSinceLastTurn = 0f;

    public int GoldenTiles = 0;

    public bool OpponentNoGo = false;
    public int CachedFreePlays = 0;

    public float PlayQuickerStart = -1f;
    public float PlayQuickerEnd = 0f;

    private int HigherBoardsUnlocked = 0;

    private bool IsPlayingQuicker => Time.time < PlayQuickerEnd;
    private bool IsPlayingQuickerCached = false;

    public System.Random Rng = new System.Random();

    [Header("Upgradable Stats")]

    [ReadOnly]
    public Big WinValue = (Big)0;
    [ReadOnly]
    public float TurnInterval = 1f;
    [ReadOnly]
    public float SmartPlayChance = 0f;
    [ReadOnly]
    public float DumbPlayChance = 0f;
    [ReadOnly]
    public float GoldenTileChance = 0f;

    /// <summary>True while this board uses analytic wins instead of Tick() simulation.</summary>
    public bool IsPassiveSimulation { get; private set; }

    /// <summary>Tier auto-boards support passive mode; president and manual boards do not.</summary>
    public virtual bool SupportsPassiveSimulation => Tier >= 0;

    private double _fractionalPassiveWins;
    private const int MaxSyntheticPassiveWinsPerUpdate = 48;

	private void Awake()
    {
        RecalculateUnlockCost();
        SettingsManager.Instance.BoolSettingChanged += OnBoolSettingChanged;
        ProgressManager.Instance.ProgressLoaded += OnProgressLoaded;
		CurrencyManager.Instance.AvailableCurrencyChanged += OnAvailableCurrencyChanged;
		BonusManager.Instance.FloatBonusChanged += OnFloatBonusChanged;
        if (SupportsPassiveSimulation)
            BonusManager.Instance.BigBonusChanged += OnBigBonusChangedForPassive;
	}

    private void Update()
    {
        if(IsPlayingQuicker != IsPlayingQuickerCached)
        {
            UpdateAnimationsSpeed();
            IsPlayingQuickerCached = IsPlayingQuicker;
        }
        if (PlayQuickerProgressImage)
        {
            UpdatePlayQuickerProgressImage();
        }
        if (SupportsPassiveSimulation && Unlocked && Engine != null)
            UpdatePassiveModeUi();
    }

    private void UpdatePlayQuickerProgressImage()
    {
        var fill = Mathf.Clamp01(Mathf.InverseLerp(PlayQuickerEnd, PlayQuickerStart, Time.time));
        PlayQuickerProgressImage.fillAmount = fill;
        PlayQuickerBaseIcon.SetActive(fill > 0);
    }

    public virtual void Init()
	{
		if (IncludeInSaveData && string.IsNullOrWhiteSpace(Id))
		{
			Debug.LogError($"A board has no Id, this is bad and will not save/load progress!");
		}
		Tiles = new Tile[Size, Size];
		for (int row = 0; row < Size; row++)
		{
			for (int col = 0; col < Size; col++)
			{
                InitTile(row, col);
			}
		}
		SetUnlocked(UnlockedByDefault);

		InitializeStartingPlayer();

		RecalculateUnlockCost();

        if (WinValueUpgrade) WinValueUpgrade.Init();
        if (PlaySpeedUpgrade) PlaySpeedUpgrade.Init();
        if (PlayIntelligenceUpgrade) PlayIntelligenceUpgrade.Init();
        if (GoldenTileUpgrade) GoldenTileUpgrade.Init();

        if (SupportsPassiveSimulation)
        {
            SubscribePassiveDependencyEvents();
            RefreshPassiveSimulationMode();
        }
    }

    protected virtual void InitTile(int row, int col)
    {
        var tileGO = Instantiate(TilePrefab, TileGrid);
        Tiles[row, col] = tileGO.GetComponent<Tile>();
        Tiles[row, col].Init(Size, row, col);
        FreeTiles.Add(Tiles[row, col]);
    }

	protected void OnDestroy()
    {
        SettingsManager.Instance.BoolSettingChanged -= OnBoolSettingChanged;
        ProgressManager.Instance.ProgressLoaded -= OnProgressLoaded;
		CurrencyManager.Instance.AvailableCurrencyChanged -= OnAvailableCurrencyChanged;
		BonusManager.Instance.FloatBonusChanged -= OnFloatBonusChanged;
        if (BonusManager.Instance != null && SupportsPassiveSimulation)
            BonusManager.Instance.BigBonusChanged -= OnBigBonusChangedForPassive;
        UnsubscribePassiveDependencyEvents();
    }

	private void OnProgressLoaded()
    {
        if(!IncludeInSaveData || !ProgressManager.Instance.BoardProgressDict.ContainsKey(Id))
        {
            return;
        }
        PlayQuickerEnd = PlayQuickerStart;
        var data = ProgressManager.Instance.BoardProgressDict[Id];
        SetUnlocked(data.On > 0 || UnlockedByDefault);
		if (WinValueUpgrade) WinValueUpgrade.SetLevelUnchecked(data.R);
        if (PlaySpeedUpgrade) PlaySpeedUpgrade.SetLevelUnchecked(data.S);
        if (PlayIntelligenceUpgrade) PlayIntelligenceUpgrade.SetLevelUnchecked(data.I);
        if (GoldenTileUpgrade) GoldenTileUpgrade.SetLevelUnchecked(data.G);
        RefreshPassiveSimulationMode();
    }

    private void OnBoolSettingChanged(string key, bool value)
    {
        if (key == Big.SCIENTIFIC_NOTATION_KEY)
        {
            UpdateUnlockCostText();
        }
    }

    protected virtual void OnFloatBonusChanged(BonusType bonus, float val)
	{
        if(bonus == BonusType.BoardUnlockCost || bonus == BonusType.GlobalGumCostMultiplier)
		{
			RecalculateUnlockCost();
		}
        else if(bonus == BonusType.SkillTrickleDownGum)
        {
            RecalculateTrickleDownGumSkillMultiplier();
        }

        if (SupportsPassiveSimulation && bonus == BonusType.DoublePlayChance)
            RefreshPassiveSimulationMode();
	}

    protected virtual void RecalculateUnlockCost()
	{
		UnlockCost = BaseUnlockCost *
            BonusManager.Instance.TotalBonusFloat(BonusType.BoardUnlockCost) *
            BonusManager.Instance.TotalBonusFloat(BonusType.GlobalGumCostMultiplier);
		UpdateUnlockButtonInteractable();
		UpdateUnlockCostText();
	}

    public void ResetBoard()
    {
        State = BoardState.InProgress;
        InitializeStartingPlayer();
        TurnsTaken = 0;
        GoldenTiles = 0;
        FreeTiles.Clear();
        for(int row = 0; row < Size; row++)
        {
            for(int col = 0; col < Size; col++)
            {
                Tiles[row, col].ResetTile();
                FreeTiles.Add(Tiles[row, col]);
            }
        }

        WonRow = -1;
        WonCol = -1;
        WonPrimaryDiag = false;
        WonSecondaryDiag = false;
        foreach(var winLine in WinLines)
        {
            Destroy(winLine.gameObject);
        }
        WinLines.Clear();

        Reset?.Invoke();

        // This could theoretically cause problems is enough cached
        // free plays were accrued, but that *shouldn't* happen!
        while(CachedFreePlays > 0 && State == BoardState.InProgress)
        {
            CachedFreePlays--;
            FreePlay();
        }
    }

    protected virtual void InitializeStartingPlayer()
    {
        if (GoFirst == Owner.None)
        {
            CurrentTurnOwner = Rng.PassFail(0.5f) ? Owner.Opponent : Owner.Player;
        }
        else
        {
            CurrentTurnOwner = GoFirst;
        }
    }

    public void SetGoFirst(Owner goFirst)
    {
        GoFirst = goFirst;
        if(TurnsTaken == 0)
        {
            InitializeStartingPlayer();
        }
    }

    private bool WasPlayingQuicker = false;
    public void DoUpdate(float dt)
    {
        if(!Active || !Unlocked)
        {
            return;
        }

        if(!IsPlayingQuicker && WasPlayingQuicker)
        {
            // Try automatically refreshing according to chance from skills
            var refreshAutomatically = Util.PassFail(Rng, BonusManager.Instance.TotalBonusFloat(BonusType.PlayFasterAutoRenewChance));
            if (refreshAutomatically)
            {
                RefreshPlayingQuicker();
            }
        }
        if (IsPlayingQuicker)
        {
            dt *= BasePlayQuickerFactor; // TODO multiply by bonuses?
        }
        WasPlayingQuicker = IsPlayingQuicker;

        RefreshPassiveSimulationMode();

        if (IsPassiveSimulation)
        {
            var winsPerSecond = Engine.GetEstimatedWinsPerSecond(this);
            _fractionalPassiveWins += winsPerSecond * dt;
            ProcessPassiveSyntheticWins();
            return;
        }

        TimeSinceLastTurn += dt;
        while (TimeSinceLastTurn > TurnInterval)
        {
            TimeSinceLastTurn -= TurnInterval;
            Tick();
        }
    }

    private void OnBigBonusChangedForPassive(BonusType bonus, Big total)
    {
        if (!SupportsPassiveSimulation) return;
        RefreshPassiveSimulationMode();
    }

    private void SubscribePassiveDependencyEvents()
    {
        if (!SupportsPassiveSimulation) return;
        if (WinValueUpgrade) WinValueUpgrade.EffectChanged += OnPassiveDependencyChanged;
        if (PlaySpeedUpgrade) PlaySpeedUpgrade.EffectChanged += OnPassiveDependencyChanged;
        if (PlayIntelligenceUpgrade) PlayIntelligenceUpgrade.EffectChanged += OnPassiveDependencyChanged;
        if (GoldenTileUpgrade) GoldenTileUpgrade.EffectChanged += OnPassiveDependencyChanged;
    }

    private void UnsubscribePassiveDependencyEvents()
    {
        if (!SupportsPassiveSimulation) return;
        if (WinValueUpgrade) WinValueUpgrade.EffectChanged -= OnPassiveDependencyChanged;
        if (PlaySpeedUpgrade) PlaySpeedUpgrade.EffectChanged -= OnPassiveDependencyChanged;
        if (PlayIntelligenceUpgrade) PlayIntelligenceUpgrade.EffectChanged -= OnPassiveDependencyChanged;
        if (GoldenTileUpgrade) GoldenTileUpgrade.EffectChanged -= OnPassiveDependencyChanged;
    }

    private void OnPassiveDependencyChanged() => RefreshPassiveSimulationMode();

    public void RefreshPassiveSimulationMode()
    {
        if (!SupportsPassiveSimulation || Engine == null)
        {
            if (IsPassiveSimulation)
                SetPassiveSimulation(false);
            UpdatePassiveModeUi();
            return;
        }

        if (!Unlocked || !Active || TurnInterval <= 1e-6f)
        {
            if (IsPassiveSimulation)
                SetPassiveSimulation(false);
            UpdatePassiveModeUi();
            return;
        }

        var baseTurnsPerSecond = 1f / TurnInterval;
        var wantPassive = baseTurnsPerSecond >= Engine.PassiveModeMinBaseTurnsPerSecond;

        if (wantPassive == IsPassiveSimulation)
        {
            UpdatePassiveModeUi();
            return;
        }

        SetPassiveSimulation(wantPassive);
    }

    private void SetPassiveSimulation(bool passive)
    {
        if (IsPassiveSimulation == passive) return;

        IsPassiveSimulation = passive;
        _fractionalPassiveWins = 0;
        ResetBoard();
        if (!passive)
            TimeSinceLastTurn = Mathf.Min(TimeSinceLastTurn, TurnInterval);
        UpdatePassiveModeUi();
    }

    /// <summary>Six / Balloon: advance passive progress by one turn interval without Tick() simulation.</summary>
    public void FastForwardPassiveByTurnInterval()
    {
        if (!IsPassiveSimulation || Engine == null || !Unlocked) return;
        _fractionalPassiveWins += Engine.GetEstimatedWinsPerSecond(this) * TurnInterval;
        ProcessPassiveSyntheticWins();
    }

    private void ProcessPassiveSyntheticWins()
    {
        if (!IsPassiveSimulation || Engine == null) return;

        var n = 0;
        while (_fractionalPassiveWins >= 1d && n < MaxSyntheticPassiveWinsPerUpdate)
        {
            _fractionalPassiveWins -= 1d;
            n++;
            var reward = Engine.GetExpectedPassiveRewardPerWin(this);
            CurrencyManager.Instance.Earn(reward);
            Engine.OnBoardWon(this, reward);
            Win?.Invoke(reward);
        }
    }

    private void UpdatePassiveModeUi()
    {
        if (PassiveModePanel)
            PassiveModePanel.SetActive(IsPassiveSimulation && Unlocked);
        if (!PassiveModeGumRateText || Engine == null || !Unlocked)
            return;
        if (IsPassiveSimulation)
            PassiveModeGumRateText.text = $"<sprite name=Gum>{Engine.GetExpectedPassiveGumPerSecond(this).ToStringShorthand()}/s";
        else
            PassiveModeGumRateText.text = "";
    }

    public void SetTurnInterval(float turnInterval)
    {
        TurnInterval = turnInterval;
        UpdateAnimationsSpeed();
        RefreshPassiveSimulationMode();
    }

    protected float AnimationSpeed = 1f;
    private void UpdateAnimationsSpeed()
    {
        AnimationSpeed = GetAnimationSpeed();
        UpdateTileAnimationSpeed();
        UpdateWinLineAnimationSpeed();
    }

    private void UpdateTileAnimationSpeed()
    {
        if (Tiles is null) return;

        foreach (var tile in Tiles)
        {
            tile.SetAnimationSpeed(AnimationSpeed);
        }
    }

    private void UpdateWinLineAnimationSpeed()
    {
        foreach(var winLine in WinLines)
        {
            winLine.SetAnimationSpeed(AnimationSpeed);
        }
    }


    private float GetAnimationSpeed()
    {
        var animationSpeed = 0.5f / TurnInterval;
        if (IsPlayingQuicker)
        {
            animationSpeed *= BasePlayQuickerFactor;
        }
        return Mathf.Max(animationSpeed, 0.3f);
    }

	protected void OnValidate()
    {
        if (WinValueUpgrade)
        {
            WinValueUpgrade.Apply();
        }
        else
        {
            WinValue = BaseWinValue;
        }
        if (BaseTurnInterval > 0) // Safeguard against hanging
        {
            if (PlaySpeedUpgrade)
            {
                PlaySpeedUpgrade.Apply();
            }
            else
            {
                TurnInterval = BaseTurnInterval;
                TimeSinceLastTurn = Mathf.Min(TimeSinceLastTurn, TurnInterval);
            }
        }
    }

    public void Tick()
    {
        if (IsPassiveSimulation) return;

        switch (State)
        {
            case BoardState.InProgress:
                TakeTurn();
                TryApplyExtraPlays();
				break;
            case BoardState.Win:
            case BoardState.Loss:
            case BoardState.Draw:
                State = BoardState.Reset;
                break;
            case BoardState.Reset:
                ResetBoard();
                break;
            default:
                Debug.LogError($"Unhandled BoardState {State} in Board.Tick!");
                break;
        }
    }

    /// <summary>
    /// Each full 100% of double-play chance grants an extra player turn after the current play.
    /// The remainder (≤ 100%) is rolled once for one additional extra turn.
    /// </summary>
    private void TryApplyExtraPlays()
    {
        if (!AllowDoublePlay) return;

        var remainingChance = BonusManager.Instance.TotalBonusFloat(BonusType.DoublePlayChance);
        while (remainingChance > 1f && CanTakeExtraPlay())
        {
            remainingChance -= 1f;
            TakeExtraPlay();
        }

        if (CanTakeExtraPlay() && Rng.PassFail(remainingChance))
            TakeExtraPlay();
    }

    private bool CanTakeExtraPlay() =>
        State == BoardState.InProgress && CurrentTurnOwner == Owner.Opponent && FreeTiles.Any();

    private void TakeExtraPlay()
    {
        CurrentTurnOwner = Owner.Player;
        TakeTurn();
    }

    public void TryUnlock()
    {
        if(CurrencyManager.Instance.TryPay(UnlockCost))
        {
            SetUnlocked(true);
        }
    }

    public void SetUnlocked(bool unlocked)
    {
        LockedPanel.SetActive(!unlocked);
        if (Unlocked == unlocked) return;

		Unlocked = unlocked;
        if(!Unlocked)
        {
            UpdateUnlockButtonInteractable();
            UpdateUnlockCostText();
		}
        else
        {
            ResetBoard();
            OnUnlocked();
            Engine.OnBoardUnlocked(this);
        }
        RefreshPassiveSimulationMode();
    }

    public void UpdateUnlockButtonInteractable()
    {
        UnlockButton.interactable = CurrencyManager.Instance.Currency >= UnlockCost;
    }

    public void UpdateUnlockCostText()
    {
        UnlockCostText.text = $"<sprite name=Gum> {UnlockCost.ToStringShorthand()}";
    }

    protected virtual void OnUnlocked() => Unlock?.Invoke();

    private void OnAvailableCurrencyChanged(Big available, Big change)
    {
        if(!Unlocked)
        {
            UpdateUnlockButtonInteractable();
		}
    }

    #region Play
    /// <summary>Halves displayed/used intelligence while OpponentGoldenPlays is active.</summary>
    public float IntelligenceMultiplier => OpponentGoldenPlaysSpecialization.IsActive ? 0.5f : 1f;

    public float EffectiveSmartPlayChance => SmartPlayChance * IntelligenceMultiplier;
    public float EffectiveDumbPlayChance => DumbPlayChance * IntelligenceMultiplier;

    /// <summary>
    /// Per-golden reward base: (2 + sponsorship/etc.) × spec multiplier when active.
    /// </summary>
    public static float GetGoldenPlayRewardMultiplierPerTile()
    {
        var mult = 2f + BonusManager.Instance.TotalBonusFloat(BonusType.GoldenPlayEffectiveness);
        if (OpponentGoldenPlaysSpecialization.IsActive &&
            WinningManager.Instance.CurrentSpecialization is OpponentGoldenPlaysSpecialization spec)
        {
            mult *= spec.CurrentEffectivenessMultiplier;
        }
        return mult;
    }

    protected virtual float TotalDumbPlayChance => EffectiveDumbPlayChance +
                HiddenSmarts +
                (Time.time < PlayQuickerEnd ? HiddenSmartsPlayFaster : 0);


    protected readonly List<Tile> FreeTiles = new List<Tile>();
	protected readonly List<Tile> BestTilesForPlayer = new List<Tile>();
	protected readonly List<Tile> WorstTilesForOpponent = new List<Tile>();

    public void TakeTurn()
    {
        if(CurrentTurnOwner == Owner.Opponent && OpponentNoGo)
        {
            CurrentTurnOwner = Owner.Player;
        }

        if(CurrentTurnOwner == Owner.Player)
        {
            var goldenPlay = RollGoldenPlay(forOpponent: false);

            var totalSmartPlayChance =
                EffectiveSmartPlayChance +
                HiddenSmarts +
                (Time.time < PlayQuickerEnd ? HiddenSmartsPlayFaster : 0);
            if(Rng.PassFail(totalSmartPlayChance))
            {
                MakeGoodPlay(goldenPlay);
            }
            else
            {
                MakeRandomPlay(goldenPlay);
            }
        }
        else
        {
            var goldenPlay = RollGoldenPlay(forOpponent: true);
            var totalDumbPlayChance = TotalDumbPlayChance;
            if(totalDumbPlayChance < 0)
            {
                if (Rng.PassFail(TotalDumbPlayChance*-1))
                {
                    MakeGoodPlay(goldenPlay);
                }
                else
                {
                    MakeRandomPlay(goldenPlay);
                }
            }
            else
            {
                if (Rng.PassFail(TotalDumbPlayChance))
                {
                    MakeBadPlay(goldenPlay);
                }
                else
                {
                    MakeRandomPlay(goldenPlay);
                }
            }
        }
    }

    protected bool RollGoldenPlay(bool forOpponent)
    {
        if (forOpponent && !OpponentGoldenPlaysSpecialization.IsActive)
        {
            return false;
        }

        // Golden Hour guarantees player goldens only — opponents still use board chance.
        var goldenPlay = (!forOpponent && PremiumInventoryManager.Instance.GoldenHourActive) ||
            Rng.PassFail(GoldenTileChance);
        if (!forOpponent && BonusManager.Instance.TotalBonusBool(BonusType.LuckyGoldenPlayChance))
        {
            goldenPlay = goldenPlay || Rng.PassFail(GoldenTileChance);
        }
        return goldenPlay;
    }

    public virtual Tile MakeRandomPlay(bool goldenPlay)
    {
        var randomTile = Rng.ItemFrom(FreeTiles);
        MakePlay(randomTile, goldenPlay);
        return randomTile;
    }

    public Tile MakeGoodPlay(bool goldenPlay)
    {
        UpdateBestTilesForPlayer();
        var randomTile = Rng.ItemFrom(BestTilesForPlayer);
        MakePlay(randomTile, goldenPlay);
        return randomTile;
    }

    public Tile MakeBadPlay(bool goldenPlay)
    {
        UpdateWorstTilesForOpponent();
        var randomTile = Rng.ItemFrom(WorstTilesForOpponent);
        MakePlay(randomTile, goldenPlay);
        return randomTile;
    }

    public virtual void MakePlay(Tile tile, bool goldenPlay)
    {
        TurnsTaken++;
        if(goldenPlay)
        {
            GoldenTiles++;
        }
        FreeTiles.Remove(tile);
        tile.AssignOwner(CurrentTurnOwner, goldenPlay);
        CurrentTurnOwner = CurrentTurnOwner.Other();
        if(DetectGameWins(tile))
        {
            if(CurrentTurnOwner == Owner.Opponent)
			{
				State = BoardState.Win;
				OnWon(tile);
			}
            else
            {
                State = BoardState.Loss;
                OnLoss(tile);
            }
        }
        else if(IsGameADraw())
        {
            State = BoardState.Draw;
            OnDraw();
        }
        UpdateTileUtilitiesAfterMove(tile);
    }

    public void FreePlay()
    {
        if(State != BoardState.InProgress)
        {
            CachedFreePlays++;
            return;
        }
        var turnOwner = CurrentTurnOwner;
        CurrentTurnOwner = Owner.Player;
        TakeTurn();
        CurrentTurnOwner = turnOwner;
    }

    protected float LastEarningsFloaterTime = 0;
    protected int WonCol = -1;
    protected int WonRow = -1;
    protected bool WonPrimaryDiag = false;
    protected bool WonSecondaryDiag = false;
    protected virtual void OnWon(Tile winningPlay)
	{
		var reward = CalculateWinValue();
		CurrencyManager.Instance.Earn(reward);

        ShowWinLines(true, GoldenTiles > 0);
		Engine.OnBoardWon(this, reward);
		RaiseWin(reward);

        // Don't produce floater if not visible, or if winning too frequently
        if (gameObject.activeInHierarchy && Time.time - LastEarningsFloaterTime > MinTimeBetweenEarningsFloaters)
		{
            LastEarningsFloaterTime = Time.time;
			var floater = Instantiate(EarningsFloaterPrefab, transform);
			if (GoldenTiles > 0) floater.GetComponentInChildren<Image>().color = EarningsFloaterGoldenColor;
			floater.GetComponentInChildren<TMP_Text>().text = $"<sprite name=Gum>+{reward.ToStringShorthand()}";
			((RectTransform)floater.transform).anchoredPosition = new Vector2(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100));
		}
	}

    protected void RaiseWin(Big reward) => Win?.Invoke(reward);

    protected virtual void OnDraw()
    {
        Draw?.Invoke();
    }

    protected virtual void OnLoss(Tile losingPlay)
    {
        ShowWinLines(false, false); 
        Loss?.Invoke();
    }

    protected List<BoardWinLine> WinLines = new List<BoardWinLine>();
    protected void ShowWinLines(bool playerWin, bool golden)
    {
        Action<BoardWinLine> setColor = golden ?
            (BoardWinLine line) => line.SetColorToGold() :
            playerWin ?
            (BoardWinLine line) => line.SetColorToPlayer() :
            (BoardWinLine line) => line.SetColorToOpponent();
        BoardWinLine makeLine()
        {
            var line = Instantiate(WinLinePrefab, transform).GetComponent<BoardWinLine>();
            setColor(line);
            line.SetAnimationSpeed(AnimationSpeed);
            WinLines.Add(line);
            return line;
        }
        if (WonRow > -1)
        {
            var rowLine = makeLine();
            rowLine.ShowRow(WonRow);
        }
        if(WonCol > -1)
        {
            var colLine = makeLine();
            colLine.ShowCol(WonCol);
        }
        if(WonPrimaryDiag)
        {
            var primaryDiagLine = makeLine();
            primaryDiagLine.ShowPrimaryDiag();
        }
        if (WonSecondaryDiag)
        {
            var secondaryDiagLine = makeLine();
            secondaryDiagLine.ShowSecondaryDiag();
        }
    }
	#endregion

	#region Responding to User Input
    public void StartPlayingQuicker()
    {
        if(BonusManager.Instance.TotalBonusBool(BonusType.PlayFasterAllAtOnce))
        {
            Engine.StartPlayingQuickerOnAllUnlockedBoards();
        }
        else
        {
            RefreshPlayingQuicker();
        }
    }

    public void RefreshPlayingQuicker()
    {
        if (!Unlocked) return;

        var now = Time.time;
        PlayQuickerStart = now;

        var duration = BasePlayQuickerDuration * BonusManager.Instance.TotalBonusFloat(BonusType.PlayFasterDurationMultiplier);
        var projectedEndTime = now + duration;
        if (projectedEndTime > PlayQuickerEnd)
        {
            PlayQuickerEnd = projectedEndTime;
        }
    }
    #endregion

    public Big TrickleDownGumSkillMultiplier { get; set; } = Big.ONE;
    public void SetHigherBoardsUnlocked(int numUnlocked)
    {
        HigherBoardsUnlocked = numUnlocked;
        RecalculateTrickleDownGumSkillMultiplier();
    }

    private void RecalculateTrickleDownGumSkillMultiplier()
    {
        TrickleDownGumSkillMultiplier = Big.FromExponential(1, BonusManager.Instance.TotalBonusFloat(BonusType.SkillTrickleDownGum), HigherBoardsUnlocked);
        if(WinValueUpgrade)
        {
            WinValueUpgrade.EffectChangedExternally();
        }
    }

    protected virtual Big CalculateWinValue() =>
        WinValue *
        Math.Pow(GetGoldenPlayRewardMultiplierPerTile(), GoldenTiles) *
        TrickleDownGumSkillMultiplier *
        BonusManager.Instance.TotalBonusBig(BonusType.BoardRewardsMultiplier) *
        BonusManager.Instance.TotalBonusBig(BonusType.PrimaryCurrencyEarnings);

    #region Win Detection
    public bool IsGameADraw() => !FreeTiles.Any();

    private bool DetectGameWins(Tile justPlayedTile)
    {
        var result = false;
        if(IsRowWon(justPlayedTile.Row))
        {
            WonRow = justPlayedTile.Row;
            result = true;
        }
        if(IsColWon(justPlayedTile.Col))
        {
            WonCol = justPlayedTile.Col;
            result = true;
        }
        if(justPlayedTile.IsOnPrimaryDiagonal && IsPrimaryDiagonalWon())
        {
            WonPrimaryDiag = true;
            result = true;
        }
        if(justPlayedTile.IsOnSecondaryDiagonal && IsSecondaryDiagonalWon())
        {
            WonSecondaryDiag = true;
            result = true;
        }
        return result;
    }

    public bool IsRowWon(int row)
    {
        var first = Tiles[row, 0].Owner;
        if (first == Owner.None) return false;
        for(int col = 1; col < Size; col++)
        {
            if (Tiles[row, col].Owner != first) return false;
        }
        return true;
    }
    public bool IsColWon(int col)
    {
        var first = Tiles[0, col].Owner;
        if (first == Owner.None) return false;
        for(int row = 1; row < Size; row++)
        {
            if (Tiles[row, col].Owner != first) return false;
        }
        return true;
    }
    public bool IsPrimaryDiagonalWon()
    {
        var first = Tiles[0, 0].Owner;
        if (first == Owner.None) return false;
        for(int rowCol = 1; rowCol < Size; rowCol++)
        {
            if (Tiles[rowCol, rowCol].Owner != first) return false;
        }
        return true;
    }
    public bool IsSecondaryDiagonalWon()
    {
        var first = Tiles[0, Size - 1].Owner;
        if (first == Owner.None) return false;
        for (int row = 1; row < Size; row++)
        {
            if (Tiles[row, Size - row - 1].Owner != first) return false;
        }
        return true;
    }
    #endregion

    #region Tile Utility Calculation
    // Winning the game should take slightly higher priority over blocking the opponent
    private const float HELP_SELF_FACTOR = 1f;
    private const float BLOCK_OTHER_FACTOR = 0.99f;

    private void UpdateTileUtilitiesAfterMove(Tile changedTile)
    {
        var row = changedTile.Row;
        var col = changedTile.Col;

        ResetUtilitiesAroundTile(changedTile);

        MultiplyUtilitiesInRow(row, Owner.Player, GetTotalRowUtilityMultiplier(row, Owner.Player));
        MultiplyUtilitiesInRow(row, Owner.Opponent, GetTotalRowUtilityMultiplier(row, Owner.Opponent));

        MultiplyUtilitiesInCol(col, Owner.Player, GetTotalColUtilityMultiplier(col, Owner.Player));
        MultiplyUtilitiesInCol(col, Owner.Opponent, GetTotalColUtilityMultiplier(col, Owner.Opponent));

        if(changedTile.IsOnPrimaryDiagonal)
        {
            MultiplyUtilitiesInPrimaryDiagonal(Owner.Player, GetTotalPrimaryDiagonalUtilityMultiplier(Owner.Player));
            MultiplyUtilitiesInPrimaryDiagonal(Owner.Opponent, GetTotalPrimaryDiagonalUtilityMultiplier(Owner.Opponent));
        }
        if(changedTile.IsOnSecondaryDiagonal)
        {
            MultiplyUtilitiesInSecondaryDiagonal(Owner.Player, GetTotalSecondaryDiagonalUtilityMultiplier(Owner.Player));
            MultiplyUtilitiesInSecondaryDiagonal(Owner.Opponent, GetTotalSecondaryDiagonalUtilityMultiplier(Owner.Opponent));
        }
    }

    private float GetTotalRowUtilityMultiplier(int row, Owner owner) =>
        GetRowUtilityMultiplier(row, owner, HELP_SELF_FACTOR) * GetRowUtilityMultiplier(row, owner.Other(), BLOCK_OTHER_FACTOR);
    private float GetTotalColUtilityMultiplier(int col, Owner owner) =>
        GetColUtilityMultiplier(col, owner, HELP_SELF_FACTOR) * GetColUtilityMultiplier(col, owner.Other(), BLOCK_OTHER_FACTOR);
    private float GetTotalPrimaryDiagonalUtilityMultiplier(Owner owner) =>
        GetPrimaryDiagonalUtilityMultiplier(owner, HELP_SELF_FACTOR) * GetPrimaryDiagonalUtilityMultiplier(owner.Other(), BLOCK_OTHER_FACTOR);
    private float GetTotalSecondaryDiagonalUtilityMultiplier(Owner owner) =>
        GetSecondaryDiagonalUtilityMultiplier(owner, HELP_SELF_FACTOR) * GetSecondaryDiagonalUtilityMultiplier(owner.Other(), BLOCK_OTHER_FACTOR);


    private float GetRowUtilityMultiplier(int row, Owner owner, float multiplierStep)
    {
        var multiplier = 0f;
        var otherOwner = owner.Other();
        for(int col = 0; col < Size; col++)
        {
            var tileOwner = Tiles[row, col].Owner;
            if (tileOwner == owner)
            {
                multiplier += multiplierStep;
                multiplier *= 2;
            }
            else if(tileOwner == otherOwner)
            {
                return 1;
            }
        }
        return multiplier + 1;
    }
    private float GetColUtilityMultiplier(int col, Owner owner, float multiplierStep)
    {
        var multiplier = 0f;
        var otherOwner = owner.Other();
        for (int row = 0; row < Size; row++)
        {
            var tileOwner = Tiles[row, col].Owner;
            if (tileOwner == owner)
            {
                multiplier += multiplierStep;
                multiplier *= 2;
            }
            else if (tileOwner == otherOwner)
            {
                return 1;
            }
        }
        return multiplier + 1;
    }
    private float GetPrimaryDiagonalUtilityMultiplier(Owner owner, float multiplierStep)
    {
        var multiplier = 0f;
        var otherOwner = owner.Other();
        for (int rowCol = 0; rowCol < Size; rowCol++)
        {
            var tileOwner = Tiles[rowCol, rowCol].Owner;
            if (tileOwner == owner)
            {
                multiplier += multiplierStep;
                multiplier *= 2;
            }
            else if (tileOwner == otherOwner)
            {
                return 1;
            }
        }
        return multiplier + 1;
    }
    private float GetSecondaryDiagonalUtilityMultiplier(Owner owner, float multiplierStep)
    {
        var multiplier = 0f;
        var otherOwner = owner.Other();
        for (int row = 0; row < Size; row++)
        {
            var tileOwner = Tiles[row, Size - row - 1].Owner;
            if (tileOwner == owner)
            {
                multiplier += multiplierStep;
                multiplier *= 2;
            }
            else if (tileOwner == otherOwner)
            {
                return 1;
            }
        }
        return multiplier + 1;
    }

    private void ResetUtilitiesAroundTile(Tile tile)
    {
        var row = tile.Row;
        var col = tile.Col;

        for (int c = 0; c < Size; c++)
        {
            Tiles[row, c].SetInitialUtilities();
        }
        for (int r = 0; r < Size; r++)
        {
            Tiles[r, col].SetInitialUtilities();
        }

        if(tile.IsOnPrimaryDiagonal)
        {
            for (int rc = 0; rc < Size; rc++)
            {
                Tiles[rc, rc].SetInitialUtilities();
            }
        }
        if(tile.IsOnSecondaryDiagonal)
        {
            for (int rc = 0; rc < Size; rc++)
            {
                Tiles[rc, Size - rc - 1].SetInitialUtilities();
            }
        }
    }

    private static readonly Dictionary<Owner, Action<Tile, float>> UtilityMultipliers = new Dictionary<Owner, Action<Tile, float>>()
    {
        { Owner.Player, (t, mult) => t.UtilityForPlayer *= mult },
        { Owner.Opponent, (t, mult) => t.UtilityForOpponent *= mult },
    };
    private void MultiplyUtilitiesInRow(int row, Owner owner, float multiplier)
    {
        var multiplierFunc = UtilityMultipliers[owner];
        for(int col = 0; col < Size; col++)
        {
            multiplierFunc(Tiles[row, col], multiplier);
        }
    }
    private void MultiplyUtilitiesInCol(int col, Owner owner, float multiplier)
    {
        var multiplierFunc = UtilityMultipliers[owner];
        for (int row = 0; row < Size; row++)
        {
            multiplierFunc(Tiles[row, col], multiplier);
        }
    }
    private void MultiplyUtilitiesInPrimaryDiagonal(Owner owner, float multiplier)
    {
        var multiplierFunc = UtilityMultipliers[owner];
        for (int rowCol = 0; rowCol < Size; rowCol++)
        {
            multiplierFunc(Tiles[rowCol, rowCol], multiplier);
        }
    }
    private void MultiplyUtilitiesInSecondaryDiagonal(Owner owner, float multiplier)
    {
        var multiplierFunc = UtilityMultipliers[owner];
        for (int row = 0; row < Size; row++)
        {
            multiplierFunc(Tiles[row, Size - row - 1], multiplier);
        }
    }

    private void UpdateBestTilesForPlayer()
    {
        var maxUtility = 0f;
        foreach(var tile in FreeTiles)
        {
            maxUtility = Mathf.Max(maxUtility, tile.UtilityForPlayer);
        }
        BestTilesForPlayer.Clear();
        foreach(var tile in FreeTiles)
        {
            if(tile.UtilityForPlayer == maxUtility)
            {
                BestTilesForPlayer.Add(tile);
            }
        }
    }

    private void UpdateWorstTilesForOpponent()
    {
        var minUtility = 99999999999999999f;
        foreach (var tile in FreeTiles)
        {
            minUtility = Mathf.Min(minUtility, tile.UtilityForOpponent);
        }
        WorstTilesForOpponent.Clear();
        foreach (var tile in FreeTiles)
        {
            if (tile.UtilityForOpponent == minUtility)
            {
                WorstTilesForOpponent.Add(tile);
            }
        }
    }
    #endregion
}

public enum BoardState
{
    InProgress,
    Win,
    Loss,
    Draw,
    Reset
}