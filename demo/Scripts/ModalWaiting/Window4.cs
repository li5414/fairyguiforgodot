// Ported from FairyGUI-unity: Assets/Examples/ModalWaiting/Window4.cs
using FairyGUI;
using Window = FairyGUI.Window;

public class Window4 : Window
{
    public Window4()
    {
    }

    protected override void OnInit()
    {
        this.contentPane = UIPackage.CreateObject("ModalWaiting", "TestWin").asCom;
        this.contentPane.GetChild("n1").onClick.Add(OnClick);
    }

    void OnClick()
    {
        this.ShowModalWait();
        Timers.inst.Add(3, 1, (object param) =>
        {
            // [Godot] isDisposed check added: the example may be closed before the timer fires.
            if (!this.isDisposed)
                this.CloseModalWait();
        });
    }
}
