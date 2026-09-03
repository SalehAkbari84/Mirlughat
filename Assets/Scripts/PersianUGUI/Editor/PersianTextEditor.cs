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
        private SerializedProperty _abbreviateNumbersProp;
        private SerializedProperty _abbreviateThresholdProp;
        private SerializedProperty _abbreviationDecimalsProp;
        private SerializedProperty _abbreviationDecimalSeparatorProp;
        private SerializedProperty _thousandUnitProp;
        private SerializedProperty _millionUnitProp;
        private SerializedProperty _billionUnitProp;
        private SerializedProperty _trillionUnitProp;

        private void OnEnable()
        {
            _sourceTextProp = serializedObject.FindProperty("sourceText");
            _autoRightAlignProp = serializedObject.FindProperty("autoRightAlign");
            _convertDigitsProp = serializedObject.FindProperty("convertDigitsToPersian");
            _autoFormatNumbersProp = serializedObject.FindProperty("autoFormatNumbers");
            _separatorProp = serializedObject.FindProperty("thousandsSeparator");
            _abbreviateNumbersProp = serializedObject.FindProperty("abbreviateNumbers");
            _abbreviateThresholdProp = serializedObject.FindProperty("abbreviateThreshold");
            _abbreviationDecimalsProp = serializedObject.FindProperty("abbreviationDecimals");
            _abbreviationDecimalSeparatorProp = serializedObject.FindProperty("abbreviationDecimalSeparator");
            _thousandUnitProp = serializedObject.FindProperty("thousandUnit");
            _millionUnitProp = serializedObject.FindProperty("millionUnit");
            _billionUnitProp = serializedObject.FindProperty("billionUnit");
            _trillionUnitProp = serializedObject.FindProperty("trillionUnit");
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

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("کوتاه‌سازی اعداد بزرگ", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_abbreviateNumbersProp, new GUIContent("کوتاه‌سازی فعال"));

            using (new EditorGUI.DisabledScope(!_abbreviateNumbersProp.boolValue))
            {
                EditorGUILayout.PropertyField(_abbreviateThresholdProp, new GUIContent("آستانه (از این عدد به بعد)"));
                EditorGUILayout.PropertyField(_abbreviationDecimalsProp, new GUIContent("تعداد رقم اعشار"));
                EditorGUILayout.PropertyField(_abbreviationDecimalSeparatorProp, new GUIContent("جداکننده اعشار"));
                EditorGUILayout.PropertyField(_thousandUnitProp, new GUIContent("برچسب هزار"));
                EditorGUILayout.PropertyField(_millionUnitProp, new GUIContent("برچسب میلیون"));
                EditorGUILayout.PropertyField(_billionUnitProp, new GUIContent("برچسب میلیارد"));
                EditorGUILayout.PropertyField(_trillionUnitProp, new GUIContent("برچسب تریلیون"));
            }

            if (_abbreviateNumbersProp.boolValue)
                EditorGUILayout.HelpBox("مثلاً 1000000 می‌شه \"1 میلیون\". اگه عدد از آستانه کمتر باشه، دست‌نخورده می‌مونه (یا با فرمت هزارگان معمولی، اگه اونم فعال باشه).", MessageType.Info);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);

            string raw = _sourceTextProp.stringValue;
            if (!string.IsNullOrEmpty(raw))
            {
                var options = new PersianTextEngine.Options
                {
                    ConvertDigitsToPersian = _convertDigitsProp.boolValue,
                    AutoFormatNumbers = _autoFormatNumbersProp.boolValue,
                    ThousandsSeparator = (char)_separatorProp.intValue,
                    AbbreviateNumbers = _abbreviateNumbersProp.boolValue,
                    AbbreviateThreshold = _abbreviateThresholdProp.longValue,
                    AbbreviationDecimals = _abbreviationDecimalsProp.intValue,
                    AbbreviationDecimalSeparator = (char)_abbreviationDecimalSeparatorProp.intValue,
                    ThousandUnit = _thousandUnitProp.stringValue,
                    MillionUnit = _millionUnitProp.stringValue,
                    BillionUnit = _billionUnitProp.stringValue,
                    TrillionUnit = _trillionUnitProp.stringValue
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