using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Nodes.Props
{
    public enum HitDetectionMode
    {
        Auto,           // engine decides based on node's shader/mesh
        ScreenBounds,   // 2D rect check, for billboard quads
        Raycast         // 3D raycast from camera, for mesh nodes
    }

    public class ControlProperty : NodeProperty
    {
        // --- state ---
        public bool IsVisible = true;
        public bool IsInteractable = true;
        public bool IsHovered { get; private set; }
        public bool IsPressed { get; private set; }
        public bool IsFocused { get; private set; }

        // --- drag and drop ---
        public bool IsDraggable = false;
        public bool IsDropTarget = false;
        public string DragGroupId = null;
        public bool IsDragging { get; private set; }

        // --- hit detection ---
        public HitDetectionMode HitDetection = HitDetectionMode.Auto;

        // --- signals ---
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
        public readonly Signal<Node> OnDrop = new();          // fired on drop target, passes dragged node
        public readonly Signal<Node> OnDropReceived = new();  // fired on dragged node, passes target node

        // --- internal state setters called by UI system ---
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
            if (value) OnPress.Emit();
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
    }
}