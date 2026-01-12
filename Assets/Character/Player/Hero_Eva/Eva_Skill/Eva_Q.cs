using System;
using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

public class Eva_Q : NetworkBehaviour
{
    [Networked] private TickTimer life { get; set; }

    private PlayerRef owner;
    [SerializeField] private GameObject HitVFX;
    
    public void Init(PlayerRef player)
    {
        owner = player;
        Debug.Log($"구체의 주인 : {owner}");
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
        if(other.GetComponentInParent<NetworkObject>() == null) return;
        // 주인이 맞았다면
        if (other.GetComponentInParent<NetworkObject>().InputAuthority == owner)
        {
            Debug.Log($"구체의 오너 : {owner} || 맞은넘 : {other.GetComponentInParent<NetworkObject>().InputAuthority} ==> 내꺼니까 무시할게");
            
            return;
        }
        
        Debug.Log($"구체의 오너 : {owner} || 맞은넘 : {other.GetComponentInParent<NetworkObject>().InputAuthority} ==> 데미지 줄게");
     
        IDamageProcess damageProcess = other.GetComponent<IDamageProcess>();
        if (damageProcess != null && other.GetComponent<HeroState>().GetCurrHealth() > 0f)
        {
            damageProcess.OnTakeDamage(10);
            
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
