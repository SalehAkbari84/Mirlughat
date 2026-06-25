#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UIToolkit.Animation.Timeline;
using UIToolkit.Animation.Particles;

namespace UIToolkit.Animation.Editor
{
    // The single, unified authoring window. Everything lives here as tabs so you
    // never switch windows:
    //   Left   : hierarchy + live UXML preview (click an element to select it)
    //   Right  : Setup | Animation (embedded timeline) | Particles | Bindings
    //   Toolbar: scene asset, zoom, and a one-click "Setup & Play".
    //
    // Open: Window -> UI Toolkit -> Scene Animator (or double-click a UISceneAnimation).
    public class UISceneAnimatorWindow : EditorWindow
    {
        // ---- palette ----
        static readonly Color C_Panel   = new Color(0.17f, 0.17f, 0.20f);
        static readonly Color C_Accent  = new Color(0.30f, 0.50f, 0.85f);
        static readonly Color C_AccentD = new Color(0.22f, 0.30f, 0.42f);
        static readonly Color C_Text    = new Color(0.85f, 0.85f, 0.88f);
        static readonly Color C_Sub     = new Color(0.62f, 0.62f, 0.67f);
        static readonly Color C_Green   = new Color(0.28f, 0.60f, 0.34f);
        static readonly Color C_GreenHi = new Color(0.34f, 0.72f, 0.40f);

        UISceneAnimation _scene;

        // library tab filter + persisted state keys
        string _librarySearch = "";
        const string PrefScene = "UISceneAnimator.LastSceneGUID";
        const string PrefTab = "UISceneAnimator.ActiveTab";

        // preview
        VisualElement _previewClip;     // clipping container that the canvas fits into
        VisualElement _previewHost;     // scaled host that holds the cloned tree
        VisualElement _clonedRoot;
        Label _previewInfo;
        float _previewScale = 1f;
        bool _autoFit = true;
        Vector2 _canvasSize = new Vector2(1920, 1080);
        Slider _zoomSlider;
        ToolbarToggle _autoFitToggle;

        // pan/zoom (UI Builder style: wheel to zoom toward cursor, middle-drag to pan)
        Vector2 _panOffset;
        bool _panning;
        Vector2 _panStartMouse;
        Vector2 _panStartOffset;

        // hierarchy
        ScrollView _hierarchyScroll;
        readonly Dictionary<VisualElement, VisualElement> _treeRowByElement = new Dictionary<VisualElement, VisualElement>();
        readonly Dictionary<VisualElement, bool> _expanded = new Dictionary<VisualElement, bool>();
        string _hierarchySearch = "";

        // selection
        VisualElement _selectedElement;
        VisualElement _selectionOverlay;

        // right panel
        VisualElement _rightPanel;
        int _activeTab; // 0 Setup, 1 Animate, 2 Effects, 3 Manage
        const int TabAnimation = 1;
        static readonly string[] TabNames = { "Setup", "Animate", "Effects", "Manage" };

        // animation editing
        UIAnimationClip _activeClip;
        TimelineView _timeline;
        ClipPlayer _previewClipPlayer;
        VisualElement _timelineDock;
        Label _timelineDockTitle;
        ObjectField _dockClipField;

        // record mode (drag the selected element in the live preview to write keys)
        bool _recording;
        bool _recDragging;
        Vector2 _recStartMouse;
        Vector2 _recStartTranslate;

        // preset selection (persisted across panel rebuilds)
        AnimationPresets.Kind _animPresetKind = AnimationPresets.Kind.FadeIn;
        ParticlePresets.Kind _particlePresetKind = ParticlePresets.Kind.Confetti;

        // full-scene live preview (ticks all bound clips + particles in the editor)
        bool _scenePreviewing;
        double _scenePrevLast;
        Button _previewSceneBtn;
        readonly List<ClipPlayer> _scenePlayers = new List<ClipPlayer>();
        readonly List<ParticleEmitter> _sceneEmitters = new List<ParticleEmitter>();
        Sequence _previewSequence;

        // particle designer (Effects tab)
        ParticleSystemConfig _fxConfig;
        VisualElement _fxPreviewHost;
        ParticleEmitter _fxEmitter;
        double _fxLast;
        bool _fxRunning;

        [MenuItem("Window/UI Toolkit/Scene Animator")]
        public static void Open()
        {
            var w = GetWindow<UISceneAnimatorWindow>();
            w.titleContent = new GUIContent("UI Scene Animator");
            w.minSize = new Vector2(820, 520);
        }

        [UnityEditor.Callbacks.OnOpenAsset]
        public static bool OnOpen(int instanceID, int line)
        {
#if UNITY_6000_5_OR_NEWER
            // OnOpenAsset only gives an int; EntityIdToObject takes an EntityId, so
            // the int->EntityId conversion is unavoidable here. Silence just that
            // deprecation on this line.
#pragma warning disable 618
            var obj = EditorUtility.EntityIdToObject(instanceID);   // InstanceIDToObject renamed in 6.5
#pragma warning restore 618
#else
            var obj = EditorUtility.InstanceIDToObject(instanceID);
#endif
            if (obj is UISceneAnimation scene)
            {
                var w = GetWindow<UISceneAnimatorWindow>();
                w.titleContent = new GUIContent("UI Scene Animator");
                w.SetScene(scene);
                return true;
            }
            if (obj is UIAnimationClip clip)
            {
                var w = GetWindow<UISceneAnimatorWindow>();
                w.titleContent = new GUIContent("UI Scene Animator");
                w.OpenClip(clip);
                return true;
            }
            return false;
        }

        // Open a clip in this single window (Animation tab + timeline dock).
        public void OpenClip(UIAnimationClip clip)
        {
            _activeClip = clip;
            _activeTab = TabAnimation;
            if (_rightPanel != null) SetActiveClip(clip);
        }

        public void SetScene(UISceneAnimation scene)
        {
            _scene = scene;
            _selectedElement = null;
            if (scene != null)
                EditorPrefs.SetString(PrefScene, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(scene)));
            RebuildAll();
        }

