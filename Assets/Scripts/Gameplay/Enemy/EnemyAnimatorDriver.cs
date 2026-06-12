using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Minimal bridge from enemy runtime movement/attack logic to the shared enemy Animator parameters.
/// </summary>
public sealed class EnemyAnimatorDriver
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private const float StaticIdleFrequency = 2.2f;
    private const float StaticMoveFrequency = 7.5f;
    private const float StaticAttackDuration = 0.32f;
    private const float StaticGroundAlignInterval = 0.15f;
    private const int StaticGroundAlignAttempts = 10;

    private readonly Component _owner;
    private readonly Animator _animator;
    private readonly Transform _staticVisualTarget;
    private readonly Vector3 _staticBaseLocalPosition;
    private readonly Quaternion _staticBaseLocalRotation;
    private readonly Vector3 _staticBaseLocalScale;
    private readonly Transform _animatedRootBone;
    private readonly Vector3 _animatedRootBaseLocalPosition;
    private float _currentSpeed;
    private float _staticPhase;
    private float _staticAttackTimer;
    private float _nextGroundAlignTime;
    private int _groundAlignAttemptsRemaining = StaticGroundAlignAttempts;

    public EnemyAnimatorDriver(Component owner)
    {
        _owner = owner;
        _animator = ResolveAnimator(owner);
        PrimeAnimator(owner, _animator);
        EnemyRuntimeVisualUtility.AlignVisualToGround(owner);

        if (ShouldUseStaticFallback(owner, _animator))
        {
            _staticVisualTarget = ResolveFallbackVisualTarget(owner);
            if (_staticVisualTarget != null)
            {
                _staticBaseLocalPosition = _staticVisualTarget.localPosition;
                _staticBaseLocalRotation = _staticVisualTarget.localRotation;
                _staticBaseLocalScale = _staticVisualTarget.localScale;

                string ownerName = owner != null ? owner.name : "Enemy";
                Debug.Log(
                    $"[{ownerName}] Enemy model has no skinned mesh or visible bone hierarchy. Using static visual fallback until a rigged/skinned model is assigned.",
                    owner);
            }
        }
        else
        {
            _animatedRootBone = ResolveAnimatedRootBone(_animator);
            if (_animatedRootBone != null)
            {
                _animatedRootBaseLocalPosition = _animatedRootBone.localPosition;
            }
        }
    }

    public void SetSpeed(float speed)
    {
        _currentSpeed = Mathf.Max(0f, speed);
        if (!HasPlayableAnimator())
        {
            TickGroundAlignment();
            TickStaticFallback();
            return;
        }

        _animator.SetFloat(SpeedHash, _currentSpeed);
        TickGroundAlignment();
        TickStaticFallback();
    }

    public void SetSpeedFromAgent(NavMeshAgent agent)
    {
        if (agent == null)
        {
            SetSpeed(0f);
            return;
        }

        SetSpeed(agent.isStopped ? 0f : agent.velocity.magnitude);
    }

    public void TriggerAttack()
    {
        if (!HasPlayableAnimator())
        {
            _staticAttackTimer = StaticAttackDuration;
            return;
        }

        _animator.SetTrigger(AttackHash);
        _staticAttackTimer = StaticAttackDuration;
    }

    public void LateUpdate()
    {
        StabilizeAnimatedRootBone();
    }

    private static Animator ResolveAnimator(Component owner)
    {
        if (owner == null)
        {
            return null;
        }

        Animator[] animators = owner.GetComponentsInChildren<Animator>(true);
        if (animators == null || animators.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                return animator;
            }
        }

        return animators[0];
    }

    private static void PrimeAnimator(Component owner, Animator animator)
    {
        string ownerName = owner != null ? owner.name : "Enemy";
        if (animator == null)
        {
            Debug.LogWarning($"[{ownerName}] Enemy has no Animator in children.", owner);
            return;
        }

        animator.enabled = true;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[{ownerName}] Enemy Animator has no controller.", animator);
            return;
        }

        if (animator.avatar != null && !animator.avatar.isValid)
        {
            Debug.LogWarning($"[{ownerName}] Enemy Animator Avatar is invalid: {animator.avatar.name}.", animator);
        }

        animator.Rebind();
        int idleHash = Animator.StringToHash("Idle");
        if (animator.HasState(0, idleHash))
        {
            animator.Play(idleHash, 0, 0f);
        }

        animator.Update(0f);
    }

    private void TickGroundAlignment()
    {
        if (_staticVisualTarget == null ||
            _owner == null ||
            _groundAlignAttemptsRemaining <= 0 ||
            Time.time < _nextGroundAlignTime)
        {
            return;
        }

        _groundAlignAttemptsRemaining--;
        _nextGroundAlignTime = Time.time + StaticGroundAlignInterval;
        EnemyRuntimeVisualUtility.AlignVisualToGround(_owner);
    }

    private void TickStaticFallback()
    {
        if (_staticVisualTarget == null)
        {
            return;
        }

        float deltaTime = Mathf.Max(0f, Time.deltaTime);
        float speed01 = Mathf.Clamp01(_currentSpeed / 3f);
        _staticPhase += deltaTime * Mathf.Lerp(StaticIdleFrequency, StaticMoveFrequency, speed01);

        float idleWave = Mathf.Sin(Time.time * StaticIdleFrequency);
        float moveWave = Mathf.Sin(_staticPhase);
        float stepWave = Mathf.Abs(Mathf.Sin(_staticPhase));
        float bob = Mathf.Lerp(idleWave * 0.012f, stepWave * 0.04f, speed01);
        float roll = Mathf.Lerp(idleWave * 1.4f, moveWave * 4.5f, speed01);
        float pitch = Mathf.Lerp(0f, Mathf.Sin(_staticPhase * 2f) * 2.5f, speed01);

        float attack01 = 0f;
        if (_staticAttackTimer > 0f)
        {
            _staticAttackTimer = Mathf.Max(0f, _staticAttackTimer - deltaTime);
            float normalized = 1f - _staticAttackTimer / StaticAttackDuration;
            attack01 = Mathf.Sin(normalized * Mathf.PI);
        }

        _staticVisualTarget.localPosition = _staticBaseLocalPosition + new Vector3(0f, bob, attack01 * 0.08f);
        _staticVisualTarget.localRotation = _staticBaseLocalRotation * Quaternion.Euler(pitch - attack01 * 10f, 0f, roll);
        _staticVisualTarget.localScale = _staticBaseLocalScale * (1f + Mathf.Lerp(idleWave * 0.008f, 0.018f, speed01) + attack01 * 0.05f);
    }

    private void StabilizeAnimatedRootBone()
    {
        if (_animatedRootBone == null)
        {
            return;
        }

        Vector3 localPosition = _animatedRootBone.localPosition;
        localPosition.x = _animatedRootBaseLocalPosition.x;
        localPosition.z = _animatedRootBaseLocalPosition.z;
        _animatedRootBone.localPosition = localPosition;
    }

    private bool HasPlayableAnimator()
    {
        return _animator != null && _animator.runtimeAnimatorController != null;
    }

    private static Transform ResolveAnimatedRootBone(Animator animator)
    {
        if (animator == null || animator.avatar != null)
        {
            return null;
        }

        Transform hips = animator.transform.Find("mixamorig:Hips");
        if (hips != null)
        {
            return hips;
        }

        Transform[] transforms = animator.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name.EndsWith(":Hips", System.StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool ShouldUseStaticFallback(Component owner, Animator animator)
    {
        if (owner == null)
        {
            return false;
        }

        if (HasUsableSkinnedMesh(owner) || HasVisibleBoneHierarchy(owner, animator))
        {
            return false;
        }

        return ResolveFallbackVisualTarget(owner) != null;
    }

    private static bool HasUsableSkinnedMesh(Component owner)
    {
        SkinnedMeshRenderer[] skinnedMeshRenderers = owner.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinnedMeshRenderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = skinnedMeshRenderers[i];
            if (renderer != null &&
                renderer.enabled &&
                (renderer.rootBone != null || (renderer.bones != null && renderer.bones.Length > 0)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasVisibleBoneHierarchy(Component owner, Animator animator)
    {
        Transform searchRoot = animator != null ? animator.transform : owner != null ? owner.transform : null;
        if (searchRoot == null)
        {
            return false;
        }

        Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            if (lowerName.Contains("mixamorig") || lowerName == "hips" || lowerName.EndsWith(":hips"))
            {
                return true;
            }
        }

        return false;
    }

    private static Transform ResolveFallbackVisualTarget(Component owner)
    {
        if (owner == null)
        {
            return null;
        }

        Transform root = owner.transform;
        Transform visualRoot = root.Find("Visual");
        if (visualRoot == null)
        {
            visualRoot = root;
        }

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null ||
                !renderer.enabled ||
                renderer is LineRenderer ||
                renderer.GetComponentInParent<Canvas>() != null)
            {
                continue;
            }

            return renderer.transform;
        }

        return visualRoot != root ? visualRoot : null;
    }
}

public static class EnemyRuntimeVisualUtility
{
    private const float GroundSampleRadius = 3f;
    private const float MinAlignDelta = 0.01f;
    private const float MaxAlignDelta = 10f;
    private const float FootBoneRendererGap = 0.15f;
    private const float FootContactPadding = 0.03f;

    public static void AlignVisualToGround(Component owner)
    {
        if (owner == null)
        {
            return;
        }

        Transform root = owner.transform;
        Transform visualRoot = root.Find("Visual");
        if (visualRoot == null)
        {
            visualRoot = root;
        }

        if (!TryResolveRendererBottom(visualRoot, out float rendererBottomY))
        {
            return;
        }

        if (TryResolveFootContactBottom(visualRoot, rendererBottomY, out float footBottomY))
        {
            rendererBottomY = footBottomY;
        }

        float groundY = root.position.y;
        if (NavMesh.SamplePosition(root.position, out NavMeshHit hit, GroundSampleRadius, NavMesh.AllAreas))
        {
            groundY = hit.position.y;
        }

        float delta = groundY - rendererBottomY;
        if (Mathf.Abs(delta) < MinAlignDelta)
        {
            return;
        }

        if (Mathf.Abs(delta) > MaxAlignDelta)
        {
            Debug.LogWarning($"[{owner.name}] Skipped enemy visual ground alignment. Delta {delta:F2} is too large.", owner);
            return;
        }

        visualRoot.position += Vector3.up * delta;
    }

    private static bool TryResolveRendererBottom(Transform visualRoot, out float bottomY)
    {
        bottomY = 0f;
        if (visualRoot == null)
        {
            return false;
        }

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        bool hasRenderer = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null ||
                !renderer.enabled ||
                renderer is LineRenderer ||
                renderer.GetComponentInParent<Canvas>() != null)
            {
                continue;
            }

            float minY = renderer.bounds.min.y;
            bottomY = hasRenderer ? Mathf.Min(bottomY, minY) : minY;
            hasRenderer = true;
        }

        return hasRenderer;
    }

    private static bool TryResolveFootContactBottom(Transform visualRoot, float rendererBottomY, out float bottomY)
    {
        bottomY = 0f;
        if (visualRoot == null)
        {
            return false;
        }

        Transform[] transforms = visualRoot.GetComponentsInChildren<Transform>(true);
        bool hasToeBone = false;
        bool hasFootBone = false;
        float toeY = 0f;
        float footY = 0f;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null)
            {
                continue;
            }

            string name = candidate.name;
            bool isToeBone =
                name.EndsWith(":LeftToeBase", System.StringComparison.Ordinal) ||
                name.EndsWith(":RightToeBase", System.StringComparison.Ordinal) ||
                name == "LeftToeBase" ||
                name == "RightToeBase";
            if (isToeBone)
            {
                toeY = hasToeBone ? Mathf.Min(toeY, candidate.position.y) : candidate.position.y;
                hasToeBone = true;
                continue;
            }

            bool isFootBone =
                name.EndsWith(":LeftFoot", System.StringComparison.Ordinal) ||
                name.EndsWith(":RightFoot", System.StringComparison.Ordinal) ||
                name == "LeftFoot" ||
                name == "RightFoot";
            if (isFootBone)
            {
                footY = hasFootBone ? Mathf.Min(footY, candidate.position.y) : candidate.position.y;
                hasFootBone = true;
            }
        }

        if (!hasToeBone && !hasFootBone)
        {
            return false;
        }

        bottomY = hasToeBone ? toeY : footY;
        if (bottomY - rendererBottomY <= FootBoneRendererGap)
        {
            return false;
        }

        bottomY -= FootContactPadding;
        return true;
    }
}
