using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Player = GorillaLocomotion.Player;

// Falcon Staff HAND menu: a world-space panel parented to the left hand,
// in the spirit of the Flux menu but built from this game's own parts
// (legacy UI Text, like the scoreboard and computer - this build has no
// TextMeshPro in its scripts). Spawned at runtime by FalconStaffBootstrap,
// so no scene editing is needed. The IMGUI FalconStaffMenu is NOT spawned
// anymore; this is the in-VR menu. All effects still live in the shared
// FalconStaffNet / FalconStaffOutfits / FalconSoundboard layer.
//
// Interaction: aim the RIGHT hand at the panel - the button under the
// laser highlights - and pull the right trigger to press it. When a gun
// is armed, the same trigger fires the gun UNLESS the laser is on the
// panel (the panel always wins its own clicks). Open/close: left
// controller menu button. Everything is gated by FalconStaffRole, and
// troll guns additionally need headadmin + an open trolling event
// (Sans's Discord-stage rule: firing outside an event logs a denial).
//
// Tunables for the first on-device fit check live at the top: if the
// panel sits wrong on the hand (mirrored, face-down, too far out),
// PanelLocalPos / PanelLocalEuler are the one-line fixes.
public class FalconStaffHandMenu : MonoBehaviour
{
    // ---- hand fit tunables (adjust from Sans's device report) ----
    private static readonly Vector3 PanelLocalPos = new Vector3(0f, 0.04f, 0.17f);
    private static readonly Vector3 PanelLocalEuler = new Vector3(0f, 180f, 0f);
    private const float PanelScale = 0.0009f; // canvas unit -> meters
    private const float PanelW = 300f;
    private const float PanelH = 380f;

    // ---- colors ----
    private static readonly Color BgColor = new Color(0.05f, 0.05f, 0.08f, 0.88f);
    private static readonly Color BtnNormal = new Color(0.16f, 0.17f, 0.22f, 0.95f);
    private static readonly Color BtnOn = new Color(0.13f, 0.45f, 0.18f, 0.95f);
    private static readonly Color BtnArmed = new Color(0.55f, 0.14f, 0.14f, 0.95f);
    private static readonly Color BtnHover = new Color(0.9f, 0.68f, 0.12f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.95f, 1f);
    private static readonly Color DimColor = new Color(0.7f, 0.7f, 0.75f, 1f);

    private class Btn
    {
        public float cx, cy, w, h;
        public Image img;
        public System.Func<int> state; // 0 normal, 1 on, 2 armed
        public System.Action onClick;
    }

    private GameObject panelRoot;
    private RectTransform contentRoot;
    private Text titleRoleText;
    private Text footerText;
    private Text diagStatusText;
    public static int MenuButtonPresses;
    private Font donorFont;
    private readonly List<Btn> clickables = new List<Btn>();
    private readonly List<Btn> contentBtns = new List<Btn>();
    private Btn hovered;

    private bool open;
    private int tab; // 0 staff, 1 troll, 2 sound, 3 move
    private string armedGun = "";
    private bool selfTest;
    private bool eventOpen;
    private bool prevMenu;
    private bool prevTrigger;
    private float nextSnowball;
    private float footerTimer;
    private string lastRole = null;

    // Movement toggles (Move tab). ON values match the staff spec: arm cap
    // effectively removed, jump above the 1.4x scene-edit values. While a
    // toggle is on, the values are re-asserted every frame, because
    // PhotonNetworkController resets jump values at room join.
    private bool longArmsOn;
    private bool speedOn;
    private const float ArmReachOn = 10000f;
    private const float JumpSpeedOn = 12f;
    private const float JumpMultOn = 2f;
    private const float ArmReachOff = 1.5f;
    private const float JumpSpeedOff = 6.5f;
    private const float JumpMultOff = 1.1f;

    public GameObject snowballPrefab; // unwired in bootstrap builds (no Inspector); snowball stays inert until a prefab is loadable
    public float snowballInterval = 0.1f; // 100 ms; 0.044f = the 44 ms option

    private FalconSoundboard soundboard;
    private FalconCoordCube coordCube;

    private void Awake()
    {
        FalconStaffWebhook.Init(this);
    }

    private void Start()
    {
        soundboard = GetComponent<FalconSoundboard>();
        if (soundboard == null) soundboard = gameObject.AddComponent<FalconSoundboard>();
        coordCube = GetComponent<FalconCoordCube>();
    }

