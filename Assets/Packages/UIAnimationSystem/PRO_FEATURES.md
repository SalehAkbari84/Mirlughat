# UI Toolkit Animation System - Professional Features

This document covers the professional/enterprise capabilities added on top of the
core engine. For the engine basics see `README.md`, `TIMELINE_GUIDE.md`,
`PARTICLES_GUIDE.md`, and `SCENE_ANIMATOR_GUIDE.md`.

---

## 1. One unified window

`Window > UI Toolkit > Scene Animator` is the single authoring window. Everything
is here as tabs - you never switch windows:

- **Left**: hierarchy + live, auto-fitted UXML preview (click to select).
- **Right tabs**: Setup | Animation | Particles | Bindings | Library.
- **Toolbar**: scene asset, Auto Fit, zoom, Preview Scene, and one-click Setup & Play.

The standalone `Animation Timeline` window still exists for quick single-clip
edits; both share the same `TimelineView` element (`Editor/TimelineView.cs`).

### Auto-fit preview
The preview detects the canvas size (the scene's `previewSize` if set, else
1920x1080) and **auto-scales to fit and center** in the pane. Recomputed on
resize. Turn off **Auto Fit** in the toolbar to use the manual zoom slider.

### Setup & Play (one click)
The green **Setup & Play** button finds the `UIDocument` in the open scene,
attaches a `UISceneDirector` pointing at the current scene asset, and enters Play
Mode. Author, press it, done. (You still need a `UIDocument` with a
`PanelSettings` somewhere in the scene.)

### Full-scene live preview (no Play Mode)
**Preview Scene** in the toolbar plays every bound clip and particle system live
inside the editor window, driven by `EditorApplication.update`. Press again to stop.

---

## 2. Fast authoring

- **Record mode** (Animation tab): toggle on, then drag the selected element in
  the live preview to write Translate keys at the playhead.
- **Keyboard shortcuts** (window focused, not editing a text field):
  - `Space` play/pause, `K` key snapshot of the selected element,
  - `Del` delete selected key(s), `Left/Right` scrub, `Shift+Left/Right` move key,
  - `Ctrl/Cmd + C / V / D` copy / paste / duplicate selected keys.

## 3. Keyframe editing power (timeline)

- Multi-select (Ctrl/Cmd/Shift click), **box-select** (drag on empty track area),
  group-drag, copy/paste/duplicate, multi-delete.
- **Track-level ease**: the `E` button on a property track sets the ease for all
  its keys at once.

## 4. Preset libraries

- **Animation presets** (`AnimationPresets.Create`): FadeIn/Out, PopIn/Out,
  SlideIn (4 dirs), Pulse, Shake, Spin, Wiggle, FloatUpDown.
- **Particle presets** (`ParticlePresets.Create`): Confetti, Sparkle, Smoke,
  Burst, Fireworks.
- In the Animation/Particles tabs choose a preset and click **Apply** - it creates
  the asset, saves it to the right folder, and binds it to the selected element.

## 5. Clip options

- **Relative mode** (`UIAnimationClip.relative`): float values are added on top of
  each element's pose at play time, so a preset works regardless of layout.
- **Timed events** (`UIAnimationClip.events`): fire named events during playback.
  Subscribe via `ClipPlayer.OnEvent`:

```csharp
var player = clip.Play(root);
player.OnEvent += name => Debug.Log("clip event: " + name);
```

- **Seek/scrub at runtime**: `player.Seek(time, pause: true);`

---

## 6. Calling animations by name from code

Clips you create are saved to `Assets/Resources/UIAnimations/` and registered in
`Assets/Resources/UIAnimationLibrary.asset`, so they are callable by name with no
setup:

```csharp
using UIToolkit.Animation.Timeline;

UIAnimation.Play("PopIn_loginButton", rootVisualElement);
```

Other API:

```csharp
UIAnimation.Register("intro", clip);   // register a clip under a custom name
UIAnimation.Get("intro");              // look up (registry -> library -> Resources)
UIAnimation.Has("intro");
UIAnimation.Unregister("intro");
```

The name is the clip asset's file name (rename it in the Library tab).

---

## 7. Library tab

Lists every `UIAnimationClip` and `ParticleSystemConfig` in the project, with a
search filter. Per clip: **Edit** (load in Animation tab), **Copy Call** (copies
`UIAnimation.Play("name", root);`), **Ping**, inline **rename**, and **delete**
(moves to trash - recoverable). Per particle: Bind to selected host, Ping, delete.

---

## 8. Particle performance (mesh renderer)

Set `ParticleSystemConfig.meshRenderer = true` for Circle/Square particles to draw
all of them with a single `Painter2D` mesh instead of one `VisualElement` each.
This scales to thousands of particles. Image/CustomClass kinds fall back to the
per-element path.

