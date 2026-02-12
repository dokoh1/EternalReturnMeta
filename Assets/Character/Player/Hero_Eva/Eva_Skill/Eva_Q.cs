using System;
using System.Collections.Generic;
using Character.Player;
using Character.Player.ControlSettings;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

public class Eva_Q : NetworkBehaviour
{
    private const float ProjectileSpeed = 15f;
    private const float ProjectileLifetime = 5f;
    private const float ProjectileDamage = 10f;
    private const int HitVfxLifetimeMs = 1000;

    [Networked] private TickTimer life { get; set; }

    private PlayerRef owner;
    private Eva_Skill ownerSkill;
    [SerializeField] private GameObject HitVFX;

    [SerializeField] private ControlSettingsConfig _controlConfig;

    private float _hitRadius;
    private List<LagCompensatedHit> _lagHits = new();

    public void Init(PlayerRef player, Eva_Skill skill = null)
    {
        owner = player;
        ownerSkill = skill;
    }

    public override void Spawned()
    {
        life = TickTimer.CreateFromSeconds(Runner, ProjectileLifetime);

        var sc = GetComponent<SphereCollider>();
        _hitRadius = sc != null ? sc.radius : 0.5f;

        if (UseLagComp() && sc != null)
            sc.enabled = false;
    }

    public void ActiveInit()
    {
        var ps = GetComponentInChildren<ParticleSystem>();
        ps.Play();

        if (!UseLagComp())
        {
            var sc = GetComponent<SphereCollider>();
            sc.enabled = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (life.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        transform.position += ProjectileSpeed * transform.forward * Runner.DeltaTime;

        if (HasStateAuthority && UseLagComp())
            CheckLagCompensatedHit();
    }

    private void CheckLagCompensatedHit()
    {
        _lagHits.Clear();
        Runner.LagCompensation.OverlapSphere(transform.position, _hitRadius, owner, _lagHits,
            LayerMask.GetMask("Character"));

        foreach (var hit in _lagHits)
        {
            var targetNO = hit.Hitbox?.Root?.GetComponentInParent<NetworkObject>();
            if (targetNO == null || targetNO.InputAuthority == owner) continue;

            var damageProcess = targetNO.GetComponentInChildren<IDamageProcess>();
            var heroState = targetNO.GetComponentInChildren<HeroState>();

            if (damageProcess != null && heroState != null && heroState.GetCurrHealth() > 0f)
            {
                damageProcess.OnTakeDamage(ProjectileDamage);

                if (ownerSkill != null)
                {
                    ownerSkill.TryApplyVFLight(targetNO);
                }

                if (Runner.IsServer)
                {
                    var no = Runner.Spawn(HitVFX, targetNO.transform.position + new Vector3(0, 1, 0), Quaternion.identity);
                    HitVFXDestroy(no).Forget();
                }
            }

            Runner.Despawn(Object);
            return;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;
        if (UseLagComp()) return;

        if (other.GetComponentInParent<NetworkObject>() == null) return;

        if (other.GetComponentInParent<NetworkObject>().InputAuthority == owner)
        {
            return;
        }

        IDamageProcess damageProcess = other.GetComponent<IDamageProcess>();
        var targetNO = other.GetComponentInParent<NetworkObject>();

        if (damageProcess != null && other.GetComponent<HeroState>().GetCurrHealth() > 0f)
        {
            damageProcess.OnTakeDamage(ProjectileDamage);

            if (ownerSkill != null && targetNO != null)
            {
                ownerSkill.TryApplyVFLight(targetNO);
            }

            if (Runner.IsServer)
            {
                var no = Runner.Spawn(HitVFX, other.transform.position + new Vector3(0, 1, 0), Quaternion.identity);
                HitVFXDestroy(no).Forget();
            }
        }
    }

    public async UniTaskVoid HitVFXDestroy(NetworkObject no)
    {
        await UniTask.Delay(HitVfxLifetimeMs);
        if (this == null || no == null) return;

        if (Runner.IsServer)
        {
            Runner.Despawn(no);
        }
    }

    private bool UseLagComp() => _controlConfig != null && _controlConfig.EnableLagCompensation
                                && Runner != null && Runner.LagCompensation != null;
}
