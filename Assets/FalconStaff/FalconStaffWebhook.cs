using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Staff log webhook: every ban and every kick posts here (Sans, 2026-11-10).
// Paste the staff-log webhook URL between the quotes. It ships in the game
// build by design in this in-game system; never post it publicly.
public static class FalconStaffWebhook
{
    private const string WebhookUrl = "";

    private static MonoBehaviour host;

    // Called once by FalconStaffMenu so coroutines have somewhere to live.
    public static void Init(MonoBehaviour coroutineHost)
    {
        host = coroutineHost;
    }

    public static void Log(string message)
    {
        if (string.IsNullOrEmpty(WebhookUrl) || host == null) return;
        host.StartCoroutine(Post(message));
    }

    private static IEnumerator Post(string message)
    {
        string json = "{\"content\":\"" + Escape(message) + "\"}";
        var req = new UnityWebRequest(WebhookUrl, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
        req.Dispose();
    }

    private static string Escape(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
