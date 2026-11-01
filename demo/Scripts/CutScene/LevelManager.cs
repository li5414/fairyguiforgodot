// Ported from FairyGUI-unity: Assets/Examples/CutScene/LevelManager.cs
using System.Collections;
using Godot;
using FairyGUI;

// [Godot] Was a MonoBehaviour singleton (DontDestroyOnLoad GameObject) that loaded Unity scenes with
// SceneManager.LoadSceneAsync. Now a plain class: the coroutine runs on Timers.inst.StartCoroutine and
// "loading a level" only drives the progress bar for 3 seconds (the demo has no level scenes to load).
public class LevelManager
{
    static LevelManager _instance;
    public static LevelManager inst
    {
        get
        {
            if (_instance == null)
                _instance = new LevelManager();
            return _instance;
        }
    }

    GComponent _cutSceneView;
    GComponent _mainView;

    public LevelManager()
    {
    }

    public void Init()
    {
        // [Godot] Init may run again when the example is reopened: release the views of the previous run.
        DisposeViews();

        _cutSceneView = UIPackage.CreateObject("CutScene", "CutScene").asCom;
        _cutSceneView.SetSize(GRoot.inst.width, GRoot.inst.height);
        _cutSceneView.AddRelation(GRoot.inst, RelationType.Size);

        _mainView = UIPackage.CreateObject("CutScene", "Main").asCom;
        _mainView.SetSize(GRoot.inst.width, GRoot.inst.height);
        _mainView.AddRelation(GRoot.inst, RelationType.Size);

        _mainView.GetChild("n0").onClick.Add(() =>
        {
            LoadLevel("scene1");
        });

        _mainView.GetChild("n1").onClick.Add(() =>
        {
            LoadLevel("scene2");
        });
    }

    public void LoadLevel(string levelName)
    {
        Timers.inst.StartCoroutine(DoLoad(levelName));
        GRoot.inst.AddChild(_cutSceneView);
    }

    IEnumerator DoLoad(string sceneName)
    {
        // [Godot] The views captured here are checked with isDisposed: the example can be closed while loading.
        GComponent cutSceneView = _cutSceneView;
        GComponent mainView = _mainView;

        GRoot.inst.AddChild(cutSceneView);
        GProgressBar pb = cutSceneView.GetChild("pb").asProgress;
        pb.value = 0;
        // [Godot] SceneManager.LoadSceneAsync(sceneName) removed: there is no level scene to load, so the
        // operation counts as done at once and only the 3 second progress animation remains.
        bool opDone = true;
        float startTime = FrameTime.time;
        while (!opDone || pb.value != 100)
        {
            if (pb.isDisposed)
                yield break;

            int value = (int)((FrameTime.time - startTime) * 100f / 3f);
            if (value > 100)
                value = 100;
            pb.value = value;
            yield return null;
        }

        if (cutSceneView.isDisposed || mainView.isDisposed)
            yield break;

        GRoot.inst.RemoveChild(cutSceneView);
        GRoot.inst.AddChild(mainView);
    }

    // [Godot] New: called when the example node leaves the tree (Unity destroyed everything with the scene).
    public void Release()
    {
        DisposeViews();
        _instance = null;
    }

    void DisposeViews()
    {
        if (_cutSceneView != null)
        {
            _cutSceneView.Dispose();
            _cutSceneView = null;
        }
        if (_mainView != null)
        {
            _mainView.Dispose();
            _mainView = null;
        }
    }
}