        // Restore the last scene + tab so reopening the window resumes where you left off.
        void RestoreState()
        {
            _activeTab = Mathf.Clamp(EditorPrefs.GetInt(PrefTab, 0), 0, TabNames.Length - 1);
            if (_scene == null)
            {
                string guid = EditorPrefs.GetString(PrefScene, "");
                if (!string.IsNullOrEmpty(guid))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(p)) _scene = AssetDatabase.LoadAssetAtPath<UISceneAnimation>(p);
                }
            }
        }

        // ===============================================================
        //  CONSTRUCTION
        // ===============================================================
        void CreateGUI()
        {
            if (minSize.x > 821f || minSize.y > 521f) minSize = new Vector2(820, 520);

            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            root.AddToClassList("usa-root");
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UIAnimationSystem/Editor/SceneAnimator.uss");
            if (uss != null) root.styleSheets.Add(uss);

            RestoreState();
            BuildToolbar(root);

            // Vertical split: main work area on top, full-width timeline dock at the
            // bottom (like Unity's Animation window docked under the editor).
            var vsplit = new TwoPaneSplitView(1, 280, TwoPaneSplitViewOrientation.Vertical);
            root.Add(vsplit);

            var topArea = new VisualElement { style = { flexGrow = 1, minHeight = 200 } };
            vsplit.Add(topArea);

            var hsplit = new TwoPaneSplitView(0, 240, TwoPaneSplitViewOrientation.Horizontal);
            topArea.Add(hsplit);

            // LEFT: hierarchy
            var leftPane = new VisualElement { style = { minWidth = 180 } };
            leftPane.Add(Header("HIERARCHY"));
            _hierarchyScroll = new ScrollView { style = { flexGrow = 1 } };
            leftPane.Add(_hierarchyScroll);
            hsplit.Add(leftPane);

            // CENTER + RIGHT
            var centerRight = new TwoPaneSplitView(1, 360, TwoPaneSplitViewOrientation.Horizontal);
            hsplit.Add(centerRight);

            // CENTER: preview
            var centerPane = new VisualElement { style = { flexGrow = 1, minWidth = 300 } };
            centerPane.Add(Header("PREVIEW  (auto-fit; click an element to select)"));
            _previewClip = new VisualElement
            {
                style = { flexGrow = 1, marginLeft = 6, marginRight = 6, marginBottom = 6,
                          overflow = Overflow.Hidden, position = Position.Relative,
                          backgroundColor = new Color(0.08f,0.08f,0.1f) }
            };
            _previewClip.RegisterCallback<GeometryChangedEvent>(_ => FitPreview());
            _previewClip.RegisterCallback<WheelEvent>(OnPreviewWheel);
            _previewClip.RegisterCallback<MouseDownEvent>(OnPreviewPanDown);
            _previewClip.RegisterCallback<MouseMoveEvent>(OnPreviewPanMove);
            _previewClip.RegisterCallback<MouseUpEvent>(OnPreviewPanUp);
            _previewHost = new VisualElement
            {
                style = { position = Position.Absolute, left = 0, top = 0,
                          transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(0)) }
            };
            _previewClip.Add(_previewHost);

            _selectionOverlay = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, borderTopWidth = 2, borderBottomWidth = 2,
                          borderLeftWidth = 2, borderRightWidth = 2,
                          borderTopColor = Color.yellow, borderBottomColor = Color.yellow,
                          borderLeftColor = Color.yellow, borderRightColor = Color.yellow,
                          display = DisplayStyle.None }
            };
            // NOTE: the overlay is parented to _previewHost (the scaled canvas) in
            // ReloadPreview so it zooms/pans together with the element it marks.

            _previewInfo = new Label { style = { position = Position.Absolute, bottom = 4, left = 6,
                          fontSize = 10, color = C_Sub } };
            _previewClip.Add(_previewInfo);

            centerPane.Add(_previewClip);
            centerRight.Add(centerPane);

            // RIGHT: tabs
            _rightPanel = new VisualElement { style = { minWidth = 260, flexGrow = 0 } };
            centerRight.Add(_rightPanel);

            // BOTTOM: full-width timeline dock.
            BuildTimelineDock(vsplit);

            RebuildAll();
        }

        // Full-width timeline strip at the bottom. Always shows the active clip and
        void BuildTimelineDock(VisualElement parent)
        {
            _timelineDock = new VisualElement { style = { flexGrow = 1, minHeight = 120 } };

            var header = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center,
                          backgroundColor = C_Panel, paddingLeft = 8, paddingRight = 6, paddingTop = 2, paddingBottom = 2 }
            };
            _timelineDockTitle = new Label("TIMELINE") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text, flexShrink = 0, marginRight = 8 } };
            header.Add(_timelineDockTitle);

            _dockClipField = new ObjectField { objectType = typeof(UIAnimationClip), value = _activeClip, style = { flexGrow = 1, flexShrink = 1, minWidth = 80, maxWidth = 260 } };
            _dockClipField.RegisterValueChangedCallback(e => SetActiveClip(e.newValue as UIAnimationClip));
            header.Add(_dockClipField);
            header.Add(new Button(CreateClipFlow) { text = "New", style = { width = 44, marginLeft = 4, flexShrink = 0 } });
            _timelineDock.Add(header);

            EnsureTimeline();
            _timelineDock.Add(_timeline);

            parent.Add(_timelineDock);
            UpdateTimelineDock();
        }

        void UpdateTimelineDock()
        {
            if (_timelineDockTitle != null)
                _timelineDockTitle.text = _activeClip != null ? $"TIMELINE  -  {_activeClip.name}" : "TIMELINE  (no clip)";
            if (_dockClipField != null) _dockClipField.SetValueWithoutNotify(_activeClip);
        }

        Label Header(string t) => new Label(t)
        {
            style = { unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 6, marginTop = 4,
                      marginBottom = 2, fontSize = 11, color = C_Text }
        };

        void BuildToolbar(VisualElement root)
        {
            // A wrapping row so the controls reflow onto multiple lines on narrow
            // windows instead of being clipped (responsive toolbar).
            var bar = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, alignItems = Align.Center,
                          backgroundColor = C_Panel, paddingLeft = 4, paddingRight = 4, paddingTop = 2, paddingBottom = 2,
                          borderBottomWidth = 1, borderBottomColor = new Color(0, 0, 0, 0.3f) }
            };

            var sceneField = new ObjectField { objectType = typeof(UISceneAnimation), value = _scene, style = { width = 200 } };
            sceneField.RegisterValueChangedCallback(e => SetScene(e.newValue as UISceneAnimation));
            bar.Add(sceneField);

            bar.Add(new ToolbarSpacer());
            bar.Add(new ToolbarButton(ReloadPreview) { text = "Reload UXML" }
                .SetIcon("Reload UXML", "Refresh", "d_Refresh"));
            bar.Add(new ToolbarButton(CreateSceneFlow) { text = "New Scene" }
                .SetIcon("New Scene Animation", "Toolbar Plus", "d_Toolbar Plus"));

            bar.Add(new ToolbarSpacer());
            _autoFitToggle = new ToolbarToggle { text = "Auto Fit", value = _autoFit };
            _autoFitToggle.RegisterValueChangedCallback(e => { _autoFit = e.newValue; FitPreview(); });
            bar.Add(_autoFitToggle);

            bar.Add(new Label("Zoom") { style = { unityTextAlign = TextAnchor.MiddleCenter, marginLeft = 4 } });
            _zoomSlider = new Slider(0.05f, 5f) { value = _previewScale, style = { width = 100 } };
            _zoomSlider.RegisterValueChangedCallback(e =>
            {
                DisableAutoFit();
                _previewScale = e.newValue; ApplyPreviewTransform();
            });
            bar.Add(_zoomSlider);
            bar.Add(new ToolbarButton(FrameSelected) { text = "Frame" });
            bar.Add(new ToolbarButton(ResetView) { text = "Reset View" });

            bar.Add(new ToolbarSpacer());
            _previewSceneBtn = new ToolbarButton(ToggleScenePreview) { text = "Preview Scene" };
            _previewSceneBtn.tooltip = "Play all bound clips and particles live inside this window (no Play Mode).";
            bar.Add(_previewSceneBtn);

            var playBtn = HoverButton(">  Setup & Play", SetupAndPlay, C_Green, C_GreenHi);
            playBtn.tooltip = "Attach a UISceneDirector to the UI host (Panel Renderer on 6.5+, else UIDocument) in the open scene and enter Play Mode.";
            playBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            playBtn.style.color = Color.white;
            playBtn.style.paddingLeft = 10; playBtn.style.paddingRight = 10;
            playBtn.style.marginRight = 4;
            bar.Add(playBtn);

            root.Add(bar);
        }

        // ===============================================================
        //  SHARED UI HELPERS
        // ===============================================================
        static Button HoverButton(string text, Action onClick, Color bg, Color hover)
        {
            var b = new Button(onClick) { text = text };
            b.style.backgroundColor = bg;
            b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = hover);
            b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = bg);
            return b;
        }

        static Label SectionLabel(string text) => new Label(text)
        {
            style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8, marginBottom = 2, color = C_Text }
        };

        static VisualElement Card()
        {
            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = C_Panel, marginLeft = 8, marginRight = 8, marginTop = 6,
                    paddingLeft = 8, paddingRight = 8, paddingTop = 6, paddingBottom = 8,
                    borderTopLeftRadius = 5, borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5, borderBottomRightRadius = 5
                }
            };
            card.AddToClassList("usa-card");
            return card;
        }

        static Foldout Fold(string title, VisualElement content, bool open)
        {
            var f = new Foldout { text = title, value = open, style = { marginLeft = 8, marginRight = 8, marginTop = 4 } };
            f.contentContainer.Add(content);
            return f;
        }

        // ===============================================================
        //  REBUILD
        // ===============================================================
        void RebuildAll()
        {
            if (_previewHost == null) return;
            ReloadPreview();
            RebuildHierarchy();
            RebuildRightPanel();
        }

        // ---------- PREVIEW ----------
        void ReloadPreview()
        {
            if (_previewHost == null) return;
            _previewHost.Clear();
            _clonedRoot = null;
            _treeRowByElement.Clear();
            _expanded.Clear();
            _previewClipPlayer = null;

            if (_scene == null || _scene.uxml == null)
            {
                if (_previewInfo != null) _previewInfo.text = "No UXML assigned.";
                RebuildHierarchy();
                return;
            }

            // Auto-detect the canvas size: use the scene's previewSize if it was
            // set, otherwise fall back to a 1080p design canvas. No manual sizing.
            _canvasSize = (_scene.previewSize.x > 1f && _scene.previewSize.y > 1f)
                ? _scene.previewSize
                : new Vector2(1920, 1080);

            _clonedRoot = new VisualElement { name = "__scene_root__" };
            _clonedRoot.style.width = _canvasSize.x;
            _clonedRoot.style.height = _canvasSize.y;
            _clonedRoot.style.backgroundColor = _scene.previewBackground;

            _scene.uxml.CloneTree(_clonedRoot);

            foreach (var ss in _scene.additionalStyleSheets)
                if (ss != null) _clonedRoot.styleSheets.Add(ss);

            _previewHost.Add(_clonedRoot);
            if (_selectionOverlay != null) _previewHost.Add(_selectionOverlay);   // overlay scales/pans with the canvas
            HookSelection(_clonedRoot);

            RebuildPreviewPlayer();
            // Fit after the layout has resolved so we measure the real pane size.
            _previewHost.schedule.Execute(() => { FitPreview(); UpdateSelectionOverlay(); }).ExecuteLater(30);
            FitPreview();
        }

        void RebuildPreviewPlayer()
        {
            _previewClipPlayer = (_activeClip != null && _clonedRoot != null)
                ? new ClipPlayer(_activeClip, _clonedRoot)
                : null;
            if (_timeline != null) _previewClipPlayer?.Sample(_timeline.Playhead);
        }

        // Scale and center the canvas inside the preview pane. When Auto Fit is on
        // the scale is computed to fit the whole layout; otherwise the zoom value
        // is used. Recomputed whenever the pane resizes or the scene reloads.
        void FitPreview()
        {
            if (_previewHost == null || _previewClip == null) return;

            float availW = _previewClip.resolvedStyle.width;
            float availH = _previewClip.resolvedStyle.height;
            float cw = Mathf.Max(1f, _canvasSize.x);
            float ch = Mathf.Max(1f, _canvasSize.y);

            if (_autoFit && availW > 1f && availH > 1f)
            {
                _previewScale = Mathf.Min(availW / cw, availH / ch) * 0.96f;
                _panOffset = new Vector2((availW - cw * _previewScale) * 0.5f, (availH - ch * _previewScale) * 0.5f);
            }
            ApplyPreviewTransform();
        }

        // Apply current scale + pan to the preview host and refresh the readout.
        void ApplyPreviewTransform()
        {
            if (_previewHost == null) return;
            float scale = Mathf.Max(0.01f, _previewScale);
            _previewHost.style.scale = new Scale(new Vector3(scale, scale, 1f));
            _previewHost.style.left = _panOffset.x;
            _previewHost.style.top = _panOffset.y;
            if (_zoomSlider != null) _zoomSlider.SetValueWithoutNotify(scale);
            if (_previewInfo != null)
                _previewInfo.text = $"{_canvasSize.x:0}x{_canvasSize.y:0}  @  {scale * 100f:0}%{(_autoFit ? "  (auto)" : "")}   [wheel: zoom, middle-drag: pan]";
            UpdateSelectionOverlay();
        }

        void DisableAutoFit()
        {
            _autoFit = false;
            _autoFitToggle?.SetValueWithoutNotify(false);
        }

        // Zoom/pan so the given element fills ~60% of the preview, centered.
        void FrameElement(VisualElement el)
        {
            if (el == null || _previewClip == null || _clonedRoot == null) return;
            float scale = Mathf.Max(0.01f, _previewScale);
            var rootW = _clonedRoot.worldBound;
            var eb = el.worldBound;
            if (eb.width < 1f || eb.height < 1f) return;

            float elx = (eb.x - rootW.x) / scale;
            float ely = (eb.y - rootW.y) / scale;
            float ew = eb.width / scale;
            float eh = eb.height / scale;

            float availW = _previewClip.resolvedStyle.width;
            float availH = _previewClip.resolvedStyle.height;
            float target = Mathf.Clamp(Mathf.Min(availW / Mathf.Max(1f, ew), availH / Mathf.Max(1f, eh)) * 0.6f, 0.05f, 5f);

            Vector2 center = new Vector2(elx + ew * 0.5f, ely + eh * 0.5f);
            _panOffset = new Vector2(availW * 0.5f - center.x * target, availH * 0.5f - center.y * target);
            _previewScale = target;
            DisableAutoFit();
            ApplyPreviewTransform();
        }

        void FrameSelected() { if (_selectedElement != null) FrameElement(_selectedElement); }

        void ResetView()
        {
            _autoFit = true;
            _autoFitToggle?.SetValueWithoutNotify(true);
            FitPreview();
        }

        void OnPreviewWheel(WheelEvent e)
        {
            DisableAutoFit();
            float old = Mathf.Max(0.01f, _previewScale);
            float factor = e.delta.y < 0f ? 1.1f : 0.9f;     // wheel up = zoom in
            float ns = Mathf.Clamp(old * factor, 0.05f, 5f);
            // keep the content point under the cursor fixed while zooming
            Vector2 m = _previewClip.WorldToLocal(e.mousePosition);
            Vector2 content = (m - _panOffset) / old;
            _panOffset = m - content * ns;
            _previewScale = ns;
            ApplyPreviewTransform();
            e.StopPropagation();
        }

        void OnPreviewPanDown(MouseDownEvent e)
        {
            if (e.button != 2) return;     // middle mouse button only
            _panning = true;
            _panStartMouse = e.mousePosition;
            _panStartOffset = _panOffset;
            _previewClip.CaptureMouse();
            e.StopPropagation();
        }

        void OnPreviewPanMove(MouseMoveEvent e)
        {
            if (!_panning) return;
            DisableAutoFit();
            _panOffset = _panStartOffset + ((Vector2)e.mousePosition - _panStartMouse);
            ApplyPreviewTransform();
            e.StopPropagation();
        }

        void OnPreviewPanUp(MouseUpEvent e)
        {
            if (!_panning || e.button != 2) return;
            _panning = false;
            if (_previewClip.HasMouseCapture()) _previewClip.ReleaseMouse();
            e.StopPropagation();
        }

        void HookSelection(VisualElement ve)
        {
            ve.RegisterCallback<MouseDownEvent>(OnPreviewMouseDown, TrickleDown.NoTrickleDown);
            ve.RegisterCallback<MouseMoveEvent>(OnPreviewMouseMove, TrickleDown.NoTrickleDown);
            ve.RegisterCallback<MouseUpEvent>(OnPreviewMouseUp, TrickleDown.NoTrickleDown);
        }

        void OnPreviewMouseDown(MouseDownEvent e)
        {
            if (e.button == 2) return;   // middle button is reserved for panning

            // Record mode: if the click lands on the already-selected element,
            // start dragging it to write Translate keys instead of re-selecting.
            if (_recording && _activeClip != null && _selectedElement != null
                && _selectedElement.worldBound.Contains(e.mousePosition))
            {
                _recDragging = true;
                _recStartMouse = e.mousePosition;
                _recStartTranslate = new Vector2(_selectedElement.resolvedStyle.translate.x, _selectedElement.resolvedStyle.translate.y);
                _clonedRoot.CaptureMouse();
                e.StopPropagation();
                return;
            }

            if (e.target is VisualElement hit)
            {
                SelectElement(hit);
                e.StopPropagation();
            }
        }

        void OnPreviewMouseMove(MouseMoveEvent e)
        {
            if (!_recDragging || _selectedElement == null) return;
            float scale = Mathf.Max(0.0001f, _previewScale);
            Vector2 deltaWorld = (Vector2)e.mousePosition - _recStartMouse;
            Vector2 nt = _recStartTranslate + deltaWorld / scale;
            _selectedElement.style.translate = new Translate(nt.x, nt.y, 0f);
            UpdateSelectionOverlay();
            e.StopPropagation();
        }

        void OnPreviewMouseUp(MouseUpEvent e)
        {
            if (!_recDragging) return;
            _recDragging = false;
            if (_clonedRoot != null && _clonedRoot.HasMouseCapture()) _clonedRoot.ReleaseMouse();

            if (_activeClip == null || _selectedElement == null || string.IsNullOrEmpty(_selectedElement.name)) return;

            Vector2 nt = new Vector2(_selectedElement.resolvedStyle.translate.x, _selectedElement.resolvedStyle.translate.y);
            float t = _timeline != null ? _timeline.Playhead : 0f;

            Undo.RecordObject(_activeClip, "Record Position Key");
            var el = _activeClip.GetOrCreateElement(_selectedElement.name);
            UpsertFloatKey(el.GetOrCreate(AnimatableProperty.TranslateX), t, nt.x);
            UpsertFloatKey(el.GetOrCreate(AnimatableProperty.TranslateY), t, nt.y);
            EditorUtility.SetDirty(_activeClip);

            RebuildPreviewPlayer();   // element track may be new -> re-resolve
            _timeline?.Refresh();
            e.StopPropagation();
        }

        // ---------- HIERARCHY ----------
        void RebuildHierarchy()
        {
            if (_hierarchyScroll == null) return;
            _hierarchyScroll.Clear();
            _treeRowByElement.Clear();

            if (_clonedRoot == null)
            {
                _hierarchyScroll.Add(new Label("Load a scene with a UXML to see its elements.")
                    { style = { marginTop = 12, marginLeft = 8, color = C_Sub, whiteSpace = WhiteSpace.Normal } });
                return;
            }

            // search box
            var search = new ToolbarSearchField { value = _hierarchySearch, style = { marginLeft = 4, marginRight = 4, marginTop = 2, marginBottom = 2 } };
            search.RegisterValueChangedCallback(e => { _hierarchySearch = e.newValue; RebuildHierarchy(); });
            _hierarchyScroll.Add(search);

            if (!string.IsNullOrEmpty(_hierarchySearch))
            {
                // flat, filtered view (ignores fold state)
                foreach (var node in VisualTreeUtil.Flatten(_clonedRoot))
                {
                    if (node.element == _clonedRoot) continue;
                    if (node.displayName.IndexOf(_hierarchySearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    AddHierarchyRow(node.element, 0, false);
                }
            }
            else
            {
                foreach (var child in _clonedRoot.Children())
                    AddRowRecursive(child, 0);
            }
        }

        void AddRowRecursive(VisualElement el, int depth)
        {
            AddHierarchyRow(el, depth, el.childCount > 0);
            if (el.childCount > 0 && IsExpanded(el))
                foreach (var child in el.Children())
                    AddRowRecursive(child, depth + 1);
        }

        bool IsExpanded(VisualElement el) => !_expanded.TryGetValue(el, out var v) || v;

        void AddHierarchyRow(VisualElement el, int depth, bool foldable)
        {
            bool hasName = !string.IsNullOrEmpty(el.name);
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, height = 20,
                          paddingLeft = 4 + depth * 14, alignItems = Align.Center }
            };

            // fold arrow (or spacer)
            if (foldable)
            {
                var arrow = new Label(IsExpanded(el) ? "v" : ">")
                { style = { width = 12, fontSize = 9, color = C_Sub, unityTextAlign = TextAnchor.MiddleCenter } };
                var capt = el;
                arrow.RegisterCallback<MouseDownEvent>(e =>
                {
                    _expanded[capt] = !IsExpanded(capt);
                    RebuildHierarchy();
                    e.StopPropagation();
                });
                row.Add(arrow);
            }
            else
            {
                row.Add(new VisualElement { style = { width = 12 } });
            }

            row.Add(new VisualElement
            {
                style = { width = 6, height = 6, marginRight = 5,
                          backgroundColor = hasName ? new Color(0.5f,0.8f,1f) : new Color(0.45f,0.45f,0.45f),
                          borderTopLeftRadius = 3, borderTopRightRadius = 3,
                          borderBottomLeftRadius = 3, borderBottomRightRadius = 3 }
            });

            row.Add(new Label(VisualTreeUtil.DisplayName(el))
            { style = { flexGrow = 1, fontSize = 11, unityTextAlign = TextAnchor.MiddleLeft,
                        color = hasName ? C_Text : C_Sub } });

            // badges: animated / has particles
            if (hasName && HasAnim(el.name)) row.Add(Badge(new Color(0.45f,0.7f,1f), "A", "Animated in the active clip"));
            if (hasName && HasParticle(el.name)) row.Add(Badge(new Color(0.95f,0.6f,0.3f), "P", "Has particles"));

            var captured = el;
            row.RegisterCallback<MouseDownEvent>(e =>
            {
                SelectElement(captured);
                if (e.clickCount == 2) FrameElement(captured);
                e.StopPropagation();
            });

            _treeRowByElement[el] = row;
            _hierarchyScroll.Add(row);
        }

        static VisualElement Badge(Color color, string letter, string tooltip)
        {
            var b = new VisualElement
            {
                tooltip = tooltip,
                style = { width = 14, height = 14, marginLeft = 3, justifyContent = Justify.Center, alignItems = Align.Center,
                          backgroundColor = new Color(color.r, color.g, color.b, 0.25f),
                          borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3 }
            };
            b.Add(new Label(letter) { style = { fontSize = 9, color = color, unityFontStyleAndWeight = FontStyle.Bold } });
            return b;
        }

        bool HasAnim(string name)
        {
            if (_activeClip == null || string.IsNullOrEmpty(name)) return false;
            var el = _activeClip.elements.Find(e => e.elementName == name);
            return el != null && el.properties.Count > 0;
        }

        bool HasParticle(string name)
        {
            if (_scene == null || string.IsNullOrEmpty(name)) return false;
            return _scene.particles.Exists(p => p.hostElementName == name);
        }

        // ---------- SELECTION ----------
        void SelectElement(VisualElement ve)
        {
            _selectedElement = ve;
            HighlightHierarchy();
            UpdateSelectionOverlay();
            RebuildRightPanel();
        }

        void HighlightHierarchy()
        {
            foreach (var kv in _treeRowByElement)
                kv.Value.style.backgroundColor = (kv.Key == _selectedElement)
                    ? new Color(0.3f, 0.4f, 0.55f) : Color.clear;
        }

        void UpdateSelectionOverlay()
        {
            if (_selectionOverlay == null) return;
            if (_selectedElement == null || _previewHost == null || _selectionOverlay.parent != _previewHost)
            {
                _selectionOverlay.style.display = DisplayStyle.None;
                return;
            }

            // Position the overlay in the host's LOCAL space (the un-scaled canvas
            // coordinates). Because the overlay lives inside the scaled host, it
            // zooms/pans together with the element, so it never drifts.
            Vector2 tl = _selectedElement.ChangeCoordinatesTo(_previewHost, Vector2.zero);
            Vector2 br = _selectedElement.ChangeCoordinatesTo(_previewHost,
                new Vector2(_selectedElement.layout.width, _selectedElement.layout.height));

            _selectionOverlay.style.display = DisplayStyle.Flex;
            _selectionOverlay.style.left = tl.x;
            _selectionOverlay.style.top = tl.y;
            _selectionOverlay.style.width = Mathf.Max(0f, br.x - tl.x);
            _selectionOverlay.style.height = Mathf.Max(0f, br.y - tl.y);

            // keep the border ~2px on screen regardless of zoom
            float bw = 2f / Mathf.Max(0.01f, _previewScale);
            _selectionOverlay.style.borderTopWidth = bw;
            _selectionOverlay.style.borderBottomWidth = bw;
            _selectionOverlay.style.borderLeftWidth = bw;
            _selectionOverlay.style.borderRightWidth = bw;
        }

        // ===============================================================
        //  RIGHT PANEL (tabs)
        // ===============================================================
        void RebuildRightPanel()
        {
            if (_rightPanel == null) return;
            FxStop();   // stop any running particle-designer preview before rebuilding
            _rightPanel.Clear();

            var tabs = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4, marginLeft = 6, marginRight = 6 } };
            for (int i = 0; i < TabNames.Length; i++)
                tabs.Add(TabButton(TabNames[i], i));
            _rightPanel.Add(tabs);

            var body = new ScrollView { style = { flexGrow = 1 } };
            _rightPanel.Add(body);

            switch (_activeTab)
            {
                case 0: BuildSetupTab(body); break;
                case 1: BuildAnimationTab(body); break;
                case 2: BuildParticlesTab(body); break;
                case 3: BuildManageTab(body); break;
            }
        }

        Button TabButton(string label, int index)
        {
            bool active = _activeTab == index;
            var b = new Button(() => { _activeTab = index; EditorPrefs.SetInt(PrefTab, index); RebuildRightPanel(); }) { text = label };
            b.style.flexGrow = 1;
            b.style.height = 26;
            b.style.marginLeft = 0; b.style.marginRight = 0;
            b.style.backgroundColor = active ? C_AccentD : C_Panel;
            b.style.color = active ? Color.white : C_Sub;
            b.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            b.style.borderBottomWidth = active ? 2 : 0;
            b.style.borderBottomColor = C_Accent;
            if (!active)
            {
                b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = new Color(0.22f,0.22f,0.25f));
                b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = C_Panel);
            }
            return b;
        }

        Label SelectedElementBanner()
        {
            string name = _selectedElement != null ? VisualTreeUtil.DisplayName(_selectedElement) : "(none)";
            return new Label($"Selected element:  {name}")
            { style = { marginLeft = 8, marginTop = 8, color = C_Sub, fontSize = 11 } };
        }

        // ----- Setup tab -----
        void BuildSetupTab(VisualElement body)
        {
            if (_scene == null)
            {
                body.Add(new HelpBox("Assign or create a UISceneAnimation asset in the toolbar to begin.", HelpBoxMessageType.Info));
                body.Add(HoverButton("Create New Scene Asset", CreateSceneFlow, C_AccentD, C_Accent));
                return;
            }

            var card = Card();
            card.Add(new Label("SCENE SETUP") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });

            var uxmlField = new ObjectField("UXML Layout") { objectType = typeof(VisualTreeAsset), value = _scene.uxml };
            uxmlField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(_scene, "Set UXML");
                _scene.uxml = e.newValue as VisualTreeAsset;
                EditorUtility.SetDirty(_scene);
                RebuildAll();
            });
            card.Add(uxmlField);

            var sizeField = new Vector2Field("Preview Size") { value = _scene.previewSize };
            sizeField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(_scene, "Preview Size");
                _scene.previewSize = e.newValue;
                EditorUtility.SetDirty(_scene);
                ReloadPreview();
            });
            card.Add(sizeField);

            var bgField = new ColorField("Preview Background") { value = _scene.previewBackground };
            bgField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(_scene, "Preview Background");
                _scene.previewBackground = e.newValue;
                EditorUtility.SetDirty(_scene);
                ReloadPreview();
            });
            card.Add(bgField);
            body.Add(card);

            // Additional style sheets
            var ssCard = Card();
            ssCard.Add(new Label("ADDITIONAL STYLE SHEETS") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });
            for (int i = 0; i < _scene.additionalStyleSheets.Count; i++)
            {
                int idx = i;
                var rowEl = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 } };
                var ssf = new ObjectField { objectType = typeof(StyleSheet), value = _scene.additionalStyleSheets[idx], style = { flexGrow = 1 } };
                ssf.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(_scene, "Edit Style Sheet");
                    _scene.additionalStyleSheets[idx] = e.newValue as StyleSheet;
                    EditorUtility.SetDirty(_scene);
                    ReloadPreview();
                });
                rowEl.Add(ssf);
                rowEl.Add(new Button(() =>
                {
                    Undo.RecordObject(_scene, "Remove Style Sheet");
                    _scene.additionalStyleSheets.RemoveAt(idx);
                    EditorUtility.SetDirty(_scene);
                    RebuildRightPanel(); ReloadPreview();
                }) { text = "x", style = { width = 22 } });
                ssCard.Add(rowEl);
            }
            ssCard.Add(new Button(() =>
            {
                Undo.RecordObject(_scene, "Add Style Sheet");
                _scene.additionalStyleSheets.Add(null);
                EditorUtility.SetDirty(_scene);
                RebuildRightPanel();
            }) { text = "+ Add Style Sheet", style = { marginTop = 4 } });
            body.Add(ssCard);

            // ---- Debug ----
            var dbgCard = Card();
            dbgCard.Add(new Label("DEBUG") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });
            var logToggle = new Toggle("Log (trace runtime in Console)") { value = _scene.debugLog };
            logToggle.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(_scene, "Debug Log");
                _scene.debugLog = e.newValue;
                UILog.Enabled = e.newValue;     // also affects editor previews immediately
                EditorUtility.SetDirty(_scene);
            });
            dbgCard.Add(logToggle);
            dbgCard.Add(new Label("When on, the director, panel host, Play Order and clip player print a step-by-step trace tagged [UIAnim] so you can see exactly where playback goes or stops.")
                { style = { color = C_Sub, fontSize = 10, marginTop = 2, whiteSpace = WhiteSpace.Normal } });
            body.Add(dbgCard);

            body.Add(new HelpBox("Tip: name your elements in UXML/UI Builder. Animations and particles bind by element name.", HelpBoxMessageType.Info));
        }

        // ----- Animation tab -----
        void BuildAnimationTab(VisualElement body)
        {
            if (_scene == null) { body.Add(new HelpBox("Assign a scene asset first (Setup tab).", HelpBoxMessageType.Info)); return; }

            var card = Card();
            card.Add(new Label("CLIP") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });

            var clipRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var clipField = new ObjectField { objectType = typeof(UIAnimationClip), value = _activeClip, style = { flexGrow = 1 } };
            clipField.RegisterValueChangedCallback(e => SetActiveClip(e.newValue as UIAnimationClip));
            clipRow.Add(clipField);
            clipRow.Add(new Button(CreateClipFlow) { text = "New", style = { width = 44 } });
            card.Add(clipRow);

            card.Add(SelectedElementBanner());

            bool hasNamedSelection = _selectedElement != null && !string.IsNullOrEmpty(_selectedElement.name);
            var addTrackBtn = new Button(() =>
            {
                if (_activeClip == null || !hasNamedSelection || _timeline == null) return;
                _timeline.AddElementByName(_selectedElement.name);
            }) { text = hasNamedSelection ? $"Add track for '{_selectedElement.name}'" : "Select a named element to add a track", style = { marginTop = 4 } };
            addTrackBtn.SetEnabled(_activeClip != null && hasNamedSelection);
            card.Add(addTrackBtn);

            if (_selectedElement != null && !hasNamedSelection)
                card.Add(new HelpBox("This element has no name. Give it a name in UXML so it can be animated.", HelpBoxMessageType.Warning));

            // add to the play order (the single place clips are scheduled)
            if (_activeClip != null)
            {
                bool inOrder = _scene.sequence.Exists(s => s.clip == _activeClip);
                var addBtn = HoverButton(inOrder ? "Already in Play Order" : "Add to Play Order",
                    () =>
                    {
                        if (_scene.sequence.Exists(s => s.clip == _activeClip)) return;
                        Undo.RecordObject(_scene, "Add to Play Order");
                        _scene.sequence.Add(new UISceneAnimation.SequenceStep
                        { id = _activeClip.name, clip = _activeClip });
                        EditorUtility.SetDirty(_scene);
                        RebuildRightPanel();
                    }, inOrder ? C_Green : C_AccentD, inOrder ? C_Green : C_Accent);
                addBtn.SetEnabled(!inOrder);
                addBtn.tooltip = "Schedule this clip in Manage > Play Order. For on-demand playback from code use UIAnimation.Play(\"" + _activeClip.name + "\", root).";
                addBtn.style.marginTop = 6; addBtn.style.color = Color.white;
                card.Add(addBtn);
            }
            body.Add(card);

            // Element inspector for the selected, named element.
            if (_activeClip != null && hasNamedSelection)
                body.Add(BuildElementInspectorCard(_selectedElement.name));

            // Preset library (one-click ready-made animations).
            var presetCard = Card();
            presetCard.Add(new Label("ANIMATION PRESETS") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });
            var pRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 } };
            var presetField = new EnumField(_animPresetKind) { style = { flexGrow = 1 } };
            presetField.RegisterValueChangedCallback(e => _animPresetKind = (AnimationPresets.Kind)e.newValue);
            pRow.Add(presetField);
            var applyPreset = HoverButton("Apply", () => ApplyAnimationPreset(_animPresetKind), C_AccentD, C_Accent);
            applyPreset.style.width = 72; applyPreset.style.color = Color.white;
            applyPreset.SetEnabled(hasNamedSelection);
            pRow.Add(applyPreset);
            presetCard.Add(pRow);
            if (!hasNamedSelection)
                presetCard.Add(new Label("Select a named element to apply.") { style = { color = C_Sub, fontSize = 11, marginTop = 2 } });
            body.Add(Fold("Presets", presetCard, false));

            // Record mode toggle.
            var recCard = Card();
            var recRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var recToggle = new Toggle("Record (drag element in preview to key position)") { value = _recording };
            recToggle.RegisterValueChangedCallback(e => _recording = e.newValue);
            recRow.Add(recToggle);
            recCard.Add(recRow);
            recCard.Add(new Label("Shortcuts:  Space play  |  K key snapshot  |  Del remove  |  Arrows scrub  |  Shift+Arrows move key  |  F frame")
                { style = { color = C_Sub, fontSize = 10, marginTop = 2, whiteSpace = WhiteSpace.Normal } });
            body.Add(Fold("Record & shortcuts", recCard, false));

            // Embedded timeline (drives the real preview).
            if (_activeClip == null)
            {
                body.Add(new HelpBox("Assign or create a clip above, or apply a preset. Scrubbing the timeline animates the live preview.", HelpBoxMessageType.Info));
                return;
            }

            // Clip options + timed events.
            var optCard = Card();
            optCard.Add(new Label("CLIP OPTIONS") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });
            var relToggle = new Toggle("Relative (add on top of current pose)") { value = _activeClip.relative };
            relToggle.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(_activeClip, "Relative Mode");
                _activeClip.relative = e.newValue;
                EditorUtility.SetDirty(_activeClip);
                RebuildPreviewPlayer();
            });
            optCard.Add(relToggle);

            // Note: repeat/loop is set per animation in Manage (Scene Bindings &
            // Play Order), so it is intentionally not here to avoid confusion.

            var speedF = new FloatField("Playback Speed") { value = _activeClip.playbackSpeed };
            speedF.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(_activeClip, "Playback Speed");
                _activeClip.playbackSpeed = Mathf.Max(0.01f, e.newValue);
                EditorUtility.SetDirty(_activeClip);
            });
            optCard.Add(speedF);

            optCard.Add(new Label("Events") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text, marginTop = 6 } });
            for (int i = 0; i < _activeClip.events.Count; i++)
            {
                int idx = i;
                var ev = _activeClip.events[idx];
                var evRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 } };
                var tF = new FloatField { value = ev.time, style = { width = 60 } };
                tF.RegisterValueChangedCallback(e => { Undo.RecordObject(_activeClip, "Event Time"); ev.time = Mathf.Max(0f, e.newValue); EditorUtility.SetDirty(_activeClip); });
                evRow.Add(tF);
                var nF = new TextField { value = ev.name, style = { flexGrow = 1 } };
                nF.RegisterValueChangedCallback(e => { Undo.RecordObject(_activeClip, "Event Name"); ev.name = e.newValue; EditorUtility.SetDirty(_activeClip); });
                evRow.Add(nF);
                evRow.Add(new Button(() => { Undo.RecordObject(_activeClip, "Remove Event"); _activeClip.events.RemoveAt(idx); EditorUtility.SetDirty(_activeClip); RebuildRightPanel(); }) { text = "x", style = { width = 22 } });
                optCard.Add(evRow);
            }
            optCard.Add(new Button(() =>
            {
                Undo.RecordObject(_activeClip, "Add Event");
                _activeClip.events.Add(new ClipEvent { time = _timeline != null ? _timeline.Playhead : 0f, name = "event" });
                EditorUtility.SetDirty(_activeClip);
                RebuildRightPanel();
            }) { text = "+ Event at playhead", style = { marginTop = 4 } });
            body.Add(Fold("Clip options & events", optCard, false));

            if (_timeline != null && _timeline.Clip != _activeClip) _timeline.SetClip(_activeClip);

            var dockNote = Card();
            dockNote.Add(new Label("The timeline for this clip is docked at the bottom (full width). Drag the divider above it to resize.")
            { style = { color = C_Sub, fontSize = 11, whiteSpace = WhiteSpace.Normal } });
            body.Add(dockNote);
        }

        // Precise per-element inspector: shows the active clip's tracks for this
        // element with live values, quick-key, remove, and add-property.
        VisualElement BuildElementInspectorCard(string elementName)
        {
            var insp = Card();
            insp.Add(new Label("ELEMENT INSPECTOR") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });
            insp.Add(new Label(VisualTreeUtil.DisplayName(_selectedElement)) { style = { color = C_Sub, fontSize = 11, marginBottom = 2 } });

            var el = _activeClip.elements.Find(e => e.elementName == elementName);
            if (el == null || el.properties.Count == 0)
            {
                insp.Add(new Label("No tracks yet. Add a property below, then key it.") { style = { color = C_Sub, fontSize = 11 } });
            }
            else
            {
                foreach (var pt in el.properties)
                {
                    var capt = pt;
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 } };
                    row.Add(new Label(PropertyMeta.DisplayName(pt.property)) { style = { flexGrow = 1, fontSize = 11 } });
                    row.Add(new Label($"{LiveValueString(pt)}  ({pt.keys.Count})") { style = { width = 96, fontSize = 10, color = C_Sub, unityTextAlign = TextAnchor.MiddleRight } });
                    row.Add(new Button(() => QuickKey(el, capt)) { text = "Key", tooltip = "Key the current value at the playhead", style = { width = 40, fontSize = 10 } });
                    row.Add(new Button(() =>
                    {
                        Undo.RecordObject(_activeClip, "Remove Track");
                        el.properties.Remove(capt);
                        EditorUtility.SetDirty(_activeClip);
                        RebuildPreviewPlayer(); _timeline?.Refresh(); RebuildRightPanel();
                    }) { text = "x", style = { width = 24 } }
                        .SetIcon("Remove track", "TreeEditor.Trash", "d_TreeEditor.Trash"));
                    insp.Add(row);
                }
            }

            insp.Add(new Button(() => ShowAddPropertyMenu(el, elementName)) { text = "+ Property", style = { marginTop = 4 } });
            return insp;
        }

        string LiveValueString(PropertyTrack pt)
        {
            if (_selectedElement == null) return "-";
            if (pt.ValueType == PropertyValueType.Color)
                return "#" + ColorUtility.ToHtmlStringRGB(PropertyBinder.ReadColor(_selectedElement, pt.property));
            return PropertyBinder.ReadFloat(_selectedElement, pt.property).ToString("0.##");
        }

        void QuickKey(ElementTrack el, PropertyTrack pt)
        {
            if (_activeClip == null || _selectedElement == null) return;
            Undo.RecordObject(_activeClip, "Key Property");
            float t = _timeline != null ? _timeline.Playhead : 0f;
            if (pt.ValueType == PropertyValueType.Color)
                UpsertColorKey(pt, t, PropertyBinder.ReadColor(_selectedElement, pt.property));
            else
                UpsertFloatKey(pt, t, PropertyBinder.ReadFloat(_selectedElement, pt.property));
            EditorUtility.SetDirty(_activeClip);
            _timeline?.Refresh(); RebuildRightPanel();
        }

        void ShowAddPropertyMenu(ElementTrack el, string elementName)
        {
            var menu = new GenericMenu();
            foreach (AnimatableProperty p in Enum.GetValues(typeof(AnimatableProperty)))
            {
                bool exists = el != null && el.properties.Exists(x => x.property == p);
                var cap = p;
                if (exists) menu.AddDisabledItem(new GUIContent(PropertyMeta.DisplayName(p)));
                else menu.AddItem(new GUIContent(PropertyMeta.DisplayName(p)), false, () =>
                {
                    Undo.RecordObject(_activeClip, "Add Property");
                    var et = _activeClip.GetOrCreateElement(elementName);
                    et.GetOrCreate(cap);
                    EditorUtility.SetDirty(_activeClip);
                    RebuildPreviewPlayer(); _timeline?.Refresh(); RebuildRightPanel();
                });
            }
            menu.ShowAsContext();
        }

        void EnsureTimeline()
        {
            if (_timeline != null) return;
            _timeline = new TimelineView();
            _timeline.SampleHook = t => _previewClipPlayer?.Sample(t);
            _timeline.StructureChanged = RebuildPreviewPlayer;
            if (_activeClip != null) _timeline.SetClip(_activeClip);
        }

        void SetActiveClip(UIAnimationClip clip)
        {
            _activeClip = clip;
            RebuildPreviewPlayer();
            if (_timeline != null) _timeline.SetClip(clip);
            UpdateTimelineDock();
            RebuildRightPanel();
        }

        // ----- Effects tab: a full particle designer with live preview -----
        void BuildParticlesTab(VisualElement body)
        {
            // ---------------- DESIGNER ----------------
            var dz = Card();
            dz.Add(new Label("PARTICLE DESIGNER") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });

            var crow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var cfgField = new ObjectField { objectType = typeof(ParticleSystemConfig), value = _fxConfig, style = { flexGrow = 1 } };
            cfgField.RegisterValueChangedCallback(e => { _fxConfig = e.newValue as ParticleSystemConfig; RebuildRightPanel(); });
            crow.Add(cfgField);
            crow.Add(new Button(CreateParticleFlow) { text = "New", style = { width = 44 } });
            dz.Add(crow);

            if (_fxConfig == null)
            {
                var qp = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
                var pf = new EnumField(_particlePresetKind) { style = { flexGrow = 1 } };
                pf.RegisterValueChangedCallback(e => _particlePresetKind = (ParticlePresets.Kind)e.newValue);
                qp.Add(pf);
                qp.Add(new Button(() => CreateParticleFromPreset(_particlePresetKind)) { text = "Create", style = { width = 72 } });
                dz.Add(qp);
                dz.Add(new HelpBox("Create a Particle System to design (New), start from a preset, or assign an existing one.", HelpBoxMessageType.Info));
                body.Add(dz);
                return;
            }

            // live preview surface (particles emit from its center)
            _fxPreviewHost = new VisualElement
            {
                style = { height = 200, marginTop = 4, overflow = Overflow.Hidden, position = Position.Relative,
                          backgroundColor = new Color(0.06f, 0.06f, 0.08f),
                          borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4 }
            };
            dz.Add(_fxPreviewHost);

            var ctrl = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            ctrl.Add(new Button(FxPlay) { text = "Play", style = { flexGrow = 1 } }.SetIcon("Play", "Animation.Play", "PlayButton", "d_PlayButton"));
            ctrl.Add(new Button(FxRestart) { text = "Restart", style = { flexGrow = 1 } }.SetIcon("Restart", "Refresh", "d_Refresh"));
            ctrl.Add(new Button(FxStop) { text = "Stop", style = { flexGrow = 1 } });
            ctrl.Add(new Button(() => { Selection.activeObject = _fxConfig; EditorGUIUtility.PingObject(_fxConfig); }) { text = "Ping", style = { width = 44 } }.SetIcon("Ping in Project", "d_Search Icon", "Search Icon"));
            dz.Add(ctrl);
            body.Add(dz);

            // full property editor (emission / visual / bursts / modules) - Unity's
            // own inspector for the asset, embedded right here.
            var inspCard = Card();
            inspCard.Add(new Label("PROPERTIES") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });
            inspCard.Add(new InspectorElement(_fxConfig));
            body.Add(inspCard);

            // start the live preview for this config
            FxPlay();

            // ---------------- BIND TO SCENE ----------------
            if (_scene != null)
            {
                bool hasNamedSelection = _selectedElement != null && !string.IsNullOrEmpty(_selectedElement.name);
                string elementName = hasNamedSelection ? _selectedElement.name : null;

                var bindCard = Card();
                bindCard.Add(new Label("BIND TO SCENE") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });
                bindCard.Add(SelectedElementBanner());

                var bindBtn = HoverButton(hasNamedSelection ? $"Bind to '{elementName}'" : "Select a named host element", () =>
                {
                    if (!hasNamedSelection) return;
                    Undo.RecordObject(_scene, "Bind Particles");
                    _scene.particles.Add(new UISceneAnimation.SceneParticleBinding
                    { id = _fxConfig.name, particleConfig = _fxConfig, hostElementName = elementName, playOnStart = true });
                    EditorUtility.SetDirty(_scene);
                    RebuildRightPanel();
                }, C_AccentD, C_Accent);
                bindBtn.SetEnabled(hasNamedSelection);
                bindBtn.style.color = Color.white; bindBtn.style.marginTop = 4;
                bindCard.Add(bindBtn);

                if (hasNamedSelection)
                {
                    bindCard.Add(new Label($"Bound to '{elementName}':") { style = { color = C_Sub, fontSize = 11, marginTop = 6 } });
                    bool any = false;
                    foreach (var p in _scene.particles)
                    {
                        if (p.hostElementName != elementName) continue;
                        any = true;
                        var rowEl = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 } };
                        rowEl.Add(new Label($"{p.id}") { style = { flexGrow = 1, fontSize = 11 } });
                        var capt = p;
                        rowEl.Add(new Button(() => { Undo.RecordObject(_scene, "Remove Particle Binding"); _scene.particles.Remove(capt); EditorUtility.SetDirty(_scene); RebuildRightPanel(); })
                        { text = "x", style = { width = 24 } }.SetIcon("Remove binding", "TreeEditor.Trash", "d_TreeEditor.Trash"));
                        bindCard.Add(rowEl);
                    }
                    if (!any) bindCard.Add(new Label("None yet.") { style = { color = C_Sub, fontSize = 11 } });
                }
                body.Add(bindCard);
            }
        }

        // ---------- particle designer preview ----------
        void FxPlay()
        {
            FxStop();
            if (_fxConfig == null || _fxPreviewHost == null) return;
            _fxEmitter = new ParticleEmitter(_fxConfig, _fxPreviewHost);
            _fxRunning = true;
            _fxLast = EditorApplication.timeSinceStartup;
            EditorApplication.update += FxTick;
        }

        void FxTick()
        {
            if (!_fxRunning) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= FxTick;
                _fxRunning = false;
                return;
            }
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(0.05f, (float)(now - _fxLast));
            _fxLast = now;
            _fxEmitter?.Update(dt);
            Repaint();
        }

        void FxStop()
        {
            EditorApplication.update -= FxTick;
            _fxEmitter?.Kill();
            _fxEmitter = null;
            _fxRunning = false;
        }

        void FxRestart() => FxPlay();

        void CreateParticleFlow()
        {
            var cfg = ParticlePresets.Create(ParticlePresets.Kind.Sparkle);
            cfg.name = "NewUIParticle";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{EnsureFolderPath(ParticlesFolder)}/{cfg.name}.asset");
            AssetDatabase.CreateAsset(cfg, path);
            AssetDatabase.SaveAssets();
            _fxConfig = cfg;
            RebuildRightPanel();
        }

        void CreateParticleFromPreset(ParticlePresets.Kind kind)
        {
            var cfg = ParticlePresets.Create(kind);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{EnsureFolderPath(ParticlesFolder)}/{cfg.name}.asset");
            AssetDatabase.CreateAsset(cfg, path);
            AssetDatabase.SaveAssets();
            _fxConfig = cfg;
            RebuildRightPanel();
        }

        // ----- Manage tab: library + play order + validation in collapsible sections -----
        void BuildManageTab(VisualElement body)
        {
            if (_scene == null) { body.Add(new HelpBox("Assign a scene asset first (Setup tab).", HelpBoxMessageType.Info)); }

            var lib = new VisualElement();
            BuildLibraryTab(lib);
            body.Add(Fold("Library  (all clips & particles)", lib, true));

            var seqC = new VisualElement();
            BuildSequenceSection(seqC);
            body.Add(Fold("Play Order", seqC, true));

            var trg = new VisualElement();
            BuildTriggersSection(trg);
            body.Add(Fold("Interaction Triggers", trg, false));

            var val = new VisualElement();
            BuildValidateTab(val);
            body.Add(Fold("Validation", val, false));
        }

        // Event-driven triggers: play a clip when an element is clicked/held/hovered.
        void BuildTriggersSection(VisualElement body)
        {
            if (_scene == null) return;

            var card = Card();
            card.Add(new Label("INTERACTION TRIGGERS") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });
            card.Add(new Label("Play a clip when an element is clicked / held / double-clicked / hovered. Pointer events work on both mouse and touch; use Platform for desktop- or mobile-only (e.g. hover is desktop-only).")
                { style = { color = C_Sub, fontSize = 10, marginBottom = 2, whiteSpace = WhiteSpace.Normal } });

            if (_scene.triggers.Count == 0)
                card.Add(new Label("No triggers yet.") { style = { color = C_Sub, fontSize = 11 } });

            for (int i = 0; i < _scene.triggers.Count; i++)
            {
                int idx = i;
                var t = _scene.triggers[idx];
                var row = new VisualElement { style = { marginTop = 4, paddingTop = 4, borderTopWidth = 1, borderTopColor = new Color(0,0,0,0.25f) } };

                var head = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                var elF = new TextField("Element") { value = t.elementName, style = { flexGrow = 1 } };
                elF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Trigger Element"); t.elementName = e.newValue; EditorUtility.SetDirty(_scene); });
                head.Add(elF);
                head.Add(new Button(() => { Undo.RecordObject(_scene, "Remove Trigger"); _scene.triggers.RemoveAt(idx); EditorUtility.SetDirty(_scene); RebuildRightPanel(); }) { text = "x", style = { width = 24 } }
                    .SetIcon("Remove trigger", "TreeEditor.Trash", "d_TreeEditor.Trash"));
                row.Add(head);

                var evF = new EnumField("On Event", t.trigger);
                evF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Trigger Event"); t.trigger = (UITrigger)e.newValue; EditorUtility.SetDirty(_scene); RebuildRightPanel(); });
                row.Add(evF);

                var clipF = new ObjectField("Play Clip") { objectType = typeof(UIAnimationClip), value = t.clip };
                clipF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Trigger Clip"); t.clip = e.newValue as UIAnimationClip; EditorUtility.SetDirty(_scene); });
                row.Add(clipF);

                var platF = new EnumField("Platform", t.platform);
                platF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Trigger Platform"); t.platform = (TriggerPlatform)e.newValue; EditorUtility.SetDirty(_scene); });
                row.Add(platF);

                if (t.trigger == UITrigger.Hold)
                {
                    var holdF = new FloatField("Hold (s)") { value = t.holdSeconds };
                    holdF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Trigger Hold"); t.holdSeconds = Mathf.Max(0.05f, e.newValue); EditorUtility.SetDirty(_scene); });
                    row.Add(holdF);
                }

                var repF = new IntegerField("Repeat (1=once, 0=forever)") { value = t.loops };
                repF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Trigger Repeat"); t.loops = Mathf.Max(0, e.newValue); EditorUtility.SetDirty(_scene); });
                row.Add(repF);

                var ltF = new EnumField("Repeat Type", t.loopType);
                ltF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Trigger Repeat Type"); t.loopType = (LoopType)e.newValue; EditorUtility.SetDirty(_scene); });
                row.Add(ltF);

                var ignoreF = new Toggle("Ignore while playing (anti-spam)") { value = t.ignoreWhilePlaying };
                ignoreF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Trigger Anti-spam"); t.ignoreWhilePlaying = e.newValue; EditorUtility.SetDirty(_scene); });
                row.Add(ignoreF);

                var cdF = new FloatField("Cooldown (s)") { value = t.cooldown };
                cdF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Trigger Cooldown"); t.cooldown = Mathf.Max(0f, e.newValue); EditorUtility.SetDirty(_scene); });
                row.Add(cdF);

                if (_clonedRoot != null && !string.IsNullOrEmpty(t.elementName) && !NameExists(t.elementName))
                    row.Add(new HelpBox("Element not found in UXML: " + t.elementName, HelpBoxMessageType.Warning));

                card.Add(row);
            }

            card.Add(new Button(() =>
            {
                Undo.RecordObject(_scene, "Add Trigger");
                _scene.triggers.Add(new UISceneAnimation.InteractionTrigger
                {
                    elementName = (_selectedElement != null && !string.IsNullOrEmpty(_selectedElement.name)) ? _selectedElement.name : "",
                    clip = _activeClip
                });
                EditorUtility.SetDirty(_scene);
                RebuildRightPanel();
            }) { text = "+ Add Trigger", style = { marginTop = 6 } });

            body.Add(card);
        }

        // Ordered playlist editor: pick clips, set their order, and choose whether
        // each repeats (loops) and whether it waits for the previous step.
        void BuildSequenceSection(VisualElement body)
        {
            if (_scene == null) return;

            var card = Card();
            card.Add(new Label("PLAY ORDER (top to bottom)") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });

            var onStart = new Toggle("Play sequence on start") { value = _scene.playSequenceOnStart };
            onStart.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(_scene, "Sequence On Start");
                _scene.playSequenceOnStart = e.newValue;
                EditorUtility.SetDirty(_scene);
            });
            card.Add(onStart);

            if (_scene.sequence.Count == 0)
                card.Add(new Label("No steps yet. Add clips below and order them.") { style = { color = C_Sub, fontSize = 11, marginTop = 2 } });

            bool afterInfinite = false;   // steps after a "repeat forever" step never play
            for (int i = 0; i < _scene.sequence.Count; i++)
            {
                int idx = i;
                var s = _scene.sequence[idx];
                bool unreachable = afterInfinite;
                bool parallel = !s.waitForPrevious && idx > 0;   // plays together with previous
                var step = new VisualElement { style = { marginTop = 4, paddingTop = 4, borderTopWidth = 1, borderTopColor = new Color(0,0,0,0.25f) } };
                if (parallel)
                {
                    // visually group parallel steps under the one above
                    step.style.paddingLeft = 16;
                    step.style.borderLeftWidth = 2;
                    step.style.borderLeftColor = C_Accent;
                    step.style.borderTopWidth = 0;
                }

                var head = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                head.Add(new Label(parallel ? "+" : $"#{idx + 1}") { tooltip = parallel ? "Plays together with the step above" : null, style = { width = 26, unityFontStyleAndWeight = FontStyle.Bold, color = parallel ? C_Accent : C_Text } });
                var clipF = new ObjectField { objectType = typeof(UIAnimationClip), value = s.clip, style = { flexGrow = 1 } };
                clipF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Step Clip"); s.clip = e.newValue as UIAnimationClip; EditorUtility.SetDirty(_scene); });
                head.Add(clipF);
                head.Add(new Button(() => MoveStep(idx, -1)) { text = "^", tooltip = "Move up", style = { width = 22 } });
                head.Add(new Button(() => MoveStep(idx, 1)) { text = "v", tooltip = "Move down", style = { width = 22 } });
                head.Add(new Button(() => { Undo.RecordObject(_scene, "Remove Step"); _scene.sequence.RemoveAt(idx); EditorUtility.SetDirty(_scene); RebuildRightPanel(); }) { text = "x", style = { width = 24 } }
                    .SetIcon("Remove step", "TreeEditor.Trash", "d_TreeEditor.Trash"));
                step.Add(head);

                // fields that get disabled when the step is unreachable
                var fields = new VisualElement();

                var loopsF = new IntegerField("Repeat (1=once, 0=forever)") { value = s.loops, isDelayed = true };
                loopsF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Step Repeat"); s.loops = Mathf.Max(0, e.newValue); EditorUtility.SetDirty(_scene); RebuildRightPanel(); });
                fields.Add(loopsF);

                var loopTypeF = new EnumField("Repeat Type", s.loopType);
                loopTypeF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Step Repeat Type"); s.loopType = (LoopType)e.newValue; EditorUtility.SetDirty(_scene); });
                fields.Add(loopTypeF);

                var delayF = new FloatField("Delay before (s)") { value = s.delay };
                delayF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Step Delay"); s.delay = Mathf.Max(0f, e.newValue); EditorUtility.SetDirty(_scene); });
                fields.Add(delayF);

                var togetherT = new Toggle("Play together with previous (parallel)") { value = !s.waitForPrevious };
                togetherT.SetEnabled(idx > 0);   // first step has nothing to pair with
                togetherT.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Step Parallel"); s.waitForPrevious = !e.newValue; EditorUtility.SetDirty(_scene); RebuildRightPanel(); });
                fields.Add(togetherT);

                var rootF = new TextField("Root element (optional)") { value = s.rootElementName };
                rootF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Step Root"); s.rootElementName = e.newValue; EditorUtility.SetDirty(_scene); });
                fields.Add(rootF);

                // optional particle on this step (fires at the step's start time)
                fields.Add(new Label("Particle on this step (optional)") { style = { color = C_Sub, fontSize = 10, marginTop = 4 } });
                var partF = new ObjectField("Particle") { objectType = typeof(ParticleSystemConfig), value = s.particle };
                partF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Step Particle"); s.particle = e.newValue as ParticleSystemConfig; EditorUtility.SetDirty(_scene); });
                fields.Add(partF);
                var partHostF = new TextField("Particle Host") { value = s.particleHost };
                partHostF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Step Particle Host"); s.particleHost = e.newValue; EditorUtility.SetDirty(_scene); });
                fields.Add(partHostF);
                var burstF = new IntegerField("Burst (0 = continuous)") { value = s.particleBurst };
                burstF.RegisterValueChangedCallback(e => { Undo.RecordObject(_scene, "Step Burst"); s.particleBurst = Mathf.Max(0, e.newValue); EditorUtility.SetDirty(_scene); });
                fields.Add(burstF);

                step.Add(fields);

                if (unreachable)
                {
                    step.style.opacity = 0.45f;
                    fields.SetEnabled(false);   // head buttons (move/remove) stay usable
                    step.Add(new HelpBox("Disabled: a step above repeats forever, so this never plays.", HelpBoxMessageType.Warning));
                }
                else if (s.clip != null && s.loops == 0)
                {
                    step.Add(new Label("Repeats forever - steps below are disabled.") { style = { color = new Color(0.9f,0.78f,0.3f), fontSize = 10, marginTop = 2, whiteSpace = WhiteSpace.Normal } });
                }

                card.Add(step);

                if (s.clip != null && s.loops == 0) afterInfinite = true;
            }

            var addRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            addRow.Add(new Button(() =>
            {
                Undo.RecordObject(_scene, "Add Clip Step");
                _scene.sequence.Add(new UISceneAnimation.SequenceStep { id = "step" + (_scene.sequence.Count + 1), clip = _activeClip });
                EditorUtility.SetDirty(_scene);
                RebuildRightPanel();
            }) { text = "+ Clip Step", style = { flexGrow = 1 } });
            addRow.Add(new Button(() =>
            {
                Undo.RecordObject(_scene, "Add Particle Step");
                _scene.sequence.Add(new UISceneAnimation.SequenceStep { id = "fx" + (_scene.sequence.Count + 1), particle = _fxConfig, particleBurst = 30 });
                EditorUtility.SetDirty(_scene);
                RebuildRightPanel();
            }) { text = "+ Particle Step", style = { flexGrow = 1 } });
            card.Add(addRow);

            var prevBtn = HoverButton("Preview Sequence", PreviewSequence, C_AccentD, C_Accent);
            prevBtn.style.marginTop = 4; prevBtn.style.color = Color.white;
            card.Add(prevBtn);

            body.Add(card);
        }

        void MoveStep(int idx, int dir)
        {
            int j = idx + dir;
            if (_scene == null || j < 0 || j >= _scene.sequence.Count) return;
            Undo.RecordObject(_scene, "Reorder Step");
            var tmp = _scene.sequence[idx];
            _scene.sequence[idx] = _scene.sequence[j];
            _scene.sequence[j] = tmp;
            EditorUtility.SetDirty(_scene);
            RebuildRightPanel();
        }

        // ----- Library section (every animation / particle asset in the project) -----
        void BuildLibraryTab(VisualElement body)
        {
            var topRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 8, marginRight = 8, marginTop = 6 } };
            topRow.Add(new Label("Filter") { style = { marginRight = 4, color = C_Sub, fontSize = 11 } });
            var searchField = new TextField { value = _librarySearch, isDelayed = true, style = { flexGrow = 1 } };
            searchField.RegisterValueChangedCallback(e => { _librarySearch = e.newValue; RebuildRightPanel(); });
            topRow.Add(searchField);
            topRow.Add(new Button(RebuildRightPanel) { text = "Refresh", style = { width = 28 } }
                .SetIcon("Refresh list", "Refresh", "d_Refresh"));
            body.Add(topRow);

            bool Match(string n) => string.IsNullOrEmpty(_librarySearch)
                || (n != null && n.IndexOf(_librarySearch, StringComparison.OrdinalIgnoreCase) >= 0);

            // ---- animation clips ----
            var clipGuids = AssetDatabase.FindAssets("t:" + nameof(UIAnimationClip));
            var clipCard = Card();
            clipCard.Add(new Label($"ANIMATION CLIPS  ({clipGuids.Length})") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });
            if (clipGuids.Length == 0)
                clipCard.Add(new Label("None yet. Create one in the Animation tab or apply a preset.") { style = { color = C_Sub, fontSize = 11 } });

            foreach (var guid in clipGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<UIAnimationClip>(path);
                if (clip == null || !Match(clip.name)) continue;
                clipCard.Add(BuildClipRow(clip, path));
            }
            body.Add(clipCard);

            // ---- particle presets ----
            var partGuids = AssetDatabase.FindAssets("t:" + nameof(ParticleSystemConfig));
            var partCard = Card();
            partCard.Add(new Label($"PARTICLE PRESETS  ({partGuids.Length})") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });
            if (partGuids.Length == 0)
                partCard.Add(new Label("None yet.") { style = { color = C_Sub, fontSize = 11 } });

            foreach (var guid in partGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var cfg = AssetDatabase.LoadAssetAtPath<ParticleSystemConfig>(path);
                if (cfg == null || !Match(cfg.name)) continue;
                partCard.Add(BuildParticleAssetRow(cfg, path));
            }
            body.Add(partCard);

            body.Add(new HelpBox("Tip: clips under Resources/UIAnimations can be played from code with UIAnimation.Play(\"name\", root). Use \"Copy Call\" to grab the snippet.", HelpBoxMessageType.Info));
        }

        VisualElement BuildClipRow(UIAnimationClip clip, string path)
        {
            var row = new VisualElement { style = { marginTop = 4, paddingTop = 4, borderTopWidth = 1, borderTopColor = new Color(0,0,0,0.25f) } };

            var line = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var elementNames = new List<string>();
            foreach (var et in clip.elements) if (!string.IsNullOrEmpty(et.elementName)) elementNames.Add(et.elementName);
            var nameField = new TextField { value = clip.name, isDelayed = true,
                tooltip = elementNames.Count > 0 ? "Targets: " + string.Join(", ", elementNames) : "No element tracks",
                style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } };
            nameField.RegisterValueChangedCallback(e =>
            {
                string newName = e.newValue;
                if (!string.IsNullOrEmpty(newName) && newName != clip.name)
                {
                    AssetDatabase.RenameAsset(path, newName);
                    AssetDatabase.SaveAssets();
                    RebuildRightPanel();
                }
            });
            line.Add(nameField);
            line.Add(new Label($"{clip.Duration:0.00}s  |  {clip.elements.Count} el{(clip.loop ? "  |  loop" : "")}") { style = { color = C_Sub, fontSize = 10, marginRight = 6 } });
            row.Add(line);

            var btns = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2 } };
            btns.Add(WideButton("Edit", () => { SetActiveClip(clip); _activeTab = TabAnimation; RebuildRightPanel(); }));
            btns.Add(WideButton("Copy Call", () =>
            {
                EditorGUIUtility.systemCopyBuffer = $"UIAnimation.Play(\"{clip.name}\", root);";
                ShowNotification(new GUIContent("Copied: " + clip.name));
            }));
            btns.Add(WideButton("Ping", () => { Selection.activeObject = clip; EditorGUIUtility.PingObject(clip); })
                .SetIcon("Ping in Project", "d_Search Icon", "Search Icon"));
            btns.Add(WideButton("X", () =>
            {
                if (EditorUtility.DisplayDialog("Delete Clip", $"Move '{clip.name}' to the trash?\n{path}", "Delete", "Cancel"))
                { AssetDatabase.MoveAssetToTrash(path); RebuildRightPanel(); }
            }).SetIcon("Delete (to trash)", "TreeEditor.Trash", "d_TreeEditor.Trash"));
            row.Add(btns);
            return row;
        }

        VisualElement BuildParticleAssetRow(ParticleSystemConfig cfg, string path)
        {
            var row = new VisualElement { style = { marginTop = 4, paddingTop = 4, borderTopWidth = 1, borderTopColor = new Color(0,0,0,0.25f) } };

            var line = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            line.Add(new Label(cfg.name) { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } });
            line.Add(new Label($"{(cfg.loop ? "loop" : cfg.duration.ToString("0.0") + "s")}  |  {cfg.maxParticles} max") { style = { color = C_Sub, fontSize = 10, marginRight = 6 } });
            row.Add(line);

            var btns = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2 } };
            bool hasNamedSelection = _selectedElement != null && !string.IsNullOrEmpty(_selectedElement.name);
            var bindBtn = WideButton(hasNamedSelection ? $"Bind to '{_selectedElement.name}'" : "Bind (select host)", () =>
            {
                if (_scene == null || !hasNamedSelection) return;
                Undo.RecordObject(_scene, "Bind Particles");
                _scene.particles.Add(new UISceneAnimation.SceneParticleBinding
                { id = cfg.name, particleConfig = cfg, hostElementName = _selectedElement.name, playOnStart = true });
                EditorUtility.SetDirty(_scene);
                ShowNotification(new GUIContent("Bound: " + cfg.name));
            });
            bindBtn.SetEnabled(hasNamedSelection && _scene != null);
            btns.Add(bindBtn);
            btns.Add(WideButton("Ping", () => { Selection.activeObject = cfg; EditorGUIUtility.PingObject(cfg); })
                .SetIcon("Ping in Project", "d_Search Icon", "Search Icon"));
            btns.Add(WideButton("X", () =>
            {
                if (EditorUtility.DisplayDialog("Delete Preset", $"Move '{cfg.name}' to the trash?\n{path}", "Delete", "Cancel"))
                { AssetDatabase.MoveAssetToTrash(path); RebuildRightPanel(); }
            }).SetIcon("Delete (to trash)", "TreeEditor.Trash", "d_TreeEditor.Trash"));
            row.Add(btns);
            return row;
        }

        static Button WideButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.flexGrow = 1;
            b.style.marginLeft = 0; b.style.marginRight = 2;
            b.style.fontSize = 10;
            return b;
        }

        // ----- Validate tab -----
        void BuildValidateTab(VisualElement body)
        {
            var issues = GatherIssues();
            int errors = 0, warns = 0, infos = 0;
            foreach (var i in issues)
            {
                if (i.severity == ValidationSeverity.Error) errors++;
                else if (i.severity == ValidationSeverity.Warning) warns++;
                else infos++;
            }

            var top = Card();
            top.Add(new Label("VALIDATION") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = C_Text } });
            top.Add(new Label($"{errors} errors   |   {warns} warnings   |   {infos} info")
            { style = { color = errors > 0 ? new Color(0.9f,0.45f,0.45f) : (warns > 0 ? new Color(0.9f,0.78f,0.3f) : new Color(0.5f,0.8f,0.5f)), fontSize = 11, marginTop = 2 } });

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            row.Add(WideButton("Re-validate", RebuildRightPanel));
            row.Add(WideButton("Auto-fix safe issues", AutoFixAll));
            top.Add(row);
            body.Add(top);

            if (issues.Count == 0)
            {
                body.Add(new HelpBox("No issues found. You are good to go.", HelpBoxMessageType.Info));
                return;
            }

            var listCard = Card();
            foreach (var i in issues)
                listCard.Add(BuildIssueRow(i));
            body.Add(listCard);
        }

        VisualElement BuildIssueRow(ValidationIssue issue)
        {
            var r = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexStart, marginTop = 3 } };
            Color dotColor = issue.severity == ValidationSeverity.Error ? new Color(0.85f,0.3f,0.3f)
                : issue.severity == ValidationSeverity.Warning ? new Color(0.9f,0.75f,0.25f)
                : new Color(0.55f,0.55f,0.6f);
            r.Add(new VisualElement { style = { width = 8, height = 8, marginTop = 4, marginRight = 6,
                backgroundColor = dotColor, borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4 } });
            r.Add(new Label(issue.message) { style = { flexGrow = 1, fontSize = 11, whiteSpace = WhiteSpace.Normal } });
            if (issue.target != null)
            {
                var capt = issue.target;
                r.Add(new Button(() => { Selection.activeObject = capt; EditorGUIUtility.PingObject(capt); }) { text = "Ping", style = { width = 46, fontSize = 10 } });
            }
            return r;
        }

        List<ValidationIssue> GatherIssues()
        {
            var issues = new List<ValidationIssue>();
            if (_scene != null) issues.AddRange(UIAnimationValidator.ValidateScene(_scene, _clonedRoot));
            else issues.Add(new ValidationIssue(ValidationSeverity.Info, "No scene assigned (Setup tab)."));

            if (_activeClip != null) issues.AddRange(UIAnimationValidator.ValidateClip(_activeClip));

            // editor-only: duplicate clip asset names make UIAnimation.Play ambiguous
            var guids = AssetDatabase.FindAssets("t:" + nameof(UIAnimationClip));
            var counts = new Dictionary<string, int>();
            foreach (var g in guids)
            {
                var clip = AssetDatabase.LoadAssetAtPath<UIAnimationClip>(AssetDatabase.GUIDToAssetPath(g));
                if (clip == null) continue;
                counts.TryGetValue(clip.name, out int n);
                counts[clip.name] = n + 1;
            }
            foreach (var kv in counts)
                if (kv.Value > 1)
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Duplicate clip name '{kv.Key}' ({kv.Value} assets) - UIAnimation.Play(\"{kv.Key}\") is ambiguous."));

            // library with missing references
            var lib = AssetDatabase.LoadAssetAtPath<UIAnimationLibrary>(LibraryAssetPath);
            if (lib != null)
                foreach (var c in lib.clips)
                    if (c == null)
                    { issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Animation library has a missing (deleted) clip reference.", lib)); break; }

            return issues;
        }

        void AutoFixAll()
        {
            int fixes = 0;
            var clips = new HashSet<UIAnimationClip>();
            if (_activeClip != null) clips.Add(_activeClip);
            if (_scene != null) foreach (var s in _scene.sequence) if (s.clip != null) clips.Add(s.clip);
            foreach (var clip in clips)
            {
                Undo.RecordObject(clip, "Auto-fix Clip");
                int n = UIAnimationValidator.AutoFixClip(clip);
                if (n > 0) { EditorUtility.SetDirty(clip); fixes += n; }
            }

            var cfgs = new HashSet<ParticleSystemConfig>();
            if (_scene != null) foreach (var p in _scene.particles) if (p.particleConfig != null) cfgs.Add(p.particleConfig);
            foreach (var cfg in cfgs)
            {
                Undo.RecordObject(cfg, "Auto-fix Particles");
                int n = UIAnimationValidator.AutoFixParticles(cfg);
                if (n > 0) { EditorUtility.SetDirty(cfg); fixes += n; }
            }

            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent(fixes > 0 ? $"Fixed {fixes} issue(s)" : "Nothing to auto-fix"));
            RebuildPreviewPlayer();
            RebuildRightPanel();
        }

        [MenuItem("Window/UI Toolkit/Validate Selected Scene Animation")]
        static void ValidateSelectedMenu()
        {
            var scene = Selection.activeObject as UISceneAnimation;
            if (scene == null) { Debug.LogWarning("[Validate] Select a UISceneAnimation asset first."); return; }
            var issues = UIAnimationValidator.ValidateScene(scene, null);
            if (issues.Count == 0) { Debug.Log($"[Validate] '{scene.name}': no issues."); return; }
            foreach (var i in issues)
            {
                string msg = $"[Validate] {scene.name}: {i.message}";
                if (i.severity == ValidationSeverity.Error) Debug.LogError(msg, i.target);
                else if (i.severity == ValidationSeverity.Warning) Debug.LogWarning(msg, i.target);
                else Debug.Log(msg, i.target);
            }
        }

        // ===============================================================
        //  SETUP & PLAY
        // ===============================================================
        void SetupAndPlay()
        {
            if (_scene == null)
            {
                EditorUtility.DisplayDialog("Setup & Play", "Assign a scene asset first.", "OK");
                return;
            }

            var director = FindOrCreateDirector();
            if (director == null) return;

            EnsureSomethingPlays();

            Undo.RecordObject(director, "Assign Scene");
            director.scene = _scene;
            EditorUtility.SetDirty(director);
            if (director.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(director.gameObject.scene);

            Selection.activeGameObject = director.gameObject;
            EditorApplication.isPlaying = true;
        }

        // Make sure pressing Setup & Play actually shows something: if nothing is
        // scheduled, add the active clip to the Play Order and enable auto-play.
        void EnsureSomethingPlays()
        {
            if (_scene == null) return;
            bool willPlay =
                (_scene.playSequenceOnStart && _scene.sequence.Exists(s => s.clip != null || s.particle != null))
                || _scene.particles.Exists(p => p.playOnStart && p.particleConfig != null)
                || (_scene.triggers != null && _scene.triggers.Count > 0);
            if (willPlay) return;

            Undo.RecordObject(_scene, "Auto-setup Play");
            if (_activeClip != null && !_scene.sequence.Exists(s => s.clip == _activeClip))
                _scene.sequence.Add(new UISceneAnimation.SequenceStep { id = _activeClip.name, clip = _activeClip });
            _scene.playSequenceOnStart = true;
            EditorUtility.SetDirty(_scene);
            ShowNotification(new GUIContent("Nothing was scheduled - added it to Play Order so it plays."));
        }

        UISceneDirector FindOrCreateDirector()
        {
            // Auto-detects the UI host for the current Unity version:
            // Panel Renderer on 6.5+, UIDocument on older versions.
            var host = UIPanel.FindHost();
            if (host == null)
            {
                EditorUtility.DisplayDialog("Setup & Play",
                    $"No UI host found in the open scene.\n\nAdd a GameObject with a {UIPanel.HostTypeName} (and a PanelSettings asset) that uses this UXML, then press Setup & Play again.",
                    "OK");
                return null;
            }

            // Make sure the host renders this scene's UXML if it has none yet.
            if (UIPanel.GetVisualTree(host) == null && _scene.uxml != null)
                UIPanel.SetVisualTree(host, _scene.uxml);

            var dir = host.GetComponent<UISceneDirector>();
            if (dir == null) dir = Undo.AddComponent<UISceneDirector>(host);
            return dir;
        }

        // ===============================================================
        //  KEYBOARD SHORTCUTS
        // ===============================================================
        void OnKeyDown(KeyDownEvent e)
        {
            if (EditingText()) return;

            if ((e.ctrlKey || e.commandKey) && _timeline != null)
            {
                switch (e.keyCode)
                {
                    case KeyCode.C: _timeline.CopySelection(); e.StopPropagation(); return;
                    case KeyCode.V: _timeline.PasteAtPlayhead(); e.StopPropagation(); return;
                    case KeyCode.D: _timeline.DuplicateSelection(); e.StopPropagation(); return;
                }
            }

            float step = _timeline != null ? _timeline.SnapStepSeconds : 0.1f;

            switch (e.keyCode)
            {
                case KeyCode.Space:
                    _timeline?.TogglePlayPublic();
                    e.StopPropagation();
                    break;
                case KeyCode.K:
                    KeySelectedSnapshot();
                    e.StopPropagation();
                    break;
                case KeyCode.F:
                    FrameSelected();
                    e.StopPropagation();
                    break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    _timeline?.DeleteSelectedKey();
                    e.StopPropagation();
                    break;
                case KeyCode.LeftArrow:
                    if (e.shiftKey && _timeline != null && _timeline.HasSelectedKey) _timeline.NudgeSelectedKey(-step);
                    else _timeline?.NudgePlayhead(-step);
                    e.StopPropagation();
                    break;
                case KeyCode.RightArrow:
                    if (e.shiftKey && _timeline != null && _timeline.HasSelectedKey) _timeline.NudgeSelectedKey(step);
                    else _timeline?.NudgePlayhead(step);
                    e.StopPropagation();
                    break;
            }
        }

        bool EditingText()
        {
            var fe = rootVisualElement.panel?.focusController?.focusedElement as VisualElement;
            if (fe == null) return false;
            if (fe.GetType().Name.Contains("Text")) return true;
            return fe.GetFirstAncestorOfType<TextField>() != null;
        }

        // Snapshot the selected element's current resolved values into keys on its
        // existing property tracks at the playhead (the "K" key).
        void KeySelectedSnapshot()
        {
            if (_activeClip == null || _selectedElement == null || string.IsNullOrEmpty(_selectedElement.name)) return;
            var el = _activeClip.elements.Find(x => x.elementName == _selectedElement.name);
            if (el == null || el.properties.Count == 0) return;

            Undo.RecordObject(_activeClip, "Key Snapshot");
            float t = _timeline != null ? _timeline.Playhead : 0f;
            foreach (var pt in el.properties)
            {
                if (pt.ValueType == PropertyValueType.Color)
                    UpsertColorKey(pt, t, PropertyBinder.ReadColor(_selectedElement, pt.property));
                else
                    UpsertFloatKey(pt, t, PropertyBinder.ReadFloat(_selectedElement, pt.property));
            }
            EditorUtility.SetDirty(_activeClip);
            _timeline?.Refresh();
        }

        static void UpsertFloatKey(PropertyTrack pt, float t, float v)
        {
            var k = pt.keys.Find(x => Mathf.Abs(x.time - t) < 0.001f);
            if (k != null) k.floatValue = v;
            else pt.keys.Add(new UIKeyframe(t, v));
            pt.SortKeys();
        }

        static void UpsertColorKey(PropertyTrack pt, float t, Color v)
        {
            var k = pt.keys.Find(x => Mathf.Abs(x.time - t) < 0.001f);
            if (k != null) k.colorValue = v;
            else pt.keys.Add(new UIKeyframe(t, v));
            pt.SortKeys();
        }

        // ===============================================================
        //  PRESETS
        // ===============================================================
        // Clips live under Resources so UIAnimation.Play(name, root) can load them
        // by name; particles get their own sibling folder.
        const string AnimationsFolder = "Assets/Resources/" + UIAnimation.ResourcesFolder;
        const string ParticlesFolder = "Assets/Resources/UIParticles";

        const string LibraryAssetPath = "Assets/Resources/UIAnimationLibrary.asset";

        // The library asset (in Resources) that lets UIAnimation.Play find clips by
        // name at runtime without putting every clip into Resources.
        static UIAnimationLibrary GetOrCreateLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<UIAnimationLibrary>(LibraryAssetPath);
            if (lib == null)
            {
                EnsureFolderPath("Assets/Resources");
                lib = CreateInstance<UIAnimationLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryAssetPath);
            }
            return lib;
        }

        static void AddClipToLibrary(UIAnimationClip clip)
        {
            if (clip == null) return;
            var lib = GetOrCreateLibrary();
            if (!lib.Contains(clip)) { lib.AddUnique(clip); EditorUtility.SetDirty(lib); }
        }

        // Create every folder in an "Assets/a/b/c" path that does not exist yet.
        static string EnsureFolderPath(string full)
        {
            if (AssetDatabase.IsValidFolder(full)) return full;
            var parts = full.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
            return full;
        }

        void ApplyAnimationPreset(AnimationPresets.Kind kind)
        {
            if (_scene == null) return;
            if (_selectedElement == null || string.IsNullOrEmpty(_selectedElement.name))
            { EditorUtility.DisplayDialog("Preset", "Select a named element first.", "OK"); return; }

            var clip = AnimationPresets.Create(kind, _selectedElement.name);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{EnsureFolderPath(AnimationsFolder)}/{clip.name}.asset");
            AssetDatabase.CreateAsset(clip, path);
            AddClipToLibrary(clip);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(_scene, "Apply Animation Preset");
            _scene.sequence.Add(new UISceneAnimation.SequenceStep { id = clip.name, clip = clip });
            EditorUtility.SetDirty(_scene);
            SetActiveClip(clip);
        }

        void ApplyParticlePreset(ParticlePresets.Kind kind)
        {
            if (_scene == null) return;
            if (_selectedElement == null || string.IsNullOrEmpty(_selectedElement.name))
            { EditorUtility.DisplayDialog("Preset", "Select a named host element first.", "OK"); return; }

            var cfg = ParticlePresets.Create(kind);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{EnsureFolderPath(ParticlesFolder)}/{cfg.name}.asset");
            AssetDatabase.CreateAsset(cfg, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(_scene, "Apply Particle Preset");
            _scene.particles.Add(new UISceneAnimation.SceneParticleBinding
            { id = cfg.name, particleConfig = cfg, hostElementName = _selectedElement.name, playOnStart = true });
            EditorUtility.SetDirty(_scene);
            RebuildRightPanel();
        }

        // ===============================================================
        //  FULL-SCENE LIVE PREVIEW
        // ===============================================================
        void ToggleScenePreview()
        {
            if (_scenePreviewing) StopScenePreview();
            else StartScenePreview();
        }

        void StartScenePreview()
        {
            if (_scene == null) { EditorUtility.DisplayDialog("Preview Scene", "Assign a scene asset first.", "OK"); return; }
            UILog.Enabled = _scene.debugLog;
            StopScenePreview();
            ReloadPreview();
            if (_clonedRoot == null) return;

            // clips play through the ordered Play Order (sequence)
            _previewSequence = UISequenceRunner.Build(_scene, _clonedRoot);
            foreach (var p in _scene.particles)
            {
                if (p.particleConfig == null || !p.playOnStart) continue;
                var host = string.IsNullOrEmpty(p.hostElementName)
                    ? _clonedRoot : (_clonedRoot.Q<VisualElement>(p.hostElementName) ?? _clonedRoot);
                host.style.overflow = Overflow.Hidden;
                _sceneEmitters.Add(new ParticleEmitter(p.particleConfig, host));
            }

            _scenePreviewing = true;
            _scenePrevLast = EditorApplication.timeSinceStartup;
            EditorApplication.update += ScenePreviewTick;
            if (_previewSceneBtn != null) _previewSceneBtn.text = "Stop Preview";
        }

        void ScenePreviewTick()
        {
            if (!_scenePreviewing) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= ScenePreviewTick;
                _scenePreviewing = false; _previewSequence = null;
                return;
            }
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(0.05f, (float)(now - _scenePrevLast));
            _scenePrevLast = now;
            for (int i = 0; i < _scenePlayers.Count; i++) _scenePlayers[i].Update(dt);
            for (int i = 0; i < _sceneEmitters.Count; i++) _sceneEmitters[i].Update(dt);
            _previewSequence?.Update(dt);
            Repaint();
        }

        void StopScenePreview()
        {
            EditorApplication.update -= ScenePreviewTick;
            foreach (var em in _sceneEmitters) em.Kill();
            _sceneEmitters.Clear();
            _scenePlayers.Clear();
            _previewSequence = null;
            bool was = _scenePreviewing;
            _scenePreviewing = false;
            if (_previewSceneBtn != null) _previewSceneBtn.text = "Preview Scene";
            if (was) ReloadPreview();
        }

        // Preview the ordered sequence live in the editor.
        void PreviewSequence()
        {
            if (_scene == null) { EditorUtility.DisplayDialog("Sequence", "Assign a scene asset first.", "OK"); return; }
            UILog.Enabled = _scene.debugLog;
            StopScenePreview();
            ReloadPreview();
            if (_clonedRoot == null) return;
            _previewSequence = UISequenceRunner.Build(_scene, _clonedRoot);
            _scenePreviewing = true;
            _scenePrevLast = EditorApplication.timeSinceStartup;
            EditorApplication.update += ScenePreviewTick;
            if (_previewSceneBtn != null) _previewSceneBtn.text = "Stop Preview";
        }

        void OnDisable()
        {
            EditorApplication.update -= ScenePreviewTick;
            foreach (var em in _sceneEmitters) em.Kill();
            _sceneEmitters.Clear();
            _scenePlayers.Clear();
            _previewSequence = null;
            _scenePreviewing = false;
            FxStop();
        }

        // True when a named element resolves under the loaded preview tree.
        bool NameExists(string name)
        {
            if (_clonedRoot == null || string.IsNullOrEmpty(name)) return false;
            return name == _clonedRoot.name || _clonedRoot.Q<VisualElement>(name) != null;
        }

        // ===============================================================
        //  MISC
        // ===============================================================
        void Update()
        {
            if (_selectedElement != null && !_scenePreviewing) UpdateSelectionOverlay();
        }

        void CreateSceneFlow()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Scene Animation", "NewUIScene", "asset", "");
            if (string.IsNullOrEmpty(path)) return;
            var s = CreateInstance<UISceneAnimation>();
            AssetDatabase.CreateAsset(s, path);
            AssetDatabase.SaveAssets();
            SetScene(s);
        }

        void CreateClipFlow()
        {
            var clip = CreateInstance<UIAnimationClip>();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{EnsureFolderPath(AnimationsFolder)}/NewUIAnimClip.asset");
            AssetDatabase.CreateAsset(clip, path);
            AddClipToLibrary(clip);
            AssetDatabase.SaveAssets();
            SetActiveClip(clip);
        }
    }
}
#endif
