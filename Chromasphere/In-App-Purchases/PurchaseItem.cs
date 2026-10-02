using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UI;

public class PurchaseItem : MonoBehaviour
{
    [Header("Required References")]
    public Button PurchaseButton;
    public GameObject OwnedPanel;

    public TMP_Text PriceText;

    public string ProductId;

    [Header("Debug")]
    public string FakeStorePrice = "$";

    void Start()
    {
        if (ProductId == "ad_free" && (AdManager.Instance == null || !AdManager.Instance.AdsEnabled))
        {
            HideAdFreeSection();
            return;
        }
        if(string.IsNullOrWhiteSpace(ProductId) || !PriceText || !PurchaseButton)
        {
            Debug.LogError($"PurchaseItem \"{gameObject.name}\" incorrectly configured");
            return;
        }
        var product = PurchaseManager.Instance.GetProduct(ProductId);
        if(product?.definition.type != ProductType.Consumable)
        {
            SetOwned(PurchaseManager.Instance.NonConsumableOwned(ProductId));
        }
        PriceText.text = PurchaseManager.Instance.UseFakeStore ? FakeStorePrice : product.metadata.localizedPriceString;
        PurchaseManager.Instance.PurchaseProcessed += OnPurchaseProcessed;
    }

    private void OnDestroy()
    {
        PurchaseManager.Instance.PurchaseProcessed -= OnPurchaseProcessed;
    }

    public void InitiatePurchase()
    {
        if (string.IsNullOrWhiteSpace(ProductId))
        {
            Debug.LogError($"Cannot initiate purchase from IAPButtonView {gameObject.name}, no ProductId specified");
        }
        PurchaseManager.Instance.InitiatePurchase(ProductId);
    }

    private void OnPurchaseProcessed(string productId, Product product)
    {
        if(productId == ProductId &&
            product.definition.type != ProductType.Consumable)
        {
            SetOwned(true);
        }
    }

    public void SetOwned(bool owned)
    {
        PurchaseButton.gameObject.SetActive(!owned);
        OwnedPanel.SetActive(owned);
    }

    private void HideAdFreeSection()
    {
        var section = transform;
        while (section != null)
        {
            if (section.name == "Ad Free Section")
            {
                section.gameObject.SetActive(false);
                return;
            }
            section = section.parent;
        }
        gameObject.SetActive(false);
    }
}
