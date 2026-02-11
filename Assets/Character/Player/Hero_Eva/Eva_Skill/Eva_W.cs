using System.Collections.Generic;
using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

public class Eva_W : NetworkBehaviour
{
    private const int HitVfxLifetimeMs = 1000;
    private const float AirborneHeight = 1f;

    [Networked] private TickTimer lifeTimer { get; set; }

    private PlayerRef owner;
    private Eva_Skill ownerSkill;

    [SerializeField] private GameObject HitVFX;
    [SerializeField] private float spawnDamage = 40f;
    [SerializeField] private float endDamage = 40f;
    [SerializeField] private float slowAmount = 0.35f;
    [SerializeField] private float skillRadius = 3.5f;
    [SerializeField] private float centerRadius = 1.5f;
    [SerializeField] private float duration = 1.0f;
    [SerializeField] private float airborneDuration = 1.0f;

    private HashSet<HeroMovement> affectedTargets = new();

    public void Init(PlayerRef player, Eva_Skill skill = null)
    {
        owner = player;
        ownerSkill = skill;
    }

    public override void Spawned()
    {
        lifeTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    public void ActiveInit()
    {
        if (this == null || gameObject == null) return;

        var ps = GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Play();

        var col = GetComponent<SphereCollider>();
        if (col != null)
        {
            col.enabled = true;
            col.radius = skillRadius;
        }

        if (HasStateAuthority)
        {
            DealDamageToAllInRange(spawnDamage);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (lifeTimer.Expired(Runner))
        {
            OnSkillEnd();
            Runner.Despawn(Object);
        }
    }

    private bool IsOwner(NetworkObject no)
    {
        return no != null && no.InputAuthority == owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null) return;
        if (IsOwner(no)) return;

        var targetMovement = other.GetComponentInParent<HeroMovement>();
        if (targetMovement != null && !affectedTargets.Contains(targetMovement))
        {
            affectedTargets.Add(targetMovement);
            if (!targetMovement.IsSlowImmune)
            {
                targetMovement.SpeedMultiplier = 1.0f - slowAmount;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!HasStateAuthority) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null) return;
        if (IsOwner(no)) return;

        var targetMovement = other.GetComponentInParent<HeroMovement>();
        if (targetMovement != null && affectedTargets.Contains(targetMovement))
        {
            affectedTargets.Remove(targetMovement);
            targetMovement.SpeedMultiplier = 1.0f;
        }
    }

    private void OnSkillEnd()
    {
        DealDamageToAllInRange(endDamage);
        ApplyAirborneToCenter();

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
        Collider[] hits = Physics.OverlapSphere(transform.position, skillRadius, LayerMask.GetMask("Character"));

        foreach (var hit in hits)
        {
            var targetNO = hit.GetComponentInParent<NetworkObject>();
            if (targetNO == null) continue;
            if (IsOwner(targetNO)) continue;

            var damageProcess = hit.GetComponentInParent<IDamageProcess>();
            var heroState = hit.GetComponentInParent<HeroState>();

            if (damageProcess != null && heroState != null && heroState.GetCurrHealth() > 0f)
            {
                damageProcess.OnTakeDamage(damage);

                if (ownerSkill != null && targetNO != null)
                {
                    ownerSkill.TryApplyVFLight(targetNO);
                }

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
        Collider[] hits = Physics.OverlapSphere(transform.position, centerRadius, LayerMask.GetMask("Character"));

        foreach (var hit in hits)
        {
            var targetNO = hit.GetComponentInParent<NetworkObject>();
            if (targetNO == null) continue;
            if (IsOwner(targetNO)) continue;

            var targetMovement = hit.GetComponentInParent<HeroMovement>();
            if (targetMovement != null)
            {
                targetMovement.ApplyAirborne(AirborneHeight, airborneDuration);
            }
        }
    }

    private async UniTaskVoid HitVFXDestroy(NetworkObject no)
    {
        await UniTask.Delay(HitVfxLifetimeMs);
        if (this == null || no == null) return;
        if (Runner.IsServer)
        {
            Runner.Despawn(no);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        foreach (var target in affectedTargets)
        {
            if (target != null)
            {
                target.SpeedMultiplier = 1.0f;
            }
        }
        affectedTargets.Clear();
    }
}
