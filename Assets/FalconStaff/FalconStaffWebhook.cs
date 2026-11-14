using System.Collections;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.Networking;

// Staff log plumbing (rounds 27+29). Text logs relay through the
// CloudScript handler "FalconLog" (staff-gated; the webhook URL lives
// server-side only). Report VOICE CLIPS can't ride CloudScript (no
// binary posts), so those upload straight to Discord from here - the
// webhook URL for clips is fetched at runtime from PlayFab Title Data
// key "falcon_clip_hook", so it never sits in the game files or the
// repo either; Sans can rotate it any time by changing the webhook
// and the title-data value.
public static class FalconStaffWebhook
{
    private static MonoBehaviour host;
    private static string clipHook;
    private static bool clipHookFetched;

    public static void Init(MonoBehaviour coroutineHost) { host = coroutineHost; }

    public static void Log(string message)
    {
        try
        {
            var request = new ExecuteCloudScriptRequest
            {
                FunctionName = "FalconLog",
                FunctionParameter = new { msg = message }
            };
            PlayFabClientAPI.ExecuteCloudScript(request, delegate (ExecuteCloudScriptResult result) { }, null, null, null);
        }
        catch { }
    }

    public static void UploadReportClip(byte[] wav, string caption)
    {
        if (wav == null || wav.Length == 0 || host == null) return;
        if (!clipHookFetched) { FetchClipHook(wav, caption); return; }
        if (string.IsNullOrEmpty(clipHook)) return;
        host.StartCoroutine(PostClip(wav, caption));
    }

    private static void FetchClipHook(byte[] wav, string caption)
    {
        try
        {
            var request = new GetTitleDataRequest { Keys = new System.Collections.Generic.List<string> { "falcon_clip_hook" } };
            PlayFabClientAPI.GetTitleData(request, delegate (GetTitleDataResult result)
            {
                clipHookFetched = true;
                string v;
                if (result.Data != null && result.Data.TryGetValue("falcon_clip_hook", out v)) clipHook = v;
                UploadReportClip(wav, caption);
            }, delegate (PlayFabError error) { clipHookFetched = true; });
        }
        catch { clipHookFetched = true; }
    }

    private static IEnumerator PostClip(byte[] wav, string caption)
    {
        WWWForm form = new WWWForm();
        form.AddField("payload_json", "{\"content\":\"" + Escape(caption) + "\"}");
        form.AddBinaryData("file", wav, "voice-last-25s.wav", "audio/wav");
        var req = UnityWebRequest.Post(clipHook, form);
        yield return req.SendWebRequest();
        req.Dispose();
    }

    private static string Escape(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
