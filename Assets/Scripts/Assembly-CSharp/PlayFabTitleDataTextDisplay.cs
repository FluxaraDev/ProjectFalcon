using GorillaNetworking;
using PlayFab;
using UnityEngine;
using UnityEngine.UI;

public class PlayFabTitleDataTextDisplay : MonoBehaviour
{
	[SerializeField]
	private Text textBox;

	[Tooltip("PlayFab Title Data key from where to pull display text")]
	[SerializeField]
	private string playfabKey;

	[Tooltip("Should append current app version to Playfab key for version specific display text")]
	[SerializeField]
	private bool appendAppVersion;

	[Tooltip("Text to display when error occurs during fetch")]
	[TextArea(3, 5)]
	[SerializeField]
	private string fallbackText;

	private string PlayFabKey
	{
		get
		{
			if (!appendAppVersion)
			{
				return playfabKey;
			}
			return playfabKey + "_" + Application.version;
		}
	}

	private void Start()
	{
		PlayFabTitleDataCache.Instance.GetTitleData(PlayFabKey, OnTitleDataRequestComplete, OnPlayFabError);
	}

	private void OnPlayFabError(PlayFabError error)
	{
		textBox.text = fallbackText;
	}

	private void OnTitleDataRequestComplete(string titleDataResult)
	{
		string text = titleDataResult.Replace("\\r", "\r").Replace("\\n", "\n");
		if (text[0] == '"' && text[text.Length - 1] == '"')
		{
			text = text.Substring(1, text.Length - 2);
		}
		textBox.text = text;
	}
}
