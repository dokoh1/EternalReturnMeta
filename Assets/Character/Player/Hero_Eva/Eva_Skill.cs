// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Eva_Skill.cs - Eva 캐릭터의 스킬 시스템
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 개요 (Overview)                                                                                      │
// │                                                                                                       │
// │ 이 파일은 Eva 캐릭터의 모든 스킬(Q, W, E, R)과 기본 공격을 담당합니다.                                │
// │ Photon Fusion 네트워크 프레임워크를 사용하여 멀티플레이어 동기화를 처리합니다.                        │
// │                                                                                                       │
// │ 핵심 기술:                                                                                            │
// │ 1. Photon Fusion [Networked] 변수 - 서버-클라이언트 간 상태 동기화                                    │
// │ 2. Input Buffering - 스킬 시전 중 입력을 저장했다가 시전 완료 후 실행                                 │
// │ 3. Animation Canceling - 스킬 후딜레이를 캔슬하여 빠른 연계 가능                                      │
// │ 4. Server-Authoritative Model - 모든 게임 로직은 서버(Host)에서 처리                                  │
// │ 5. UniTask - 비동기 스킬 시전 처리                                                                    │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 설계 철학 (Design Philosophy)                                                                        │
// │                                                                                                       │
// │ Q: 왜 서버에서만 스킬을 처리하나요?                                                                  │
// │ A: 치팅 방지! 클라이언트가 "나 스킬 맞았어"라고 보고하면 해킹 가능.                                  │
// │    서버가 판정해야 공정한 게임이 됨.                                                                  │
// │                                                                                                       │
// │ Q: 왜 Input Buffering이 필요한가요?                                                                  │
// │ A: 스킬 시전 중 다음 스킬을 누르면, 시전 완료 후 바로 실행되어야 자연스러움.                          │
// │    버퍼링 없으면 "씹힘" 현상 발생 → 프로게이머들이 싫어함.                                           │
// │                                                                                                       │
// │ Q: 왜 Animation Canceling이 필요한가요?                                                              │
// │ A: 스킬 후딜레이(데미지 적용 후 남은 애니메이션)는 게임플레이에 불필요.                               │
// │    이걸 캔슬할 수 있으면 고수 플레이어가 더 빠른 콤보 가능 → 스킬 격차.                              │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

using Character.Player;
using Character.Player.ControlSettings;  // Phase 1, 2에서 만든 시스템
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Eva_Skill 클래스
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
//
// 상속 구조: NetworkBehaviour → HeroSkill → Eva_Skill
//
// NetworkBehaviour: Photon Fusion의 네트워크 동기화 기본 클래스
// HeroSkill: 모든 영웅의 스킬 공통 인터페이스 (Skill_Q(), Skill_W() 등)
// Eva_Skill: Eva 캐릭터 전용 스킬 구현
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
public class Eva_Skill : HeroSkill
{
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 1. 입력 상태 관리 (Input State Management)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // [Networked] 변수들의 역할:
    // - 서버에서 값을 변경하면 자동으로 모든 클라이언트에 동기화됨
    // - 새 플레이어가 접속해도 현재 상태를 바로 알 수 있음
    //
    // ButtonsPrevious: 이전 프레임의 버튼 상태
    // - WasPressed() 판정에 사용: 이전=0, 현재=1 → "방금 눌림"
    // - 중복 입력 방지에 필수
    //
    // ButtonsPreviousQ/W/E/R: 개별 스킬 버튼 상태
    // - 왜 별도로 관리? → 버튼 "떼기"도 감지해야 해서
    // - 예: Q 누른 상태에서 W 누르면, Q 버튼 상태는 유지되어야 함
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private HeroInput heroInput;

    /// <summary>
    /// 이전 프레임의 버튼 상태 (모든 버튼)
    /// Photon Fusion의 NetworkButtons 구조체 사용
    /// </summary>
    [Networked] public NetworkButtons ButtonsPrevious { get; set; }

    /// <summary>
    /// 개별 스킬 버튼의 눌림 상태 (0=떼어짐, 1=눌림)
    /// WasReleased() 판정을 위해 별도 관리
    /// </summary>
    [Networked] private int ButtonsPreviousQ { get; set; }
    [Networked] private int ButtonsPreviousW { get; set; }
    [Networked] private int ButtonsPreviousE { get; set; }
    [Networked] private int ButtonsPreviousR { get; set; }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 2. 스킬 프리팹 (Skill Prefabs)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // [SerializeField]: Inspector에서 할당 가능하게 함
    //
    // 각 스킬의 시각적 효과와 히트박스는 별도의 프리팹으로 관리
    // 스킬 사용 시 Runner.Spawn()으로 네트워크에 생성
    //
    // 프리팹 구조:
    // - _skillQ: 투사체 (Eva_Q.cs 컴포넌트)
    // - _skillW: 범위 스킬 (Eva_W.cs 컴포넌트)
    // - _skillR: 지속형 스킬 (Eva_R.cs 컴포넌트)
    // - _vfLightPrefab: E 스킬 효과 - 추가 데미지 투사체 (Eva_VFLight.cs)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    [SerializeField] private GameObject _skillQ;
    [SerializeField] private GameObject _skillW;
    [SerializeField] private GameObject _skillR;
    [SerializeField] private GameObject _vfLightPrefab;  // VF 라이트 프리팹

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 3. Input Buffering 시스템 (Phase 2)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 왜 Input Buffering이 필요한가?
    //
    // [문제 상황]
    // 1. 플레이어가 Q 스킬 시전 (시전 시간 300ms)
    // 2. 200ms 시점에 W 스킬 입력
    // 3. 버퍼링 없으면: W 입력 무시됨 → "씹혔다!"
    // 4. 버퍼링 있으면: W 입력 저장 → Q 끝나면 바로 W 실행
    //
    // [작동 원리]
    // 1. 스킬 시전 중(IsCasting=true) → BufferSkillInputs() 호출
    // 2. 새 입력이 들어오면 _inputBuffer에 저장
    // 3. 스킬 시전 완료 → ProcessBufferedInputs() 호출
    // 4. 버퍼에서 입력 꺼내서 ExecuteBufferedSkill() 실행
    //
    // [ControlSettingsConfig]
    // - ScriptableObject로 만든 설정 파일
    // - BufferTimeSeconds: 입력 유효 시간 (보통 0.2~0.5초)
    // - EnableAnimationCanceling: 애니메이션 캔슬 활성화 여부
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    [Header("=== Control Settings (Phase 2) ===")]
    [Tooltip("조작감 설정 (Project 창에서 생성한 ScriptableObject 할당)")]
    [SerializeField] private ControlSettingsConfig _controlConfig;

