using Photon.Pun;
using UnityEngine;

// Built-in Photon bot (Sans, round 18): the Python bot's tricks as a
// native staff feature. Spawns a second "Gorilla Player Networked"
// rig from this client and puppets it - in a room it is a real
// PhotonNetwork.Instantiate (the whole room sees it, owned by you);
// solo it spawns locally (only you see it). Modes: Spin (the bot's
// signature), Rocket (hovers high, hands up), Track (walks after
// you), Idle (stands at its anchor).
//
// How the puppeting works: an owned rig's own VRRig.LateUpdate drives
// it from your headset. This driver runs after it
// (DefaultExecutionOrder 1000) and overwrites the root, the head
// constraint and both hands - exactly what the rig serializes to the
// room - so the room sees the bot, not a mirror of you.
[DefaultExecutionOrder(1000)]
public class FalconBot : MonoBehaviour
{
    public enum Mode { Idle, Spin, Rocket, Track }

    public static FalconBot Instance;
    public static string LastResult = "";
    public static bool Active => Instance != null && Instance.botGo != null;
    public static bool Networked => Instance != null && Instance.networked;
    public static Mode CurrentMode => Instance != null ? Instance.mode : Mode.Idle;

    private GameObject botGo;
    private VRRig botRig;
    private bool networked;
    private Mode mode = Mode.Idle;
    private Vector3 anchor;
    private float yaw;

    private void Awake() { Instance = this; }

    public static void ToggleSpawn()
    {
        if (Instance == null) { LastResult = "bot system missing"; return; }
        if (Instance.botGo != null) Instance.Despawn();
        else Instance.Spawn();
    }

    public static void SetMode(Mode m)
    {
        if (Instance == null || Instance.botGo == null) { LastResult = "spawn the bot first"; return; }
        Instance.mode = Instance.mode == m ? Mode.Idle : m;
        LastResult = "bot mode: " + Instance.mode.ToString().ToLowerInvariant();
    }

    private void Spawn()
    {
        Vector3 pos = new Vector3(-64f, 12.4f, -83f);
        Quaternion rot = Quaternion.identity;
        Transform head = null;
        var player = GorillaLocomotion.Player.Instance;
        if (player != null && player.headCollider != null) head = player.headCollider.transform;
        if (head != null)
        {
            Vector3 f = head.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            f.Normalize();
            pos = head.position + f * 2f;
            pos.y -= 1.3f;
            rot = Quaternion.LookRotation(-f);
        }
        if (PhotonNetwork.InRoom)
        {
            botGo = PhotonNetwork.Instantiate("GorillaPrefabs/Gorilla Player Networked", pos, rot);
            networked = true;
        }
        else
        {
            var prefab = Resources.Load<GameObject>("GorillaPrefabs/Gorilla Player Networked");
            if (prefab == null) { LastResult = "bot prefab missing"; return; }
            botGo = Object.Instantiate(prefab, pos, rot);
            networked = false;
        }
        if (botGo == null) { LastResult = "bot spawn failed"; return; }
        botGo.name = "Falcon Bot";
        botRig = botGo.GetComponent<VRRig>();
        if (botRig == null) botRig = botGo.GetComponentInChildren<VRRig>();
        anchor = pos;
        yaw = rot.eulerAngles.y;
        mode = Mode.Idle;
        LastResult = networked ? "bot spawned (in room)" : "bot spawned (local only)";
    }

    private void Despawn()
    {
        if (botGo != null)
        {
            if (networked && PhotonNetwork.InRoom) PhotonNetwork.Destroy(botGo);
            else Object.Destroy(botGo);
        }
        botGo = null;
        botRig = null;
        mode = Mode.Idle;
        LastResult = "bot removed";
    }

    private void LateUpdate()
    {
        if (botGo == null || botRig == null) return;
        var player = GorillaLocomotion.Player.Instance;
        Transform myHead = player != null && player.headCollider != null ? player.headCollider.transform : null;
        float dt = Time.deltaTime;
        Vector3 feet = anchor;
        switch (mode)
        {
            case Mode.Spin:
                yaw += dt * 240f;
                break;
            case Mode.Rocket:
                feet = anchor + Vector3.up * (5f + Mathf.Sin(Time.time * 1.7f) * 0.4f);
                break;
            case Mode.Track:
                if (myHead != null)
                {
                    Vector3 to = myHead.position - anchor;
                    to.y = 0f;
                    float dist = to.magnitude;
                    if (dist > 1.4f && dist > 0.01f)
                    {
                        anchor += to.normalized * Mathf.Min(2.4f * dt, dist - 1.2f);
                        float targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                        yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-8f * dt));
                    }
                    feet = anchor;
                }
                break;
        }
        Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
        botRig.transform.position = feet;
        botRig.transform.rotation = facing;
        if (botRig.headConstraint != null)
        {
            botRig.headConstraint.position = feet + Vector3.up * 1.32f;
            botRig.headConstraint.rotation = facing;
        }
        Vector3 side = facing * Vector3.right;
        Vector3 fwd = facing * Vector3.forward;
        float handY = mode == Mode.Rocket ? 1.7f : 0.75f;
        if (botRig.leftHandTransform != null)
            botRig.leftHandTransform.position = feet - side * 0.28f + Vector3.up * handY + fwd * 0.12f;
        if (botRig.rightHandTransform != null)
            botRig.rightHandTransform.position = feet + side * 0.28f + Vector3.up * handY + fwd * 0.12f;
    }
}
