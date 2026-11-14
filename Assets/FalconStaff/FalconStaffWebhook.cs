using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

// Staff log relay (round 27). Every ban, kick and troll action posts to
// Sans's Discord through here (Sans, 2026-11-10) - but NOT directly:
// this class used to POST to a webhook URL compiled into the client,
// and that path never delivered a single message. The URL const shipped
// empty, the coroutine host was only ever wired by the retired IMGUI
// menu, and the webhook from the old kit turned out to be deleted at
// Discord's end anyway. Logs now ride the PlayFab CloudScript handler
// "FalconLog" (staff-gated server-side), which owns the webhook URL
// privately - no URL in the client, none in the public repo.
public static class FalconStaffWebhook
{
    // Legacy no-op: older callers wired a coroutine host here. The
    // CloudScript relay needs no host; kept so call sites still compile.
    public static void Init(MonoBehaviour coroutineHost) { }

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
}
