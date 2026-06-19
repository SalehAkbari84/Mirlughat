#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Editor
{
    // Utilities for inspecting a cloned visual tree: building a flat list with
    // depth, and producing a stable display name for elements that lack a name.
    public static class VisualTreeUtil
    {
        public struct Node
        {
            public VisualElement element;
            public int depth;
            public string displayName;
            public bool hasName;
        }

        public static List<Node> Flatten(VisualElement root)
        {
            var list = new List<Node>();
            if (root == null) return list;
            Recurse(root, 0, list);
            return list;
        }

        static void Recurse(VisualElement ve, int depth, List<Node> list)
        {
            list.Add(new Node
            {
                element = ve,
                depth = depth,
                hasName = !string.IsNullOrEmpty(ve.name),
                displayName = DisplayName(ve)
            });
            foreach (var child in ve.Children())
                Recurse(child, depth + 1, list);
        }

        public static string DisplayName(VisualElement ve)
        {
            if (!string.IsNullOrEmpty(ve.name)) return ve.name;
            string type = ve.GetType().Name;
            foreach (var c in ve.GetClasses())
                return $"{type} .{c}"; // first class only
            return type;
        }
    }
}
#endif
