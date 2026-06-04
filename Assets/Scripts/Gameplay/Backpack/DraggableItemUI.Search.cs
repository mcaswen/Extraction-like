using UnityEngine;
using UnityEngine.UI;

public partial class DraggableItemUI
{
    // 搜索未完成或揭示动画仍在播放时，物品应保持不可交互
    private bool CanInteractWithItem()
    {
        return _runtimeState.CanInteract && !_isRevealAnimating;
    }

    // 自动推进搜索进度，并在完成时切换到揭示动画阶段
    private void TickSearchProgress()
    {
        if (!RequiresSearch || IsSearched || !gameObject.activeInHierarchy)
        {
            return;
        }

        bool completed = _runtimeState.AdvanceSearchProgress(Time.unscaledDeltaTime);
        if (completed)
        {
            _isRevealAnimating = true;
            _revealAnimationTimer = 0f;
            UpdateAmountText();
        }

        UpdateSearchVisualState();
    }

    // 播放搜索完成后的短暂揭示动画，让遮罩自然退场
    private void TickSearchRevealAnimation()
    {
        if (!_isRevealAnimating)
        {
            return;
        }

        _revealAnimationTimer += Time.unscaledDeltaTime;
        if (_revealAnimationTimer >= RevealAnimationDuration)
        {
            _isRevealAnimating = false;
            _revealAnimationTimer = 0f;
        }

        UpdateSearchVisualState();
    }

