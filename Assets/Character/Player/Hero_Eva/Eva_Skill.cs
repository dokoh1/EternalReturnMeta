using Character.Player;
using Character.Player.ControlSettings;
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
    [SerializeField] private GameObject _vfLightPrefab;

    [Header("=== Control Settings (Phase 2) ===")]
    [Tooltip("조작감 설정 (Project 창에서 생성한 ScriptableObject 할당)")]
    [SerializeField] private ControlSettingsConfig _controlConfig;

    private InputBuffer _inputBuffer;

    [Header("=== Skill Cancel Data (Phase 4) ===")]
    [Tooltip("스킬 캔슬 타이밍 데이터 (Project 창에서 생성한 ScriptableObject 할당)")]
    [SerializeField] private SkillCancelData _skillCancelData;

    private NetworkObject _spawnedSkillR;
    private NetworkObject _spawnedSkillQ;
    private NetworkObject _spawnedSkillW;

    private const float SkillW_Range = 5.0f;

    [Networked] private TickTimer SkillW_CooldownTimer { get; set; }
    private const float SkillW_Cooldown = 1.0f;

    [Networked] private TickTimer SkillE_CooldownTimer { get; set; }
    [Networked] private TickTimer SkillE_DurationTimer { get; set; }
    [Networked] private TickTimer VFLight_WindowTimer { get; set; }

    [Networked] private NetworkBool IsUsingSkillE { get; set; }
    [Networked] private NetworkBool IsVFLightReady { get; set; }

    private const float SkillE_Cooldown = 1.0f;
    private const float SkillE_Duration = 1.0f;
    private const float VFLight_Window = 3.0f;
    private const float VFLight_Damage = 20f;

    private float[] SkillE_SpeedBonus = { 1.2f, 1.25f, 1.3f, 1.35f, 1.4f };
    private int SkillE_Level = 0;
    private float originalSpeedMultiplier = 1.0f;

    private Vector3 _skillDirection {get; set;}

    [Networked] private NetworkBool IsCasting { get; set; }

    private HeroMovement heroMovement;
    private Eva_AnimationController animationController;

    [Networked] private NetworkBool _isRActive { get; set; }

    [Networked] private Vector3 skillR_Dir { get; set; }

    private Vector3 Skill_R_MousePosition { get; set; }

    private const float BasicAttackRange = 30.0f;
    private const float BasicAttackDamage = 5.0f;
    private const float BasicAttackCooldown = 0.5f;

    [Networked] private TickTimer BasicAttackCooldownTimer { get; set; }
    [Networked] private NetworkId CurrentTargetId { get; set; }
    [Networked] private NetworkBool IsInAttackRange { get; set; }

    public override void Spawned()
    {
        ButtonsPreviousQ = 0;
        IsCasting = false;
        _isRActive = false;

        heroMovement = GetComponent<HeroMovement>();
        animationController = GetComponent<Eva_AnimationController>();

        if (HasInputAuthority && !HasStateAuthority)
        {
            animationController.IsLocalFeedbackActive = IsLocalFeedbackEnabled();
        }

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

    private NetworkButtons _localButtonsPrevious;

    public override void FixedUpdateNetwork()
    {
        if (HasInputAuthority && !HasStateAuthority && IsLocalFeedbackEnabled()
            && !Runner.IsResimulation)
        {
            if (GetInput(out HeroInput localInput))
            {
                PredictLocalAnimations(localInput);
            }
        }

        if (!HasStateAuthority)
            return;

        if (IsUsingSkillE && SkillE_DurationTimer.Expired(Runner))
        {
            EndSkillE();
        }

        if (IsVFLightReady && VFLight_WindowTimer.Expired(Runner))
        {
            IsVFLightReady = false;
        }

        if (_isRActive)
        {
            if (heroMovement.IsDeath)
            {
                Runner.Despawn(_spawnedSkillR);
                _isRActive = false;
            }

            if (_spawnedSkillR != null)
            {
                skillR_Dir = (Skill_R_MousePosition - _spawnedSkillR.transform.position).normalized;

                _spawnedSkillR.transform.rotation = Quaternion.Slerp(_spawnedSkillR.transform.rotation, Quaternion.LookRotation(skillR_Dir),
                    Runner.DeltaTime * 3.0f
                );

                heroMovement.SetLookRotationFromQuaternion(_spawnedSkillR.transform.rotation);
            }
        }

        if (GetInput(out heroInput))
        {
            if (heroMovement.IsAirborne)
            {
                ButtonsPrevious = heroInput.Buttons;
                return;
            }

            if (_isRActive)
            {
                if (heroInput.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillR))
                {
                    if (ButtonsPreviousR == 0)
                    {
                        ButtonsPreviousR = 1;
                        Skill_R(heroInput);
                    }
                }
                if (heroInput.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillR))
                    ButtonsPreviousR = 0;
            }

            if (IsCasting || IsUsingSkillE)
            {
                BufferSkillInputs(heroInput);
            }
            else
            {
                ProcessBufferedInputs();
                ProcessDirectSkillInputs(heroInput);
            }

            if (!IsUsingSkillE)
            {
                if (heroInput.Buttons.WasPressed(ButtonsPrevious, InputButton.RightClick))
                {
                    if (heroInput.TargetNetworkId.IsValid)
                    {
                        CurrentTargetId = heroInput.TargetNetworkId;
                        IsInAttackRange = false;
                    }
                    else
                    {
                        CancelBasicAttack();
                    }
                }

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
        if (IsCasting)
            return;

        IsCasting = true;
        heroMovement.IsCastingSkill = true;
        heroMovement.SetMovePosition(transform.position);

        animationController.RPC_Multi_Skill_Q();

        _skillDirection = _heroInput.HitPosition_Skill - gameObject.transform.position;
        _skillDirection = new Vector3(_skillDirection.x, 0, _skillDirection.z);

        var dir = Quaternion.LookRotation(_skillDirection);

        heroMovement.SetLookDirection(_skillDirection.normalized);

        Q_SpawnProcess(_heroInput, dir).Forget();
    }

    private async UniTaskVoid Q_SpawnProcess(HeroInput hi, Quaternion dir)
    {
        var cancelInfo = _skillCancelData?.GetSkillInfo("Q");

        int damagePointMs = cancelInfo?.DamagePointMs ?? 100;
        int waitAfterDamageMs = cancelInfo?.WaitAfterDamageMs ?? 20;
        int skippableDelayMs = cancelInfo?.SkippableDelayMs ?? 30;

        await UniTask.Delay(damagePointMs);

        _spawnedSkillQ = Runner.Spawn(_skillQ, transform.position + new Vector3(0,1,0), Quaternion.LookRotation(_skillDirection));
        _spawnedSkillQ.GetComponent<Eva_Q>().Init(hi.Owner, this);

        RPC_Skill_Q_Activate_Init(_spawnedSkillQ);

        var nt = _spawnedSkillQ.GetComponent<NetworkRigidbody3D>();
        nt.Teleport(transform.position + new Vector3(0,1,0), dir);

        await TryAnimationCancel("Q", damagePointMs, waitAfterDamageMs, skippableDelayMs);
    }

    private async UniTask<bool> TryAnimationCancel(
        string skillName, int damagePointMs, int waitAfterDamageMs, int skippableDelayMs)
    {
        bool canCancel = _controlConfig != null && _controlConfig.EnableAnimationCanceling;

        if (canCancel && _inputBuffer != null)
        {
            await UniTask.Delay(waitAfterDamageMs);

            if (_inputBuffer.HasPendingInput())
            {
                ControlMetricsCollector.Instance?.RecordCancel(
                    skillName,
                    cancelPoint: (float)(damagePointMs + waitAfterDamageMs) / (damagePointMs + waitAfterDamageMs + skippableDelayMs),
                    timeSavedMs: skippableDelayMs
                );

                animationController.RPC_Multi_CancelSkillAnimation();

                IsCasting = false;
                heroMovement.IsCastingSkill = false;
                return true;
            }

            await UniTask.Delay(skippableDelayMs);
        }
        else
        {
            await UniTask.Delay(waitAfterDamageMs + skippableDelayMs);
        }

        IsCasting = false;
        heroMovement.IsCastingSkill = false;
        return false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_Skill_Q_Activate_Init(NetworkObject no)
    {
        var evaQ = no.GetComponent<Eva_Q>();
        evaQ.ActiveInit();
    }

    private void Skill_W(HeroInput _heroInput)
    {
        if (!SkillW_CooldownTimer.ExpiredOrNotRunning(Runner))
            return;
        if (IsCasting)
            return;

        IsCasting = true;
        heroMovement.IsCastingSkill = true;
        heroMovement.SetMovePosition(transform.position);

        animationController.RPC_Multi_Skill_W();

        Vector3 skillDir = _heroInput.HitPosition_Skill - transform.position;
        skillDir = new Vector3(skillDir.x, 0, skillDir.z);

        if (skillDir != Vector3.zero)
        {
            heroMovement.SetLookDirection(skillDir.normalized);
        }

        Vector3 spawnPos = _heroInput.HitPosition_Skill;
        float distance = Vector3.Distance(transform.position, spawnPos);
        if (distance > SkillW_Range)
        {
            spawnPos = transform.position + skillDir.normalized * SkillW_Range;
        }
        spawnPos.y = 0;

        W_SpawnProcess(_heroInput, spawnPos).Forget();
    }

    private async UniTaskVoid W_SpawnProcess(HeroInput hi, Vector3 spawnPos)
    {
        var cancelInfo = _skillCancelData?.GetSkillInfo("W");

        int damagePointMs = cancelInfo?.DamagePointMs ?? 50;
        int waitAfterDamageMs = cancelInfo?.WaitAfterDamageMs ?? 20;
        int skippableDelayMs = cancelInfo?.SkippableDelayMs ?? 30;

        await UniTask.Delay(damagePointMs);

        _spawnedSkillW = Runner.Spawn(_skillW, spawnPos, Quaternion.identity);
        _spawnedSkillW.GetComponent<Eva_W>().Init(hi.Owner, this);
        RPC_Skill_W_Activate_Init(_spawnedSkillW);

        SkillW_CooldownTimer = TickTimer.CreateFromSeconds(Runner, SkillW_Cooldown);

        await TryAnimationCancel("W", damagePointMs, waitAfterDamageMs, skippableDelayMs);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_Skill_W_Activate_Init(NetworkObject no)
    {
        var evaW = no.GetComponent<Eva_W>();
        evaW.ActiveInit();
    }

    protected override void Skill_E()
    {
        if (!SkillE_CooldownTimer.ExpiredOrNotRunning(Runner))
            return;
        if (IsCasting)
            return;
        if (IsUsingSkillE)
            return;

        IsUsingSkillE = true;
        IsVFLightReady = true;
        heroMovement.IsPlayingSkillAnimation = true;
        heroMovement.IsSlowImmune = true;

        CancelBasicAttack();

        originalSpeedMultiplier = heroMovement.SpeedMultiplier;
        heroMovement.SpeedMultiplier = SkillE_SpeedBonus[SkillE_Level];

        SkillE_DurationTimer = TickTimer.CreateFromSeconds(Runner, SkillE_Duration);
        VFLight_WindowTimer = TickTimer.CreateFromSeconds(Runner, VFLight_Window);

        animationController.RPC_Multi_Skill_E();
    }

    private void EndSkillE()
    {

        IsUsingSkillE = false;

        heroMovement.SpeedMultiplier = 1.0f;
        heroMovement.IsSlowImmune = false;
        heroMovement.IsPlayingSkillAnimation = false;

        SkillE_CooldownTimer = TickTimer.CreateFromSeconds(Runner, SkillE_Cooldown);

        animationController.RPC_Multi_Skill_E_End();
    }

    public void TryApplyVFLight(NetworkObject target)
    {
        if (!IsVFLightReady)
            return;
        if (target == null)
            return;
        if (_vfLightPrefab == null)
            return;
        if (Runner == null || !Runner.IsServer)
            return;

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
        if (IsCasting && !_isRActive) return;

        if (!_isRActive)
        {
            IsCasting = true;
            _isRActive = true;

            _skillDirection = _heroInput.HitPosition_Skill - transform.position;
            _skillDirection = new Vector3(_skillDirection.x, 0, _skillDirection.z);

            heroMovement.SetLookDirection(_skillDirection.normalized);

            var dir = Quaternion.LookRotation(_skillDirection);
            _spawnedSkillR = Runner.Spawn(_skillR, transform.position, dir);

            _spawnedSkillR.GetComponent<Eva_R>().Init(_heroInput.Owner, this);

            var nt = _spawnedSkillR.GetComponent<NetworkTransform>();
            nt.Teleport(transform.position, dir);

            RPC_Skill_R_Activate_Init(_spawnedSkillR);

            animationController.RPC_Multi_Skill_R_Activate_Animation();
            heroMovement.IsCastingSkill = true;
        }
        else
        {
            Runner.Despawn(_spawnedSkillR);

            IsCasting = false;
            _isRActive = false;
            animationController.RPC_Multi_Skill_R_Deactivate_Animation();
            heroMovement.IsCastingSkill = false;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_Skill_R_Activate_Init(NetworkObject no)
    {
        var evaR = no.GetComponent<Eva_R>();
        evaR.ActiveInit();
    }

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
            heroMovement.IsAttacking = false;
        }
    }

    private void ProcessBasicAttack()
    {
        if (!Runner.TryFindObject(CurrentTargetId, out NetworkObject targetObject))
        {
            CurrentTargetId = default;
            IsInAttackRange = false;
            heroMovement.IsAttacking = false;
            return;
        }

        var targetState = targetObject.GetComponentInChildren<HeroState>();
        if (targetState == null || targetState.GetCurrHealth() <= 0f)
        {
            CurrentTargetId = default;
            IsInAttackRange = false;
            heroMovement.IsAttacking = false;
            return;
        }

        var targetMovement = targetObject.GetComponentInChildren<HeroMovement>();
        Vector3 targetPosition = targetMovement != null
            ? targetMovement.GetKcc().Position
            : targetObject.transform.position;
        Vector3 myPosition = transform.position;
        float distance = Vector3.Distance(myPosition, targetPosition);

        if (distance > BasicAttackRange)
        {
            if (IsInAttackRange)
            {
                animationController.RPC_Multi_CancelBasicAttack();
                IsInAttackRange = false;
            }

            heroMovement.IsAttacking = false;
            heroMovement.SetMovePosition(targetPosition);
            return;
        }

        IsInAttackRange = true;
        heroMovement.IsAttacking = true;

        if (BasicAttackCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            Vector3 dir = (targetPosition - myPosition).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
            {
                heroMovement.SetLookDirection(dir);
            }

            animationController.RPC_Multi_BasicAttack();

            var damageProcess = targetObject.GetComponentInChildren<IDamageProcess>();
            if (damageProcess != null)
            {
                damageProcess.OnTakeDamage(BasicAttackDamage);
            }

            BasicAttackCooldownTimer = TickTimer.CreateFromSeconds(Runner, BasicAttackCooldown);
        }
    }

    private void BufferSkillInputs(HeroInput input)
    {
        if (_inputBuffer == null)
            return;

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

        if (!_isRActive)
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

    private void ProcessBufferedInputs()
    {
        if (_inputBuffer == null)
            return;

        if (_inputBuffer.TryGetNextInput(out BufferedInput bufferedInput))
        {
            ExecuteBufferedSkill(bufferedInput);
        }
    }

    private void ExecuteBufferedSkill(BufferedInput bufferedInput)
    {
        if (IsUsingSkillE && bufferedInput.Button != InputButton.SkillE)
            return;

        var tempInput = new HeroInput
        {
            HitPosition_Skill = bufferedInput.HitPosition,
            MousePosition = bufferedInput.MousePosition,
            Owner = heroInput.Owner
        };

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

    private void ProcessDirectSkillInputs(HeroInput input)
    {
        if (input.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillQ))
        {
            if (ButtonsPreviousQ == 0 && !IsUsingSkillE)
            {
                ButtonsPreviousQ = 1;
                CancelBasicAttack();
                ControlMetricsCollector.Instance?.RecordInput("Q", wasBuffered: false, 0f);
                Skill_Q(input);
            }
        }
        if (input.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillQ))
            ButtonsPreviousQ = 0;

        if (input.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillW))
        {
            if (ButtonsPreviousW == 0 && !IsUsingSkillE)
            {
                ButtonsPreviousW = 1;
                CancelBasicAttack();
                ControlMetricsCollector.Instance?.RecordInput("W", wasBuffered: false, 0f);
                Skill_W(input);
            }
        }
        if (input.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillW))
            ButtonsPreviousW = 0;

        if (input.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillE))
        {
            if (ButtonsPreviousE == 0)
            {
                ButtonsPreviousE = 1;
                CancelBasicAttack();
                ControlMetricsCollector.Instance?.RecordInput("E", wasBuffered: false, 0f);
                Skill_E();
            }
        }
        if (input.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillE))
            ButtonsPreviousE = 0;

        if (input.Buttons.WasPressed(ButtonsPrevious, InputButton.SkillR))
        {
            if (ButtonsPreviousR == 0 && !IsUsingSkillE)
            {
                ButtonsPreviousR = 1;
                CancelBasicAttack();
                ControlMetricsCollector.Instance?.RecordInput("R", wasBuffered: false, 0f);
                Skill_R(input);
            }
        }
        if (input.Buttons.WasReleased(ButtonsPrevious, InputButton.SkillR))
            ButtonsPreviousR = 0;
    }

    private void PredictLocalAnimations(HeroInput input)
    {
        if (heroMovement.IsAirborne || heroMovement.IsDeath)
        {
            _localButtonsPrevious = input.Buttons;
            return;
        }

        if (IsCasting && !_isRActive)
        {
            _localButtonsPrevious = input.Buttons;
            return;
        }

        if (input.Buttons.WasPressed(_localButtonsPrevious, InputButton.SkillQ) && !IsUsingSkillE)
            animationController.Local_Skill_Q();

        if (input.Buttons.WasPressed(_localButtonsPrevious, InputButton.SkillW) && !IsUsingSkillE)
            animationController.Local_Skill_W();

        if (input.Buttons.WasPressed(_localButtonsPrevious, InputButton.SkillE))
            animationController.Local_Skill_E();

        if (input.Buttons.WasPressed(_localButtonsPrevious, InputButton.SkillR) && !IsUsingSkillE)
        {
            if (!_isRActive) animationController.Local_Skill_R_Activate();
            else animationController.Local_Skill_R_Deactivate();
        }

        _localButtonsPrevious = input.Buttons;
    }

    private bool IsLocalFeedbackEnabled()
    {
        return _controlConfig != null && _controlConfig.EnableLocalFeedback;
    }
}
