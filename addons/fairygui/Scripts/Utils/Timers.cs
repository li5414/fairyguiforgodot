// Ported from FairyGUI-unity: Assets/Scripts/Utils/Timers.cs
using System.Collections.Generic;
using System.Collections;
using Godot;

namespace FairyGUI
{
    public delegate void TimerCallback(object param);

    /// <summary>
    ///
    /// </summary>
    public class Timers
    {
        public static int repeat;
        public static float time;

        public static bool catchCallbackExceptions = false;

        Dictionary<TimerCallback, Anymous_T> _items;
        Dictionary<TimerCallback, Anymous_T> _toAdd;
        List<Anymous_T> _toRemove;
        List<Anymous_T> _pool;

        // [Godot] The hidden "[FairyGUI.Timers]" GameObject and its TimersEngine MonoBehaviour are removed.
        // Update() is driven by StageEngine._Process; coroutines (StartCoroutine) run on a small built-in runner.
        List<CoroutineRunner> _coroutines;

        private static Timers _inst;
        public static Timers inst
        {
            get
            {
                if (_inst == null)
                {
                    // [Godot] Make sure the StageEngine that drives Update() exists. Stage's constructor
                    // itself uses Timers.inst, so _inst may already be set when this returns.
                    Stage.Instantiate();
                    if (_inst == null)
                        _inst = new Timers();
                }
                return _inst;
            }
        }

        // [Godot] RuntimeInitializeOnLoadMethod (domain-reload reset) removed: Godot resets statics on assembly reload.

        public Timers()
        {
            _inst = this;

            _items = new Dictionary<TimerCallback, Anymous_T>();
            _toAdd = new Dictionary<TimerCallback, Anymous_T>();
            _toRemove = new List<Anymous_T>();
            _pool = new List<Anymous_T>(100);
            _coroutines = new List<CoroutineRunner>();
        }

        public void Add(float interval, int repeat, TimerCallback callback)
        {
            Add(interval, repeat, callback, null);
        }

        /**
         * @interval in seconds
         * @repeat 0 indicate loop infinitely, otherwise the run count
         **/
        public void Add(float interval, int repeat, TimerCallback callback, object callbackParam)
        {
            if (callback == null)
            {
                GD.PushWarning("timer callback is null, " + interval + "," + repeat);
                return;
            }

            Anymous_T t;
            if (_items.TryGetValue(callback, out t))
            {
                t.set(interval, repeat, callback, callbackParam);
                t.elapsed = 0;
                t.deleted = false;
                return;
            }

            if (_toAdd.TryGetValue(callback, out t))
            {
                t.set(interval, repeat, callback, callbackParam);
                return;
            }

            t = GetFromPool();
            t.interval = interval;
            t.repeat = repeat;
            t.callback = callback;
            t.param = callbackParam;
            _toAdd[callback] = t;
        }

        public void CallLater(TimerCallback callback)
        {
            Add(0.001f, 1, callback);
        }

        public void CallLater(TimerCallback callback, object callbackParam)
        {
            Add(0.001f, 1, callback, callbackParam);
        }

        public void AddUpdate(TimerCallback callback)
        {
            Add(0.001f, 0, callback);
        }

        public void AddUpdate(TimerCallback callback, object callbackParam)
        {
            Add(0.001f, 0, callback, callbackParam);
        }

        // [Godot] Unity ran the routine on the hidden MonoBehaviour. Here it runs on the built-in runner with
        // Unity's main semantics: the routine runs synchronously up to its first yield; afterwards it is resumed
        // once per frame from Update(). Supported yields: null / any object (wait one frame),
        // WaitForSeconds (scaled time, like Unity), and a nested IEnumerator (run to completion first).
        // An exception stops the routine and is logged, as in Unity.
        public void StartCoroutine(IEnumerator routine)
        {
            if (routine == null)
                return;

            CoroutineRunner runner = new CoroutineRunner(routine);
            runner.startFrame = FrameTime.frameCount;
            if (runner.Step())
                _coroutines.Add(runner);
        }

        public bool Exists(TimerCallback callback)
        {
            if (_toAdd.ContainsKey(callback))
                return true;

            Anymous_T at;
            if (_items.TryGetValue(callback, out at))
                return !at.deleted;

            return false;
        }

        public void Remove(TimerCallback callback)
        {
            Anymous_T t;
            if (_toAdd.TryGetValue(callback, out t))
            {
                _toAdd.Remove(callback);
                ReturnToPool(t);
            }

            if (_items.TryGetValue(callback, out t))
                t.deleted = true;
        }

        private Anymous_T GetFromPool()
        {
            Anymous_T t;
            int cnt = _pool.Count;
            if (cnt > 0)
            {
                t = _pool[cnt - 1];
                _pool.RemoveAt(cnt - 1);
                t.deleted = false;
                t.elapsed = 0;
            }
            else
                t = new Anymous_T();
            return t;
        }

        private void ReturnToPool(Anymous_T t)
        {
            t.callback = null;
            _pool.Add(t);
        }

