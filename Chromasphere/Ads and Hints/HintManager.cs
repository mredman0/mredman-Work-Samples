using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public class HintManager : MonoBehaviour
{
    public static HintManager Instance;

    public event Action Initialized;

    public event Action HintGained;
    public event Action HintUsed;

    private const string HINTS_KEY = "Hints";
    private const int DEFAULT_HINTS = 3;

    private bool Offline;
    private int Hints;
    private int OfflineHintsUsed;

    [Header("Debug")]
    public int DebugGrantHintsOnStartup = 0;

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

        if(PlayerAuthManager.Instance.IsAuthenticated)
        {
            StartupSyncOnline();
        }
        else if(PlayerAuthManager.Instance.HasAuthenticationFailed)
        {
            StartupSyncOffline();
        }
        PlayerAuthManager.Instance.AuthenticationComplete += StartupSyncOnline;
        PlayerAuthManager.Instance.AuthenticationFailed += StartupSyncOffline;

        PurchaseManager.Instance.HintsPurchased += GainHintsSync;
    }

    private void OnDestroy()
    {
        PlayerAuthManager.Instance.AuthenticationComplete -= StartupSyncOnline;
        PlayerAuthManager.Instance.AuthenticationFailed -= StartupSyncOffline;

        PurchaseManager.Instance.HintsPurchased -= GainHintsSync;
    }

    private void StartupSyncOnline()
    {
        PlayerAuthManager.Instance.AuthenticationFailed -= StartupSyncOffline;
        _ = Startup(false);
    }
    private void StartupSyncOffline()
    {
        PlayerAuthManager.Instance.AuthenticationComplete -= StartupSyncOnline;
        _ = Startup(true);
    }
    private async Task Startup(bool offline)
    {
        Offline = offline;

        if(Offline)
        {
            Debug.Log("Initializing hint manager in offline mode");
            var loadedFromLocal = LoadHintsFromLocal();
            if(!loadedFromLocal)
            {
                Hints = DEFAULT_HINTS;
                SaveHintsToLocal();
                PlayerPrefs.SetInt("HCP", 1);
                Debug.Log("No local hint data, granting default hints");
            }
            OfflineHintsUsed = PlayerPrefs.GetInt("HOU", 0);
            if (DebugGrantHintsOnStartup > 0)
            {
                Hints += DebugGrantHintsOnStartup;
                SaveHintsToLocal();
            }
        }
        else
        {
            Debug.Log("Initializing hint manager in online mode");
            if (PlayerPrefs.GetInt("HCP", 0) > 0)
            {
                await LoadHintsFromCloud();
                SaveHintsToLocal();
                PlayerPrefs.DeleteKey("HCP");
            }
            if(PlayerPrefs.GetInt("HLOC", -1) < 0)
            {
                var loadedFromCloud = await LoadHintsFromCloud();
                if(!loadedFromCloud)
                {
                    Hints = DEFAULT_HINTS;
                }
                SaveHintsToLocal();
            }
            else
            {
                LoadHintsFromLocal();
            }
            OfflineHintsUsed = PlayerPrefs.GetInt("HOU", 0);
            if (OfflineHintsUsed != 0)
            {
                Hints = Mathf.Max(0, Hints - OfflineHintsUsed);
                SaveHintsToLocal();
                var successfullySavedToCloud = await SaveHintsToCloud();
                if (successfullySavedToCloud)
                {
                    PlayerPrefs.DeleteKey("HOU");
                    OfflineHintsUsed = 0;
                }
                else
                {
                    Hints += OfflineHintsUsed;
                    SaveHintsToLocal();
                }
            }

            if (DebugGrantHintsOnStartup > 0)
            {
                Hints += DebugGrantHintsOnStartup;
                SaveHintsToLocal();
                SaveHintsToCloud();
            }
        }
        Initialized?.Invoke();
    }

    public bool UseHint()
    {
#if DEMO
        return true;
#else
        if (Offline)
        {
            return UseHintOffline();
        }


        if (Hints < 1)
        {
            return false;
        }
        _ = UseHintOnline();
        return true;
#endif
    }


    private async Task<bool> UseHintOnline()
    {
        if (Hints < 1)
        {
            return false;
        }

        Hints--;
        SaveHintsToLocal();
        var successfullySavedToCloud = await SaveHintsToCloud();
        if(!successfullySavedToCloud)
        {
            Hints++;
            SaveHintsToLocal();
            return UseHintOffline();
        }
        Debug.Log("Hint used online");
        HintUsed?.Invoke();
        return true;
    }
    private bool UseHintOffline()
    {
        if (Hints <= OfflineHintsUsed)
        {
            return false;
        }

        OfflineHintsUsed++;
        PlayerPrefs.SetInt("HOU", OfflineHintsUsed);
        Debug.Log("Hint used offline");
        HintUsed?.Invoke();
        return true;
    }

    private void GainHintsSync(int amount)
    {
        _ = GainHints(amount);
    }
    public async Task GainHints(int amount)
    {
        Debug.Log($"Attempting to increase hints by {amount}");
        if(amount < 1)
        {
            Debug.LogWarning("Do not use GainHints to reduce number of hints.");
            return;
        }
#if !DEMO
        if(Offline)
        {
            GainHintsOffline(amount);
        }
        else
        {
            await GainHintsOnline(amount);
        }
#endif
        HintGained?.Invoke();
    }

    private async Task GainHintsOnline(int amount)
    {
        Hints += amount;
        SaveHintsToLocal();
        var successfullySavedToCloud = await SaveHintsToCloud();
        if(!successfullySavedToCloud)
        {
            Hints -= amount;
            SaveHintsToLocal();
            GainHintsOffline(amount);
            return;
        }
        Debug.Log("Hint(s) gained online");
    }
    private void GainHintsOffline(int amount)
    {
        OfflineHintsUsed -= amount;
        PlayerPrefs.SetInt("HOU", OfflineHintsUsed);
        Debug.Log("Hint(s) gained offline");
    }

    public int GetHints() => Hints - OfflineHintsUsed;

    private bool LoadHintsFromLocal()
    {
        var clientSideHints = PlayerPrefs.GetInt("HLOC", -1);
        if (clientSideHints < 0)
        {
            Debug.Log("Player does not have local hints");
            return false;
        }
        Hints = clientSideHints;
        return true;
    }

    private async Task<bool> LoadHintsFromCloud()
    {
        try
        {
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string>() { HINTS_KEY });
            if (!data.ContainsKey(HINTS_KEY))
            {
                Debug.Log($"Player does not have hints stored in cloud");
                return false;
            }
            Hints = data[HINTS_KEY].Value.GetAs<int>();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

    private void SaveHintsToLocal()
    {
        PlayerPrefs.SetInt("HLOC", Hints);
    }
    private async Task<bool> SaveHintsToCloud()
    {
        try
        {
            var data = new Dictionary<string, object>() { { HINTS_KEY, Hints } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }
}
