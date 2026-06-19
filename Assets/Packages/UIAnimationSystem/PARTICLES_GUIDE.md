# UI Toolkit Particle System

A pooled, module-based particle engine for UI Toolkit. Particles are real
VisualElements, so they can be styled with USS or replaced by any custom element.
The engine ticks through the same TweenManager as the rest of the system.

## Folder
```
Particles/
  Particle.cs                  runtime state of one particle (pooled)
  ParticleModule.cs            base class for behaviours
  Modules.cs                   built-in modules (Shape, Velocity, Lifetime,
                               Size, Color, Gravity, Rotation)
  ParticleSystemConfig.cs      reusable preset (ScriptableObject)
  ParticleEmitter.cs           the engine (pool + simulation)
  ParticleVisualFactory.cs     builds the per-particle VisualElement
  UIParticleSystemComponent.cs MonoBehaviour + code API
Editor/
  ParticleSystemConfigEditor.cs  add/remove/reorder modules in the inspector
```

## 1. Create a preset
Project window: Create -> UI Toolkit -> Particle System.
A fresh asset comes with sensible default modules already added.

In the inspector you can:
- set Emission (rate, max particles, loop/duration, warmup)
- add Bursts (time + count)
- choose Visual (Circle / Square / Image / CustomClass)
- add / remove / reorder Modules with the buttons (+ Add Module)

## 2. Run it
### Option A - component (no code)
Add "UI Toolkit/UI Particle System" to the GameObject with the UIDocument.
- Config: your preset
- Host Element Name: name of the UXML element to render inside (empty = root)
Particles are clipped to the host bounds.

### Option B - code
```csharp
using UIToolkit.Animation.Particles;

public ParticleSystemConfig confetti;

void Celebrate(VisualElement host)
{
    host.SpawnParticles(confetti);          // continuous
    // or a one-shot burst (set loop=false on the asset):
    host.Burst(confetti, 60);
}
```

Runtime control:
```csharp
var emitter = host.SpawnParticles(confetti);
emitter.Pause();
emitter.Play();
emitter.Emit(30);        // manual burst
emitter.Stop(clear:true);
int n = emitter.AliveCount;
```

## 3. Built-in modules
- Shape: where particles spawn (Point / Circle / Rectangle / Edge)
- Velocity: initial direction + speed range (angle 0=right, 90=up)
- Lifetime: min/max seconds
- Size: start size range + size-over-lifetime curve
- Color: gradient over lifetime (alpha fade included)
- Gravity / Force: constant acceleration + drag
- Rotation: angular velocity, or align-to-velocity

Module order matters: they run top to bottom each frame.

## 4. Writing a custom module
```csharp
using UIToolkit.Animation.Particles;
using UnityEngine;

[System.Serializable]
public class TurbulenceModule : ParticleModule
{
    public float strength = 40f;
    public float frequency = 2f;

    public override void OnUpdate(Particle p, in ParticleContext ctx)
    {
        float n = Mathf.PerlinNoise(p.seed * 10f, p.age * frequency) - 0.5f;
        p.velocity += new Vector2(n, -n) * strength * ctx.deltaTime;
    }

    public override string DisplayName => "Turbulence";
}
```
It will automatically appear in the "+ Add Module" menu (reflection-based).

## 5. Custom particle appearance with USS
Set Visual = CustomClass and give a USS class name (default "ui-particle").
Then in your USS:
```css
.ui-particle {
    background-color: #ffd24a;
    border-radius: 2px;
}
```
Or Visual = Image and assign a Texture2D sprite; the particle color tints it.

## Performance notes
- The pool is pre-allocated to Max Particles, so steady-state has no GC.
- Particles past their lifetime are hidden and reused, not destroyed.
- For thousands of particles, prefer small simple visuals (Circle/Square) and
  keep modules lightweight.

## Extensibility
The data model uses [SerializeReference] for modules, so adding new module
types or fields will not break existing presets.