        public void Update()
        {
            float dt = FrameTime.unscaledDeltaTime;
            Dictionary<TimerCallback, Anymous_T>.Enumerator iter;

            if (_items.Count > 0)
            {
                iter = _items.GetEnumerator();
                while (iter.MoveNext())
                {
                    Anymous_T i = iter.Current.Value;
                    if (i.deleted)
                    {
                        _toRemove.Add(i);
                        continue;
                    }

                    i.elapsed += dt;
                    if (i.elapsed < i.interval)
                        continue;

                    i.elapsed -= i.interval;
                    if (i.elapsed < 0 || i.elapsed > 0.03f)
                        i.elapsed = 0;

                    if (i.repeat > 0)
                    {
                        i.repeat--;
                        if (i.repeat == 0)
                        {
                            i.deleted = true;
                            _toRemove.Add(i);
                        }
                    }
                    repeat = i.repeat;
                    if (i.callback != null)
                    {
                        if (catchCallbackExceptions)
                        {
                            try
                            {
                                i.callback(i.param);
                            }
                            catch (System.Exception e)
                            {
                                i.deleted = true;
                                GD.PushWarning("FairyGUI: timer(internal=" + i.interval + ", repeat=" + i.repeat + ") callback error > " + e.Message);
                            }
                        }
                        else
                            i.callback(i.param);
                    }
                }
                iter.Dispose();
            }

            int len = _toRemove.Count;
            if (len > 0)
            {
                for (int k = 0; k < len; k++)
                {
                    Anymous_T i = _toRemove[k];
                    if (i.deleted && i.callback != null)
                    {
                        _items.Remove(i.callback);
                        ReturnToPool(i);
                    }
                }
                _toRemove.Clear();
            }

            if (_toAdd.Count > 0)
            {
                iter = _toAdd.GetEnumerator();
                while (iter.MoveNext())
                    _items.Add(iter.Current.Key, iter.Current.Value);
                iter.Dispose();
                _toAdd.Clear();
            }

            UpdateCoroutines();
        }

        // [Godot] Resumes the coroutines started with StartCoroutine (replaces TimersEngine/MonoBehaviour).
        void UpdateCoroutines()
        {
            int cnt = _coroutines.Count;
            if (cnt == 0)
                return;

            int frame = FrameTime.frameCount;
            float now = FrameTime.time;
            int i = 0;
            while (i < _coroutines.Count)
            {
                CoroutineRunner runner = _coroutines[i];
                //started in this frame: the first step already ran synchronously in StartCoroutine
                if (runner.startFrame == frame || runner.waitUntil > now)
                {
                    i++;
                    continue;
                }

                if (runner.Step())
                    i++;
                else
                    _coroutines.RemoveAt(i);
            }
        }

        // [Godot] Minimal IEnumerator coroutine runner (replaces MonoBehaviour.StartCoroutine).
        class CoroutineRunner
        {
            Stack<IEnumerator> _stack;
            public float waitUntil;
            public int startFrame;

            public CoroutineRunner(IEnumerator routine)
            {
                _stack = new Stack<IEnumerator>();
                _stack.Push(routine);
            }

            /// <summary>
            /// Run until the next yield. Returns false when the routine has finished.
            /// </summary>
            public bool Step()
            {
                waitUntil = 0;
                while (_stack.Count > 0)
                {
                    IEnumerator top = _stack.Peek();
                    bool hasNext;
                    try
                    {
                        hasNext = top.MoveNext();
                    }
                    catch (System.Exception e)
                    {
                        GD.PushError("FairyGUI: coroutine error > " + e);
                        _stack.Clear();
                        return false;
                    }

                    if (!hasNext)
                    {
                        _stack.Pop();
                        continue;
                    }

                    object current = top.Current;
                    if (current is IEnumerator)
                    {
                        _stack.Push((IEnumerator)current);
                        continue;
                    }

                    if (current is WaitForSeconds)
                        waitUntil = FrameTime.time + ((WaitForSeconds)current).seconds;

                    return true;
                }

                return false;
            }
        }
    }

    class Anymous_T
    {
        public float interval;
        public int repeat;
        public TimerCallback callback;
        public object param;

        public float elapsed;
        public bool deleted;

        public void set(float interval, int repeat, TimerCallback callback, object param)
        {
            this.interval = interval;
            this.repeat = repeat;
            this.callback = callback;
            this.param = param;
        }
    }

    // [Godot] class TimersEngine : MonoBehaviour removed (StageEngine calls Timers.inst.Update()).

    /// <summary>
    /// Yield instruction for coroutines started with Timers.inst.StartCoroutine.
    /// Waits the given number of seconds in scaled time (FrameTime.time), like UnityEngine.WaitForSeconds.
    /// </summary>
    // [Godot] New: replaces UnityEngine.WaitForSeconds so ported coroutines (e.g. TypingEffect.Print) compile unchanged.
    public sealed class WaitForSeconds
    {
        public readonly float seconds;

        public WaitForSeconds(float seconds)
        {
            this.seconds = seconds;
        }
    }
}
