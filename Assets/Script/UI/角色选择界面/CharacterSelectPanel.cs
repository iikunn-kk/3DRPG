using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectPanel : MonoBehaviour
{
    [SerializeField] private UI3DStudioArray ui3DStudioArray;
    [SerializeField] private GameObject serverSelectionObj;
    [SerializeField] private Transform serverSelectionParent;
    [SerializeField] private PlayerLogInManager playerLogInManager;
    [Header("弹窗父物体(可选，不填则挂在本面板下)")]
    [SerializeField] private Transform popupParent;
    [Header("删除角色确认面板预制体")]
    [SerializeField] private GameObject confirmDeletePanelPrefab;
    [Header("面板上的删除按钮（由面板统一管理）")]
    [SerializeField] private Button panelDeleteButton;

    private List<GameObject> characterObjects = new List<GameObject>();
    private bool _isEnteringGame = false;

    // 选中/双击逻辑
    private CharacterSelectMod _selectedMod;
    private float _lastClickTime = -1f;
    private const float DoubleClickThreshold = 0.35f; // 双击阈值 (秒)

    // 记录最近一次 Init 的 uid/serverId，便于刷新
    private string _lastUid;
    private int _lastServerId;

    public void Init(string uid,int serverId)
    {
        _lastUid = uid;
        _lastServerId = serverId;
        _isEnteringGame = false;
        _selectedMod = null;
        _lastClickTime = -1f;

        // 禁用面板删除按钮（没有选中项）
        if (panelDeleteButton != null)
        {
            panelDeleteButton.interactable = false;
        }

        // 清除现有的角色显示对象
        foreach (var obj in characterObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        characterObjects.Clear();

        // 使用协程异步加载角色数据
        StartCoroutine(LoadCharactersAsync(uid, serverId));
    }
    private IEnumerator LoadCharactersAsync(string uid, int serverId)
    {
        var getCharactersTask = MongoDBManager.Instance.GetCharactersByPlayerUIDAndServer(uid, serverId);
        yield return new WaitUntil(() => getCharactersTask.IsCompleted);
        
        if (getCharactersTask.Exception != null)
        {
            Debug.LogError($"加载角色数据时发生错误: {getCharactersTask.Exception.Message}");
            yield break;
        }
        
        var allCharacterData = getCharactersTask.Result;
        foreach (var data in allCharacterData)
        {
            var obj = Instantiate(serverSelectionObj, serverSelectionParent);
            var mod = obj.GetComponent<CharacterSelectMod>();
            if (mod != null && ui3DStudioArray != null)
            {
                mod.Initialized(data, ui3DStudioArray.GetRenderTexture(data.profession), OnModClicked, OnRequestDeleteCharacter);
            }
            characterObjects.Add(obj);
        }
    }

    // 单击/双击选择
    private void OnModClicked(CharacterSelectMod mod, CharacterData data)
    {
        if (mod == null || data == null) return;
        float now = Time.unscaledTime;
        bool isDoubleClick = (_selectedMod == mod) && (_lastClickTime > 0f) && ((now - _lastClickTime) <= DoubleClickThreshold);

        // 先处理选中高亮
        if (_selectedMod != null && _selectedMod != mod)
        {
            _selectedMod.SetSelected(false);
        }
        _selectedMod = mod;
        _selectedMod.SetSelected(true);

        // 启用面板删除按钮（现在有选中项）
        if (panelDeleteButton != null)
        {
            panelDeleteButton.interactable = true;
        }

        if (isDoubleClick)
        {
            LoadGameScene(data);
            _lastClickTime = -1f; // 重置
        }
        else
        {
            _lastClickTime = now;
        }
    }

    // 面板上的删除按钮点击（用户流程：先选中 -> 点击此按钮 -> 弹窗确认 -> 删除并刷新）
    public void OnDeleteButtonClicked()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        if (_selectedMod == null)
        {
            Debug.LogWarning("未选中任何角色，无法删除");
            return;
        }

        var data = _selectedMod.GetData();
        if (data == null)
        {
            Debug.LogWarning("选中项数据为空，无法删除");
            return;
        }

        if (confirmDeletePanelPrefab == null)
        {
            Debug.LogError("未配置 删除角色确认面板 预制体，无法弹出删除确认窗口。");
            return;
        }

        var parent = popupParent != null ? popupParent : transform;
        var popupObj = Instantiate(confirmDeletePanelPrefab, parent);
        var popup = popupObj.GetComponent<ConfirmDeleteCharacterPanel>();
        if (popup == null)
        {
            Debug.LogError("确认面板预制体上缺少 ConfirmDeleteCharacterPanel 组件。");
            return;
        }

        // 确认后的回调：删除当前选中角色（注意：使用当前数据快照）
        popup.Init(data, async () =>
        {
            await DoDeleteCharacterAsync(data);

            // 删除后取消选中并禁用面板删除按钮
            if (_selectedMod != null)
            {
                _selectedMod.SetSelected(false);
                _selectedMod = null;
            }
            if (panelDeleteButton != null)
            {
                panelDeleteButton.interactable = false;
            }
        });
    }

    // 当玩家选择创建新角色时调用
    public void OnCreateCharacterButtonClicked()
    {
        if (playerLogInManager != null)
        {
            playerLogInManager.ShowCreateCharacterPanel();
        }
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }
    // 返回服务器选择面板
    public void ReturnToServerSelection()
    {
        if (playerLogInManager != null)
        {
            playerLogInManager.OnLoginSuccess(); // 内部会刷新并显示 ServerSelectionPanel
        }
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }
    private void LoadGameScene(CharacterData data)
    {
        if (_isEnteringGame)
        {
            return;
        } 
        _isEnteringGame = true; 
        playerLogInManager.GotoGameScene(data);
    }

    private void OnRequestDeleteCharacter(CharacterData data, CharacterSelectMod mod)
    {
        // 保持原有回调兼容性（如果某处仍调用 mod 的删除按钮）
        if (confirmDeletePanelPrefab == null)
        {
            Debug.LogError("未配置 删除角色确认面板 预制体，无法弹出删除确认窗口。");
            return;
        }
        var parent = popupParent != null ? popupParent : transform;
        var popupObj = Instantiate(confirmDeletePanelPrefab, parent);
        var popup = popupObj.GetComponent<ConfirmDeleteCharacterPanel>();
        if (popup == null)
        {
            Debug.LogError("确认面板预制体上缺少 ConfirmDeleteCharacterPanel 组件。");
            return;
        }
        popup.Init(data, async () =>
        {
            await DoDeleteCharacterAsync(data);

            // 如果删除的是当前选中项，清理选中引用
            if (_selectedMod == mod)
            {
                if (_selectedMod != null) _selectedMod.SetSelected(false);
                _selectedMod = null;
                if (panelDeleteButton != null) panelDeleteButton.interactable = false;
            }
        });
    }

    private async Task DoDeleteCharacterAsync(CharacterData data)
    {
        if (data == null)
        {
            return;
        }
        try
        {
            bool ok = await MongoDBManager.Instance.DeleteCharacterData(data.Id);
            if (ok)
            {
                AudioManager.Instance.PlayUISound(UISoundType.确认);
                // 删除成功后刷新面板（重拉取角色列表）
                Init(_lastUid, _lastServerId);
            }
            else
            {
                Debug.LogWarning("删除角色失败或未找到角色。");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"删除角色时发生异常: {e.Message}");
        }
    }

}