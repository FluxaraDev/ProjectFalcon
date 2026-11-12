using System.Collections.Generic;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Falcon computer repair (round 5). In this build the computer's red
// "failed" state only ever clears after a no-internet failure -
// GorillaComputer.Update restores it solely on the internetFailure path.
// Every Falcon gate message (name gate, rave join lock, rave refusals)
// therefore bricked the computer until the game was reopened (Sans's
// build #8 report). FalconFailure.Mark() is called by those gates when
// they show a message; this watcher clears the state through
// GorillaComputer.FalconClearFailureState() once the Falcon-side
// blocking condition is actually gone, or immediately on a successful
// room join. Stock failures never set the flag, so genuine errors keep
// the stock behavior.
public static class FalconFailure
{
    public static bool Shown;
    public static float ShownAt;

    public static void Mark()
    {
        Shown = true;
        ShownAt = Time.realtimeSinceStartup;
    }
}

public class FalconComputerFix : MonoBehaviour, IMatchmakingCallbacks
{
    private float nextCheck;

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Update()
    {
        if (!FalconFailure.Shown) return;
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.5f;
        if (Time.realtimeSinceStartup - FalconFailure.ShownAt < 2f) return;
        if (FalconRave.JoinLocked) return; // solo rave still running

        GorillaComputer gc = GorillaComputer.instance;
        if (gc == null) return;
        if (NameIsDefault(gc.savedName)) return; // name gate still applies

        FalconFailure.Shown = false;
        gc.FalconClearFailureState();
    }

    // A successful join means every gate passed; clear immediately.
    public void OnJoinedRoom()
    {
        if (!FalconFailure.Shown) return;
        FalconFailure.Shown = false;
        GorillaComputer gc = GorillaComputer.instance;
        if (gc != null) gc.FalconClearFailureState();
    }

    // Same rule as PhotonNetworkController's name gate.
    private static bool NameIsDefault(string n)
    {
        if (string.IsNullOrEmpty(n)) return true;
        n = n.Trim().ToLowerInvariant();
        if (n == "gorilla") return true;
        if (!n.StartsWith("gorilla") || n.Length <= 7) return false;
        for (int i = 7; i < n.Length; i++)
        {
            if (!char.IsDigit(n[i])) return false;
        }
        return true;
    }

    public void OnFriendListUpdate(List<FriendInfo> friendList) { }
    public void OnCreatedRoom() { }
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
    public void OnLeftRoom() { }
}
