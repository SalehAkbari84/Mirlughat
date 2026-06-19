# Multi-Element Animation System + Visual Editor

## What changed
- One central component `UIAnimationDirector` replaces the old per-element component.
  Add ONE to the GameObject with the UIDocument and list as many animations as you want.
- Clips are now multi-element: a clip can animate several named elements at once,
  each with its own property tracks.
- New editor window with a live, draggable preview connected to the timeline.
- All code/comments are English (Inspector-safe).

## Folder layout
```
UIAnimationSystem/
  Core/            Easing, Tween, Sequence, TweenManager
  Timeline/        AnimatableProperty, UIAnimationClip, PropertyTrack,
                   ElementTrack, PropertyBinder, ClipPlayer,
                   ClipExtensions, UIAnimationDirector
  Editor/          UIAnimationEditorWindow (+ asmdef)
  UIAnim.cs        Fluent extension API (FadeIn, ScaleTo, ...)
  Tweening.cs      Sequence builder + global control
```

## 1. Make a clip
Project window: Create -> UI Toolkit -> Animation Clip.

## 2. Open the editor
Window -> UI Toolkit -> Animation Timeline (or double-click the clip).

Layout:
- LEFT  = live preview. Drag the boxes with the mouse; releasing records
          TranslateX/Y keyframes at the current playhead.
- RIGHT = timeline. Add elements, add property tracks, place/drag/delete keyframes.
- BOTTOM= keyframe + clip inspector.

### Editor actions
- "+ Element"          add a new element track (name it to match your UXML element)
- per-element "+"      add a property track (Opacity, Scale, Color, ...)
- per-property "+"     add a keyframe at the playhead
- double-click a lane  add a keyframe at that point
- drag a diamond       move a keyframe in time
- right-click diamond  delete keyframe
- click the ruler      scrub the playhead (preview updates live)
- "Play" / "Stop"      preview playback inside the window (no Unity Play needed)

## 3. Name matching
Each element track has an `elementName`. At runtime the player finds the element
with that `name` under the resolve root (Q<VisualElement>(elementName)). Set names
in UXML/UI Builder:
```xml
<ui:VisualElement name="panel" />
<ui:Label name="title" />
```

## 4. Play at runtime
### Option A - central director (no code)
Add component "UI Toolkit/UI Animation Director" to the UIDocument object.
For each entry set: id, clip, trigger (OnEnable/OnStart/Manual), optional
rootElementName. Call from code/buttons:
```csharp
GetComponent<UIAnimationDirector>().Play("intro");
```

### Option B - code
```csharp
using UIToolkit.Animation.Timeline;
public UIAnimationClip clip;
void OnEnable() {
    var root = GetComponent<UIDocument>().rootVisualElement;
    clip.Play(root); // element tracks resolved by name under root
}
```

## 5. Extending
- New animatable property: add to enum `AnimatableProperty` and a case in
  `PropertyBinder.ApplyFloat/ApplyColor` and `ReadFloat/ReadColor`.
- The data model is plain serializable classes, so it is forward-compatible:
  adding fields will not break existing assets.
