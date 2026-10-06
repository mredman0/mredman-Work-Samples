using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UI.GridLayoutGroup;

public class Tile : MonoBehaviour
{
    [Header("Required References")]
    public Image Image;
    public Animator Animator;
    public Sprite FinalXTexture;
    public Sprite FinalOTexture;

    [Header("Settings")]
    public int BoardSize;
    public int Row;
    public int Col;
    public Color PlayerColor;
    public Color OpponentColor;
    public Color GoldenColor;

    [Header("State")]
    public Owner Owner = Owner.None;
    public bool Golden = false;

    public bool IsOnPrimaryDiagonal = false;
    public bool IsOnSecondaryDiagonal = false;

    public float UtilityForPlayer;
    public float UtilityForOpponent;

    private const string DRAW_X_STATE = "X";
    private const string DRAW_O_STATE = "O";
    public string DrawPlayerState;
    public string DrawOpponentState;
    public Sprite FinalPlayerTexture;
    public Sprite FinalOpponentTexture;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        UpdateTextureReferences();
        SettingsManager.Instance.BoolSettingChanged += OnBoolSettingChanged;
    }

    private void OnEnable()
    {
    }

    private void OnDestroy()
    {
        SettingsManager.Instance.BoolSettingChanged -= OnBoolSettingChanged;
    }

    public void Init(int boardSize, int row, int col)
    {
        BoardSize = boardSize;
        Row = row;
        Col = col;
        IsOnPrimaryDiagonal = Row == Col;
        IsOnSecondaryDiagonal = Row == BoardSize - Col - 1;
        SetInitialUtilities();

        UpdateTextureReferences();
    }

    private void OnBoolSettingChanged(string key, bool value)
    {
        if(key == SettingsController.PLAYER_IS_X_KEY)
        {
            UpdateTextureReferences();
        }
    }

    private void UpdateTextureReferences()
    {
        if(SettingsManager.Instance.GetBool(SettingsController.PLAYER_IS_X_KEY))
        {
            DrawPlayerState = DRAW_X_STATE;
            DrawOpponentState = DRAW_O_STATE;
            FinalPlayerTexture = FinalXTexture;
            FinalOpponentTexture = FinalOTexture;
        }
        else
        {
            DrawPlayerState = DRAW_O_STATE;
            DrawOpponentState = DRAW_X_STATE;
            FinalPlayerTexture = FinalOTexture;
            FinalOpponentTexture = FinalXTexture;
        }

        // Force update of currently displayed textures as well
        if(Owner != Owner.None)
        {
            AssignOwner(Owner, Golden);
        }
    }

    public void SetAnimationSpeed(float speed)
    {
        Animator.speed = speed;
    }

    public void ResetTile()
    {
        Owner = Owner.None;
        Image.enabled = false;
        Golden = false;
        Animator.enabled = true;
        Animator.Rebind();
        SetInitialUtilities();
    }

    public void AssignOwner(Owner owner, bool goldenPlay)
    {
        if(owner == Owner.None)
        {
            Debug.LogError($"Should not be assigning tile owner to None (Tile[{Row},{Col}])");
        }
        Owner = owner;
        Golden = goldenPlay;

        Image.enabled = true;
        if(isActiveAndEnabled)
        {
            Animator.Play(owner == Owner.Player ? DrawPlayerState : DrawOpponentState);
        }
        else
        {
            SetXOrOTexture();
        }
        Image.color = goldenPlay ? GoldenColor : (owner == Owner.Player ? PlayerColor : OpponentColor);
    }

    public void SetXOrOTexture()
    {
        if (Owner != Owner.None)
        {
            Animator.enabled = false;
            Image.sprite = Owner == Owner.Player ? FinalPlayerTexture : FinalOpponentTexture;
        }
    }

    private const float DIAGONAL_INITIAL_UTILITY_MULTIPLIER = 2f;
    public void SetInitialUtilities()
    {
        var utility = 1f;

        if (IsOnPrimaryDiagonal) utility *= DIAGONAL_INITIAL_UTILITY_MULTIPLIER;
        if (IsOnSecondaryDiagonal) utility *= DIAGONAL_INITIAL_UTILITY_MULTIPLIER;

        UtilityForPlayer = utility;
        UtilityForOpponent = utility;
    }
}

public enum Owner
{
    None,
    Player,
    Opponent
}
