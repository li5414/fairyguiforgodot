// Ported from FairyGUI-unity: Assets/Scripts/Core/UpdateContext.cs
using System;
using System.Collections.Generic;
using Godot;
using FairyGUI.Utils;

namespace FairyGUI
{
    /// <summary>
    /// UpdateContext is for internal use.
    /// </summary>
    // [Godot] Rect clipping is done by CanvasItemSetClip on the container node and masks by ClipChildren /
    // CanvasGroup (see Container). The clip and stencil values are still computed exactly like the Unity version
    // so that NGraphics and other code can read them, but there is no stencil buffer to configure.
    public class UpdateContext
    {
        public struct ClipInfo
        {
            public Rect rect;
            public Vector4 clipBox;
            public bool soft;
            public Vector4 softness;//left-top-right-bottom
            public uint clipId;
            public int rectMaskDepth;
            public int referenceValue;
            public bool reversed;
        }

        Stack<ClipInfo> _clipStack;

        public bool clipped;
        public ClipInfo clipInfo;

        public int renderingOrder;
        public int batchingDepth;
        public int rectMaskDepth;
        public int stencilReferenceValue;
        public int stencilCompareValue;

        public float alpha;
        public bool grayed;

        public static UpdateContext current;
        public static bool working;

        public static event Action OnBegin;
        public static event Action OnEnd;

        static Action _tmpBegin;

        public UpdateContext()
        {
            _clipStack = new Stack<ClipInfo>();
        }

        /// <summary>
        ///
        /// </summary>
        public void Begin()
        {
            current = this;

            renderingOrder = 0;
            batchingDepth = 0;
            rectMaskDepth = 0;
            stencilReferenceValue = 0;
            alpha = 1;
            grayed = false;

            clipped = false;
            _clipStack.Clear();

            Stats.ObjectCount = 0;
            Stats.GraphicsCount = 0;

            _tmpBegin = OnBegin;
            OnBegin = null;

            //允许OnBegin里再次Add，这里没有做死锁检查
            while (_tmpBegin != null)
            {
                _tmpBegin.Invoke();
                _tmpBegin = OnBegin;
                OnBegin = null;
            }

            working = true;
        }

        /// <summary>
        ///
        /// </summary>
        public void End()
        {
            working = false;

            if (OnEnd != null)
                OnEnd.Invoke();

            OnEnd = null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="clipId"></param>
        /// <param name="clipRect"></param>
        /// <param name="softness"></param>
        public void EnterClipping(uint clipId, Rect clipRect, Vector4? softness)
        {
            _clipStack.Push(clipInfo);

            if (rectMaskDepth > 0)
                clipRect = ToolSet.Intersection(ref clipInfo.rect, ref clipRect);

            clipped = true;
            clipInfo.rectMaskDepth = ++rectMaskDepth;
            /* clipPos = xy * clipBox.zw + clipBox.xy
                * 利用这个公式，使clipPos变为当前顶点距离剪切区域中心的距离值，剪切区域的大小为2x2
                * 那么abs(clipPos)>1的都是在剪切区域外
                */
            clipInfo.rect = clipRect;
            clipRect.x = clipRect.x + clipRect.width * 0.5f;
            clipRect.y = clipRect.y + clipRect.height * 0.5f;
            clipRect.width *= 0.5f;
            clipRect.height *= 0.5f;
            if (clipRect.width == 0 || clipRect.height == 0)
                clipInfo.clipBox = new Vector4(-2, -2, 0, 0);
            else
                clipInfo.clipBox = new Vector4(-clipRect.x / clipRect.width, -clipRect.y / clipRect.height,
                    1.0f / clipRect.width, 1.0f / clipRect.height);
            clipInfo.clipId = clipId;
            clipInfo.soft = softness != null;
            if (clipInfo.soft)
            {
                // [Godot] clipSoftness is not rendered (Godot clips hard); the values are kept for API parity.
                // Screen.height -> Stage.inst.size.y
                float screenHeight = Stage.inst.size.Y;
                clipInfo.softness = (Vector4)softness;
                float vx = clipInfo.rect.width * screenHeight * 0.25f;
                float vy = clipInfo.rect.height * screenHeight * 0.25f;

                if (clipInfo.softness.X > 0)
                    clipInfo.softness.X = vx / clipInfo.softness.X;
                else
                    clipInfo.softness.X = 10000f;

                if (clipInfo.softness.Y > 0)
                    clipInfo.softness.Y = vy / clipInfo.softness.Y;
                else
                    clipInfo.softness.Y = 10000f;

                if (clipInfo.softness.Z > 0)
                    clipInfo.softness.Z = vx / clipInfo.softness.Z;
                else
                    clipInfo.softness.Z = 10000f;

                if (clipInfo.softness.W > 0)
                    clipInfo.softness.W = vy / clipInfo.softness.W;
                else
                    clipInfo.softness.W = 10000f;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="clipId"></param>
        /// <param name="reversedMask"></param>
        public void EnterClipping(uint clipId, bool reversedMask)
        {
            _clipStack.Push(clipInfo);

            if (stencilReferenceValue == 0)
                stencilReferenceValue = 1;
            else
                stencilReferenceValue = stencilReferenceValue << 1;

            if (reversedMask)
            {
                if (clipInfo.reversed)
                    stencilCompareValue = (stencilReferenceValue >> 1) - 1;
                else
                    stencilCompareValue = stencilReferenceValue - 1;
            }
            else
                stencilCompareValue = (stencilReferenceValue << 1) - 1;

            clipInfo.clipId = clipId;
            clipInfo.referenceValue = stencilReferenceValue;
            clipInfo.reversed = reversedMask;
            clipped = true;
        }

        /// <summary>
        ///
        /// </summary>
        public void LeaveClipping()
        {
            clipInfo = _clipStack.Pop();
            stencilReferenceValue = clipInfo.referenceValue;
            rectMaskDepth = clipInfo.rectMaskDepth;
            clipped = stencilReferenceValue != 0 || rectMaskDepth != 0;
        }

        public void EnterPaintingMode()
        {
            //Reset clipping
            _clipStack.Push(clipInfo);

            clipInfo.rectMaskDepth = 0;
            clipInfo.referenceValue = 0;
            clipInfo.reversed = false;
            clipped = false;
        }

        public void LeavePaintingMode()
        {
            clipInfo = _clipStack.Pop();
            stencilReferenceValue = clipInfo.referenceValue;
            rectMaskDepth = clipInfo.rectMaskDepth;
            clipped = stencilReferenceValue != 0 || rectMaskDepth != 0;
        }

        public void ApplyClippingProperties(Material mat, bool isStdMaterial)
        {
            // [Godot] Degraded to no-op. Unity wrote _ClipBox/_ClipSoftness and the stencil state into every
            // material and toggled the CLIPPED/SOFT_CLIPPED keywords. In Godot, rect clipping is applied once on
            // the clipping container's canvas item (RenderingServer.CanvasItemSetClip) and masks use
            // ClipChildren/CanvasGroup, both of which affect every descendant whatever its material is.
            // So no per-material state is needed. Soft clipping is not supported.
        }

        public void ApplyAlphaMaskProperties(Material mat, bool erasing)
        {
            // [Godot] Degraded to no-op. Unity configured the stencil write/erase passes of an alpha mask here.
            // Godot has no stencil buffer for canvas items; Container implements masks with
            // CanvasItem.ClipChildren (normal mask) or a CanvasGroup with a multiply "eraser" (reversed mask).
        }
    }
}
