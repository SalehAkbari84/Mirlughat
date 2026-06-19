#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UIToolkit.Animation.Timeline;

namespace UIToolkit.Animation.Editor
{
    // Reusable timeline editor element for a single UIAnimationClip. Hosts a
    // compact transport toolbar, a precise ruler with a draggable playhead,
    // per-element/per-property keyframe lanes, and a keyframe/clip inspector.
    //
    // The host owns the preview and supplies SampleHook so the same timeline can
    // drive either the real UXML preview or a simple box preview. StructureChanged
    // is raised when element tracks are added/removed/renamed so the host can
    // rebuild its ClipPlayer (which resolves elements by name on construction).
    public sealed class TimelineView : VisualElement
    {
        static readonly Color C_Accent = new Color(0.30f, 0.50f, 0.85f);

        // Host hooks.
        public Action<float> SampleHook;     // called with the current time to refresh the host preview
        public Action StructureChanged;      // called after element tracks are added/removed/renamed

        UIAnimationClip _clip;

        // timeline surfaces
        VisualElement _timelineRoot;         // scrollview content host
        ScrollView _timelineScroll;
        VisualElement _inspectorRoot;

        // ruler + playhead overlay
        VisualElement _rulerLane;
        VisualElement _playheadLine;
        VisualElement _playheadHandle;
        Label _playheadTimeBadge;
        Label _hoverTimeBadge;
        FloatField _timeField;
        Label _durationLabel;

        // playback
        bool _playing;
        double _lastTime;
        float _playhead;

        // view
        float _pixelsPerSecond = 140f;
        bool _snap = true;
        float _snapStep = 0.1f;
        const float RulerHeight = 34f;
        const float RowHeight = 26f;
        const float NameColWidth = 160f;
        const float MinViewSeconds = 3f;

        // selection (primary key drives the inspector; _selection is the multi-set)
        ElementTrack _selElement;
        PropertyTrack _selProperty;
        UIKeyframe _selKey;

        struct KeyRef { public ElementTrack el; public PropertyTrack pt; public UIKeyframe key; }
        readonly List<KeyRef> _selection = new List<KeyRef>();
        readonly List<KeyVisual> _keyVisuals = new List<KeyVisual>();
        struct KeyVisual { public VisualElement dia; public KeyRef r; }

        // clipboard is shared across all timeline views in the session
        struct ClipKey { public string elementName; public AnimatableProperty property; public float relTime; public float floatValue; public Color colorValue; public Ease ease; public bool useCurve; public AnimationCurve curve; }
        static readonly List<ClipKey> _clipboard = new List<ClipKey>();

        // marquee (box-select)
        VisualElement _tlHost;
        VisualElement _marquee;
        bool _marqueeActive;
        Vector2 _marqueeStartWorld;
        Vector2 _lastMarqueeWorld;

        public float Playhead => _playhead;
        public UIAnimationClip Clip => _clip;
        public float SnapStepSeconds => _snapStep;
        public bool HasSelectedKey => _selKey != null;

        // ---- public commands (used by host keyboard shortcuts) ----
        public void TogglePlayPublic() => TogglePlay();

        public void DeleteSelectedKey()
        {
            if (_clip == null) return;
            if (_selection.Count == 0 && _selKey == null) return;
            Undo.RecordObject(_clip, "Delete Keyframes");
            if (_selection.Count > 0)
                foreach (var r in _selection) r.pt.keys.Remove(r.key);
            else if (_selProperty != null)
                _selProperty.keys.Remove(_selKey);
            _selection.Clear();
            _selKey = null;
            MarkDirty(); RebuildTimeline(); RebuildInspector(); SampleToPreview();
        }

        // ---- copy / paste / duplicate (used by host shortcuts) ----
        public void CopySelection()
        {
            if (_selection.Count == 0) return;
            float minTime = float.MaxValue;
            foreach (var r in _selection) minTime = Mathf.Min(minTime, r.key.time);
            _clipboard.Clear();
            foreach (var r in _selection)
            {
                _clipboard.Add(new ClipKey
                {
                    elementName = r.el.elementName,
                    property = r.pt.property,
                    relTime = r.key.time - minTime,
                    floatValue = r.key.floatValue,
                    colorValue = r.key.colorValue,
                    ease = r.key.ease,
                    useCurve = r.key.useCurve,
                    curve = r.key.curve != null ? new AnimationCurve(r.key.curve.keys) : null
                });
            }
        }

        public void PasteAtPlayhead()
        {
            if (_clip == null || _clipboard.Count == 0) return;
            PasteFrom(_clipboard, _playhead);
        }

        public void DuplicateSelection()
        {
            if (_selection.Count == 0) return;
            CopySelection();
            float minTime = float.MaxValue;
            foreach (var r in _selection) minTime = Mathf.Min(minTime, r.key.time);
            PasteFrom(_clipboard, minTime + _snapStep);
        }

