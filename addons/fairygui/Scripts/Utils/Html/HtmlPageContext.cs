// Ported from FairyGUI-unity: Assets/Scripts/Utils/Html/HtmlPageContext.cs
using System.Collections.Generic;
using Godot;

namespace FairyGUI.Utils
{
    /// <summary>
    ///
    /// </summary>
    public class HtmlPageContext : IHtmlPageContext
    {
        Stack<IHtmlObject> _imagePool;
        Stack<IHtmlObject> _inputPool;
        Stack<IHtmlObject> _buttonPool;
        Stack<IHtmlObject> _selectPool;
        Stack<IHtmlObject> _linkPool;

        static HtmlPageContext _inst;

        public static HtmlPageContext inst
        {
            get
            {
                if (_inst == null)
                    _inst = new HtmlPageContext();
                return _inst;
            }
        }

        // [Godot] Unity: a hidden, inactive GameObject created by Stage.CreatePoolManager, used as the parent of pooled
        // objects. Godot allows orphan nodes, so pooled objects are simply taken out of the tree (see FreeObject).
        // _poolManager is a plain orphan Node (never added to the tree) whose existence marks pooling as active,
        // exactly like the Unity null checks below. It is freed in Dispose().
        static Node _poolManager;

        // [Godot] Unity's #if UNITY_2019_3_OR_NEWER [RuntimeInitializeOnLoadMethod] InitializeOnLoad (resets _inst and
        // _poolManager for "enter play mode without domain reload") is dropped: Godot reloads the C# assembly,
        // so static state starts fresh on every run.

        public HtmlPageContext()
        {
            _imagePool = new Stack<IHtmlObject>();
            _inputPool = new Stack<IHtmlObject>();
            _buttonPool = new Stack<IHtmlObject>();
            _selectPool = new Stack<IHtmlObject>();
            _linkPool = new Stack<IHtmlObject>();

            // [Godot] Unity: Application.isPlaying && _poolManager == null -> Stage.inst.CreatePoolManager("HtmlObjectPool")
            if (!Engine.IsEditorHint() && !GodotObject.IsInstanceValid(_poolManager))
                _poolManager = new Node { Name = "[HtmlObjectPool]" };
        }

        virtual public IHtmlObject CreateObject(RichTextField owner, HtmlElement element)
        {
            IHtmlObject ret = null;
            bool fromPool = false;
            if (element.type == HtmlElementType.Image)
            {
                if (_imagePool.Count > 0 && GodotObject.IsInstanceValid(_poolManager))
                {
                    ret = _imagePool.Pop();
                    fromPool = true;
                }
                else
                    ret = new HtmlImage();
            }
            else if (element.type == HtmlElementType.Link)
            {
                if (_linkPool.Count > 0 && GodotObject.IsInstanceValid(_poolManager))
                {
                    ret = _linkPool.Pop();
                    fromPool = true;
                }
                else
                    ret = new HtmlLink();
            }
            else if (element.type == HtmlElementType.Input)
            {
                string type = element.GetString("type");
                if (type != null)
                    type = type.ToLower();
                if (type == "button" || type == "submit")
                {
                    if (_buttonPool.Count > 0 && GodotObject.IsInstanceValid(_poolManager))
                    {
                        ret = _buttonPool.Pop();
                        fromPool = true;
                    }
                    else
                    {
                        if (HtmlButton.resource != null)
                            ret = new HtmlButton();
                        else
                            GD.PushWarning("FairyGUI: Set HtmlButton.resource first");
                    }
                }
                else
                {
                    if (_inputPool.Count > 0 && GodotObject.IsInstanceValid(_poolManager))
                    {
                        ret = _inputPool.Pop();
                        fromPool = true;
                    }
                    else
                        ret = new HtmlInput();
                }
            }
            else if (element.type == HtmlElementType.Select)
            {
                if (_selectPool.Count > 0 && GodotObject.IsInstanceValid(_poolManager))
                {
                    ret = _selectPool.Pop();
                    fromPool = true;
                }
                else
                {
                    if (HtmlSelect.resource != null)
                        ret = new HtmlSelect();
                    else
                        GD.PushWarning("FairyGUI: Set HtmlSelect.resource first");
                }
            }

            //Debug.Log("from=" + fromPool);
            if (ret != null)
            {
                //可能已经被GameObject tree deleted了，不再使用
                if (fromPool && ret.displayObject != null && ret.displayObject.isDisposed)
                {
                    ret.Dispose();
                    return CreateObject(owner, element);

                }
                ret.Create(owner, element);
                if (ret.displayObject != null)
                    ret.displayObject.home = owner.node; // [Godot] Unity: owner.cachedTransform
            }

            return ret;
        }

        virtual public void FreeObject(IHtmlObject obj)
        {
            if (!GodotObject.IsInstanceValid(_poolManager))
            {
                obj.Dispose();
                return;
            }

            //可能已经被GameObject tree deleted了，不再回收
            if (obj.displayObject != null && obj.displayObject.isDisposed)
            {
                obj.Dispose();
                return;
            }

            obj.Release();
            if (obj is HtmlImage)
                _imagePool.Push(obj);
            else if (obj is HtmlInput)
                _inputPool.Push(obj);
            else if (obj is HtmlButton)
                _buttonPool.Push(obj);
            else if (obj is HtmlLink)
                _linkPool.Push(obj);
            else if (obj is HtmlSelect)
                _selectPool.Push(obj); // [Godot] missing in Unity too; there the orphan was destroyed with the pool GameObject

            if (obj.displayObject != null)
            {
                // [Godot] Unity: obj.displayObject.cachedTransform.SetParent(_poolManager, false), which parks the object
                // under the hidden pool GameObject. Here the object leaves the display list and its node is detached from
                // any Godot parent it still has (e.g. its home), so a pooled object stays an orphan and is never freed
                // together with its previous owner.
                obj.displayObject.RemoveFromParent();
                Node node = obj.displayObject.node;
                Node nodeParent = node.GetParent();
                if (nodeParent != null)
                    nodeParent.RemoveChild(node);
            }
        }

        virtual public NTexture GetImageTexture(HtmlImage image)
        {
            return null;
        }

        virtual public void FreeImageTexture(HtmlImage image, NTexture texture)
        {
        }

        /// <summary>
        /// [Godot] Added (no Unity counterpart). Disposes the objects pooled by this context and frees the orphan
        /// pool Node. In Unity the pool GameObject lives under the Stage and is destroyed with it; Godot does not free
        /// orphan nodes automatically. After this call pooling is off: objects are created fresh and disposed on free.
        /// </summary>
        /// <summary>
        /// [Godot] Frees the pooled objects and the pool node if the shared context was created (called when the
        /// application quits: Unity destroyed the pool GameObject with the Stage, Godot never frees orphan nodes).
        /// </summary>
        public static void DisposeInstance()
        {
            if (_inst != null)
                _inst.Dispose();
        }

        virtual public void Dispose()
        {
            DisposePool(_imagePool);
            DisposePool(_inputPool);
            DisposePool(_buttonPool);
            DisposePool(_selectPool);
            DisposePool(_linkPool);

            if (GodotObject.IsInstanceValid(_poolManager))
                _poolManager.Free();
            _poolManager = null;
        }

        static void DisposePool(Stack<IHtmlObject> pool)
        {
            while (pool.Count > 0)
                pool.Pop().Dispose();
        }
    }
}
