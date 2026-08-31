#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PersianUGUI.Editor
{
    /// <summary>
    /// اینسپکتور اختصاصی برای PersianText - متن shaped و فرمت‌شده رو
    /// مستقیم زیر فیلدها نشون می‌ده تا نیازی به Play کردن نباشه.
    ///
    /// این فایل باید داخل یک پوشه به اسم "Editor" قرار بگیره
    /// (مثلاً Assets/PersianUGUI/Editor/PersianTextEditor.cs)
    /// </summary>
    [CustomEditor(typeof(PersianText))]
    [CanEditMultipleObjects]
    public class PersianTextEditor : UnityEditor.Editor
    {
        private SerializedProperty _sourceTextProp;
        private SerializedProperty _autoRightAlignProp;
        private SerializedProperty _convertDigitsProp;
        private SerializedProperty _autoFormatNumbersProp;
        private SerializedProperty _separatorProp;

        private void OnEnable()
        {
            _sourceTextProp = serializedObject.FindProperty("sourceText");
            _autoRightAlignProp = serializedObject.FindProperty("autoRightAlign");
            _convertDigitsProp = serializedObject.FindProperty("convertDigitsToPersian");
            _autoFormatNumbersProp = serializedObject.FindProperty("autoFormatNumbers");
            _separatorProp = serializedObject.FindProperty("thousandsSeparator");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_sourceTextProp, new GUIContent("متن خام (Source Text)"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("تراز", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_autoRightAlignProp, new GUIContent("راست‌چین خودکار"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("عدد و مبلغ", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_convertDigitsProp, new GUIContent("تبدیل اعداد به فارسی"));
            EditorGUILayout.PropertyField(_autoFormatNumbersProp, new GUIContent("فرمت خودکار مبلغ"));

            using (new EditorGUI.DisabledScope(!_autoFormatNumbersProp.boolValue))
                EditorGUILayout.PropertyField(_separatorProp, new GUIContent("جداکننده هزارگان"));

            if (_autoFormatNumbersProp.boolValue)
                EditorGUILayout.HelpBox("هر عدد خالصی که مستقیم توی متن خام بنویسی، خودکار جدا می‌شه. نیازی به تایپ کاما یا هیچ نشونه‌ی خاصی نیست.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);

            string raw = _sourceTextProp.stringValue;
            if (!string.IsNullOrEmpty(raw))
            {
                var options = new PersianTextEngine.Options
                {
                    ConvertDigitsToPersian = _convertDigitsProp.boolValue,
                    AutoFormatNumbers = _autoFormatNumbersProp.boolValue,
                    ThousandsSeparator = (char)_separatorProp.intValue
                };
                string shaped = PersianTextEngine.Process(raw, options);

                EditorGUILayout.LabelField("پیش‌نمایش خروجی:", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(shaped, MessageType.None);
            }

            if (GUILayout.Button("پاک کردن کش موتور (Clear Engine Cache)"))
                PersianTextEngine.ClearCache();
        }
    }
}
#endif