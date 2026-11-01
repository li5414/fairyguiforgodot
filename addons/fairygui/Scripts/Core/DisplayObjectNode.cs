// FairyGUI for Godot - new file (no Unity counterpart).
// The Godot node that backs every DisplayObject. It replaces the Unity GameObject/Transform pair
// (DisplayObject.gameObject / cachedTransform) and the DisplayObjectInfo MonoBehaviour.
using System.Text;
using Godot;

namespace FairyGUI
{
    /// <summary>
    /// Node2D owned by a DisplayObject. _Draw() is forwarded to DisplayObject.OnNodeDraw(),
    /// which is the only place NGraphics may submit its canvas commands.
    /// </summary>
    public partial class DisplayObjectNode : Node2D
    {
        /// <summary>
        /// The display object this node belongs to.
        /// </summary>
        public DisplayObject owner;

        public DisplayObjectNode()
        {
        }

        public override void _Draw()
        {
            owner?.OnNodeDraw();
        }

        public override void _Notification(int what)
        {
            // [Godot] Replaces DisplayObjectInfo.OnDestroy: the node was freed (possibly by someone else),
            // so the owner must stop touching it.
            if (what == NotificationPredelete && owner != null)
                owner._flags |= DisplayObject.Flags.GameObjectDisposed;
        }

        /// <summary>
        /// Returns a name that is valid for Node.Name: the characters . : @ / " % are replaced with _ .
        /// Empty or null names become "Node".
        /// </summary>
        public static string ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Node";

            StringBuilder sb = null;
            int len = name.Length;
            for (int i = 0; i < len; i++)
            {
                char c = name[i];
                if (c == '.' || c == ':' || c == '@' || c == '/' || c == '"' || c == '%')
                {
                    if (sb == null)
                        sb = new StringBuilder(name);
                    sb[i] = '_';
                }
            }

            return sb != null ? sb.ToString() : name;
        }
    }
}
