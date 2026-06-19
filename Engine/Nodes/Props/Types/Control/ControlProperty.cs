using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;
using OssianForge.Engine.UI;
using OssianForge.Engine.Utils;

namespace OssianForge.Engine.Nodes.Props
{
    public class ControlProperty : NodeProperty
    {
        // ── state ────────────────────────────────────────────────────────────
        public bool IsInteractable = true;
        public bool IsHovered { get; private set; }
        public bool IsPressed { get; private set; }
        public bool IsFocused { get; private set; }

        // ── drag & drop ──────────────────────────────────────────────────────
        public bool IsDraggable = false;
        public bool IsDropTarget = false;
        public string DragGroupId = null;
        public bool IsDragging { get; private set; }

        // ── signals ──────────────────────────────────────────────────────────
        public readonly Signal OnClick = new();
        public readonly Signal OnHover = new();
        public readonly Signal OnExit = new();
        public readonly Signal OnFocus = new();
        public readonly Signal OnUnfocus = new();
        public readonly Signal OnPress = new();
        public readonly Signal OnRelease = new();
        public readonly Signal<float> OnScroll = new();
        public readonly Signal OnDragStart = new();
        public readonly Signal OnDragEnd = new();
        public readonly Signal<Node> OnDrop = new();
        public readonly Signal<Node> OnDropReceived = new();

        public List<string> OnClickActions { get; } = new();
        public List<string> OnHoverActions { get; } = new();
        public List<string> OnExitActions { get; } = new();
        public List<string> OnFocusActions { get; } = new();
        public List<string> OnUnfocusActions { get; } = new();
        public List<string> OnPressActions { get; } = new();
        public List<string> OnReleaseActions { get; } = new();
        public List<string> OnScrollActions { get; } = new();
        public List<string> OnDragStartActions { get; } = new();
        public List<string> OnDragEndActions { get; } = new();
        public List<string> OnDropActions { get; } = new();
        public List<string> OnDropReceivedActions { get; } = new();

        // ── drag internals ───────────────────────────────────────────────────
        private const float DragThreshold = 4f;   // pixels before drag begins
        private Vector2 _pressMousePos;
        private bool _watchingForDrag;

        // ────────────────────────────────────────────────────────────────────
        // State setters — called by this class and by DragDrop
        // ────────────────────────────────────────────────────────────────────

        public ControlProperty(
            bool isInteractable = true,
            bool isDraggable = false,
            bool isDropTarget = false,
            string dragGroupId = null,
            Dictionary<string, List<string>> actionMap = null)
        {
            IsInteractable = isInteractable;
            IsDraggable = isDraggable;
            IsDropTarget = isDropTarget;
            DragGroupId = dragGroupId;

            if (actionMap == null) return;

            if (actionMap.TryGetValue("OnClick", out var v)) OnClickActions = v;
            if (actionMap.TryGetValue("OnHover", out v)) OnHoverActions = v;
            if (actionMap.TryGetValue("OnExit", out v)) OnExitActions = v;
            if (actionMap.TryGetValue("OnFocus", out v)) OnFocusActions = v;
            if (actionMap.TryGetValue("OnUnfocus", out v)) OnUnfocusActions = v;
            if (actionMap.TryGetValue("OnPress", out v)) OnPressActions = v;
            if (actionMap.TryGetValue("OnRelease", out v)) OnReleaseActions = v;
            if (actionMap.TryGetValue("OnScroll", out v)) OnScrollActions = v;
            if (actionMap.TryGetValue("OnDragStart", out v)) OnDragStartActions = v;
            if (actionMap.TryGetValue("OnDragEnd", out v)) OnDragEndActions = v;
            if (actionMap.TryGetValue("OnDrop", out v)) OnDropActions = v;
            if (actionMap.TryGetValue("OnDropReceived", out v)) OnDropReceivedActions = v;
        }

        public void SetHovered(bool value)
        {
            if (IsHovered == value) return;
            IsHovered = value;
            if (value) OnHover.Emit();
            else OnExit.Emit();
        }

        public void SetPressed(bool value)
        {
            if (IsPressed == value) return;
            IsPressed = value;
            if (value)
            {
                OnPress.Emit();
            }
            else
            {
                OnRelease.Emit();
                if (IsHovered) OnClick.Emit();
            }
        }

