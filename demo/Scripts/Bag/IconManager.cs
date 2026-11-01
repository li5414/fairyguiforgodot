// Ported from FairyGUI-unity: Assets/Examples/Bag/IconManager.cs
using System.Collections;
using System.Collections.Generic;
using Godot;
using FairyGUI;

public delegate void LoadCompleteCallback(NTexture texture);
public delegate void LoadErrorCallback(string error);

/// <summary>
/// Use to load icons from asset bundle, and pool them
/// </summary>
// [Godot] Was a MonoBehaviour singleton (DontDestroyOnLoad GameObject) loading AssetBundles from StreamingAssets with
// UnityWebRequest. Now a plain class: coroutines run on Timers.inst.StartCoroutine and icons are loaded from
// res://demo/Icons/<name>.png with ResourceLoader.LoadThreadedRequest (still asynchronous, still pooled).
public class IconManager
{
    static IconManager _instance;
    public static IconManager inst
    {
        get
        {
            if (_instance == null)
                _instance = new IconManager();
            return _instance;
        }
    }

    public const int POOL_CHECK_TIME = 30;
    public const int MAX_POOL_SIZE = 10;

    List<LoadItem> _items;
    bool _started;
    Dictionary<string, NTexture> _pool; // [Godot] Hashtable -> Dictionary
    string _basePath;

    // [Godot] Awake
    public IconManager()
    {
        _items = new List<LoadItem>();
        _pool = new Dictionary<string, NTexture>();
        // [Godot] Application.streamingAssetsPath + "/fairygui-examples/" (*.ab bundles) -> the imported pngs.
        _basePath = DemoUtil.IconPath;

        Timers.inst.StartCoroutine(FreeIdleIcons());
    }

    public void LoadIcon(string url,
                    LoadCompleteCallback onSuccess,
                    LoadErrorCallback onFail)
    {
        LoadItem item = new LoadItem();
        item.url = url;
        item.onSuccess = onSuccess;
        item.onFail = onFail;
        _items.Add(item);
        if (!_started)
            Timers.inst.StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        _started = true;

        LoadItem item = null;
        while (true)
        {
            if (_items.Count > 0)
            {
                item = _items[0];
                _items.RemoveAt(0);
            }
            else
                break;

            if (_pool.ContainsKey(item.url))
            {
                //Debug.Log("hit " + item.url);

                NTexture texture = _pool[item.url];
                texture.refCount++;

                if (item.onSuccess != null)
                    item.onSuccess(texture);

                continue;
            }

            // [Godot] UnityWebRequestAssetBundle.GetAssetBundle(url + ".ab") -> threaded ResourceLoader request.
            string url = _basePath + item.url + ".png";
            string error = null;
            Texture2D nativeTexture = null;
            if (!ResourceLoader.Exists(url))
                error = "not found: " + url;
            else if (ResourceLoader.LoadThreadedRequest(url, "Texture2D") != Error.Ok)
                error = "request failed: " + url;
            else
            {
                ResourceLoader.ThreadLoadStatus status;
                while ((status = ResourceLoader.LoadThreadedGetStatus(url)) == ResourceLoader.ThreadLoadStatus.InProgress)
                    yield return null;

                if (status == ResourceLoader.ThreadLoadStatus.Loaded)
                    nativeTexture = ResourceLoader.LoadThreadedGet(url) as Texture2D;
                else
                    error = "load failed (" + status + "): " + url;
            }

            if (error == null && nativeTexture != null)
            {
                // the same icon may have been requested twice while it was loading
                NTexture texture;
                if (!_pool.TryGetValue(item.url, out texture))
                {
                    texture = new NTexture(nativeTexture);
                    _pool[item.url] = texture;
                }
                texture.refCount++;

                if (item.onSuccess != null)
                    item.onSuccess(texture);
            }
            else
            {
                if (error == null)
                    error = "not a texture: " + url;
                if (item.onFail != null)
                    item.onFail(error);
            }
        }

        _started = false;
    }

    IEnumerator FreeIdleIcons()
    {
        yield return new WaitForSeconds(POOL_CHECK_TIME); //check the pool every 30 seconds

        int cnt = _pool.Count;
        if (cnt > MAX_POOL_SIZE)
        {
            List<string> toRemove = null;
            foreach (KeyValuePair<string, NTexture> de in _pool)
            {
                string key = de.Key;
                NTexture texture = de.Value;
                if (texture.refCount == 0)
                {
                    if (toRemove == null)
                        toRemove = new List<string>();
                    toRemove.Add(key);
                    texture.Dispose();

                    //Debug.Log("free icon " + de.Key);

                    cnt--;
                    if (cnt <= 8)
                        break;
                }
            }
            if (toRemove != null)
            {
                foreach (string key in toRemove)
                    _pool.Remove(key);
            }
        }
    }

}

class LoadItem
{
    public string url;
    public LoadCompleteCallback onSuccess;
    public LoadErrorCallback onFail;
}
