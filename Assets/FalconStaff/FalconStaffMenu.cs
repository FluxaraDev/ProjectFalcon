using System.Collections;
using ExitGames.Client.Photon;
using GorillaLocomotion;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.XR;
using Player = GorillaLocomotion.Player;

// Falcon Staff Menu. IMGUI window in the spirit of the Flux menu, renamed
// and rebuilt for Project Falcon. Visibility and every action are gated by
// the PlayFab role read at login (FalconStaffRole): the menu will not even
// open without a role, kick needs staff+, troll guns need headadmin AND an
// open trolling event (Sans's Discord-stage rule: firing troll tools with
// no event open demotes the shooter and logs it).
//
// Open/close: left controller menu button (CommonUsages.menuButton).
// Guns: arm a gun, aim the right hand, pull the right trigger. The hit
// player's rig gives the target; effects travel as FalconStaffNet events.
public class FalconStaffMenu : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public GameObject snowballPrefab;
    public float snowballInterval = 0.1f; // 100 ms; 0.044f = the 44 ms option

    private bool open;
    private int tab; // 0 staff, 1 troll, 2 sound, 3 movement
    private string armedGun = "";
    private bool selfTest; // guns may target your own rig for testing
    private bool eventOpen; // trolling event switch, headadmin flips it during events
    private Vector2 scroll;
    private bool prevTrigger;
    private float nextSnowball;

    private FalconSoundboard soundboard;

    // Movement page state. Defaults sit ABOVE the 2026-11-10 scene edits
    // (scene: maxJumpSpeed 9.1, jumpMultiplier 1.54, arm cap removed).
    private float armReach = 10000f; // cap value; reach feel comes from SteamLongArm scale
    private float jumpSpeed = 12f;
    private float jumpMult = 2f;

    private void Start()
    {
        soundboard = GetComponent<FalconSoundboard>();
        if (GetComponent<FalconStaffNet>() == null) gameObject.AddComponent<FalconStaffNet>();
        if (soundboard == null) soundboard = gameObject.AddComponent<FalconSoundboard>();
        FalconStaffWebhook.Init(this);
    }

    private void Update()
    {
        if (!FalconStaffRole.IsStaffOrHigher) { open = false; return; }

        InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        bool menuBtn;
        left.TryGetFeatureValue(CommonUsages.menuButton, out menuBtn);
        bool pressed = menuBtn && !prevMenu;
        prevMenu = menuBtn;
        if (pressed) open = !open;

        if (armedGun == "snowball" && FalconStaffRole.IsHeadAdmin && eventOpen)
            SnowballSpam();
        else
            GunInput();
    }

    private bool prevMenu;

    private void GunInput()
    {
        if (armedGun == "" || armedGun == "snowball") return;
        InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool trig;
        right.TryGetFeatureValue(CommonUsages.triggerButton, out trig);
        bool edge = trig && !prevTrigger;
        prevTrigger = trig;
        if (!edge) return;

        Transform hand = Player.Instance != null ? Player.Instance.rightHandTransform : null;
        if (hand == null) return;
        RaycastHit hit;
        if (!Physics.Raycast(hand.position, hand.forward, out hit, 100f)) return;
        VRRig rig = hit.collider.GetComponentInParent<VRRig>();
        if (rig == null || rig.myPlayer == null) return;
        if (rig.myPlayer.IsLocal)
        {
            // Self test: every gun can be pointed at yourself. Needs the
            // Self test toggle on, so an accidental self-hit in a real
            // lobby does nothing.
            if (selfTest) FireSelf(armedGun);
            return;
        }
        Fire(armedGun, rig);
    }

    private void FireSelf(string gun)
    {
        // Applies the effect locally through the exact receiver code a
        // victim's client runs (FalconStaffNet.ApplyLocal), so a self test
        // is a real test. Troll-event and headadmin gates still apply.
        if (gun == "kick")
        {
            // Dry run only: a real self kick would just leave the room.
            FalconStaffWebhook.Log("SELFTEST kick (dry run, not applied)");
            return;
        }
        if (!FalconStaffRole.IsHeadAdmin) return;
        if (!eventOpen)
        {
            FalconStaffWebhook.Log("SELFTEST DENIED (no event) " + gun);
            return;
        }
        int me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        switch (gun)
        {
            case "fling":
                Vector3 dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.5f;
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvFling, new object[] { dir.normalized * 30f });
                break;
            case "fakelag":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvFakeLag, new object[0]);
                break;
            case "creepify":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvCreepify, new object[0]);
                break;
            case "slip":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvSlip, new object[0]);
                break;
            case "spin":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvSpin, new object[0]);
                break;
            case "loudmic":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvLoudMic, new object[] { me });
                break;
            case "outfitrandom":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvOutfitRandom, new object[0]);
                break;
            case "outfitall":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvOutfitAll, new object[0]);
                break;
            case "snowball":
                break; // snowball is shooter-side already
        }
        FalconStaffWebhook.Log("SELFTEST " + gun);
    }

    private void Fire(string gun, VRRig targetRig)
    {
        Photon.Realtime.Player target = targetRig.myPlayer;
        string me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.NickName : "?";
        string them = target.NickName;

        if (gun == "kick")
        {
            if (!FalconStaffRole.IsStaffOrHigher) return;
            if (PhotonNetwork.IsMasterClient) PhotonNetwork.CloseConnection(target);
            FalconStaffNet.SendTo(FalconStaffNet.EvKick, target, new object[0]);
            FalconStaffWebhook.Log("KICK " + them + " by " + me);
            return;
        }

        // Everything below is troll-tab: headadmin + open event only.
        if (!FalconStaffRole.IsHeadAdmin) return;
        if (!eventOpen)
        {
            // Sans's rule: troll tools outside a hosted event = demotion.
            FalconStaffWebhook.Log("TROLL DENIED (no event) " + gun + " by " + me + " - demotion rule applies");
            return;
        }

        switch (gun)
        {
            case "fling":
                Vector3 dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.5f;
                FalconStaffNet.SendTo(FalconStaffNet.EvFling, target, new object[] { dir.normalized * 30f });
                break;
            case "fakelag":
                FalconStaffNet.SendTo(FalconStaffNet.EvFakeLag, target, new object[0]);
                break;
            case "creepify":
                FalconStaffNet.SendTo(FalconStaffNet.EvCreepify, target, new object[0]);
                break;
            case "slip":
                FalconStaffNet.SendTo(FalconStaffNet.EvSlip, target, new object[0]);
                break;
            case "spin":
                FalconStaffNet.SendTo(FalconStaffNet.EvSpin, target, new object[0]);
                break;
            case "loudmic":
                FalconStaffNet.SendToAll(FalconStaffNet.EvLoudMic, new object[] { target.ActorNumber });
                break;
            case "outfitrandom":
                FalconStaffNet.SendTo(FalconStaffNet.EvOutfitRandom, target, new object[0]);
                break;
            case "outfitall":
                FalconStaffNet.SendTo(FalconStaffNet.EvOutfitAll, target, new object[0]);
                break;
        }
        FalconStaffWebhook.Log("TROLL " + gun + " on " + them + " by " + me);
    }

    private void SnowballSpam()
    {
        InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool trig;
        right.TryGetFeatureValue(CommonUsages.triggerButton, out trig);
        if (!trig || snowballPrefab == null || Time.time < nextSnowball) return;
        nextSnowball = Time.time + snowballInterval;
        Transform hand = Player.Instance != null ? Player.Instance.rightHandTransform : null;
        if (hand == null) return;
        GameObject ball = PhotonNetwork.Instantiate(
            snowballPrefab.name, hand.position + hand.forward * 0.2f, hand.rotation);
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null) rb.velocity = hand.forward * 25f;
    }

    // ---------------- IMGUI ----------------

    private void OnGUI()
    {
        if (!open) return;
        GUI.Window(777001, new Rect(40, 40, 430, 560), Window, "Falcon Staff Menu - " + FalconStaffRole.Role);
    }

    private void Window(int id)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Staff")) tab = 0;
        if (FalconStaffRole.IsHeadAdmin && GUILayout.Button("Troll")) tab = 1;
        if (FalconStaffRole.IsHeadAdmin && GUILayout.Button("Sound")) tab = 2;
        if (GUILayout.Button("Move")) tab = 3;
        GUILayout.EndHorizontal();
        scroll = GUILayout.BeginScrollView(scroll);

        if (tab == 0)
        {
            GUILayout.Label("Armed: " + (armedGun == "" ? "none" : armedGun));
            if (GUILayout.Button("Kick gun (staff+)")) armedGun = "kick";
            if (GUILayout.Button("Disarm")) armedGun = "";
            selfTest = GUILayout.Toggle(selfTest, "Self test (guns can hit YOU)");
            if (GUILayout.Button("Spawn test dummy")) FalconTestDummy.Toggle(transform);
            GUILayout.Label("Dummy: a frozen player rig for target practice. Shoot it with any gun to verify hits; effects that move a real player will not move it (it has no locomotion).");
            GUILayout.Label("Reports: as " + (FalconStaffRole.IsAdminOrHigher
                ? "admin tier, your reports ban instantly."
                : "staff, your reports log to the webhook only."));
            GUILayout.Label("Photon bot: internal module hook - see README.");
        }
        else if (tab == 1)
        {
            eventOpen = GUILayout.Toggle(eventOpen, "Trolling event OPEN (hosted by Sans)");
            string[] guns = { "fling", "fakelag", "creepify", "slip", "spin", "loudmic", "outfitrandom", "outfitall", "snowball" };
            foreach (string g in guns)
                if (GUILayout.Button("Arm " + g)) armedGun = g;
            if (GUILayout.Button("Spooky music (lobby, 2x, vol 999)"))
            {
                if (eventOpen) FalconStaffNet.SendToAll(FalconStaffNet.EvSpookyMusic, new object[0]);
                else FalconStaffWebhook.Log("TROLL DENIED (no event) spookymusic");
            }
        }
        else if (tab == 2)
        {
            if (GUILayout.Button("Refresh folder")) soundboard.Refresh();
            GUILayout.Label("Folder: " + soundboard.Folder);
            foreach (string f in soundboard.Files)
                if (GUILayout.Button(System.IO.Path.GetFileName(f))) soundboard.Play(f);
            if (GUILayout.Button("Stop")) soundboard.Stop();
            GUILayout.Label("Playing: " + soundboard.NowPlaying);
        }
        else
        {
            GUILayout.Label("Long arms (cap value; scene ships uncapped)");
            armReach = GUILayout.HorizontalSlider(armReach, 1.5f, 10000f);
            GUILayout.Label("Jump speed " + jumpSpeed.ToString("0.0"));
            jumpSpeed = GUILayout.HorizontalSlider(jumpSpeed, 6.5f, 20f);
            GUILayout.Label("Jump multiplier " + jumpMult.ToString("0.00"));
            jumpMult = GUILayout.HorizontalSlider(jumpMult, 1.1f, 3f);
            if (GUILayout.Button("Apply movement"))
            {
                if (Player.Instance != null)
                {
                    Player.Instance.maxArmLength = armReach;
                    Player.Instance.maxJumpSpeed = jumpSpeed;
                    Player.Instance.jumpMultiplier = jumpMult;
                }
            }
            GUILayout.Label("True long-arm stretch: add SteamLongArm to the rig and toggle it.");
        }

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }
}
