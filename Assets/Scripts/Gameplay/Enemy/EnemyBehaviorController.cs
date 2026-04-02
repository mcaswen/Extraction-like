using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人巡逻加敌人追踪
/// </summary>
public class EnemyBehaviorController : MonoBehaviour
{
    //敌人状态机
    public enum EnemyState { Patrol, Chase, Attack }

    //创建敌人状态机实例
    public EnemyState CurrentState;

    //获取Player位置实例
    [Header("组件引用")]
    public Transform PlayerTransform;

    // 私有字段：下划线 + 驼峰命名法
    private NavMeshAgent _navMeshAgent;//追踪component
    private PlayerHealthController _playerHealthController; //人物生命值控制器  别的脚本联动
    [Header("巡逻设置")]
    public float PatrolRadius = 10f;//巡逻半径
    public float PatrolWaitTime = 2f;//巡逻等待时间

    private float _waitTimer;//等待时间计时器
    private Vector3 _startingPosition;//3维空间巡逻位置

    [Header("索敌设置")]
    public float DetectionRange = 15f;//进入索敌范围
    public float LoseRange = 20f;//取消索敌范围

    [Header("攻击设置")]
    public float AttackRange = 2.5f;//攻击范围
    public float AttackDamage = 15f;//攻击力度
    public float AttackInterval = 1.5f;//攻击间隔时长

    private float _attackTimer;//攻击计时器

    /// <summary>
    /// 脚本开始运行
    /// </summary>
    void Start()
    {
        //游戏变量初始化
        _navMeshAgent = GetComponent<NavMeshAgent>();//获得component
        _startingPosition = transform.position;//获取当前Player位置
        CurrentState = EnemyState.Patrol;//赋值初始人物状态

        //用Tag寻找Player位置
        if (PlayerTransform == null)
        {
            PlayerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        }

        if (PlayerTransform != null)
        {
            _playerHealthController = PlayerTransform.GetComponent<PlayerHealthController>();//获取Player实例身上的血条component
        }

        GetNewPatrolPoint();//刷新新巡逻位置
    }

    /// <summary>
    /// 每帧实时更新部分
    /// </summary>
    void Update()
    {
        if (PlayerTransform == null) return;//没获取人物  就不进行Update

        float distanceToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);//获取敌人和角色两点之间距离

        //判断敌人状态  调用相关方法
        switch (CurrentState)
        {
            case EnemyState.Patrol:
                PatrolBehavior(distanceToPlayer);
                break;
            case EnemyState.Chase:
                ChaseBehavior(distanceToPlayer);
                break;
            case EnemyState.Attack:
                AttackBehavior(distanceToPlayer);
                break;
        }
    }
    /// <summary>
    /// 巡逻行为逻辑
    /// </summary>
    /// <param name="distanceToPlayer"></param>输入跟Player之间距离
    private void PatrolBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer <= DetectionRange)//小于检测范围
        {
            CurrentState = EnemyState.Chase;//修改状态为追踪
            return;
        }

        if (_navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance && !_navMeshAgent.pathPending)//剩余的距离小于停止追踪的距离并且距离计算已经结束已经完成
        {
            _waitTimer += Time.deltaTime;//开始计时
            if (_waitTimer >= PatrolWaitTime)//超过停留时间
            {
                GetNewPatrolPoint();//获取新的巡逻点
                _waitTimer = 0f;//清空计时器
            }
        }
    }
    /// <summary>
    /// 追踪行为逻辑
    /// </summary>
    /// <param name="distanceToPlayer"></param>输入和人物之间的距离
    private void ChaseBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange)//距离超过指定范围
        {
            CurrentState = EnemyState.Patrol;//修改状态为巡逻
            GetNewPatrolPoint();//立即更新巡逻点
            return;
        }

        if (distanceToPlayer <= AttackRange)//距离小于可以开始攻击范围
        {
            CurrentState = EnemyState.Attack;//修改状态为攻击
            _navMeshAgent.isStopped = true;//修改状态为停在原地
            return;
        }

        _navMeshAgent.isStopped = false;//如果没有任何变化就继续追击不停止
        _navMeshAgent.SetDestination(PlayerTransform.position);//更新新的追踪目标
    }
    /// <summary>
    /// 攻击行为逻辑
    /// </summary>
    /// <param name="distanceToPlayer"></param>输入和人物之间的距离
    private void AttackBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > AttackRange)//超过攻击范围
        {
            CurrentState = EnemyState.Chase;//回到追击行为
            _navMeshAgent.isStopped = false;//不停止
            return;
        }

        Vector3 lookPos = new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z);
        transform.LookAt(lookPos);//控制敌人看向Player

        _attackTimer += Time.deltaTime;//攻击计时器计时
        if (_attackTimer >= AttackInterval)//超过攻击间隔
        {
            if (_playerHealthController != null)//保证存在人物血条
            {
                _playerHealthController.TakeDamage(AttackDamage);//进行扣血操作
            }
            _attackTimer = 0f;//清零计时器
        }
    }
    /// <summary>
    /// 获取新的随机巡逻地点
    /// </summary>
    private void GetNewPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * PatrolRadius;//在巡逻范围中随机选取某个点在unity中的位置进行获取
        randomDirection += _startingPosition;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, PatrolRadius, NavMesh.AllAreas))
        {
            _navMeshAgent.SetDestination(hit.position);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, PatrolRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }
}