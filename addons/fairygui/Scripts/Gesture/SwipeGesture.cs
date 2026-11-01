// Ported from FairyGUI-unity: Assets/Scripts/Gesture/SwipeGesture.cs
using System;
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    /// <summary>
    /// 滑动手势。你可以通过onBegin+onMove+onEnd关心整个滑动过程，也可以只使用onAction关注最后的滑动结果。滑动结果包括方向和加速度，可以从position和velocity获得。
    /// 注意onAction仅当滑动超过一定距离(actionDistance)时才触发。
    /// </summary>
    public class SwipeGesture : EventDispatcher
    {
        /// <summary>
        ///
        /// </summary>
        public GObject host { get; private set; }

        /// <summary>
        /// 当手指开始扫动时派发该事件。
        /// </summary>
        public EventListener onBegin { get; private set; }
        /// <summary>
        /// 手指离开屏幕时派发该事件。
        /// </summary>
        public EventListener onEnd { get; private set; }
        /// <summary>
        /// 手指在滑动时派发该事件。
        /// </summary>
        public EventListener onMove { get; private set; }
        /// <summary>
        /// 当手指从按下到离开经过的距离大于actionDistance时派发该事件。
        /// </summary>
        public EventListener onAction { get; private set; }

        /// <summary>
        /// 手指离开时的加速度
        /// </summary>
        public Vector2 velocity;

        /// <summary>
        /// 你可以在onBegin事件中设置这个值，那个后续将根据手指移动的距离修改这个值。如果不设置，那position初始为(0,0)，反映手指扫过的距离。
        /// </summary>
        public Vector2 position;

        /// <summary>
        /// 移动的变化值
        /// </summary>
        public Vector2 delta;

        /// <summary>
        /// The min distance to fire onAction event
        /// 派发onAction事件的最小距离。如果手指扫过的距离少于此值，onAction不会触发（但onEnd仍然会派发）
        /// </summary>
        public int actionDistance;

        /// <summary>
        /// 是否把变化量强制为整数。默认true。
        /// </summary>
        public bool snapping;

        Vector2 _startPoint;
        Vector2 _lastPoint;
        float _time;
        bool _started;
        bool _touchBegan;

        public static int ACTION_DISTANCE = 200;

        public SwipeGesture(GObject host)
        {
            this.host = host;
            actionDistance = ACTION_DISTANCE;
            snapping = true;
            Enable(true);

            onBegin = new EventListener(this, "onSwipeBegin");
            onEnd = new EventListener(this, "onSwipeEnd");
            onMove = new EventListener(this, "onSwipeMove");
            onAction = new EventListener(this, "onnSwipeAction");
        }

        public void Dispose()
        {
            Enable(false);
            host = null;
        }

        public void Enable(bool value)
        {
            if (value)
            {
                if (host == GRoot.inst)
                {
                    Stage.inst.onTouchBegin.Add(__touchBegin);
                    Stage.inst.onTouchMove.Add(__touchMove);
                    Stage.inst.onTouchEnd.Add(__touchEnd);
                }
                else
                {
                    host.onTouchBegin.Add(__touchBegin);
                    host.onTouchMove.Add(__touchMove);
                    host.onTouchEnd.Add(__touchEnd);
                }
            }
            else
            {
                _started = false;
                _touchBegan = false;
                if (host == GRoot.inst)
                {
                    Stage.inst.onTouchBegin.Remove(__touchBegin);
                    Stage.inst.onTouchMove.Remove(__touchMove);
                    Stage.inst.onTouchEnd.Remove(__touchEnd);
                }
                else
                {
                    host.onTouchBegin.Remove(__touchBegin);
                    host.onTouchMove.Remove(__touchMove);
                    host.onTouchEnd.Remove(__touchEnd);
                }
            }
        }

        void __touchBegin(EventContext context)
        {
            if (Stage.inst.touchCount > 1)
            {
                _touchBegan = false;
                if (_started)
                {
                    _started = false;
                    onEnd.Call(context.inputEvent);
                }
                return;
            }

            InputEvent evt = context.inputEvent;
            _startPoint = _lastPoint = host.GlobalToLocal(new Vector2(evt.x, evt.y));
            _lastPoint = _startPoint;

            _time = FrameTime.unscaledTime;
            _started = false;
            velocity = Vector2.Zero;
            position = Vector2.Zero;
            _touchBegan = true;

            context.CaptureTouch();
        }

        void __touchMove(EventContext context)
        {
            if (!_touchBegan || Stage.inst.touchCount > 1)
                return;

            InputEvent evt = context.inputEvent;
            Vector2 pt = host.GlobalToLocal(new Vector2(evt.x, evt.y));
            delta = pt - _lastPoint;
            if (snapping)
            {
                delta.X = UMath.Round(delta.X);
                delta.Y = UMath.Round(delta.Y);
                if (delta.X == 0 && delta.Y == 0)
                    return;
            }

            float deltaTime = FrameTime.unscaledDeltaTime;
            float elapsed = (FrameTime.unscaledTime - _time) * 60 - 1;
            if (elapsed > 1) //速度衰减
                velocity = velocity * UMath.Pow(0.833f, elapsed);
            // Unity: Vector3.Lerp on implicitly converted Vector2s (z is 0), so the clamped 2D lerp is equivalent.
            velocity = UVec.Lerp(velocity, delta / deltaTime, deltaTime * 10);
            _time = FrameTime.unscaledTime;
            position += delta;
            _lastPoint = pt;

            if (!_started)
            { //灵敏度检查，为了和点击区分
                int sensitivity;
                if (Stage.touchScreen)
                    sensitivity = UIConfig.touchDragSensitivity;
                else
                    sensitivity = 5;

                if (UMath.Abs(delta.X) < sensitivity && UMath.Abs(delta.Y) < sensitivity)
                    return;

                _started = true;
                onBegin.Call(evt);
            }

            onMove.Call(evt);
        }

        void __touchEnd(EventContext context)
        {
            _touchBegan = false;

            if (!_started)
                return;

            _started = false;

            InputEvent evt = context.inputEvent;
            Vector2 pt = host.GlobalToLocal(new Vector2(evt.x, evt.y));
            delta = pt - _lastPoint;
            if (snapping)
            {
                delta.X = UMath.Round(delta.X);
                delta.Y = UMath.Round(delta.Y);
            }
            position += delta;

            //更新速度
            float elapsed = (FrameTime.unscaledTime - _time) * 60 - 1;
            if (elapsed > 1)
                velocity = velocity * UMath.Pow(0.833f, elapsed);
            if (snapping)
            {
                velocity.X = UMath.Round(velocity.X);
                velocity.Y = UMath.Round(velocity.Y);
            }
            onEnd.Call(evt);

            pt -= _startPoint;
            if (UMath.Abs(pt.X) > actionDistance || UMath.Abs(pt.Y) > actionDistance)
                onAction.Call(evt);
        }
    }
}