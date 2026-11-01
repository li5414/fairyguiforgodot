// Ported from FairyGUI-unity: Assets/Examples/Bag/MyGLoader.cs
using Godot;
using FairyGUI;

/// <summary>
/// Extend the ability of GLoader
/// </summary>
public class MyGLoader : GLoader
{
    protected override void LoadExternal()
    {
        IconManager.inst.LoadIcon(this.url, OnLoadSuccess, OnLoadFail);
    }

    protected override void FreeExternal(NTexture texture)
    {
        texture.refCount--;
    }

    void OnLoadSuccess(NTexture texture)
    {
        // [Godot] isDisposed check added: the loader may be disposed (example closed) while the icon is loading.
        if (this.isDisposed || string.IsNullOrEmpty(this.url))
            return;

        this.onExternalLoadSuccess(texture);
    }

    void OnLoadFail(string error)
    {
        if (this.isDisposed)
            return;

        GD.Print("load " + this.url + " failed: " + error);
        this.onExternalLoadFailed();
    }
}
