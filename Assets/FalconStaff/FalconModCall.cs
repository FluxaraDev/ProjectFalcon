using Photon.Pun;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

// Project Falcon MOD CALL (Sans, 2026-11-13): the computer tab any
// player can use to ping staff into their lobby through the staff
// webhook. The post itself happens server-side (CloudScript ModCall,
// 2-minute per-player cooldown, false calls are on the caller); this
// class only collects the room code and reports the result back onto
// the computer screen.
public static class FalconModCall
{
    private static string lastResult = "";
    private static bool sending;

    public static string GetScreenText()
    {
        string text = "MOD CALL\n\n";
        text += "<color=red>By pressing enter you are pinging staff to join this code !! False calls are bannable !!</color>\n\n";
        text += "PRESS ENTER TO PING STAFF TO THIS LOBBY.";
        if (lastResult != "") text += "\n\n" + lastResult;
        return text;
    }

    public static void TryCallFromComputer()
    {
        if (sending) return;
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            lastResult = "NOT IN A LOBBY - JOIN A ROOM FIRST.";
            return;
        }
        sending = true;
        lastResult = "SENDING...";
        try
        {
            var request = new ExecuteCloudScriptRequest
            {
                FunctionName = "ModCall",
                FunctionParameter = new { code = PhotonNetwork.CurrentRoom.Name }
            };
            PlayFabClientAPI.ExecuteCloudScript(request, delegate (ExecuteCloudScriptResult result)
            {
                sending = false;
                string json = result.FunctionResult != null ? result.FunctionResult.ToString() : "";
                if (json.Contains("\"success\":true") || json.Contains("\"success\": true"))
                    lastResult = "STAFF PINGED - HELP IS ON THE WAY.";
                else if (json.Contains("cooldown"))
                    lastResult = "ON COOLDOWN - WAIT BEFORE CALLING AGAIN.";
                else
                    lastResult = "CALL FAILED - TRY AGAIN.";
            }, delegate (PlayFabError error)
            {
                sending = false;
                lastResult = "CALL FAILED - TRY AGAIN.";
            });
        }
        catch
        {
            sending = false;
            lastResult = "CALL FAILED - TRY AGAIN.";
        }
    }
}