    private void Update()
    {
        // Input is read BEFORE the role gate (round 5): after build #8's
        // silently dead menu, a missing role must never make the panel
        // unreachable - without a role it opens as a status page instead.
        // Two input paths: the XR menu button, and the game's own
        // ControllerInputPoller (left secondary / Y), whose data the
        // whole game runs on. Presses are counted either way; the count
        // shows on the status page and the computer's RAVE page.
        InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        bool menuBtn;
        left.TryGetFeatureValue(CommonUsages.menuButton, out menuBtn);
        bool yBtn = ControllerInputPoller.instance != null && ControllerInputPoller.instance.leftControllerSecondaryButton;
        bool anyBtn = menuBtn || yBtn;
        bool menuEdge = anyBtn && !prevMenu;
        prevMenu = anyBtn;
        if (menuEdge)
        {
            MenuButtonPresses++;
            FalconStaffRole.EnsureRole();
            open = !open;
            if (open) { EnsurePanel(); RebuildContent(); }
            SetPanelVisible(open);
        }

        bool staff = FalconStaffRole.IsStaffOrHigher;

        // Role can arrive (login) or change while the menu exists.
        if (lastRole != FalconStaffRole.Role)
        {
            lastRole = FalconStaffRole.Role;
            if (titleRoleText != null) titleRoleText.text = "FALCON STAFF - " + lastRole.ToUpperInvariant();
            if (open) RebuildContent();
        }

        InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool trig;
        right.TryGetFeatureValue(CommonUsages.triggerButton, out trig);
        trig = trig || (ControllerInputPoller.instance != null && ControllerInputPoller.instance.rightControllerIndexFloat > 0.5f);
        bool trigEdge = trig && !prevTrigger;
        prevTrigger = trig;

        if (open && panelRoot == null) { EnsurePanel(); if (panelRoot != null) RebuildContent(); }

        hovered = open ? ComputeHover() : null;
        PaintButtons();

        if (open && hovered != null)
        {
            if (trigEdge && hovered.onClick != null) hovered.onClick();
        }
        else if (staff && armedGun == "snowball")
        {
            if (trig) SnowballSpam();
        }
        else if (staff && trigEdge)
        {
            GunFire();
        }

        if (staff) ApplyMovement();

        if (open)
        {
            footerTimer -= Time.deltaTime;
            if (footerTimer <= 0f) { footerTimer = 0.5f; RefreshFooter(); if (!staff) RefreshDiagStatus(); }
        }
    }

    // ---------------- panel construction ----------------

    private Font FindDonorFont()
    {
        if (donorFont != null) return donorFont;
        foreach (Text t in FindObjectsOfType<Text>())
        {
            if (t != null && t.font != null) { donorFont = t.font; break; }
        }
        return donorFont;
    }

    private void EnsurePanel()
    {
        if (panelRoot != null) { AttachPanel(); return; }
        if (FindDonorFont() == null) return; // scene texts not up yet; next open retries

        panelRoot = new GameObject("FalconStaffPanel");
        RectTransform rt = panelRoot.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(PanelW, PanelH);
        Canvas cv = panelRoot.AddComponent<Canvas>();
        cv.renderMode = RenderMode.WorldSpace;
        CanvasScaler scaler = panelRoot.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 8f;

        MakeImage(rt, 0f, 0f, PanelW, PanelH, BgColor);
        Text title = MakeText(rt, 0f, 168f, 280f, 26f, 17, TextAnchor.MiddleCenter);
        title.text = "FALCON STAFF";
        titleRoleText = MakeText(rt, 0f, 146f, 280f, 18f, 12, TextAnchor.MiddleCenter);
        titleRoleText.color = DimColor;
        titleRoleText.text = "FALCON STAFF - " + FalconStaffRole.Role.ToUpperInvariant();
        lastRole = FalconStaffRole.Role;

        string[] tabNames = { "Staff", "Troll", "Sound", "Move" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            int idx = i;
            Btn b = MakeButton(rt, -103.5f + 69f * i, 116f, 66f, 28f, tabNames[i],
                () => { tab = idx; RebuildContent(); },
                () => tab == idx ? 1 : 0);
            clickables.Add(b);
        }

        var contentGo = new GameObject("Content");
        contentRoot = contentGo.AddComponent<RectTransform>();
        contentRoot.SetParent(rt, false);
        contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
        contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        contentRoot.pivot = new Vector2(0.5f, 0.5f);
        contentRoot.anchoredPosition = Vector2.zero;
        contentRoot.sizeDelta = new Vector2(PanelW, PanelH);

        footerText = MakeText(rt, 0f, -164f, 280f, 24f, 11, TextAnchor.MiddleCenter);
        footerText.color = DimColor;

        AttachPanel();
        SetPanelVisible(open);
    }

