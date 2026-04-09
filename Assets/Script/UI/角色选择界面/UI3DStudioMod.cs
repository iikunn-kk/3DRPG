using UnityEngine;
using UnityEngine.UI;

public class UI3DStudioMod : MonoBehaviour
{
    [SerializeField] private Camera studioCamera;
    [SerializeField] private Transform objParent;
    private GameObject _currentObj;
    public RenderTexture renderTexture=>_renderTexture;
    private RenderTexture _renderTexture;
    private CharacterSelectData _characterSelectData;
    public void Init( CharacterSelectData characterSelectData)
    {
        _characterSelectData = characterSelectData;
        _renderTexture=_characterSelectData.texture;
        studioCamera.targetTexture = _renderTexture;
        _currentObj = Instantiate(characterSelectData.showObj, objParent);
        if (characterSelectData.job != CharacterProfession.嘉然)
        {
            _currentObj.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
        }
    }
    
}