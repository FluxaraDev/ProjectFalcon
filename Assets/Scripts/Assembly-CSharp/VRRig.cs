using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GorillaLocomotion;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class VRRig : MonoBehaviourPun, IPunObservable, IPunInstantiateMagicCallback, IOnPhotonViewPreNetDestroy, IPhotonViewCallback
{
	public struct VelocityTime
	{
		public Vector3 vel;

		public double time;

		public VelocityTime(Vector3 velocity, double velTime)
		{
			vel = velocity;
			time = velTime;
		}
	}

	public VRMap head;

	public VRMap rightHand;

	public VRMap leftHand;

	public VRMapThumb leftThumb;

	public VRMapIndex leftIndex;

	public VRMapMiddle leftMiddle;

	public VRMapThumb rightThumb;

	public VRMapIndex rightIndex;

	public VRMapMiddle rightMiddle;

	[Tooltip("- False in 'Gorilla Player Networked.prefab'.\n- True in 'Local VRRig.prefab/Local Gorilla Player'.\n- False in 'Local VRRig.prefab/Actual Gorilla'")]
	public bool isOfflineVRRig;

	public GameObject mainCamera;

	public Transform playerOffsetTransform;

	public int SDKIndex;

	public bool isMyPlayer;

	public AudioSource leftHandPlayer;

	public AudioSource rightHandPlayer;

	public AudioSource tagSound;

	[SerializeField]
	private float ratio;

	public Transform headConstraint;

	public Vector3 headBodyOffset = Vector3.zero;

	public GameObject headMesh;

	public Vector3 syncPos;

	public Quaternion syncRotation;

	public AudioClip[] clipToPlay;

	public AudioClip[] handTapSound;

	public int currentMatIndex;

	public int setMatIndex;

	private int tempMatIndex;

	public float lerpValueFingers;

	public float lerpValueBody;

	public GameObject backpack;

	public Transform leftHandTransform;

	public Transform rightHandTransform;

	public SkinnedMeshRenderer mainSkin;

	public Photon.Realtime.Player myPlayer;

	public GameObject spectatorSkin;

	public int handSync;

	public Material[] materialsToChangeTo;

	public float red;

	public float green;

	public float blue;

	public string playerName;

	public Text playerText;

	[Tooltip("- True in 'Gorilla Player Networked.prefab'.\n- True in 'Local VRRig.prefab/Local Gorilla Player'.\n- False in 'Local VRRig.prefab/Actual Gorilla'")]
	public bool showName;

	public CosmeticItemRegistry cosmeticsObjectRegistry = new CosmeticItemRegistry();

	public GameObject[] cosmetics;

	public GameObject[] overrideCosmetics;

	public GameObject[] combinedCosmetics;

	public string concatStringOfCosmeticsAllowed = "";

	public bool initializedCosmetics;

	public CosmeticsController.CosmeticSet cosmeticSet;

	public CosmeticsController.CosmeticSet tryOnSet;

	public CosmeticsController.CosmeticSet mergedSet;

	public CosmeticsController.CosmeticSet prevSet;

	public SizeManager sizeManager;

	public float pitchScale = 0.3f;

	public float pitchOffset = 1f;

	public VRRigReliableState reliableState;

	public bool inTryOnRoom;

	public bool muted;

	public float scaleFactor;

	private float timeSpawned;

	public float doNotLerpConstant = 1f;

	public string tempString;

	private Photon.Realtime.Player tempPlayer;

	private Photon.Realtime.Player creator;

	private VRRig tempRig;

	private float[] speedArray;

	private double handLerpValues;

	private bool initialized;

	public BattleBalloons battleBalloons;

	private int tempInt;

	public BodyDockPositions myBodyDockPositions;

	public ParticleSystem lavaParticleSystem;

	public ParticleSystem rockParticleSystem;

	public ParticleSystem iceParticleSystem;

	public string tempItemName;

	public CosmeticsController.CosmeticItem tempItem;

	public string tempItemId;

	public int tempItemCost;

	public int leftHandHoldableStatus;

	public int rightHandHoldableStatus;

	[SerializeReference]
	public AudioSource[] musicDrums;

	public TransferrableObject[] instrumentSelfOnly;

	public float bonkTime;

	public float bonkCooldown = 2f;

	public bool isQuitting;

	private VRRig tempVRRig;

	public GameObject huntComputer;

	public Slingshot slingshot;

	public bool playerLeftHanded;

	public Slingshot.SlingshotState slingshotState;

	private PhotonVoiceView myPhotonVoiceView;

	private VRRig senderRig;

	public TransferrableObject.PositionState currentState;

	private bool isInitialized;

	private List<VelocityTime> velocityHistoryList = new List<VelocityTime>();

	public int velocityHistoryMaxLength = 200;

	private Vector3 lastPosition;

	private AudioSource voiceAudio;

	public int ExtraSerializedState
	{
		get
		{
			return reliableState.extraSerializedState;
		}
		set
		{
			reliableState.extraSerializedState = value;
		}
	}

	public int LeftHandState
	{
		get
		{
			return reliableState.lHandState;
		}
		set
		{
			reliableState.lHandState = value;
		}
	}

	public int RightHandState
	{
		get
		{
			return reliableState.rHandState;
		}
		set
		{
			reliableState.rHandState = value;
		}
	}

	public int SizeLayerMask
	{
		get
		{
			return reliableState.sizeLayerMask;
		}
		set
		{
			reliableState.sizeLayerMask = value;
		}
	}

	public int ActiveTransferrableObjectIndex(int idx)
	{
		return reliableState.activeTransferrableObjectIndex[idx];
	}

	public int ActiveTransferrableObjectIndexLength()
	{
		return reliableState.activeTransferrableObjectIndex.Length;
	}

	public void SetActiveTransferrableObjectIndex(int idx, int v)
	{
		reliableState.activeTransferrableObjectIndex[idx] = v;
	}

	public TransferrableObject.PositionState TransferrablePosStates(int idx)
	{
		return reliableState.transferrablePosStates[idx];
	}

	public void SetTransferrablePosStates(int idx, TransferrableObject.PositionState v)
	{
		reliableState.transferrablePosStates[idx] = v;
	}

	public TransferrableObject.ItemStates TransferrableItemStates(int idx)
	{
		return reliableState.transferrableItemStates[idx];
	}

	public void SetTransferrableItemStates(int idx, TransferrableObject.ItemStates v)
	{
		reliableState.transferrableItemStates[idx] = v;
	}

	private void Awake()
	{
		Dictionary<string, GameObject> dictionary = new Dictionary<string, GameObject>();
		GameObject[] array = cosmetics;
		GameObject value;
		foreach (GameObject gameObject in array)
		{
			if (!dictionary.TryGetValue(gameObject.name, out value))
			{
				dictionary.Add(gameObject.name, gameObject);
			}
		}
		array = overrideCosmetics;
		foreach (GameObject gameObject2 in array)
		{
			if (dictionary.TryGetValue(gameObject2.name, out value) && value.name == gameObject2.name)
			{
				value.name = "OVERRIDDEN";
			}
		}
		cosmetics = cosmetics.Concat(overrideCosmetics).ToArray();
		cosmeticsObjectRegistry.Initialize(cosmetics);
		lastPosition = base.transform.position;
	}

	private void Start()
	{
		SharedStart();
	}

	private void SharedStart()
	{
		if (isInitialized)
		{
			return;
		}
		isInitialized = true;
		myBodyDockPositions = GetComponent<BodyDockPositions>();
		reliableState.SharedStart(isOfflineVRRig, myBodyDockPositions);
		Application.quitting += Quitting;
		concatStringOfCosmeticsAllowed = "";
		playerText.transform.parent.GetComponent<Canvas>().worldCamera = GorillaTagger.Instance.mainCamera.GetComponent<Camera>();
		materialsToChangeTo[0] = UnityEngine.Object.Instantiate(materialsToChangeTo[0]);
		initialized = false;
		currentState = TransferrableObject.PositionState.OnChest;
		if (setMatIndex > -1 && setMatIndex < materialsToChangeTo.Length)
		{
			mainSkin.material = materialsToChangeTo[setMatIndex];
		}
		if (!isOfflineVRRig && base.photonView.IsMine)
		{
			CosmeticsController.instance.currentWornSet.LoadFromPlayerPreferences(CosmeticsController.instance);
			red = PlayerPrefs.GetFloat("redValue");
			green = PlayerPrefs.GetFloat("greenValue");
			blue = PlayerPrefs.GetFloat("blueValue");
			InitializeNoobMaterialLocal(red, green, blue, GorillaComputer.instance.leftHanded);
			playerOffsetTransform = GorillaLocomotion.Player.Instance.turnParent.transform;
			mainCamera = GorillaTagger.Instance.mainCamera;
			leftHand.overrideTarget = GorillaLocomotion.Player.Instance.leftHandFollower;
			rightHand.overrideTarget = GorillaLocomotion.Player.Instance.rightHandFollower;
			SDKIndex = -1;
			ratio = 1f;
			if ((bool)GetComponent<VoiceConnection>() && (bool)GetComponent<Recorder>())
			{
				GetComponent<VoiceConnection>().InitRecorder(GetComponent<Recorder>());
			}
			playerText.gameObject.SetActive(value: false);
			if (Application.platform == RuntimePlatform.Android && spectatorSkin != null)
			{
				UnityEngine.Object.Destroy(spectatorSkin);
			}
			/*if (XRSettings.loadedDeviceName == "OpenVR")
			{
				leftHand.trackingPositionOffset = new Vector3(0.02f, -0.06f, 0f);
				leftHand.trackingRotationOffset = new Vector3(-141f, 204f, -27f);
				rightHand.trackingPositionOffset = new Vector3(-0.02f, -0.06f, 0f);
				rightHand.trackingRotationOffset = new Vector3(-141f, 156f, 27f);
			}*/
		}
		else if (isOfflineVRRig)
		{
			CosmeticsController.instance.currentWornSet.LoadFromPlayerPreferences(CosmeticsController.instance);
			if (Application.platform == RuntimePlatform.Android && spectatorSkin != null)
			{
				UnityEngine.Object.Destroy(spectatorSkin);
			}
			/*if (XRSettings.loadedDeviceName == "OpenVR")
			{
				leftHand.trackingPositionOffset = new Vector3(0.02f, -0.06f, 0f);
				leftHand.trackingRotationOffset = new Vector3(-141f, 204f, -27f);
				rightHand.trackingPositionOffset = new Vector3(-0.02f, -0.06f, 0f);
				rightHand.trackingRotationOffset = new Vector3(-141f, 156f, 27f);
			}*/
		}
		else if (!base.photonView.IsMine && !isOfflineVRRig)
		{
			if (spectatorSkin != null)
			{
				UnityEngine.Object.Destroy(spectatorSkin);
			}
			head.syncPos = -headBodyOffset;
			if (UnityEngine.Object.FindObjectOfType<GorillaGameManager>() == null)
			{
				PhotonView.Get(this).RPC("RequestMaterialColor", PhotonView.Get(this).Owner, PhotonNetwork.LocalPlayer, true);
			}
			else
			{
				PhotonView.Get(this).RPC("RequestMaterialColor", PhotonView.Get(this).Owner, PhotonNetwork.LocalPlayer, false);
				base.photonView.RPC("RequestCosmetics", base.photonView.Owner);
			}
			if (GorillaGameManager.instance != null && GorillaGameManager.instance.gameObject.GetComponent<GorillaHuntManager>() != null && !GorillaLocomotion.Player.Instance.inOverlay)
			{
				huntComputer.SetActive(value: true);
			}
			else
			{
				huntComputer.SetActive(value: false);
			}
		}
		if (base.transform.parent == null)
		{
			base.transform.parent = GorillaParent.instance.transform;
		}
		StartCoroutine(OccasionalUpdate());
	}

	private IEnumerator OccasionalUpdate()
	{
		while (true)
		{
			try
			{
				if (!isOfflineVRRig)
				{
					if (PhotonNetwork.IsMasterClient && base.photonView.IsRoomView && base.photonView.IsMine)
					{
						Debug.Log("network deleting vrrig");
						PhotonNetwork.Destroy(base.gameObject);
					}
					if (base.photonView.IsRoomView)
					{
						Debug.Log("local disabling vrrig");
						base.gameObject.SetActive(value: false);
					}
					if (base.photonView == null || base.photonView.Owner == null || !PhotonNetwork.CurrentRoom.Players.TryGetValue(base.photonView.Owner.ActorNumber, out tempPlayer) || (PhotonNetwork.CurrentRoom.Players.TryGetValue(base.photonView.Owner.ActorNumber, out tempPlayer) && tempPlayer == null))
					{
						if (GorillaParent.instance.vrrigs.IndexOf(this) > -1)
						{
							GorillaParent.instance.vrrigs.Remove(this);
						}
						if (base.photonView != null && base.photonView.Owner != null && GorillaParent.instance.vrrigDict.TryGetValue(base.photonView.Owner, out tempVRRig))
						{
							GorillaParent.instance.vrrigDict.Remove(base.photonView.Owner);
						}
						Debug.Log("destroying vrrig of " + base.photonView.Owner);
						UnityEngine.Object.Destroy(base.gameObject);
					}
					if (base.photonView != null && creator != null && !base.photonView.IsRoomView && GorillaParent.instance.vrrigDict.TryGetValue(creator, out tempVRRig) && tempVRRig != null && tempVRRig != this)
					{
						GorillaNot.instance.SendReport("inappropriate tag data being sent multiple vrrigs", creator.UserId, creator.NickName);
						UnityEngine.Object.Destroy(base.gameObject);
					}
					if (PhotonNetwork.IsMasterClient && GorillaGameManager.instance == null)
					{
						PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameMode", out var value);
						if (value.ToString().Contains("CASUAL") || value.ToString().Contains("INFECTION"))
						{
							PhotonNetwork.InstantiateRoomObject("GorillaPrefabs/Gorilla Tag Manager", base.transform.position, base.transform.rotation, 0);
						}
						else if (value.ToString().Contains("HUNT"))
						{
							PhotonNetwork.InstantiateRoomObject("GorillaPrefabs/Gorilla Hunt Manager", base.transform.position, base.transform.rotation, 0);
						}
						else if (value.ToString().Contains("BATTLE"))
						{
							PhotonNetwork.InstantiateRoomObject("GorillaPrefabs/Gorilla Battle Manager", base.transform.position, base.transform.rotation, 0);
						}
					}
				}
				if (myPhotonVoiceView == null)
				{
					myPhotonVoiceView = GetComponent<PhotonVoiceView>();
				}
				else
				{
					myPhotonVoiceView.SpeakerInUse.enabled = GorillaComputer.instance.voiceChatOn == "TRUE" && !muted;
				}
			}
			catch
			{
			}
			yield return new WaitForSeconds(1f);
		}
	}

	public bool IsItemAllowed(string itemName)
	{
		if (itemName == "Slingshot")
		{
			return true;
		}
		if (concatStringOfCosmeticsAllowed == null)
		{
			return false;
		}
		if (concatStringOfCosmeticsAllowed.Contains(itemName))
		{
			return true;
		}
		bool canTryOn = CosmeticsController.instance.GetItemFromDict(itemName).canTryOn;
		if (inTryOnRoom && canTryOn)
		{
			return true;
		}
		return false;
	}

	private void LateUpdate()
	{
		base.transform.localScale = Vector3.one * scaleFactor;
		if (isOfflineVRRig || base.photonView.IsMine)
		{
			if (GorillaGameManager.instance != null)
			{
				speedArray = GorillaGameManager.instance.LocalPlayerSpeed();
				GorillaLocomotion.Player.Instance.jumpMultiplier = speedArray[1];
				GorillaLocomotion.Player.Instance.maxJumpSpeed = speedArray[0];
			}
			else
			{
				GorillaLocomotion.Player.Instance.jumpMultiplier = 1.1f;
				GorillaLocomotion.Player.Instance.maxJumpSpeed = 6.5f;
			}
			scaleFactor = GorillaLocomotion.Player.Instance.scale;
			base.transform.localScale = Vector3.one * scaleFactor;
			base.transform.eulerAngles = new Vector3(0f, mainCamera.transform.rotation.eulerAngles.y, 0f);
			base.transform.position = mainCamera.transform.position + headConstraint.rotation * head.trackingPositionOffset * scaleFactor + base.transform.rotation * headBodyOffset * scaleFactor;
			head.MapMine(scaleFactor, playerOffsetTransform);
			rightHand.MapMine(scaleFactor, playerOffsetTransform);
			leftHand.MapMine(scaleFactor, playerOffsetTransform);
			rightIndex.MapMyFinger(lerpValueFingers);
			rightMiddle.MapMyFinger(lerpValueFingers);
			rightThumb.MapMyFinger(lerpValueFingers);
			leftIndex.MapMyFinger(lerpValueFingers);
			leftMiddle.MapMyFinger(lerpValueFingers);
			leftThumb.MapMyFinger(lerpValueFingers);
			reliableState.activeTransferrableObjectIndex = GorillaTagger.Instance.offlineVRRig.reliableState.activeTransferrableObjectIndex;
			reliableState.transferrablePosStates = GorillaTagger.Instance.offlineVRRig.reliableState.transferrablePosStates;
			reliableState.transferrableItemStates = GorillaTagger.Instance.offlineVRRig.reliableState.transferrableItemStates;
			reliableState.extraSerializedState = GorillaTagger.Instance.offlineVRRig.reliableState.extraSerializedState;
			reliableState.lHandState = GorillaTagger.Instance.offlineVRRig.reliableState.lHandState;
			reliableState.rHandState = GorillaTagger.Instance.offlineVRRig.reliableState.rHandState;
			if (XRSettings.loadedDeviceName == "Oculus" && ((isOfflineVRRig && !PhotonNetwork.InRoom) || (!isOfflineVRRig && PhotonNetwork.InRoom)))
			{
				mainSkin.enabled = (OVRManager.hasInputFocus ? true : false);
			}
			if ((isOfflineVRRig && !PhotonNetwork.InRoom) || (!isOfflineVRRig && PhotonNetwork.InRoom))
			{
				mainSkin.enabled = ((!GorillaLocomotion.Player.Instance.inOverlay) ? true : false);
			}
		}
		else
		{
			if (voiceAudio == null)
			{
				voiceAudio = myPhotonVoiceView.SpeakerInUse.GetComponent<AudioSource>();
			}
			if (voiceAudio != null)
			{
				float num = (GorillaTagger.Instance.offlineVRRig.transform.localScale.x - base.transform.localScale.x) / pitchScale + pitchOffset;
				if (!Mathf.Approximately(voiceAudio.pitch, num))
				{
					voiceAudio.pitch = num;
				}
			}
			if (Time.time > timeSpawned + doNotLerpConstant)
			{
				base.transform.position = Vector3.Lerp(base.transform.position, syncPos, lerpValueBody * 0.66f);
			}
			else
			{
				base.transform.position = syncPos;
			}
			base.transform.rotation = Quaternion.Lerp(base.transform.rotation, syncRotation, lerpValueBody);
			base.transform.position = SanitizeVector3(base.transform.position);
			base.transform.rotation = SanitizeQuaternion(base.transform.rotation);
			head.syncPos = base.transform.rotation * -headBodyOffset * scaleFactor;
			head.MapOther(lerpValueBody);
			rightHand.MapOther(lerpValueBody);
			leftHand.MapOther(lerpValueBody);
			rightIndex.MapOtherFinger((float)(handSync % 10) / 10f, lerpValueFingers);
			rightMiddle.MapOtherFinger((float)(handSync % 100) / 100f, lerpValueFingers);
			rightThumb.MapOtherFinger((float)(handSync % 1000) / 1000f, lerpValueFingers);
			leftIndex.MapOtherFinger((float)(handSync % 10000) / 10000f, lerpValueFingers);
			leftMiddle.MapOtherFinger((float)(handSync % 100000) / 100000f, lerpValueFingers);
			leftThumb.MapOtherFinger((float)(handSync % 1000000) / 1000000f, lerpValueFingers);
			leftHandHoldableStatus = handSync % 10000000 / 1000000;
			rightHandHoldableStatus = handSync % 100000000 / 10000000;
			if (!initializedCosmetics && GorillaGameManager.instance != null && GorillaGameManager.instance.playerCosmeticsLookup.TryGetValue(base.photonView.Owner.UserId, out tempString))
			{
				initializedCosmetics = true;
				concatStringOfCosmeticsAllowed = tempString;
				CheckForEarlyAccess();
				SetCosmeticsActive();
				myBodyDockPositions.RefreshTransferrableItems();
			}
		}
		if (!isOfflineVRRig)
		{
			tempMatIndex = ((GorillaGameManager.instance != null) ? GorillaGameManager.instance.MyMatIndex(base.photonView.Owner) : 0);
			if (setMatIndex != tempMatIndex)
			{
				setMatIndex = tempMatIndex;
				ChangeMaterialLocal(setMatIndex);
			}
		}
	}

	public void SetHeadBodyOffset()
	{
	}

	public void VRRigResize(float ratioVar)
	{
		ratio *= ratioVar;
	}

	public int ReturnHandPosition()
	{
		return 0 + Mathf.FloorToInt(rightIndex.calcT * 9.99f) + Mathf.FloorToInt(rightMiddle.calcT * 9.99f) * 10 + Mathf.FloorToInt(rightThumb.calcT * 9.99f) * 100 + Mathf.FloorToInt(leftIndex.calcT * 9.99f) * 1000 + Mathf.FloorToInt(leftMiddle.calcT * 9.99f) * 10000 + Mathf.FloorToInt(leftThumb.calcT * 9.99f) * 100000 + leftHandHoldableStatus * 1000000 + rightHandHoldableStatus * 10000000;
	}

	public void OnDestroy()
	{
		if (!isQuitting)
		{
			if (GorillaParent.instance != null && GorillaParent.instance.vrrigDict != null && creator != null && GorillaParent.instance.vrrigDict.TryGetValue(creator, out var value) && value == this)
			{
				GorillaParent.instance.vrrigDict[creator] = null;
				GorillaParent.instance.vrrigDict.Remove(creator);
			}
			if (base.photonView != null && base.photonView.IsMine && PhotonNetwork.InRoom && !base.photonView.IsRoomView)
			{
				isQuitting = true;
				PhotonNetwork.Destroy(base.photonView);
			}
		}
	}

	void IOnPhotonViewPreNetDestroy.OnPreNetDestroy(PhotonView rootView)
	{
		if (isQuitting || !PhotonNetwork.InRoom || creator == null)
		{
			return;
		}
		Dictionary<Photon.Realtime.Player, VRRig> dictionary = GorillaParent.instance?.vrrigDict;
		if (dictionary != null && dictionary.TryGetValue(creator, out var value) && value == this)
		{
			isQuitting = true;
			dictionary.Remove(creator);
			if (rootView.IsMine)
			{
				PhotonNetwork.Destroy(rootView);
			}
		}
	}

	void IPunInstantiateMagicCallback.OnPhotonInstantiate(PhotonMessageInfo info)
	{
		creator = info.Sender;
		if (creator != null && creator != info.photonView.Owner)
		{
			GorillaNot.instance.SendReport("creating rigs for someone else", creator.UserId, creator.NickName);
			isQuitting = true;
		}
		if (base.photonView.IsRoomView)
		{
			isQuitting = true;
			if (PhotonNetwork.IsMasterClient && base.photonView.IsMine)
			{
				PhotonNetwork.Destroy(base.gameObject);
				Debug.Log("network deleting vrrig");
			}
			else
			{
				base.gameObject.SetActive(value: false);
				Debug.Log("local setting vrrig false");
			}
			if (creator != null)
			{
				GorillaNot.instance.SendReport("creating rigs as room objects", creator.UserId, creator.NickName);
			}
			return;
		}
		if (isQuitting)
		{
			if (info.photonView.IsMine)
			{
				PhotonNetwork.Destroy(info.photonView);
			}
			else
			{
				base.gameObject.SetActive(value: false);
			}
			return;
		}
		timeSpawned = Time.time;
		base.transform.parent = GorillaParent.instance.GetComponent<GorillaParent>().vrrigParent.transform;
		GorillaParent.instance.vrrigs.Add(this);
		if (creator != null && GorillaParent.instance.vrrigDict.TryGetValue(creator, out tempVRRig) && tempVRRig != null && tempVRRig != this)
		{
			GorillaNot.instance.SendReport("inappropriate tag data being sent creating multiple vrrigs", creator.UserId, creator.NickName);
			tempVRRig.isQuitting = true;
			UnityEngine.Object.Destroy(tempVRRig.gameObject);
		}
		if (GorillaParent.instance.vrrigDict.ContainsKey(creator))
		{
			GorillaParent.instance.vrrigDict[creator] = this;
		}
		else
		{
			GorillaParent.instance.vrrigDict.Add(creator, this);
		}
		info.photonView.AddCallbackTarget(this);
		if (GorillaGameManager.instance != null && GorillaGameManager.instance.GetComponent<PhotonView>().IsMine)
		{
			object value;
			bool didTutorial = base.photonView.Owner.CustomProperties.TryGetValue("didTutorial", out value) && !(bool)value;
			Debug.Log("guy who just joined didnt do the tutorial already: " + didTutorial);
			GorillaGameManager.instance.NewVRRig(base.photonView.Owner, base.photonView.ViewID, didTutorial);
		}
		Debug.Log(info.Sender.UserId, this);
		SharedStart();
	}

	void IPunObservable.OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
	{
		if (!isOfflineVRRig && info.Sender == info.photonView.Owner)
		{
			if (stream.IsWriting)
			{
				stream.SendNext(head.rigTarget.localRotation);
				stream.SendNext(rightHand.rigTarget.localPosition);
				stream.SendNext(rightHand.rigTarget.localRotation);
				stream.SendNext(leftHand.rigTarget.localPosition);
				stream.SendNext(leftHand.rigTarget.localRotation);
				stream.SendNext(base.transform.position);
				stream.SendNext(Mathf.RoundToInt(base.transform.rotation.eulerAngles.y));
				stream.SendNext(ReturnHandPosition());
				stream.SendNext(currentState);
			}
			else
			{
				head.syncRotation = SanitizeQuaternion((Quaternion)stream.ReceiveNext());
				rightHand.syncPos = SanitizeVector3((Vector3)stream.ReceiveNext());
				rightHand.syncRotation = SanitizeQuaternion((Quaternion)stream.ReceiveNext());
				leftHand.syncPos = SanitizeVector3((Vector3)stream.ReceiveNext());
				leftHand.syncRotation = SanitizeQuaternion((Quaternion)stream.ReceiveNext());
				syncPos = SanitizeVector3((Vector3)stream.ReceiveNext());
				syncRotation.eulerAngles = SanitizeVector3(new Vector3(0f, (int)stream.ReceiveNext(), 0f));
				handSync = (int)stream.ReceiveNext();
				currentState = (TransferrableObject.PositionState)stream.ReceiveNext();
				lastPosition = syncPos;
				AddVelocityToQueue(syncPos, info);
			}
		}
	}

	public void ChangeMaterial(int materialIndex, PhotonMessageInfo info)
	{
		if (info.Sender == PhotonNetwork.MasterClient)
		{
			ChangeMaterialLocal(materialIndex);
		}
	}

	public void ChangeMaterialLocal(int materialIndex)
	{
		setMatIndex = materialIndex;
		if (setMatIndex > -1 && setMatIndex < materialsToChangeTo.Length)
		{
			mainSkin.material = materialsToChangeTo[setMatIndex];
		}
		if (lavaParticleSystem != null)
		{
			if (!isOfflineVRRig && materialIndex == 2 && lavaParticleSystem.isStopped)
			{
				lavaParticleSystem.Play();
			}
			else if (!isOfflineVRRig && lavaParticleSystem.isPlaying)
			{
				lavaParticleSystem.Stop();
			}
		}
		if (rockParticleSystem != null)
		{
			if (!isOfflineVRRig && materialIndex == 1 && rockParticleSystem.isStopped)
			{
				rockParticleSystem.Play();
			}
			else if (!isOfflineVRRig && rockParticleSystem.isPlaying)
			{
				rockParticleSystem.Stop();
			}
		}
		if (iceParticleSystem != null)
		{
			if (!isOfflineVRRig && materialIndex == 3 && rockParticleSystem.isStopped)
			{
				iceParticleSystem.Play();
			}
			else if (!isOfflineVRRig && iceParticleSystem.isPlaying)
			{
				iceParticleSystem.Stop();
			}
		}
	}

	[PunRPC]
	public void InitializeNoobMaterial(float red, float green, float blue, bool leftHanded, PhotonMessageInfo info)
	{
		IncrementRPC(info, "InitializeNoobMaterial");
		if (info.Sender == base.photonView.Owner && (!initialized || (initialized && GorillaComputer.instance.friendJoinCollider.playerIDsCurrentlyTouching.Contains(info.Sender.UserId))))
		{
			initialized = true;
			red = Mathf.Clamp(red, 0f, 1f);
			green = Mathf.Clamp(green, 0f, 1f);
			blue = Mathf.Clamp(blue, 0f, 1f);
			InitializeNoobMaterialLocal(red, green, blue, leftHanded);
		}
		else
		{
			GorillaNot.instance.SendReport("inappropriate tag data being sent init noob", info.Sender.UserId, info.Sender.NickName);
		}
		playerLeftHanded = leftHanded;
	}

	public void InitializeNoobMaterialLocal(float red, float green, float blue, bool leftHanded)
	{
		materialsToChangeTo[0].color = new Color(red, green, blue);
		if (base.photonView != null)
		{
			playerText.text = NormalizeName(doIt: true, base.photonView.Owner.NickName);
		}
		else if (showName)
		{
			playerText.text = PlayerPrefs.GetString("playerName");
		}
	}

	public string NormalizeName(bool doIt, string text)
	{
		if (doIt)
		{
			if (GorillaComputer.instance.CheckAutoBanListForName(text))
			{
				text = new string(Array.FindAll(text.ToCharArray(), (char c) => char.IsLetterOrDigit(c)));
				if (text.Length > 12)
				{
					text = text.Substring(0, 11);
				}
				text = text.ToUpper();
			}
			else
			{
				text = "BADGORILLA";
			}
		}
		return text;
	}

	public void SetJumpLimitLocal(float maxJumpSpeed)
	{
		GorillaLocomotion.Player.Instance.maxJumpSpeed = maxJumpSpeed;
	}

	public void SetJumpMultiplierLocal(float jumpMultiplier)
	{
		GorillaLocomotion.Player.Instance.jumpMultiplier = jumpMultiplier;
	}

	[PunRPC]
	public void SetTaggedTime(PhotonMessageInfo info)
	{
		IncrementRPC(info, "SetTaggedTime");
		if (GorillaGameManager.instance != null)
		{
			if (info.Sender == PhotonNetwork.MasterClient)
			{
				GorillaTagger.Instance.ApplyStatusEffect(GorillaTagger.StatusEffect.Frozen, GorillaTagger.Instance.tagCooldown);
				GorillaTagger.Instance.StartVibration(forLeftController: true, GorillaTagger.Instance.taggedHapticStrength, GorillaTagger.Instance.taggedHapticDuration);
				GorillaTagger.Instance.StartVibration(forLeftController: false, GorillaTagger.Instance.taggedHapticStrength, GorillaTagger.Instance.taggedHapticDuration);
			}
			else
			{
				GorillaNot.instance.SendReport("inappropriate tag data being sent set tagged time", info.Sender.UserId, info.Sender.NickName);
			}
		}
	}

	[PunRPC]
	public void SetSlowedTime(PhotonMessageInfo info)
	{
		IncrementRPC(info, "SetSlowedTime");
		if (!(GorillaGameManager.instance != null))
		{
			return;
		}
		if (info.Sender == PhotonNetwork.MasterClient)
		{
			if (GorillaTagger.Instance.currentStatus != GorillaTagger.StatusEffect.Slowed)
			{
				GorillaTagger.Instance.StartVibration(forLeftController: true, GorillaTagger.Instance.taggedHapticStrength, GorillaTagger.Instance.taggedHapticDuration);
				GorillaTagger.Instance.StartVibration(forLeftController: false, GorillaTagger.Instance.taggedHapticStrength, GorillaTagger.Instance.taggedHapticDuration);
			}
			GorillaTagger.Instance.ApplyStatusEffect(GorillaTagger.StatusEffect.Slowed, GorillaTagger.Instance.slowCooldown);
		}
		else
		{
			GorillaNot.instance.SendReport("inappropriate tag data being sent set slowed time", info.Sender.UserId, info.Sender.NickName);
		}
	}

	[PunRPC]
	public void SetJoinTaggedTime(PhotonMessageInfo info)
	{
		IncrementRPC(info, "SetJoinTaggedTime");
		if (info.Sender == PhotonNetwork.MasterClient)
		{
			GorillaTagger.Instance.StartVibration(forLeftController: true, GorillaTagger.Instance.taggedHapticStrength, GorillaTagger.Instance.taggedHapticDuration);
			GorillaTagger.Instance.StartVibration(forLeftController: false, GorillaTagger.Instance.taggedHapticStrength, GorillaTagger.Instance.taggedHapticDuration);
		}
		else
		{
			GorillaNot.instance.SendReport("inappropriate tag data being sent set join tagged time", info.Sender.UserId, info.Sender.NickName);
		}
	}

	[PunRPC]
	public void RequestMaterialColor(Photon.Realtime.Player askingPlayer, bool noneBool, PhotonMessageInfo info)
	{
		IncrementRPC(info, "RequestMaterialColor");
		if (base.photonView.IsMine)
		{
			PhotonView.Get(this).RPC("InitializeNoobMaterial", info.Sender, materialsToChangeTo[0].color.r, materialsToChangeTo[0].color.g, materialsToChangeTo[0].color.b, GorillaComputer.instance.leftHanded);
		}
	}

	[PunRPC]
	public void RequestCosmetics(PhotonMessageInfo info)
	{
		IncrementRPC(info, "RequestCosmetics");
		if (base.photonView.IsMine && CosmeticsController.instance != null)
		{
			string[] array = CosmeticsController.instance.currentWornSet.ToDisplayNameArray();
			string[] array2 = CosmeticsController.instance.tryOnSet.ToDisplayNameArray();
			base.photonView.RPC("UpdateCosmeticsWithTryon", info.Sender, array, array2);
		}
	}

	[PunRPC]
	public void PlayTagSound(int soundIndex, float soundVolume, PhotonMessageInfo info)
	{
		IncrementRPC(info, "PlayTagSound");
		if (info.Sender.IsMasterClient)
		{
			tagSound.volume = Mathf.Max(0.25f, soundVolume);
			tagSound.PlayOneShot(clipToPlay[soundIndex]);
		}
		else
		{
			GorillaNot.instance.SendReport("inappropriate tag data being sent play tag sound", info.Sender.UserId, info.Sender.NickName);
		}
	}

	public void Bonk(int soundIndex, float bonkPercent, PhotonMessageInfo info)
	{
		if (info.Sender == base.photonView.Owner)
		{
			if (bonkTime + bonkCooldown < Time.time)
			{
				bonkTime = Time.time;
				tagSound.volume = bonkPercent * 0.25f;
				tagSound.PlayOneShot(clipToPlay[soundIndex]);
				if (base.photonView.IsMine)
				{
					GorillaTagger.Instance.StartVibration(forLeftController: true, GorillaTagger.Instance.taggedHapticStrength, GorillaTagger.Instance.taggedHapticDuration);
					GorillaTagger.Instance.StartVibration(forLeftController: false, GorillaTagger.Instance.taggedHapticStrength, GorillaTagger.Instance.taggedHapticDuration);
				}
			}
		}
		else
		{
			GorillaNot.instance.SendReport("inappropriate tag data being sent bonk", info.Sender.UserId, info.Sender.NickName);
		}
	}

	[PunRPC]
	public void PlayDrum(int drumIndex, float drumVolume, PhotonMessageInfo info)
	{
		IncrementRPC(info, "PlayDrum");
		senderRig = GorillaGameManager.instance.FindVRRigForPlayer(info.Sender).GetComponent<VRRig>();
		if (!(senderRig != null) || senderRig.muted)
		{
			return;
		}
		if (drumIndex >= 0 && drumIndex < musicDrums.Length && (senderRig.transform.position - base.transform.position).magnitude < 3f)
		{
			if (base.photonView.IsMine)
			{
				if (GorillaTagger.Instance.offlineVRRig.musicDrums[drumIndex].gameObject.activeSelf)
				{
					GorillaTagger.Instance.offlineVRRig.musicDrums[drumIndex].time = 0f;
					GorillaTagger.Instance.offlineVRRig.musicDrums[drumIndex].volume = Mathf.Max(Mathf.Min(GorillaComputer.instance.instrumentVolume, drumVolume * GorillaComputer.instance.instrumentVolume), 0f);
					GorillaTagger.Instance.offlineVRRig.musicDrums[drumIndex].Play();
				}
			}
			else if (musicDrums[drumIndex].gameObject.activeSelf)
			{
				musicDrums[drumIndex].time = 0f;
				musicDrums[drumIndex].volume = Mathf.Max(Mathf.Min(GorillaComputer.instance.instrumentVolume, drumVolume * GorillaComputer.instance.instrumentVolume), 0f);
				musicDrums[drumIndex].Play();
			}
		}
		else
		{
			GorillaNot.instance.SendReport("inappropriate tag data being sent drum", info.Sender.UserId, info.Sender.NickName);
		}
	}

	[PunRPC]
	public void PlaySelfOnlyInstrument(int selfOnlyIndex, int noteIndex, float instrumentVol, PhotonMessageInfo info)
	{
		IncrementRPC(info, "PlaySelfOnlyInstrument");
		if (info.Sender != base.photonView.Owner || muted)
		{
			return;
		}
		if (selfOnlyIndex >= 0 && selfOnlyIndex < instrumentSelfOnly.Length && info.Sender == base.photonView.Owner)
		{
			if (instrumentSelfOnly[selfOnlyIndex].gameObject.activeSelf)
			{
				instrumentSelfOnly[selfOnlyIndex].PlayNote(noteIndex, Mathf.Max(Mathf.Min(GorillaComputer.instance.instrumentVolume, instrumentVol * GorillaComputer.instance.instrumentVolume), 0f) / 2f);
			}
		}
		else
		{
			GorillaNot.instance.SendReport("inappropriate tag data being sent self only instrument", info.Sender.UserId, info.Sender.NickName);
		}
	}

	[PunRPC]
	public void PlayHandTap(int soundIndex, bool isLeftHand, float tapVolume, PhotonMessageInfo info)
	{
		IncrementRPC(info, "PlayHandTap");
		if (info.Sender == base.photonView.Owner)
		{
			PlayHandTapLocal(soundIndex, isLeftHand, Mathf.Max(tapVolume, 0.1f));
		}
		else
		{
			GorillaNot.instance.SendReport("inappropriate tag data being sent hand tap", info.Sender.UserId, info.Sender.NickName);
		}
	}

	public void PlayHandTapLocal(int soundIndex, bool isLeftHand, float tapVolume)
	{
		if (soundIndex > -1 && soundIndex < GorillaLocomotion.Player.Instance.materialData.Count)
		{
			if (isLeftHand)
			{
				leftHandPlayer.volume = tapVolume;
				leftHandPlayer.clip = (GorillaLocomotion.Player.Instance.materialData[soundIndex].overrideAudio ? GorillaLocomotion.Player.Instance.materialData[soundIndex].audio : GorillaLocomotion.Player.Instance.materialData[0].audio);
				leftHandPlayer.PlayOneShot(leftHandPlayer.clip);
			}
			else
			{
				rightHandPlayer.volume = tapVolume;
				rightHandPlayer.clip = (GorillaLocomotion.Player.Instance.materialData[soundIndex].overrideAudio ? GorillaLocomotion.Player.Instance.materialData[soundIndex].audio : GorillaLocomotion.Player.Instance.materialData[0].audio);
				rightHandPlayer.PlayOneShot(rightHandPlayer.clip);
			}
		}
	}

	[PunRPC]
	public void UpdateCosmetics(string[] currentItems, PhotonMessageInfo info)
	{
		IncrementRPC(info, "UpdateCosmetics");
		if (info.Sender == base.photonView.Owner)
		{
			CosmeticsController.CosmeticSet newSet = new CosmeticsController.CosmeticSet(currentItems, CosmeticsController.instance);
			LocalUpdateCosmetics(newSet);
		}
		else
		{
			GorillaNot.instance.SendReport("inappropriate tag data being sent update cosmetics", info.Sender.UserId, info.Sender.NickName);
		}
	}

	[PunRPC]
	public void UpdateCosmeticsWithTryon(string[] currentItems, string[] tryOnItems, PhotonMessageInfo info)
	{
		IncrementRPC(info, "UpdateCosmeticsWithTryon");
		if (info.Sender == base.photonView.Owner)
		{
			CosmeticsController.CosmeticSet newSet = new CosmeticsController.CosmeticSet(currentItems, CosmeticsController.instance);
			CosmeticsController.CosmeticSet newTryOnSet = new CosmeticsController.CosmeticSet(tryOnItems, CosmeticsController.instance);
			LocalUpdateCosmeticsWithTryon(newSet, newTryOnSet);
		}
		else
		{
			GorillaNot.instance.SendReport("inappropriate tag data being sent update cosmetics with tryon", info.Sender.UserId, info.Sender.NickName);
		}
	}

	public void UpdateAllowedCosmetics()
	{
		if (GorillaGameManager.instance != null && GorillaGameManager.instance.playerCosmeticsLookup.TryGetValue(base.photonView.Owner.UserId, out tempString))
		{
			concatStringOfCosmeticsAllowed = tempString;
			CheckForEarlyAccess();
		}
	}

	public void LocalUpdateCosmetics(CosmeticsController.CosmeticSet newSet)
	{
		cosmeticSet = newSet;
		if (initializedCosmetics)
		{
			SetCosmeticsActive();
		}
	}

	public void LocalUpdateCosmeticsWithTryon(CosmeticsController.CosmeticSet newSet, CosmeticsController.CosmeticSet newTryOnSet)
	{
		cosmeticSet = newSet;
		tryOnSet = newTryOnSet;
		if (initializedCosmetics)
		{
			SetCosmeticsActive();
		}
	}

	private void CheckForEarlyAccess()
	{
		if (IsItemAllowed("Early Access Supporter Pack"))
		{
			concatStringOfCosmeticsAllowed += "LBAAE.LFAAM.LFAAN.LHAAA.LHAAK.LHAAL.LHAAM.LHAAN.LHAAO.LHAAP.LHABA.LHABB.";
		}
		initializedCosmetics = true;
	}

	public void SetCosmeticsActive()
	{
		if (!(CosmeticsController.instance == null))
		{
			prevSet.CopyItems(mergedSet);
			mergedSet.MergeSets(inTryOnRoom ? tryOnSet : null, cosmeticSet);
			BodyDockPositions component = GetComponent<BodyDockPositions>();
			mergedSet.ActivateCosmetics(prevSet, this, component, CosmeticsController.instance.nullItem, cosmeticsObjectRegistry);
		}
	}

	public void GetUserCosmeticsAllowed()
	{
		if (CosmeticsController.instance != null)
		{
			PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), delegate (GetUserInventoryResult result)
			{
				foreach (ItemInstance item in result.Inventory)
				{
					if (item.CatalogVersion == CosmeticsController.instance.catalog)
					{
						concatStringOfCosmeticsAllowed += item.ItemId;
					}
				}
				Debug.Log("successful result. allowed cosmetics are: " + concatStringOfCosmeticsAllowed);
				CheckForEarlyAccess();
				SetCosmeticsActive();
			}, delegate (PlayFabError error)
			{
				Debug.Log("Got error retrieving user data:");
				Debug.Log(error.GenerateErrorReport());
				initializedCosmetics = true;
				SetCosmeticsActive();
			});
		}
		concatStringOfCosmeticsAllowed += "Slingshot";
	}

	private void Quitting()
	{
		isQuitting = true;
	}

	public void GenerateFingerAngleLookupTables()
	{
		GenerateTableIndex(ref leftIndex);
		GenerateTableIndex(ref rightIndex);
		GenerateTableMiddle(ref leftMiddle);
		GenerateTableMiddle(ref rightMiddle);
		GenerateTableThumb(ref leftThumb);
		GenerateTableThumb(ref rightThumb);
	}

	private void GenerateTableThumb(ref VRMapThumb thumb)
	{
		thumb.angle1Table = new Quaternion[11];
		thumb.angle2Table = new Quaternion[11];
		for (int i = 0; i < thumb.angle1Table.Length; i++)
		{
			Debug.Log((float)i / 10f);
			thumb.angle1Table[i] = Quaternion.Lerp(Quaternion.Euler(thumb.startingAngle1), Quaternion.Euler(thumb.closedAngle1), (float)i / 10f);
			thumb.angle2Table[i] = Quaternion.Lerp(Quaternion.Euler(thumb.startingAngle2), Quaternion.Euler(thumb.closedAngle2), (float)i / 10f);
		}
	}

	private void GenerateTableIndex(ref VRMapIndex index)
	{
		index.angle1Table = new Quaternion[11];
		index.angle2Table = new Quaternion[11];
		index.angle3Table = new Quaternion[11];
		for (int i = 0; i < index.angle1Table.Length; i++)
		{
			index.angle1Table[i] = Quaternion.Lerp(Quaternion.Euler(index.startingAngle1), Quaternion.Euler(index.closedAngle1), (float)i / 10f);
			index.angle2Table[i] = Quaternion.Lerp(Quaternion.Euler(index.startingAngle2), Quaternion.Euler(index.closedAngle2), (float)i / 10f);
			index.angle3Table[i] = Quaternion.Lerp(Quaternion.Euler(index.startingAngle3), Quaternion.Euler(index.closedAngle3), (float)i / 10f);
		}
	}

	private void GenerateTableMiddle(ref VRMapMiddle middle)
	{
		middle.angle1Table = new Quaternion[11];
		middle.angle2Table = new Quaternion[11];
		middle.angle3Table = new Quaternion[11];
		for (int i = 0; i < middle.angle1Table.Length; i++)
		{
			middle.angle1Table[i] = Quaternion.Lerp(Quaternion.Euler(middle.startingAngle1), Quaternion.Euler(middle.closedAngle1), (float)i / 10f);
			middle.angle2Table[i] = Quaternion.Lerp(Quaternion.Euler(middle.startingAngle2), Quaternion.Euler(middle.closedAngle2), (float)i / 10f);
			middle.angle3Table[i] = Quaternion.Lerp(Quaternion.Euler(middle.startingAngle3), Quaternion.Euler(middle.closedAngle3), (float)i / 10f);
		}
	}

	private Quaternion SanitizeQuaternion(Quaternion quat)
	{
		if (float.IsNaN(quat.w) || float.IsNaN(quat.x) || float.IsNaN(quat.y) || float.IsNaN(quat.z) || float.IsInfinity(quat.w) || float.IsInfinity(quat.x) || float.IsInfinity(quat.y) || float.IsInfinity(quat.z))
		{
			return Quaternion.identity;
		}
		return quat;
	}

	private Vector3 SanitizeVector3(Vector3 vec)
	{
		if (float.IsNaN(vec.x) || float.IsNaN(vec.y) || float.IsNaN(vec.z) || float.IsInfinity(vec.x) || float.IsInfinity(vec.y) || float.IsInfinity(vec.z))
		{
			return Vector3.zero;
		}
		return Vector3.ClampMagnitude(vec, 1000f);
	}

	private void IncrementRPC(PhotonMessageInfo info, string sourceCall)
	{
		if (GorillaGameManager.instance != null)
		{
			GorillaNot.IncrementRPCCall(info, sourceCall);
		}
	}

	private void AddVelocityToQueue(Vector3 position, PhotonMessageInfo info)
	{
		Vector3 velocity;
		if (velocityHistoryList.Count == 0)
		{
			velocity = Vector3.zero;
			lastPosition = position;
		}
		else
		{
			velocity = (position - lastPosition) / (float)(info.SentServerTime - velocityHistoryList[0].time);
		}
		velocityHistoryList.Insert(0, new VelocityTime(velocity, info.SentServerTime));
		if (velocityHistoryList.Count > velocityHistoryMaxLength)
		{
			velocityHistoryList.RemoveRange(velocityHistoryMaxLength, velocityHistoryList.Count - velocityHistoryMaxLength);
		}
	}

	private Vector3 ReturnVelocityAtTime(double timeToReturn)
	{
		if (velocityHistoryList.Count <= 1)
		{
			return Vector3.zero;
		}
		int num = 0;
		int num2 = velocityHistoryList.Count - 1;
		int num3 = 0;
		if (num2 == num)
		{
			return velocityHistoryList[num].vel;
		}
		while (num2 - num > 1 && num3 < 1000)
		{
			num3++;
			int num4 = (num2 - num) / 2;
			if (velocityHistoryList[num4].time > timeToReturn)
			{
				num2 = num4;
			}
			else
			{
				num = num4;
			}
		}
		float num5 = (float)(velocityHistoryList[num].time - timeToReturn);
		double num6 = velocityHistoryList[num].time - velocityHistoryList[num2].time;
		if (num6 == 0.0)
		{
			num6 = 0.001;
		}
		num5 /= (float)num6;
		num5 = Mathf.Clamp(num5, 0f, 1f);
		return Vector3.Lerp(velocityHistoryList[num].vel, velocityHistoryList[num2].vel, num5);
	}
}
