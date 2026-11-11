using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Photon.Pun;
using Photon.Realtime;
using PlayFab;
using PlayFab.ClientModels;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace GorillaNetworking
{
	public class PlayFabAuthenticator : MonoBehaviour
	{
		public static volatile PlayFabAuthenticator instance;

		public string _playFabPlayerIdCache;

		private string _sessionTicket;

		private string _displayName;

		public string userID;

		private string orgScopedID;

		private string userToken;

		private bool enableCustomAuth;

		public GorillaComputer gorillaComputer;

		private byte[] m_Ticket;

		private uint m_pcbTicket;

		public Text debugText;

		public bool screenDebugMode;

		public bool loginFailed;

		public string loginDisplayID;

		public GameObject emptyObject;

		private HAuthTicket m_HAuthTicket;

		private byte[] ticketBlob;

		private uint ticketSize;

		protected Callback<GetAuthSessionTicketResponse_t> m_GetAuthSessionTicketResponse;

		private string isup = "true";

		private string version = "live1122";

		public void Awake()
		{
			PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = "9c5612c9-328a-48a2-92e0-ed33cdbfeb88";
			PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice = "960e4c20-8d21-4c30-b0b3-e1c598d9bdc0";
			PlayFabSettings.TitleId = "114812";
			if (instance == null)
			{
				instance = this;
			}
			else if (instance != this)
			{
				UnityEngine.Object.Destroy(base.gameObject);
			}
			if (screenDebugMode)
			{
				debugText.text = "";
			}
			Debug.Log("doing steam thing");
			OnGetAuthSessionTicketResponse();
			AuthenticateWithPlayFab();
			PlayFabSettings.DisableFocusTimeCollection = true;
		}

		public void AuthenticateWithPlayFab()
		{
			if (!loginFailed)
			{
				Debug.Log("authenticating with playFab!");
				if (SteamManager.Initialized)
				{
					Debug.Log("trying to auth with steam");
					m_HAuthTicket = SteamUser.GetAuthSessionTicket(ticketBlob, ticketBlob.Length, out ticketSize);
				}
			}
		}

		private void ClientGetTitleData(LoginResult obj)
		{
			FalconStaffRole.FetchRole(); // Falcon staff: cache own falcon_role right after login
			LogMessage("Playfab authenticated ... Getting Title Data");
			enableCustomAuth = false;
			PlayFabClientAPI.GetTitleData(new GetTitleDataRequest(), delegate (GetTitleDataResult result)
			{
				if (result.Data == null || !result.Data.ContainsKey("EnableCustomAuthentication"))
				{
					Debug.Log("Couldn't find EnableCustomAuthentication in the title data");
					enableCustomAuth = false;
				}
				else
				{
					enableCustomAuth = result.Data["EnableCustomAuthentication"] == "true";
				}
				RequestPhotonToken(obj);
			}, delegate (PlayFabError error)
			{
				Debug.Log("Got error getting titleData:");
				Debug.Log(error.GenerateErrorReport());
				RequestPhotonToken(obj);
			});
		}

		private void RequestPhotonToken(LoginResult obj)
		{
			LogMessage("Received Title Data. Requesting photon token...");
			_playFabPlayerIdCache = obj.PlayFabId;
			_sessionTicket = obj.SessionTicket;
			PlayFabClientAPI.GetPhotonAuthenticationToken(new GetPhotonAuthenticationTokenRequest
			{
				PhotonApplicationId = PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime
			}, AuthenticateWithPhoton, OnPlayFabError);
		}

		private void AuthenticateWithPhoton(GetPhotonAuthenticationTokenResult obj)
		{
			AuthenticationValues authenticationValues = new AuthenticationValues(PlayFabSettings.DeviceUniqueIdentifier);
			authenticationValues.AuthType = CustomAuthenticationType.Custom;
			string playFabPlayerIdCache = _playFabPlayerIdCache;
			_ = obj.PhotonCustomAuthenticationToken;
			authenticationValues.AddAuthParameter("username", _playFabPlayerIdCache);
			authenticationValues.AddAuthParameter("token", obj.PhotonCustomAuthenticationToken);
			if (enableCustomAuth)
			{
				Dictionary<string, object> authPostData = new Dictionary<string, object>
				{
					{ "UserId", playFabPlayerIdCache },
					{
						"AppId",
						PlayFabSettings.TitleId
					},
					{
						"AppVersion",
						PhotonNetwork.AppVersion ?? "-1"
					},
					{ "Ticket", _sessionTicket },
					{ "Token", obj.PhotonCustomAuthenticationToken }
				};
				authenticationValues.SetAuthPostData(authPostData);
			}
			PhotonNetwork.AuthValues = authenticationValues;
			GetPlayerDisplayName(_playFabPlayerIdCache);
			PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
			{
				FunctionName = "AddOrRemoveDLCOwnership",
				FunctionParameter = new { }
			}, delegate
			{
				Debug.Log("got results! updating!");
				if (GorillaTagger.Instance != null)
				{
					GorillaTagger.Instance.offlineVRRig.GetUserCosmeticsAllowed();
				}
			}, delegate (PlayFabError error)
			{
				Debug.Log("Got error retrieving user data:");
				Debug.Log(error.GenerateErrorReport());
				if (GorillaTagger.Instance != null)
				{
					GorillaTagger.Instance.offlineVRRig.GetUserCosmeticsAllowed();
				}
			});
			if (CosmeticsController.instance != null)
			{
				Debug.Log("itinitalizing cosmetics");
				CosmeticsController.instance.Initialize();
			}
			if (gorillaComputer != null)
			{
				gorillaComputer.OnConnectedToMasterStuff();
			}
			if (PhotonNetworkController.Instance != null)
			{
				PhotonNetworkController.Instance.InitiateConnection();
			}
			IsUp();
			checkforupdate();
			SetMotd();
		}

		private void OnPlayFabError(PlayFabError obj)
		{
			LogMessage(obj.ErrorMessage);
			Debug.Log(obj.ErrorMessage);
			loginFailed = true;
			if (obj.ErrorMessage == "The account making this request is currently banned")
			{
				using (Dictionary<string, List<string>>.Enumerator enumerator = obj.ErrorDetails.GetEnumerator())
				{
					if (enumerator.MoveNext())
					{
						KeyValuePair<string, List<string>> current = enumerator.Current;
						if (current.Value[0] != "Indefinite")
						{
							gorillaComputer.GeneralFailureMessage("YOUR ACCOUNT " + loginDisplayID + " HAS BEEN BANNED FROM LEGACY RUNNERS. YOU WILL NOT BE ABLE TO PLAY UNTIL THE BAN EXPIRES.\nREASON: " + current.Key + "\nHOURS LEFT: " + (int)((DateTime.Parse(current.Value[0]) - DateTime.UtcNow).TotalHours + 1.0));
						}
						else
						{
							gorillaComputer.GeneralFailureMessage("YOUR ACCOUNT " + loginDisplayID + " HAS BEEN BANNED FROM LEGACY RUNNERS INDEFINITELY.\nREASON: " + current.Key);
						}
					}
					return;
				}
			}
			if (obj.ErrorMessage == "The IP making this request is currently banned")
			{
				using (Dictionary<string, List<string>>.Enumerator enumerator2 = obj.ErrorDetails.GetEnumerator())
				{
					if (enumerator2.MoveNext())
					{
						KeyValuePair<string, List<string>> current2 = enumerator2.Current;
						if (current2.Value[0] != "Indefinite")
						{
							gorillaComputer.GeneralFailureMessage("THIS IP HAS BEEN BANNED FROM LEGACY RUNNERS. YOU WILL NOT BE ABLE TO PLAY UNTIL THE BAN EXPIRES.\nREASON: " + current2.Key + "\nHOURS LEFT: " + (int)((DateTime.Parse(current2.Value[0]) - DateTime.UtcNow).TotalHours + 1.0));
						}
						else
						{
							gorillaComputer.GeneralFailureMessage("THIS IP HAS BEEN BANNED FROM LEGACY RUNNERS INDEFINITELY.\nREASON: " + current2.Key);
						}
					}
					return;
				}
			}
			if (gorillaComputer != null)
			{
				gorillaComputer.GeneralFailureMessage(gorillaComputer.unableToConnect);
			}
		}

		private static void AddGenericId(string serviceName, string userId)
		{
			PlayFabClientAPI.AddGenericID(new AddGenericIDRequest
			{
				GenericId = new GenericServiceId
				{
					ServiceName = serviceName,
					UserId = userId
				}
			}, delegate
			{
			}, delegate
			{
				Debug.LogError("Error setting generic id");
			});
		}

		public void LogMessage(string message)
		{
		}

		private void GetPlayerDisplayName(string playFabId)
		{
			PlayFabClientAPI.GetPlayerProfile(new GetPlayerProfileRequest
			{
				PlayFabId = playFabId,
				ProfileConstraints = new PlayerProfileViewConstraints
				{
					ShowDisplayName = true
				}
			}, delegate (GetPlayerProfileResult result)
			{
				_displayName = result.PlayerProfile.DisplayName;
			}, delegate (PlayFabError error)
			{
				Debug.LogError(error.GenerateErrorReport());
			});
		}

		public void SetDisplayName(string playerName)
		{
			if (_displayName == null || (_displayName.Length > 4 && _displayName.Substring(0, _displayName.Length - 4) != playerName))
			{
				PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest
				{
					DisplayName = playerName
				}, delegate
				{
					_displayName = playerName;
				}, delegate (PlayFabError error)
				{
					Debug.LogError(error.GenerateErrorReport());
				});
			}
		}

		public void ScreenDebug(string debugString)
		{
			Debug.Log(debugString);
			if (screenDebugMode)
			{
				Text text = debugText;
				text.text = text.text + debugString + "\n";
			}
		}

		public void ScreenDebugClear()
		{
			debugText.text = "";
		}

		public string GetSteamAuthTicket()
		{
			Array.Resize(ref ticketBlob, (int)ticketSize);
			StringBuilder stringBuilder = new StringBuilder();
			byte[] array = ticketBlob;
			foreach (byte b in array)
			{
				stringBuilder.AppendFormat("{0:x2}", b);
			}
			return stringBuilder.ToString();
		}

		public PlayFabAuthenticator()
		{
			loginDisplayID = "";
			ticketBlob = new byte[1024];
			ticketBlob = new byte[1024];
		}

		private void OnGetAuthSessionTicketResponse()
		{
			PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest
			{
				CreateAccount = true,
				CustomId = PlayFabSettings.DeviceUniqueIdentifier
			}, ClientGetTitleData, OnPlayFabError);
		}

		private void SetMotd()
		{

		}

		private void checkforupdate()
		{

		}

		private void IsUp()
		{

		}
		private void OnApplicationQuit()
		{
			PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = "NULL";
			PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice = "NULL";
			PlayFabSettings.TitleId = "NULL";
			Debug.Log("Application Quitting");
			Debug.Log("Hideing IDS??");
		}
	}
}