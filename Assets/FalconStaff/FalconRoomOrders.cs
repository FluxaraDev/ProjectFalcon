using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using PlayFab;
using PlayFab.ClientModels;

// Project Falcon room orders (round 39, Sans): the Discord staff
// bot can order a room shut down - the "Shutdown lobby" button on
// the lobby log embed. The bot writes the Title Data key
// falcon_room_orders:  {"ROOMCODE": <unix seconds>, ...}
// Every client polls it while in a room; if its own room is named
// and the order is fresh (< 10 min), the client leaves - one bot
// click empties the room and Photon closes it. Each order is
// acted on once per client.
public class FalconRoomOrders : MonoBehaviour
{
    private string actedKey;

    private void Start() { StartCoroutine(Loop()); }

    private IEnumerator Loop()
    {
        while (true)
        {
            yield return new WaitForSeconds(25f);
            try { Check(); } catch { }
        }
    }

    private void Check()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
        if (!PlayFabClientAPI.IsClientLoggedIn()) return;
        string code = PhotonNetwork.CurrentRoom.Name;
        PlayFabClientAPI.GetTitleData(new GetTitleDataRequest { Keys = new List<string> { "falcon_room_orders" } },
            result =>
            {
                try
                {
                    if (result.Data == null || !result.Data.ContainsKey("falcon_room_orders")) return;
                    long ts;
                    if (!TryGetOrder(result.Data["falcon_room_orders"], code, out ts)) return;
                    long now = (long)(System.DateTime.UtcNow - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
                    if (now - ts > 600 || ts - now > 600) return;
                    string key = code + "@" + ts;
                    if (actedKey == key) return;
                    actedKey = key;
                    FalconStaffWebhook.Log("ROOM SHUTDOWN - bot order executed (this client left)\nroom: " + code);
                    PhotonNetwork.LeaveRoom();
                }
                catch { }
            },
            error => { });
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
}
