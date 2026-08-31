using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CustomToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image backgroundImage;   // کادر سفید
    [SerializeField] private RectTransform handleRect; // دکمه گرد

    [Header("Colors")]
    [SerializeField] private Color onColor = Color.green;
    [SerializeField] private Color offColor = Color.red;

    [Header("Settings")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private float padding = 8f;

    private bool isOn = true;
    private Coroutine currentAnimation;

    private void Start()
    {
        // چک کردن صحت تنظیمات
        ValidateComponents();
        SetStateImmediate(isOn);
    }

    private void ValidateComponents()
    {
        if (backgroundImage == null) Debug.LogError("لطفاً 'Background Image' را در Inspector وصل کنید!");
        if (handleRect == null) Debug.LogError("لطفاً 'Handle Rect' را در Inspector وصل کنید!");
        
        // چک کردن Anchorها برای جلوگیری از غیب شدن
        if (handleRect != null && (handleRect.anchorMin != new Vector2(0.5f, 0.5f) || handleRect.anchorMax != new Vector2(0.5f, 0.5f)))
        {
            Debug.LogWarning("⚠️ لنگرهای (Anchors) دکمه گرد روی 'Middle-Center' نیستند! تنظیم خودکار انجام شد.");
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    // ==================== متدهای عمومی ====================

    public void TurnOn()
    {
        if (isOn) return;
        isOn = true;
        AnimateToState(isOn);
    }

    public void TurnOff()
    {
        if (!isOn) return;
        isOn = false;
        AnimateToState(isOn);
    }

    public void Toggle()
    {
        if (isOn) TurnOff();
        else TurnOn();
    }

    // ==================== انیمیشن ====================

    private void AnimateToState(bool targetState)
    {
        if (currentAnimation != null) StopCoroutine(currentAnimation);
        currentAnimation = StartCoroutine(AnimateCoroutine(targetState));
    }

    private IEnumerator AnimateCoroutine(bool targetState)
    {
        Vector2 startPos = handleRect.anchoredPosition;
        Vector2 endPos = GetTargetPosition(targetState);
        
        Color startColor = backgroundImage.color;
        Color endColor = targetState ? onColor : offColor;

        float elapsedTime = 0f;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration;
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            handleRect.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
            backgroundImage.color = Color.Lerp(startColor, endColor, smoothT);

            yield return null;
        }

        handleRect.anchoredPosition = endPos;
        backgroundImage.color = endColor;
        currentAnimation = null;
    }

    private void SetStateImmediate(bool state)
    {
        handleRect.anchoredPosition = GetTargetPosition(state);
        backgroundImage.color = state ? onColor : offColor;
    }

    // ==================== محاسبه امن موقعیت ====================

    private Vector2 GetTargetPosition(bool isOnRight)
    {
        if (handleRect.parent == null) return Vector2.zero;

        RectTransform parentRect = handleRect.parent as RectTransform;
        float parentWidth = parentRect.rect.width;
        float handleWidth = handleRect.rect.width;

        // اگر عرض والد صفر باشد (مثلاً GameObject جدید است) خطا نده
        if (parentWidth <= 0 || handleWidth <= 0) return handleRect.anchoredPosition;

        float offset = (parentWidth / 2f) - (handleWidth / 2f) - padding;

        // اگر Padding بزرگتر از حد مجاز بود، آن را محدود کن تا دکمه غیب نشود
        if (offset < 0) offset = 0;

        return new Vector2(isOnRight ? offset : -offset, handleRect.anchoredPosition.y);
    }

    // ==================== تست در ادیتور ====================

    #if UNITY_EDITOR
    [ContextMenu("Toggle Switch")]
    private void ToggleInEditor()
    {
        Toggle();
    }
    #endif
}