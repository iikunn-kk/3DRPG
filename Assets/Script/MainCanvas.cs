using System;
using Game.UI.SkillUpgrade;
using UnityEngine;
using System.Linq; // 添加以便使用 ToList
using Cysharp.Threading.Tasks;

public class MainCanvas : MonoBehaviour
{
    [SerializeField] private Transform makePanel;
    public Transform MakePanelTransform => makePanel;

    [Header("Target Details")]
    [Tooltip("Assign the TargetDetailsPanel here (or will be auto-found among children). MainCanvas will control its activation so it can be hooked in the Inspector without the panel needing to be active at start.")]
    [SerializeField] private TargetDetailsPanel targetDetailsPanel;
    public ToastManager toastManager;
    [SerializeField] private SkillToastManager skillToastManager;
    [SerializeField] private Transform taskArrowUI;
    public TaskTrackerPanel _taskTrackerPanel; // 新增字段声明

    private void Awake()
    {
        if (_taskTrackerPanel != null)
        {
            _taskTrackerPanel.Init();
        }
    }

    private void OnEnable()
    {
        UIManager.Instance.SetMainCanvas(this);
    }

    public void ShowTargetDetails(MonsterBase target)
    {
        if (targetDetailsPanel == null) return;
        if (!targetDetailsPanel.gameObject.activeSelf)
            targetDetailsPanel.gameObject.SetActive(true);
        targetDetailsPanel.Init(target);
    }

    public void HideTargetDetails()
    {
        if (targetDetailsPanel == null) return;
        targetDetailsPanel.Init(null);
    }

    public void ShowPlayerStatePanel()
    {
        var playerStatePanel = UIManager.Instance.OpenPanel<PlayerStatePanel>(out var isOpen);
        if (isOpen)
        {
            playerStatePanel.Init(CharacterService.Instance.CurrentPlayerCharacter());
        }
    }
    public void ShowInventoryPanel()
    {
        // Open inventory panel; no further initialization required here. Discard the out bool to avoid unused variable warnings.
        UIManager.Instance.OpenPanel<InventoryPanel>(out _);
    }

    public void ShowTaskPanel()
    {
        var taskPanel = UIManager.Instance.OpenPanel<TaskPanel>(out var isOpen);
        if (isOpen)
        {
            var list = TaskManager.Instance != null ? TaskManager.Instance.tasks.Values.ToList() : new System.Collections.Generic.List<BaseTask>();
            taskPanel.Init(list);
        }
    }

    public void ShowWorldShopPanel()
    {
        var worldShopPanel = UIManager.Instance.OpenPanel<WorldShopPanel>(out _);
        if (worldShopPanel != null)
        {
            worldShopPanel.Show();
        }
    }
    public void ShowGuildPanel()
    {
        var guildPanel = UIManager.Instance.OpenPanel<GuildPanel>(out var isOpen);
        if (isOpen)
        {
            guildPanel.Init(SessionManager.Instance.CurrentCharacter).Forget();
        }
    }

    public void ShowSettingPanel()
    {
        // Open settings panel; no immediate init required here.
        UIManager.Instance.OpenPanel<SettingPanel>(out _);
    }

    public void ShowSkillUpgradePanel()
    {
        var skillDetailsPanel = UIManager.Instance.OpenPanel<SkillUpgradePanel>(out var isOpen);
        if (isOpen)
        {
            skillDetailsPanel.Init();
        }
    }

    // Skill Toast 相关方法
    public void ShowSkillToast(string message, float duration = -1f)
    {
        if (skillToastManager == null)
        {
            skillToastManager = GetComponentInChildren<SkillToastManager>(true);
        }

        if (skillToastManager != null)
        {
            skillToastManager.Show(message, duration);
        }
        else
        {
            Debug.LogWarning("SkillToastManager not found under MainCanvas. Please place one to configure position and style.");
        }
    }

    public void HideSkillToast()
    {
        if (skillToastManager == null)
        {
            skillToastManager = GetComponentInChildren<SkillToastManager>(true);
        }

        if (skillToastManager != null)
        {
            skillToastManager.Hide();
        }
    }

    public void OnMapPanelShow()
    {
        taskArrowUI.gameObject.SetActive(false);
    }
    public void OnMapPanelHide()
    {
        taskArrowUI.gameObject.SetActive(true);
    }

    public void ShowDrawCardPanel()
    {
        var drawCardPanel = UIManager.Instance.OpenPanel<DrawCardPanel>(out var isOpen);
        if (isOpen)
        {

        }
    }

}