using System;
using System.Numerics;
using OssianForge.Engine;
using OssianForge.Engine.Graphics;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Config;

/// <summary>
/// Lives on the tree node. While ModesConfig says "edit": a free camera scene and the gizmo scene are attached
/// under the tree node, the free camera becomes the current camera, and the gizmo follows the selected node and
/// edits its transform. Leaving edit removes both again.
/// Keep this the ONLY public type in the file (ScriptFile resolves the script type from the exported types).
/// </summary>
public class EditModeScript
{
    private const string EditMode = "edit";
    private const string GizmoSceneId = "configfile.scene.gizmo";
    private const string FreeCameraSceneId = "configfile.scene.freeCamera";
    private const string FreeCameraLocalId = "freeCamera";

    private const float MoveSpeed = 3f;      // units per second
    private const float RotateSpeed = 90f;   // degrees per second
    private const float ScaleSpeed = 1f;     // units per second
    private const float FineFactor = 0.1f;

    private enum GizmoMode { Position, Rotate, Scale }

    // ScriptProperty owns the instance; the static is only the door for the ControlProperty click action.
    private static EditModeScript _instance;

    private Node _root;
    private Node _gizmo;
    private Node _freeCamera;
    private Node _target;
    private string _lastMode;
    private GizmoMode _gizmoMode = GizmoMode.Position;
    private bool _faulted;

    // Best candidate among this frame's clicks (all hovered nodes fire OnClick, the nearest one wins).
    private Node _pickNode;
    private float _pickDistance = float.MaxValue;

    public void OnStart(Node node)
    {
        _root = node;
        _instance = this;
        Console.WriteLine($"[EDIT] EditModeScript started on '{node.Id}'");
    }

    public void OnUpdate(Node node, double delta)
    {
        if (_faulted) return;

        try
        {
            SyncMode();
            if (_gizmo == null) return;

            ApplyPick();
            UpdateGizmo((float)delta);
        }
        catch (Exception ex)
        {
            _faulted = true;
            Console.WriteLine($"[EDIT] Edit mode script stopped: {ex}");
        }
    }

    /// <summary>Called by action.systemActions.selectEditNode for every hovered node that gets clicked.</summary>
    public static void Select(Node node) => _instance?.Pick(node);

    // ── mode ─────────────────────────────────────────────────────────────────

    private static bool IsEdit(string mode) => string.Equals(mode, EditMode, StringComparison.OrdinalIgnoreCase);

    private void SyncMode()
    {
        // Null until Resources.PostLoad has built it, which happens after this script's OnStart.
        var modes = Engine.Resources.GetResource<ModesConfig>("configfile.modes");
        string mode = modes?.CurrentMode ?? "";
        if (mode == _lastMode) return;

        bool wasEdit = IsEdit(_lastMode);
        _lastMode = mode;

        if (wasEdit) ExitEdit();
        if (IsEdit(mode)) EnterEdit();
    }

    private void EnterEdit()
    {
        // Read the current camera before the free camera exists, so the free camera starts where the view is.
        var previous = Engine.Graphics.GetCurrentCamera();
        Vector3 position = Vector3.Zero;
        Vector3 forward = Vector3.UnitZ;
        if (previous != null)
        {
            position = previous.EyePosition;
            var view = previous.GetView();
            forward = -new Vector3(view.M13, view.M23, view.M33);   // 3rd column of a look-at view = backward
        }

        Engine.Nodes.LoadSceneResources(FreeCameraSceneId);
        _freeCamera = Attach(FreeCameraSceneId, scene => SeedFreeCamera(scene, position, forward));

        Engine.Nodes.LoadSceneResources(GizmoSceneId);
        _gizmo = Attach(GizmoSceneId, null);
        RefreshGizmo();

        var cameraNode = _freeCamera.FindLocal(FreeCameraLocalId) ?? _freeCamera;
        Engine.Graphics.SetCameraOverride(cameraNode.Id);

        Console.WriteLine($"[EDIT] Entered edit mode, camera '{cameraNode.Id}' is current.");
    }

