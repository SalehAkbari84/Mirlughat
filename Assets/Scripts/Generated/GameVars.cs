// ============================================================================
//  GameVars.cs — AUTO-GENERATED توسط GameVarGeneratorWindow
//  ⚠️  این فایل را دستی ویرایش نکنید — با هر بار «تولید کد» کاملاً بازنویسی می‌شود.
//  برای افزودن/حذف متغیر: Tools > GameSecurity > Variable Generator
// ============================================================================

using UnityEngine;
using GameSecurity;
using System.Threading.Tasks;

public static class GameVars
{
    // ── متغیرها: دسترسی مستقیم از هر جای پروژه، بدون هیچ واسطه‌ای ──
    public static ObscuredInt Coin = 0;

    internal const float ReobscureInterval = 4f;

    // ── نصب خودکار تیک‌زن — نیازی به هیچ کار دستی نیست ──
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstall()
    {
        if (Object.FindObjectOfType<GameVarsTicker>() != null) return; // idempotent
        var go = new GameObject("~GameVarsTicker");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<GameVarsTicker>();
    }

    /// <summary>هر متغیر مستقیم Reobscure می‌شود — بدون Reflection، بدون Loop روی Dictionary.</summary>
    internal static void ReobscureAll()
    {
        Coin.Reobscure();
    }

    // ── ذخیره / بارگذاری خودکار (فقط متغیرهای Persistent) ──
    public static void SaveAll(string key = "playerSave")
    {
        var data = new GameVarsSaveData();
        data.Coin = Coin.Value;
        SecureSave.Save(key, data);
    }

    public static async Task SaveAllAsync(string key = "playerSave")
    {
        var data = new GameVarsSaveData();
        data.Coin = Coin.Value;
        await SecureSave.SaveAsync(key, data);
    }

    public static void LoadAll(string key = "playerSave")
    {
        var data = SecureSave.Load(key, new GameVarsSaveData());
        Coin = data.Coin;
    }

    public static async Task LoadAllAsync(string key = "playerSave")
    {
        var data = await SecureSave.LoadAsync(key, new GameVarsSaveData());
        Coin = data.Coin;
    }
}

// ── تیک‌زن داخلی: هر چند ثانیه یک‌بار (نه هر فریم) بایت‌های RAM را جابه‌جا می‌کند ──
internal sealed class GameVarsTicker : MonoBehaviour
{
    private void Start() => StartCoroutine(Tick());

    private System.Collections.IEnumerator Tick()
    {
        var wait = new WaitForSeconds(GameVars.ReobscureInterval);
        while (true)
        {
            yield return wait;
            GameVars.ReobscureAll();
        }
    }
}