---

## 9. Reliability

- **Validation**: the Bindings tab warns when a binding references an element name
  that does not exist in the loaded UXML.
- **Undo**: edits (keys, presets, bindings, events, record, rename) use
  `Undo.RecordObject`. Library delete uses trash (recoverable).
- **Persistence**: the window remembers the last scene asset and active tab
  (`EditorPrefs`).
- **Tests**: edit-mode tests under `Tests/Editor/` cover easing, tween loop math
  (incl. the Incremental regression), sequence timing, and track sampling. Run
  them via Window > General > Test Runner (requires the Test Framework package).

---

## 10. Code-first API (for designers who script)

You can author and run everything from code - no editor needed.

### Fluent one-shots (on any VisualElement)
```csharp
using UIToolkit.Animation;          // tween extensions
element.FadeIn(); element.FadeOut();
element.ScaleTo(1f, 0.3f).SetEase(Ease.OutBack);
element.MoveTo(new Vector2(0,0), 0.4f);
element.PunchScale(); element.Shake(); element.Spin();
element.Pulse(); element.Wiggle(); element.FloatLoop();      // looping idles
element.MovePath(points, 1f);                                // spline motion
element.SetPivot(0.5f, 0.5f);                                // pivot for scale/rotate
```

### Stagger a list
```csharp
buttons.StaggerFadeIn(0.3f, 0.05f);
cards.StaggerScaleIn();
rows.StaggerSlideInFromBottom();
```

### Build a multi-element clip in code
```csharp
using UIToolkit.Animation.Timeline;
UIClip.New("intro")
    .Element("panel").Opacity(0,0).Opacity(0.3f,1).Move(0, new Vector2(0,40)).Move(0.3f, Vector2.zero)
    .Element("title").ScaleXY(0,0.6f).ScaleXY(0.35f,1, Ease.OutBack)
    .Event(0.3f, "intro_done")
    .Play(root);
```

### Play a saved clip by name
```csharp
UIAnimation.Play("PopIn_loginButton", root);
```

### Schedule a whole scene timeline
```csharp
UITimeline.New()
    .Clip(0f, introClip, root)
    .Spawn(0.3f, host, confetti, burst: 80)
    .Call(0.3f, () => PlaySfx())
    .Clip(0.5f, titleClip, root)
    .Play();
```

### Sequence / await / coroutine
```csharp
// fluent sequence
Tweening.Sequence().Append(a.FadeIn()).Join(b.ScaleTo(1f,0.3f)).AppendCallback(() => {...});

// async/await
await panel.FadeIn();
await title.ScaleTo(1f, 0.3f);

// coroutine
yield return panel.FadeIn().WaitForCompletion();
```

### Particles from code
```csharp
host.SpawnPreset(ParticlePresets.Kind.Sparkle);
button.BurstPreset(ParticlePresets.Kind.Confetti, 80);
host.SpawnParticles(myConfig);
```

### Clip events
```csharp
var player = clip.Play(root);
player.OnEvent += name => Debug.Log("event: " + name);
player.Seek(0.5f, pause: true);     // scrub from code
```

---

## 11. Particle modules & trails

Built-in modules (all appear in the Add Module menu): Shape, Velocity, Lifetime,
Size, Color, Gravity/Force, Rotation, **Attractor**, **Noise/Turbulence**. Add new
behaviour by subclassing `ParticleModule`.

**Trails**: set `ParticleSystemConfig.trail = true` (with `trailLength` /
`trailWidthScale`) to draw a fading ribbon behind each particle. Trails use the
mesh (Painter2D) renderer and work with Circle/Square kinds. The `Trail` particle
preset is ready to use.

---

## Preview navigation

Wheel to zoom toward the cursor, middle-mouse drag to pan (like UI Builder).
Toggle **Auto Fit** to re-center and fit automatically.

---

## New / key files

```
Timeline/UIAnimation.cs          Play("name", root) + name registry
Timeline/UIAnimationLibrary.cs   ScriptableObject list of clips (auto-loaded)
Timeline/AnimationPresets.cs     ready-made animation clips
Timeline/UIClipBuilder.cs        UIClip - fluent code-first clip builder
Timeline/UITimeline.cs           schedule clips/tweens/particles/calls in code
Timeline/Validation.cs           UIAnimationValidator (checks + auto-fix)
Particles/ParticlePresets.cs     ready-made particle configs (incl. Trail)
Core/TweenExtensions.cs          WaitForCompletion (coroutine)
Core/TweenAwaiter.cs             await support for any tween
Editor/TimelineView.cs           shared timeline element (incl. ease graph)
Editor/SceneAnimator.uss         theming foundation for the window
Tests/Editor/                    edit-mode engine + validation tests
```
