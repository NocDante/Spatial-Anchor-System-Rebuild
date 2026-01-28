using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class SpatialAnchorManager : MonoBehaviour
{

    //結尾流程
   
    //決定是否進入場景編輯模式
    public bool SceneEditmode = true;

    // 用於生成錨點的預製體陣列
    public GameObject[] Prefabs;
    private int currentPrefabIndex = 0;  // 當前選擇的預製體索引

    // 用於存儲 UUID 數量的 PlayerPref 鍵
    public const string NumUuidsPlayerPref = "numUuids";

    // 旋轉軸：0 = X軸, 1 = Y軸, 2 = Z軸
    private int currentRotationAxis = 0;

    // 存儲所有生成的錨點及其資訊
    private List<OVRSpatialAnchor> AnchorsInf = new List<OVRSpatialAnchor>();
    private OVRSpatialAnchor lastCreatedAnchor;  // 存儲最後創建的錨點
    private AnchorLoader anchorLoader;  // 用於加載已保存錨點的組件
    private int playerNumUuids;

    private bool isAnchorVisible = true; // 用於記錄最後一個錨點的可見性
    private int AnchorEditMode = 0; // 用於調整錨點編輯模式
    private int MoveAxisSwitch = 0; // 用於調整編輯模式時的軸向
    private int RotateAxisSwitch = 0; // 用於調整編輯模式時的軸向

    private Vector3 moveSpeed = new Vector3(0.1f, 0.1f, 0.1f); // 錨點移動速度
    private float rotationSpeed = 30f; // 錨點旋轉速度
    private float scaleSpeed = 0.1f;  // 錨點縮放速度

    //for narration
    private GameObject LastNotHaveAnchorGameObject;

    // 在物件初始化時獲取 AnchorLoader 組件
    private void Awake() => anchorLoader = GetComponent<AnchorLoader>();

    //文字相關
    public TextMeshProUGUI RightHandTMP;
    public TextMeshProUGUI LeftHandTMP;

    //按鍵相關
    [Serializable]
    public struct ButtonClickAction
    {
        public enum ButtonClickMode
        {
            OnButtonUp,
            OnButtonDown,
            OnButton
        }

        public string Title;
        public OVRInput.Button Button;                 // 主鍵
        public OVRInput.Button ModifierButton;         // 輔助鍵（可選）
        public bool UseModifier;                       // 是否啟用組合鍵判斷
        public ButtonClickMode ButtonMode;
        public UnityEvent Callback;
    }

    [SerializeField]
    private List<ButtonClickAction> _buttonClickActions;

    public List<ButtonClickAction> ButtonClickActions
    {
        get => _buttonClickActions;
        set => _buttonClickActions = value;
    }

    void Start()
    {
        SceneEditmode = PlayerPrefs.GetInt("SceneEditmode", 0) == 1;
        
        if(SceneEditmode)
        {
            RightHandTMP.text="Edit Mode\n"+"Object"+Prefabs[currentPrefabIndex].name;
            LeftHandTMP.text="ObjectMove\n"+"Movement:Right/Left";
        }
        else
        {
            RightHandTMP.text="Game Mode\n";
            LeftHandTMP.text=" ";
        }

        LoadSavedAnchors();
        

    }
    

    

    void Update()
    {

            foreach (var buttonClickAction in ButtonClickActions)
            {
                var button = buttonClickAction.Button;
                var buttonMode = buttonClickAction.ButtonMode;
                var useModifier = buttonClickAction.UseModifier;
                var modifierButton = buttonClickAction.ModifierButton;

                bool modifierPressed = !useModifier || OVRInput.Get(modifierButton); // 沒開 modifier 功能就當作通過

                if (modifierPressed &&
                (
                    (buttonMode == ButtonClickAction.ButtonClickMode.OnButtonUp && OVRInput.GetUp(button)) ||
                    (buttonMode == ButtonClickAction.ButtonClickMode.OnButtonDown && OVRInput.GetDown(button)) ||
                    (buttonMode == ButtonClickAction.ButtonClickMode.OnButton && OVRInput.Get(button))
                ))
                {
                buttonClickAction.Callback?.Invoke();
            }
            }
            LastAnchorMove();
 
        
    }

    private void RestartGame()
    {
        if (Application.isEditor) return;

        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            const int kIntent_FLAG_ACTIVITY_CLEAR_TASK = 0x00008000;
            const int kIntent_FLAG_ACTIVITY_NEW_TASK = 0x10000000;

            var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var pm = currentActivity.Call<AndroidJavaObject>("getPackageManager");
            var intent = pm.Call<AndroidJavaObject>("getLaunchIntentForPackage", Application.identifier);

            intent.Call<AndroidJavaObject>("setFlags", kIntent_FLAG_ACTIVITY_NEW_TASK | kIntent_FLAG_ACTIVITY_CLEAR_TASK);
            currentActivity.Call("startActivity", intent);
            currentActivity.Call("finish");
            var process = new AndroidJavaClass("android.os.Process");
            int pid = process.CallStatic<int>("myPid");
            process.CallStatic("killProcess", pid);
        }

    }

    
    public void EditmodeChange()
    {
        SceneEditmode=!SceneEditmode;

        PlayerPrefs.SetInt("SceneEditmode", SceneEditmode ? 1 : 0);
        PlayerPrefs.Save(); // 保存變更

 

        Invoke("RestartGame", 1f);  

    }



    public void PrefabPrev ()
    {
        currentPrefabIndex = (currentPrefabIndex - 1 + Prefabs.Length) % Prefabs.Length;
         if(SceneEditmode)
        {
            RightHandTMP.text="Edit Mode\n"+"Object:"+Prefabs[currentPrefabIndex].name;
        }
        else
        {
            RightHandTMP.text="Game Mode";
        }
    }

    public void PrefabNext()
    {
        currentPrefabIndex = (currentPrefabIndex + 1) % Prefabs.Length;
         if(SceneEditmode)
        {
            RightHandTMP.text="Edit Mode\n"+"Object"+Prefabs[currentPrefabIndex].name;
        }
        else
        {
            RightHandTMP.text="Game Mode\n"+"Object"+Prefabs[currentPrefabIndex].name;
        }
    }

    public void CreatePrefab()
    {
        if(SceneEditmode)
        {
            isAnchorVisible = true;//重置變數狀態
            Vector3 spawnPosition = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
            Quaternion spawnRotation = Prefabs[currentPrefabIndex].transform.rotation;
            LastNotHaveAnchorGameObject = Instantiate(Prefabs[currentPrefabIndex], spawnPosition, spawnRotation);
        }
       
   
    }

    public void CreateSpatialAnchor()
    {
        if(SceneEditmode)
        {
            OVRSpatialAnchor workingAnchor = LastNotHaveAnchorGameObject.AddComponent<OVRSpatialAnchor>();
            StartCoroutine(AnchorCreated(workingAnchor));
        }
    }



    private IEnumerator AnchorCreated(OVRSpatialAnchor workingAnchor)
    {
        // 等待錨點完成創建和定位
        while (!workingAnchor.Created && !workingAnchor.Localized)
        {
            yield return new WaitForEndOfFrame();
        }

        AnchorsInf.Add(workingAnchor);
        lastCreatedAnchor = workingAnchor;
        SaveLastCreatedAnchor();
    }

    private async void SaveLastCreatedAnchor()
    {
        if(SceneEditmode)
        {
            if (lastCreatedAnchor == null)
            {
                Debug.Log("No anchor has been created to save.");
                return;
            }

            var result = await lastCreatedAnchor.SaveAnchorAsync();

            if (result.Success)
            {
                Debug.Log($"Anchor {lastCreatedAnchor.Uuid} saved successfully.");
                SaveUuidAndPrefabIndexToPlayerPrefs(lastCreatedAnchor.Uuid, currentPrefabIndex, isAnchorVisible);
            }
            else
            {
                Debug.LogError($"Failed to save anchor {lastCreatedAnchor.Uuid} with error {result.Status}");
            }
        }
    }

    void SaveUuidAndPrefabIndexToPlayerPrefs(Guid uuid, int prefabIndex, bool AnchorVisible)
    {
        if (!PlayerPrefs.HasKey(NumUuidsPlayerPref))
        {
            PlayerPrefs.SetInt(NumUuidsPlayerPref, 0);
        }

        playerNumUuids = PlayerPrefs.GetInt(NumUuidsPlayerPref);
        PlayerPrefs.SetString("uuid" + playerNumUuids, uuid.ToString());
        PlayerPrefs.SetInt("prefabIndex" + playerNumUuids, prefabIndex);
        PlayerPrefs.SetInt("visibility" + playerNumUuids, AnchorVisible ? 1 : 0);
        LastNotHaveAnchorGameObject.SetActive(isAnchorVisible);
        PlayerPrefs.SetInt(NumUuidsPlayerPref, ++playerNumUuids);
     

    }

    

    public async void UnsaveLastCreatedAnchor()
    {
        if(SceneEditmode)
        {
            if (lastCreatedAnchor == null)
            {
                Debug.LogError("No anchor to unsave.");
                return;
            }

            var result = await OVRSpatialAnchor.EraseAnchorsAsync(new List<OVRSpatialAnchor> { lastCreatedAnchor }, null);

            if (result.Success)
            {
                RemoveUuidFromPlayerPrefs(lastCreatedAnchor.Uuid);
                Debug.Log("Anchor unsaved successfully.");
            }
            else
            {
                Debug.LogError($"Failed to unsave anchor {lastCreatedAnchor.Uuid} with result {result.Status}");
            }
        }
    }

    void RemoveUuidFromPlayerPrefs(Guid uuid)
    {
        int numUuids = PlayerPrefs.GetInt(NumUuidsPlayerPref, 0);

        for (int i = 0; i < numUuids; i++)
        {
            string savedUuid = PlayerPrefs.GetString("uuid" + i, "");
            if (savedUuid == uuid.ToString())
            {
                PlayerPrefs.DeleteKey("uuid" + i);
                PlayerPrefs.DeleteKey("prefabIndex" + i);
                PlayerPrefs.DeleteKey("visibility" + i);
                break;
            }
        }
    }

    public void UnsaveAllAnchors()
    {
        if(SceneEditmode)
        {
            foreach (var anchor in AnchorsInf)
            {
                UnsaveAnchor(anchor);
            }

            AnchorsInf.Clear();
            ClearAllUuidsFromPlayerPrefs();
        }
    }

    async void UnsaveAnchor(OVRSpatialAnchor anchor)
    {
        if (anchor == null)
        {
            Debug.LogError("Anchor is null, cannot unsave.");
            return;
        }

        var result = await OVRSpatialAnchor.EraseAnchorsAsync(new List<OVRSpatialAnchor> { anchor }, null);

        if (result.Success)
        {
            Debug.Log("Anchor unsaved successfully.");
        }
        else
        {
            Debug.LogError($"Failed to unsave anchor {anchor.Uuid} with result {result.Status}");
        }
    }

    private void ClearAllUuidsFromPlayerPrefs()
    {
        if (PlayerPrefs.HasKey(NumUuidsPlayerPref))
        {
            int playerNumUuids = PlayerPrefs.GetInt(NumUuidsPlayerPref);
            for (int i = 0; i < playerNumUuids; i++)
            {
                PlayerPrefs.DeleteKey("uuid" + i);
                PlayerPrefs.DeleteKey("prefabIndex" + i);
                PlayerPrefs.DeleteKey("visibility" + i);
            }
            PlayerPrefs.DeleteKey(NumUuidsPlayerPref);
            PlayerPrefs.Save();
        }
    }

    public void LoadSavedAnchors()
    {
        anchorLoader.LoadAnchorsByUuid();
    }

    public void ToggleLastAnchorVisibility()
    {
        if (LastNotHaveAnchorGameObject == null)
        {
            Debug.LogError("No anchor to toggle visibility.");
            return;
        }

        isAnchorVisible = !isAnchorVisible;
        Debug.Log("Toggled last anchor visibility to: " + isAnchorVisible);
    }

    void LeftTextControl()
    {

        if(AnchorEditMode==0)
        {
            string ObjectAxis="";
            if(MoveAxisSwitch==0)
            {
                ObjectAxis="Right/Left";
                LeftHandTMP.text="ObjectMove\n"+"Movement:"+ObjectAxis;
            }
            else if(MoveAxisSwitch==1)
            {
                ObjectAxis="UP/DOWN";
                LeftHandTMP.text="ObjectMove\n"+"Movement:"+ObjectAxis;
            }
            else if(MoveAxisSwitch==2)
            {
                ObjectAxis="Forward/BackWard";
                LeftHandTMP.text="ObjectMove\n"+"Movement:"+ObjectAxis;
            }

            LeftHandTMP.text="ObjectMove\n"+"Movement:"+ObjectAxis;
        }
        else if(AnchorEditMode==1)
        {
            string ObjectAxis="";
            if(RotateAxisSwitch==0)
            {
                ObjectAxis="X";
                LeftHandTMP.text="ObjectRotate\n"+"RotationAxis:"+ObjectAxis;
            }
            else if(RotateAxisSwitch==1)
            {
                ObjectAxis="Y";
                LeftHandTMP.text="ObjectRotate\n"+"RotationAxis:"+ObjectAxis;
            }
            else if(RotateAxisSwitch==2)
            {
                ObjectAxis="Z";
                LeftHandTMP.text="ObjectRotate\n"+"RotationAxis:"+ObjectAxis;
            }
            LeftHandTMP.text="ObjectRotate\n"+"RotationAxis:"+ObjectAxis;
        }
    }


    public void AnchorEditModeChange()
    {
        AnchorEditMode += 1;

        if (AnchorEditMode>1)
        {
            AnchorEditMode = 0;
        }

        LeftTextControl();

    }
    public void EditModeAxisChage()
    {
        switch (AnchorEditMode)
        {
            case 0:
                MoveAxisSwitch += 1;
                if (MoveAxisSwitch > 2)
                {
                    MoveAxisSwitch = 0;
                }
                break;

            case 1:
                RotateAxisSwitch += 1;
                if (RotateAxisSwitch > 2)
                {
                    RotateAxisSwitch = 0;
                }
                break;
            default:

                break;
        }
        LeftTextControl();

    }


    void LastAnchorMove()
    {
        if (LastNotHaveAnchorGameObject!=null)
        {
            Vector2 thumbstickInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

            switch (AnchorEditMode)
            {
                case 0: // **平移模式**
                    if (thumbstickInput != Vector2.zero)
                    {
                        Vector3 moveDelta = new Vector3(0,0,0);
                        switch (MoveAxisSwitch)
                        {
                            case 0:
                                // 計算移動的增量(X軸)
                                moveDelta = new Vector3(thumbstickInput.y, 0, 0 ) * moveSpeed.x * Time.deltaTime;
                                break;
                            case 1:
                                // 計算移動的增量(Y軸)
                                moveDelta = new Vector3(0, thumbstickInput.y, 0) * moveSpeed.y * Time.deltaTime;
                                break;
                            case 2:
                                // 計算移動的增量(Z軸)
                                moveDelta = new Vector3(0, 0, thumbstickInput.y) * moveSpeed.z * Time.deltaTime;
                                break;
                            default:
                         
                                break;
                        }
                        // 更新物件的位置
                        LastNotHaveAnchorGameObject.transform.localPosition += moveDelta;
                        Debug.Log($"Anchor New Position: {LastNotHaveAnchorGameObject.transform.localPosition}");
                    }
                    break;

                case 1: // **旋轉模式**
                    float rotationInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y; // 搖桿上下軸控制旋轉

                    if (Mathf.Abs(rotationInput) > 0.01f) // 避免極小數值抖動
                    {
                        Vector3 rotationAxis = Vector3.zero;

                        // 根據 RotateAxisSwitch 決定旋轉的軸
                        switch (RotateAxisSwitch)
                        {
                            case 0: // X 軸旋轉
                                rotationAxis = Vector3.right;
                                break;
                            case 1: // Y 軸旋轉
                                rotationAxis = Vector3.up;
                                break;
                            case 2: // Z 軸旋轉
                                rotationAxis = Vector3.forward;
                                break;
                            default:

                                break;
                        }

                        // 計算旋轉角度（右正左負）
                        float rotationAmount = rotationInput * rotationSpeed * Time.deltaTime;

                        // 對物件進行旋轉
                        LastNotHaveAnchorGameObject.transform.Rotate(rotationAxis, rotationAmount, Space.Self);

                        Debug.Log($"Anchor New Rotation: {LastNotHaveAnchorGameObject.transform.rotation.eulerAngles}");
                    }
                    break;

                // case 2: // **縮放模式**
                //     float scaleInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y;
                //     if (Mathf.Abs(scaleInput) > 0.01f)
                //     {
                //         // 更新物件縮放
                //         Vector3 newScale = LastNotHaveAnchorGameObject.gameObject.transform.localScale +
                //                            Vector3.one * scaleSpeed * scaleInput * Time.deltaTime;

                //         LastNotHaveAnchorGameObject.gameObject.transform.localScale = newScale;

                //         Debug.Log($"Anchor New Scale: {LastNotHaveAnchorGameObject.gameObject.transform.localScale}");
                //     }
                //     break;

                default:
                    Debug.Log("Invalid Anchor Edit Mode");
                    break;
            }
        }
    }
           
    
}
