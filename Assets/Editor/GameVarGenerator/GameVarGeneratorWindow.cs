// ============================================================================
//  GameVarGeneratorWindow.cs  —  فقط Editor  —  پنجره‌ی ورودی کاربر
// ============================================================================
//
//  Tools > GameSecurity > Variable Generator
//
//  اینجا اسم متغیر + نوعش + Persistent بودن یا نبودنش رو وارد می‌کنید.
//  با زدن «تولید کد»، GameVars.cs (و در صورت نیاز GameVarsSaveData.cs)
//  به‌طور کامل از نو ساخته می‌شوند. خود شما هیچ‌وقت این دو فایل تولیدی رو
//  دستی ویرایش نمی‌کنید — فقط از همین پنجره کار می‌کنید.
// ============================================================================

using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameSecurity.CodeGen
{
    public class GameVarGeneratorWindow : EditorWindow
    {
        private const string RegistryPath = "Assets/Editor/GameVarRegistry.asset";
        private static readonly Regex ValidIdentifier = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$");

        private GameVarRegistry _registry;
        private Vector2 _scroll;

        private string _newName = "";
        private GameVarType _newType = GameVarType.Int;
        private bool _newPersistent = false;

        // مقدار اولیه — بسته به نوع انتخاب‌شده، فقط کنترل متناسب نمایش داده می‌شود
        private int _newIntValue = 0;
        private float _newFloatValue = 0f;
        private bool _newBoolValue = false;
        private string _newStringValue = "";
        private long _newLongValue = 0L;

        [MenuItem("Tools/GameSecurity/Variable Generator")]
        private static void Open()
        {
            var win = GetWindow<GameVarGeneratorWindow>("GameVars Generator");
            win.minSize = new Vector2(440, 420);
        }

        private void OnEnable() => LoadOrCreateRegistry();

        private void LoadOrCreateRegistry()
        {
            _registry = AssetDatabase.LoadAssetAtPath<GameVarRegistry>(RegistryPath);
            if (_registry != null) return;

            if (!AssetDatabase.IsValidFolder("Assets/Editor"))
                AssetDatabase.CreateFolder("Assets", "Editor");

            _registry = ScriptableObject.CreateInstance<GameVarRegistry>();
            AssetDatabase.CreateAsset(_registry, RegistryPath);
            AssetDatabase.SaveAssets();
        }

        private void OnGUI()
        {
            if (_registry == null)
            {
                LoadOrCreateRegistry();
                return;
            }

            EditorGUILayout.LabelField("تنظیمات کلی", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _registry.outputFolder = EditorGUILayout.TextField("پوشه‌ی خروجی", _registry.outputFolder);
            _registry.defaultSaveKey = EditorGUILayout.TextField("کلید سیو پیش‌فرض", _registry.defaultSaveKey);
            _registry.reobscureInterval = Mathf.Max(0.5f,
                EditorGUILayout.FloatField("فاصله‌ی Reobscure (ثانیه)", _registry.reobscureInterval));
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_registry);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"متغیرها ({_registry.variables.Count})", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
            for (int i = 0; i < _registry.variables.Count; i++)
            {
                var v = _registry.variables[i];
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField(v.name, GUILayout.Width(110));
                EditorGUILayout.LabelField(v.type.ToString(), GUILayout.Width(55));

                EditorGUI.BeginChangeCheck();
                if (v.type == GameVarType.Bool)
                {
                    bool cur = v.defaultValueRaw?.ToLowerInvariant() == "true";
                    bool val = EditorGUILayout.Toggle(cur, GUILayout.Width(30));
                    v.defaultValueRaw = val ? "true" : "false";
                }
                else
                {
                    v.defaultValueRaw = EditorGUILayout.TextField(v.defaultValueRaw ?? "", GUILayout.Width(90));
                }
                v.persistent = EditorGUILayout.ToggleLeft("Persistent", v.persistent, GUILayout.Width(90));
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(_registry);

                if (GUILayout.Button("حذف", GUILayout.Width(50)))
                {
                    _registry.variables.RemoveAt(i);
                    EditorUtility.SetDirty(_registry);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("افزودن متغیر جدید", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _newName = EditorGUILayout.TextField("اسم", _newName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _newType = (GameVarType)EditorGUILayout.EnumPopup("نوع", _newType);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            switch (_newType)
            {
                case GameVarType.Int:
                    _newIntValue = EditorGUILayout.IntField("مقدار اولیه", _newIntValue);
                    break;
                case GameVarType.Float:
                    _newFloatValue = EditorGUILayout.FloatField("مقدار اولیه", _newFloatValue);
                    break;
                case GameVarType.Bool:
                    _newBoolValue = EditorGUILayout.Toggle("مقدار اولیه", _newBoolValue);
                    break;
                case GameVarType.String:
                    _newStringValue = EditorGUILayout.TextField("مقدار اولیه", _newStringValue);
                    break;
                case GameVarType.Long:
                    _newLongValue = EditorGUILayout.LongField("مقدار اولیه", _newLongValue);
                    break;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _newPersistent = EditorGUILayout.ToggleLeft("Persistent (توی فایل سیو ذخیره بشه)", _newPersistent);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("افزودن", GUILayout.Height(26)))
                TryAddVariable();

            EditorGUILayout.Space(15);
            using (new EditorGUI.DisabledScope(_registry.variables.Count == 0))
            {
                if (GUILayout.Button("تولید کد (Generate)", GUILayout.Height(36)))
                {
                    EditorUtility.SetDirty(_registry);
                    AssetDatabase.SaveAssets();
                    GameVarCodeGenerator.Generate(_registry);
                }
            }

            EditorGUILayout.HelpBox(
                "بعد از هر تغییر (افزودن/حذف/تغییر Persistent) دوباره «تولید کد» را بزنید. " +
                "GameVars.cs و GameVarsSaveData.cs هر بار کامل بازنویسی می‌شوند — آن‌ها را دستی ویرایش نکنید.",
                MessageType.Info);
        }

        private void TryAddVariable()
        {
            string name = _newName.Trim();

            if (string.IsNullOrEmpty(name) || !ValidIdentifier.IsMatch(name))
            {
                EditorUtility.DisplayDialog("نام نامعتبر",
                    "اسم متغیر باید یک شناسه‌ی معتبر C# باشد (فقط حروف انگلیسی، عدد، آندرلاین — نه شروع با عدد).",
                    "باشه");
                return;
            }

            if (_registry.variables.Any(v => v.name == name))
            {
                EditorUtility.DisplayDialog("تکراری", $"متغیری با نام '{name}' از قبل وجود دارد.", "باشه");
                return;
            }

            string rawValue = _newType switch
            {
                GameVarType.Int => _newIntValue.ToString(),
                GameVarType.Float => _newFloatValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                GameVarType.Bool => _newBoolValue ? "true" : "false",
                GameVarType.String => _newStringValue,
                GameVarType.Long => _newLongValue.ToString(),
                _ => ""
            };

            _registry.variables.Add(new GameVarDefinition
            {
                name = name,
                type = _newType,
                persistent = _newPersistent,
                defaultValueRaw = rawValue
            });
            EditorUtility.SetDirty(_registry);

            _newName = "";
            _newPersistent = false;
            _newIntValue = 0;
            _newFloatValue = 0f;
            _newBoolValue = false;
            _newStringValue = "";
            _newLongValue = 0L;
        }
    }
}