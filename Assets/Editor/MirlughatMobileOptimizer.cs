using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot optimization batch for Mirlughat mobile build.
/// Sets Android texture import overrides on all UI textures. Run via menu:
/// Tools > Mirlughat Optimize > Apply All.
///
/// NOTE: TMP font assets are intentionally NOT touched. The Persian text pipeline
/// (PersianTextEngine -> PersianText) renders via Arabic presentation forms
/// (U+FB50-U+FEFF), which are only available at runtime when the font atlas is
/// Dynamic with the source TTF connected. Flipping fonts to Static breaks every
/// Persian label in the game (missing glyphs render as dashes/underscores).
/// </summary>
public static class MirlughatMobileOptimizer
{
    const string Menu = "Tools/Mirlughat Optimize/";

    [MenuItem(Menu + "Apply All (Textures)", priority = 0)]
    public static void ApplyAll()
    {
        OptimizeTextures();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MirlughatOptimizer] All done. Check console for counts.");
    }

    // ────────────────────────────── TEXTURES ──────────────────────────────

    [MenuItem(Menu + "Textures Only", priority = 1)]
    public static void OptimizeTextures()
    {
        string[] folders = { "Assets/Elements", "Assets/Packages/EpicVictoryEffects" };
        int changed = 0, skipped = 0;

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) { Debug.LogWarning($"[MirlughatOptimizer] folder missing: {folder}"); continue; }

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if ( importer == null) { skipped++; continue; }

                // Idempotency: skip if Android override already set to our values.
                var existing = importer.GetPlatformTextureSettings("Android");
                if (existing.overridden && existing.maxTextureSize == 1024) { skipped++; continue; }

                importer.mipmapEnabled = false;              // UI sprites never need mips
                importer.streamingMipmaps = false;

                var android = new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    maxTextureSize = 1024,
                    format = TextureImporterFormat.Automatic   // respects hardware (ETC2/ASTC)
                };
                importer.SetPlatformTextureSettings(android);

                // Crunched saves a lot of APK/RAM for UI art with no visible loss
                importer.crunchedCompression = true;
                importer.compressionQuality = 60;

                importer.SaveAndReimport();
                changed++;
            }
        }
        Debug.Log($"[MirlughatOptimizer] Textures: {changed} changed, {skipped} skipped (already optimized).");
    }

    // ─────────────────────────────── FONTS ───────────────────────────────

    [MenuItem(Menu + "Fonts (skipped — see note)", priority = 2)]
    public static void OptimizeFonts()
    {
        Debug.LogWarning(
            "[MirlughatOptimizer] Font assets are intentionally left untouched: " +
            "the Persian text pipeline (PersianTextEngine) needs Dynamic atlases with the " +
            "source TTF connected so Arabic presentation-form glyphs can be added at runtime. " +
            "Flipping them to Static breaks every Persian label in the game.");
    }
}
