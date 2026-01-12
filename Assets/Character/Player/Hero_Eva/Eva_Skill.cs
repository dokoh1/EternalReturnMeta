using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class Eva_Skill : HeroSkill
{
    private HeroInput heroInput;
    [Networked] public NetworkButtons ButtonsPrevious { get; set; }
    [Networked] private int ButtonsPreviousQ { get; set; }
    [Networked] private int ButtonsPreviousR { get; set; }

    [SerializeField] private GameObject _skillQ;
    [SerializeField] private GameObject _skillR;
    private NetworkObject skillR_Dummy;
    private NetworkObject skillQ_Dummy;
    
    private Vector3 _skillQDir {get; set;}

    private bool IsCasting;
    
    private HeroMovement heroMovement;
    private Eva_AnimationController animationController;

    private bool IsActivating_R{ get; set;}
    private Coroutine Coroutine_R;

    [Networked] private Vector3 skillR_Dir { get; set; }
    private Vector3 Skill_R_MousePosition { get; set; }

    public override void Spawned()
    {
        ButtonsPreviousQ = 0;
        IsCasting = false;
        IsActivating_R = false;
        
        heroMovement = GetComponent<HeroMovement>();
        animationController = GetComponent<Eva_AnimationController>();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (IsActivating_R)
        {
            if (heroMovement.IsDeath)
            {
                Runner.Despawn(skillR_Dummy);
                IsActivating_R = false;
            }
            
            if (skillR_Dummy != null)
            {
                skillR_Dir = (Skill_R_MousePosition - skillR_Dummy.transform.position).normalized;

                // 현재 회전에서 목표 회전으로 천천히 이동
                skillR_Dummy.transform.rotation = Quaternion.Slerp(skillR_Dummy.transform.rotation, Quaternion.LookRotation(skillR_Dir),
                    Runner.DeltaTime * 0.5f // 속도 조정 (5.0f 값을 조절 가능)
                );
                
                heroMovement.GetKcc().SetLookRotation(skillR_Dummy.transform.rotation, true, false);
            }
        }
        
        if (GetInput(out heroInput))
        {
            // =================== 스킬 Q =======================================
            if(heroInput.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillQ))
            {
                if (ButtonsPreviousQ == 0)
                {
                    ButtonsPreviousQ = 1; 
                    Skill_Q(heroInput);
                }
            }
            if (heroInput.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillQ))
            {
                ButtonsPreviousQ = 0;
            }
            
            // =================== 스킬 R =======================================
            if(heroInput.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillR))
            {
                if (ButtonsPreviousR == 0)
                {
                    ButtonsPreviousR = 1;
                    Skill_R(heroInput);
                }
            }
            if (heroInput.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillR))
            {
                ButtonsPreviousR = 0;
            }
        }

        Skill_R_MousePosition = heroInput.MousePosition;
        
        ButtonsPrevious = heroInput.Buttons;
    }
    

    private void Skill_Q(HeroInput _heroInput)
    {
        if (IsCasting) return;
        
        IsCasting = true;
        
        animationController.RPC_Multi_Skill_Q();
        
        _skillQDir = _heroInput.HitPosition_Skill - gameObject.transform.position;
        _skillQDir = new Vector3(_skillQDir.x, 0, _skillQDir.z);
        Quaternion lookRotation = Quaternion.LookRotation(_skillQDir.normalized);
        
        var dir = Quaternion.LookRotation(_skillQDir);

        heroMovement.GetKcc().SetLookRotation(lookRotation, true, false);
        heroMovement.IsCastingSkill = true;
        
        Q_SpawnProcess(_heroInput, dir).Forget();
    }

  

    private async UniTaskVoid Q_SpawnProcess(HeroInput hi, Quaternion dir)
    {
        await UniTask.Delay(300);
        
        skillQ_Dummy = Runner.Spawn(_skillQ, transform.position + new Vector3(0,1,0), Quaternion.LookRotation(_skillQDir));
        skillQ_Dummy.GetComponent<Eva_Q>().Init(hi.Owner);
        RPC_Skill_Q_Activate_Init(skillQ_Dummy);
        
        var nt = skillQ_Dummy.GetComponent<NetworkRigidbody3D>();
        nt.Teleport(transform.position + new Vector3(0,1,0), dir);
        
        await UniTask.Delay(600);
        
        heroMovement.IsCastingSkill = false;
        IsCasting = false;
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_Skill_Q_Activate_Init(NetworkObject no)
    {
        var evaQ = no.GetComponent<Eva_Q>();
        evaQ.ActiveInit().Forget();
    }
    
    protected override void Skill_Q() {}
    protected override void Skill_W() {}
    protected override void Skill_E() {}
    protected override void Skill_R() {}

    private void Skill_R(HeroInput _heroInput)
    {
        if (IsCasting && !IsActivating_R) return;
        
        if (!IsActivating_R)
        {
            IsCasting = true;
            IsActivating_R = true;
            
            _skillQDir = _heroInput.HitPosition_Skill - transform.position;
            _skillQDir = new Vector3(_skillQDir.x, 0, _skillQDir.z);
            Quaternion lookRotation = Quaternion.LookRotation(_skillQDir.normalized);
            
            heroMovement.GetKcc().SetLookRotation(lookRotation, true, false);

            var dir = Quaternion.LookRotation(_skillQDir);
            skillR_Dummy = Runner.Spawn(_skillR, transform.position, dir);
            
            skillR_Dummy.GetComponent<Eva_R>().Init(_heroInput.Owner);
            
            var nt = skillR_Dummy.GetComponent<NetworkTransform>();
            nt.Teleport(transform.position, dir);
            
            RPC_Skill_R_Activate_Init(skillR_Dummy);
            
            animationController.RPC_Multi_Skill_R_Activate_Animation();
            heroMovement.IsCastingSkill = true;
        }
        else
        {
            Runner.Despawn(skillR_Dummy);
            
            IsCasting = false;
            IsActivating_R = false;
            animationController.RPC_Multi_Skill_R_Deactivate_Animation();
            heroMovement.IsCastingSkill = false;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_Skill_R_Activate_Init(NetworkObject no)
    {
        var evaR = no.GetComponent<Eva_R>();
        evaR.ActiveInit().Forget();
    }
}