    private void ExitEdit()
    {
        if (_freeCamera != null) Engine.Nodes.NodeManager.DestroySubtree(_freeCamera);
        if (_gizmo != null) Engine.Nodes.NodeManager.DestroySubtree(_gizmo);
        Engine.Graphics.ClearCameraOverride();

        _freeCamera = null;
        _gizmo = null;
        _target = null;
        _pickNode = null;
        _pickDistance = float.MaxValue;

        Console.WriteLine("[EDIT] Left edit mode.");
    }

    /// <summary>Same steps as Nodes.AttachScene, but under this node. Safe mid-update: the tree node is not iterating its children yet.</summary>
    private Node Attach(string sceneId, Action<Node> beforeStart)
    {
        var config = Engine.Resources.GetResource<SceneConfig>(sceneId)
            ?? throw new Exception($"[EDIT] Scene '{sceneId}' not found.");

        var scene = config.GetScene();
        scene.IsInstanceRoot = true;          // FindLocal stays inside this scene
        beforeStart?.Invoke(scene);           // before OnStart, so Transform/Camera start from the seeded pose

        _root.AddChild(scene);
        Engine.Nodes.NodeManager.StartSubtree(scene);
        return scene;
    }

    private static void SeedFreeCamera(Node scene, Vector3 position, Vector3 forward)
    {
        var tp = scene.FindLocal(FreeCameraLocalId)?.GetProperty<TransformProperty>();
        if (tp == null) return;

        // Transform.ToMatrix() = scale * rotX * rotY * rotZ, so its forward row is
        // (cos(p) sin(y), -sin(p), cos(p) cos(y)). Invert that.
        forward = Vector3.Normalize(forward);
        float pitch = float.RadiansToDegrees(MathF.Asin(Math.Clamp(-forward.Y, -1f, 1f)));
        float yaw = float.RadiansToDegrees(MathF.Atan2(forward.X, forward.Z));

        var t = tp.Transform;
        t.Position = position;
        t.Rotation = new Vector3(pitch, yaw, 0f);
        tp.Transform = t;
    }

    // ── picking ──────────────────────────────────────────────────────────────

    private void Pick(Node node)
    {
        if (_gizmo == null || node == null) return;

        // Never select the editor's own nodes.
        for (var n = node; n != null; n = n.Parent)
            if (n == _gizmo || n == _freeCamera) return;

        float distance = RayEntryDistance(node);
        if (_pickNode == null || distance < _pickDistance)
        {
            _pickNode = node;
            _pickDistance = distance;
        }
    }

    private void ApplyPick()
    {
        if (_pickNode == null) return;

        _target = _pickNode;
        _pickNode = null;
        _pickDistance = float.MaxValue;
        RefreshGizmo();

        Console.WriteLine($"[EDIT] Selected '{_target.Id}'");
    }

