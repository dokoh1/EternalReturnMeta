using System.Threading;
using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

public class Eva_R : NetworkBehaviour
{
    private PlayerRef owner;
    private CancellationTokenSource _cts;
    [SerializeField] private GameObject HitVFX;
    
    private void Awake()
    {
        Utility.RefreshToken(ref _cts);
    }
    
    public void Init(PlayerRef player)
    {
        owner = player;
        Debug.Log($"구체의 주인 : {owner}");
    }

    public async UniTaskVoid ActiveInit()
    {
        await UniTask.Delay(500);
        
        if (this == null || gameObject == null) return;
        
        var ps = GetComponent<ParticleSystem>();
        ps.Play();
        
        var bc = GetComponent<BoxCollider>();
        bc.enabled = true;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if(other.GetComponentInParent<NetworkObject>() == null) return;
        // 주인이 맞았다면
        if (other.GetComponentInParent<NetworkObject>().InputAuthority == owner)
        {
            //Debug.Log($"구체의 오너 : {owner} || 맞은넘 : {other.GetComponentInParent<NetworkObject>().InputAuthority} ==> 내꺼니까 무시할게");
            return;
        }
        
        Debug.Log($"구체의 오너 : {owner} || 맞은넘 : {other.GetComponentInParent<NetworkObject>().InputAuthority} ==> 데미지 줄게");
     
        DamageLoop(other, _cts.Token).Forget();
        
    }

    private void OnTriggerExit(Collider other)
    {
        if(other.GetComponentInParent<NetworkObject>() == null) return;
        // 주인이 맞았다면
        if (other.GetComponentInParent<NetworkObject>().InputAuthority == owner) return;
        
        Utility.RefreshToken(ref _cts);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Utility.RefreshToken(ref _cts);
    }

    private async UniTaskVoid DamageLoop(Collider other, CancellationToken token)
    {
        IDamageProcess damageProcess = other.GetComponent<IDamageProcess>();
        
        while (damageProcess != null && other.GetComponent<HeroState>().GetCurrHealth() > 0f)
        {
            damageProcess.OnTakeDamage(2f);
            Debug.Log(other.GetComponent<HeroState>().GetCurrHealth());
            
            if (Runner.IsServer)
            {
                var no = Runner.Spawn(HitVFX, other.transform.position + new Vector3(0, 1, 0), Quaternion.identity);
                if (no != null)
                {
                    HitVFXDestroy(no).Forget();
                }
            }
            await UniTask.Delay(100, cancellationToken:token).SuppressCancellationThrow();
            
            if(token.IsCancellationRequested)
                break;
        }
    }
    
    public async UniTaskVoid HitVFXDestroy(NetworkObject no)
    {
        await UniTask.Delay(1000);
        
        if (this == null || gameObject == null) return;
        Runner.Despawn(no);
    }
    
    
}
