using UnityEngine;
using UnityEngine.UI;
using UITween;

/// <summary>
/// کامپوننتی آماده برای شمارشِ عددیِ یک متن — مثلاً امتیاز از ۰ تا ۱۰۰۰، یا درصدِ پیشرفت.
/// </summary>
[RequireComponent(typeof(Text))]
public class UICounterText : MonoBehaviour
{
    [Tooltip("مثلاً N0 برای عدد صحیح با جداکننده‌ی هزارگان، یا F1 برای یک رقمِ اعشار")]
    [SerializeField] private string format = "N0";

    Text text;
    void Awake() => text = GetComponent<Text>();

    /// <summary>شمارشِ عددیِ صحیح (مثلاً امتیاز)</summary>
    public Tween CountTo(int targetValue, float duration, int fromValue = 0)
    {
        if (text == null) text = GetComponent<Text>();
        return text.DOCountUp(fromValue, targetValue, duration, format);
    }

    /// <summary>شمارشِ عددیِ اعشاری (مثلاً درصد یا پول)</summary>
    public Tween CountTo(float targetValue, float duration, float fromValue = 0f)
    {
        if (text == null) text = GetComponent<Text>();
        return text.DOCountUp(fromValue, targetValue, duration, format);
    }
}
