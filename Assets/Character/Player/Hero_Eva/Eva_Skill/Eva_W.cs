using System.Collections.Generic;
using System.Threading;
using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

public class Eva_W : NetworkBehaviour
{
    [Networked] private TickTimer lifeTimer { get; set; }

    private PlayerRef owner;
    private Eva_Skill ownerSkill;  // VF 라이트 체크용
    private CancellationTokenSource _cts;

    [SerializeField] private GameObject HitVFX;
    [SerializeField] private float spawnDamage = 40f;
    [SerializeField] private float endDamage = 40f;
    [SerializeField] private float slowAmount = 0.35f;  // 35% 감소 = 0.65 배율
    [SerializeField] private float skillRadius = 3.5f;  // 스킬 반경
    [SerializeField] private float centerRadius = 1.5f; // 중심부 반경 (에어본 적용)
    [SerializeField] private float duration = 1.0f;     // 지속 시간
    [SerializeField] private float airborneDuration = 1.0f;  // 에어본 지속 시간

    // 범위 안에 있는 적들 추적
    private HashSet<HeroMovement> affectedTargets = new HashSet<HeroMovement>();

    private void Awake()
    {
        Utility.RefreshToken(ref _cts);
    }

    public void Init(PlayerRef player, Eva_Skill skill = null)
    {
        owner = player;
        ownerSkill = skill;
    }

    public override void Spawned()
    {
        lifeTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    public async UniTaskVoid ActiveInit()
    {
        await UniTask.Delay(50);

        if (this == null || gameObject == null) return;

        // 파티클 재생
        var ps = GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Play();

        // 콜라이더 활성화
        var col = GetComponent<SphereCollider>();
        if (col != null)
        {
            col.enabled = true;
            col.radius = skillRadius;
        }

        // 생성 시 범위 내 적에게 데미지 (서버에서만)
        if (HasStateAuthority)
        {
            DealDamageToAllInRange(spawnDamage);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // 지속 시간 종료
        if (lifeTimer.Expired(Runner))
        {
            OnSkillEnd();
            Runner.Despawn(Object);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;
        if (other.GetComponentInParent<NetworkObject>() == null) return;

        // 자기 자신 제외
        if (other.GetComponentInParent<NetworkObject>().InputAuthority == owner) return;

        var targetMovement = other.GetComponentInParent<HeroMovement>();
        if (targetMovement != null && !affectedTargets.Contains(targetMovement))
        {
            affectedTargets.Add(targetMovement);
            // 이동 속도 35% 감소 (0.65 배율) - 슬로우 면역이면 적용 안함
            if (!targetMovement.IsSlowImmune)
            {
                targetMovement.SpeedMultiplier = 1.0f - slowAmount;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!HasStateAuthority) return;
        if (other.GetComponentInParent<NetworkObject>() == null) return;

        // 자기 자신 제외
        if (other.GetComponentInParent<NetworkObject>().InputAuthority == owner) return;

        var targetMovement = other.GetComponentInParent<HeroMovement>();
        if (targetMovement != null && affectedTargets.Contains(targetMovement))
        {
            affectedTargets.Remove(targetMovement);
            // 이동 속도 복구
            targetMovement.SpeedMultiplier = 1.0f;
        }
    }

    private void OnSkillEnd()
    {
        // 종료 시 범위 내 적에게 데미지
        DealDamageToAllInRange(endDamage);

        // 중심부 적들 에어본
        ApplyAirborneToCenter();

        // 모든 슬로우 해제
        foreach (var target in affectedTargets)
        {
            if (target != null)
            {
                target.SpeedMultiplier = 1.0f;
            }
        }
        affectedTargets.Clear();
    }

    private void DealDamageToAllInRange(float damage)
    {
        // 범위 내 모든 콜라이더 검색
        Collider[] hits = Physics.OverlapSphere(transform.position, skillRadius, LayerMask.GetMask("Character"));

        foreach (var hit in hits)
        {
            var targetNO = hit.GetComponentInParent<NetworkObject>();
            if (targetNO == null) continue;

            // 자기 자신 제외
            if (targetNO.InputAuthority == owner) continue;

            var damageProcess = hit.GetComponentInParent<IDamageProcess>();
            var heroState = hit.GetComponentInParent<HeroState>();

            if (damageProcess != null && heroState != null && heroState.GetCurrHealth() > 0f)
            {
                damageProcess.OnTakeDamage(damage);

                // VF 라이트 투사체 발사 (E 스킬 사용 후 3초 내)
                if (ownerSkill != null && targetNO != null)
                {
                    ownerSkill.TryApplyVFLight(targetNO);
                }

                // 히트 이펙트
                if (Runner.IsServer && HitVFX != null)
                {
                    var vfx = Runner.Spawn(HitVFX, hit.transform.position + Vector3.up, Quaternion.identity);
                    HitVFXDestroy(vfx).Forget();
                }
            }
        }
    }

    private void ApplyAirborneToCenter()
    {
        // 중심부 범위 내 적들 검색
        Collider[] hits = Physics.OverlapSphere(transform.position, centerRadius, LayerMask.GetMask("Character"));

        foreach (var hit in hits)
        {
            var targetNO = hit.GetComponentInParent<NetworkObject>();
            if (targetNO == null) continue;

            // 자기 자신 제외
            if (targetNO.InputAuthority == owner) continue;

            var targetMovement = hit.GetComponentInParent<HeroMovement>();
            if (targetMovement != null)
            {
                targetMovement.ApplyAirborne(1f, airborneDuration);
            }
        }
    }

    private async UniTaskVoid HitVFXDestroy(NetworkObject no)
    {
        await UniTask.Delay(1000);
        if (this == null || no == null) return;
        if (Runner.IsServer)
        {
            Runner.Despawn(no);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // 슬로우 해제
        foreach (var target in affectedTargets)
        {
            if (target != null)
            {
                target.SpeedMultiplier = 1.0f;
            }
        }
        affectedTargets.Clear();

        Utility.RefreshToken(ref _cts);
    }
}