    /// <summary>
    /// 입력 버퍼 인스턴스
    /// 스킬 시전 중 입력을 저장하고, 시전 완료 후 꺼냄
    /// </summary>
    private InputBuffer _inputBuffer;

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 4. Animation Canceling 시스템 (Phase 4)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 왜 Animation Canceling이 필요한가?
    //
    // [스킬 타임라인 예시 - Q 스킬]
    // 0ms ─────────────────────────────────────────────────────────> 시간
    //     ├─── 시전 딜레이 ───┤─── 데미지 적용 ───┤─── 후딜레이 ───┤
    //     │    (100ms)       │                   │    (50ms)      │
    //     │                  │                   │                │
    //     └── 캔슬 불가 ─────┴── 캔슬 가능 시점 ──┴── 생략 가능 ───┘
    //
    // [캔슬 조건]
    // 1. 데미지가 적용된 후 (게임플레이에 영향 없는 시점)
    // 2. 버퍼에 다음 스킬이 대기 중
    // 3. EnableAnimationCanceling이 ON
    //
    // [SkillCancelData]
    // - ScriptableObject로 만든 스킬별 타이밍 데이터
    // - DamagePointMs: 데미지 적용 시점 (시전 시작부터)
    // - WaitAfterDamageMs: 데미지 후 캔슬 가능까지 대기
    // - SkippableDelayMs: 캔슬 시 스킵되는 후딜레이
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    [Header("=== Skill Cancel Data (Phase 4) ===")]
    [Tooltip("스킬 캔슬 타이밍 데이터 (Project 창에서 생성한 ScriptableObject 할당)")]
    [SerializeField] private SkillCancelData _skillCancelData;

    /// <summary>
    /// 스폰된 스킬 오브젝트 참조
    /// R 스킬 비활성화, 스킬 정리 등에 사용
    /// </summary>
    private NetworkObject skillR_Dummy;
    private NetworkObject skillQ_Dummy;
    private NetworkObject skillW_Dummy;

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 5. W 스킬 설정 (범위 스킬)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // W 스킬 특징:
    // - 지정된 위치에 범위 데미지
    // - 사거리 제한 있음 (SkillW_Range)
    // - 사거리 밖을 클릭하면 최대 사거리 지점에 생성
    //
    // [Networked] TickTimer 사용 이유:
    // - 서버-클라이언트 간 시간 동기화
    // - Runner.Tick 기반이라 정확함 (Time.deltaTime보다 네트워크 친화적)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private const float SkillW_Range = 5.0f;  // 사거리 5m

    /// <summary>
    /// W 스킬 쿨다운 타이머
    /// TickTimer: Photon Fusion의 네트워크 동기화 타이머
    /// </summary>
    [Networked] private TickTimer SkillW_CooldownTimer { get; set; }
    private const float SkillW_Cooldown = 1.0f;  // 쿨다운 1초

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 6. E 스킬 설정 (VF 라이트 버프)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // E 스킬 메카닉:
    // 1. E 사용 → 이동속도 증가 (SkillE_Duration 동안)
    // 2. E 사용 후 일정 시간(VFLight_Window) 내 Q/W/R 적중 시
    //    → 추가 VF 라이트 투사체 발사
    //
    // [상태 플래그]
    // - IsUsingSkillE: E 스킬 사용 중 (다른 스킬 사용 불가)
    // - IsVFLightReady: VF 라이트 발동 가능 상태
    //
    // [이동속도 증가]
    // - SkillE_SpeedBonus 배열로 레벨별 속도 배율 관리
    // - 원래 속도 저장 후 증가, E 종료 시 복구
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    [Networked] private TickTimer SkillE_CooldownTimer { get; set; }
    [Networked] private TickTimer SkillE_DurationTimer { get; set; }  // E 스킬 지속시간 (0.5초)
    [Networked] private TickTimer VFLight_WindowTimer { get; set; }   // VF 라이트 발동 가능 시간 (3초)

    /// <summary>
    /// E 스킬 사용 중 플래그
    /// true일 때: 이동만 가능, Q/W/R 사용 불가
    /// </summary>
    [Networked] private NetworkBool IsUsingSkillE { get; set; }

    /// <summary>
    /// VF 라이트 발동 가능 상태
    /// E 사용 후 일정 시간 동안 true, 스킬 적중 시 추가 데미지
    /// </summary>
    [Networked] private NetworkBool IsVFLightReady { get; set; }

    private const float SkillE_Cooldown = 1.0f;  // 쿨다운 1초
    private const float SkillE_Duration = 1.0f;   // 지속시간 1초 (애니메이션 동안 다른 스킬 사용 불가)
    private const float VFLight_Window = 3.0f;    // VF 라이트 발동 가능 시간 3초
    private const float VFLight_Damage = 20f;     // VF 라이트 추가 데미지

