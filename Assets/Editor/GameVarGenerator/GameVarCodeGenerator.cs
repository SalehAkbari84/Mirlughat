// ============================================================================
//  GameVarCodeGenerator.cs  —  فقط Editor  —  موتور تولید کد
// ============================================================================
//
//  این کلاس از روی GameVarRegistry، دو فایل runtime واقعی می‌سازد:
//
//    • GameVars.cs         — فیلدهای static عمومی (Obscured...) + تیک‌زن خودکار
//    • GameVarsSaveData.cs — فقط اگه حداقل یک متغیر Persistent باشه
//
//  چرا کد را «تولید» می‌کنیم و از Dictionary/Reflection استفاده نمی‌کنیم؟
//  چون شما گفتید سرعت خواندن/نوشتن باید حداکثر و بدون مکث باشد. یک
//  Dictionary<string, object> برای هر خواندن/نوشتن یک struct مثل ObscuredInt
//  نیاز به boxing دارد (یعنی GC allocation) و یک string lookup هم دارد.
//  اما یک فیلد static معمولی (GameVars.Gold) دقیقاً هم‌ارزِ دسترسی مستقیم به
//  متغیر است — صفر overhead، صفر تخصیص حافظه.
//
//  به همین دلیل ReobscureAll() و SaveAll/LoadAll هم به‌جای Reflection،
//  مستقیماً برای هر متغیر یک خط کد صریح تولید می‌کنند.
// ============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameSecurity.CodeGen
{
    public static class GameVarCodeGenerator
    {
        public static void Generate(GameVarRegistry registry)
        {
            if (registry == null || registry.variables.Count == 0)
            {
                Debug.LogWarning("[GameVarGenerator] هیچ متغیری تعریف نشده — چیزی تولید نشد.");
                return;
            }

            string folder = string.IsNullOrEmpty(registry.outputFolder)
                ? "Assets/Scripts/Generated"
                : registry.outputFolder;

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var persistentVars = registry.variables.Where(v => v.persistent).ToList();

            File.WriteAllText(Path.Combine(folder, "GameVars.cs"), BuildGameVarsSource(registry, persistentVars));

            string saveDataPath = Path.Combine(folder, "GameVarsSaveData.cs");
            if (persistentVars.Count > 0)
            {
                File.WriteAllText(saveDataPath, BuildSaveDataSource(persistentVars));
            }
            else if (File.Exists(saveDataPath))
            {
                // دیگه هیچ متغیر Persistent‌ای نیست — فایل قدیمی رو پاک کن که کامپایل خراب نشه
                File.Delete(saveDataPath);
            }

            AssetDatabase.Refresh();
            Debug.Log($"[GameVarGenerator] {registry.variables.Count} متغیر ({persistentVars.Count} تای اون Persistent) در '{folder}' تولید شد.");
        }

        // ────────────────────────────────────────────────────────────────
        //  GameVars.cs
        // ────────────────────────────────────────────────────────────────
        private static string BuildGameVarsSource(GameVarRegistry registry, List<GameVarDefinition> persistentVars)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// ============================================================================");
            sb.AppendLine("//  GameVars.cs — AUTO-GENERATED توسط GameVarGeneratorWindow");
            sb.AppendLine("//  ⚠️  این فایل را دستی ویرایش نکنید — با هر بار «تولید کد» کاملاً بازنویسی می‌شود.");
            sb.AppendLine("//  برای افزودن/حذف متغیر: Tools > GameSecurity > Variable Generator");
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using GameSecurity;");
            if (persistentVars.Count > 0)
                sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine();
            sb.AppendLine("public static class GameVars");
            sb.AppendLine("{");

            sb.AppendLine("    // ── متغیرها: دسترسی مستقیم از هر جای پروژه، بدون هیچ واسطه‌ای ──");
            foreach (var v in registry.variables)
                sb.AppendLine($"    public static {ObscuredTypeName(v.type)} {v.name} = {DefaultLiteral(v.type)};");
            sb.AppendLine();

            sb.AppendLine($"    internal const float ReobscureInterval = {registry.reobscureInterval}f;");
            sb.AppendLine();

            sb.AppendLine("    // ── نصب خودکار تیک‌زن — نیازی به هیچ کار دستی نیست ──");
            sb.AppendLine("    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]");
            sb.AppendLine("    private static void AutoInstall()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (Object.FindObjectOfType<GameVarsTicker>() != null) return; // idempotent");
            sb.AppendLine("        var go = new GameObject(\"~GameVarsTicker\");");
            sb.AppendLine("        Object.DontDestroyOnLoad(go);");
            sb.AppendLine("        go.hideFlags = HideFlags.HideInHierarchy;");
            sb.AppendLine("        go.AddComponent<GameVarsTicker>();");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    /// <summary>هر متغیر مستقیم Reobscure می‌شود — بدون Reflection، بدون Loop روی Dictionary.</summary>");
            sb.AppendLine("    internal static void ReobscureAll()");
            sb.AppendLine("    {");
            foreach (var v in registry.variables)
                sb.AppendLine($"        {v.name}.Reobscure();");
            sb.AppendLine("    }");

            if (persistentVars.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("    // ── ذخیره / بارگذاری خودکار (فقط متغیرهای Persistent) ──");

                sb.AppendLine($"    public static void SaveAll(string key = \"{registry.defaultSaveKey}\")");
                sb.AppendLine("    {");
                sb.AppendLine("        var data = new GameVarsSaveData();");
                foreach (var v in persistentVars) sb.AppendLine($"        data.{v.name} = {v.name}.Value;");
                sb.AppendLine("        SecureSave.Save(key, data);");
                sb.AppendLine("    }");
                sb.AppendLine();

                sb.AppendLine($"    public static async Task SaveAllAsync(string key = \"{registry.defaultSaveKey}\")");
                sb.AppendLine("    {");
                sb.AppendLine("        var data = new GameVarsSaveData();");
                foreach (var v in persistentVars) sb.AppendLine($"        data.{v.name} = {v.name}.Value;");
                sb.AppendLine("        await SecureSave.SaveAsync(key, data);");
                sb.AppendLine("    }");
                sb.AppendLine();

                sb.AppendLine($"    public static void LoadAll(string key = \"{registry.defaultSaveKey}\")");
                sb.AppendLine("    {");
                sb.AppendLine("        var data = SecureSave.Load(key, new GameVarsSaveData());");
                foreach (var v in persistentVars) sb.AppendLine($"        {v.name} = data.{v.name};");
                sb.AppendLine("    }");
                sb.AppendLine();

                sb.AppendLine($"    public static async Task LoadAllAsync(string key = \"{registry.defaultSaveKey}\")");
                sb.AppendLine("    {");
                sb.AppendLine("        var data = await SecureSave.LoadAsync(key, new GameVarsSaveData());");
                foreach (var v in persistentVars) sb.AppendLine($"        {v.name} = data.{v.name};");
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("// ── تیک‌زن داخلی: هر چند ثانیه یک‌بار (نه هر فریم) بایت‌های RAM را جابه‌جا می‌کند ──");
            sb.AppendLine("internal sealed class GameVarsTicker : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine("    private void Start() => StartCoroutine(Tick());");
            sb.AppendLine();
            sb.AppendLine("    private System.Collections.IEnumerator Tick()");
            sb.AppendLine("    {");
            sb.AppendLine("        var wait = new WaitForSeconds(GameVars.ReobscureInterval);");
            sb.AppendLine("        while (true)");
            sb.AppendLine("        {");
            sb.AppendLine("            yield return wait;");
            sb.AppendLine("            GameVars.ReobscureAll();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────────────
        //  GameVarsSaveData.cs
        // ────────────────────────────────────────────────────────────────
        private static string BuildSaveDataSource(List<GameVarDefinition> persistentVars)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// AUTO-GENERATED توسط GameVarGeneratorWindow — دستی ویرایش نکنید.");
            sb.AppendLine("// این کلاس فقط مقادیر خام (plain) متغیرهای Persistent را نگه می‌دارد؛");
            sb.AppendLine("// چون Obscured Types فیلدهای [SerializeField] ندارند و JsonUtility نمی‌تواند");
            sb.AppendLine("// مستقیم آن‌ها را سریالایز کند.");
            sb.AppendLine("[System.Serializable]");
            sb.AppendLine("public class GameVarsSaveData");
            sb.AppendLine("{");
            foreach (var v in persistentVars)
                sb.AppendLine($"    public {PlainTypeName(v.type)} {v.name};");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────────────
        //  نگاشت نوع‌ها
        // ────────────────────────────────────────────────────────────────
        private static string ObscuredTypeName(GameVarType t) => t switch
        {
            GameVarType.Int => "ObscuredInt",
            GameVarType.Float => "ObscuredFloat",
            GameVarType.Bool => "ObscuredBool",
            GameVarType.String => "ObscuredString",
            GameVarType.Long => "ObscuredLong",
            _ => "ObscuredInt"
        };

        private static string PlainTypeName(GameVarType t) => t switch
        {
            GameVarType.Int => "int",
            GameVarType.Float => "float",
            GameVarType.Bool => "bool",
            GameVarType.String => "string",
            GameVarType.Long => "long",
            _ => "int"
        };

        private static string DefaultLiteral(GameVarType t) => t switch
        {
            GameVarType.Int => "0",
            GameVarType.Float => "0f",
            GameVarType.Bool => "false",
            GameVarType.String => "\"\"",
            GameVarType.Long => "0L",
            _ => "default"
        };
    }
}
