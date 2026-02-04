using System;
using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

public class Eva_Q : NetworkBehaviour
{
    [Networked] private TickTimer life { get; set; }

    private PlayerRef owner;
    private Eva_Skill ownerSkill;  // VF 라이트 체크용
    [SerializeField] private GameObject HitVFX;

    public void Init(PlayerRef player, Eva_Skill skill = null)
    {
        owner = player;
        ownerSkill = skill;
    }
    public override void Spawned()
    {
        life = TickTimer.CreateFromSeconds(Runner, 5.0f);
    }

    public async UniTaskVoid ActiveInit()
    {
        await UniTask.Delay(100);
        
        var ps = GetComponentInChildren<ParticleSystem>();
        ps.Play();
        
        var sc = GetComponent<SphereCollider>();
        sc.enabled = true;
    }
    public override void FixedUpdateNetwork()
    {
        if(life.Expired(Runner))
            Runner.Despawn(Object);
        else
            transform.position += 15 * transform.forward * Runner.DeltaTime;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // 서버에서만 데미지 처리
        if (!HasStateAuthority) return;

        if(other.GetComponentInParent<NetworkObject>() == null) return;
        // 주인이 맞았다면
        if (other.GetComponentInParent<NetworkObject>().InputAuthority == owner)
        {
            return;
        }

        IDamageProcess damageProcess = other.GetComponent<IDamageProcess>();
        var targetNO = other.GetComponentInParent<NetworkObject>();
        if (damageProcess != null && other.GetComponent<HeroState>().GetCurrHealth() > 0f)
        {
            damageProcess.OnTakeDamage(10);

            // VF 라이트 투사체 발사 (E 스킬 사용 후 3초 내)
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
        await UniTask.Delay(1000);
        
        if (Runner.IsServer)
        {
            Runner.Despawn(no);
        }
       
    }

}
