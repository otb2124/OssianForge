# OssianForge Engine

OssianForge is a custom C# / .NET 3D game engine designed around a data-driven node-graph scene model, dynamic asset dependency resolution, and modular configuration pipelines.

---

## Architecture Overview

```mermaid
graph TD
    A[Scene JSON / TreeConfig] -->|Extract Tree / Scene| B(NodeDependency Extractor)
    B -->|Parse Prefix Matches| C{Resource Registry}
    
    subgraph Resource Resolution
        C -->|Resolve Prefixes| D[Meshes - mesh.*]
        C -->|Resolve Prefixes| E[Shaders - shader.*]
        C -->|Resolve Prefixes| F[Textures & Cubemaps]
        C -->|Recursive Resolve| G[SceneReferenceProperty]
        G -->|Extract Referenced Scene| B
    end

    D --> H[Resource Loader & Memory Cache]
    E --> H
    F --> H
    H --> I[Scene Graph Initialization]