    /// <summary>
    /// 레벨별 이동속도 증가 배율
    /// Level 0: 120%, Level 4: 140%
    /// </summary>
    private float[] SkillE_SpeedBonus = { 1.2f, 1.25f, 1.3f, 1.35f, 1.4f };
    private int SkillE_Level = 0;  // E 스킬 레벨 (0~4)
    private float originalSpeedMultiplier = 1.0f;  // E 사용 전 원래 속도 배율

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 7. 스킬 방향 및 시전 상태
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // _skillQDir: Q 스킬 발사 방향 (마우스 위치 - 캐릭터 위치)
    //
    // IsCasting: 스킬 시전 중 플래그
    // - true일 때: 새 스킬 입력은 버퍼에 저장
    // - 이동, 다른 스킬 사용 불가
    //
    // 참고: IsCasting은 [Networked]가 아님
    // - 왜? → 서버에서만 스킬 로직을 처리하므로 클라이언트 동기화 불필요
    // - 클라이언트는 애니메이션만 재생 (RPC로 동기화)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Q 스킬 발사 방향
    /// 마우스 클릭 위치 - 캐릭터 위치로 계산
    /// </summary>
    private Vector3 _skillQDir {get; set;}

    /// <summary>
    /// 스킬 시전 중 플래그
    /// true: 입력 버퍼링, 이동 정지
    /// false: 직접 스킬 실행
    /// </summary>
    private bool IsCasting;

    /// <summary>
    /// 참조 캐시 - 매 프레임 GetComponent() 호출 방지
    /// </summary>
    private HeroMovement heroMovement;
    private Eva_AnimationController animationController;

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 8. R 스킬 설정 (지속형 스킬)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // R 스킬 특징:
    // - 토글 방식: R 누르면 활성화, 다시 R 누르면 비활성화
    // - 활성화 중 마우스 방향으로 계속 회전
    //
    // [Networked] skillR_Dir:
    // - 서버에서 계산한 R 스킬 방향
    // - 모든 클라이언트에 동기화되어 동일한 방향 표시
    //
    // IsActivating_R:
    // - R 스킬 활성화 상태 (로컬 플래그)
    // - 왜 [Networked] 아님? → 스킬 오브젝트 자체가 네트워크에 존재해서 불필요
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// R 스킬 활성화 상태 (로컬)
    /// </summary>
    private bool IsActivating_R{ get; set;}

    /// <summary>
    /// R 스킬 코루틴 (현재 미사용, UniTask로 대체)
    /// </summary>
    private Coroutine Coroutine_R;

    /// <summary>
    /// R 스킬 방향 (네트워크 동기화)
    /// 마우스 위치를 향해 스킬 오브젝트가 회전
    /// </summary>
    [Networked] private Vector3 skillR_Dir { get; set; }

    /// <summary>
    /// R 스킬 마우스 위치 (로컬)
    /// 매 프레임 업데이트되어 skillR_Dir 계산에 사용
    /// </summary>
    private Vector3 Skill_R_MousePosition { get; set; }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 9. 기본 공격 설정
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 기본 공격 메카닉:
    // 1. 적 우클릭 → 타겟 설정 (CurrentTargetId)
    // 2. 사거리 밖이면 추적 이동
    // 3. 사거리 안이면 멈추고 공격
    // 4. 땅 우클릭 또는 스킬 사용 → 타겟 해제 + 공격 캔슬
    //
    // [Networked] CurrentTargetId:
    // - NetworkId로 타겟 참조 (NetworkObject 직접 참조 불가)
    // - Runner.TryFindObject()로 실제 오브젝트 찾음
    //
    // IsInAttackRange:
    // - 사거리 내 여부 (캔슬 애니메이션 판정에 사용)
    // - 사거리 안 → 밖으로 나가면 캔슬 애니메이션 재생
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private const float BasicAttackRange = 30.0f;  // 사거리
    private const float BasicAttackDamage = 5.0f;
    private const float BasicAttackCooldown = 0.5f;

    /// <summary>
    /// 기본 공격 쿨다운 타이머
    /// </summary>
    [Networked] private TickTimer BasicAttackCooldownTimer { get; set; }

    /// <summary>
    /// 현재 타겟의 NetworkId
    /// NetworkId: Photon Fusion에서 네트워크 오브젝트 식별자
    /// </summary>
    [Networked] private NetworkId CurrentTargetId { get; set; }

