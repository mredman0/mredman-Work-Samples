using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class ColorManager : MonoBehaviour
{
    public static ColorManager Instance { get; set; }

    public event Action Initialized;

    public event Action ColorSchemeChanged;

    [Header("Required References")]
    public List<string> BaseColorSchemes;

    [Header("State")]
    public bool IsInitialized = false;
    public ColorScheme Current;
    public string CurrentId;
    public Dictionary<string, ColorScheme> AvailableColorSchemes = new Dictionary<string, ColorScheme>();

    private const string AVAILABLE_COLOR_SCHEMES_KEY = "AvailableColorSchemes";
    private const string CURRENT_COLOR_SCHEME_KEY = "CurrentColorScheme";
    // Start is called before the first frame update
    void Start()
    {
        if(Instance)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        var availableColorSchemesValue = SettingsManager.Instance.GetString(AVAILABLE_COLOR_SCHEMES_KEY);
        IEnumerable<string> colorSchemesToLoad;
        if(string.IsNullOrWhiteSpace(availableColorSchemesValue))
        {
            colorSchemesToLoad = BaseColorSchemes;
        }
        else
        {
            colorSchemesToLoad = availableColorSchemesValue.Split(',');
        }
        LoadColorSchemes(colorSchemesToLoad, OnFirstLoadComplete);
    }

    private void LoadColorSchemes(IEnumerable<string> schemeIds, Action onLoadComplete)
    {
        var handle = Addressables.LoadAssetsAsync<ColorScheme>(schemeIds.Select(id => $"ColorSchemes/{id}"), (scheme) =>
        {
            AvailableColorSchemes[scheme.Id] = scheme;
        }, Addressables.MergeMode.Union);
        handle.Completed += (_) => onLoadComplete?.Invoke();
    }

    private void OnFirstLoadComplete()
    {
        SettingsManager.Instance.SetString(AVAILABLE_COLOR_SCHEMES_KEY, string.Join(',', AvailableColorSchemes.Keys));
        var saved = SettingsManager.Instance.GetString(CURRENT_COLOR_SCHEME_KEY);
        if (!AvailableColorSchemes.ContainsKey(saved))
        {
            saved = SettingsManager.Instance.GetStringDefault(CURRENT_COLOR_SCHEME_KEY);
        }
        SelectColorScheme(saved);
        IsInitialized = true;
        Initialized?.Invoke();
    }

    public void LoadColorScheme(string colorSchemeId)
    {
        if(AvailableColorSchemes.ContainsKey(colorSchemeId))
        {
            return; // Already loaded
        }

        try
        {
            var scheme = Addressables.LoadAssetAsync<ColorScheme>($"ColorSchemes/{colorSchemeId}").WaitForCompletion();
            AvailableColorSchemes[colorSchemeId] = scheme;
        }
        catch
        {
            Debug.LogWarning($"Unable to load color scheme {colorSchemeId}");
        }
    }

    public bool SelectColorScheme(string colorSchemeId)
    {
        if (colorSchemeId is null || !AvailableColorSchemes.ContainsKey(colorSchemeId))
        {
            Debug.LogError($"Could not select color scheme {colorSchemeId}");
            return false;
        }
        Current = AvailableColorSchemes[colorSchemeId];
        CurrentId = colorSchemeId;
        SettingsManager.Instance.SetString(CURRENT_COLOR_SCHEME_KEY, CurrentId);
        ColorSchemeChanged?.Invoke();
        return true;
    }

    public Color GetColor(int colorIndex) => Current.Colors[colorIndex];
}
