using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LitJson;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace GorillaNetworking
{
    public class PlayFabTitleDataCache : MonoBehaviour
    {
        private class DataRequest
        {
            public string Name { get; set; }
            public Action<string> Callback { get; set; }
            public Action<PlayFabError> ErrorCallback { get; set; }
        }

        private const string FileName = "TitleDataCache.json";
        private const float UpdateInterval = 30f; // Update every 30 seconds

        private List<DataRequest> requests = new List<DataRequest>();
        private Dictionary<string, string> titleData = new Dictionary<string, string>();
        private Dictionary<string, int> dataVersions = new Dictionary<string, int>();
        private bool isDataUpToDate;

        public static PlayFabTitleDataCache Instance { get; private set; }
        private static string FilePath => Path.Combine(Application.persistentDataPath, "TitleDataCache.json");

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject); // Don't destroy the cache object when loading scenes
            StartCoroutine(UpdateDataPeriodically());
        }

        private IEnumerator UpdateDataPeriodically()
        {
            while (true)
            {
                yield return new WaitForSeconds(UpdateInterval);
                if (!isDataUpToDate)
                {
                    LoadDataFromFile(FilePath);
                    UpdateData();
                }
            }
        }

        public void GetTitleData(string name, Action<string> callback, Action<PlayFabError> errorCallback)
        {
            if (isDataUpToDate && titleData.ContainsKey(name))
            {
                callback?.Invoke(titleData[name]);
                return;
            }
            DataRequest item = new DataRequest
            {
                Name = name,
                Callback = callback,
                ErrorCallback = errorCallback
            };
            requests.Add(item);
        }

        private void LoadDataFromFile(string filepath)
        {
            try
            {
                if (!File.Exists(filepath))
                {
                    Debug.LogWarning("Title data file " + filepath + " does not exist!");
                    return;
                }
                string json = File.ReadAllText(filepath);
                titleData = JsonMapper.ToObject<Dictionary<string, string>>(json);
                if (titleData.ContainsKey("Versions"))
                {
                    dataVersions = JsonMapper.ToObject<Dictionary<string, int>>(titleData["Versions"]);
                }
            }
            catch (Exception arg)
            {
                Debug.LogError($"Error reading PlayFab title data from file: {arg}");
            }
        }

        private void SaveDataToFile(string filepath)
        {
            try
            {
                string value = JsonMapper.ToJson(dataVersions);
                titleData["Versions"] = value;
                string contents = JsonMapper.ToJson(titleData);
                File.WriteAllText(filepath, contents);
            }
            catch (Exception arg)
            {
                Debug.LogError($"Error writing PlayFab title data to file: {arg}");
            }
        }

        private void UpdateData()
        {
            PlayFabClientAPI.GetTitleData(new GetTitleDataRequest
            {
                Keys = new List<string> { "Versions" }
            }, (data) =>
            {
                if (data.Data.TryGetValue("Versions", out var value))
                {
                    Dictionary<string, int> newDataVersions = JsonMapper.ToObject<Dictionary<string, int>>(value);
                    Debug.Log("Got new version data: " + value);
                    UpdateData(newDataVersions);
                }
                else
                {
                    Debug.LogError("Could not find dataVersionKey in PlayFab title data");
                }
            }, (e) =>
            {
                Debug.LogError(e);
                ClearRequestWithError(e);
            });
        }

        private void UpdateData(Dictionary<string, int> newDataVersions)
        {
            List<string> list = new List<string>();
            foreach (KeyValuePair<string, int> newDataVersion in newDataVersions)
            {
                if (!dataVersions.TryGetValue(newDataVersion.Key, out var value) || newDataVersion.Value != value)
                {
                    list.Add(newDataVersion.Key);
                }
                dataVersions[newDataVersion.Key] = newDataVersion.Value;
            }
            if (list.Count > 0)
            {
                Debug.Log("Versions to update: " + string.Join(", ", list));
            }
            FetchData(list);
        }

        private void FetchData(List<string> keysToUpdate)
        {
            requests.RemoveAll((request) =>
            {
                if (titleData.ContainsKey(request.Name) && !keysToUpdate.Contains(request.Name))
                {
                    request.Callback?.Invoke(titleData[request.Name]);
                    return true;
                }
                return false;
            });

            keysToUpdate = keysToUpdate.Union(requests.Select(request => request.Name)).ToList();

            if (keysToUpdate.Count == 0)
            {
                isDataUpToDate = true;
                return;
            }

            PlayFabClientAPI.GetTitleData(new GetTitleDataRequest
            {
                Keys = keysToUpdate
            }, (data) =>
            {
                foreach (KeyValuePair<string, string> datum in data.Data)
                {
                    titleData[datum.Key] = datum.Value;
                }
                if (data.Data.Count > 0)
                {
                    SaveDataToFile(FilePath);
                }
                requests.RemoveAll((request) =>
                {
                    if (titleData.TryGetValue(request.Name, out var value))
                    {
                        request.Callback?.Invoke(value);
                        return true;
                    }
                    return false;
                });
                ClearRequestWithError();
                isDataUpToDate = true;
            }, (e) =>
            {
                Debug.LogError(e);
                ClearRequestWithError(e);
                isDataUpToDate = true;
            });
        }

        private void ClearRequestWithError(PlayFabError e = null)
        {
            if (e == null)
            {
                e = new PlayFabError();
            }
            foreach (DataRequest request in requests)
            {
                request.ErrorCallback?.Invoke(e);
            }
            requests.Clear();
        }
    }
}