    /// <summary>
    /// 사거리 내 여부 (캔슬 판정용)
    /// 사거리 안→밖 전환 시 캔슬 애니메이션 재생
    /// </summary>
    [Networked] private NetworkBool IsInAttackRange { get; set; }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Spawned() - 네트워크 오브젝트 생성 시 호출
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // Photon Fusion 생명주기:
    // Awake() → Spawned() → FixedUpdateNetwork() 반복 → Despawned()
    //
    // Spawned()에서 초기화해야 하는 이유:
    // - Awake()는 네트워크 연결 전에 호출될 수 있음
    // - Runner, Object.Id 등 네트워크 관련 값이 Spawned()에서 확정됨
    //
    // GetComponent() 캐싱:
    // - 매 프레임 GetComponent() 호출은 비용이 큼
    // - Spawned()에서 한 번만 호출하고 참조 저장
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public override void Spawned()
    {
        // 버튼 상태 초기화
        ButtonsPreviousQ = 0;
        IsCasting = false;
        IsActivating_R = false;

        // 컴포넌트 참조 캐싱
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

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // FixedUpdateNetwork() - 네트워크 게임 루프 (핵심!)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 호출 타이밍:
    // - 서버: 매 네트워크 틱마다 호출 (기본 60Hz)
    // - 클라이언트: 예측(Prediction) 시 호출될 수 있음
    //
    // HasStateAuthority 체크가 중요한 이유:
    // - 스킬 로직은 서버에서만 처리해야 함 (치팅 방지)
    // - 클라이언트가 스킬 로직 실행하면 서버와 상태 불일치
    //
    // 입력 처리 흐름:
    // 1. GetInput() → 클라이언트가 보낸 입력 받기
    // 2. 에어본 체크 → 공중에 뜬 상태면 조작 불가
    // 3. R 토글 처리 → R은 특수 케이스로 먼저 처리
    // 4. 버퍼링/직접 실행 분기
    // 5. 기본 공격 처리
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public override void FixedUpdateNetwork()
    {
        // ═══════════════════════════════════════════════════════════════
        // 서버 권한 체크 (Server-Authoritative Model)
        //
        // HasStateAuthority가 true인 경우:
        // - 서버(Host): 항상 true
        // - 클라이언트: 자신의 캐릭터에 대해 예측 모드일 때 true
        //
        // 스킬 로직은 서버에서만 처리해야 치팅 방지됨
        // ═══════════════════════════════════════════════════════════════
        if (!HasStateAuthority) return;

        // ═══════════════════════════════════════════════════════════════
        // E 스킬 지속시간 체크
        // TickTimer.Expired(): 타이머가 만료되었는지 확인
        // ═══════════════════════════════════════════════════════════════
        if (IsUsingSkillE && SkillE_DurationTimer.Expired(Runner))
        {
            EndSkillE();
        }

        // ═══════════════════════════════════════════════════════════════
        // VF 라이트 윈도우 체크
        // E 스킬 사용 후 일정 시간이 지나면 VF 라이트 비활성화
        // ═══════════════════════════════════════════════════════════════
        if (IsVFLightReady && VFLight_WindowTimer.Expired(Runner))
        {
            IsVFLightReady = false;
        }

        // ═══════════════════════════════════════════════════════════════
        // R 스킬 활성화 중 처리
        //
        // R 스킬은 지속형이라 별도 처리 필요:
        // 1. 캐릭터 사망 시 R 해제
        // 2. 마우스 방향으로 스킬 오브젝트 회전
        // 3. 캐릭터 회전 동기화
        // ═══════════════════════════════════════════════════════════════
        if (IsActivating_R)
        {
            // 사망 시 R 스킬 강제 종료
            if (heroMovement.IsDeath)
            {
                Runner.Despawn(skillR_Dummy);
                IsActivating_R = false;
            }

            // R 스킬 오브젝트 회전 처리
            if (skillR_Dummy != null)
            {
                // 마우스 방향 계산
                skillR_Dir = (Skill_R_MousePosition - skillR_Dummy.transform.position).normalized;

                // Quaternion.Slerp(): 부드러운 회전 보간
                // - 현재 회전 → 목표 회전을 천천히 전환
                // - Runner.DeltaTime * 3.0f: 회전 속도 (높을수록 빠름)
                skillR_Dummy.transform.rotation = Quaternion.Slerp(skillR_Dummy.transform.rotation, Quaternion.LookRotation(skillR_Dir),
                    Runner.DeltaTime * 3.0f // 속도 조정 (6배 빠르게)
                );

                // ═══════════════════════════════════════════════════════
                // 회전 동기화
                //
                // SetLookRotationFromQuaternion():
                // - R 스킬 오브젝트의 회전을 캐릭터에 적용
                // - CurrentYaw 업데이트 → 모든 클라이언트에 동기화
                // ═══════════════════════════════════════════════════════
                heroMovement.SetLookRotationFromQuaternion(skillR_Dummy.transform.rotation);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 입력 처리
        //
        // GetInput(out heroInput):
        // - 클라이언트가 NetworkRunner에 전송한 입력 가져오기
        // - 버튼 상태, 마우스 위치 등 포함
        //
        // 반환값이 false면 이번 틱에 입력 없음
        // ═══════════════════════════════════════════════════════════════
        if (GetInput(out heroInput))
        {
            // ═══════════════════════════════════════════════════════════
            // 에어본 상태 체크
            //
            // 에어본 중에는 모든 조작 불가:
            // - 스킬 사용 X
            // - 이동 X
            // - 기본 공격 X
            //
            // 단, ButtonsPrevious는 업데이트해야 함
            // (에어본 끝나고 바로 스킬 쓸 때 WasPressed 오작동 방지)
            // ═══════════════════════════════════════════════════════════
            if (heroMovement.IsAirborne)
            {
                ButtonsPrevious = heroInput.Buttons;
                return;
            }

            // ═══════════════════════════════════════════════════════════
            // R 스킬 토글 처리 (특수 케이스)
            //
            // R은 활성화 상태에서 다시 R 누르면 비활성화되는 토글 방식
            // 버퍼링과 상관없이 항상 즉시 처리해야 함
            //
            // 왜 별도 처리?
            // - 버퍼에 R이 쌓이면 토글이 꼬임
            // - 예: R→R→R 버퍼 → 켜고→끄고→켜고 = 혼란
            // ═══════════════════════════════════════════════════════════
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

            // ═══════════════════════════════════════════════════════════
            // Phase 2: Input Buffering 처리
            //
            // 핵심 로직:
            // 1. 스킬 시전 중(IsCasting) 또는 E스킬 중 → 입력을 버퍼에 저장
            // 2. 스킬 시전 완료 → 버퍼에서 입력 꺼내서 실행
            // 3. 스킬 시전 중 아니면 → 즉시 실행 (기존 동작)
            //
            // 왜 E스킬 중에도 버퍼링?
            // - E 스킬 중에는 Q/W/R 사용 불가
            // - 하지만 입력은 저장해두고, E 끝나면 바로 실행
            // ═══════════════════════════════════════════════════════════

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

            // ═══════════════════════════════════════════════════════════
            // 기본 공격 처리
            //
            // E 스킬 중에는 기본 공격 불가 (이동만 가능)
            //
            // 우클릭 로직:
            // - 적 클릭 → 타겟 설정, 추적 시작
            // - 땅 클릭 → 타겟 해제, 공격 캔슬
            // ═══════════════════════════════════════════════════════════
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

        // R 스킬용 마우스 위치 업데이트
        Skill_R_MousePosition = heroInput.MousePosition;

        // 이번 프레임 버튼 상태 저장 (다음 프레임 WasPressed 판정용)
        ButtonsPrevious = heroInput.Buttons;
    }


    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Q 스킬 - 투사체 발사
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 작동 방식:
    // 1. 캐릭터 이동 정지
    // 2. 마우스 방향으로 회전
    // 3. 애니메이션 재생 (RPC로 모든 클라이언트 동기화)
    // 4. 시전 딜레이 후 투사체 스폰
    // 5. 후딜레이 또는 캔슬
    //
    // SetLookDirection() 사용 이유:
    // - KCC 직접 회전은 클라이언트에 동기화 안 됨
    // - SetLookDirection()은 CurrentYaw 업데이트 → 자동 동기화
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void Skill_Q(HeroInput _heroInput)
    {
        // 이미 시전 중이면 무시
        if (IsCasting) return;

        // 스킬 시전 시작
        IsCasting = true;
        heroMovement.IsCastingSkill = true;  // 스킬 사용 중 이동 정지
        heroMovement.SetMovePosition(transform.position);  // 현재 위치에서 멈춤

        // ═══════════════════════════════════════════════════════════════
        // 애니메이션 동기화 (RPC)
        //
        // RPC_Multi_Skill_Q():
        // - RpcSources.StateAuthority: 서버에서만 호출 가능
        // - RpcTargets.All: 모든 클라이언트에서 실행
        // - 결과: 서버가 호출 → 모든 클라이언트 애니메이션 재생
        // ═══════════════════════════════════════════════════════════════
        animationController.RPC_Multi_Skill_Q();

        // 스킬 방향 계산 (마우스 위치 - 캐릭터 위치)
        _skillQDir = _heroInput.HitPosition_Skill - gameObject.transform.position;
        _skillQDir = new Vector3(_skillQDir.x, 0, _skillQDir.z);  // Y축 제거 (수평 방향만)

        var dir = Quaternion.LookRotation(_skillQDir);

        // ═══════════════════════════════════════════════════════════════
        // 회전 동기화: SetLookDirection 사용
        //
        // 왜 kcc.SetLookRotation() 직접 호출 안 하나?
        // - KCC 회전은 로컬에서만 적용됨
        // - SetLookDirection()은 CurrentYaw 업데이트 → [Networked]라 동기화됨
        // ═══════════════════════════════════════════════════════════════
        heroMovement.SetLookDirection(_skillQDir.normalized);

        // 비동기 스킬 프로세스 시작 (UniTask)
        Q_SpawnProcess(_heroInput, dir).Forget();
    }


    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Q_SpawnProcess() - Q 스킬 비동기 처리
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // UniTask 사용 이유:
    // - 코루틴보다 성능 좋음 (GC 할당 적음)
    // - async/await 문법으로 가독성 좋음
    // - .Forget(): 결과를 기다리지 않고 실행 (fire-and-forget)
    //
    // 스킬 타임라인:
    // 0ms ───────────────────────────────────────────────> 시간
    //     ├─ damagePointMs ─┤─ waitAfterDamageMs ─┤─ skippableDelayMs ─┤
    //     │   (시전 딜레이)  │  (캔슬 가능 대기)   │    (후딜레이)      │
    //     │                 │                     │                    │
    //     └─ 투사체 스폰 ───┴─ 캔슬 체크 ─────────┴─ IsCasting = false ┘
    //
    // Animation Canceling 로직:
    // 1. 데미지 적용 후 waitAfterDamageMs 대기
    // 2. 버퍼에 다음 스킬이 있으면 skippableDelayMs 스킵
    // 3. 캔슬 시 RPC_Multi_CancelSkillAnimation() 호출하여 동기화
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private async UniTaskVoid Q_SpawnProcess(HeroInput hi, Quaternion dir)
    {
        // ═══════════════════════════════════════════════════════════════
        // Phase 4: 스킬 캔슬 데이터 가져오기
        //
        // SkillCancelData: ScriptableObject로 만든 스킬 타이밍 설정
        // - DamagePointMs: 스킬 시작 → 데미지 적용까지 시간
        // - WaitAfterDamageMs: 데미지 후 캔슬 가능까지 대기
        // - SkippableDelayMs: 캔슬 시 스킵되는 후딜레이
        // ═══════════════════════════════════════════════════════════════
        var cancelInfo = _skillCancelData?.GetSkillInfo("Q");

        // 캔슬 데이터가 없으면 기본값 사용
        int damagePointMs = cancelInfo?.DamagePointMs ?? 100;
        int waitAfterDamageMs = cancelInfo?.WaitAfterDamageMs ?? 20;
        int skippableDelayMs = cancelInfo?.SkippableDelayMs ?? 30;

        // [1] 시전 딜레이 (데미지 적용 시점까지)
        await UniTask.Delay(damagePointMs);

        // ═══════════════════════════════════════════════════════════════
        // [2] 데미지 적용 (투사체 발사)
        //
        // Runner.Spawn(): 네트워크에 오브젝트 생성
        // - 서버에서 호출 → 모든 클라이언트에 자동 생성
        // - 위치: 캐릭터 위치 + 높이 보정 (1m 위)
        // - 회전: 스킬 방향
        // ═══════════════════════════════════════════════════════════════
        skillQ_Dummy = Runner.Spawn(_skillQ, transform.position + new Vector3(0,1,0), Quaternion.LookRotation(_skillQDir));
        skillQ_Dummy.GetComponent<Eva_Q>().Init(hi.Owner, this);

        // RPC로 투사체 초기화 (VFX 등)
        RPC_Skill_Q_Activate_Init(skillQ_Dummy);

        // NetworkRigidbody3D로 위치/회전 강제 설정
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
        //
        // 왜 캔슬이 필요한가?
        // - 고수 플레이어는 빠른 콤보를 원함
        // - 후딜레이는 게임플레이에 영향 없음 (데미지는 이미 적용)
        // - 캔슬 가능 = 스킬 격차 = 재미
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

                // ═══════════════════════════════════════════════════════
                // 동기화: 모든 클라이언트에게 "캔슬됐다" 알림
                //
                // 이게 없으면?
                // - 서버: Q 애니메이션 120ms에 끊김 → W 시작
                // - 클라이언트: Q 애니메이션 150ms까지 재생 → 30ms 늦게 W 시작
                // - 결과: 상대방 화면에서 내 캐릭터 타이밍이 이상함
                // ═══════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // RPC_Skill_Q_Activate_Init() - Q 스킬 투사체 초기화 RPC
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // RPC (Remote Procedure Call) 특징:
    // - 서버 → 클라이언트 함수 호출
    // - [Rpc] 어트리뷰트로 정의
    //
    // RpcSources.StateAuthority: 서버(Host)에서만 호출 가능
    // RpcTargets.All: 모든 클라이언트에서 실행
    //
    // 왜 RPC로 초기화하나?
    // - Runner.Spawn()은 오브젝트만 생성
    // - VFX, 사운드 등 클라이언트 전용 초기화는 RPC 필요
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_Skill_Q_Activate_Init(NetworkObject no)
    {
        var evaQ = no.GetComponent<Eva_Q>();
        evaQ.ActiveInit().Forget();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // W 스킬 - 범위 스킬
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 작동 방식:
    // 1. 쿨다운 체크
    // 2. 캐릭터 이동 정지
    // 3. 마우스 방향으로 회전
    // 4. 사거리 계산 (사거리 밖이면 최대 사거리 지점에 생성)
    // 5. 범위 스킬 오브젝트 스폰
    // 6. 후딜레이 또는 캔슬
    //
    // 사거리 제한 이유:
    // - 밸런스: 무한 사거리면 너무 강함
    // - UX: 사거리 밖 클릭해도 최대 사거리에 생성 → 의도대로 작동하는 느낌
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void Skill_W(HeroInput _heroInput)
    {
        // ═══════════════════════════════════════════════════════════════
        // 쿨다운 체크
        //
        // ExpiredOrNotRunning(): 타이머가 만료됐거나 시작 안 됐으면 true
        // - 첫 사용 시: 타이머 미시작 → true
        // - 쿨다운 중: 타이머 진행 중 → false
        // - 쿨다운 끝: 타이머 만료 → true
        // ═══════════════════════════════════════════════════════════════
        if (!SkillW_CooldownTimer.ExpiredOrNotRunning(Runner)) return;
        if (IsCasting) return;

        IsCasting = true;
        heroMovement.IsCastingSkill = true;  // 스킬 사용 중 이동 정지
        heroMovement.SetMovePosition(transform.position);  // 현재 위치에서 멈춤

        // 애니메이션 동기화
        animationController.RPC_Multi_Skill_W();

        // ═══════════════════════════════════════════════════════════════
        // 마우스 방향으로 회전 (동기화됨)
        //
        // 왜 Y축을 0으로?
        // - 3D 공간에서 마우스는 지면에 있음
        // - 캐릭터는 지면 위에 있음
        // - Y 차이가 있으면 위아래를 보게 됨 → 이상함
        // ═══════════════════════════════════════════════════════════════
        Vector3 skillDir = _heroInput.HitPosition_Skill - transform.position;
        skillDir = new Vector3(skillDir.x, 0, skillDir.z);

        if (skillDir != Vector3.zero)
        {
            heroMovement.SetLookDirection(skillDir.normalized);
        }

        // ═══════════════════════════════════════════════════════════════
        // 스킬 위치 계산 (사거리 내로 제한)
        //
        // 사거리 밖 클릭 시:
        // - 클릭 위치 그대로 사용하면 사거리 밖에 생성 → 밸런스 문제
        // - 최대 사거리 지점에 생성 → 의도대로 작동
        // ═══════════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // W_SpawnProcess() - W 스킬 비동기 처리
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // Q_SpawnProcess()와 구조 동일:
    // 1. 시전 딜레이
    // 2. 스킬 오브젝트 스폰
    // 3. 캔슬 체크
    // 4. 후딜레이
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private async UniTaskVoid W_SpawnProcess(HeroInput hi, Vector3 spawnPos)
    {
        // Phase 4: 스킬 캔슬 데이터 가져오기
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
        // (Q 스킬과 동일한 패턴)
        // ═══════════════════════════════════════════════════════════════
        bool canCancel = _controlConfig != null && _controlConfig.EnableAnimationCanceling;

        if (canCancel && _inputBuffer != null)
        {
            // [3] 캔슬 가능 시점까지 대기
            await UniTask.Delay(waitAfterDamageMs);

            // [4] 버퍼에 다음 스킬이 있으면 캔슬!
            if (_inputBuffer.HasPendingInput())
            {
                ControlMetricsCollector.Instance?.RecordCancel(
                    "W",
                    cancelPoint: (float)(damagePointMs + waitAfterDamageMs) / (damagePointMs + waitAfterDamageMs + skippableDelayMs),
                    timeSavedMs: skippableDelayMs
                );

                Debug.Log($"[AnimCancel] W 스킬 캔슬! {skippableDelayMs}ms 절약");

                // 동기화: 모든 클라이언트에게 캔슬 알림
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

    /// <summary>
    /// W 스킬 오브젝트 초기화 RPC
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_Skill_W_Activate_Init(NetworkObject no)
    {
        var evaW = no.GetComponent<Eva_W>();
        evaW.ActiveInit().Forget();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // E 스킬 - VF 라이트 버프
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 작동 방식:
    // 1. E 사용 → 이동속도 증가 (SkillE_Duration 동안)
    // 2. E 사용 후 일정 시간(VFLight_Window) 내 Q/W/R 적중 시
    //    → 추가 VF 라이트 투사체 발사
    //
    // 특이점:
    // - E 스킬 중에는 Q/W/R 사용 불가 (이동만 가능)
    // - 슬로우 면역 (IsSlowImmune)
    // - 이동 애니메이션 차단 (IsPlayingSkillAnimation)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    protected override void Skill_E()
    {
        // 쿨다운 체크
        if (!SkillE_CooldownTimer.ExpiredOrNotRunning(Runner)) return;
        if (IsCasting) return;
        if (IsUsingSkillE) return;

        // ═══════════════════════════════════════════════════════════════
        // E 스킬 시작 - 플래그들 먼저 설정
        //
        // 순서가 중요한 이유:
        // 1. IsUsingSkillE = true 먼저
        // 2. heroMovement 플래그 설정
        // 3. 이동속도 변경
        //
        // 순서가 잘못되면 한 프레임 동안 의도치 않은 동작 가능
        // ═══════════════════════════════════════════════════════════════
        IsUsingSkillE = true;
        IsVFLightReady = true;
        heroMovement.IsPlayingSkillAnimation = true;  // 이동 애니메이션 차단 (먼저 설정)
        heroMovement.IsSlowImmune = true;

        // 기본 공격 캔슬
        CancelBasicAttack();

        // ═══════════════════════════════════════════════════════════════
        // 원래 속도 저장 후 이동속도 증가
        //
        // SpeedMultiplier: 이동속도 배율
        // - 기본값 1.0 = 100%
        // - SkillE_SpeedBonus[0] = 1.2 = 120%
        // ═══════════════════════════════════════════════════════════════
        originalSpeedMultiplier = heroMovement.SpeedMultiplier;
        heroMovement.SpeedMultiplier = SkillE_SpeedBonus[SkillE_Level];

        // ═══════════════════════════════════════════════════════════════
        // 타이머 시작
        //
        // SkillE_DurationTimer: E 스킬 지속시간 (이동속도 버프)
        // VFLight_WindowTimer: VF 라이트 발동 가능 시간
        //
        // 쿨타임은 E 종료 후 시작 (EndSkillE에서)
        // ═══════════════════════════════════════════════════════════════
        SkillE_DurationTimer = TickTimer.CreateFromSeconds(Runner, SkillE_Duration);
        VFLight_WindowTimer = TickTimer.CreateFromSeconds(Runner, VFLight_Window);

        // E 스킬 애니메이션
        animationController.RPC_Multi_Skill_E();
    }

    /// <summary>
    /// E 스킬 종료 처리
    /// FixedUpdateNetwork()에서 SkillE_DurationTimer 만료 시 호출
    /// </summary>
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

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // TryApplyVFLight() - VF 라이트 투사체 발사
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 호출 시점:
    // - Eva_Q, Eva_W, Eva_R 등에서 스킬이 적중했을 때 호출
    //
    // 조건:
    // 1. IsVFLightReady가 true (E 스킬 사용 후 윈도우 내)
    // 2. 타겟이 유효함
    // 3. 서버(Host)에서만 스폰
    //
    // 서버 체크가 중요한 이유:
    // - 클라이언트도 스킬 적중 판정을 할 수 있음 (예측)
    // - 클라이언트가 스폰하면 중복 생성됨
    // - Runner.IsServer로 서버에서만 스폰
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
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

    // HeroSkill 추상 메서드 구현 (미사용)
    protected override void Skill_Q() {}
    protected override void Skill_W() {}
    protected override void Skill_R() {}

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // R 스킬 - 지속형 토글 스킬
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 작동 방식 (토글):
    // - 첫 R 입력: 활성화 (스킬 오브젝트 생성, 마우스 추적)
    // - 두 번째 R 입력: 비활성화 (스킬 오브젝트 제거)
    //
    // 특이점:
    // - 다른 스킬과 달리 IsCasting이 true인 동안 유지
    // - FixedUpdateNetwork()에서 매 틱 마우스 방향으로 회전
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void Skill_R(HeroInput _heroInput)
    {
        if (IsCasting && !IsActivating_R) return;

        if (!IsActivating_R)
        {
            // ═══════════════════════════════════════════════════════════
            // R 스킬 활성화
            // ═══════════════════════════════════════════════════════════
            IsCasting = true;
            IsActivating_R = true;

            // 스킬 방향 계산
            _skillQDir = _heroInput.HitPosition_Skill - transform.position;
            _skillQDir = new Vector3(_skillQDir.x, 0, _skillQDir.z);

            // 회전 동기화
            heroMovement.SetLookDirection(_skillQDir.normalized);

            // 스킬 오브젝트 스폰
            var dir = Quaternion.LookRotation(_skillQDir);
            skillR_Dummy = Runner.Spawn(_skillR, transform.position, dir);

            skillR_Dummy.GetComponent<Eva_R>().Init(_heroInput.Owner, this);  // VF 라이트용 참조 전달

            // NetworkTransform으로 위치/회전 강제 설정
            var nt = skillR_Dummy.GetComponent<NetworkTransform>();
            nt.Teleport(transform.position, dir);

            RPC_Skill_R_Activate_Init(skillR_Dummy);

            // 애니메이션 시작
            animationController.RPC_Multi_Skill_R_Activate_Animation();
            heroMovement.IsCastingSkill = true;
        }
        else
        {
            // ═══════════════════════════════════════════════════════════
            // R 스킬 비활성화
            // ═══════════════════════════════════════════════════════════
            Runner.Despawn(skillR_Dummy);

            IsCasting = false;
            IsActivating_R = false;
            animationController.RPC_Multi_Skill_R_Deactivate_Animation();
            heroMovement.IsCastingSkill = false;
        }
    }

    /// <summary>
    /// R 스킬 오브젝트 초기화 RPC
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_Skill_R_Activate_Init(NetworkObject no)
    {
        var evaR = no.GetComponent<Eva_R>();
        evaR.ActiveInit().Forget();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // CancelBasicAttack() - 기본 공격 캔슬 헬퍼
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 호출 시점:
    // - 땅 우클릭 (타겟 해제)
    // - 스킬 사용 (스킬 우선)
    // - E 스킬 시작
    //
    // 캔슬 애니메이션:
    // - 사거리 내에서 공격 중이었을 때만 캔슬 애니메이션 재생
    // - 추적 중이었으면 (IsInAttackRange=false) 애니메이션 캔슬 불필요
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void CancelBasicAttack()
    {
        if (CurrentTargetId.IsValid)
        {
            if (IsInAttackRange)
            {
                // 사거리 내 공격 중 → 캔슬 애니메이션
                animationController.RPC_Multi_CancelBasicAttack();
            }
            CurrentTargetId = default;  // 타겟 해제
            IsInAttackRange = false;
            heroMovement.IsAttacking = false;  // 이동 가능하게
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // ProcessBasicAttack() - 기본 공격 처리
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 매 틱 호출 (CurrentTargetId가 유효할 때)
    //
    // 처리 순서:
    // 1. 타겟 오브젝트 찾기 (Runner.TryFindObject)
    // 2. 타겟 생존 확인
    // 3. 거리 계산
    // 4. 사거리 밖 → 추적 이동
    // 5. 사거리 안 → 멈추고 공격
    //
    // 왜 NetworkId로 관리하나?
    // - NetworkObject 직접 참조는 네트워크 동기화 불가
    // - NetworkId는 [Networked]로 동기화 가능
    // - TryFindObject()로 실제 오브젝트 찾음
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void ProcessBasicAttack()
    {
        // ═══════════════════════════════════════════════════════════════
        // 타겟 오브젝트 찾기
        //
        // TryFindObject(): NetworkId로 NetworkObject 찾기
        // - 성공: targetObject에 결과 저장, true 반환
        // - 실패: 타겟이 없어짐 (사망, 디스폰 등)
        // ═══════════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════════
        // 실제 캐릭터 위치 가져오기
        //
        // 왜 transform.position이 아닌 KCC 위치?
        // - KCC (Kinematic Character Controller)가 실제 충돌 위치
        // - transform은 루트 오브젝트, KCC는 캐릭터 본체
        // - 정확한 사거리 판정에 필수
        // ═══════════════════════════════════════════════════════════════
        var targetMovement = targetObject.GetComponentInChildren<HeroMovement>();
        Vector3 targetPosition = targetMovement != null
            ? targetMovement.GetKcc().Position
            : targetObject.transform.position;
        Vector3 myPosition = transform.position;
        float distance = Vector3.Distance(myPosition, targetPosition);

        // ═══════════════════════════════════════════════════════════════
        // 사거리 밖이면 타겟 방향으로 이동
        // ═══════════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════════
        // 사거리 안: 멈추고 공격
        // ═══════════════════════════════════════════════════════════════
        IsInAttackRange = true;
        heroMovement.IsAttacking = true;  // 이동 정지

        // 쿨다운 완료 시 공격
        if (BasicAttackCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            // 타겟 방향으로 회전 (동기화됨)
            Vector3 dir = (targetPosition - myPosition).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
            {
                heroMovement.SetLookDirection(dir);
            }

            // 공격 애니메이션
            animationController.RPC_Multi_BasicAttack();

            // ═══════════════════════════════════════════════════════════
            // 데미지 적용
            //
            // IDamageProcess 인터페이스 사용:
            // - 모든 데미지 받는 오브젝트가 구현
            // - OnTakeDamage(float damage) 호출로 데미지 적용
            // ═══════════════════════════════════════════════════════════
            var damageProcess = targetObject.GetComponentInChildren<IDamageProcess>();
            if (damageProcess != null)
            {
                damageProcess.OnTakeDamage(BasicAttackDamage);
            }

            // 쿨다운 시작
            BasicAttackCooldownTimer = TickTimer.CreateFromSeconds(Runner, BasicAttackCooldown);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Phase 2: Input Buffering 메서드들
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // Input Buffering 시스템 개요:
    //
    // [문제]
    // 스킬 시전 중 다음 스킬을 누르면 "씹힘" 발생
    // 예: Q 시전 중 W 누름 → Q 끝나면 W가 안 나감
    //
    // [해결]
    // 1. 스킬 시전 중 입력을 버퍼에 저장
    // 2. 시전 완료 후 버퍼에서 꺼내서 실행
    //
    // [버퍼 구조]
    // - FIFO (First In First Out) 큐
    // - 입력 시간 기록 → 오래된 입력 자동 제거
    // - 설정 가능한 버퍼 시간 (보통 0.2~0.5초)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════════════
        // Q 입력 버퍼링
        //
        // WasPressed() 체크:
        // - 이전 프레임: 버튼 안 눌림
        // - 현재 프레임: 버튼 눌림
        // - = "방금 눌렸다"
        //
        // ButtonsPreviousQ 체크:
        // - 버튼을 계속 누르고 있으면 매 프레임 버퍼링됨
        // - 한 번만 버퍼링하기 위해 별도 플래그 사용
        // ═══════════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════════
        // 버퍼에 저장된 마우스 위치로 임시 HeroInput 생성
        //
        // 왜 임시 HeroInput?
        // - Skill_Q(), Skill_W() 등은 HeroInput을 매개변수로 받음
        // - 버퍼에 저장된 위치로 스킬 발동해야 함
        // - 현재 마우스 위치가 아닌 "입력 당시" 위치 사용
        // ═══════════════════════════════════════════════════════════════
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
