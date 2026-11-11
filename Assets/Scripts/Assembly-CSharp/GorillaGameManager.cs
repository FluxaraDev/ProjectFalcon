using System.Collections.Generic;
using System.Text.RegularExpressions;
using GorillaLocomotion;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public abstract class GorillaGameManager : MonoBehaviourPunCallbacks, IInRoomCallbacks, IPunInstantiateMagicCallback
{
    public struct ProjectileInfo
    {
        public double timeLaunched;

        public Vector3 shotVelocity;

        public Vector3 launchOrigin;

        public int projectileCount;

        public float scale;

        public ProjectileInfo(double newTime, Vector3 newVel, Vector3 origin, int newCount, float newScale)
        {
            timeLaunched = newTime;
            shotVelocity = newVel;
            launchOrigin = origin;
            projectileCount = newCount;
            scale = newScale;
        }
    }

    public class VRRigData
    {
        public static string allcosmetics = "1 Early Access Supporter Pack, 1 1000SHINYROCKS, 1 2200SHINYROCKS, 1 5000SHINYROCKS, 1 DAILY LOGIN, 1 LBAAA., 1 LBAAB., 1 LBAAC., 1 LBAAF., 1 LBAAG., 1 LBAAH., 1 LBAAI., 1 LBAAJ., 1 LFAAA., 1 LFAAB., 1 LFAAC., 1 LFAAD., 1 LFAAE., 1 LFAAF., 1 LFAAG., 1 LFAAH., 1 LFAAI., 1 LFAAJ., 1 LFAAK., 1 LFAAL., 1 LFAAM., 1 LFAAN., 1 LFAAO., 1 LHAAA., 1 LHAAB., 1 LHAAC., 1 LHAAD., 1 LHAAE., 1 LHAAF., 1 LHAAH., 1 LHAAI., 1 LHAAJ., 1 LHAAK., 1 LHAAL., 1 LHAAM., 1 LHAAN., 1 LHAAO., 1 LHAAP., 1 LHAAQ., 1 LHAAR., 1 LHAAS., 1 FIRST LOGIN, 1 LHAAG., 1 LBAAE., 1 LHAAT., 1 LHAAU., 1 LHAAV., 1 LHAAW., 1 LHAAX., 1 LHAAY., 1 LHAAZ., 1 LFAAP., 1 LFAAQ., 1 LFAAR., 1 LFAAS., 1 LFAAT., 1 LFAAU., 1 LBAAL., 1 LBAAM., 1 LBAAN., 1 LBAAO., 1 LSAAA., 1 LSAAB., 1 LSAAC., 1 LSAAD., 1 LHABA., 1 LHABB., 1 LHABC., 1 LFAAV., 1 LFAAW., 1 LBAAP., 1 LBAAQ., 1 LBAAR., 1 LBAAS., 1 LFAAX., 1 LFAAY., 1 LFAAZ., 1 LFABA., 1 LHABD., 1 LHABE., 1 LHABF., 1 LHABG., 1 LSAAE., 1 LFABB., 1 LFABC., 1 LHABH., 1 LHABI., 1 LHABJ., 1 LHABK., 1 LHABL., 1 LHABM., 1 LHABN., 1 LHABO., 1 LBAAT., 1 LHABP., 1 LHABQ., 1 LHABR., 1 LFABD., 1 LBAAU., 1 LBAAV., 1 LBAAW., 1 LBAAX., 1 LBAAY., 1 LBAAZ., 1 LBABA., 1 LBABB., 1 LBABC., 1 LBABD., 1 LBABE., 1 LFABE., 1 LHABS., 1 LHABT., 1 LHABU., 1 LHABV., 1 LFABF., 1 LFABG., 1 LBABF., 1 LBABG., 1 LHABW., 1 LBABH., 1 LHABX., 1 LHABY., 1 LMAAA., 1 LMAAB., 1 LHABZ., 1 LHACA., 1 LBABJ., 1 LBABK., 1 LBABL., 1 LMAAC., 1 LMAAD., 1 LMAAE., 1 LBABI., 1 LMAAF., 1 LMAAG., 1 LMAAH., 1 LFABH., 1 LHACB., 1 LHACC., 1 LFABI., 1 LBABM., 1 LBABN., 1 LHACD., 1 LMAAI., 1 LMAAJ., 1 LMAAK., 1 LMAAL., 1 LMAAM., 1 LMAAN., 1 LMAAO., 1 LHACE., 1 LFABJ., 1 LFABK., 1 LFABL., 1 LFABM., 1 LFABN., 1 LFABO., 1 LBABO., 1 LBABP., 1 LMAAP., 1 LBABQ., 1 LBABR., 1 LBABS., 1 LBABT., 1 LBABU., 1 LFABP., 1 LFABQ., 1 LFABR., 1 LHACF., 1 LHACG., 1 LHACH., 1 LMAAQ., 1 LMAAR., 1 LMAAS., 1 LMAAT., 1 LMAAU., 1 LMAAV., 1 LSAAF., 1 LSAAG., 1 LHACI., 1 LFABS., 1 LFABT., 1 LBABV., 1 LHACJ., 1 LHACK., 1 LBABW., 1 LHACL., 1 LBABX., 1 LFABU., 1 LFABV., 1 LBABY., 1 LHACN., 1 LHACO., 1 LBABZ., 1 LHACM., 1 LMAAW., 1 LMAAX., 1 LMAAY., 1 LMAAZ., 1 LMABA., 1 LMABB., 1 LMABC., 1 LSAAI., 1 LSAAJ., 1 LSAAK., 1 LSAAL., 1 LSAAM., 1 LMABD., 1 LMABE., 1 LMABF., 1 LMABG., 1 LMABH., 1 LMABI., 1 LMABJ., 1 LMABK., 1 LMABL., 1 LMABM., 1 LHACP., 1 LMABN., 1 LMABO., 1 LFABW., 1 LHACQ., 1 LHACR., 1 LMABP., 1 LMABQ., 1 LMABR., 1 LMABS., 1 LHACS., 1 LMABT., 1 LBACA., 1 LMABU., 1 LBACB., 1 LHACT., 1 LHACU., 1 LHACV., 1 LBACC., 1 LMABV., 1 LMABW., 1 LHACW., 1 LHACX., 1 LHACY., 1 LBACD., 1 LBACE., 1 LBACF., 1 LHACZ., 1 LMABX., 1 LMABY., 1 LMABZ., 1 LMACA., 1 LBACG., 1 LHADA., 1 LSAAN., 1 LSAAO., 1 LSAAP2., 1 S. FIRST LOGIN, 1 LHADB., 1 LHADC., 1 LHADD., 1 LHADE., 1 LHADF., 1 LHADG., 1 LFABX., 1 LFABY., 1 LBACH., 1 LBACI., 1 LMACB., 1 LMACC., 1 LMACD., 1 LSAAP., 1 LSAAQ., 1 LMACE., 1 LMACF., 1 LBACK., 1 LBACJ., 1 LHADH., 1 LMACG., 1 LMACH., 1 LMACI., 1 LMACJ., 1 LSAAR., 1 LMACK., 1 LSAAP.2. (1), 1 LFABZ., 1 LFACA., 1 LMACL., 1 game-purchase-bundle, 1 LHADI., 1 LHADJ., 1 LHADK., 1 LHADL., 1 LFACB., 1 LFACC., 1 LFACD., 1 LBACL., 1 LBACM., 1 LBACN., 1 LBACO., 1 LBACP., 1 LMACM., 1 LMACN., 1 LMACO., 1 LMACP., 1 LMACQ., 1 LMACR., 1 LMACS., 1 LMACT., 1 LMACU., 1 LMACV., 1 LMACW., 1 LMACX., 1 LMACY., 1 LHADM., 1 LBACQ., 1 LBACR., 1 LBACS., 1 LSAAS., 1 LHADN., 1 LHADO., 1 LHADP., 1 LFACE., 1 LFACF., 1 LBACT., 1 LBACU., 1 LBACV., 1 LBACW., 1 LMACZ., 1 LMADA., 1 LMADB., 1 LMADC., 1 LMADD., 1 LMADE., 1 LHADQ., 1 LMADG., 1 LMADF., 1 LMADH., 1 LMADI., 1 LMADJ., 1 LMADK., 1 LSAAT., 1 LMADL., 1 LSAAU., 1 LHADR., 1 LHADS., 1 LHADT., 1 LHADU., 1 LHADV., 1 LFACG., 1 LFACH., 1 LFACI., 1 LFACJ., 1 LMADM., 1 LMADN., 1 LMADO., 1 LMADP., 1 LMADQ., 1 LMADR., 1 LMADS., 1 LMADT., 1 LMADU., 1 LMADV., 1 LBACX., 1 LBACY., 1 LBACZ., 1 LBADA., 1 LSAAV., 1 LSAAW., 1 LSAAY., 1 LSAAX., 1 LBADB., 1 LHADW., 1 LHADX., 1 LHADY., 1 LFACK., 1 LBADC., 1 LBADD., 1 LMADW., 1 LMADX., 1 LMADY., 1 LMADZ., 1 LMAEA., 1 LMAEB., 1 LMAEC., 1 LMAED., 1 LMAEE., 1 LMAEF., 1 LMAEG., 1 LMAEH., 1 LMAEI., 1 LBADE., 1 LHADZ., 1 LMAEJ., 1 LHAEA., 1 LHAEB., 1 LFACL., 1 LMAEK., 1 LMAEL., 1 LMAEM., 1 LMAEN., 1 LHAEC., 1 LHAED., 1 LHAEE., 1 LFACM., 1 LFACN., 1 LMAEO., 1 LMAEP., 1 LMAEQ., 1 LMAER., 1 LMAES., 1 LMAET., 1 LBADF., 1 LBADG., 1 LBADH., 1 LBADI., 1 LBADJ., 1 LBADK., 1 LBADL., 1 LBADM., 1 LSAAZ., 1 LSABA., 1 LMAEU., 1 LBADN., 1 LMAEZ., 1 LHAEF., 1 LHAEG., 1 LHAEH., 1 LHAEI., 1 LFACO., 1 LFACP., 1 LBADO., 1 LBADP., 1 LBADQ., 1 LMAEV., 1 LMAEW., 1 LMAEX., 1 LMAEY., 1 LMAFA., 1 LMAFB., 1 LMAFC., 1 LMAFD., 1 LMAFE., 1 LMAFF., 1 LMAFG., 1 LMAFH., 1 LMAFI., 1 LSABB., 1 LHAEJ., 1 LHAEK., 1 LHAEL., 1 LHAEM., 1 LFACQ., 1 LFACR., 1 LFACS., 1 LBADR., 1 LBADS., 1 LBADT., 1 LBADU., 1 LBADV., 1 LMAFJ., 1 LMAFK., 1 LMAFL., 1 LMAFM., 1 LMAFN., 1 LMAFO., 1 LMAFP., 1 LMAFQ., 1 LMAFR., 1 LMAFS., 1 LFACT., 1 LFACU., 1 LFACV., 1 LFACW., 1 LFACX., 1 LFACY., 1 LFACZ., 1 LSABC., 1 LHAEN., 1 LHAEO., 1 LHAEP., 1 LFADA., 1 LFADB., 1 LFADC., 1 LFADD., 1 LBADW., 1 LBADX., 1 LBADY., 1 LBADZ., 1 LMAFT., 1 LMAFU., 1 LMAFV., 1 LMAFW., 1 LMAFX., 1 LMAFY., 1 LMAFZ., 1 LMAGA., 1 LMAGB., 1 LMAGC., 1 LMAGD., 1 LMAGE., 1 LHAEQ., 1 LHAER., 1 LHAES., 1 LHAET., 1 LHAEU., 1 LFADE., 1 LBAEA., 1 LBAEB., 1 LBAEC., 1 LBAED., 1 LBAEE., 1 LMAGF., 1 LMAGG., 1 LMAGH., 1 LMAGI., 1 LMAGJ., 1 LMAGK., 1 LMAGL., 1 LMAGM., 1 LMAGN., 1 LMAGO., 1 LMAGP., 1 LMAGQ., 1 LHAEV., 1 LHAEW., 1 LHAEX., 1 LHAEY., 1 LFADF., 1 LFADG., 1 LFADH., 1 LFADI., 1 LBAEF., 1 LBAEG., 1 LBAEH., 1 LMAGR., 1 LMAGS., 1 LMAGT., 1 LMAGU., 1 LMAGV., 1 LMAGW., 1 LMAGX., 1 LMAGY., 1 LMAGZ., 1 LSABD., 1 LBAEI., 1 GT1000SHINYROCKS, 1 GT2200SHINYROCKS, 1 GT5000SHINYROCKS, 1 LBAAK., 1 LBAAD.";
    }

    public static volatile GorillaGameManager instance;

    public Room currentRoom;

    public object obj;

    public float stepVolumeMax = 0.2f;

    public float stepVolumeMin = 0.05f;

    public float fastJumpLimit;

    public float fastJumpMultiplier;

    public float slowJumpLimit;

    public float slowJumpMultiplier;

    public byte roomSize;

    public float lastCheck;

    public float checkCooldown = 3f;

    public float userDecayTime = 15f;

    public Dictionary<int, VRRig> playerVRRigDict = new Dictionary<int, VRRig>();

    public Dictionary<string, float> expectedUsersDecay = new Dictionary<string, float>();

    public Dictionary<string, string> playerCosmeticsLookup = new Dictionary<string, string>();

    public string tempString;

    public float startingToLookForFriend;

    public float timeToSpendLookingForFriend = 10f;

    public bool successfullyFoundFriend;

    public int maxProjectilesToKeepTrackOfPerPlayer = 50;

    public GameObject playerImpactEffectPrefab;

    private int localPlayerProjectileCounter;

    public Dictionary<Photon.Realtime.Player, List<ProjectileInfo>> playerProjectiles = new Dictionary<Photon.Realtime.Player, List<ProjectileInfo>>();

    public float tagDistanceThreshold = 8f;

    public bool testAssault;

    public bool endGameManually;

    public Photon.Realtime.Player currentMasterClient;

    public PhotonView returnPhotonView;

    public VRRig returnRig;

    private Photon.Realtime.Player outPlayer;

    private int outInt;

    private VRRig tempRig;

    public Photon.Realtime.Player[] currentPlayerArray;

    public virtual void Awake()
    {
        currentRoom = PhotonNetwork.CurrentRoom;
        currentPlayerArray = PhotonNetwork.PlayerList;
        DestroyInvalidManager();
        localPlayerProjectileCounter = 0;
        playerProjectiles.Add(PhotonNetwork.LocalPlayer, new List<ProjectileInfo>());
    }

    public virtual void Update()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && instance != this && PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(base.photonView);
        }
        if (!(lastCheck + checkCooldown < Time.time))
        {
            return;
        }
        List<string> list = new List<string>();
        Photon.Realtime.Player[] playerListOthers = PhotonNetwork.PlayerListOthers;
        foreach (Photon.Realtime.Player player in playerListOthers)
        {
            if (!playerCosmeticsLookup.TryGetValue(player.UserId, out var _))
            {
                list.Add(player.UserId);
            }
        }
        if (list.Count > 0)
        {
            Debug.Log("group id to look up: " + PhotonNetwork.CurrentRoom.Name + Regex.Replace(PhotonNetwork.CloudRegion, "[^a-zA-Z0-9]", "").ToUpper());
            foreach (string item in list)
            {
                playerCosmeticsLookup[item] = VRRigData.allcosmetics;
            }
            PlayFabClientAPI.GetSharedGroupData(new GetSharedGroupDataRequest
            {
                Keys = list,
                SharedGroupId = PhotonNetwork.CurrentRoom.Name + Regex.Replace(PhotonNetwork.CloudRegion, "[^a-zA-Z0-9]", "").ToUpper()
            }, delegate (GetSharedGroupDataResult result)
            {
                foreach (KeyValuePair<string, SharedGroupDataRecord> datum in result.Data)
                {
                    playerCosmeticsLookup[datum.Key] = datum.Value.Value;
                    if (base.photonView.IsMine && datum.Value.Value == "BANNED")
                    {
                        Photon.Realtime.Player[] playerList = PhotonNetwork.PlayerList;
                        foreach (Photon.Realtime.Player player2 in playerList)
                        {
                            if (player2.UserId == datum.Key)
                            {
                                Debug.Log("this guy needs banned: " + player2.NickName);
                                PhotonNetwork.CloseConnection(player2);
                            }
                        }
                    }
                }
            }, delegate (PlayFabError error)
            {
                Debug.Log("Got error retrieving user data:");
                Debug.Log(error.GenerateErrorReport());
                if (error.Error == PlayFabErrorCode.NotAuthenticated)
                {
                    PlayFabAuthenticator.instance.AuthenticateWithPlayFab();
                }
                else if (error.Error == PlayFabErrorCode.AccountBanned)
                {
                    Application.Quit();
                    PhotonNetwork.Disconnect();
                }
            });
        }
        lastCheck = Time.time;
        if (base.photonView.IsMine && PhotonNetwork.InRoom)
        {
            int num = 0;
            if (PhotonNetwork.CurrentRoom.ExpectedUsers != null && PhotonNetwork.CurrentRoom.ExpectedUsers.Length != 0)
            {
                string[] expectedUsers = PhotonNetwork.CurrentRoom.ExpectedUsers;
                foreach (string key in expectedUsers)
                {
                    if (expectedUsersDecay.TryGetValue(key, out var value2))
                    {
                        if (value2 + userDecayTime < Time.time)
                        {
                            num++;
                        }
                    }
                    else
                    {
                        expectedUsersDecay.Add(key, Time.time);
                    }
                }
                if (num >= PhotonNetwork.CurrentRoom.ExpectedUsers.Length && num != 0)
                {
                    PhotonNetwork.CurrentRoom.ClearExpectedUsers();
                }
            }
        }
        InfrequentUpdate();
    }

    public virtual void InfrequentUpdate()
    {
        currentPlayerArray = PhotonNetwork.PlayerList;
    }

    public virtual string GameMode()
    {
        return "NONE";
    }

    public virtual void ReportTag(Photon.Realtime.Player taggedPlayer, Photon.Realtime.Player taggingPlayer)
    {
    }

    public void ReportStep(VRRig steppingRig, bool isLeftHand, float velocityRatio)
    {
        float num = 0f;
        if (isLeftHand)
        {
            num = 1f;
        }
        PhotonView.Get(steppingRig).RPC("PlayHandTap", RpcTarget.All, num + Mathf.Max(velocityRatio * stepVolumeMax, stepVolumeMin));
        Debug.Log("bbbb:sending tap to " + isLeftHand.ToString() + " at volume " + Mathf.Max(velocityRatio * stepVolumeMax, stepVolumeMin));
    }

    void IPunInstantiateMagicCallback.OnPhotonInstantiate(PhotonMessageInfo info)
    {
        if (info.Sender != null && !PhotonNetwork.CurrentRoom.Players.TryGetValue(info.Sender.ActorNumber, out outPlayer) && info.Sender != PhotonNetwork.MasterClient)
        {
            GorillaNot.instance.SendReport("trying to inappropriately create game managers", info.Sender.UserId, info.Sender.NickName);
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.Destroy(base.gameObject);
            }
            else
            {
                Object.Destroy(base.gameObject);
            }
            return;
        }
        if (info.Sender != null && instance != null && instance != this)
        {
            GorillaNot.instance.SendReport("trying to create multiple game managers", info.Sender.UserId, info.Sender.NickName);
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.Destroy(base.gameObject);
            }
            else
            {
                Object.Destroy(base.gameObject);
            }
            return;
        }
        if ((instance == null && info.Sender != null && info.Sender.ActorNumber == PhotonNetwork.CurrentRoom.MasterClientId) || (base.photonView.Owner != null && base.photonView.Owner.ActorNumber == PhotonNetwork.CurrentRoom.MasterClientId))
        {
            instance = this;
        }
        else if (instance != null && instance != this)
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            {
                Debug.Log("existing game manager! i'm host. destroying newly created manager");
                PhotonNetwork.Destroy(base.photonView);
            }
            else
            {
                Debug.Log("existing game manager! i'm not host. destroying newly created manager");
                Object.Destroy(this);
            }
            return;
        }
        base.transform.parent = GorillaParent.instance.transform;
    }

    public virtual void NewVRRig(Photon.Realtime.Player player, int vrrigPhotonViewID, bool didTutorial)
    {
        if (playerVRRigDict.ContainsKey(player.ActorNumber))
        {
            playerVRRigDict[player.ActorNumber] = PhotonView.Find(vrrigPhotonViewID).GetComponent<VRRig>();
        }
        else
        {
            playerVRRigDict.Add(player.ActorNumber, PhotonView.Find(vrrigPhotonViewID).GetComponent<VRRig>());
        }
    }

    public virtual bool LocalCanTag(Photon.Realtime.Player myPlayer, Photon.Realtime.Player otherPlayer)
    {
        return false;
    }

    public virtual PhotonView FindVRRigForPlayer(Photon.Realtime.Player player)
    {
        if (player == null)
        {
            return null;
        }
        if (GorillaParent.instance.vrrigDict.TryGetValue(player, out returnRig) && returnRig != null)
        {
            if (returnRig != null && returnRig.photonView != null)
            {
                return returnRig.photonView;
            }
            return null;
        }
        if (playerVRRigDict.TryGetValue(player.ActorNumber, out returnRig))
        {
            return returnRig.photonView;
        }
        foreach (VRRig vrrig in GorillaParent.instance.vrrigs)
        {
            if (!vrrig.isOfflineVRRig && vrrig.GetComponent<PhotonView>().Owner == player)
            {
                return vrrig.GetComponent<PhotonView>();
            }
        }
        return null;
    }

    public static PhotonView StaticFindRigForPlayer(Photon.Realtime.Player player)
    {
        if (instance != null)
        {
            return instance.FindVRRigForPlayer(player);
        }
        if (player == null)
        {
            return null;
        }
        if (GorillaParent.instance.vrrigDict.TryGetValue(player, out var value))
        {
            return value.photonView;
        }
        foreach (VRRig vrrig in GorillaParent.instance.vrrigs)
        {
            if (!vrrig.isOfflineVRRig && vrrig.GetComponent<PhotonView>().Owner == player)
            {
                return vrrig.GetComponent<PhotonView>();
            }
        }
        return null;
    }

    [PunRPC]
    public virtual void ReportTagRPC(Photon.Realtime.Player taggedPlayer, PhotonMessageInfo info)
    {
        GorillaNot.IncrementRPCCall(info, "ReportTagRPC");
        ReportTag(taggedPlayer, info.Sender);
    }

    public void AttemptGetNewPlayerCosmetic(Photon.Realtime.Player playerToUpdate, int attempts)
    {
        List<string> list = new List<string>();
        list.Add(playerToUpdate.UserId);
        PlayFabClientAPI.GetSharedGroupData(new GetSharedGroupDataRequest
        {
            Keys = list,
            SharedGroupId = PhotonNetwork.CurrentRoom.Name + Regex.Replace(PhotonNetwork.CloudRegion, "[^a-zA-Z0-9]", "").ToUpper()
        }, delegate (GetSharedGroupDataResult result)
        {
            foreach (KeyValuePair<string, SharedGroupDataRecord> datum in result.Data)
            {
                Debug.Log("for player " + playerToUpdate.UserId);
                Debug.Log("current allowed: " + playerCosmeticsLookup[datum.Key]);
                Debug.Log("new allowed: " + datum.Value.Value);
                if (playerCosmeticsLookup[datum.Key] != datum.Value.Value)
                {
                    playerCosmeticsLookup[datum.Key] = datum.Value.Value;
                    FindVRRigForPlayer(playerToUpdate).GetComponent<VRRig>().UpdateAllowedCosmetics();
                    FindVRRigForPlayer(playerToUpdate).GetComponent<VRRig>().SetCosmeticsActive();
                    Debug.Log("success on attempt " + attempts);
                }
                else if (attempts - 1 >= 0)
                {
                    Debug.Log("failure on attempt " + attempts + ". trying again");
                    AttemptGetNewPlayerCosmetic(playerToUpdate, attempts - 1);
                }
            }
        }, delegate (PlayFabError error)
        {
            Debug.Log("Got error retrieving user data:");
            Debug.Log(error.GenerateErrorReport());
            if (error.Error == PlayFabErrorCode.NotAuthenticated)
            {
                PlayFabAuthenticator.instance.AuthenticateWithPlayFab();
            }
            else if (error.Error == PlayFabErrorCode.AccountBanned)
            {
                Application.Quit();
                PhotonNetwork.Disconnect();
                Object.DestroyImmediate(PhotonNetworkController.Instance);
                Object.DestroyImmediate(GorillaLocomotion.Player.Instance);
                GameObject[] array = Object.FindObjectsOfType<GameObject>();
                for (int i = 0; i < array.Length; i++)
                {
                    Object.Destroy(array[i]);
                }
            }
        });
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        playerVRRigDict.Remove(otherPlayer.ActorNumber);
        playerCosmeticsLookup.Remove(otherPlayer.UserId);
        currentPlayerArray = PhotonNetwork.PlayerList;
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        currentPlayerArray = PhotonNetwork.PlayerList;
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        currentPlayerArray = PhotonNetwork.PlayerList;
    }

    [PunRPC]
    public void UpdatePlayerCosmetic(PhotonMessageInfo info)
    {
        GorillaNot.IncrementRPCCall(info, "UpdatePlayerCosmetic");
        AttemptGetNewPlayerCosmetic(info.Sender, 2);
    }

    [PunRPC]
    public void JoinPubWithFriends(string shufflerStr, string keyStr, PhotonMessageInfo info)
    {
        GorillaNot.IncrementRPCCall(info, "JoinPubWithFriends");
        if (GorillaComputer.instance.friendJoinCollider.playerIDsCurrentlyTouching.Contains(PhotonNetwork.LocalPlayer.UserId))
        {
            this.startingToLookForFriend = Time.time;
            PhotonNetworkController.Instance.AttemptToFollowFriendIntoPub(info.Sender.UserId, info.Sender.ActorNumber, keyStr, shufflerStr);
            return;
        }
        GorillaNot.instance.SendReport("possible kick attempt", info.Sender.UserId, info.Sender.NickName);
    }

    public virtual float[] LocalPlayerSpeed()
    {
        return new float[2] { 6.5f, 1.1f };
    }

    public bool FindUserIDInRoom(string userID)
    {
        Photon.Realtime.Player[] playerList = PhotonNetwork.PlayerList;
        for (int i = 0; i < playerList.Length; i++)
        {
            if (playerList[i].UserId == userID)
            {
                return false;
            }
        }
        return true;
    }

    public virtual int MyMatIndex(Photon.Realtime.Player forPlayer)
    {
        return 0;
    }

    public virtual void DestroyInvalidManager()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameMode", out obj);
            if (!obj.ToString().Contains(GameMode()))
            {
                if (base.photonView.IsMine)
                {
                    PhotonNetwork.Destroy(base.photonView);
                }
                else
                {
                    Object.Destroy(base.gameObject);
                }
            }
        }
        else
        {
            Object.Destroy(base.gameObject);
        }
    }

    [PunRPC]
    public void LaunchSlingshotProjectile(Vector3 slingshotLaunchLocation, Vector3 slingshotLaunchVelocity, int projHash, int trailHash, bool forLeftHand, int projectileCount, PhotonMessageInfo info)
    {
        GorillaNot.IncrementRPCCall(info, "LaunchSlingshotProjectile");
        tempRig = FindVRRigForPlayer(info.Sender).GetComponent<VRRig>();
        if (tempRig != null && (tempRig.transform.position - slingshotLaunchLocation).magnitude < tagDistanceThreshold)
        {
            tempRig.slingshot.LaunchNetworkedProjectile(slingshotLaunchLocation, slingshotLaunchVelocity, projHash, trailHash, projectileCount, tempRig.scaleFactor, info);
        }
    }

    [PunRPC]
    public void SpawnSlingshotPlayerImpactEffect(Vector3 hitLocation, float teamColorR, float teamColorG, float teamColorB, float teamColorA, int projectileCount, PhotonMessageInfo info)
    {
        GorillaNot.IncrementRPCCall(info, "SpawnSlingshotPlayerImpactEffect");
        Color color = new Color(teamColorR, teamColorG, teamColorB, teamColorA);
        GameObject obj = ObjectPools.instance.Instantiate(playerImpactEffectPrefab, hitLocation);
        tempRig = FindVRRigForPlayer(info.Sender).GetComponent<VRRig>();
        obj.transform.localScale = Vector3.one * tempRig.scaleFactor;
        obj.GetComponent<GorillaColorizableBase>().SetColor(color);
    }

    public int IncrementLocalPlayerProjectileCount()
    {
        localPlayerProjectileCounter++;
        return localPlayerProjectileCounter;
    }
}