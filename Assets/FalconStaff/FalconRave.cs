using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Player = GorillaLocomotion.Player;

// RAVE mode (Sans, 2026-11-12). The computer's old CREDITS tab is now the
// RAVE tab (GorillaComputer routes its ENTER key and screen text here).
// Pressing ENTER starts the rave: Sans's song plays map-wide (2D audio so
// distance does not matter), the lighting goes disco, fog rolls in and
// cycles color, gravity drops, and movement speed jumps.
//
// Rules from Sans:
//  - Start only while NOT in a room (solo rave; joining stays locked until
//    the song ends) OR as the master of a PRIVATE lobby, where it runs
//    lobby-wide through a Photon event and every client plays its own copy
//    of the song.
//  - Leaving the room ends the rave locally and restores EVERYTHING, so no
//    effect (low gravity, speed) can leak into another lobby.
// The song ships in the project at Assets/Resources/rave_song.mp3 and is
// loaded with Resources.Load; if it is missing the rave refuses to start.
public class FalconRave : MonoBehaviour, IOnEventCallback, IMatchmakingCallbacks
{
    public const byte EvRaveStart = 210;

    // Tunables ------------------------------------------------------------
    private const float RaveGravityY = -2.0f;    // stock gravity is -9.81
    private const float RaveJumpSpeed = 15f;     // below the game's own 2x
    private const float RaveJumpMult = 2.4f;     // tripwires (17 / 2.6)
    private const float RaveFogDensity = 0.015f;
    private const float FallbackSongSeconds = 197.8f; // used if a client is missing the clip
    private const string ClipResourceName = "rave_song";
    private const string SongTitle = "SOPHIE - VYZEE";

    public static FalconRave Instance;
    public static bool IsRaving { get; private set; }

    private static float raveEndTime;
    private static bool soloRave;

    private AudioSource audioSource;
    private AudioClip clip;
    private bool clipMissing;

    private Vector3 savedGravity;
    private float savedJumpSpeed = 6.5f;
    private float savedJumpMult = 1.1f;
    private bool savedFog;
    private Color savedFogColor;
    private FogMode savedFogMode;
    private float savedFogDensity, savedFogStart, savedFogEnd;
    private Color savedAmbient;
    private readonly List<Light> discoLights = new List<Light>();
    private readonly List<Color> discoLightColors = new List<Color>();
    private float hue;
    private float nextScreenPush;

    // True while a solo rave is running: every join path (computer AND zone
    // triggers, which all funnel through PhotonNetworkController) is locked.
    public static bool JoinLocked => IsRaving && soloRave;

    public static int SecondsLeft =>
        IsRaving ? Mathf.Max(0, Mathf.CeilToInt(raveEndTime - Time.time)) : 0;

    public static string CountdownText()
    {
        int s = SecondsLeft;
        return (s / 60) + ":" + (s % 60).ToString("00") + " LEFT";
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        if (IsRaving) EndRave();
    }

    // ---- computer hooks ---------------------------------------------------

    // Screen text for the RAVE page (GorillaComputer's old Credits case).
    public static string GetScreenText()
    {
        if (IsRaving)
        {
            return "RAVE MODE\n\n" + SongTitle + "\n\nTIME LEFT: " + CountdownText()
                + "\n\nLOW GRAVITY + SPEED BOOST ACTIVE."
                + (soloRave ? "\nJOINING IS LOCKED UNTIL THE SONG ENDS." : "");
        }
        return "RAVE MODE\n\nPRESS ENTER TO START THE RAVE.\n\n" + SongTitle
            + "\n\nTHE SONG PLAYS EVERYWHERE, THE LIGHTS GO DISCO, FOG ROLLS IN, GRAVITY DROPS AND YOU GET A SPEED BOOST."
            + "\n\nWORKS SOLO (NOT IN A ROOM), OR LOBBY-WIDE IF YOU ARE THE HOST OF A PRIVATE LOBBY.";
    }

