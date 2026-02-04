using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class Eva_Skill : HeroSkill
{
    private HeroInput heroInput;
    [Networked] public NetworkButtons ButtonsPrevious { get; set; }
    [Networked] private int ButtonsPreviousQ { get; set; }
    [Networked] private int ButtonsPreviousW { get; set; }
    [Networked] private int ButtonsPreviousE { get; set; }
    [Networked] private int ButtonsPreviousR { get; set; }

    [SerializeField] private GameObject _skillQ;
    [SerializeField] private GameObject _skillW;
    [SerializeField] private GameObject _skillR;
    [SerializeField] private GameObject _vfLightPrefab;  // VF 라이트 프리팹
    private NetworkObject skillR_Dummy;
    private NetworkObject skillQ_Dummy;
    private NetworkObject skillW_Dummy;

    // W 스킬 관련
    private const float SkillW_Range = 5.0f;  // 사거리 5m
    [Networked] private TickTimer SkillW_CooldownTimer { get; set; }
    private const float SkillW_Cooldown = 1.0f;  // 쿨다운 1초

    // E 스킬 관련
    [Networked] private TickTimer SkillE_CooldownTimer { get; set; }
    [Networked] private TickTimer SkillE_DurationTimer { get; set; }  // E 스킬 지속시간 (0.5초)
    [Networked] private TickTimer VFLight_WindowTimer { get; set; }   // VF 라이트 발동 가능 시간 (3초)
    [Networked] private NetworkBool IsUsingSkillE { get; set; }       // E 스킬 사용 중
    [Networked] private NetworkBool IsVFLightReady { get; set; }      // VF 라이트 발동 가능 상태
    private const float SkillE_Cooldown = 1.0f;  // 쿨다운 1초
    private const float SkillE_Duration = 1.0f;   // 지속시간 1초 (애니메이션 동안 다른 스킬 사용 불가)
    private const float VFLight_Window = 3.0f;    // VF 라이트 발동 가능 시간 3초
    private const float VFLight_Damage = 20f;     // VF 라이트 추가 데미지
    private float[] SkillE_SpeedBonus = { 1.2f, 1.25f, 1.3f, 1.35f, 1.4f };  // 레벨별 이동속도
    private int SkillE_Level = 0;  // E 스킬 레벨 (0~4)
    private float originalSpeedMultiplier = 1.0f;  // E 사용 전 원래 속도 배율
    
    private Vector3 _skillQDir {get; set;}

    private bool IsCasting;
    
    private HeroMovement heroMovement;
    private Eva_AnimationController animationController;

    private bool IsActivating_R{ get; set;}
    private Coroutine Coroutine_R;

    [Networked] private Vector3 skillR_Dir { get; set; }
    private Vector3 Skill_R_MousePosition { get; set; }

    // 기본 공격 관련 변수
    private const float BasicAttackRange = 30.0f;  // 사거리
    private const float BasicAttackDamage = 5.0f;
    private const float BasicAttackCooldown = 0.5f;
    [Networked] private TickTimer BasicAttackCooldownTimer { get; set; }
    [Networked] private NetworkId CurrentTargetId { get; set; }
    [Networked] private NetworkBool IsInAttackRange { get; set; }  // 사거리 내 여부 (캔슬 판정용)

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

        // E 스킬 지속시간 체크
        if (IsUsingSkillE && SkillE_DurationTimer.Expired(Runner))
        {
            EndSkillE();
        }

        // VF 라이트 윈도우 체크
        if (IsVFLightReady && VFLight_WindowTimer.Expired(Runner))
        {
            IsVFLightReady = false;
        }

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
                    Runner.DeltaTime * 3.0f // 속도 조정 (6배 빠르게)
                );
                
                heroMovement.GetKcc().SetLookRotation(skillR_Dummy.transform.rotation, true, false);
            }
        }
        
        if (GetInput(out heroInput))
        {
            // 에어본 상태면 모든 조작 불가
            if (heroMovement.IsAirborne)
            {
                ButtonsPrevious = heroInput.Buttons;
                return;
            }

            // =================== 스킬 Q =======================================
            if(heroInput.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillQ))
            {
                if (ButtonsPreviousQ == 0 && !IsUsingSkillE)  // E 스킬 중에는 사용 불가
                {
                    ButtonsPreviousQ = 1;

                    // Q 스킬 사용 시 기본 공격 캔슬
                    CancelBasicAttack();

                    Skill_Q(heroInput);
                }
            }
            if (heroInput.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillQ))
            {
                ButtonsPreviousQ = 0;
            }

            // =================== 스킬 W =======================================
            if (heroInput.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillW))
            {
                if (ButtonsPreviousW == 0 && !IsUsingSkillE)  // E 스킬 중에는 사용 불가
                {
                    ButtonsPreviousW = 1;

                    // W 스킬 사용 시 기본 공격 캔슬
                    CancelBasicAttack();

                    Skill_W(heroInput);
                }
            }
            if (heroInput.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillW))
            {
                ButtonsPreviousW = 0;
            }

            // =================== 스킬 E =======================================
            if (heroInput.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillE))
            {
                if (ButtonsPreviousE == 0)
                {
                    ButtonsPreviousE = 1;

                    // E 스킬 사용 시 기본 공격 캔슬
                    CancelBasicAttack();

                    Skill_E();
                }
            }
            if (heroInput.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillE))
            {
                ButtonsPreviousE = 0;
            }

            // =================== 스킬 R =======================================
            if(heroInput.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillR))
            {
                if (ButtonsPreviousR == 0 && !IsUsingSkillE)  // E 스킬 중에는 사용 불가
                {
                    ButtonsPreviousR = 1;

                    // R 스킬 사용 시 기본 공격 캔슬
                    CancelBasicAttack();

                    Skill_R(heroInput);
                }
            }
            if (heroInput.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillR))
            {
                ButtonsPreviousR = 0;
            }

            // =================== 기본 공격 =======================================
            // E 스킬 중에는 기본 공격 불가 (이동만 가능)
            if (!IsUsingSkillE)
            {
                if (heroInput.Buttons.WasPressed(ButtonsPrevious, InputButton.RightClick))
                {
                    if (heroInput.TargetNetworkId.IsValid)
                    {
                        // 적 클릭: 타겟 설정
                        CurrentTargetId = heroInput.TargetNetworkId;
                        IsInAttackRange = false;
                    }
                    else
                    {
                        // 땅 클릭: 타겟 해제 + 공격 캔슬
                        CancelBasicAttack();
                    }
                }

                // 타겟이 있으면 기본 공격 처리
                if (CurrentTargetId.IsValid)
                {
                    ProcessBasicAttack();
                }
            }
        }

        Skill_R_MousePosition = heroInput.MousePosition;
        
        ButtonsPrevious = heroInput.Buttons;
    }
    

    private void Skill_Q(HeroInput _heroInput)
    {
        if (IsCasting) return;

        IsCasting = true;
        heroMovement.IsCastingSkill = true;  // 스킬 사용 중 이동 정지
        heroMovement.SetMovePosition(transform.position);  // 현재 위치에서 멈춤

        animationController.RPC_Multi_Skill_Q();

        _skillQDir = _heroInput.HitPosition_Skill - gameObject.transform.position;
        _skillQDir = new Vector3(_skillQDir.x, 0, _skillQDir.z);
        Quaternion lookRotation = Quaternion.LookRotation(_skillQDir.normalized);

        var dir = Quaternion.LookRotation(_skillQDir);

        heroMovement.GetKcc().SetLookRotation(lookRotation, true, false);

        Q_SpawnProcess(_heroInput, dir).Forget();
    }

  

    private async UniTaskVoid Q_SpawnProcess(HeroInput hi, Quaternion dir)
    {
        await UniTask.Delay(100);  // 시전 딜레이

        skillQ_Dummy = Runner.Spawn(_skillQ, transform.position + new Vector3(0,1,0), Quaternion.LookRotation(_skillQDir));
        skillQ_Dummy.GetComponent<Eva_Q>().Init(hi.Owner, this);  // VF 라이트용 참조 전달
        RPC_Skill_Q_Activate_Init(skillQ_Dummy);

        var nt = skillQ_Dummy.GetComponent<NetworkRigidbody3D>();
        nt.Teleport(transform.position + new Vector3(0,1,0), dir);

        await UniTask.Delay(50);  // 후딜레이 (줄임)

        IsCasting = false;
        heroMovement.IsCastingSkill = false;
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_Skill_Q_Activate_Init(NetworkObject no)
    {
        var evaQ = no.GetComponent<Eva_Q>();
        evaQ.ActiveInit().Forget();
    }

    private void Skill_W(HeroInput _heroInput)
    {
        // 쿨다운 체크
        if (!SkillW_CooldownTimer.ExpiredOrNotRunning(Runner)) return;
        if (IsCasting) return;

        IsCasting = true;
        heroMovement.IsCastingSkill = true;  // 스킬 사용 중 이동 정지
        heroMovement.SetMovePosition(transform.position);  // 현재 위치에서 멈춤

        animationController.RPC_Multi_Skill_W();

        // 마우스 방향으로 회전
        Vector3 skillDir = _heroInput.HitPosition_Skill - transform.position;
        skillDir = new Vector3(skillDir.x, 0, skillDir.z);

        if (skillDir != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(skillDir.normalized);
            heroMovement.GetKcc().SetLookRotation(lookRotation, true, false);
        }

        // 스킬 위치 계산 (사거리 내로 제한)
        Vector3 spawnPos = _heroInput.HitPosition_Skill;
        float distance = Vector3.Distance(transform.position, spawnPos);
        if (distance > SkillW_Range)
        {
            // 사거리 밖이면 최대 사거리 지점에 생성
            spawnPos = transform.position + skillDir.normalized * SkillW_Range;
        }
        spawnPos.y = 0;  // 바닥 높이로

        W_SpawnProcess(_heroInput, spawnPos).Forget();
    }

    private async UniTaskVoid W_SpawnProcess(HeroInput hi, Vector3 spawnPos)
    {
        await UniTask.Delay(50);  // 시전 딜레이 (줄임)

        skillW_Dummy = Runner.Spawn(_skillW, spawnPos, Quaternion.identity);
        skillW_Dummy.GetComponent<Eva_W>().Init(hi.Owner, this);  // VF 라이트용 참조 전달
        RPC_Skill_W_Activate_Init(skillW_Dummy);

        // 쿨다운 시작
        SkillW_CooldownTimer = TickTimer.CreateFromSeconds(Runner, SkillW_Cooldown);

        await UniTask.Delay(50);  // 후딜레이 (줄임)

        IsCasting = false;
        heroMovement.IsCastingSkill = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_Skill_W_Activate_Init(NetworkObject no)
    {
        var evaW = no.GetComponent<Eva_W>();
        evaW.ActiveInit().Forget();
    }

    // =================== E 스킬 =======================================
    protected override void Skill_E()
    {
        // 쿨다운 체크
        if (!SkillE_CooldownTimer.ExpiredOrNotRunning(Runner)) return;
        if (IsCasting) return;
        if (IsUsingSkillE) return;

        // E 스킬 시작 - 플래그들 먼저 설정
        IsUsingSkillE = true;
        IsVFLightReady = true;
        heroMovement.IsPlayingSkillAnimation = true;  // 이동 애니메이션 차단 (먼저 설정)
        heroMovement.IsSlowImmune = true;

        // 기본 공격 캔슬
        CancelBasicAttack();

        // 원래 속도 저장 후 이동속도 증가
        originalSpeedMultiplier = heroMovement.SpeedMultiplier;
        heroMovement.SpeedMultiplier = SkillE_SpeedBonus[SkillE_Level];

        // 타이머 시작 (쿨타임은 E 종료 후 시작)
        SkillE_DurationTimer = TickTimer.CreateFromSeconds(Runner, SkillE_Duration);
        VFLight_WindowTimer = TickTimer.CreateFromSeconds(Runner, VFLight_Window);

        // E 스킬 애니메이션
        animationController.RPC_Multi_Skill_E();
    }

    private void EndSkillE()
    {

        IsUsingSkillE = false;

        // 이동속도 복구
        heroMovement.SpeedMultiplier = 1.0f;

        // 슬로우 면역 해제
        heroMovement.IsSlowImmune = false;

        // 이동 애니메이션 차단 해제
        heroMovement.IsPlayingSkillAnimation = false;

        // 쿨타임 시작 (E 종료 후부터 쿨타임 계산)
        SkillE_CooldownTimer = TickTimer.CreateFromSeconds(Runner, SkillE_Cooldown);

        // 애니메이션 종료
        animationController.RPC_Multi_Skill_E_End();
    }

    // VF 라이트 투사체 발사 (스킬 히트 시 호출)
    public void TryApplyVFLight(NetworkObject target)
    {
        if (!IsVFLightReady) return;
        if (target == null) return;
        if (_vfLightPrefab == null) return;

        // VF 라이트 투사체 스폰 (내 위치에서 타겟으로)
        Vector3 spawnPos = transform.position + Vector3.up;
        var vfLight = Runner.Spawn(_vfLightPrefab, spawnPos, Quaternion.identity);
        if (vfLight != null)
        {
            var vfLightScript = vfLight.GetComponent<Eva_VFLight>();
            if (vfLightScript != null)
            {
                vfLightScript.Init(spawnPos, target);
            }
        }
    }

    protected override void Skill_Q() {}
    protected override void Skill_W() {}
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
            
            skillR_Dummy.GetComponent<Eva_R>().Init(_heroInput.Owner, this);  // VF 라이트용 참조 전달
            
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

    // 기본 공격 캔슬 헬퍼 메서드
    private void CancelBasicAttack()
    {
        if (CurrentTargetId.IsValid)
        {
            if (IsInAttackRange)
            {
                animationController.RPC_Multi_CancelBasicAttack();
            }
            CurrentTargetId = default;
            IsInAttackRange = false;
            heroMovement.IsAttacking = false;  // 이동 가능하게
        }
    }

    private void ProcessBasicAttack()
    {
        // 타겟 오브젝트 찾기
        if (!Runner.TryFindObject(CurrentTargetId, out NetworkObject targetObject))
        {
            CurrentTargetId = default;
            IsInAttackRange = false;
            heroMovement.IsAttacking = false;
            return;
        }

        // 타겟이 죽었는지 확인
        var targetState = targetObject.GetComponentInChildren<HeroState>();
        if (targetState == null || targetState.GetCurrHealth() <= 0f)
        {
            CurrentTargetId = default;
            IsInAttackRange = false;
            heroMovement.IsAttacking = false;
            return;
        }

        // 실제 캐릭터 위치 가져오기 (KCC 또는 HeroMovement 위치 사용)
        var targetMovement = targetObject.GetComponentInChildren<HeroMovement>();
        Vector3 targetPosition = targetMovement != null
            ? targetMovement.GetKcc().Position
            : targetObject.transform.position;
        Vector3 myPosition = transform.position;
        float distance = Vector3.Distance(myPosition, targetPosition);

        // 사거리 밖이면 타겟 방향으로 이동
        if (distance > BasicAttackRange)
        {
            // 사거리 안 → 밖으로 나갔을 때만 캔슬 애니메이션
            if (IsInAttackRange)
            {
                animationController.RPC_Multi_CancelBasicAttack();
                IsInAttackRange = false;
            }

            // 이동 가능하게 설정
            heroMovement.IsAttacking = false;

            // 타겟 방향으로 추적 이동
            heroMovement.SetMovePosition(targetPosition);
            return;
        }

        // 사거리 안: 멈추고 공격
        IsInAttackRange = true;
        heroMovement.IsAttacking = true;  // 이동 정지

        // 쿨다운 완료 시 공격
        if (BasicAttackCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            // 타겟 방향으로 회전
            Vector3 dir = (targetPosition - myPosition).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(dir);
                heroMovement.GetKcc().SetLookRotation(lookRotation, true, false);
            }

            // 공격 애니메이션
            animationController.RPC_Multi_BasicAttack();

            // 데미지 적용
            var damageProcess = targetObject.GetComponentInChildren<IDamageProcess>();
            if (damageProcess != null)
            {
                damageProcess.OnTakeDamage(BasicAttackDamage);
            }

            // 쿨다운 시작
            BasicAttackCooldownTimer = TickTimer.CreateFromSeconds(Runner, BasicAttackCooldown);
        }
    }
}

