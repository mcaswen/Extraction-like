using UnityEngine;
using UnityEngine.AI;
/// <summary>
/// 远程敌人ai
/// </summary>
public class RangedEnemyBehaviorController : MonoBehaviour
{
    public enum EnemyState { Patrol, Chase, Attack }//敌人状态
    public EnemyState CurrentState;//实例化状态

    [Header("组件引用")]
    public Transform PlayerTransform;//获得目标transform
    public Transform FirePoint; // 新增：敌人开火的枪口位置
    public GameObject EnemyBulletPrefab; // 新增：拖入红色的敌人子弹预制体

    private NavMeshAgent _navMeshAgent;//AI寻路部分

    [Header("巡逻设置")]
    public float PatrolRadius = 10f;//巡逻半径
    public float PatrolWaitTime = 2f;//巡逻等待时间
    private float _waitTimer;//等待计时器
    private Vector3 _startingPosition;//巡逻开始位置

    [Header("索敌设置")]
    public float DetectionRange = 15f;//检测范围
    public float LoseRange = 20f;//失去索敌状态范围

    [Header("攻击设置")]
    public float AttackRange = 10f; // 核心修改：远程敌人在 10 米外就会停下开枪！
    public float AttackInterval = 2f; // 每 2 秒开一枪
    private float _attackTimer;//射击计时器
    /// <summary>
    /// 脚本开始运行
    /// </summary>
    void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();//敌人AI脚本实例化
        _startingPosition = transform.position;//获得敌人当前位置
        CurrentState = EnemyState.Patrol;//初始化敌人状态
        //用tag寻找玩家    findgameobjectwithtag
        if (PlayerTransform == null)
        {
            PlayerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        }

        GetNewPatrolPoint();//开始刷新开始巡逻地点
    }
    /// <summary>
    /// 实时更新
    /// </summary>
    void Update()
    {//实时更新敌人和角色之间的距离
        //实时根据人物不同的状态调用不同状态逻辑函数
        if (PlayerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);

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

    private void PatrolBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer <= DetectionRange)
        {
            CurrentState = EnemyState.Chase;
            return;
        }
        //敌人追击到角色  开始停止   计时器开始计时
        if (_navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance && !_navMeshAgent.pathPending)
        {
            _waitTimer += Time.deltaTime;
            if (_waitTimer >= PatrolWaitTime)
            {//时间通过   重新开始巡逻
                GetNewPatrolPoint();
                _waitTimer = 0f;
            }
        }
    }
    /// <summary>
    /// 追击行为逻辑
    /// </summary>
    /// <param name="distanceToPlayer"></param>和主角之间的距离
    private void ChaseBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange)//超过丢失范围   就换状态
        {
            CurrentState = EnemyState.Patrol;
            GetNewPatrolPoint();
            return;
        }

        if (distanceToPlayer <= AttackRange)//进入攻击范围  切换状态
        {
            CurrentState = EnemyState.Attack;
            _navMeshAgent.isStopped = true; // 停下脚步，准备开枪  ！！
            return;
        }

        _navMeshAgent.isStopped = false;//追击过程不能停下
        _navMeshAgent.SetDestination(PlayerTransform.position);//继续追击人物
    }
    /// <summary>
    /// 攻击行为
    /// </summary>
    /// <param name="distanceToPlayer"></param>
    private void AttackBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > AttackRange)//超出攻击范围   换追击状态
        {
            CurrentState = EnemyState.Chase;
            _navMeshAgent.isStopped = false; // 玩家跑远了，继续追  从人物静止到开始运动
            return;
        }

        // 核心修改 1：让敌人始终瞄准玩家（保持 Y 轴水平防倾斜）
        Vector3 lookPos = new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z);
        transform.LookAt(lookPos);

        // 核心修改 2：计时器开火
        _attackTimer += Time.deltaTime;
        if (_attackTimer >= AttackInterval)//超过等待时间开始射击  清空计时器
        {
            Shoot();
            _attackTimer = 0f;
        }
    }
    /// <summary>
    /// 射击函数
    /// </summary>
    private void Shoot()
    {
        if (FirePoint != null && EnemyBulletPrefab != null)//检查开火点和子弹预制体
        {
            // 在敌人的枪口位置，生成敌人专属的红色子弹
            Instantiate(EnemyBulletPrefab, FirePoint.position, FirePoint.rotation);//实例化    预制体  位置  方向
        }
    }
    /// <summary>
    /// 获得随机巡逻起始点
    /// </summary>
    private void GetNewPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * PatrolRadius;//根据倍数在单位圆内  随机获得方向  insideunitysphere
        randomDirection += _startingPosition;//经典方向加上位置   给定一个指定的路径
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, PatrolRadius, NavMesh.AllAreas))//安全检查场景中可移动位置   路径  空箱子  巡逻范围   巡逻区域（后两个是防止前面选择出来的路径不符合标准，将自动生成一个路径）
        {
            _navMeshAgent.SetDestination(hit.position);//设置目标路径
        }
    }
    //画出
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, PatrolRadius);
        // 画出远程攻击距离
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }
}