        public void SetFocused(bool value)
        {
            if (IsFocused == value) return;
            IsFocused = value;
            if (value) OnFocus.Emit();
            else OnUnfocus.Emit();
        }

        public void SetDragging(bool value)
        {
            if (IsDragging == value) return;
            IsDragging = value;
            if (value) OnDragStart.Emit();
            else OnDragEnd.Emit();
        }

        public void ReceiveDrop(Node dragged)
        {
            if (!IsDropTarget) return;
            OnDrop.Emit(dragged);
        }

        public void NotifyDroppedOnto(Node target)
        {
            OnDropReceived.Emit(target);
        }


        public override void OnStart(Node node)
        {
            OnClick.Connect(() => OnClickActions.ForEach(id => Engine.Resources.InvokeAction(id, node)));
            OnHover.Connect(() => OnHoverActions.ForEach(id => Engine.Resources.InvokeAction(id, node)));
            OnExit.Connect(() => OnExitActions.ForEach(id => Engine.Resources.InvokeAction(id, node)));
            OnFocus.Connect(() => OnFocusActions.ForEach(id => Engine.Resources.InvokeAction(id, node)));
            OnUnfocus.Connect(() => OnUnfocusActions.ForEach(id => Engine.Resources.InvokeAction(id, node)));
            OnPress.Connect(() => OnPressActions.ForEach(id => Engine.Resources.InvokeAction(id, node)));
            OnRelease.Connect(() => OnReleaseActions.ForEach(id => Engine.Resources.InvokeAction(id, node)));
            OnScroll.Connect(_ => OnScrollActions.ForEach(id => Engine.Resources.InvokeAction(id, node)));
            OnDragStart.Connect(() => OnDragStartActions.ForEach(id => Engine.Resources.InvokeAction(id, node)));
            OnDragEnd.Connect(() => OnDragEndActions.ForEach(id => Engine.Resources.InvokeAction(id, node)));
            OnDrop.Connect(_ => OnDropActions.ForEach(id => Engine.Resources.InvokeAction(id, node)));
            OnDropReceived.Connect(_ => OnDropReceivedActions.ForEach(id => Engine.Resources.InvokeAction(id, node)));
        }

        // ────────────────────────────────────────────────────────────────────
        // Per-frame update
        // ────────────────────────────────────────────────────────────────────

        public override void OnUpdate(Node node, double delta)
        {
            if (!IsInteractable) return;

            var transform = node.GetProperty<TransformProperty>();
            var mesh = node.GetProperty<MeshProperty>();
            if (transform == null || mesh == null) return;

            Vector2 mouse = Engine.Inputs.mouse.Position;

            bool inside = transform.RenderSpace == RenderSpace.ScreenSpace
                ? HitTestScreen(transform, mouse)
                : HitTestWorld(transform, mesh, mouse);

            // ── hover ───────────────────────────────────────────────────────
            SetHovered(inside);

            // ── scroll ──────────────────────────────────────────────────────
            if (inside)
            {
                float scroll = Engine.Inputs.mouse.ScrollDelta;
                if (scroll != 0f)
                    OnScroll.Emit(scroll);
            }

            bool lmbDown = Engine.Inputs.KeyHandler.IsStateActive("mouseleft_down");
            bool lmbPressed = Engine.Inputs.KeyHandler.IsStateActive("mouseleft_click");
            bool lmbReleased = Engine.Inputs.KeyHandler.IsStateActive("mouseleft_released");

            // ── press ───────────────────────────────────────────────────────
            if (lmbPressed && inside)
            {
                SetPressed(true);
                DragDrop.RequestFocus(this, node);

                _pressMousePos = mouse;
                _watchingForDrag = IsDraggable;
            }

            // ── drag start ──────────────────────────────────────────────────
            if (_watchingForDrag && lmbDown && !DragDrop.IsDragging)
            {
                float dist = Vector2.Distance(mouse, _pressMousePos);
                if (dist >= DragThreshold)
                {
                    _watchingForDrag = false;
                    DragDrop.BeginDrag(this, node);
                }
            }

            // ── release ─────────────────────────────────────────────────────
            if (lmbReleased && IsPressed)
            {
                _watchingForDrag = false;

                if (DragDrop.IsDragging)
                {
                    // If we are the drag source and released over a drop target,
                    // DragDrop.HandleDrop is responsible for cleanup.
                    // If we released over nothing, end the drag ourselves.
                    if (IsDragging)
                        DragDrop.EndDrag();
                }

                SetPressed(false);

                // Clear foreign focus when releasing outside it
                DragDrop.NotifyReleasedOutside(this);
            }

            // ── drop target: receive drag-release while hovered ──────────────
            if (IsDropTarget && inside && lmbReleased && DragDrop.IsDragging && !IsDragging)
                DragDrop.HandleDrop(this, node);
        }

