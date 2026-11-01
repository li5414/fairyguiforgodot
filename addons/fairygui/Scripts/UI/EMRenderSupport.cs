// Ported from FairyGUI-unity: Assets/Scripts/UI/EMRenderSupport.cs
using System;
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    public interface EMRenderTarget
    {
        int EM_sortingOrder { get; }

        void EM_BeforeUpdate();
        void EM_Update(UpdateContext context);
        void EM_Reload();
    }

    /// <summary>
    /// 这是一个在编辑状态下渲染UI的功能类。EM=Edit Mode
    /// </summary>
    // [Godot] Edit-mode rendering does not exist in the Godot port (UIPanel/UIPainter are not [Tool] scripts and
    // nothing calls Update() from the editor). The type is kept because UIPanel/UIPainter/UIConfig reference it;
    // at runtime every entry point returns immediately, exactly like Unity's play mode.
    public class EMRenderSupport
    {
        /// <summary>
        ///
        /// </summary>
        public static bool orderChanged;

        static UpdateContext _updateContext;
        static List<EMRenderTarget> _targets = new List<EMRenderTarget>();

        /// <summary>
        ///
        /// </summary>
        public static bool packageListReady { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public static bool hasTarget
        {
            get { return _targets.Count > 0; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        public static void Add(EMRenderTarget value)
        {
            if (!_targets.Contains(value))
                _targets.Add(value);
            orderChanged = true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        public static void Remove(EMRenderTarget value)
        {
            _targets.Remove(value);
        }

        /// <summary>
        /// 由StageCamera调用
        /// </summary>
        // [Godot] Nothing calls this in Godot (StageCamera.OnRenderObject does not exist).
        public static void Update()
        {
            // [Godot] Application.isPlaying -> !Engine.IsEditorHint()
            if (!Engine.IsEditorHint())
                return;

            if (_updateContext == null)
                _updateContext = new UpdateContext();

            if (orderChanged)
            {
                _targets.Sort(CompareDepth);
                orderChanged = false;
            }

            int cnt = _targets.Count;
            for (int i = 0; i < cnt; i++)
            {
                EMRenderTarget panel = _targets[i];
                panel.EM_BeforeUpdate();
            }

            if (packageListReady)
            {
                _updateContext.Begin();
                for (int i = 0; i < cnt; i++)
                {
                    EMRenderTarget panel = _targets[i];
                    panel.EM_Update(_updateContext);
                }
                _updateContext.End();
            }
        }

        /// <summary>
        /// 当发生二进制重载时，或用户点击刷新菜单
        /// </summary>
        public static void Reload()
        {
            // [Godot] Application.isPlaying -> !Engine.IsEditorHint()
            if (!Engine.IsEditorHint())
                return;

            UIConfig.ClearResourceRefs();
            // [Godot] GameObject.FindObjectsOfType<UIConfig>() -> walk the scene tree for UIConfig nodes.
            List<UIConfig> configs = new List<UIConfig>();
            SceneTree tree = Engine.GetMainLoop() as SceneTree;
            if (tree != null && tree.Root != null)
                CollectConfigs(tree.Root, configs);
            foreach (UIConfig config in configs)
                config.Load();

            packageListReady = true;

            int cnt = _targets.Count;
            for (int i = 0; i < cnt; i++)
            {
                EMRenderTarget panel = _targets[i];
                panel.EM_Reload();
            }
        }

        // [Godot] Helper for Reload (replaces FindObjectsOfType).
        static void CollectConfigs(Node node, List<UIConfig> result)
        {
            if (node is UIConfig)
                result.Add((UIConfig)node);

            int cnt = node.GetChildCount();
            for (int i = 0; i < cnt; i++)
                CollectConfigs(node.GetChild(i), result);
        }

        static int CompareDepth(EMRenderTarget c1, EMRenderTarget c2)
        {
            return c1.EM_sortingOrder - c2.EM_sortingOrder;
        }
    }
}