    // 延迟创建搜索遮罩相关节点，避免普通物品平白增加层级和组件
    private void EnsureSearchOverlay()
    {
        if (_searchOverlayRoot != null)
        {
            return;
        }

        if (_defaultSearchSprite == null)
        {
            Texture2D whiteTexture = Texture2D.whiteTexture;
            _defaultSearchSprite = Sprite.Create(
                whiteTexture,
                new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
                new Vector2(0.5f, 0.5f));
        }

        GameObject overlayRootObject = new GameObject("SearchOverlay", typeof(RectTransform));
        overlayRootObject.transform.SetParent(transform, false);
        _searchOverlayRoot = overlayRootObject.GetComponent<RectTransform>();
        _searchOverlayCanvasGroup = overlayRootObject.AddComponent<CanvasGroup>();
        _searchOverlayRoot.anchorMin = Vector2.zero;
        _searchOverlayRoot.anchorMax = Vector2.one;
        _searchOverlayRoot.offsetMin = Vector2.zero;
        _searchOverlayRoot.offsetMax = Vector2.zero;
        _searchOverlayRoot.pivot = new Vector2(0.5f, 0.5f);

        GameObject backdropObject = new GameObject("Backdrop", typeof(Image));
        backdropObject.transform.SetParent(_searchOverlayRoot, false);
        _searchBackdropImage = backdropObject.GetComponent<Image>();
        RectTransform backdropRect = _searchBackdropImage.rectTransform;
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        _searchBackdropImage.sprite = _defaultSearchSprite;
        _searchBackdropImage.type = Image.Type.Simple;
        _searchBackdropImage.color = new Color(0.02f, 0.03f, 0.04f, 0.78f);

        GameObject outerTrackObject = new GameObject("OuterTrack", typeof(Image));
        outerTrackObject.transform.SetParent(_searchOverlayRoot, false);
        _searchOuterTrackImage = outerTrackObject.GetComponent<Image>();
        RectTransform outerTrackRect = _searchOuterTrackImage.rectTransform;
        outerTrackRect.anchorMin = new Vector2(0.08f, 0.08f);
        outerTrackRect.anchorMax = new Vector2(0.92f, 0.92f);
        outerTrackRect.offsetMin = Vector2.zero;
        outerTrackRect.offsetMax = Vector2.zero;
        _searchOuterTrackImage.sprite = _defaultSearchSprite;
        _searchOuterTrackImage.type = Image.Type.Simple;
        _searchOuterTrackImage.color = new Color(0.15f, 0.18f, 0.22f, 0.9f);

        GameObject progressObject = new GameObject("Progress", typeof(Image));
        progressObject.transform.SetParent(_searchOverlayRoot, false);
        _searchProgressImage = progressObject.GetComponent<Image>();
        RectTransform progressRect = _searchProgressImage.rectTransform;
        progressRect.anchorMin = new Vector2(0.12f, 0.12f);
        progressRect.anchorMax = new Vector2(0.88f, 0.88f);
        progressRect.offsetMin = Vector2.zero;
        progressRect.offsetMax = Vector2.zero;
        _searchProgressImage.sprite = _defaultSearchSprite;
        _searchProgressImage.type = Image.Type.Filled;
        _searchProgressImage.fillMethod = Image.FillMethod.Radial360;
        _searchProgressImage.fillOrigin = 2;
        _searchProgressImage.fillClockwise = false;
        _searchProgressImage.color = new Color(0.98f, 0.92f, 0.52f, 0.95f);

        GameObject pulseRingObject = new GameObject("PulseRing", typeof(Image));
        pulseRingObject.transform.SetParent(_searchOverlayRoot, false);
        _searchPulseRingImage = pulseRingObject.GetComponent<Image>();
        RectTransform pulseRect = _searchPulseRingImage.rectTransform;
        pulseRect.anchorMin = new Vector2(0.16f, 0.16f);
        pulseRect.anchorMax = new Vector2(0.84f, 0.84f);
        pulseRect.offsetMin = Vector2.zero;
        pulseRect.offsetMax = Vector2.zero;
        _searchPulseRingImage.sprite = _defaultSearchSprite;
        _searchPulseRingImage.type = Image.Type.Simple;
        _searchPulseRingImage.color = new Color(1f, 1f, 1f, 0.15f);

        GameObject sweepObject = new GameObject("Sweep", typeof(Image));
        sweepObject.transform.SetParent(_searchOverlayRoot, false);
        _searchSweepImage = sweepObject.GetComponent<Image>();
        RectTransform sweepRect = _searchSweepImage.rectTransform;
        sweepRect.anchorMin = new Vector2(0.485f, 0.1f);
        sweepRect.anchorMax = new Vector2(0.515f, 0.9f);
        sweepRect.offsetMin = Vector2.zero;
        sweepRect.offsetMax = Vector2.zero;
        _searchSweepImage.sprite = _defaultSearchSprite;
        _searchSweepImage.type = Image.Type.Simple;
        _searchSweepImage.color = new Color(0.96f, 0.99f, 1f, 0.18f);

        GameObject centerGlowObject = new GameObject("CenterGlow", typeof(Image));
        centerGlowObject.transform.SetParent(_searchOverlayRoot, false);
        _searchCenterGlowImage = centerGlowObject.GetComponent<Image>();
        RectTransform centerGlowRect = _searchCenterGlowImage.rectTransform;
        centerGlowRect.anchorMin = new Vector2(0.28f, 0.28f);
        centerGlowRect.anchorMax = new Vector2(0.72f, 0.72f);
        centerGlowRect.offsetMin = Vector2.zero;
        centerGlowRect.offsetMax = Vector2.zero;
        _searchCenterGlowImage.sprite = _defaultSearchSprite;
        _searchCenterGlowImage.type = Image.Type.Simple;
        _searchCenterGlowImage.color = new Color(1f, 1f, 1f, 0.08f);

        GameObject revealFlashObject = new GameObject("RevealFlash", typeof(Image));
        revealFlashObject.transform.SetParent(_searchOverlayRoot, false);
        _searchRevealFlashImage = revealFlashObject.GetComponent<Image>();
        RectTransform revealFlashRect = _searchRevealFlashImage.rectTransform;
        revealFlashRect.anchorMin = Vector2.zero;
        revealFlashRect.anchorMax = Vector2.one;
        revealFlashRect.offsetMin = Vector2.zero;
        revealFlashRect.offsetMax = Vector2.zero;
        _searchRevealFlashImage.sprite = _defaultSearchSprite;
        _searchRevealFlashImage.type = Image.Type.Simple;
        _searchRevealFlashImage.color = new Color(1f, 1f, 1f, 0f);

        _searchOverlayRoot.gameObject.SetActive(false);
    }

