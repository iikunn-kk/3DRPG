using UnityEngine;

public class CharacterActionMod : MonoBehaviour
{
   [SerializeField] private Transform objParent;
   [Header("角色展示界面的Controller")]
   [SerializeField] private RuntimeAnimatorController animatorController;
   
   public RenderTexture RenderTexture=>_renderTexture;
   private RenderTexture _renderTexture;
   private GameObject _activeObj;
   private Animator _animator;
   
   // 添加旋转相关字段
   private float _rotationY = 0f;
   private float _smoothRotationY = 0f;
   private const float RotationSpeed = 15f; // 进一步提高旋转速度使更跟手
   private const float BaseRotation = 180f; // 基础旋转角度，补偿objParent的初始180度旋转
   
   void Update()
   {
       // 平滑旋转，使用更高的速度使更跟手
       _smoothRotationY = Mathf.LerpAngle(_smoothRotationY, _rotationY, Time.deltaTime * RotationSpeed);
       if (objParent != null)
       {
           // 添加基础旋转角度，确保模型初始面向玩家
           objParent.rotation = Quaternion.Euler(0, _smoothRotationY + BaseRotation, 0);
       }
   }
   
   public void ChangeGameObject(GameObject obj,CharacterProfession profession)
   {
      if (_activeObj)
      {
         Destroy(_activeObj);
         _activeObj = null;
      }
      SetActionObj(obj,profession);

      // 重置旋转角度，确保新模型面向玩家
      ResetRotation();
   }
   
   public void SetAction(CharacterActionEnum action)
   {
      _animator.SetTrigger(action.ToString());
   }
   
   private void SetActionObj(GameObject obj,CharacterProfession profession)
   {
      _activeObj= Instantiate(obj, objParent);
      _animator = _activeObj.GetComponent<Animator>();
      _animator.runtimeAnimatorController = animatorController;
      if (profession!=CharacterProfession.嘉然)
      {
         _activeObj.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
      }
   }
   
   // 添加设置旋转的方法
   public void SetRotation(float rotation)
   {
       _rotationY = rotation;
   }
   
   // 添加增加旋转的方法
   public void AddRotation(float deltaRotation)
   {
       // 反转旋转方向，解决拖动方向相反的问题
       _rotationY += deltaRotation;
   }
   
   // 获取当前旋转角度
   public float GetRotation()
   {
       return _rotationY;
   }
   
   // 重置旋转角度
   public void ResetRotation()
   {
       _rotationY = 0f;
       _smoothRotationY = 0f;
   }
   
   // 设置基础旋转角度（用于补偿objParent的初始旋转）
   public void SetBaseRotation(float baseRotation)
   {
       // 这个方法可以用于动态调整基础旋转角度
   }
}
public enum CharacterActionEnum
{
  Idle,Shy,Dance
}