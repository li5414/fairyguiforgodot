// Ported from FairyGUI-unity: Assets/Examples/EmitNumbers/EmitManager.cs
using System.Collections.Generic;
using Godot;
using FairyGUI;

public class EmitManager
{
    static EmitManager _instance;
    public static EmitManager inst
    {
        get
        {
            if (_instance == null)
                _instance = new EmitManager();
            return _instance;
        }
    }

    public string hurtFont1;
    public string hurtFont2;
    public string criticalSign;

    public GComponent view { get; private set; }

    private readonly Stack<EmitComponent> _componentPool = new Stack<EmitComponent>();

    public EmitManager()
    {
        hurtFont1 = "ui://EmitNumbers/number1";
        hurtFont2 = "ui://EmitNumbers/number2";
        criticalSign = "ui://EmitNumbers/critical";

        view = new GComponent();
        GRoot.inst.AddChild(view);
    }

    // [Godot] Transform owner -> Node2D owner (the 2D npc placeholder)
    public void Emit(Node2D owner, int type, long hurt, bool critical)
    {
        EmitComponent ec;
        if (_componentPool.Count > 0)
            ec = _componentPool.Pop();
        else
            ec = new EmitComponent();
        ec.SetHurt(owner, type, hurt, critical);
    }

    public void ReturnComponent(EmitComponent com)
    {
        _componentPool.Push(com);
    }

    // [Godot] New: called when the example node leaves the tree. Disposing the view disposes the flying numbers
    // (their tweens are killed with them); pooled components are not on stage and are disposed separately.
    public static void Release()
    {
        if (_instance == null)
            return;

        EmitManager mgr = _instance;
        _instance = null;

        foreach (EmitComponent ec in mgr._componentPool)
        {
            if (!ec.isDisposed)
                ec.Dispose();
        }
        mgr._componentPool.Clear();

        mgr.view.Dispose();
        mgr.view = null;
    }
}
