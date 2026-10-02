using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Purchasing;
using UnityEngine.SceneManagement;

public class PurchaseUIController : MonoBehaviour
{
    public Dialog ErrorDialog;

    // Start is called before the first frame update
    void Start()
    {
        if (Puzzle.Current)
        {
            Puzzle.Current.LockInput();
            Puzzle.Current.CameraController.LockInput();
        }

        InputManager.Instance.AddBackAction(this, HideStore);
        PurchaseManager.Instance.PurchaseFailed += OnPurchaseFailed;
        HideAdFreeSectionIfAdsDisabled();
    }

    private void OnDestroy()
    {
        InputManager.Instance.RemoveBackAction(this);
        PurchaseManager.Instance.PurchaseFailed -= OnPurchaseFailed;
    }

    public void HideStore()
    {
        if (Puzzle.Current)
        {
            Puzzle.Current.FreeInput();
            Puzzle.Current.CameraController.FreeInput();
        }
        Destroy(gameObject);
    }

    private void OnPurchaseFailed(IEnumerable<Product> products, PurchaseFailureReason reason)
    {
        ErrorDialog.Show();
    }

    private static readonly string AdFreeSectionName = "Ad Free Section";
    private void HideAdFreeSectionIfAdsDisabled()
    {
        if (AdManager.Instance != null && AdManager.Instance.AdsEnabled)
        {
            return;
        }
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == AdFreeSectionName)
            {
                child.gameObject.SetActive(false);
                return;
            }
        }
    }
}
