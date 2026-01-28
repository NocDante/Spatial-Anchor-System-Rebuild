using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;

public class AnchorLoader : MonoBehaviour
{
    private GameObject[] Prefabs;  // 錨點的預製體陣列
    public List<GameObject> PicturesAndWindow = new List<GameObject>();
    private SpatialAnchorManager spatialAnchorManager;

    private Action<OVRSpatialAnchor.UnboundAnchor, bool> _onLoadAnchor;
    //public bool HadUUid = false;

    // 新增：儲存 UUID、PrefabIndex 和 Visibility 的列表
    private List<(Guid uuid, int prefabIndex, bool visibility)> loadedAnchorInfo = new List<(Guid, int, bool)>();

    private bool isEnd=false;
    private Animator pictureAnimator;

    private void Awake()
    {
        spatialAnchorManager = GetComponent<SpatialAnchorManager>();
        Prefabs = spatialAnchorManager.Prefabs;
        _onLoadAnchor = OnLocalized;
    }

    public void LoadAnchorsByUuid()
    {
        if (!PlayerPrefs.HasKey(SpatialAnchorManager.NumUuidsPlayerPref))
        {
            PlayerPrefs.SetInt(SpatialAnchorManager.NumUuidsPlayerPref, 0);
        }

        int playerUuidCount = PlayerPrefs.GetInt(SpatialAnchorManager.NumUuidsPlayerPref);
        if (playerUuidCount == 0) return;
        
        loadedAnchorInfo.Clear();

        // 將所有 UUID、PrefabIndex 和 Visibility 加入列表
        for (int i = 0; i < playerUuidCount; ++i)
        {
            var uuidKey = "uuid" + i;
            var prefabIndexKey = "prefabIndex" + i;
            var visibilityKey = "visibility" + i;

            var currentUuid = PlayerPrefs.GetString(uuidKey);
            int prefabIndex = PlayerPrefs.GetInt(prefabIndexKey);
            bool visibility = PlayerPrefs.GetInt(visibilityKey) == 1;  // 轉為布林值
           

            loadedAnchorInfo.Add((new Guid(currentUuid), prefabIndex, visibility));
            Debug.Log("loadByUuid is sucess");
        }

        Load(loadedAnchorInfo.ConvertAll(info => info.uuid));
    }

    // 用於緩存未綁定的錨點列表，減少 GC 壓力
    List<OVRSpatialAnchor.UnboundAnchor> _unboundAnchors = new();

    public async void Load(IEnumerable<Guid> uuids)
    {
        var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuids, _unboundAnchors);

        if (result.Success)
        {
            Debug.Log("Anchors loaded successfully.");

            foreach (var unboundAnchor in result.Value)
            {
                bool success = await unboundAnchor.LocalizeAsync();
                if (success)
                {
                    // 根據 UUID 查找對應的 prefabIndex 和 Visibility
                    var anchorInfo = loadedAnchorInfo.Find(info => info.uuid == unboundAnchor.Uuid);
                    int prefabIndex = anchorInfo.prefabIndex;
                    bool visibility = anchorInfo.visibility;

                    // 實例化對應的預製體
                    var prefab = Prefabs[prefabIndex];
                    var spatialAnchorObject = Instantiate(prefab, unboundAnchor.Pose.position, unboundAnchor.Pose.rotation);
                    PicturesAndWindow.Add(spatialAnchorObject.gameObject);


                    // 將錨點綁定到實例化的物件
                    spatialAnchorObject.AddComponent<OVRSpatialAnchor>();
                    unboundAnchor.BindTo(spatialAnchorObject.GetComponent<OVRSpatialAnchor>());

                    isEnd = PlayerPrefs.GetInt("IsEnd", 0) == 1;
                    if(isEnd==false)
                    {
                        spatialAnchorObject.SetActive(visibility);
                    }
                    else if(isEnd==true)
                    {
                        EndFunction(spatialAnchorObject,prefabIndex);
                        
                    }

                    Debug.Log($"Anchor {unboundAnchor.Uuid} localized and prefab instantiated with visibility: {visibility}");
                }
                else
                {
                    Debug.LogError($"Localization failed for anchor {unboundAnchor.Uuid}");
                }
            }
        }
        else
        {
            Debug.LogError($"Load failed with error {result.Status}.");
        }
    }

    private void OnLocalized(OVRSpatialAnchor.UnboundAnchor unboundAnchor, bool success)
    {
        if (!success) return;

        var pose = unboundAnchor.Pose;

        // 找到對應的 prefabIndex 和 Visibility
        var anchorInfo = loadedAnchorInfo.Find(info => info.uuid == unboundAnchor.Uuid);
        int prefabIndex = anchorInfo.prefabIndex;
        bool visibility = anchorInfo.visibility;

        // 使用 prefabIndex 創建對應的錨點預製體
        var spatialAnchor = Instantiate(Prefabs[prefabIndex], pose.position, pose.rotation);
        spatialAnchor.AddComponent<OVRSpatialAnchor>();
        unboundAnchor.BindTo(spatialAnchor.GetComponent<OVRSpatialAnchor>());

        // isEnd = PlayerPrefs.GetInt("IsEnd", 0) == 1;
        // if(isEnd==false)
        // {
        //     spatialAnchor.SetActive(visibility);
        // }
        // else if(isEnd==true)
        // {
        //     EndFunction(spatialAnchor,prefabIndex);
        // }
        

    }

    void EndFunction(GameObject AnchorPrefab,int prefabIndex )
    {
        if(prefabIndex==0 || prefabIndex==4 || prefabIndex==7 || prefabIndex==8)
        {
            AnchorPrefab.SetActive(false);
        }
        else if(prefabIndex==1 || prefabIndex==2 || prefabIndex==3 || prefabIndex==5 || prefabIndex==6 )
        {
            AnchorPrefab.SetActive(true);
            
            if(prefabIndex==6)
            {
                StartCoroutine(WaitAndPlayAnimation(AnchorPrefab));
            }
        }

    }

    private IEnumerator WaitAndPlayAnimation(GameObject AnchorPrefab)
    {
        yield return null;  // 等待 1 ?，確保物件完全初始化

        Animator pictureAnimator = AnchorPrefab.GetComponentInChildren<Animator>();
        if (pictureAnimator != null)
        {
            pictureAnimator.Play("MR_FamilyPictureDrift", 0, 0.07f);
        }
        else
        {
            Debug.LogWarning("Animator not found on prefabIndex 6.");
        }
    }

   
}