    // ENTER pressed on the RAVE page.
    public static void TryStartFromComputer()
    {
        if (Instance == null) return;
        if (IsRaving)
        {
            GorillaComputer.instance.UpdateFailureText("THE RAVE IS ALREADY GOING. " + CountdownText());
            return;
        }
        if (!Instance.TryLoadClip())
        {
            GorillaComputer.instance.UpdateFailureText("RAVE SONG FILE IS MISSING FROM THIS BUILD.");
            return;
        }
        if (PhotonNetwork.InRoom)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                GorillaComputer.instance.UpdateFailureText("ONLY THE HOST OF A PRIVATE LOBBY CAN START THE RAVE.");
                return;
            }
            if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.IsVisible)
            {
                GorillaComputer.instance.UpdateFailureText("THE RAVE ONLY WORKS SOLO OR IN A PRIVATE LOBBY.");
                return;
            }
            double startAt = PhotonNetwork.Time + 1.0;
            var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(EvRaveStart, startAt, options, SendOptions.SendReliable);
        }
        else
        {
            Instance.BeginRave(solo: true, audioDelay: 0.0);
        }
    }

    // ---- rave lifecycle ---------------------------------------------------

    private bool TryLoadClip()
    {
        if (clip != null) return true;
        if (clipMissing) return false;
        clip = Resources.Load<AudioClip>(ClipResourceName);
        if (clip == null)
        {
            clipMissing = true;
            Debug.LogError("[FalconRave] Resources/" + ClipResourceName + " not found; rave disabled.");
            return false;
        }
        return true;
    }

    private void BeginRave(bool solo, double audioDelay)
    {
        if (IsRaving) return;
        TryLoadClip();
        float duration = clip != null ? clip.length : FallbackSongSeconds;
        soloRave = solo;
        IsRaving = true;
        raveEndTime = Time.time + (float)audioDelay + duration;

        savedGravity = Physics.gravity;
        Physics.gravity = new Vector3(0f, RaveGravityY, 0f);

        if (Player.Instance != null)
        {
            savedJumpSpeed = Player.Instance.maxJumpSpeed;
            savedJumpMult = Player.Instance.jumpMultiplier;
            Player.Instance.maxJumpSpeed = RaveJumpSpeed;
            Player.Instance.jumpMultiplier = RaveJumpMult;
        }

        savedFog = RenderSettings.fog;
        savedFogColor = RenderSettings.fogColor;
        savedFogMode = RenderSettings.fogMode;
        savedFogDensity = RenderSettings.fogDensity;
        savedFogStart = RenderSettings.fogStartDistance;
        savedFogEnd = RenderSettings.fogEndDistance;
        savedAmbient = RenderSettings.ambientLight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = RaveFogDensity;

        discoLights.Clear();
        discoLightColors.Clear();
        foreach (Light l in FindObjectsOfType<Light>())
        {
            if (l != null && l.type == LightType.Directional)
            {
                discoLights.Add(l);
                discoLightColors.Add(l.color);
            }
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f; // 2D: heard everywhere on the map
            audioSource.playOnAwake = false;
        }
        audioSource.Stop();
        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.volume = 1f;
            audioSource.loop = false;
            audioSource.PlayScheduled(AudioSettings.dspTime + System.Math.Max(0.0, audioDelay));
        }
        Debug.Log("[FalconRave] Rave started. Solo=" + soloRave + " Duration=" + duration);
    }

    private void EndRave()
    {
        if (!IsRaving) return;
        IsRaving = false;
        soloRave = false;

        Physics.gravity = savedGravity;
        if (Player.Instance != null)
        {
            Player.Instance.maxJumpSpeed = savedJumpSpeed;
            Player.Instance.jumpMultiplier = savedJumpMult;
        }

        RenderSettings.fog = savedFog;
        RenderSettings.fogColor = savedFogColor;
        RenderSettings.fogMode = savedFogMode;
        RenderSettings.fogDensity = savedFogDensity;
        RenderSettings.fogStartDistance = savedFogStart;
        RenderSettings.fogEndDistance = savedFogEnd;
        RenderSettings.ambientLight = savedAmbient;
        for (int i = 0; i < discoLights.Count; i++)
        {
            if (discoLights[i] != null) discoLights[i].color = discoLightColors[i];
        }
        discoLights.Clear();
        discoLightColors.Clear();

        if (audioSource != null) audioSource.Stop();
        Debug.Log("[FalconRave] Rave ended; everything restored.");
    }

    private void Update()
    {
        if (!IsRaving) return;
        if (Time.time >= raveEndTime)
        {
            EndRave();
            return;
        }
        // Re-assert the speed boost so room-join resets cannot stomp it.
        if (Player.Instance != null)
        {
            Player.Instance.maxJumpSpeed = RaveJumpSpeed;
            Player.Instance.jumpMultiplier = RaveJumpMult;
        }
        // Countdown on the computer board while the RAVE page is open.
        if (Time.time >= nextScreenPush)
        {
            nextScreenPush = Time.time + 0.25f;
            GorillaComputer gc = GorillaComputer.instance;
            if (gc != null && gc.currentState == GorillaComputer.ComputerState.Credits && gc.screenText != null)
            {
                gc.screenText.Text = GetScreenText();
            }
        }
    }

    private void LateUpdate()
    {
        if (!IsRaving) return;
        // LateUpdate so the disco wins the frame over the day/night manager.
        hue = (hue + Time.deltaTime * 0.22f) % 1f;
        Color c = Color.HSVToRGB(hue, 0.85f, 1f);
        RenderSettings.fogColor = c;
        RenderSettings.ambientLight = Color.Lerp(Color.black, c, 0.55f);
        for (int i = 0; i < discoLights.Count; i++)
        {
            if (discoLights[i] != null) discoLights[i].color = c;
        }
    }

    // ---- Photon -----------------------------------------------------------

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != EvRaveStart || IsRaving) return;
        double startAt = photonEvent.CustomData is double d ? d : PhotonNetwork.Time;
        double delay = startAt - PhotonNetwork.Time;
        BeginRave(solo: false, audioDelay: delay);
    }

    // Leaving the room for any reason cuts the rave for this player and
    // restores everything, so nothing leaks into the next lobby.
    public void OnLeftRoom()
    {
        if (IsRaving) EndRave();
    }

    public void OnFriendListUpdate(System.Collections.Generic.List<FriendInfo> friendList) { }
    public void OnCreatedRoom() { }
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinedRoom() { }
    public void OnJoinRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
}
