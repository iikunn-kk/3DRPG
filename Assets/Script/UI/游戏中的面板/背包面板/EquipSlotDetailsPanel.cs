using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipSlotDetailsPanel : SlotDetailsPanel
{
    [Header("装备额外详情区域")] 
    [SerializeField] private TMP_Text extraAttributesTitle; // 例如 "装备属性" 标题
    [SerializeField] private List<TMP_Text> extraAttributeTexts;  // 更详细的属性列表，和基类 attributesText 区分
    [SerializeField] private Image bkImage;
    
    /// <summary>
    /// 覆盖：显示装备或普通物品详情。装备时额外显示更清晰的属性块。
    /// </summary>
    public override void ShowDetails(InventoryItem item)
    {
        base.ShowDetails(item); // 先调用基类填充通用信息以及基础 attributesText

        if (item == null)
        {
            HideExtra();
            return;
        }
        var template = GameDataConfig.Instance.ItemDataSo.GetItemDataById(item.itemId);
        if (template is not EquipmentData)
        {
            // 非装备隐藏额外区域
            HideExtra();
            return;
        }

        // 是装备：显示额外区域
        if (extraAttributesTitle != null)
        {
            extraAttributesTitle.gameObject.SetActive(true);
            // 禁用自动换行以保证只显示一行：使用 overflowMode + maxVisibleLines 更稳健
            extraAttributesTitle.overflowMode = TextOverflowModes.Truncate;
            extraAttributesTitle.maxVisibleLines = 1;

            // 尝试从模板中读取基础属性并生成单行摘要
            if (template is EquipmentData eqTemplate && eqTemplate.baseProperties != null && eqTemplate.baseProperties.Count > 0)
            {
                string FormatProp(EquipmentProperty p)
                {
                    // 优先显示已有的 actualValue；否则显示 min~max 范围
                    if (p.actualValue != 0f)
                    {
                        if (p.IsPercentage)
                            return $"+{p.actualValue:F1}% {p.propertyType}";
                        else
                            return $"+{Mathf.RoundToInt(p.actualValue)} {p.propertyType}";
                    }
                    else
                    {
                        if (p.IsPercentage)
                            return $"+{p.minValue:F1}%~{p.maxValue:F1}% {p.propertyType}";
                        else
                            return $"+{Mathf.RoundToInt(p.minValue)}~{Mathf.RoundToInt(p.maxValue)} {p.propertyType}";
                    }
                }

                var parts = eqTemplate.baseProperties
                    .Where(p => p != null) // 防御空引用
                    .Select(FormatProp)
                    .ToList();

                // 用竖线分隔各个属性，确保没有换行
                var oneLine = string.Join(" | ", parts).Replace('\n', ' ').Replace('\r', ' ');
                extraAttributesTitle.text = "基础属性: " + oneLine;
            }
            else
            {
                extraAttributesTitle.text = "装备属性";
            }
        }

        // 优先实例随机属性
        List<EquipmentProperty> propertiesToShow = new List<EquipmentProperty>();
        if (item.generatedProperties != null && item.generatedProperties.Count > 0)
        {
            propertiesToShow = item.generatedProperties.Where(p => p.actualValue != 0).ToList();
        }
        else if (template is EquipmentData eq)
        {
            propertiesToShow = eq.GetAllProperties().Where(p => p.actualValue != 0).ToList();
        }

        // 显示属性
        for (int i = 0; i < extraAttributeTexts.Count; i++)
        {
            if (i < propertiesToShow.Count)
            {
                extraAttributeTexts[i].gameObject.SetActive(true);
                extraAttributeTexts[i].text = propertiesToShow[i].GetDisplayText();
            }
            else
            {
                extraAttributeTexts[i].gameObject.SetActive(false);
            }
        }

        // 根据物品品质设置背景颜色
        if (bkImage != null)
        {
            bkImage.color = ItemQualityUtility.GetQualityColor(item.quantity);
        }
    }

    private void HideExtra()
    {
        if (extraAttributesTitle != null) extraAttributesTitle.gameObject.SetActive(false);
        
        // 隐藏所有属性文本
        if (extraAttributeTexts != null)
        {
            foreach (var text in extraAttributeTexts)
            {
                text.gameObject.SetActive(false);
            }
        }
    }
}