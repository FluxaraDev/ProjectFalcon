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

        // Round 17: Falcon failure messages are TRANSIENT - always
        // clear once shown. The old rule (hold the screen while the
        // blocking condition lasts) deadlocked the name gate: the
        // message told default-named players to change their name at
        // the computer, while the failure screen itself bricked the
        // computer - they could never reach the NAME page (Sans,
        // build #11: "locks you out the computer, you can't change
        // it", everyone affected). The gates still block every join
        // attempt and re-show the message each time; nothing is
        // weaker, the computer just stays usable.
        GorillaComputer gc = GorillaComputer.instance;
        if (gc == null) return;

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

    public void OnFriendListUpdate(List<FriendInfo> friendList) { }
    public void OnCreatedRoom() { }
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
    public void OnLeftRoom() { }
}