    // 搜索遮罩始终压在物品视觉最上层，避免被数量文本等元素覆盖
    private void ResizeSearchOverlay()
    {
        if (_searchOverlayRoot == null)
        {
            return;
        }

        _searchOverlayRoot.SetAsLastSibling();
    }

    // 根据搜索进度和揭示阶段刷新遮罩 动画 物品显隐
    private void UpdateSearchVisualState()
    {
        EnsureSearchOverlay();

        bool showOverlay = RequiresSearch && !IsSearched;
        bool showReveal = _isRevealAnimating;
        if (_searchOverlayRoot != null)
        {
            _searchOverlayRoot.gameObject.SetActive(showOverlay || showReveal);
        }

        if (!showOverlay && !showReveal)
        {
            if (_itemImage != null)
            {
                _itemImage.color = Color.white;
            }

            return;
        }

        float normalizedProgress = SearchDurationSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(SearchProgressSeconds / SearchDurationSeconds);
        float revealNormalized = _isRevealAnimating
            ? Mathf.Clamp01(_revealAnimationTimer / RevealAnimationDuration)
            : 0f;

        if (_searchOverlayCanvasGroup != null)
        {
            _searchOverlayCanvasGroup.alpha = _isRevealAnimating ? 1f - revealNormalized : 1f;
        }

        if (_itemImage != null)
        {
            // 搜索中将物品压暗，揭示阶段再逐步恢复原图颜色
            Color hiddenColor = new Color(0.16f, 0.18f, 0.2f, 0.94f);
            _itemImage.color = Color.Lerp(hiddenColor, Color.white, revealNormalized);
        }

        if (_searchBackdropImage != null)
        {
            float backdropPulse = 0.76f + Mathf.Sin(Time.unscaledTime * 3.8f) * 0.06f;
            _searchBackdropImage.color = new Color(0.02f, 0.03f, 0.04f, backdropPulse);
        }

        if (_searchOuterTrackImage != null)
        {
            float trackPulse = 0.78f + Mathf.Sin(Time.unscaledTime * 4.2f) * 0.08f;
            _searchOuterTrackImage.color = new Color(0.16f, 0.2f, 0.24f, trackPulse);
        }

        if (_searchProgressImage != null)
        {
            _searchProgressImage.fillAmount = normalizedProgress;
            float progressAlpha = 0.72f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.12f;
            _searchProgressImage.color = new Color(0.92f, 0.97f, 1f, progressAlpha);
            _searchProgressImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 210f);
        }

        if (_searchPulseRingImage != null)
        {
            float pulseScale = 0.94f + Mathf.Sin(Time.unscaledTime * 5.1f) * 0.06f;
            _searchPulseRingImage.rectTransform.localScale = new Vector3(pulseScale, pulseScale, 1f);
            _searchPulseRingImage.color = new Color(0.88f, 0.95f, 1f, 0.1f + (1f - normalizedProgress) * 0.12f);
        }

        if (_searchSweepImage != null)
        {
            _searchSweepImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 280f);
            _searchSweepImage.color = new Color(0.94f, 0.99f, 1f, 0.2f);
        }

        if (_searchCenterGlowImage != null)
        {
            float glowStrength = 0.08f + normalizedProgress * 0.18f;
            _searchCenterGlowImage.color = new Color(0.92f, 0.97f, 1f, glowStrength);
        }

        if (_searchRevealFlashImage != null)
        {
            if (_isRevealAnimating)
            {
                // 完成搜索时叠加一次由亮到暗的闪光，强调揭示结果
                float flashAlpha = Mathf.Clamp01(1f - revealNormalized) * 0.85f;
                _searchRevealFlashImage.color = new Color(1f, 1f, 1f, flashAlpha);
                float flashScale = 0.88f + revealNormalized * 0.25f;
                _searchRevealFlashImage.rectTransform.localScale = new Vector3(flashScale, flashScale, 1f);
            }
            else
            {
                _searchRevealFlashImage.color = new Color(1f, 1f, 1f, 0f);
                _searchRevealFlashImage.rectTransform.localScale = Vector3.one;
            }
        }
    }
}
