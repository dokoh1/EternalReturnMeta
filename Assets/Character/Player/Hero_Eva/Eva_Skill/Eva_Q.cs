using System;
using Character.Player;
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

    public void Init(PlayerRef player, Eva_Skill skill = null)
    {
        owner = player;
        ownerSkill = skill;
    }

    public override void Spawned()
    {
        life = TickTimer.CreateFromSeconds(Runner, ProjectileLifetime);
    }

    public void ActiveInit()
    {
        var ps = GetComponentInChildren<ParticleSystem>();
        ps.Play();

        var sc = GetComponent<SphereCollider>();
        sc.enabled = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (life.Expired(Runner))
            Runner.Despawn(Object);
        else
            transform.position += ProjectileSpeed * transform.forward * Runner.DeltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;

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

        if (Runner.IsServer)
        {
            Runner.Despawn(no);
        }
    }
}