        // ────────────────────────────────────────────────────────────────────
        // Hit testing
        //
        // TransformProperty.Transform is in screen pixels after ToScreenSpace()
        // stores the NDC values back into Position/Scale.  We reconstruct pixels
        // via FromScreenSpace so this works regardless of whether the transform
        // has already been converted.
        //
        // Transform layout (after ToScreenSpace):
        //   Position.X/Y = NDC top-left corner  (before scale offset baked in)
        //   Scale.X/Y    = NDC extents
        //
        // We call FromScreenSpace on a copy to get pixel coords, then do AABB.
        // ────────────────────────────────────────────────────────────────────

        private static bool HitTestScreen(TransformProperty tp, Vector2 mousePixels)
        {
            Vector2 screen = new Vector2(Engine.Graphics.WindowSize.X,
                                         Engine.Graphics.WindowSize.Y);

            // Mouse arrives in window space: origin top-left, Y-down.
            // Convert to center-origin, Y-up — the same space ToScreenSpace
            // produces for NDC before the final divide.
            //   ndcX = mouseX / (screen.X * 0.5) - 1
            //   ndcY = 1 - mouseY / (screen.Y * 0.5)   ← flip Y
            float ndcMouseX = mousePixels.X / (screen.X * 0.5f) - 1f;
            float ndcMouseY = -(mousePixels.Y / (screen.Y * 0.5f) - 1f);

            // Transform.Position is the element CENTER in NDC.
            // Transform.Scale    is the element SIZE   in NDC (full width/height).
            float cx = tp.Transform.Position.X;
            float cy = tp.Transform.Position.Y;
            float hw = tp.Transform.Scale.X * 0.5f;   // half-width  in NDC
            float hh = tp.Transform.Scale.Y * 0.5f;   // half-height in NDC

            return ndcMouseX >= cx - hw && ndcMouseX <= cx + hw &&
                   ndcMouseY >= cy - hh && ndcMouseY <= cy + hh;
        }

        private static bool HitTestWorld(TransformProperty tp, MeshProperty mp, Vector2 mousePixels)
        {
            var ray = Raycast.ScreenToRay(mousePixels);
            var model = tp.GetCameraModel(); // includes position, rotation, scale

            // Transform local AABB corners into world space and refit —
            // handles non-uniform scale and rotation correctly.
            var localMin = mp.MeshResource.LocalAabbMin;
            var localMax = mp.MeshResource.LocalAabbMax;

            var worldMin = new Vector3(float.MaxValue);
            var worldMax = new Vector3(float.MinValue);

            // All 8 corners of the local AABB
            Span<Vector3> corners = stackalloc Vector3[8]
            {
                new(localMin.X, localMin.Y, localMin.Z),
                new(localMax.X, localMin.Y, localMin.Z),
                new(localMin.X, localMax.Y, localMin.Z),
                new(localMax.X, localMax.Y, localMin.Z),
                new(localMin.X, localMin.Y, localMax.Z),
                new(localMax.X, localMin.Y, localMax.Z),
                new(localMin.X, localMax.Y, localMax.Z),
                new(localMax.X, localMax.Y, localMax.Z),
            };

            foreach (var c in corners)
            {
                var w = Vector3.Transform(c, model);
                worldMin = Vector3.Min(worldMin, w);
                worldMax = Vector3.Max(worldMax, w);
            }

            return Raycast.RayIntersectsAABB(ray, worldMin, worldMax);
        }
    }
}