    private void AttachPanel()
    {
        if (panelRoot == null || Player.Instance == null || Player.Instance.leftHandTransform == null) return;
        Transform anchor = Player.Instance.leftHandTransform;
        if (panelRoot.transform.parent != anchor)
        {
            panelRoot.transform.SetParent(anchor, false);
            panelRoot.transform.localPosition = PanelLocalPos;
            panelRoot.transform.localRotation = Quaternion.Euler(PanelLocalEuler);
            panelRoot.transform.localScale = Vector3.one * PanelScale;
        }
    }

    private void SetPanelVisible(bool v)
    {
        if (panelRoot != null) panelRoot.SetActive(v);
    }

    private Image MakeImage(RectTransform parent, float cx, float cy, float w, float h, Color color)
    {
        var go = new GameObject("img");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx, cy);
        rt.sizeDelta = new Vector2(w, h);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Text MakeText(RectTransform parent, float cx, float cy, float w, float h, int size, TextAnchor align)
    {
        var go = new GameObject("txt");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx, cy);
        rt.sizeDelta = new Vector2(w, h);
        Text t = go.AddComponent<Text>();
        t.font = FindDonorFont();
        t.fontSize = size;
        t.alignment = align;
        t.color = TextColor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private Btn MakeButton(RectTransform parent, float cx, float cy, float w, float h, string label, System.Action onClick, System.Func<int> state)
    {
        Image img = MakeImage(parent, cx, cy, w, h, BtnNormal);
        Text t = MakeText(parent, cx, cy, w, h, 13, TextAnchor.MiddleCenter);
        t.text = label;
        return new Btn { cx = cx, cy = cy, w = w, h = h, img = img, onClick = onClick, state = state };
    }

    // ---------------- content ----------------