    /// <summary>Distance along the mouse ray to the node's world AABB (same box ControlProperty hit-tests).</summary>
    private static float RayEntryDistance(Node node)
    {
        var tp = node.GetProperty<TransformProperty>();
        var mesh = node.GetProperty<MeshProperty>();
        if (tp == null || mesh == null) return float.MaxValue;

        Vector3 lo = mesh.MeshResource.LocalAabbMin;
        Vector3 hi = mesh.MeshResource.LocalAabbMax;
        if (lo.X > hi.X) return float.MaxValue;   // mesh without a valid AABB

        var ray = Raycast.ScreenToRay(Engine.Inputs.MouseInput.Position);
        var model = tp.GetCameraModel();

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? lo.X : hi.X,
                (i & 2) == 0 ? lo.Y : hi.Y,
                (i & 4) == 0 ? lo.Z : hi.Z);
            var world = Vector3.Transform(corner, model);
            min = Vector3.Min(min, world);
            max = Vector3.Max(max, world);
        }

        float tMin = float.NegativeInfinity, tMax = float.PositiveInfinity;
        if (!Slab(ray.Origin.X, ray.Direction.X, min.X, max.X, ref tMin, ref tMax) ||
            !Slab(ray.Origin.Y, ray.Direction.Y, min.Y, max.Y, ref tMin, ref tMax) ||
            !Slab(ray.Origin.Z, ray.Direction.Z, min.Z, max.Z, ref tMin, ref tMax))
            return float.MaxValue;

        return MathF.Max(tMin, 0f);
    }

    private static bool Slab(float origin, float dir, float min, float max, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(dir) < 1e-8f) return origin >= min && origin <= max;

        float t1 = (min - origin) / dir;
        float t2 = (max - origin) / dir;
        if (t1 > t2) (t1, t2) = (t2, t1);

        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);
        return tMax >= 0f && tMin <= tMax;
    }

    // ── gizmo ────────────────────────────────────────────────────────────────

    private void UpdateGizmo(float dt)
    {
        if (_target != null && !IsAttached(_target))
        {
            _target = null;   // its scene was replaced or the node was removed
            RefreshGizmo();
        }
        if (_target == null) return;

        var targetTp = _target.GetProperty<TransformProperty>();
        var gizmoTp = _gizmo.GetProperty<TransformProperty>();
        if (targetTp == null || gizmoTp == null) return;

        if (Held("edit_gizmo_position")) SetGizmoMode(GizmoMode.Position);
        else if (Held("edit_gizmo_rotate")) SetGizmoMode(GizmoMode.Rotate);
        else if (Held("edit_gizmo_scale")) SetGizmoMode(GizmoMode.Scale);

        var axis = new Vector3(
            Axis("edit_axis_x_pos", "edit_axis_x_neg"),
            Axis("edit_axis_y_pos", "edit_axis_y_neg"),
            Axis("edit_axis_z_pos", "edit_axis_z_neg"));

        if (axis != Vector3.Zero)
        {
            float fine = Held("edit_fine") ? FineFactor : 1f;

            // Assign through the Transform setter: it marks the transform dirty, so a physics body follows.
            var t = targetTp.Transform;
            switch (_gizmoMode)
            {
                case GizmoMode.Position:
                    t.Position += axis * (MoveSpeed * fine * dt);
                    break;
                case GizmoMode.Rotate:
                    t.Rotation += axis * (RotateSpeed * fine * dt);
                    break;
                case GizmoMode.Scale:
                    t.Scale = Vector3.Max(t.Scale + axis * (ScaleSpeed * fine * dt), new Vector3(0.01f));
                    break;
            }
            targetTp.Transform = t;
        }

        Vector3 anchor = targetTp.WorldTransform.Position;
        if (gizmoTp.Position != anchor) gizmoTp.Position = anchor;
    }

    private void SetGizmoMode(GizmoMode mode)
    {
        if (_gizmoMode == mode) return;
        _gizmoMode = mode;
        RefreshGizmo();
    }

    /// <summary>Disabling a node does not stop its children from rendering, so every node is toggled explicitly.</summary>
    private void RefreshGizmo()
    {
        if (_gizmo == null) return;

        SetEnabledRecursive(_gizmo, false);
        if (_target == null) return;

        string active = _gizmoMode switch
        {
            GizmoMode.Rotate => "gizmo_rotate",
            GizmoMode.Scale => "gizmo_scale",
            _ => "gizmo_position"
        };

        _gizmo.Enabled = true;   // root must update so the children's world transforms follow it
        foreach (var child in _gizmo.Children)
            child.Enabled = child.LocalId == active;
    }

    private static void SetEnabledRecursive(Node node, bool value)
    {
        node.Enabled = value;
        foreach (var child in node.Children)
            SetEnabledRecursive(child, value);
    }

    private static bool IsAttached(Node node)
    {
        var top = node;
        while (top.Parent != null) top = top.Parent;
        return Engine.Nodes.NodeManager.Roots.Contains(top);
    }

    private static bool Held(string stateId) => Engine.Inputs.KeyHandler.IsStateActive(stateId);

    private static float Axis(string positive, string negative)
        => (Held(positive) ? 1f : 0f) - (Held(negative) ? 1f : 0f);
}