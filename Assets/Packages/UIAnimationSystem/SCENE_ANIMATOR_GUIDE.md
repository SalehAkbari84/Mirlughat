# UI Scene Animator (unified editor)

One window that loads your real UXML layout (with its USS), shows the actual
elements, and lets you attach animations and particles to them.

## Open
Window -> UI Toolkit -> Scene Animator
(or double-click a UISceneAnimation asset)

## 1. Create a scene asset
Create -> UI Toolkit -> Scene Animation. On the asset set:
- UXML            : your layout (VisualTreeAsset)
- Additional Style Sheets : optional extra USS on top of the UXML's own
- Preview Size    : the canvas size used in the editor preview
- Preview Background

Assign it in the window's top "scene asset" field (or double-click the asset).

## 2. The window
- LEFT  : HIERARCHY - the real element tree from your UXML. Named elements show
          a blue dot; unnamed ones a grey dot. Click to select.
- CENTER: PREVIEW   - a true render of your UXML + USS. Click any element to
          select it. The yellow box shows the current selection. Zoom with the
          toolbar slider.
- RIGHT : TABS
    Animation - pick/create a UIAnimationClip, add a track for the selected
                element, and "Open in Timeline Editor" for full keyframing.
                "Bind clip to scene" stores it on the asset (plays OnEnable).
    Particles - pick a ParticleSystemConfig and bind it to the selected element
                as its host. Manage bindings here.

Selection is synced: clicking in the preview highlights the hierarchy row and
vice versa.

## 3. Naming matters
Animations and particles bind by element NAME. If you select an unnamed element
the right panel warns you. Give elements names in UXML / UI Builder:
```xml
<ui:VisualElement name="hero_panel" />
<ui:Label name="title" />
```

## 4. Play it in your game
Add the "UI Toolkit/UI Scene Director" component to the GameObject that has the
UIDocument using the same UXML. Assign the same UISceneAnimation asset.
On enable it plays all bound clips (with OnEnable/OnStart triggers) and starts
bound particle systems.

From code / UI buttons:
```csharp
var dir = GetComponent<UISceneDirector>();
dir.PlayClip("intro");
dir.PlayParticles("confetti");
```

## How storage works (why a separate asset)
The UISceneAnimation asset references your UXML but never modifies it. Clips and
particle presets are their own reusable assets; the scene asset just records the
bindings (which clip/particle goes on which named element, and when). This keeps
your UXML clean and lets you reuse clips across scenes.

## Detailed editing
For precise keyframing use the Timeline window (Window -> UI Toolkit ->
Animation Timeline) - the "Open in Timeline Editor" button jumps straight there
with the active clip. For particle tuning, edit the ParticleSystemConfig asset
(its inspector has add/remove/reorder module controls).
