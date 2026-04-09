using System.Collections;
using DG.Tweening;
using UnityEngine;
using Slider = UnityEngine.UI.Slider;

public class PlayerRespawnPanel : MonoBehaviour
{
    [SerializeField] private Slider respawnSlider;
    [SerializeField] private CanvasGroup canvas;
    private float respawnTime=1.5f;
    private bool isRespawn;
    
    public void Init()
    {
        isRespawn = false;
        canvas.alpha = 0;
        respawnSlider.maxValue = 1;
        canvas.DOFade(1, 0.3f).onComplete=()=>
            {
                respawnSlider.DOValue(1, 1.2f);
            }
        ;   
        StartCoroutine(IsRespawnOk());
    }

    private IEnumerator IsRespawnOk()
    {
        yield return new WaitForSeconds(respawnTime);
        while (!isRespawn)
        {
            yield return null;
        }
        canvas.DOFade(0f, 0.3f).onComplete = () =>
        {
            transform.DOKill();
            respawnSlider.DOKill();
            UIManager.Instance.ClosePanel<PlayerRespawnPanel>();
            Destroy(gameObject);
        };
    }

    public void OnPlayerRespawn(GameObject player)
    {
        isRespawn = true;
    }
    
}
