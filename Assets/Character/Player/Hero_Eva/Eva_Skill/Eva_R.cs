using System.Collections.Generic;
using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

public class Eva_R : NetworkBehaviour
{
    private const float TickDamage = 2f;
    private const float TickIntervalSec = 0.1f;
    private const int HitVfxLifetimeMs = 1000;
    private const float HeightOffset = 1f;

    private PlayerRef owner;
    private Eva_Skill ownerSkill;
    [SerializeField] private GameObject HitVFX;

    private readonly HashSet<Collider> _targetsInRange = new();
    [Networked] private TickTimer DamageTimer { get; set; }

    public void Init(PlayerRef player, Eva_Skill skill = null)
    {
        owner = player;
        ownerSkill = skill;
    }

    public void ActiveInit()
    {
        if (this == null || gameObject == null) return;

        var ps = GetComponent<ParticleSystem>();
        ps.Play();

        var bc = GetComponent<BoxCollider>();
        bc.enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null || no.InputAuthority == owner) return;

        _targetsInRange.Add(other);

        if (!DamageTimer.IsRunning)
        {
            DamageTimer = TickTimer.CreateFromSeconds(Runner, TickIntervalSec);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null || no.InputAuthority == owner) return;

        _targetsInRange.Remove(other);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (_targetsInRange.Count == 0) return;

        if (DamageTimer.Expired(Runner))
        {
            DamageTimer = TickTimer.CreateFromSeconds(Runner, TickIntervalSec);
            DealDamageToAll();
        }
    }

    private void DealDamageToAll()
    {
        _targetsInRange.RemoveWhere(c => c == null);

        foreach (var other in _targetsInRange)
        {
            var damageProcess = other.GetComponent<IDamageProcess>();
            var heroState = other.GetComponent<HeroState>();

            if (damageProcess == null || heroState == null) continue;
            if (heroState.GetCurrHealth() <= 0f) continue;

            damageProcess.OnTakeDamage(TickDamage);

            var targetNO = other.GetComponentInParent<NetworkObject>();
            if (ownerSkill != null && targetNO != null)
            {
                ownerSkill.TryApplyVFLight(targetNO);
            }

            if (Runner != null && Runner.IsServer && HitVFX != null)
            {
                var no = Runner.Spawn(HitVFX, other.transform.position + new Vector3(0, HeightOffset, 0), Quaternion.identity);
                if (no != null)
                {
                    HitVFXDestroy(no).Forget();
                }
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _targetsInRange.Clear();
    }

    // VFX 제거는 게임 상태에 영향 없음 → UniTask 유지
    public async UniTaskVoid HitVFXDestroy(NetworkObject no)
    {
        await UniTask.Delay(HitVfxLifetimeMs);

        if (this == null || gameObject == null || Runner == null) return;
        Runner.Despawn(no);
    }
}
