using System;
using System.Collections;
using System.Text;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.Networking;

// Staff log plumbing (round 35, Sans 2026-11-14): CloudScript
// revisions are OUT - everything is in-client now, gated by the
// falcon_role data system at the call sites. Text logs AND voice
// clips post straight to the staff webhook from here. The webhook
// URL is fetched at runtime from PlayFab Title Data key
// "falcon_clip_hook", so it never sits in the game files or the
// repo; Sans rotates it by changing the webhook + the title-data
// value.
public static class FalconStaffWebhook
{
    private static MonoBehaviour host;
    private static string hookUrl;
    private static bool hookFetched;

    public static void Init(MonoBehaviour coroutineHost) { host = coroutineHost; }

    public static void Log(string message) { Post(message, null); }

    public static void Post(string content, Action<bool> done)
    {
        if (host == null) { if (done != null) done(false); return; }
        if (!hookFetched) { FetchHook(delegate { Post(content, done); }); return; }
        if (string.IsNullOrEmpty(hookUrl)) { if (done != null) done(false); return; }
        host.StartCoroutine(PostText(content, done));
    }

    public static void UploadReportClip(byte[] wav, string caption)
    {
        if (wav == null || wav.Length == 0 || host == null) return;
        if (!hookFetched) { FetchHook(delegate { UploadReportClip(wav, caption); }); return; }
        if (string.IsNullOrEmpty(hookUrl)) return;
        host.StartCoroutine(PostClip(wav, caption));
    }

    private static void FetchHook(Action then)
    {
        hookFetched = true; // one fetch per session; failures stay dry
        try
        {
            var request = new GetTitleDataRequest { Keys = new System.Collections.Generic.List<string> { "falcon_clip_hook" } };
            PlayFabClientAPI.GetTitleData(request, delegate (GetTitleDataResult result)
            {
                string v;
                if (result.Data != null && result.Data.TryGetValue("falcon_clip_hook", out v)) hookUrl = v;
                if (then != null) then();
            }, delegate (PlayFabError error) { if (then != null) then(); });
        }
        catch { if (then != null) then(); }
    }

    private static IEnumerator PostText(string content, Action<bool> done)
    {
        byte[] body = Encoding.UTF8.GetBytes("{\"content\":\"" + Escape(content) + "\"}");
        var req = new UnityWebRequest(hookUrl, "POST");
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
        bool ok = req.responseCode >= 200 && req.responseCode < 300;
        req.Dispose();
        if (done != null) done(ok);
    }

    private static IEnumerator PostClip(byte[] wav, string caption)
    {
        WWWForm form = new WWWForm();
        form.AddField("payload_json", "{\"content\":\"" + Escape(caption) + "\"}");
        form.AddBinaryData("file", wav, "voice-last-25s.wav", "audio/wav");
        var req = UnityWebRequest.Post(hookUrl, form);
        yield return req.SendWebRequest();
        long code = req.responseCode;
        req.Dispose();
        // Round 37: a failed clip upload used to vanish silently -
        // now the staff channel itself hears about it.
        if (code < 200 || code >= 300)
            host.StartCoroutine(PostText("(voice clip upload FAILED: HTTP " + code + ", " + wav.Length + " bytes)", null));
    }

    private static string Escape(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
