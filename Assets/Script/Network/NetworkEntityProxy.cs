using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 远程实体通用代理 — HP 条、名字显示。挂载到远程玩家/怪物 Prefab 上。
/// </summary>
public class NetworkEntityProxy : MonoBehaviour
{
    [SerializeField] private Text _nameText;
    [SerializeField] private Slider _hpBar;

    public void SetName(string name)
    {
        if (_nameText) _nameText.text = name;
    }

    public void SetHp(int hp, int maxHp)
    {
        if (_hpBar)
        {
            _hpBar.maxValue = maxHp;
            _hpBar.value = hp;
        }
    }
}
