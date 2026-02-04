using System.Threading;
using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

public class Eva_R : NetworkBehaviour
{
    private PlayerRef owner;
    private Eva_Skill ownerSkill;  // VF 라이트 체크용
    private CancellationTokenSource _cts;
    [SerializeField] private GameObject HitVFX;

    private void Awake()
    {
        Utility.RefreshToken(ref _cts);
    }

    public void Init(PlayerRef player, Eva_Skill skill = null)
    {
        owner = player;
        ownerSkill = skill;
    }

    public async UniTaskVoid ActiveInit()
    {
        await UniTask.Delay(200);
        
        if (this == null || gameObject == null) return;
        
        var ps = GetComponent<ParticleSystem>();
        ps.Play();
        
        var bc = GetComponent<BoxCollider>();
        bc.enabled = true;
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
        if (other == null) return;

        IDamageProcess damageProcess = other.GetComponent<IDamageProcess>();
        var targetNO = other.GetComponentInParent<NetworkObject>();
        var heroState = other.GetComponent<HeroState>();

        // null 체크
        if (damageProcess == null || heroState == null) return;

        while (heroState != null && heroState.GetCurrHealth() > 0f)
        {
            // 오브젝트 유효성 체크
            if (this == null || other == null || Runner == null) break;

            damageProcess.OnTakeDamage(2f);

            // VF 라이트 투사체 발사 (매 히트마다)
            if (ownerSkill != null && targetNO != null)
            {
                ownerSkill.TryApplyVFLight(targetNO);
            }

            if (Runner != null && Runner.IsServer && HitVFX != null)
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
