using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Falcon soundboard (headadmin). MP3 / WAV / OGG files dropped in the
// Soundboard folder appear in the menu; pressing one plays it and stops any
// current clip; Stop silences it. The folder is the one Sans specced:
//   Android/data/com.fluxtech.ProjectFalcon/files/Soundboard
// On Android that resolves under the app's external files dir; elsewhere it
// falls back to persistentDataPath/Soundboard so PC testing works.
//
// Voice chat: the clip plays on this object's AudioSource so the user hears
// themselves. Pushing the same clip into the Photon Voice recorder needs a
// custom recorder input; OnClipStarted is the hook point for that wiring
// (see README) — the event fires with every clip that starts.
public class FalconSoundboard : MonoBehaviour
{
    public static event Action<AudioClip, string> OnClipStarted;

    public readonly List<string> Files = new List<string>();
    public string Folder { get; private set; }
    public string NowPlaying { get; private set; } = "";

    private AudioSource source;
    private readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
#if UNITY_ANDROID && !UNITY_EDITOR
        Folder = "/sdcard/Android/data/com.fluxtech.ProjectFalcon/files/Soundboard";
#else
        Folder = Path.Combine(Application.persistentDataPath, "Soundboard");
#endif
        Refresh();
    }

    public void Refresh()
    {
        Files.Clear();
        try
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            foreach (string f in Directory.GetFiles(Folder))
            {
                string lower = f.ToLowerInvariant();
                if (lower.EndsWith(".mp3") || lower.EndsWith(".wav") || lower.EndsWith(".ogg"))
                    Files.Add(f);
            }
            Files.Sort();
        }
        catch (Exception e)
        {
            Debug.LogError("[FalconSoundboard] folder scan failed: " + e.Message);
        }
    }

    public void Play(string path)
    {
        StartCoroutine(PlayRoutine(path));
    }

    private IEnumerator PlayRoutine(string path)
    {
        AudioClip clip;
        if (!cache.TryGetValue(path, out clip))
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            AudioType type = ext == ".mp3" ? AudioType.MPEG : ext == ".wav" ? AudioType.WAV : AudioType.OGGVORBIS;
            using (var req = UnityWebRequestMultimedia.GetAudioClip("file://" + path, type))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[FalconSoundboard] load failed: " + path + " " + req.error);
                    yield break;
                }
                clip = DownloadHandlerAudioClip.GetContent(req);
            }
            cache[path] = clip;
        }
        source.Stop();
        source.clip = clip;
        source.Play();
        NowPlaying = Path.GetFileName(path);
        var handler = OnClipStarted;
        if (handler != null) handler(clip, path);
    }

    public void Stop()
    {
        source.Stop();
        NowPlaying = "";
    }
}