    private void ClearContent()
    {
        foreach (Btn b in contentBtns) clickables.Remove(b);
        contentBtns.Clear();
        if (contentRoot != null)
        {
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.GetChild(i).gameObject);
        }
    }

    private Btn ContentButton(float cx, float cy, float w, float h, string label, System.Action onClick, System.Func<int> state)
    {
        Btn b = MakeButton(contentRoot, cx, cy, w, h, label, onClick, state);
        contentBtns.Add(b);
        clickables.Add(b);
        return b;
    }

    private Text ContentText(float cx, float cy, float w, float h, int size, string text)
    {
        Text t = MakeText(contentRoot, cx, cy, w, h, size, TextAnchor.MiddleCenter);
        t.color = DimColor;
        t.text = text;
        return t;
    }

    private void RebuildContent()
    {
        if (panelRoot == null || contentRoot == null) return;
        ClearContent();

        if (!FalconStaffRole.IsStaffOrHigher)
        {
            ContentText(0f, 80f, 268f, 24f, 13, "NO STAFF ROLE");
            diagStatusText = ContentText(0f, 34f, 268f, 68f, 11, DiagStatusString());
            ContentButton(0f, -30f, 268f, 28f, "Retry role fetch", () => { FalconStaffRole.FetchRole(); }, () => 0);
            ContentText(0f, -80f, 268f, 40f, 11, "Role comes from PlayFab at login.\nIf FETCH is ok but role is NONE,\nthis account has no falcon_role.");
            RefreshFooter();
            PaintButtons();
            return;
        }

        if (tab == 0)
        {
            ContentButton(0f, 78f, 268f, 28f, "Kick gun (staff+)", () => { armedGun = "kick"; RebuildContent(); }, () => armedGun == "kick" ? 2 : 0);
            ContentButton(0f, 46f, 268f, 28f, "Disarm", () => { armedGun = ""; RebuildContent(); }, () => 0);
            ContentButton(0f, 14f, 268f, 28f, "Self test: " + (selfTest ? "ON" : "OFF"), () => { selfTest = !selfTest; RebuildContent(); }, () => selfTest ? 1 : 0);
            ContentButton(0f, -18f, 268f, 28f, "Spawn test dummy", () => { FalconTestDummy.Toggle(transform); }, () => 0);
            ContentText(0f, -62f, 268f, 44f, 11,
                FalconStaffRole.IsAdminOrHigher
                    ? "Your reports ban instantly (admin tier)."
                    : "Your reports log to the webhook only.");
            ContentText(0f, -92f, 268f, 20f, 11, "Dummy: frozen rig for target practice.");
        }
        else if (tab == 1 && FalconStaffRole.IsHeadAdmin)
        {
            ContentButton(0f, 80f, 268f, 24f, "Trolling event: " + (eventOpen ? "OPEN" : "CLOSED"),
                () => { eventOpen = !eventOpen; RebuildContent(); }, () => eventOpen ? 1 : 0);
            ContentButton(0f, 53f, 268f, 24f, "Spooky music (lobby)", () =>
            {
                if (eventOpen) FalconStaffNet.SendToAll(FalconStaffNet.EvSpookyMusic, new object[0]);
                else FalconStaffWebhook.Log("TROLL DENIED (no event) spookymusic");
            }, () => 0);

            string[] guns = { "fling", "fakelag", "creepify", "slip", "spin", "loudmic", "outfitrandom", "outfitall", "snowball" };
            for (int i = 0; i < guns.Length; i++)
            {
                string g = guns[i];
                float cx = (i % 2 == 0) ? -68f : 68f;
                float cy = 26f - 27f * (i / 2);
                ContentButton(cx, cy, 132f, 24f, g, () => { armedGun = g; RebuildContent(); }, () => armedGun == g ? 2 : 0);
            }
            ContentText(0f, -122f, 268f, 18f, 11, "Snowball prefab not wired in this build.");
        }
        else if (tab == 2 && FalconStaffRole.IsHeadAdmin)
        {
            ContentButton(-68f, 80f, 132f, 24f, "Refresh", () => { if (soundboard != null) soundboard.Refresh(); RebuildContent(); }, () => 0);
            ContentButton(68f, 80f, 132f, 24f, "Stop", () => { if (soundboard != null) soundboard.Stop(); }, () => 0);
            if (soundboard != null)
            {
                int shown = 0;
                for (int i = 0; i < soundboard.Files.Count && shown < 7; i++)
                {
                    string path = soundboard.Files[i];
                    float cy = 44f - 27f * shown;
                    ContentButton(0f, cy, 268f, 24f, System.IO.Path.GetFileName(path), () => soundboard.Play(path), () => 0);
                    shown++;
                }
                if (soundboard.Files.Count > shown)
                    ContentText(0f, -152f, 268f, 16f, 11, "+" + (soundboard.Files.Count - shown) + " more (paging later)");
                if (soundboard.Files.Count == 0)
                    ContentText(0f, 30f, 268f, 20f, 11, "No files in the Soundboard folder.");
            }
        }
        else if (tab == 3)
        {
            ContentButton(0f, 78f, 268f, 28f, "Long arms: " + (longArmsOn ? "ON" : "OFF"),
                () => { longArmsOn = !longArmsOn; if (!longArmsOn) RestoreMove(ArmReachOff, null, null); RebuildContent(); },
                () => longArmsOn ? 1 : 0);
            ContentButton(0f, 46f, 268f, 28f, "Speed boost: " + (speedOn ? "ON" : "OFF"),
                () => { speedOn = !speedOn; if (!speedOn) RestoreMove(null, JumpSpeedOff, JumpMultOff); RebuildContent(); },
                () => speedOn ? 1 : 0);
            ContentButton(0f, 14f, 268f, 28f, "Coord cube: " + (coordCube != null && coordCube.CubeEnabled ? "ON" : "OFF"),
                () => { if (coordCube != null) coordCube.SetEnabled(!coordCube.CubeEnabled); RebuildContent(); },
                () => coordCube != null && coordCube.CubeEnabled ? 1 : 0);
            ContentText(0f, -22f, 268f, 36f, 11, "Long arms = reach cap 10000.\nSpeed = jump 12 / mult 2.0.");
            ContentText(0f, -58f, 268f, 36f, 11, "Cube rides the right hand.\nA = drop / pick up. Coords float above it.");
        }
        else
        {
            ContentText(0f, 40f, 268f, 24f, 12, "Headadmin only.");
        }

        RefreshFooter();
        PaintButtons();
    }

    private void RefreshFooter()
    {
        if (footerText == null) return;
        string playing = soundboard != null && soundboard.NowPlaying != "" ? " | " + soundboard.NowPlaying : "";
        string cube = "";
        if (coordCube != null && coordCube.CubeEnabled)
        {
            Vector3 p = coordCube.CubePosition;
            cube = string.Format(" | Cube {0:0.0}, {1:0.0}, {2:0.0}{3}", p.x, p.y, p.z, coordCube.Held ? " (held)" : "");
        }
        footerText.text = "Armed: " + (armedGun == "" ? "none" : armedGun) + playing + cube;
    }

    private string DiagStatusString()
    {
        return "ROLE: " + (FalconStaffRole.Role == "" ? "NONE" : FalconStaffRole.Role.ToUpperInvariant())
            + "\nFETCH: " + FalconStaffRole.FetchState.ToUpperInvariant()
            + (FalconStaffRole.LastError == "" ? "" : "\nERROR: " + FalconStaffRole.LastError)
            + "\nMENU PRESSES: " + MenuButtonPresses;
    }

    private void RefreshDiagStatus()
    {
        if (diagStatusText != null) diagStatusText.text = DiagStatusString();
    }

    // ---------------- interaction ----------------

    private Btn ComputeHover()
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy) return null;
        if (Player.Instance == null || Player.Instance.rightHandTransform == null) return null;
        Transform hand = Player.Instance.rightHandTransform;
        Transform pt = panelRoot.transform;
        Plane plane = new Plane(pt.forward, pt.position);
        Ray ray = new Ray(hand.position, hand.forward);
        float dist;
        if (!plane.Raycast(ray, out dist)) return null;
        Vector3 local = pt.InverseTransformPoint(ray.GetPoint(dist));
        for (int i = clickables.Count - 1; i >= 0; i--)
        {
            Btn b = clickables[i];
            if (Mathf.Abs(local.x - b.cx) <= b.w / 2f && Mathf.Abs(local.y - b.cy) <= b.h / 2f)
                return b;
        }
        return null;
    }

    private void PaintButtons()
    {
        foreach (Btn b in clickables)
        {
            if (b.img == null) continue;
            if (b == hovered) { b.img.color = BtnHover; continue; }
            int s = b.state != null ? b.state() : 0;
            b.img.color = s == 2 ? BtnArmed : (s == 1 ? BtnOn : BtnNormal);
        }
    }

    // ---------------- movement ----------------

    private void RestoreMove(float? arm, float? speed, float? mult)
    {
        if (Player.Instance == null) return;
        if (arm.HasValue) Player.Instance.maxArmLength = arm.Value;
        if (speed.HasValue) Player.Instance.maxJumpSpeed = speed.Value;
        if (mult.HasValue) Player.Instance.jumpMultiplier = mult.Value;
    }

    private void ApplyMovement()
    {
        if (Player.Instance == null) return;
        if (longArmsOn) Player.Instance.maxArmLength = ArmReachOn;
        if (speedOn)
        {
            Player.Instance.maxJumpSpeed = JumpSpeedOn;
            Player.Instance.jumpMultiplier = JumpMultOn;
        }
    }

    // ---------------- guns (ported from FalconStaffMenu) ----------------

    private void GunFire()
    {
        if (armedGun == "" || armedGun == "snowball") return;
        Transform hand = Player.Instance != null ? Player.Instance.rightHandTransform : null;
        if (hand == null) return;
        RaycastHit hit;
        if (!Physics.Raycast(hand.position, hand.forward, out hit, 100f)) return;
        VRRig rig = hit.collider.GetComponentInParent<VRRig>();
        if (rig == null || rig.myPlayer == null) return;
        if (rig.myPlayer.IsLocal)
        {
            if (selfTest) FireSelf(armedGun);
            return;
        }
        Fire(armedGun, rig);
    }

    private void FireSelf(string gun)
    {
        if (gun == "kick")
        {
            FalconStaffWebhook.Log("SELFTEST kick (dry run, not applied)");
            return;
        }
        if (!FalconStaffRole.IsHeadAdmin) return;
        if (!eventOpen)
        {
            FalconStaffWebhook.Log("SELFTEST DENIED (no event) " + gun);
            return;
        }
        if (FalconStaffNet.Instance == null) return;
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

        if (!FalconStaffRole.IsHeadAdmin) return;
        if (!eventOpen)
        {
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
        if (!FalconStaffRole.IsHeadAdmin || !eventOpen) return;
        if (snowballPrefab == null || Time.time < nextSnowball) return;
        nextSnowball = Time.time + snowballInterval;
        Transform hand = Player.Instance != null ? Player.Instance.rightHandTransform : null;
        if (hand == null) return;
        GameObject ball = PhotonNetwork.Instantiate(
            snowballPrefab.name, hand.position + hand.forward * 0.2f, hand.rotation);
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null) rb.velocity = hand.forward * 25f;
    }
}
