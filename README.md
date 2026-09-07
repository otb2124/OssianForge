# OssianForge

OssianForge is a custom 3D game engine written in C# on top of **Silk.NET** and **OpenGL**. It follows a Godot-style node/property architecture, where behavior is composed by attaching typed `NodeProperty` components to `Node`s, and most game content — scenes, actions, state machines, input bindings — is authored as data in JSON rather than hardcoded.

A lightweight reflection dispatcher sits at the center of the engine: JSON config files reference engine and game methods by string path (e.g. `node.callMethod`, `nodes.getValue`), which are resolved, cached, and invoked at runtime. This lets scenes, animations, and gameplay logic be wired up without recompiling.

## Features

- **Node/Property architecture** — a `Node` tree (parent/children) where behavior comes from attached `NodeProperty` components (Transform, Mesh, Material, Animation, Collider, Physics, Camera, Sound, Script, StateMachine, Control, Emission, Group, Scene reference, and more).
- **Data-driven content** — scenes, actions, input axes/keys, pronouns, and state machines are all defined in JSON under `Content/ConfigFiles/` and loaded through a typed config/resource system.
- **Reflection-based action dispatcher** — a caching dispatcher (`ReflectionDispatcher`) resolves string-based calls like `"$child.playerBodyMesh", "AnimationProperty", "Play"` to compiled, cached delegate invokers, avoiding per-call reflection overhead.
- **Physics** — integrates [Jitter2](https://github.com/notgiven688/jitter2) with support for multiple simultaneous physics worlds (e.g. a 3D world and a screen-space world), rigid and static bodies, and multiple collider shapes (box, capsule, mesh, terrain).
- **Rendering** — OpenGL rendering via Silk.NET, including batching, render targets, and a post-processing stack.
- **Animation** — skeletal animation playback driven by FBX-imported animation clips, wired up through JSON action files.
- **State machines** — JSON-defined state machines (e.g. the player's idle/walk/run/sprint/jump states) that drive animation and movement actions on enter/update.
- **Input system** — configurable input axes and key bindings loaded from JSON, feeding keyboard, mouse, and gamepad input.
- **Audio** — sound playback via OpenAL (through Silk.NET.OpenAL) with Ogg Vorbis decoding via NVorbis.
- **UI** — an in-engine UI/text system with drag-and-drop and raycasting support.
- **Debug tooling** — an in-engine debug console with a command parser, plus system/graphics stats for a debug overlay.
- **Scripting packs** — user and built-in C# "script packs" (e.g. Motion, Vehicle, StateMachine) that extend node behavior and can be referenced from config data.

## Tech Stack

- **Language / Runtime:** C# / .NET 8
- **Windowing & Rendering:** Silk.NET.Windowing, Silk.NET.OpenGL
- **Math:** Silk.NET.Maths
- **Physics:** Jitter2
- **Audio:** Silk.NET.OpenAL (+ Silk.NET.OpenAL.Soft.Native), NVorbis (Ogg decoding)
- **Model/Animation Import:** Silk.NET.Assimp (FBX loading)
- **Textures:** StbImageSharp
- **Input:** Silk.NET.Input
- **Scripting/Reflection:** Microsoft.CodeAnalysis.CSharp (Roslyn), System.Reflection + compiled expression trees

## Project Structure

```
OssianForge/
├── App/                          # Entry point (Program.cs, App.cs) — wires the window loop to the Engine
├── Engine/
│   ├── Engine.cs                 # Static root that owns and drives all subsystems
│   ├── Audio/                    # Audio playback subsystem
│   ├── Core/                     # ReflectionDispatcher, TypeRegistry
│   ├── Graphics/                 # Rendering, batching, camera, render targets/post-processing
│   ├── Inputs/                   # Keyboard, mouse, gamepad input handling
│   ├── Nodes/                    # Node/NodeManager/NodeReflection + all NodeProperty types
│   │   └── Props/Types/          # Animation, Camera, Collider, Control, Emission, Group,
│   │                              # Material, Mesh, Physics, Scene, Script, Sound,
│   │                              # StateMachine, Transform properties
│   ├── Physics/                  # Jitter2 integration, physics worlds and bodies
│   ├── Resources/                # Resource loading/scheduling and typed config/resource files
│   │   └── Types/                # Animations, Colliders, Config, Fonts, Meshes, Shaders,
│   │                              # Sounds, Scripts, Textures
│   ├── UI/                       # UI, drag-drop, raycasting
│   └── Utils/                    # Console/commands, math, condition-node parsing, file helpers
├── Content/
│   ├── ConfigFiles/               # JSON-driven content: Actions, Core (tree/modes/resources),
│   │                               # Fonts, InputAxis, InputKeys, Pronouns, Scenes, StateMachines
│   └── ScriptFiles/                # User and built-in script packs (Motion, StateMachine, Vehicle)
└── OssianForge.csproj
```

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A GPU/driver with OpenGL support
- Visual Studio with the .NET desktop development workload (.NET 8 SDK)

### Build & Run

Open OssianForge.csproj (or the solution) in Visual Studio.
Let NuGet restore the dependencies (Jitter2, Silk.NET.*, NVorbis, StbImageSharp, Microsoft.CodeAnalysis.CSharp) — this happens automatically on load/build.
Build the solution (Build → Build Solution, or Ctrl+Shift+B).
Run the produced executable from App/bin/Debug/net8.0/ (or press F5 / Start in Visual Studio to build and launch in one step).

## How Content Is Authored

Most game behavior is defined declaratively in `Content/ConfigFiles/`, rather than in code:

- **`Core/tree.json`** — the root node tree and the main scene to load.
- **`Scenes/*.json`** — node hierarchies, each node listing its `properties` (e.g. `TransformProperty`, `MeshProperty`, `TextureMaterialProperty`, `ColliderProperty`, `RigidPhysicsProperty`, `StateMachineProperty`).
- **`Actions/*.json`** — named actions (`action.<file>.<name>`) that call engine/game methods via the reflection dispatcher, e.g.:
  ```json
  {
    "id": "action.animationActions.playAnimWalking",
    "call": "node.callMethod",
    "args": [ "$child.playerBodyMesh", "AnimationProperty", "Play", "remy.walking", true, 1.0 ]
  }
  ```
- **`StateMachines/*.json`** — states with `onEnter` / `onUpdate` action lists (e.g. the player's `Idle` → `WalkForward` → `RunForward` → `SprintForward` states).
- **`InputAxis/` / `InputKeys/`** — input axis and key binding definitions.
- **`Pronouns/`** — string substitutions (e.g. `$self`, `$child.<name>`) resolved before dispatching an action.

This means new gameplay wiring — playing an animation, updating a transform, responding to a state change — can typically be added or adjusted by editing JSON, without touching engine source.

## License

TBC
