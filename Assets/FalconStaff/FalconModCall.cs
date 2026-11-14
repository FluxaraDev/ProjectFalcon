using System;
using Photon.Pun;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

// Project Falcon MOD CALL (Sans, 2026-11-13; physical buttons round
// 34, 2026-11-14): the computer tab is GONE - MOD CALL is now a
// physical button placed on every map (FalconModCallButtons), which
// Sans prefers because it is reachable mid-game. Any player can ping
// staff into their lobby. The post happens server-side (CloudScript
// ModCall: 2-minute per-player cooldown; over 5 calls in 9 seconds
// is a 30-minute auto-ban for spam). This class collects the room
// code + map, fires the call, uploads the last 25 s of lobby voice
// as evidence, and reports the outcome back to the button.
public static class FalconModCall
{
    private static bool sending;

    // done receives one of: PINGED / COOLDOWN / BANNED / FAILED / NOT IN A LOBBY
    public static void TryCallFromButton(string map, Action<string> done)
    {
        if (sending) { if (done != null) done("FAILED"); return; }
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            if (done != null) done("NOT IN A LOBBY");
            return;
        }
        sending = true;
        string code = PhotonNetwork.CurrentRoom.Name;
        try
        {
            var request = new ExecuteCloudScriptRequest
            {
                FunctionName = "ModCall",
                FunctionParameter = new { code = code, map = map }
            };
            PlayFabClientAPI.ExecuteCloudScript(request, delegate (ExecuteCloudScriptResult result)
            {
                sending = false;
                string json = result.FunctionResult != null ? result.FunctionResult.ToString() : "";
                string outcome;
                if (json.Contains("\"success\":true") || json.Contains("\"success\": true"))
                {
                    outcome = "PINGED";
                    try
                    {
                        FalconStaffWebhook.UploadReportClip(FalconVoiceTap.BuildClipWav(),
                            "MOD CALL from " + PhotonNetwork.LocalPlayer.NickName + " (" + PhotonNetwork.LocalPlayer.UserId + ") in room " + code + " at " + map + " - last 25s of lobby voice");
                    }
                    catch { }
                }
                else if (json.Contains("spam")) outcome = "BANNED";
                else if (json.Contains("cooldown")) outcome = "COOLDOWN";
                else outcome = "FAILED";
                if (done != null) done(outcome);
            }, delegate (PlayFabError error)
            {
                sending = false;
                if (done != null) done("FAILED");
            });
        }
        catch
        {
            sending = false;
            if (done != null) done("FAILED");
        }
    }
}
