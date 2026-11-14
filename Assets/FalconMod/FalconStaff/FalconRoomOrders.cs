using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using PlayFab;
using PlayFab.ClientModels;
using GorillaNetworking;

// Project Falcon room orders (round 39, Sans): the Discord staff
// bot can order a room shut down - the "Shutdown lobby" button on
// the lobby log embed. The bot writes the Title Data key
// falcon_room_orders:  {"ROOMCODE": <unix seconds>, ...}
// Every client polls it while in a room; if its own room is named
// and the order is fresh (< 10 min), the client leaves - one bot
// click empties the room and Photon closes it. Each order is
// acted on once per client.
//
// Round 46 (Sans, 2026-11-14): TRANSFER orders. Title Data key
// falcon_transfer:  {"code": "ADMIN", "ts": <unix seconds>}
// Every logged-in client polls it (in a room or at the stump);
// on a fresh order it leaves its current room if any and joins
// the named private room - a fleet-wide transfer into one room
// for a meeting. Acted on once per client per order. The name
// gate still applies: default-named players are not transferred
// (they cannot join lobbies until they rename). The target room
// must already exist (someone - Sans - is sitting in it); if the
// join fails (room full / missing) the client stays put and the
// failure is logged to the staff webhook.
public class FalconRoomOrders : MonoBehaviour, IMatchmakingCallbacks
{
    private string actedKey;
    private long actedTransferTs;
    private string pendingCode;

    private void OnEnable() { PhotonNetwork.AddCallbackTarget(this); }
    private void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }

    private void Start() { StartCoroutine(Loop()); }

    private IEnumerator Loop()
    {
        while (true)
        {
            yield return new WaitForSeconds(25f);
            try { Check(); } catch { }
        }
    }

    private void Update()
    {
        // After leaving the old room, join the transfer target
        // as soon as we are back on the master server.
        if (pendingCode == null) return;
        if (PhotonNetwork.InRoom) return;
        if (!PhotonNetwork.IsConnectedAndReady) return;
        string code = pendingCode;
        if (!PhotonNetwork.JoinRoom(code))
            pendingCode = null;
    }

    private void Check()
    {
        if (!PlayFabClientAPI.IsClientLoggedIn()) return;
        PlayFabClientAPI.GetTitleData(new GetTitleDataRequest { Keys = new List<string> { "falcon_room_orders", "falcon_transfer" } },
            result =>
            {
                try
                {
                    if (result.Data == null) return;
                    long now = (long)(System.DateTime.UtcNow - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
                    if (result.Data.ContainsKey("falcon_room_orders"))
                        CheckShutdown(result.Data["falcon_room_orders"], now);
                    if (result.Data.ContainsKey("falcon_transfer"))
                        CheckTransfer(result.Data["falcon_transfer"], now);
                }
                catch { }
            },
            error => { });
    }

    private void CheckShutdown(string json, long now)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
        string code = PhotonNetwork.CurrentRoom.Name;
        long ts;
        if (!TryGetOrder(json, code, out ts)) return;
        if (now - ts > 600 || ts - now > 600) return;
        string key = code + "@" + ts;
        if (actedKey == key) return;
        actedKey = key;
        FalconStaffWebhook.Log("ROOM SHUTDOWN - bot order executed (this client left)\nroom: " + code);
        PhotonNetwork.LeaveRoom();
    }

    private void CheckTransfer(string json, long now)
    {
        string code; long ts;
        if (!TryParseTransfer(json, out code, out ts)) return;
        if (now - ts > 600 || ts - now > 600) return;
        if (ts == actedTransferTs) return;
        if (pendingCode != null) return;
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null &&
            string.Equals(PhotonNetwork.CurrentRoom.Name, code, System.StringComparison.OrdinalIgnoreCase))
            return; // already there
        string nm = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.NickName : null;
        if (PhotonNetworkController.NameIsDefault(nm))
        {
            actedTransferTs = ts; // do not retry every poll
            FalconStaffWebhook.Log("TRANSFER skipped - default name must rename first\ntarget room: " + code);
            return;
        }
        actedTransferTs = ts;
        pendingCode = code;
        FalconStaffWebhook.Log("TRANSFER - moving to room " + code);
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
    }

    private static bool TryParseTransfer(string json, out string code, out long ts)
    {
        code = null; ts = 0;
        if (string.IsNullOrEmpty(json)) return false;
        int ci = json.IndexOf("\"code\"");
        if (ci < 0) return false;
        int q1 = json.IndexOf('"', ci + 6);
        if (q1 < 0) return false;
        int q2 = json.IndexOf('"', q1 + 1);
        if (q2 < 0) return false;
        code = json.Substring(q1 + 1, q2 - q1 - 1);
        if (code.Length == 0) return false;
        int ti = json.IndexOf("\"ts\"", q2);
        if (ti < 0) return false;
        int colon = json.IndexOf(':', ti);
        if (colon < 0) return false;
        int j = colon + 1;
        while (j < json.Length && (json[j] == ' ' || json[j] == '\t')) j++;
        long v = 0; bool any = false;
        while (j < json.Length && json[j] >= '0' && json[j] <= '9') { v = v * 10 + (json[j] - '0'); j++; any = true; }
        if (!any) return false;
        ts = v; return true;
    }

    private static bool TryGetOrder(string json, string code, out long ts)
    {
        ts = 0;
        if (string.IsNullOrEmpty(json)) return false;
        int i = json.IndexOf("\"" + code + "\"");
        if (i < 0) return false;
        int colon = json.IndexOf(':', i);
        if (colon < 0) return false;
        int j = colon + 1;
        while (j < json.Length && (json[j] == ' ' || json[j] == '\t')) j++;
        long v = 0; bool any = false;
        while (j < json.Length && json[j] >= '0' && json[j] <= '9') { v = v * 10 + (json[j] - '0'); j++; any = true; }
        if (!any) return false;
        ts = v; return true;
    }

    // ---- IMatchmakingCallbacks ----
    public void OnJoinedRoom()
    {
        if (pendingCode != null && PhotonNetwork.CurrentRoom != null &&
            string.Equals(PhotonNetwork.CurrentRoom.Name, pendingCode, System.StringComparison.OrdinalIgnoreCase))
        {
            FalconStaffWebhook.Log("TRANSFER arrived - now in room " + pendingCode);
            pendingCode = null;
        }
    }

    public void OnJoinRoomFailed(short returnCode, string message)
    {
        if (pendingCode != null)
        {
            FalconStaffWebhook.Log("TRANSFER FAILED - could not join " + pendingCode + " (" + message + ")");
            pendingCode = null;
        }
    }

    public void OnJoinRandomFailed(short returnCode, string message) { }
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnFriendListUpdate(List<FriendInfo> friendList) { }
    public void OnCreatedRoom() { }
    public void OnLeftRoom() { }
}
