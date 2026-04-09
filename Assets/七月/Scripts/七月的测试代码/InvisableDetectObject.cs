using System.Collections;
using UnityEngine;

public class InvisableDetectObject : MonoBehaviour
{
    [Header("测试对象")]
    [SerializeField] private GameObject targetObject;

    [Header("攻击范围设置")]
    [SerializeField] private float attackRange = 5f;           // 攻击距离
    [SerializeField] private float attackAngle = 90f;          // 扇形角度
    [SerializeField] private LayerMask enemyLayerMask;        // 敌人层级

    private float attackAngleCos;                              // 预计算的角度余弦值

    void Start()
    {
        // 预计算角度余弦值，提升性能
        attackAngleCos = Mathf.Cos(attackAngle / 2 * Mathf.Deg2Rad);
    }

    void Update()
    {
        Test1();    // 原始扇形检测
        Test2();    // 完整扇形检测 + 左右判定
        Test3();    // 分层检测
    }

    /// <summary>
    /// 原始版本：仅检测角度
    /// </summary>
    public void Test1()
    {
        if (targetObject == null) return;

        // 玩家前方扇形攻击角度（如90°，cos45°≈0.707）
        float attackAngleCos = Mathf.Cos(45 * Mathf.Deg2Rad);
        Vector3 dirToEnemy = (targetObject.transform.position - transform.position).normalized;
        float dotResult = Vector3.Dot(transform.forward, dirToEnemy);

        if (dotResult > attackAngleCos)
        {
            Debug.Log("敌人在玩家正面扇形攻击范围内！");
        }
    }

    /// <summary>
    /// 改进版本：完整扇形检测（角度+距离）+ 左右判定
    /// </summary>
    public void Test2()
    {
        if (targetObject == null) return;

        bool inRange = IsInAttackRange(targetObject.transform, attackRange, attackAngle);
        if (inRange)
        {
            // 检测敌人在左侧还是右侧
            SidePosition side = GetSidePosition(targetObject.transform);

            string sideText = side == SidePosition.Left ? "左侧" : "右侧";
            float distance = Vector3.Distance(transform.position, targetObject.transform.position);

            Debug.Log($"【完整检测】敌人在攻击范围{sideText}！距离: {distance:F2}");
        }
    }

    /// <summary>
    /// 判断目标在左侧还是右侧
    /// </summary>
    public SidePosition GetSidePosition(Transform target)
    {
        if (target == null) return SidePosition.Center;

        Vector3 dirToTarget = (target.position - transform.position).normalized;
        Vector3 crossResult = Vector3.Cross(transform.forward, dirToTarget);

        // 叉乘结果.y > 0 表示在左侧，< 0 表示在右侧
        if (crossResult.y > 0.01f)
        {
            return SidePosition.Left;
        }
        else if (crossResult.y < -0.01f)
        {
            return SidePosition.Right;
        }
        else
        {
            return SidePosition.Center;
        }
    }

    /// <summary>
    /// 完整的扇形检测（角度+距离）
    /// </summary>
    public bool IsInAttackRange(Transform target, float range, float angle)
    {
        if (target == null) return false;

        // 距离检测
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > range)
            return false;

        // 角度检测
        float angleCos = Mathf.Cos(angle / 2 * Mathf.Deg2Rad);
        Vector3 dirToTarget = (target.position - transform.position).normalized;
        float dotResult = Vector3.Dot(transform.forward, dirToTarget);

        return dotResult > angleCos;
    }

    /// <summary>
    /// 改进版本3：分层检测（检测扇形内的所有敌人）
    /// </summary>
    public void Test3()
    {
        Collider[] enemiesInAttackRange = Physics.OverlapSphere(
            transform.position,
            attackRange,
            enemyLayerMask
        );

        if (enemiesInAttackRange.Length > 0)
        {
            Debug.Log($"【分层检测】在攻击距离内发现 {enemiesInAttackRange.Length} 个敌人");

            int leftCount = 0;
            int rightCount = 0;

            foreach (Collider enemy in enemiesInAttackRange)
            {
                if (IsInAttackRange(enemy.transform, attackRange, attackAngle))
                {
                    // 判断左右
                    SidePosition side = GetSidePosition(enemy.transform);
                    string sideText = side == SidePosition.Left ? "左侧" : "右侧";

                    if (side == SidePosition.Left)
                        leftCount++;
                    else
                        rightCount++;

                    Debug.Log($"  → 检测到敌人: {enemy.name} 在{sideText}");

                    // 对该敌人造成伤害（示例）
                    // enemy.GetComponent<EnemyHealth>().TakeDamage(10);
                }
            }

            if (leftCount + rightCount > 0)
            {
                Debug.Log($"【分层检测】总计 {leftCount + rightCount} 个敌人在扇形攻击范围内！");
                Debug.Log($"  → 左侧: {leftCount} 个，右侧: {rightCount} 个");
            }
        }
    }

    /// <summary>
    /// 可视化调试（在Scene视图中绘制攻击范围）
    /// </summary>
    private void OnDrawGizmos()
    {
        // 绘制攻击范围扇形
        Gizmos.color = Color.yellow;

        Vector3 leftDir = Quaternion.Euler(0, -attackAngle / 2, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, attackAngle / 2, 0) * transform.forward;

        // 绘制扇形边界线
        Gizmos.DrawRay(transform.position, leftDir * attackRange);
        Gizmos.DrawRay(transform.position, rightDir * attackRange);

        // 绘制攻击距离圆弧（简化为几个线段）
        Gizmos.color = Color.cyan;
        int segmentCount = 20;
        Vector3 previousPoint = transform.position + leftDir * attackRange;

        for (int i = 1; i <= segmentCount; i++)
        {
            float angle = -attackAngle / 2 + (attackAngle * i / segmentCount);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            Vector3 currentPoint = transform.position + direction * attackRange;

            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }

        // 绘制玩家朝向
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * attackRange);

        // 如果有测试对象，绘制到敌人的连线
        if (targetObject != null)
        {
            bool inRange = IsInAttackRange(targetObject.transform, attackRange, attackAngle);
            SidePosition side = GetSidePosition(targetObject.transform);

            // 根据位置设置颜色
            if (!inRange)
            {
                Gizmos.color = Color.gray;  // 不在范围内
            }
            else if (side == SidePosition.Left)
            {
                Gizmos.color = Color.green;   // 在左侧
            }
            else if (side == SidePosition.Right)
            {
                Gizmos.color = Color.blue;    // 在右侧
            }
            else
            {
                Gizmos.color = Color.yellow;   // 在正前方
            }

            Gizmos.DrawLine(transform.position, targetObject.transform.position);

            // 在目标位置绘制一个小球标记
            Gizmos.DrawSphere(targetObject.transform.position, 0.2f);
        }
    }
}

/// <summary>
/// 目标相对于玩家的位置枚举
/// </summary>
public enum SidePosition
{
    Left,       // 左侧
    Right,      // 右侧
    Center      // 正前方（中心线）
}

