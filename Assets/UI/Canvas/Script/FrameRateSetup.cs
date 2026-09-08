using UnityEngine;

/// <summary>
/// این رو روی یه آبجکتِ همیشه‌فعال (مثلاً GameManager) در همون اولین صحنه‌ی بازی بذارید.
/// بدونِ این تنظیم، خیلی از گوشی‌های اندروید پیش‌فرض روی ۳۰ فریم قفل می‌شن،
/// نه به‌خاطرِ ضعفِ هاردور بلکه چون یونیتی صراحتاً بهشون نگفته ۶۰ بخواد.
/// </summary>
public class FrameRateSetup : MonoBehaviour
{
    [SerializeField] private int targetFrameRate = 60;

    void Awake()
    {
        // vSyncCount باید صفر باشه، وگرنه روی بعضی دستگاه‌ها targetFrameRate رو نادیده می‌گیره
        // و فریم‌ریت رو به نرخِ رفرشِ صفحه (که می‌تونه ۹۰/۱۲۰ یا حتی متغیر باشه) گره می‌زنه.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;
    }
}
