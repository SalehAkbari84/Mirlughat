// ============================================================================
//  GameVarRegistry.cs  —  فقط Editor  —  منبع اصلی داده‌ی متغیرها
// ============================================================================
//
//  این فایل هیچ کد runtime تولید نمی‌کند. فقط لیست متغیرهایی که از پنجره‌ی
//  Tools > GameSecurity > Variable Generator اضافه کرده‌اید را نگه می‌دارد.
//  هر بار که دکمه‌ی "تولید کد" را بزنید، فایل‌های GameVars.cs و
//  GameVarsSaveData.cs کاملاً از روی همین لیست از نو ساخته می‌شوند —
//  یعنی این ScriptableObject تنها منبع حقیقت (Single Source of Truth) است
//  و شما هیچ‌وقت نباید GameVars.cs را دستی ویرایش کنید.
//
//  ⚠️  این فایل باید داخل یک پوشه به نام "Editor" باشد (مثلاً
//      Assets/Editor/GameVarGenerator/) تا در بیلد نهایی وارد نشود.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameSecurity.CodeGen
{
    /// <summary>انواع پشتیبانی‌شده — دقیقاً متناظر با Obscured Types در GameSecurity Toolkit</summary>
    public enum GameVarType
    {
        Int,
        Float,
        Bool,
        String,
        Long
    }

    /// <summary>تعریف یک متغیر: اسم، نوع، و این‌که آیا باید در سیو ذخیره شود یا نه</summary>
    [Serializable]
    public class GameVarDefinition
    {
        public string name;
        public GameVarType type;
        public bool persistent;

        /// <summary>
        /// مقدار اولیه به‌صورت متن خام (مثلاً "1000" برای Int، "9.5" برای Float،
        /// "true"/"false" برای Bool، متن دلخواه برای String). اگر خالی باشد،
        /// از مقدار پیش‌فرض نوع (0 / 0f / false / "" / 0L) استفاده می‌شود.
        /// </summary>
        public string defaultValueRaw;
    }

    /// <summary>
    /// دارایی (Asset) نگه‌دارنده‌ی لیست متغیرها + تنظیمات تولید کد.
    /// به‌صورت خودکار در Assets/Editor/GameVarRegistry.asset ساخته می‌شود.
    /// </summary>
    public class GameVarRegistry : ScriptableObject
    {
        [Tooltip("پوشه‌ای که فایل‌های GameVars.cs و GameVarsSaveData.cs توش ساخته می‌شوند")]
        public string outputFolder = "Assets/Scripts/Generated";

        [Tooltip("کلیدی که با آن SecureSave.Save/Load پیش‌فرض صدا زده می‌شود")]
        public string defaultSaveKey = "playerSave";

        [Tooltip("هر چند ثانیه یک‌بار بایت‌های RAM متغیرها به‌صورت خودکار Reobscure شوند")]
        public float reobscureInterval = 4f;

        public List<GameVarDefinition> variables = new List<GameVarDefinition>();
    }
}