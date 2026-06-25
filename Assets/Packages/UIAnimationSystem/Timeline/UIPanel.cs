using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Timeline
{
    // Version-agnostic bridge to the UI Toolkit runtime host.
    //
    //   Unity 6.5+ : prefers PanelRenderer (the new host), but still supports an
    //                existing UIDocument (those keep working at runtime).
    //   Older      : UIDocument only.
    //
    // Auto-detects whichever host the GameObject/scene actually has, so the same
    // project runs on old and new Unity with zero changes on your part.
    public static class UIPanel
    {
        public static string HostTypeName =>
#if UNITY_6000_5_OR_NEWER
            "Panel Renderer (or UIDocument)";
#else
            "UIDocument";
#endif

        // Invoke onReady(root) once, when the panel root is available on this GameObject.
        public static void WhenReady(GameObject go, Action<VisualElement> onReady)
        {
            if (go == null || onReady == null) return;

#if UNITY_6000_5_OR_NEWER
            var pr = go.GetComponent<PanelRenderer>();
            if (pr != null)
            {
                UILog.Log($"UIPanel: found Panel Renderer on '{go.name}', registering reload callback...");
                bool done = false;
                // 3-arg lambda binds to the versioned reload callback; fires
                // immediately if the UI is already built, else on the next reload.
                pr.RegisterUIReloadCallback((panel, root, version) =>
                {
                    UILog.Log($"UIPanel: Panel Renderer reload callback fired (version {version}, root {(root == null ? "NULL" : root.name)}).");
                    if (done || root == null) return;
                    done = true;
                    onReady(root);
                });
                return;
            }
            UILog.Log($"UIPanel: no Panel Renderer on '{go.name}', trying UIDocument...");
#endif
            var doc = GetUIDocument(go);
            if (doc == null)
            {
                UILog.Warn($"No Panel Renderer or UIDocument on '{go.name}'. Put the director on the GameObject that hosts the UI panel.");
                Debug.LogWarning($"[UIPanel] No UI host (Panel Renderer/UIDocument) on '{go.name}'.", go);
                return;
            }
            UILog.Log($"UIPanel: found UIDocument on '{go.name}'.");
            var docRoot = GetDocRoot(doc);
            if (docRoot == null) { UILog.Warn("UIDocument.rootVisualElement is null."); return; }
            docRoot.schedule.Execute(() => onReady(docRoot)).ExecuteLater(1); // wait one frame for layout
        }

        // The first UI host GameObject in the open scene, or null.
        public static GameObject FindHost()
        {
#if UNITY_6000_5_OR_NEWER
            var pr = UnityEngine.Object.FindAnyObjectByType<PanelRenderer>();
            if (pr != null) return pr.gameObject;
#endif
            var doc = FindAnyUIDocument();
            return doc != null ? doc.gameObject : null;
        }

        public static bool HasHost(GameObject go)
        {
            if (go == null) return false;
#if UNITY_6000_5_OR_NEWER
            if (go.GetComponent<PanelRenderer>() != null) return true;
#endif
            return GetUIDocument(go) != null;
        }

        public static VisualTreeAsset GetVisualTree(GameObject go)
        {
            if (go == null) return null;
#if UNITY_6000_5_OR_NEWER
            var pr = go.GetComponent<PanelRenderer>();
            if (pr != null) return pr.visualTreeAsset;
#endif
            var doc = GetUIDocument(go);
            return doc != null ? GetDocTree(doc) : null;
        }

        public static void SetVisualTree(GameObject go, VisualTreeAsset uxml)
        {
            if (go == null) return;
#if UNITY_6000_5_OR_NEWER
            var pr = go.GetComponent<PanelRenderer>();
            if (pr != null) { pr.visualTreeAsset = uxml; return; }
#endif
            var doc = GetUIDocument(go);
            if (doc != null) SetDocTree(doc, uxml);
        }

        // ---- UIDocument access (still valid at runtime on 6.5; just deprecated,
        // so the obsolete warning is silenced around each use) ----
        static UIDocument GetUIDocument(GameObject go)
        {
#pragma warning disable 618
            return go.GetComponent<UIDocument>();
#pragma warning restore 618
        }

        static UIDocument FindAnyUIDocument()
        {
#pragma warning disable 618
            return UnityEngine.Object.FindAnyObjectByType<UIDocument>();
#pragma warning restore 618
        }

        static VisualElement GetDocRoot(UIDocument doc)
        {
#pragma warning disable 618
            return doc.rootVisualElement;
#pragma warning restore 618
        }

        static VisualTreeAsset GetDocTree(UIDocument doc)
        {
#pragma warning disable 618
            return doc.visualTreeAsset;
#pragma warning restore 618
        }

        static void SetDocTree(UIDocument doc, VisualTreeAsset uxml)
        {
#pragma warning disable 618
            doc.visualTreeAsset = uxml;
#pragma warning restore 618
        }
    }
}
