using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;

namespace OssianForge.Engine.UI
{
    /// <summary>
    /// Singleton that owns globally unique UI state: which node has focus,
    /// and which node is currently being dragged.
    /// Call DragDrop.Update() once per frame BEFORE node tree updates.
    /// </summary>
    public static class DragDrop
    {
        private static ControlProperty _focused;
        private static ControlProperty _dragging;

        // The node being dragged and its drag-group, so drop targets can filter.
        public static Node DraggedNode { get; private set; }
        public static string DragGroupId { get; private set; }

        // ── Focus ────────────────────────────────────────────────────────────

        public static void RequestFocus(ControlProperty incoming, Node owner)
        {
            if (_focused == incoming) return;
            _focused?.SetFocused(false);
            _focused = incoming;
            _focused?.SetFocused(true);
        }

        public static void ClearFocus()
        {
            _focused?.SetFocused(false);
            _focused = null;
        }

        /// <summary>Called by a ControlProperty when the mouse button is released
        /// anywhere — clears focus if the release happened outside that control.</summary>
        public static void NotifyReleasedOutside(ControlProperty sender)
        {
            if (_focused != null && _focused != sender)
                ClearFocus();
        }

        // ── Drag ─────────────────────────────────────────────────────────────

        public static void BeginDrag(ControlProperty ctrl, Node owner)
        {
            if (_dragging != null) return;
            _dragging = ctrl;
            DraggedNode = owner;
            DragGroupId = ctrl.DragGroupId;
            ctrl.SetDragging(true);
        }

        public static void EndDrag()
        {
            if (_dragging == null) return;
            _dragging.SetDragging(false);
            _dragging = null;
            DraggedNode = null;
            DragGroupId = null;
        }

        public static bool IsDragging => _dragging != null;

        /// <summary>
        /// Called by a drop-target ControlProperty when the mouse button releases
        /// while hovering it.
        /// </summary>
        public static void HandleDrop(ControlProperty target, Node targetOwner)
        {
            if (_dragging == null || _dragging == target) return;

            // Group filter: both nodes must share a DragGroupId (or neither cares).
            if (target.DragGroupId != null && target.DragGroupId != DragGroupId) return;

            target.ReceiveDrop(DraggedNode);
            _dragging.NotifyDroppedOnto(targetOwner);
            EndDrag();
        }
    }
}