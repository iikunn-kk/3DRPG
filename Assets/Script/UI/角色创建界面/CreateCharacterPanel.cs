using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateCharacterPanel : MonoBehaviour
{
    #region 字段

    #region 数据源
    [Header("数据源")]
    [SerializeField] private CreateCharacterDataSO characterDataSO;
    [Header("任务数据（用于新角色初始主线任务）")][SerializeField] private TaskDataSO taskDataSo;
    [Header("全部技能数据（用于新角色初始全技能解锁）")][SerializeField] private AllSkillsSO allSkillsData;
    #endregion

    #region UI组件
    [Header("UI组件")]
    [SerializeField] private CreateCharacterPropertyBar[] propertyBars; // 预先放置的属性条
    [SerializeField] private Transform professionButtonContainer;
    [SerializeField] private GameObject professionButtonPrefab;
    [SerializeField] private TMP_Text professionNameText;
    #endregion

    #region 其他引用
    [Header("负责拍摄3D角色的摄影棚")]
    [SerializeField] private UI3DStudioArray ui3DStudio;
    [Header("角色命名面板")]
    [SerializeField] private GameObject inputNamePanel;
    [Header("登录管理器")]
    [SerializeField] private PlayerLogInManager playerLogInManager;
    #endregion

    #region 拖拽旋转相关
    [Header("拖拽旋转相关")]
    [SerializeField] private RawImage modelDisplayImage; // 显示模型的RawImage
    private bool isDragging = false;
    private Vector2 lastMousePosition;
    private const float RotationSensitivity = 2f;
    #endregion

    #region 私有变量
    private List<ChooseProfessionMod> professionButtons = new List<ChooseProfessionMod>();
    private CharacterProfession? selectedProfession = null;

    #endregion

    #endregion

    #region 生命周期方法

    void Start()
    {
        CreateProfessionButtons();
        if (characterDataSO != null && characterDataSO.characterData != null && characterDataSO.characterData.Count > 0)
        {
            // 默认选择第一个职业
            selectedProfession = characterDataSO.characterData[0].profession;
            ShowCharacterData(selectedProfession.Value);
        }

        // 添加鼠标事件监听器到模型显示图片上
        if (modelDisplayImage != null)
        {
            var dragListener = modelDisplayImage.gameObject.AddComponent<DragRotationListener>();
            dragListener.Initialize(this);
        }
    }
    #endregion

    #region 职业按钮相关方法

    /// <summary>
    /// 创建职业选择按钮
    /// </summary>
    private void CreateProfessionButtons()
    {
        if (characterDataSO == null || characterDataSO.characterData == null ||
            professionButtonContainer == null || professionButtonPrefab == null)
            return;

        ClearProfessionButtons();

        for (int i = 0; i < characterDataSO.characterData.Count; i++)
        {
            CreateCharacterData data = characterDataSO.characterData[i];
            ChooseProfessionMod button = Instantiate(professionButtonPrefab, professionButtonContainer).GetComponent<ChooseProfessionMod>();
            if (button != null)
            {
                button.Init(data, OnProfessionSelected);
                professionButtons.Add(button);

                // 如果这是当前选中的职业，设置为选中状态
                if (selectedProfession.HasValue && selectedProfession.Value == data.profession)
                {
                    button.SetSelected(true);
                }
            }
            {
                button.SetSelected(true);
            }
        }
    }

    /// <summary>
    /// 当选择一个职业时调用
    /// </summary>
    /// <param name="profession">职业枚举</param>
    private void OnProfessionSelected(CharacterProfession profession)
    {
        // 更新选中状态
        selectedProfession = profession;

        // 更新所有按钮的选中状态
        UpdateProfessionButtonSelection();

        // 显示选中职业的数据
        ShowCharacterData(profession);
    }

    /// <summary>
    /// 更新职业按钮的选中状态
    /// </summary>
    private void UpdateProfessionButtonSelection()
    {
        if (!selectedProfession.HasValue)
            return;

        foreach (var button in professionButtons)
        {
            if (button != null)
            {
                button.SetSelected(button.GetProfession() == selectedProfession.Value);
            }
        }
    }

    #endregion

    #region 数据展示方法

    /// <summary>
    /// 显示指定职业的数据
    /// </summary>
    /// <param name="profession">职业枚举</param>
    public void ShowCharacterData(CharacterProfession profession)
    {
        if (characterDataSO == null || characterDataSO.characterData == null)
            return;

        // 查找对应的职业数据
        CreateCharacterData data = null;
        foreach (var characterData in characterDataSO.characterData)
        {
            if (characterData.profession == profession)
            {
                data = characterData;
                break;
            }
        }

        if (data == null)
            return;

        // 更新选中职业
        selectedProfession = data.profession;

        // 更新按钮选中状态
        UpdateProfessionButtonSelection();

        // 更新各项属性
        UpdatePropertyBars(data);

        // 更新3D模型显示
        ui3DStudio.ChangeGameObject(profession);
    }

    /// <summary>
    /// 更新属性条显示
    /// </summary>
    /// <param name="data">角色数据</param>
    private void UpdatePropertyBars(CreateCharacterData data)
    {
        if (propertyBars == null || propertyBars.Length < 4)
            return;

        // 更新各个属性条的显示
        if (propertyBars[0] != null) propertyBars[0].Init("操作难度", data.difficultyOfOperation);
        if (propertyBars[1] != null) propertyBars[1].Init("物理攻击力", data.physicalAttackValue);
        if (propertyBars[2] != null) propertyBars[2].Init("魔法攻击力", data.magicAttackValue);
        if (propertyBars[3] != null) propertyBars[3].Init("防御力", data.defenseValue);
        professionNameText.text = data.description;
    }

    #endregion

    #region 3D模型控制方法

    /// <summary>
    /// 切换到待机动作
    /// </summary>
    public void SwitchToIdleAction()
    {
        if (ui3DStudio != null)
        {
            ui3DStudio.SetAnimation(CharacterActionEnum.Idle);
        }
    }

    /// <summary>
    /// 切换到跑步动作
    /// </summary>
    public void SwitchToAngryAction()
    {
        if (ui3DStudio != null)
        {
            ui3DStudio.SetAnimation(CharacterActionEnum.Shy);
        }
    }

    /// <summary>
    /// 切换到打招呼动作
    /// </summary>
    public void SwitchToHelloAction()
    {
        if (ui3DStudio != null)
        {
            ui3DStudio.SetAnimation(CharacterActionEnum.Dance);
        }
    }

    #endregion

    #region 拖拽旋转方法

    /// <summary>
    /// 开始拖拽
    /// </summary>
    /// <param name="position">鼠标位置</param>
    public void BeginDrag(Vector2 position)
    {
        isDragging = true;
        lastMousePosition = position;
    }

    /// <summary>
    /// 拖拽中
    /// </summary>
    /// <param name="position">鼠标位置</param>
    public void OnDrag(Vector2 position)
    {
        if (isDragging && selectedProfession.HasValue)
        {
            float deltaX = position.x - lastMousePosition.x;
            // 反转旋转方向，解决拖动方向相反的问题
            if (ui3DStudio != null)
            {
                ui3DStudio.AddRotation(-deltaX * RotationSensitivity);
            }
            lastMousePosition = position;
        }
    }

    /// <summary>
    /// 结束拖拽
    /// </summary>
    /// <param name="position">鼠标位置</param>
    public void EndDrag(Vector2 position)
    {
        isDragging = false;
    }

    #endregion

    #region 资源清理方法

    private void ClearProfessionButtons()
    {
        foreach (var button in professionButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }
        professionButtons.Clear();
    }

    private void OnDestroy()
    {
        ClearProfessionButtons();
    }

    #endregion

    #region 公共访问方法

    /// <summary>
    /// 获取当前选中的职业
    /// </summary>
    /// <returns>当前选中的职业枚举</returns>
    public CharacterProfession? GetSelectedProfession()
    {
        return selectedProfession;
    }

    /// <summary>
    /// 当玩家点击创建角色按钮时调用
    /// </summary>
    public void OnCreateCharacterButtonClick()
    {
        // 检查是否选择了职业
        if (!selectedProfession.HasValue)
        {
            Debug.LogWarning("请先选择一个职业");
            return;
        }

        // 显示角色命名面板
        if (inputNamePanel != null)
        {
            var obj = Instantiate(inputNamePanel, transform);
            var mod = obj.GetComponent<InputCharacterNamePanel>();
            mod.Init(OnCharacterNameConfirmed);
        }
    }

    /// <summary>
    /// 当玩家确认角色名时调用
    /// </summary>
    /// <param name="characterName">角色名</param>
    private async void OnCharacterNameConfirmed(string characterName)
    {
        // 创建新角色
        bool success = await CreateNewCharacter(characterName);

        if (success)
        {
            playerLogInManager.ShowCharacterSelectPanel();
        }
    }

    /// <summary>
    /// 创建新角色
    /// </summary>
    /// <param name="characterName">角色名</param>
    private async Task<bool> CreateNewCharacter(string characterName)
    {
        // 获取当前登录的玩家数据
        PlayerLoginData playerLoginData = null;
        int serverId = 0;

        if (playerLogInManager != null)
        {
            playerLoginData = playerLogInManager.GetCurrentPlayerData();
            serverId = playerLogInManager.GetCurrentServerId();
        }
        else
        {
            Debug.LogError("找不到PlayerLogInManager组件");
            return false;
        }

        // 检查角色名是否已存在
        bool nameExists = await MongoDBManager.Instance.IsCharacterNameExistsOnServer(characterName, serverId);
        if (nameExists)
        {
            Debug.LogWarning($"角色名 {characterName} 在服务器 {serverId} 上已存在");
            // 这里应该显示一个错误提示给用户
            return false;
        }

        // 创建新角色
        CharacterData newCharacter = new CharacterData(System.Guid.NewGuid().ToString(), 101, "fuck", CharacterProfession.嘉然);
        newCharacter.Id = System.Guid.NewGuid().ToString(); // 使用GUID作为角色ID
        newCharacter.characterName = characterName;
        newCharacter.profession = selectedProfession.Value;
        newCharacter.level = 1;
        newCharacter.currentScene = "Village"; // 默认起始场景,七月进行修改的初始场景
        // newCharacter.currentScene = "Level_1"; // 默认起始场景，陈子的初始默认场景
        newCharacter.position = Vector3.zero;
        newCharacter.serverId = serverId; // 设置角色所属的服务器ID
        newCharacter.playerUid = playerLoginData.uid; // 关联玩家UID
        // 初始化：首个主线任务
        try
        {
            if (taskDataSo != null && taskDataSo.mainMission != null && taskDataSo.mainMission.Count > 0)
            {
                TaskData first = null;
                foreach (var td in taskDataSo.mainMission)
                {
                    if (td != null && (td.prerequisiteTaskId == -1)) { first = td; break; }
                }
                if (first == null) first = taskDataSo.mainMission[0];
                // 仅使用 taskList 保存初始任务
                if (newCharacter.taskList == null) newCharacter.taskList = new System.Collections.Generic.List<TaskLiteData>();
                if (!newCharacter.taskList.Exists(t => t.taskId == first.taskId))
                    newCharacter.taskList.Add(new TaskLiteData(first.taskId, 0));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"初始化首个主线任务失败: {ex.Message}");
        }
        // 初始化：全部技能解锁为1级
        try
        {
            if (allSkillsData != null && allSkillsData.allSkills != null)
            {
                if (newCharacter.skills == null) newCharacter.skills = new List<SkillSaveData>();
                newCharacter.skills.Clear();
                foreach (var so in allSkillsData.allSkills)
                {
                    if (so == null) continue;
                    if (!newCharacter.skills.Exists(s => s.SkillID == so.SkillID))
                    {
                        newCharacter.skills.Add(new SkillSaveData(so.SkillID, 1));
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"初始化技能失败: {ex.Message}");
        }
        // 保存角色数据到MongoDB
        bool success = await MongoDBManager.Instance.CreateAndSaveCharacterData(newCharacter);

        if (success)
        {
            Debug.Log($"角色 {characterName} 创建成功");
        }
        else
        {
            Debug.LogError($"角色 {characterName} 创建失败");
        }

        return success;
    }

    #endregion
}