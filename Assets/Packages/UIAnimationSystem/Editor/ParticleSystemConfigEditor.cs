#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UIToolkit.Animation.Particles;

namespace UIToolkit.Animation.Editor
{
    // Custom inspector that lets you add/remove polymorphic ParticleModule entries
    // (which [SerializeReference] alone does not provide an add button for).
    [CustomEditor(typeof(ParticleSystemConfig))]
    public class ParticleSystemConfigEditor : UnityEditor.Editor
    {
        static Type[] _moduleTypes;

        void OnEnable()
        {
            if (_moduleTypes == null)
            {
                _moduleTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                    .Where(t => typeof(ParticleModule).IsAssignableFrom(t) && !t.IsAbstract)
                    .ToArray();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // draw everything except the modules list (we render it ourselves)
            DrawPropertiesExcluding(serializedObject, "modules", "m_Script");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);

            var config = (ParticleSystemConfig)target;

            for (int i = 0; i < config.modules.Count; i++)
            {
                var module = config.modules[i];
                if (module == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                module.enabled = EditorGUILayout.ToggleLeft(module.DisplayName, module.enabled, EditorStyles.boldLabel);

                GUI.enabled = i > 0;
                if (GUILayout.Button("^", GUILayout.Width(24))) { Move(config, i, i - 1); }
                GUI.enabled = i < config.modules.Count - 1;
                if (GUILayout.Button("v", GUILayout.Width(24))) { Move(config, i, i + 1); }
                GUI.enabled = true;
                if (GUILayout.Button("x", GUILayout.Width(24)))
                {
                    Undo.RecordObject(config, "Remove Module");
                    config.modules.RemoveAt(i);
                    EditorUtility.SetDirty(config);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                var modulesProp = serializedObject.FindProperty("modules");
                if (i < modulesProp.arraySize)
                {
                    var elem = modulesProp.GetArrayElementAtIndex(i);
                    EditorGUI.indentLevel++;
                    var iter = elem.Copy();
                    var end = iter.GetEndProperty();
                    bool first = true;
                    while (iter.NextVisible(first) && !SerializedProperty.EqualContents(iter, end))
                    {
                        first = false;
                        if (iter.name == "enabled") continue;
                        EditorGUILayout.PropertyField(iter, true);
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("+ Add Module"))
                ShowAddMenu(config);

            serializedObject.ApplyModifiedProperties();
            if (GUI.changed) EditorUtility.SetDirty(config);
        }

        void Move(ParticleSystemConfig config, int from, int to)
        {
            Undo.RecordObject(config, "Reorder Module");
            var m = config.modules[from];
            config.modules.RemoveAt(from);
            config.modules.Insert(to, m);
            EditorUtility.SetDirty(config);
        }

        void ShowAddMenu(ParticleSystemConfig config)
        {
            var menu = new GenericMenu();
            foreach (var t in _moduleTypes)
            {
                var captured = t;
                menu.AddItem(new GUIContent(t.Name.Replace("Module", "")), false, () =>
                {
                    Undo.RecordObject(config, "Add Module");
                    config.modules.Add((ParticleModule)Activator.CreateInstance(captured));
                    EditorUtility.SetDirty(config);
                });
            }
            menu.ShowAsContext();
        }
    }
}
#endif
