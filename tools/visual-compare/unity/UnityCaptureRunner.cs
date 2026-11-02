// FairyGUI for Godot - visual comparison tooling, Unity side (not part of the Godot build).
// Copied by tools/visual-compare/run.ps1 into the scratch Unity project (_ref/unity-capture/Assets/Examples/Capture/).
// Active only when the player is started with -captureScene. Loads that example scene, waits, and saves a PNG
// of the screen. With -captureSubdemos it also opens every sub-demo (buttons of group "btns", as in Basics) and
// saves one PNG each. The Godot counterpart is tests/CaptureRunner.cs: both must produce the same file names.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FairyGUI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UnityCaptureRunner : MonoBehaviour
{
    string _scene;
    string _name;
    string _out;
    float _wait = 3f;
    bool _subdemos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        string scene = Arg("-captureScene");
        if (string.IsNullOrEmpty(scene))
            return;

        var go = new GameObject("[UnityCaptureRunner]");
        DontDestroyOnLoad(go);
        var r = go.AddComponent<UnityCaptureRunner>();
        r._scene = scene;
        r._name = Arg("-captureName") ?? scene;
        r._out = Arg("-captureOut") ?? "Shots";
        r._subdemos = Arg("-captureSubdemos") != null;
        float w;
        if (float.TryParse(Arg("-captureWait"), NumberStyles.Float, CultureInfo.InvariantCulture, out w))
            r._wait = w;
    }

    IEnumerator Start()
    {
        Directory.CreateDirectory(_out);
        // Same default font on both sides (Unity's Basics example uses it; the others inherit it).
        UIConfig.defaultFont = "Microsoft YaHei";
        SceneManager.LoadScene(_scene);
        yield return new WaitForSecondsRealtime(_wait);
        yield return Capture(_name);

        if (_subdemos)
        {
            GComponent view = FindMainView();
            if (view == null)
                Debug.LogError("[CAPTURE] no view with a 'btns' group");
            else
            {
                var buttons = new List<GObject>();
                for (int i = 0; i < view.numChildren; i++)
                {
                    GObject c = view.GetChildAt(i);
                    if (c.group != null && c.group.name == "btns")
                        buttons.Add(c);
                }
                buttons.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                GObject back = view.GetChild("btn_Back");
                foreach (GObject b in buttons)
                {
                    b.onClick.Call();
                    yield return new WaitForSecondsRealtime(1.5f);
                    yield return Capture(_name + "__" + b.name.Substring(4));
                    if (back != null && back.visible)
                        back.onClick.Call();
                    yield return new WaitForSecondsRealtime(0.3f);
                }
            }
        }

        Debug.Log("[CAPTURE] done");
        Application.Quit(0);
    }

    IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
        string path = Path.Combine(_out, Sanitize(name) + ".png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Debug.Log("[CAPTURE] " + path + " " + tex.width + "x" + tex.height);
        Destroy(tex);
    }

    static GComponent FindMainView()
    {
        foreach (UIPanel p in FindObjectsByType<UIPanel>(FindObjectsSortMode.None))
        {
            GComponent ui = p.ui;
            if (ui == null)
                continue;
            for (int i = 0; i < ui.numChildren; i++)
            {
                GObject c = ui.GetChildAt(i);
                if (c.group != null && c.group.name == "btns")
                    return ui;
            }
        }
        return null;
    }

    static string Sanitize(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }

    static string Arg(string name)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++)
            if (a[i] == name)
                return a[i + 1];
        return null;
    }
}