        void PasteFrom(List<ClipKey> source, float baseTime)
        {
            Undo.RecordObject(_clip, "Paste Keyframes");
            _selection.Clear();
            foreach (var ck in source)
            {
                var el = _clip.GetOrCreateElement(ck.elementName);
                var pt = el.GetOrCreate(ck.property);
                var nk = new UIKeyframe
                {
                    time = Mathf.Max(0f, baseTime + ck.relTime),
                    floatValue = ck.floatValue,
                    colorValue = ck.colorValue,
                    ease = ck.ease,
                    useCurve = ck.useCurve,
                    curve = ck.curve != null ? new AnimationCurve(ck.curve.keys) : new AnimationCurve()
                };
                pt.keys.Add(nk); pt.SortKeys();
                _selection.Add(new KeyRef { el = el, pt = pt, key = nk });
            }
            MarkDirty();
            StructureChanged?.Invoke();
            RebuildTimeline(); RebuildInspector(); SampleToPreview();
        }

        // ---- marquee (box-select) ----
        void BeginMarquee(Vector2 worldPos)
        {
            _marqueeActive = true;
            _marqueeStartWorld = worldPos;
            _lastMarqueeWorld = worldPos;
            if (_marquee == null) return;
            _marquee.style.display = DisplayStyle.Flex;
            var local = _tlHost.WorldToLocal(worldPos);
            _marquee.style.left = local.x; _marquee.style.top = local.y;
            _marquee.style.width = 0; _marquee.style.height = 0;
            _marquee.BringToFront();
        }

        void UpdateMarquee(Vector2 worldPos)
        {
            _lastMarqueeWorld = worldPos;
            if (_marquee == null) return;
            var a = _tlHost.WorldToLocal(_marqueeStartWorld);
            var b = _tlHost.WorldToLocal(worldPos);
            _marquee.style.left = Mathf.Min(a.x, b.x);
            _marquee.style.top = Mathf.Min(a.y, b.y);
            _marquee.style.width = Mathf.Abs(a.x - b.x);
            _marquee.style.height = Mathf.Abs(a.y - b.y);
        }

        void EndMarquee(bool additive)
        {
            _marqueeActive = false;
            if (_marquee != null) _marquee.style.display = DisplayStyle.None;

            // build the world rect from start to last known mouse position
            var rect = Rect.MinMaxRect(
                Mathf.Min(_marqueeStartWorld.x, _lastMarqueeWorld.x),
                Mathf.Min(_marqueeStartWorld.y, _lastMarqueeWorld.y),
                Mathf.Max(_marqueeStartWorld.x, _lastMarqueeWorld.x),
                Mathf.Max(_marqueeStartWorld.y, _lastMarqueeWorld.y));

            if (!additive) _selection.Clear();
            // a click with no drag clears selection
            if (rect.width < 3f && rect.height < 3f)
            {
                _selKey = null;
                RebuildTimeline(); RebuildInspector();
                return;
            }

            foreach (var kv in _keyVisuals)
            {
                if (rect.Overlaps(kv.dia.worldBound) && !SelectionContains(kv.r.key))
                    _selection.Add(kv.r);
            }
            RebuildTimeline(); RebuildInspector();
        }

        // ---- track ease ----
        void ShowTrackEaseMenu(PropertyTrack pt)
        {
            var menu = new GenericMenu();
            foreach (Ease ease in Enum.GetValues(typeof(Ease)))
            {
                var cap = ease;
                menu.AddItem(new GUIContent(ease.ToString()), false, () =>
                {
                    Undo.RecordObject(_clip, "Set Track Ease");
                    foreach (var k in pt.keys) { k.ease = cap; k.useCurve = false; }
                    MarkDirty(); RebuildInspector(); SampleToPreview();
                });
            }
            menu.ShowAsContext();
        }

        public void NudgePlayhead(float delta) => SetPlayhead(_playhead + delta);

        public void NudgeSelectedKey(float delta)
        {
            if (_clip == null || _selKey == null || _selProperty == null) return;
            Undo.RecordObject(_clip, "Move Keyframe");
            _selKey.time = Mathf.Max(0f, _selKey.time + delta);
            _selProperty.SortKeys();
            MarkDirty(); RebuildTimeline(); RebuildInspector();
        }

        public TimelineView()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;
            Build();
            // Self-contained playback tick (runs while attached to a panel).
            schedule.Execute(Tick).Every(16);
        }

        public void SetClip(UIAnimationClip clip)
        {
            _clip = clip;
            _selElement = null; _selProperty = null; _selKey = null;
            _selection.Clear();
            _playhead = 0f; _playing = false;
            RebuildAll();
        }

        public void Refresh() => RebuildAll();

