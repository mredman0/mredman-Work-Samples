using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AdAwarePanel : MonoBehaviour
{
    public bool BannerAdAware = true;
    public RectTransform BannerAdBackground;

    // Start is called before the first frame update
    void Start()
    {
        if(BannerAdAware)
        {
            AdjustOffsetMinForBanner();
            AdManager.Instance.BannerAdInitialized += AdjustOffsetMinForBanner;
        }
        AdManager.Instance.AdFreeChanged += OnAdFreeChanged;
    }

    private void OnDestroy()
    {
        AdManager.Instance.BannerAdInitialized -= AdjustOffsetMinForBanner;
        AdManager.Instance.AdFreeChanged -= OnAdFreeChanged;
    }

    private void OnAdFreeChanged(bool adFree)
    {
        AdjustOffsetMinForBanner();
    }

    private float previouslyAppliedOffset = 0;
    private void AdjustOffsetMinForBanner()
    {
        var rect = GetComponent<RectTransform>();
        var offsetMin = rect.offsetMin;
        var bannerHeight = AdManager.Instance.BannerAdHeight; // actual height
        var canvas = GetComponentInParent<Canvas>();
        bannerHeight = (int)(bannerHeight / canvas.scaleFactor); // reference height
        if(AdManager.Instance.AdFreeMode)
        {
            bannerHeight = 0;
        }
        offsetMin.y += bannerHeight - previouslyAppliedOffset;
        rect.offsetMin = offsetMin;

        if(BannerAdBackground)
        {
            var bannerAdOffsetMax = BannerAdBackground.offsetMax;
            bannerAdOffsetMax.y += bannerHeight - previouslyAppliedOffset;
            BannerAdBackground.offsetMax = bannerAdOffsetMax;
        }
        Debug.Log($"AdAwarePanel adjusted by: {bannerHeight} (actual: {AdManager.Instance.BannerAdHeight})");

        previouslyAppliedOffset = bannerHeight;
    }
}
