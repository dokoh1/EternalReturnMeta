using Character.Player;
using Character.Player.ControlSettings;  // Phase 1, 2에서 만든 시스템
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

    // ═══════════════════════════════════════════════════════════════
    // Phase 2: Input Buffering 시스템
    // ═══════════════════════════════════════════════════════════════
    [Header("=== Control Settings (Phase 2) ===")]
    [Tooltip("조작감 설정 (Project 창에서 생성한 ScriptableObject 할당)")]
    [SerializeField] private ControlSettingsConfig _controlConfig;

    // 입력 버퍼 (스킬 시전 중 입력을 저장)
    private InputBuffer _inputBuffer;

    // ═══════════════════════════════════════════════════════════════
    // Phase 4: Animation Canceling 시스템
    // ═══════════════════════════════════════════════════════════════
    [Header("=== Skill Cancel Data (Phase 4) ===")]
    [Tooltip("스킬 캔슬 타이밍 데이터 (Project 창에서 생성한 ScriptableObject 할당)")]
    [SerializeField] private SkillCancelData _skillCancelData;
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

        // ═══════════════════════════════════════════════════════════════
        // Phase 2: Input Buffer 초기화
        // _controlConfig가 Inspector에서 할당되어 있어야 작동함
        // ═══════════════════════════════════════════════════════════════
        if (_controlConfig != null)
        {
            _inputBuffer = new InputBuffer(_controlConfig);
            Debug.Log("[Eva_Skill] Input Buffer 초기화 완료");
        }
        else
        {
            Debug.LogWarning("[Eva_Skill] ControlSettingsConfig가 할당되지 않음! Input Buffering 비활성화");
        }
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

            // ═══════════════════════════════════════════════════════════════
            // R 스킬 토글 처리 (특수 케이스)
            // R은 활성화 상태에서 다시 R 누르면 비활성화되는 토글 방식
            // 버퍼링과 상관없이 항상 즉시 처리해야 함
            // ═══════════════════════════════════════════════════════════════
            if (IsActivating_R)
            {
                if (heroInput.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillR))
                {
                    if (ButtonsPreviousR == 0)
                    {
                        ButtonsPreviousR = 1;
                        Skill_R(heroInput);  // R 끄기
                    }
                }
                if (heroInput.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillR))
                    ButtonsPreviousR = 0;
            }

            // ═══════════════════════════════════════════════════════════════
            // Phase 2: Input Buffering 처리
            //
            // 핵심 로직:
            // 1. 스킬 시전 중(IsCasting) 또는 E스킬 중 → 입력을 버퍼에 저장
            // 2. 스킬 시전 완료 → 버퍼에서 입력 꺼내서 실행
            // 3. 스킬 시전 중 아니면 → 즉시 실행 (기존 동작)
            // ═══════════════════════════════════════════════════════════════

            // 스킬 시전 중이면 → 입력을 버퍼에 저장 (R 토글은 위에서 이미 처리됨)
            if (IsCasting || IsUsingSkillE)
            {
                BufferSkillInputs(heroInput);
            }
            else
            {
                // 스킬 시전 완료 → 버퍼에서 입력 꺼내서 실행
                ProcessBufferedInputs();

                // 현재 프레임 입력 처리 (기존 로직)
                ProcessDirectSkillInputs(heroInput);
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
        // ═══════════════════════════════════════════════════════════════
        // Phase 4: 스킬 캔슬 데이터 가져오기
        // ═══════════════════════════════════════════════════════════════
        var cancelInfo = _skillCancelData?.GetSkillInfo("Q");

        // 캔슬 데이터가 없으면 기본값 사용
        int damagePointMs = cancelInfo?.DamagePointMs ?? 100;
        int waitAfterDamageMs = cancelInfo?.WaitAfterDamageMs ?? 20;
        int skippableDelayMs = cancelInfo?.SkippableDelayMs ?? 30;

        // [1] 시전 딜레이 (데미지 적용 시점까지)
        await UniTask.Delay(damagePointMs);

        // [2] 데미지 적용 (투사체 발사)
        skillQ_Dummy = Runner.Spawn(_skillQ, transform.position + new Vector3(0,1,0), Quaternion.LookRotation(_skillQDir));
        skillQ_Dummy.GetComponent<Eva_Q>().Init(hi.Owner, this);
        RPC_Skill_Q_Activate_Init(skillQ_Dummy);

        var nt = skillQ_Dummy.GetComponent<NetworkRigidbody3D>();
        nt.Teleport(transform.position + new Vector3(0,1,0), dir);

        // ═══════════════════════════════════════════════════════════════
        // Phase 4: Animation Canceling 로직
        //
        // 캔슬 조건:
        // 1. EnableAnimationCanceling이 ON
        // 2. InputBuffer에 다음 스킬이 대기 중
        //
        // 캔슬 시: 후딜레이(skippableDelayMs)를 스킵하고 바로 종료
        // ═══════════════════════════════════════════════════════════════
        bool canCancel = _controlConfig != null && _controlConfig.EnableAnimationCanceling;

        if (canCancel && _inputBuffer != null)
        {
            // [3] 캔슬 가능 시점까지 대기
            await UniTask.Delay(waitAfterDamageMs);

            // [4] 버퍼에 다음 스킬이 있으면 캔슬!
            if (_inputBuffer.HasPendingInput())
            {
                // 메트릭 기록: Q 스킬 캔슬됨, XX ms 절약
                ControlMetricsCollector.Instance?.RecordCancel(
                    "Q",
                    cancelPoint: (float)(damagePointMs + waitAfterDamageMs) / (damagePointMs + waitAfterDamageMs + skippableDelayMs),
                    timeSavedMs: skippableDelayMs
                );

                Debug.Log($"[AnimCancel] Q 스킬 캔슬! {skippableDelayMs}ms 절약");

                // ═══════════════════════════════════════════════════════════════
                // 동기화: 모든 클라이언트에게 "캔슬됐다" 알림
                //
                // 이게 없으면?
                // - 서버: Q 애니메이션 120ms에 끊김 → W 시작
                // - 클라이언트: Q 애니메이션 150ms까지 재생 → 30ms 늦게 W 시작
                // - 결과: 상대방 화면에서 내 캐릭터 타이밍이 이상함
                // ═══════════════════════════════════════════════════════════════
                animationController.RPC_Multi_CancelSkillAnimation();

                // 후딜레이 스킵하고 바로 종료
                IsCasting = false;
                heroMovement.IsCastingSkill = false;
                return;
            }

            // 캔슬 안 됨 → 남은 후딜레이 진행
            await UniTask.Delay(skippableDelayMs);
        }
        else
        {
            // 캔슬 비활성화 → 기존처럼 전체 후딜레이
            await UniTask.Delay(waitAfterDamageMs + skippableDelayMs);
        }

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
        // ═══════════════════════════════════════════════════════════════
        // Phase 4: 스킬 캔슬 데이터 가져오기
        // ═══════════════════════════════════════════════════════════════
        var cancelInfo = _skillCancelData?.GetSkillInfo("W");

        // 캔슬 데이터가 없으면 기본값 사용
        int damagePointMs = cancelInfo?.DamagePointMs ?? 50;
        int waitAfterDamageMs = cancelInfo?.WaitAfterDamageMs ?? 20;
        int skippableDelayMs = cancelInfo?.SkippableDelayMs ?? 30;

        // [1] 시전 딜레이 (데미지 적용 시점까지)
        await UniTask.Delay(damagePointMs);

        // [2] 데미지 적용 (범위 스킬 생성)
        skillW_Dummy = Runner.Spawn(_skillW, spawnPos, Quaternion.identity);
        skillW_Dummy.GetComponent<Eva_W>().Init(hi.Owner, this);  // VF 라이트용 참조 전달
        RPC_Skill_W_Activate_Init(skillW_Dummy);

        // 쿨다운 시작
        SkillW_CooldownTimer = TickTimer.CreateFromSeconds(Runner, SkillW_Cooldown);

        // ═══════════════════════════════════════════════════════════════
        // Phase 4: Animation Canceling 로직
        //
        // 캔슬 조건:
        // 1. EnableAnimationCanceling이 ON
        // 2. InputBuffer에 다음 스킬이 대기 중
        //
        // 캔슬 시: 후딜레이(skippableDelayMs)를 스킵하고 바로 종료
        // ═══════════════════════════════════════════════════════════════
        bool canCancel = _controlConfig != null && _controlConfig.EnableAnimationCanceling;

        if (canCancel && _inputBuffer != null)
        {
            // [3] 캔슬 가능 시점까지 대기
            await UniTask.Delay(waitAfterDamageMs);

            // [4] 버퍼에 다음 스킬이 있으면 캔슬!
            if (_inputBuffer.HasPendingInput())
            {
                // 메트릭 기록: W 스킬 캔슬됨, XX ms 절약
                ControlMetricsCollector.Instance?.RecordCancel(
                    "W",
                    cancelPoint: (float)(damagePointMs + waitAfterDamageMs) / (damagePointMs + waitAfterDamageMs + skippableDelayMs),
                    timeSavedMs: skippableDelayMs
                );

                Debug.Log($"[AnimCancel] W 스킬 캔슬! {skippableDelayMs}ms 절약");

                // ═══════════════════════════════════════════════════════════════
                // 동기화: 모든 클라이언트에게 "캔슬됐다" 알림
                // ═══════════════════════════════════════════════════════════════
                animationController.RPC_Multi_CancelSkillAnimation();

                // 후딜레이 스킵하고 바로 종료
                IsCasting = false;
                heroMovement.IsCastingSkill = false;
                return;
            }

            // 캔슬 안 됨 → 남은 후딜레이 진행
            await UniTask.Delay(skippableDelayMs);
        }
        else
        {
            // 캔슬 비활성화 → 기존처럼 전체 후딜레이
            await UniTask.Delay(waitAfterDamageMs + skippableDelayMs);
        }

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
    // 참고: 이 메서드는 Eva_Q, Eva_W 등에서 호출됨
    public void TryApplyVFLight(NetworkObject target)
    {
        if (!IsVFLightReady) return;
        if (target == null) return;
        if (_vfLightPrefab == null) return;
        if (Runner == null || !Runner.IsServer) return;  // 서버(Host)에서만 스폰

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

    // ═══════════════════════════════════════════════════════════════════════════
    // Phase 2: Input Buffering 메서드들
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 스킬 시전 중일 때 호출 - 입력을 버퍼에 저장
    ///
    /// 작동 예시:
    /// Q 시전 중 → 플레이어가 W 누름 → W가 버퍼에 저장됨
    /// </summary>
    private void BufferSkillInputs(HeroInput input)
    {
        // 버퍼가 없거나 비활성화면 무시
        if (_inputBuffer == null) return;

        // Q 입력 버퍼링
        if (input.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillQ))
        {
            if (ButtonsPreviousQ == 0 && !IsUsingSkillE)
            {
                _inputBuffer.AddInput(InputButton.SkillQ, input.HitPosition_Skill, input.MousePosition);
                ButtonsPreviousQ = 1;
            }
        }
        if (input.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillQ))
            ButtonsPreviousQ = 0;

        // W 입력 버퍼링
        if (input.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillW))
        {
            if (ButtonsPreviousW == 0 && !IsUsingSkillE)
            {
                _inputBuffer.AddInput(InputButton.SkillW, input.HitPosition_Skill, input.MousePosition);
                ButtonsPreviousW = 1;
            }
        }
        if (input.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillW))
            ButtonsPreviousW = 0;

        // E 입력은 E 사용 중이 아닐 때만 버퍼링 (E 중복 방지)
        if (input.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillE))
        {
            if (ButtonsPreviousE == 0 && !IsUsingSkillE)
            {
                _inputBuffer.AddInput(InputButton.SkillE, input.HitPosition_Skill, input.MousePosition);
                ButtonsPreviousE = 1;
            }
        }
        if (input.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillE))
            ButtonsPreviousE = 0;

        // R 입력 버퍼링 (단, R이 이미 활성화된 상태면 버퍼링 안 함 - 토글은 위에서 처리)
        if (!IsActivating_R)
        {
            if (input.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillR))
            {
                if (ButtonsPreviousR == 0 && !IsUsingSkillE)
                {
                    _inputBuffer.AddInput(InputButton.SkillR, input.HitPosition_Skill, input.MousePosition);
                    ButtonsPreviousR = 1;
                }
            }
            if (input.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillR))
                ButtonsPreviousR = 0;
        }
    }

    /// <summary>
    /// 스킬 시전 완료 후 호출 - 버퍼에서 입력 꺼내서 실행
    ///
    /// 작동 예시:
    /// Q 시전 완료 → 버퍼에 W가 있음 → W 자동 실행
    /// </summary>
    private void ProcessBufferedInputs()
    {
        // 버퍼가 없으면 무시
        if (_inputBuffer == null) return;

        // 버퍼에서 다음 입력 가져오기
        if (_inputBuffer.TryGetNextInput(out BufferedInput bufferedInput))
        {
            // 버퍼에서 꺼낸 입력으로 스킬 실행
            ExecuteBufferedSkill(bufferedInput);
        }
    }

    /// <summary>
    /// 버퍼에서 꺼낸 스킬 실행
    /// </summary>
    private void ExecuteBufferedSkill(BufferedInput bufferedInput)
    {
        // E 스킬 중에는 Q, W, R 사용 불가
        if (IsUsingSkillE && bufferedInput.Button != InputButton.SkillE)
            return;

        // 버퍼에 저장된 마우스 위치로 임시 HeroInput 생성
        var tempInput = new HeroInput
        {
            HitPosition_Skill = bufferedInput.HitPosition,
            MousePosition = bufferedInput.MousePosition,
            Owner = heroInput.Owner
        };

        // 스킬 실행
        switch (bufferedInput.Button)
        {
            case InputButton.SkillQ:
                CancelBasicAttack();
                Skill_Q(tempInput);
                break;

            case InputButton.SkillW:
                CancelBasicAttack();
                Skill_W(tempInput);
                break;

            case InputButton.SkillE:
                CancelBasicAttack();
                Skill_E();
                break;

            case InputButton.SkillR:
                CancelBasicAttack();
                Skill_R(tempInput);
                break;
        }
    }

    /// <summary>
    /// 스킬 시전 중이 아닐 때 호출 - 즉시 스킬 실행 (기존 동작)
    ///
    /// 이 메서드는 기존 코드와 동일하지만,
    /// 메트릭 기록 (wasBuffered: false)이 추가됨
    /// </summary>
    private void ProcessDirectSkillInputs(HeroInput input)
    {
        // =================== 스킬 Q =======================================
        if (input.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillQ))
        {
            if (ButtonsPreviousQ == 0 && !IsUsingSkillE)
            {
                ButtonsPreviousQ = 1;
                CancelBasicAttack();

                // 메트릭: 직접 실행 (버퍼 안 거침)
                ControlMetricsCollector.Instance?.RecordInput("Q", wasBuffered: false, 0f);

                Skill_Q(input);
            }
        }
        if (input.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillQ))
            ButtonsPreviousQ = 0;

        // =================== 스킬 W =======================================
        if (input.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillW))
        {
            if (ButtonsPreviousW == 0 && !IsUsingSkillE)
            {
                ButtonsPreviousW = 1;
                CancelBasicAttack();

                // 메트릭: 직접 실행
                ControlMetricsCollector.Instance?.RecordInput("W", wasBuffered: false, 0f);

                Skill_W(input);
            }
        }
        if (input.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillW))
            ButtonsPreviousW = 0;

        // =================== 스킬 E =======================================
        if (input.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillE))
        {
            if (ButtonsPreviousE == 0)
            {
                ButtonsPreviousE = 1;
                CancelBasicAttack();

                // 메트릭: 직접 실행
                ControlMetricsCollector.Instance?.RecordInput("E", wasBuffered: false, 0f);

                Skill_E();
            }
        }
        if (input.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillE))
            ButtonsPreviousE = 0;

        // =================== 스킬 R =======================================
        if (input.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillR))
        {
            if (ButtonsPreviousR == 0 && !IsUsingSkillE)
            {
                ButtonsPreviousR = 1;
                CancelBasicAttack();

                // 메트릭: 직접 실행
                ControlMetricsCollector.Instance?.RecordInput("R", wasBuffered: false, 0f);

                Skill_R(input);
            }
        }
        if (input.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillR))
            ButtonsPreviousR = 0;
    }
}
