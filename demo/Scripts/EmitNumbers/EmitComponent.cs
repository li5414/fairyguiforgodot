// Ported from FairyGUI-unity: Assets/Examples/EmitNumbers/EmitComponent.cs
using Godot;
using FairyGUI;

public class EmitComponent : GComponent
{
    GLoader _symbolLoader;
    GTextField _numberText;
    Node2D _owner; // [Godot] Transform -> Node2D

    // [Godot] Unity added 2.2 world units (above the model's head) to the owner position. The 2D npc placeholder
    // has its origin at the feet and its head top at DemoUtil.NpcHeadTop pixels.
    const float OFFSET_ADDITION = -DemoUtil.NpcHeadTop;
    static Vector2 JITTER_FACTOR = new Vector2(80, 80);

    public EmitComponent()
    {
        this.touchable = false;

        _symbolLoader = new GLoader();
        _symbolLoader.autoSize = true;
        AddChild(_symbolLoader);

        _numberText = new GTextField();
        _numberText.autoSize = AutoSizeType.Both;

        AddChild(_numberText);
    }

    public void SetHurt(Node2D owner, int type, long hurt, bool critical)
    {
        _owner = owner;

        TextFormat tf = _numberText.textFormat;
        if (type == 0)
            tf.font = EmitManager.inst.hurtFont1;
        else
            tf.font = EmitManager.inst.hurtFont2;
        _numberText.textFormat = tf;
        _numberText.text = "-" + hurt;

        if (critical)
            _symbolLoader.url = EmitManager.inst.criticalSign;
        else
            _symbolLoader.url = "";

        UpdateLayout();

        this.alpha = 1;
        UpdatePosition(Vector2.Zero);
        Vector2 rnd = DemoUtil.InsideUnitCircle() * JITTER_FACTOR; // Vector2.Scale(Random.insideUnitCircle, JITTER_FACTOR)
        int toX = (int)rnd.X * 2;
        int toY = (int)rnd.Y * 2;

        EmitManager.inst.view.AddChild(this);
        GTween.To(Vector2.Zero, new Vector2(toX, toY), 1f).SetTarget(this)
            .OnUpdate((GTweener tweener) => { this.UpdatePosition(tweener.value.vec2); }).OnComplete(this.OnCompleted);
        this.TweenFade(0, 0.5f).SetDelay(0.5f);
    }

    void UpdateLayout()
    {
        this.SetSize(_symbolLoader.width + _numberText.width, UMath.Max(_symbolLoader.height, _numberText.height));
        _numberText.SetXY(_symbolLoader.width > 0 ? (_symbolLoader.width + 2) : 0,
        (this.height - _numberText.height) / 2);
        _symbolLoader.y = (this.height - _symbolLoader.height) / 2;
    }

    void UpdatePosition(Vector2 pos)
    {
        // [Godot] Camera.main.WorldToScreenPoint + y flip -> the owner's canvas position is already in viewport
        // (stage) coordinates, y down, so "above" is -y.
        if (_owner == null || !GodotObject.IsInstanceValid(_owner) || !_owner.IsInsideTree())
            return;

        Vector2 ownerPos = _owner.GetGlobalTransformWithCanvas().Origin;
        ownerPos.Y -= OFFSET_ADDITION;
        Vector2 screenPos = ownerPos;

        Vector2 pt = GRoot.inst.GlobalToLocal(screenPos);
        this.SetXY(UMath.RoundToInt(pt.X + pos.X - this.actualWidth / 2), UMath.RoundToInt(pt.Y + pos.Y - this.height));
    }

    void OnCompleted()
    {
        _owner = null;
        EmitManager.inst.view.RemoveChild(this);
        EmitManager.inst.ReturnComponent(this);
    }

    public void Cancel()
    {
        _owner = null;
        if (this.parent != null)
        {
            GTween.Kill(this);
            EmitManager.inst.view.RemoveChild(this);
        }
        EmitManager.inst.ReturnComponent(this);
    }
}