        // Used by hosts that edit keys outside the timeline (e.g. dragging a
        // preview box). Writes/updates a float key without sorting side effects.
        public void RecordFloatKey(ElementTrack el, AnimatableProperty prop, float time, float value)
        {
            if (_clip == null || el == null) return;
            var pt = el.GetOrCreate(prop);
            UIKeyframe existing = pt.keys.Find(k => Mathf.Abs(k.time - time) < 0.001f);
            if (existing != null) existing.floatValue = value;
            else pt.keys.Add(new UIKeyframe(time, value));
            pt.SortKeys();
        }

        // ===============================================================
        //  CONSTRUCTION
        // ===============================================================
        void Build()
        {
            BuildToolbar(this);

            var vsplit = new TwoPaneSplitView(1, 180, TwoPaneSplitViewOrientation.Vertical) { style = { flexGrow = 1 } };
            Add(vsplit);

            var timelineContainer = new VisualElement { style = { flexGrow = 1 } };
            timelineContainer.Add(SectionHeader("TIMELINE"));

            _tlHost = new VisualElement { style = { flexGrow = 1, position = Position.Relative } };
            _timelineScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { style = { flexGrow = 1 } };
            _tlHost.Add(_timelineScroll);
            _timelineRoot = _timelineScroll.contentContainer;

            _playheadLine = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, width = 2, top = RulerHeight, bottom = 0,
                          backgroundColor = new Color(1f, 0.32f, 0.32f) }
            };
            _tlHost.Add(_playheadLine);

            _marquee = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, display = DisplayStyle.None,
                          backgroundColor = new Color(0.3f, 0.5f, 0.85f, 0.15f),
                          borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                          borderTopColor = C_Accent, borderBottomColor = C_Accent,
                          borderLeftColor = C_Accent, borderRightColor = C_Accent }
            };
            _tlHost.Add(_marquee);

            _hoverTimeBadge = MakeBadge(new Color(0.1f, 0.1f, 0.1f, 0.92f));
            _hoverTimeBadge.style.display = DisplayStyle.None;
            _tlHost.Add(_hoverTimeBadge);

            _timelineScroll.horizontalScroller.valueChanged += _ => UpdatePlayheadVisual();
            _timelineScroll.RegisterCallback<GeometryChangedEvent>(_ => UpdatePlayheadVisual());

            timelineContainer.Add(_tlHost);
            vsplit.Add(timelineContainer);

            _inspectorRoot = new ScrollView { style = { flexGrow = 1, minHeight = 90 } };
            vsplit.Add(_inspectorRoot);

            RebuildAll();
        }

        static Label SectionHeader(string text)
        {
            return new Label(text)
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 6, marginTop = 4,
                          marginBottom = 2, color = new Color(0.8f, 0.8f, 0.85f), fontSize = 11 }
            };
        }

        static Label MakeBadge(Color bg)
        {
            return new Label
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute, paddingLeft = 5, paddingRight = 5,
                    paddingTop = 1, paddingBottom = 1, fontSize = 10, color = Color.white,
                    backgroundColor = bg,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3, borderBottomRightRadius = 3
                }
            };
        }

        void BuildToolbar(VisualElement root)
        {
            var bar = new Toolbar();

            var toStart = new ToolbarButton(() => SetPlayhead(0f)) { text = "|<" };
            toStart.tooltip = "Go to start";
            bar.Add(toStart);

            bar.Add(new ToolbarButton(TogglePlay) { text = "Play", style = { width = 50 } });

            var toEnd = new ToolbarButton(() => SetPlayhead(_clip != null ? _clip.Duration : 0f)) { text = ">|" };
            toEnd.tooltip = "Go to end";
            bar.Add(toEnd);

            bar.Add(new ToolbarSpacer());

            bar.Add(new Label("Time") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 4, marginRight = 2 } });
            _timeField = new FloatField { value = 0f, style = { width = 60 } };
            _timeField.RegisterValueChangedCallback(e => SetPlayhead(Mathf.Max(0f, e.newValue)));
            bar.Add(_timeField);
            _durationLabel = new Label("/ 0.00s") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 4 } };
            bar.Add(_durationLabel);

            bar.Add(new ToolbarSpacer());

            var snapToggle = new ToolbarToggle { text = "Snap", value = _snap };
            snapToggle.RegisterValueChangedCallback(e => _snap = e.newValue);
            bar.Add(snapToggle);

            var snapStep = new EnumField(SnapStepEnum.S0_1) { style = { width = 70 } };
            snapStep.RegisterValueChangedCallback(e => _snapStep = StepValue((SnapStepEnum)e.newValue));
            bar.Add(snapStep);

            bar.Add(new ToolbarSpacer());

            bar.Add(new Label("Zoom") { style = { unityTextAlign = TextAnchor.MiddleCenter, marginLeft = 4 } });
            var zoom = new Slider(40f, 500f) { value = _pixelsPerSecond, style = { width = 110 } };
            zoom.RegisterValueChangedCallback(e => { _pixelsPerSecond = e.newValue; RebuildTimeline(); });
            bar.Add(zoom);

            bar.Add(new ToolbarSpacer());
            bar.Add(new ToolbarButton(AddElementDialog) { text = "+ Element" });

            root.Add(bar);
        }

        enum SnapStepEnum { S0_05, S0_1, S0_25, S0_5, S1_0 }
        static float StepValue(SnapStepEnum s)
        {
            switch (s)
            {
                case SnapStepEnum.S0_05: return 0.05f;
                case SnapStepEnum.S0_1:  return 0.1f;
                case SnapStepEnum.S0_25: return 0.25f;
                case SnapStepEnum.S0_5:  return 0.5f;
                default: return 1f;
            }
        }

        float Snap(float t) => _snap ? Mathf.Round(t / _snapStep) * _snapStep : t;

        // ===============================================================
        //  REBUILD
        // ===============================================================
        void RebuildAll()
        {
            if (_timelineRoot == null) return;
            RebuildTimeline();
            RebuildInspector();
            UpdateTimeReadouts();
            SampleToPreview();
        }

        // ---------- TIMELINE ----------
        void RebuildTimeline()
        {
            if (_timelineRoot == null) return;
            _timelineRoot.Clear();
            _keyVisuals.Clear();

            if (_clip == null)
            {
                _timelineRoot.Add(new Label("No clip. Create or assign one to start editing.")
                    { style = { marginTop = 20, marginLeft = 10, color = new Color(0.7f, 0.7f, 0.7f) } });
                UpdatePlayheadVisual();
                return;
            }

            float viewSeconds = Mathf.Max(_clip.Duration + 1f, MinViewSeconds);
            float laneWidth = NameColWidth + viewSeconds * _pixelsPerSecond + 40f;

            var content = new VisualElement { style = { minWidth = laneWidth } };
            content.Add(BuildRuler(viewSeconds));

            foreach (var el in _clip.elements)
                content.Add(BuildElementRow(el, viewSeconds));

            _timelineRoot.Add(content);
            UpdatePlayheadVisual();
        }

        VisualElement BuildRuler(float viewSeconds)
        {
            var ruler = new VisualElement
            {
                style = { height = RulerHeight, flexDirection = FlexDirection.Row,
                          backgroundColor = new Color(0.16f, 0.16f, 0.18f),
                          borderBottomWidth = 1, borderBottomColor = new Color(0,0,0,0.4f) }
            };

            var spacer = new VisualElement { style = { width = NameColWidth, justifyContent = Justify.Center } };
            spacer.Add(new Label("  Tracks") { style = { fontSize = 10, color = new Color(0.6f,0.6f,0.6f) } });
            ruler.Add(spacer);

            _rulerLane = new VisualElement { style = { flexGrow = 1, position = Position.Relative } };

            int majors = Mathf.CeilToInt(viewSeconds) + 1;
            for (int s = 0; s < majors; s++)
            {
                float xMajor = s * _pixelsPerSecond;

                _rulerLane.Add(new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, left = xMajor, top = 0, width = 1, height = RulerHeight,
                              backgroundColor = new Color(1,1,1,0.35f) }
                });

                _rulerLane.Add(new Label(FormatTime(s))
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, left = xMajor + 3, top = 2, fontSize = 10,
                              color = new Color(0.85f,0.85f,0.85f) }
                });

                for (int m = 1; m < 10; m++)
                {
                    float xm = xMajor + m * (_pixelsPerSecond / 10f);
                    bool half = (m == 5);
                    _rulerLane.Add(new VisualElement
                    {
                        pickingMode = PickingMode.Ignore,
                        style = { position = Position.Absolute, left = xm, bottom = 0, width = 1,
                                  height = half ? 12 : 7, backgroundColor = new Color(1,1,1, half ? 0.25f : 0.13f) }
                    });
                }
            }

            _rulerLane.RegisterCallback<MouseDownEvent>(e =>
            {
                _playing = false;
                SetPlayhead(Snap(e.localMousePosition.x / _pixelsPerSecond));
                _rulerLane.CaptureMouse();
                e.StopPropagation();
            });
            _rulerLane.RegisterCallback<MouseMoveEvent>(e =>
            {
                if ((e.pressedButtons & 1) != 0)
                    SetPlayhead(Snap(e.localMousePosition.x / _pixelsPerSecond));
            });
            _rulerLane.RegisterCallback<MouseUpEvent>(e => { if (_rulerLane.HasMouseCapture()) _rulerLane.ReleaseMouse(); });

            _playheadHandle = new VisualElement
            {
                style = { position = Position.Absolute, top = 0, width = 13, height = RulerHeight,
                          marginLeft = -6, backgroundColor = Color.clear }
            };
            _playheadHandle.Add(new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, top = 2, left = 1, width = 11, height = 11,
                          backgroundColor = new Color(1f,0.32f,0.32f),
                          rotate = new Rotate(new Angle(45, AngleUnit.Degree)) }
            });

            _playheadTimeBadge = MakeBadge(new Color(0.75f,0.18f,0.18f,0.95f));
            _playheadTimeBadge.style.top = 16;
            _playheadTimeBadge.style.left = -10;
            _playheadHandle.Add(_playheadTimeBadge);

            bool dragHandle = false;
            _playheadHandle.RegisterCallback<MouseDownEvent>(e =>
            {
                dragHandle = true; _playing = false; _playheadHandle.CaptureMouse(); e.StopPropagation();
            });
            _playheadHandle.RegisterCallback<MouseMoveEvent>(e =>
            {
                if (!dragHandle) return;
                float x = _rulerLane.WorldToLocal(e.mousePosition).x;
                SetPlayhead(Snap(x / _pixelsPerSecond));
                e.StopPropagation();
            });
            _playheadHandle.RegisterCallback<MouseUpEvent>(e =>
            {
                if (!dragHandle) return; dragHandle = false; _playheadHandle.ReleaseMouse(); e.StopPropagation();
            });
            _rulerLane.Add(_playheadHandle);

            ruler.Add(_rulerLane);
            return ruler;
        }

        VisualElement BuildElementRow(ElementTrack el, float viewSeconds)
        {
            var wrap = new VisualElement();

            var head = new VisualElement
            {
                style = { height = RowHeight, flexDirection = FlexDirection.Row,
                          backgroundColor = new Color(0.23f, 0.26f, 0.3f),
                          borderBottomWidth = 1, borderBottomColor = new Color(0,0,0,0.25f) }
            };
            head.Add(new Button(() => { el.expanded = !el.expanded; RebuildTimeline(); })
            { text = el.expanded ? "v" : ">", style = { width = 22, marginTop = 0, marginBottom = 0 } });

            var nameField = new TextField { value = el.elementName, style = { width = NameColWidth - 90, marginTop = 3 } };
            nameField.RegisterValueChangedCallback(e =>
            {
                el.elementName = e.newValue; MarkDirty();
                StructureChanged?.Invoke();
            });
            head.Add(nameField);

            head.Add(new Button(() => ShowAddPropertyMenu(el)) { text = "+", tooltip = "Add property track", style = { width = 22 } });
            head.Add(new Button(() =>
            {
                _clip.elements.Remove(el); MarkDirty();
                StructureChanged?.Invoke();
                RebuildAll();
            }) { text = "x", tooltip = "Remove element", style = { width = 22 } });
            wrap.Add(head);

            if (el.expanded)
                foreach (var pt in el.properties)
                    wrap.Add(BuildPropertyRow(el, pt, viewSeconds));

            return wrap;
        }

        VisualElement BuildPropertyRow(ElementTrack el, PropertyTrack pt, float viewSeconds)
        {
            var row = new VisualElement
            {
                style = { height = RowHeight, flexDirection = FlexDirection.Row,
                          backgroundColor = new Color(0.15f, 0.15f, 0.16f),
                          borderBottomWidth = 1, borderBottomColor = new Color(0,0,0,0.18f) }
            };

            row.Add(new Label("    " + PropertyMeta.DisplayName(pt.property))
            { style = { width = NameColWidth - 48, unityTextAlign = TextAnchor.MiddleLeft, fontSize = 11 } });
            row.Add(new Button(() => ShowTrackEaseMenu(pt)) { text = "E", tooltip = "Set ease for all keys in this track", style = { width = 22 } });
            row.Add(new Button(() => AddKeyAtPlayhead(el, pt)) { text = "+", tooltip = "Add key at playhead", style = { width = 22 } });

            var lane = new VisualElement { style = { flexGrow = 1, position = Position.Relative } };

            int majors = Mathf.CeilToInt(viewSeconds) + 1;
            for (int s = 0; s < majors; s++)
            {
                lane.Add(new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, left = s * _pixelsPerSecond, top = 0, bottom = 0,
                              width = 1, backgroundColor = new Color(1,1,1,0.05f) }
                });
            }

            pt.SortKeys();

            for (int i = 0; i < pt.keys.Count - 1; i++)
            {
                float x1 = pt.keys[i].time * _pixelsPerSecond;
                float x2 = pt.keys[i + 1].time * _pixelsPerSecond;
                lane.Add(new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, left = x1, width = Mathf.Max(0, x2 - x1),
                              top = RowHeight / 2 - 1, height = 2, backgroundColor = new Color(0.5f,0.7f,1f,0.4f) }
                });
            }

            foreach (var k in pt.keys)
                lane.Add(BuildKeyDiamond(el, pt, k));

            lane.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button != 0) return;
                if (e.clickCount == 2)
                {
                    _marqueeActive = false;
                    if (_marquee != null) _marquee.style.display = DisplayStyle.None;
                    AddKeyAtTime(el, pt, Snap(e.localMousePosition.x / _pixelsPerSecond));
                    e.StopPropagation();
                    return;
                }
                BeginMarquee(e.mousePosition);
                lane.CaptureMouse();
                e.StopPropagation();
            });
            lane.RegisterCallback<MouseMoveEvent>(e => { if (_marqueeActive) UpdateMarquee(e.mousePosition); });
            lane.RegisterCallback<MouseUpEvent>(e =>
            {
                if (!_marqueeActive) return;
                EndMarquee(e.shiftKey);
                if (lane.HasMouseCapture()) lane.ReleaseMouse();
                e.StopPropagation();
            });

            row.Add(lane);
            return row;
        }

        VisualElement BuildKeyDiamond(ElementTrack el, PropertyTrack pt, UIKeyframe k)
        {
            bool selected = (k == _selKey) || SelectionContains(k);
            var dia = new VisualElement
            {
                tooltip = $"t = {FormatTime(k.time)}",
                style =
                {
                    position = Position.Absolute, width = 12, height = 12,
                    left = k.time * _pixelsPerSecond - 6, top = RowHeight / 2 - 6,
                    rotate = new Rotate(new Angle(45, AngleUnit.Degree)),
                    backgroundColor = selected ? Color.yellow : new Color(0.55f, 0.78f, 1f)
                }
            };
            _keyVisuals.Add(new KeyVisual { dia = dia, r = new KeyRef { el = el, pt = pt, key = k } });

            bool dragging = false;
            float dragStartTime = 0f;
            dia.RegisterCallback<MouseDownEvent>(e =>
            {
                _selElement = el; _selProperty = pt; _selKey = k;
                if (e.button == 1)
                {
                    pt.keys.Remove(k);
                    _selection.RemoveAll(r => r.key == k);
                    _selKey = null;
                    MarkDirty(); RebuildTimeline(); RebuildInspector(); SampleToPreview();
                    e.StopPropagation();
                    return;
                }

                bool additive = e.ctrlKey || e.commandKey || e.shiftKey;
                if (additive)
                {
                    if (SelectionContains(k)) _selection.RemoveAll(r => r.key == k);
                    else _selection.Add(new KeyRef { el = el, pt = pt, key = k });
                }
                else if (!SelectionContains(k))
                {
                    _selection.Clear();
                    _selection.Add(new KeyRef { el = el, pt = pt, key = k });
                }

                dragging = true; dragStartTime = k.time; dia.CaptureMouse();
                RebuildTimeline(); RebuildInspector();
                ShowHoverBadge(k.time);
                e.StopPropagation();
            });
            dia.RegisterCallback<MouseMoveEvent>(e =>
            {
                if (!dragging) return;
                float local = dia.parent.WorldToLocal(e.mousePosition).x;
                float t = Snap(Mathf.Max(0f, local / _pixelsPerSecond));
                k.time = t;
                dia.style.left = t * _pixelsPerSecond - 6;
                dia.tooltip = $"t = {FormatTime(t)}";
                ShowHoverBadge(t);
                MarkDirty();
                e.StopPropagation();
            });
            dia.RegisterCallback<MouseUpEvent>(e =>
            {
                if (!dragging) return;
                dragging = false; dia.ReleaseMouse();

                // apply the same time delta to the rest of the selection (group move)
                float delta = k.time - dragStartTime;
                if (Mathf.Abs(delta) > 0.00001f)
                {
                    foreach (var r in _selection)
                    {
                        if (r.key == k) continue;
                        r.key.time = Mathf.Max(0f, r.key.time + delta);
                        r.pt.SortKeys();
                    }
                }
                pt.SortKeys(); HideHoverBadge();
                RebuildTimeline(); RebuildInspector(); SampleToPreview();
                e.StopPropagation();
            });
            return dia;
        }

        bool SelectionContains(UIKeyframe k) => _selection.Exists(r => r.key == k);

        void ShowHoverBadge(float t)
        {
            if (_hoverTimeBadge == null) return;
            _hoverTimeBadge.text = FormatTime(t);
            _hoverTimeBadge.style.display = DisplayStyle.Flex;
            float scrollX = _timelineScroll != null ? _timelineScroll.scrollOffset.x : 0f;
            _hoverTimeBadge.style.left = NameColWidth + t * _pixelsPerSecond - scrollX + 6;
            _hoverTimeBadge.style.top = RulerHeight + 2;
            _hoverTimeBadge.BringToFront();
        }
        void HideHoverBadge() { if (_hoverTimeBadge != null) _hoverTimeBadge.style.display = DisplayStyle.None; }

        // ---------- PLAYHEAD VISUAL ----------
        void UpdatePlayheadVisual()
        {
            float scrollX = _timelineScroll != null ? _timelineScroll.scrollOffset.x : 0f;
            float xInLane = _playhead * _pixelsPerSecond;
            float screenX = NameColWidth + xInLane - scrollX;

            if (_playheadLine != null)
            {
                _playheadLine.style.left = screenX;
                _playheadLine.BringToFront();
            }
            if (_playheadHandle != null)
                _playheadHandle.style.left = xInLane;
            if (_playheadTimeBadge != null)
                _playheadTimeBadge.text = FormatTime(_playhead);
        }

        // ---------- INSPECTOR ----------
        void RebuildInspector()
        {
            if (_inspectorRoot == null) return;
            _inspectorRoot.Clear();

            // Clip-wide settings (loop / speed) now live in the Animation tab under
            // "Clip options & events". This inspector shows the selected keyframe(s).

            // Multi-select: set ease/curve for ALL selected keys at once.
            if (_selection.Count > 1)
            {
                var multi = new VisualElement { style = { marginLeft = 6, marginTop = 4, marginBottom = 6 } };
                multi.Add(new Label($"SELECTED KEYS ({_selection.Count})") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

                var easeAll = new EnumField("Ease (all)", _selKey != null ? _selKey.ease : Ease.OutQuad);
                easeAll.RegisterValueChangedCallback(e =>
                {
                    var ease = (Ease)e.newValue;
                    Undo.RecordObject(_clip, "Set Ease (selected)");
                    foreach (var r in _selection) { r.key.ease = ease; r.key.useCurve = false; }
                    MarkDirty(); RebuildTimeline(); SampleToPreview();
                });
                multi.Add(easeAll);

                var curveAll = new CurveField("Curve (all)") { value = _selKey != null ? _selKey.curve : AnimationCurve.Linear(0, 0, 1, 1) };
                curveAll.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(_clip, "Set Curve (selected)");
                    foreach (var r in _selection) { r.key.curve = new AnimationCurve(e.newValue.keys); r.key.useCurve = true; }
                    MarkDirty(); RebuildTimeline(); SampleToPreview();
                });
                multi.Add(curveAll);

                _inspectorRoot.Add(multi);
            }

            if (_selKey == null || _selProperty == null) return;

            var box = new VisualElement { style = { marginLeft = 6, marginTop = 4 } };
            box.Add(new Label($"KEYFRAME  -  {_selElement.elementName} / {PropertyMeta.DisplayName(_selProperty.property)}")
                { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            var timeF = new FloatField("Time (s)") { value = _selKey.time };
            timeF.RegisterValueChangedCallback(e =>
            {
                _selKey.time = Mathf.Max(0f, e.newValue); _selProperty.SortKeys();
                MarkDirty(); RebuildTimeline();
            });
            box.Add(timeF);

            if (_selProperty.ValueType == PropertyValueType.Color)
            {
                var col = new ColorField("Value") { value = _selKey.colorValue };
                col.RegisterValueChangedCallback(e => { _selKey.colorValue = e.newValue; MarkDirty(); SampleToPreview(); });
                box.Add(col);
            }
            else
            {
                var val = new FloatField("Value") { value = _selKey.floatValue };
                val.RegisterValueChangedCallback(e => { _selKey.floatValue = e.newValue; MarkDirty(); SampleToPreview(); });
                box.Add(val);
            }

            var useCurve = new Toggle("Use Custom Curve") { value = _selKey.useCurve };
            box.Add(useCurve);

            var easeGraph = BuildEaseGraph();

            var easeF = new EnumField("Ease", _selKey.ease);
            easeF.style.display = _selKey.useCurve ? DisplayStyle.None : DisplayStyle.Flex;
            easeF.RegisterValueChangedCallback(e => { _selKey.ease = (Ease)e.newValue; MarkDirty(); SampleToPreview(); easeGraph.MarkDirtyRepaint(); });
            box.Add(easeF);

            var curveF = new CurveField("Curve") { value = _selKey.curve };
            curveF.style.display = _selKey.useCurve ? DisplayStyle.Flex : DisplayStyle.None;
            curveF.RegisterValueChangedCallback(e => { _selKey.curve = e.newValue; MarkDirty(); SampleToPreview(); easeGraph.MarkDirtyRepaint(); });
            box.Add(curveF);

            box.Add(easeGraph);

            useCurve.RegisterValueChangedCallback(e =>
            {
                _selKey.useCurve = e.newValue;
                easeF.style.display = e.newValue ? DisplayStyle.None : DisplayStyle.Flex;
                curveF.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                MarkDirty(); easeGraph.MarkDirtyRepaint();
            });

            _inspectorRoot.Add(box);
        }

        // Mini plot of the selected key's ease/curve (output vs normalized time).
        VisualElement BuildEaseGraph()
        {
            var g = new VisualElement
            {
                style = { height = 64, marginTop = 4, marginBottom = 2,
                          backgroundColor = new Color(0.1f, 0.1f, 0.12f),
                          borderTopLeftRadius = 3, borderTopRightRadius = 3,
                          borderBottomLeftRadius = 3, borderBottomRightRadius = 3 }
            };
            g.generateVisualContent += mgc =>
            {
                if (_selKey == null) return;
                var rect = g.contentRect;
                float w = rect.width, h = rect.height;
                if (w < 4f || h < 4f) return;

                var p2d = mgc.painter2D;
                const float pad = 5f;

                p2d.strokeColor = new Color(1f, 1f, 1f, 0.12f);
                p2d.lineWidth = 1f;
                p2d.BeginPath();
                p2d.MoveTo(new Vector2(0f, h - pad));
                p2d.LineTo(new Vector2(w, h - pad));
                p2d.Stroke();

                p2d.strokeColor = new Color(0.55f, 0.78f, 1f);
                p2d.lineWidth = 2f;
                p2d.BeginPath();
                int n = 40;
                for (int i = 0; i <= n; i++)
                {
                    float x = i / (float)n;
                    float y = _selKey.useCurve ? Easing.Evaluate(_selKey.curve, x) : Easing.Evaluate(_selKey.ease, x);
                    float px = x * w;
                    float py = pad + (1f - y) * (h - 2f * pad);
                    if (i == 0) p2d.MoveTo(new Vector2(px, py));
                    else p2d.LineTo(new Vector2(px, py));
                }
                p2d.Stroke();
            };
            return g;
        }

        // ===============================================================
        //  EDITING HELPERS
        // ===============================================================
        void AddElementDialog()
        {
            if (_clip == null) return;
            Undo.RecordObject(_clip, "Add Element");
            _clip.GetOrCreateElement("element" + (_clip.elements.Count + 1));
            MarkDirty();
            StructureChanged?.Invoke();
            RebuildAll();
        }

        // Add (or focus) a track for a specific named element, then add nothing
        // else. Used by hosts that already know the element name.
        public void AddElementByName(string elementName)
        {
            if (_clip == null || string.IsNullOrEmpty(elementName)) return;
            Undo.RecordObject(_clip, "Add Element Track");
            _clip.GetOrCreateElement(elementName);
            MarkDirty();
            StructureChanged?.Invoke();
            RebuildAll();
        }

        void ShowAddPropertyMenu(ElementTrack el)
        {
            var menu = new GenericMenu();
            foreach (AnimatableProperty p in Enum.GetValues(typeof(AnimatableProperty)))
            {
                bool exists = el.properties.Exists(x => x.property == p);
                var cap = p;
                if (exists) menu.AddDisabledItem(new GUIContent(PropertyMeta.DisplayName(p)));
                else menu.AddItem(new GUIContent(PropertyMeta.DisplayName(p)), false, () =>
                {
                    Undo.RecordObject(_clip, "Add Property");
                    el.GetOrCreate(cap);
                    MarkDirty(); RebuildTimeline();
                });
            }
            menu.ShowAsContext();
        }

        void AddKeyAtPlayhead(ElementTrack el, PropertyTrack pt) => AddKeyAtTime(el, pt, _playhead);

        void AddKeyAtTime(ElementTrack el, PropertyTrack pt, float t)
        {
            Undo.RecordObject(_clip, "Add Keyframe");
            UIKeyframe k = pt.ValueType == PropertyValueType.Color
                ? new UIKeyframe(t, Color.white)
                : new UIKeyframe(t, PropertyMeta.DefaultFloat(pt.property));
            pt.keys.Add(k); pt.SortKeys();
            _selElement = el; _selProperty = pt; _selKey = k;
            MarkDirty(); RebuildTimeline(); RebuildInspector();
        }

        // ===============================================================
        //  PLAYBACK / TIME
        // ===============================================================
        void SetPlayhead(float t)
        {
            _playhead = Mathf.Max(0f, t);
            SampleToPreview();
            UpdatePlayheadVisual();
            UpdateTimeReadouts();
        }

        void TogglePlay()
        {
            if (_clip == null) return;
            _playing = !_playing;
            _lastTime = EditorApplication.timeSinceStartup;
        }

        void UpdateTimeReadouts()
        {
            if (_timeField != null) _timeField.SetValueWithoutNotify((float)Math.Round(_playhead, 2));
            if (_durationLabel != null && _clip != null) _durationLabel.text = $"/ {FormatTime(_clip.Duration)}";
        }

        static string FormatTime(float t)
        {
            if (t >= 60f)
            {
                int m = (int)(t / 60f);
                float s = t - m * 60f;
                return $"{m}:{s:00.00}";
            }
            return $"{t:0.00}s";
        }

        void Tick()
        {
            if (!_playing || _clip == null) return;
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTime);
            _lastTime = now;
            _playhead += dt * _clip.playbackSpeed;
            if (_playhead >= _clip.Duration)
            {
                if (_clip.loop) _playhead = 0f;
                else { _playhead = _clip.Duration; _playing = false; }
            }
            SampleToPreview();
            UpdatePlayheadVisual();
            UpdateTimeReadouts();
        }

        void SampleToPreview() => SampleHook?.Invoke(_playhead);

        void MarkDirty() { if (_clip != null) EditorUtility.SetDirty(_clip); }
    }
}
#endif
