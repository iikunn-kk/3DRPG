using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateCharacterPropertyBar : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private TMP_Text propertyBarText;
    [SerializeField] private List<GameObject> propertyBarImage;

    /// <summary>
    /// 初始化属性条
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    /// <param name="value">属性值</param>
    public void Init(string propertyName, int value)
    {
        // 确保值在有效范围内
        int value1 = Mathf.Clamp(value, 0, 5);
        // 隐藏所有图像
        foreach (var image in propertyBarImage)
        {
            image.GetComponentInChildren<Image>().enabled=false;
        }
        
        // 设置属性名称和激活对应数量的图像
        if (propertyBarText != null)
            propertyBarText.text = propertyName;
            
        for (int i = 0; i < value1 && i < propertyBarImage.Count; i++)
        {
            propertyBarImage[i].GetComponentInChildren<Image>().enabled=true;
        }
    }
}