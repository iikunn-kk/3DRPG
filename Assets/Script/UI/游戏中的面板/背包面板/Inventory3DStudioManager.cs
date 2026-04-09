using UnityEngine;

/// <summary>
/// 负责管理背包面板中的3D展示摄影棚
/// </summary>
public class Inventory3DStudioManager : MonoBehaviour
{
    [Header("摄影棚Obj")]
    [SerializeField] private GameObject ui3dStudio; //UI3DStudioOfInventory预制体
    private Vector3 _ui3dStudioPos = new Vector3(2000, 2000, 0);
    private UI3DStudioOfInventory _studioInstance;
    /// <summary>
    /// 初始化3D展示摄影棚
    /// </summary>
    /// <param name="profession">角色职业</param>
    /// <returns>创建的摄影棚实例</returns>
    public UI3DStudioOfInventory InitStudio(CharacterProfession profession)
    {
        if (_studioInstance != null)
        {
            Destroy(_studioInstance.gameObject);
        }

        var studioObject = Instantiate(ui3dStudio, _ui3dStudioPos, Quaternion.identity);
        _studioInstance = studioObject.GetComponent<UI3DStudioOfInventory>();
        _studioInstance.Init(profession);
        
        return _studioInstance;
    }

    /// <summary>
    /// 获取当前摄影棚实例
    /// </summary>
    public UI3DStudioOfInventory StudioInstance => _studioInstance;

    private void OnDestroy()
    {
        if (_studioInstance != null)
        {
            Destroy(_studioInstance.gameObject);
        }
    }
}