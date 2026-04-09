using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TaskRewardPrefab : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Sprite coinIcon;
    [SerializeField] private Sprite expIcon;
    
    public void Init(RewardType rewardType, int amount,int itemId=0)
    {
        if (icon != null) icon.gameObject.SetActive(true);
        if (amountText != null) amountText.gameObject.SetActive(true);
        switch (rewardType)
        {
            case RewardType.Item:
            case RewardType.Equipment:
                // 道具显示其图标，不显示数量文本（或可按需显示）
                if (icon != null)
                {
                    // 默认图标（兜底）
                    if (coinIcon != null) icon.sprite = coinIcon;
                    if (GameManager.Instance != null && GameManager.Instance.ItemDataSo != null)
                    {
                        var data = GameManager.Instance.ItemDataSo.GetItemDataById(itemId);
                        if (data != null && data.itemSprite != null)
                            icon.sprite = data.itemSprite;
                    }
                }
                amountText.text = amount.ToString();
                break;
            case RewardType.Money:
                if (icon != null && coinIcon != null) icon.sprite = coinIcon;
                if (amountText != null)
                {
                    amountText.gameObject.SetActive(true);
                    amountText.text = amount.ToString();
                }
                break;
            case RewardType.Exp:
                if (icon != null && expIcon != null) icon.sprite = expIcon;
                if (amountText != null)
                {
                    amountText.gameObject.SetActive(true);
                    amountText.text = amount.ToString();
                }
                break;
            default:
                if (amountText != null) amountText.gameObject.SetActive(false);
                throw new ArgumentOutOfRangeException(nameof(rewardType), rewardType, null);
        }
    }
}
