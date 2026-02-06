// ╔═══════════════════════════════════════════════════════════════════════════════╗
// ║                           HeroMovement.cs                                      ║
// ║                                                                                 ║
// ║  역할: 캐릭터 이동, 회전, 에어본(CC기) 처리 및 네트워크 동기화                    ║
// ║                                                                                 ║
// ║  사용된 기술:                                                                   ║
// ║  1. Photon Fusion - NetworkBehaviour 기반 네트워크 동기화                       ║
// ║  2. SimpleKCC - Kinematic Character Controller (물리 기반 이동)                 ║
// ║  3. NavMeshAgent - AI 경로 탐색 (서버에서만 사용)                               ║
// ║  4. [Networked] 변수 - 자동 상태 동기화                                         ║
// ║  5. RPC (Remote Procedure Call) - 1회성 이벤트 전송                             ║
// ║  6. UniTask - 비동기 애니메이션 처리                                            ║
// ║  7. Render() 보간 - 클라이언트 부드러운 시각 효과                               ║
// ║                                                                                 ║
// ║  네트워크 아키텍처: Server-Authoritative (서버 권위 모델)                        ║
// ║  - 서버(Host)가 모든 게임 로직 처리                                             ║
// ║  - 클라이언트는 입력 전송 + 시각 효과만 담당                                    ║
// ╚═══════════════════════════════════════════════════════════════════════════════╝

using System;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Addons.SimpleKCC;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 캐릭터 이동 및 회전을 담당하는 핵심 컴포넌트
///
/// 상속: NetworkBehaviour
/// - Photon Fusion의 네트워크 동기화 기능 사용
/// - FixedUpdateNetwork(): 네트워크 틱마다 호출 (서버 로직)
/// - Render(): 매 프레임 호출 (클라이언트 시각 효과)
/// </summary>
public class HeroMovement : NetworkBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════
    // 인스펙터 설정 변수
    // ═══════════════════════════════════════════════════════════════════════════

    [SerializeField] private LayerMask groundLayer;      // 바닥 감지용 레이어 마스크
    [SerializeField] private GameObject[] spawnSpots;    // 플레이어 스폰 위치 배열
    [SerializeField] private GameObject ClickVFX;        // 우클릭 시 표시되는 이동 목표 이펙트

    // ═══════════════════════════════════════════════════════════════════════════
    // 이동 시스템 컴포넌트
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// NavMeshAgent: Unity의 AI 경로 탐색 시스템
    ///
    /// 왜 사용하나?
    /// - MOBA 게임은 클릭 위치로 자동 이동해야 함
    /// - 장애물을 피해서 최적 경로를 찾아야 함
    /// - NavMesh가 이미 구워진(baked) 맵에서 경로 계산
    ///
    /// 주의: 서버에서만 활성화 (클라이언트는 결과만 받음)
    /// </summary>
    [HideInInspector] public NavMeshAgent navMeshAgent;

    /// <summary>
    /// SimpleKCC: Fusion용 Kinematic Character Controller
    ///
    /// 왜 사용하나?
    /// - 네트워크 게임에서 물리 기반 이동은 동기화가 어려움
    /// - KCC는 직접 위치/속도를 제어하므로 예측 가능
    /// - Fusion과 통합되어 네트워크 동기화 지원
    ///
    /// KCC vs Rigidbody:
    /// - Rigidbody: 물리 엔진이 위치 결정 (비결정적)
    /// - KCC: 코드가 직접 위치 결정 (결정적, 네트워크 친화적)
    /// </summary>
    private SimpleKCC kcc;

    // ═══════════════════════════════════════════════════════════════════════════
    // 네트워크 동기화 변수 ([Networked])
    //
    // [Networked] 속성의 특징:
    // 1. 서버에서 값을 변경하면 자동으로 모든 클라이언트에 동기화
    // 2. 새로 접속한 플레이어도 현재 상태를 즉시 알 수 있음
    // 3. 패킷 유실 시 다음 틱에 자동 복구
    //
    // vs RPC:
    // - RPC는 1회성 이벤트 (유실되면 복구 안됨)
    // - [Networked]는 지속 상태 (항상 최신 값 유지)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 이전 프레임의 버튼 상태 (WasPressed 판정용)
    /// </summary>
    [Networked] public NetworkButtons ButtonsPrevious { get; set; }

    /// <summary>
    /// 현재 캐릭터의 Y축 회전값 (Yaw)
    ///
    /// 왜 직접 관리하나?
    /// - kcc.GetLookRotation()이 SetLookRotation()과 다른 값을 반환할 수 있음
    /// - Fusion의 보간 시스템 때문에 렌더링용 값과 시뮬레이션용 값이 다름
    /// - 직접 관리해야 MoveTowardsAngle 보간이 정확하게 작동
    /// </summary>
    [Networked] private float CurrentYaw { get; set; }

    /// <summary>
    /// 네트워크 동기화용 위치
    ///
    /// 왜 필요한가?
    /// - KCC 위치는 자동으로 동기화되지만 보간이 없음
    /// - 직접 관리해서 클라이언트에서 Lerp 보간 적용
    /// - 결과: 원격 플레이어가 부드럽게 이동하는 것처럼 보임
    /// </summary>
    [Networked] private Vector3 NetworkedPosition { get; set; }

    /// <summary>
    /// 에어본(공중에 띄워진) 상태
    ///
    /// 왜 [Networked]인가?
    /// - 에어본 중에는 이동/스킬 사용 불가
    /// - 모든 클라이언트가 이 상태를 알아야 올바른 판정 가능
    /// - 새로 접속한 플레이어도 에어본 상태를 즉시 파악
    /// </summary>
    [Networked] public NetworkBool IsAirborne { get; set; }

    // ═══════════════════════════════════════════════════════════════════════════
    // 회전/이동 설정
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("회전 설정")]
    /// <summary>
    /// 초당 회전 각도 (도/초)
    ///
    /// 값 가이드:
    /// - 360: 1초에 1바퀴 (느림)
    /// - 720: 1초에 2바퀴 (적당함, 기본값)
    /// - 1440: 1초에 4바퀴 (빠름)
    /// - 9999: 거의 즉시 회전
    /// </summary>
    [SerializeField] private float rotationDegreesPerSecond = 720f;

    /// <summary>
    /// 위치 보간 속도 (클라이언트용)
    ///
    /// 높을수록: 빠르게 목표 위치에 도달 (반응성 좋음, 약간 뚝뚝함)
    /// 낮을수록: 천천히 목표 위치에 도달 (부드러움, 지연 느낌)
    /// </summary>
    [SerializeField] private float positionLerpSpeed = 15f;

    // ═══════════════════════════════════════════════════════════════════════════
    // 로컬 상태 변수 (네트워크 동기화 안됨)
    //
    // 왜 [Networked]가 아닌가?
    // - 서버에서만 사용하는 변수들
    // - 또는 각 클라이언트가 독립적으로 관리하는 변수들
    // ═══════════════════════════════════════════════════════════════════════════

    private HeroInput heroInput;                    // 현재 프레임의 입력 데이터
    public event Action<int> OnMoveVelocityChanged; // 이동 애니메이션 트리거용 이벤트
    public float baseSpeed;                         // 기본 이동 속도
    private NavMeshPath path;                       // 계산된 경로 저장
    private Vector3 lastPos;                        // 마지막 이동 목표 위치

    // ═══════════════════════════════════════════════════════════════════════════
    // 상태 플래그
    //
    // TODO: IsCastingSkill, IsAttacking, SpeedMultiplier는 [Networked]로 변경 권장
    // 현재는 서버에서만 사용하지만, 클라이언트 예측 구현 시 필요
    // ═══════════════════════════════════════════════════════════════════════════

    public bool IsCastingSkill { get; set; }            // 스킬 시전 중 (이동 불가)
    public bool IsDeath { get; set; }                   // 사망 상태
    public bool IsAttacking { get; set; }              // 기본 공격 중 (이동 불가)
    public float SpeedMultiplier { get; set; } = 1.0f; // 이동 속도 배율 (슬로우: 0.65 등)
    public bool IsSlowImmune { get; set; }             // 슬로우 면역 (E 스킬)
    public bool IsPlayingSkillAnimation { get; set; }  // 스킬 애니메이션 재생 중

    // ═══════════════════════════════════════════════════════════════════════════
    // Unity 생명주기: Awake
    // ═══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // 컴포넌트 캐싱 (매번 GetComponent 호출하면 성능 저하)
        navMeshAgent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
        kcc = GetComponentInChildren<SimpleKCC>();

        // 상태 초기화
        IsCastingSkill = false;
        IsDeath = false;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Fusion 생명주기: Spawned
    //
    // 언제 호출되나?
    // - NetworkObject가 네트워크에 스폰될 때
    // - 서버와 모든 클라이언트에서 호출됨
    //
    // 주요 권한 체크:
    // - HasInputAuthority: 이 오브젝트의 입력을 보내는 플레이어인가?
    // - HasStateAuthority: 이 오브젝트의 상태를 결정하는 권한이 있는가? (보통 서버)
    // ═══════════════════════════════════════════════════════════════════════════

    public override void Spawned()
    {
        // 모든 클라이언트에서 NavMeshAgent 비활성화
        // 이유: 경로 계산은 서버에서만 해야 함 (서버 권위 모델)
        navMeshAgent.enabled = false;

        // 로컬 플레이어가 아닌 경우 AudioListener 비활성화
        // 이유: AudioListener가 여러 개 있으면 Unity가 경고 발생
        var audioListener = GetComponentInChildren<AudioListener>();
        if (audioListener != null && !HasInputAuthority)
        {
            audioListener.enabled = false;
        }

        // ═══════════════════════════════════════════════════════════════
        // 로컬 플레이어 설정 (카메라)
        // HasInputAuthority = 이 클라이언트가 이 캐릭터를 조작하는가?
        //내 캐릭터 (A)                                                    
        // - HasInputAuthority = true                                      
        // - 내 카메라가 이 캐릭터를 따라다녀야 함 ✓                     
        // - 카메라 설정 필요!                                            
        
        //상대 캐릭터 (B)                                                  
        //- HasInputAuthority = false                                       
        //- 내 카메라가 이걸 따라다니면 안 됨!                              
        //- 카메라 설정 불필요 
        // ═══════════════════════════════════════════════════════════════
        if (HasInputAuthority)
        {
            var cameraController = GetComponent<CameraController>();
            var heroCameraPoint = cameraController.CameraPoint.GetComponent<HeroCameraPoint>();

            // 스폰 위치 결정 (플레이어 번호에 따라 다른 위치)
            var playerInfo = Runner.LocalPlayer.ToString();
            var initPos = spawnSpots[int.Parse(playerInfo[playerInfo.Length - 2].ToString()) % 2].transform.position;
            heroCameraPoint.InitPos(initPos);

            // Cinemachine 카메라 연결
            GameObject obj = GameObject.FindWithTag("CinemachineCamera");
            cameraController._CinemachineCamera = obj.GetComponent<CinemachineCamera>();
            cameraController._CinemachineCamera.Target.TrackingTarget = heroCameraPoint.transform;
            cameraController._CinemachineCamera.transform.position = heroCameraPoint.transform.position;
            cameraController._CinemachineCamera.Target.TrackingTarget = null;
        }

        // ═══════════════════════════════════════════════════════════════
        // 서버 설정
        // HasStateAuthority = 이 오브젝트의 게임 로직을 처리하는 권한
        // Host 모드에서는 호스트가 StateAuthority를 가짐
        // ═══════════════════════════════════════════════════════════════
        if (HasStateAuthority)
        {
            var playerInfo = GetComponentInParent<NetworkObject>().InputAuthority.ToString();
            var initPos = spawnSpots[int.Parse(playerInfo[playerInfo.Length - 2].ToString()) % 2].transform.position;

            lastPos = initPos;              // 이동 목표 = 초기 위치
            kcc.SetPosition(initPos);       // KCC 위치 설정
            navMeshAgent.enabled = true;    // 서버에서만 NavMesh 활성화

            // 초기 회전값 저장 (CurrentYaw 동기화용)
            CurrentYaw = kcc.GetLookRotation(true, false).y;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Unity Update: 로컬 입력 처리 (VFX)
    //
    // 왜 Update에서 처리하나?
    // - 클릭 이펙트는 네트워크와 무관한 로컬 시각 효과
    // - 즉각적인 피드백을 위해 입력 즉시 처리
    // ═══════════════════════════════════════════════════════════════════════════

    private void Update()
    {
        // 이 캐릭터를 조작하는 플레이어만 실행
        if (!HasInputAuthority) 
            return;

        // 우클릭 시 이동 목표 위치에 VFX 표시
        if (Input.GetKeyDown(KeyCode.Mouse1))
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, LayerMask.GetMask("Ground")))
            {
                var hitPosition = hit.point;
                var go = Instantiate(ClickVFX, hitPosition + new Vector3(0, 0.2f, 0), Quaternion.identity);
                Destroy(go, 1f);  // 1초 후 자동 삭제
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Fusion 생명주기: FixedUpdateNetwork
    //
    // 핵심 게임 로직 처리 (서버에서만)
    //
    // 호출 빈도: Runner.TickRate (기본 60Hz)
    //
    // 왜 Update가 아닌 FixedUpdateNetwork인가?
    // - 네트워크 게임은 모든 클라이언트가 같은 타이밍에 같은 로직을 실행해야 함
    // - Update는 프레임레이트에 따라 호출 빈도가 다름
    // - FixedUpdateNetwork는 고정 틱레이트로 결정적(deterministic) 실행
    // ═══════════════════════════════════════════════════════════════════════════

    public override void FixedUpdateNetwork()
    {
        // 서버(StateAuthority)에서만 게임 로직 실행
        // 클라이언트는 결과만 받아서 표시
        if (!HasStateAuthority)
            return;

        // 경로 계산 및 이동 처리
        PathCalculateAndMove();

        // ═══════════════════════════════════════════════════════════════
        // 위치 동기화
        //
        // 매 틱마다 현재 KCC 위치를 [Networked] 변수에 저장
        // → Fusion이 자동으로 모든 클라이언트에 동기화
        // → 클라이언트는 Render()에서 이 값으로 보간
        //
        // KCC가 비활성화 상태(에어본 중)면 위치 업데이트 스킵
        // → 에어본 전 위치가 유지됨
        // ═══════════════════════════════════════════════════════════════

        if (kcc.enabled)
        {
            NetworkedPosition = kcc.Position;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 경로 계산 및 이동 처리 (서버 전용)
    //
    // MOBA 스타일 이동:
    // 1. 우클릭 → 목표 위치 저장
    // 2. NavMesh로 경로 계산
    // 3. 경로를 따라 KCC로 이동
    // 4. 이동 방향으로 회전
    // ═══════════════════════════════════════════════════════════════════════════

    private void PathCalculateAndMove()
    {
        // ═══════════════════════════════════════════════════════════════
        // 상태 체크: 이동 불가 상황들
        // ═══════════════════════════════════════════════════════════════

        // 사망 상태
        if (IsDeath)
        {
            if (kcc.enabled) kcc.ResetVelocity();  // KCC가 켜져있을 때만
            OnMoveVelocityChanged?.Invoke(0);      // Idle 애니메이션
            return;
        }

        // 에어본 상태 (공중에 띄워짐)
        // NavMeshAgent, KCC가 비활성화될 수 있으므로 여기서 반드시 return
        if (IsAirborne)
        {
            OnMoveVelocityChanged?.Invoke(0);
            return;
        }

        // NavMeshAgent가 비활성화 상태면 경로 계산 불가
        if (!navMeshAgent.enabled)
        {
            return;
        }

        // ═══════════════════════════════════════════════════════════════
        // 입력 처리
        //
        // GetInput(): Fusion의 입력 시스템
        // - 클라이언트가 보낸 입력을 서버에서 읽음
        // - 입력 지연을 최소화하기 위한 예측/롤백 지원
        // ═══════════════════════════════════════════════════════════════

        if (GetInput(out heroInput))
        {
            // 우클릭 위치가 있으면 이동 목표 업데이트
            if (heroInput.HitPosition_RightClick != Vector3.zero)
            {
                lastPos = heroInput.HitPosition_RightClick;
            }
        }
        ButtonsPrevious = heroInput.Buttons;  // 다음 프레임 비교용

        // ═══════════════════════════════════════════════════════════════
        // NavMesh 경로 계산
        //
        // CalculatePath(): 현재 위치 → 목표 위치 경로 계산
        // path.corners: 경로의 꺾이는 지점들 (waypoints)
        // ═══════════════════════════════════════════════════════════════

        navMeshAgent.CalculatePath(lastPos, path);

        if (path.corners.Length <= 0)
        {
            // 경로 없음 = 이동할 곳 없음
            OnMoveVelocityChanged?.Invoke(0);  // Idle 애니메이션
        }

        if (path.corners != null && path.corners.Length > 0)
        {
            // ═══════════════════════════════════════════════════════════
            // 다음 웨이포인트 결정
            //
            // corners[0] = 현재 위치
            // corners[1] = 다음 목표 지점
            // ═══════════════════════════════════════════════════════════

            Vector3 nextWaypoint;
            if (path.corners.Length == 1)
            {
                nextWaypoint = path.corners[0];
            }
            else
            {
                nextWaypoint = path.corners[1];
            }

            var dist = Vector3.Distance(kcc.Position, nextWaypoint);

            // 목표 도착 체크
            if (dist <= navMeshAgent.stoppingDistance && path.corners.Length <= 2)
            {
                OnMoveVelocityChanged?.Invoke(0);  // Idle 애니메이션
                return;
            }

            // ═══════════════════════════════════════════════════════════
            // 이동 속도 계산
            //
            // baseSpeed: 기본 속도
            // SpeedMultiplier: 슬로우/버프 배율
            // (60f / Runner.TickRate): 틱레이트 보정
            //   - 60틱이면 ×1, 30틱이면 ×2
            //   - 어떤 틱레이트에서도 같은 체감 속도
            // ═══════════════════════════════════════════════════════════

            var speed = baseSpeed * SpeedMultiplier * (60f / Runner.TickRate);
            var direction = (nextWaypoint - kcc.Position).normalized;

            // ═══════════════════════════════════════════════════════════
            // 이동 실행
            //
            // IsCastingSkill, IsAttacking: 이동 불가 상태
            // kcc.Move(): KCC로 이동 (물리 기반)
            // kcc.ResetVelocity(): 즉시 정지
            // ═══════════════════════════════════════════════════════════

            if (!IsCastingSkill && !IsAttacking && !IsAirborne && kcc.enabled)
            {
                kcc.Move(direction * speed);
            }
            else
            {
                // 스킬 사용, 공격 중, 에어본, 또는 KCC 비활성화면 이동 중지
                if (kcc.enabled) kcc.ResetVelocity();
                OnMoveVelocityChanged?.Invoke(0);
                return;
            }

            // 이동 애니메이션 트리거 (스킬 애니메이션 중이 아닐 때만)
            if (!IsPlayingSkillAnimation)
            {
                OnMoveVelocityChanged?.Invoke(1);  // Walk 애니메이션
            }

            // ═══════════════════════════════════════════════════════════
            // 부드러운 회전 (Smooth Rotation)
            //
            // 기술: MoveTowardsAngle
            // - 현재 각도에서 목표 각도로 일정 속도로 회전
            // - 즉시 회전이 아닌 부드러운 회전
            //
            // 왜 Atan2를 사용하나?
            // - direction 벡터를 각도(도)로 변환
            // - Atan2(x, z): XZ 평면에서의 방향을 -180~180도로 반환
            // - Rad2Deg: 라디안 → 도 변환
            // ═══════════════════════════════════════════════════════════

            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            // MoveTowardsAngle: 현재 → 목표로 최대 delta만큼 이동
            // rotationDegreesPerSecond * Runner.DeltaTime = 이번 틱에 회전할 각도
            CurrentYaw = Mathf.MoveTowardsAngle(CurrentYaw, targetYaw, rotationDegreesPerSecond * Runner.DeltaTime);

            // KCC에 회전 적용 (KCC가 활성화 상태일 때만)
            if (kcc.enabled)
            {
                float currentPitch = kcc.GetLookRotation(true, false).x;
                kcc.SetLookRotation(currentPitch, CurrentYaw);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Fusion 생명주기: Render
    //
    // 클라이언트 시각 효과 처리
    //
    // 호출 빈도: 매 프레임 (모니터 주사율)
    //
    // FixedUpdateNetwork vs Render:
    // - FixedUpdateNetwork: 게임 로직 (60Hz 고정)
    // - Render: 시각 효과 (프레임레이트에 따라 가변)
    //
    // 왜 Render에서 보간하나?
    // - 네트워크 틱 (60Hz)보다 화면 주사율 (144Hz)이 높음
    // - 틱 사이 빈 시간을 보간으로 채워야 부드러움
    // ═══════════════════════════════════════════════════════════════════════════

    public override void Render()
    {
        // 서버는 FixedUpdateNetwork에서 이미 처리했으므로 스킵
        if (HasStateAuthority)
            return;

        // KCC가 비활성화 상태면 스킵 (에어본 애니메이션 중)
        // KCC.Render()는 enabled 상태에서만 호출되어야 함
        if (!kcc.enabled)
            return;

        // 에어본 중에는 별도의 애니메이션이 처리하므로 스킵
        if (IsAirborne)
            return;

        // ═══════════════════════════════════════════════════════════════
        // 위치 보간 (Interpolation)
        //
        // 기술: Vector3.Lerp
        // - 현재 위치에서 목표 위치로 점진적 이동
        // - positionLerpSpeed로 속도 조절
        //
        // 왜 필요한가?
        // - 서버에서 틱마다 위치가 업데이트됨 (불연속적)
        // - 그대로 적용하면 뚝뚝 끊기는 것처럼 보임
        // - Lerp로 중간 값을 채워서 부드럽게
        // ═══════════════════════════════════════════════════════════════

        if (NetworkedPosition != Vector3.zero)
        {
            Vector3 currentPos = kcc.Position;
            Vector3 smoothedPosition = Vector3.Lerp(
                currentPos,
                NetworkedPosition,
                positionLerpSpeed * Time.deltaTime
            );
            kcc.SetPosition(smoothedPosition);
        }

        // ═══════════════════════════════════════════════════════════════
        // 회전 적용
        //
        // 서버에서 동기화된 CurrentYaw를 KCC에 적용
        // 회전은 이미 서버에서 부드럽게 처리되었으므로 직접 적용
        // ═══════════════════════════════════════════════════════════════

        float currentPitch = kcc.GetLookRotation(true, false).x;
        kcc.SetLookRotation(currentPitch, CurrentYaw);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 공개 메서드: KCC 접근자
    // ═══════════════════════════════════════════════════════════════════════════

    public SimpleKCC GetKcc()
    {
        return kcc;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 공개 메서드: 이동 목표 설정
    //
    // 사용처: 기본 공격 시 타겟 방향으로 이동
    // ═══════════════════════════════════════════════════════════════════════════

    public void SetMovePosition(Vector3 position)
    {
        lastPos = position;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 공개 메서드: 회전 설정 (스킬/공격용)
    //
    // 왜 이 메서드가 필요한가?
    // - Eva_Skill 등에서 kcc.SetLookRotation()을 직접 호출하면
    //   CurrentYaw가 업데이트되지 않아 클라이언트에 동기화 안됨
    // - 이 메서드를 통해 회전하면 CurrentYaw도 업데이트됨
    //
    // SetLookDirection: 방향 벡터로 회전 (Q, W, 기본공격)
    // SetLookRotationFromQuaternion: Quaternion으로 회전 (R 스킬)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 방향 벡터로 즉시 회전 (스킬, 기본 공격용)
    /// </summary>
    /// <param name="direction">바라볼 방향 (XZ 평면)</param>
    public void SetLookDirection(Vector3 direction)
    {
        if (!HasStateAuthority) 
            return;  // 서버에서만 실행
        if (direction == Vector3.zero) 
            return;

        // 방향 → 각도 변환
        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        // 즉시 회전 (스킬은 부드러운 회전 불필요)
        CurrentYaw = targetYaw;

        // KCC에 적용
        float currentPitch = kcc.GetLookRotation(true, false).x;
        kcc.SetLookRotation(currentPitch, CurrentYaw);
    }

    /// <summary>
    /// Quaternion으로 즉시 회전 (R 스킬 지속 회전용)
    /// </summary>
    /// <param name="rotation">목표 회전</param>
    public void SetLookRotationFromQuaternion(Quaternion rotation)
    {
        if (!HasStateAuthority) 
            return;

        // Quaternion → Euler 각도 추출
        Vector3 euler = rotation.eulerAngles;
        CurrentYaw = euler.y;

        // KCC에 적용
        float currentPitch = kcc.GetLookRotation(true, false).x;
        kcc.SetLookRotation(currentPitch, CurrentYaw);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 에어본 (Airborne) 시스템
    //
    // CC기(Crowd Control): 적을 공중에 띄워 행동 불가 상태로 만듦
    //
    // ┌─────────────────────────────────────────────────────────────────────────┐
    // │  설계 철학: 서버 = 상태 관리, 클라이언트 = 비주얼 담당                  │
    // │                                                                         │
    // │  서버:                                                                  │
    // │  - IsAirborne = true 설정 (이동/스킬 막힘)                             │
    // │  - 네트워크상 위치는 그대로 유지 (KCC, NavMesh 건드리지 않음)          │
    // │  - duration 후 IsAirborne = false                                      │
    // │                                                                         │
    // │  클라이언트 (RPC로 호출):                                              │
    // │  - KCC, NavMesh 끄기 (서버 위치 동기화 방지)                           │
    // │  - 애니메이션 재생 (올라갔다 내려오기)                                 │
    // │  - KCC, NavMesh 켜기 → 서버 위치로 자동 복귀                           │
    // │                                                                         │
    // │  장점:                                                                  │
    // │  - 서버 로직 단순화 (상태만 관리)                                      │
    // │  - 네트워크 위치 동기화 문제 없음                                      │
    // │  - 코드 중복 제거                                                      │
    // └─────────────────────────────────────────────────────────────────────────┘
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 에어본 적용 (외부에서 호출)
    ///
    /// 호출 예시: Eva_W의 ApplyAirborneToCenter()에서 호출
    /// </summary>
    /// <param name="height">띄우는 높이</param>
    /// <param name="duration">총 지속 시간</param>
    public void ApplyAirborne(float height, float duration)
    {
        if (!HasStateAuthority)
            return;  // 서버에서만 실행
        if (IsAirborne)
            return;  // 이미 에어본 중이면 무시

        // 1. 상태 변경 (이동/스킬 막힘) + duration 후 복구 예약
        SetAirborneState(duration).Forget();

        // 2. 모든 클라이언트(서버 포함)에 시각 효과 전송
        RPC_PlayAirborneVisual(height, duration);
    }

    /// <summary>
    /// 서버용: 에어본 상태 관리 (비주얼 없음!)
    ///
    /// 위치는 건드리지 않음 - 어차피 IsAirborne이면 이동 불가
    /// </summary>
    private async UniTaskVoid SetAirborneState(float duration)
    {
        // 상태 변경 (자동으로 네트워크 동기화)
        IsAirborne = true;

        // duration만큼 대기
        await UniTask.Delay((int)(duration * 1000));

        // 오브젝트가 파괴되었으면 종료
        if (this == null) return;

        // 상태 복구
        IsAirborne = false;
    }

    /// <summary>
    /// RPC: 모든 클라이언트(서버 포함)에서 에어본 시각 효과 재생
    ///
    /// [Rpc] 속성:
    /// - RpcSources.StateAuthority: 서버만 이 RPC를 호출할 수 있음
    /// - RpcTargets.All: 서버 포함 모든 클라이언트에서 실행
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAirborneVisual(float height, float duration)
    {
        // 서버, 클라이언트 모두 동일한 시각 효과 실행
        PlayAirborneAnimation(height, duration).Forget();
    }

    /// <summary>
    /// 에어본 시각 효과 (서버/클라이언트 공용)
    ///
    /// 네트워크 위치는 서버가 관리하므로,
    /// 여기서는 화면에 보이는 애니메이션만 처리
    /// </summary>
    private async UniTaskVoid PlayAirborneAnimation(float height, float duration)
    {
        // ═══════════════════════════════════════════════════════════════
        // KCC, NavMesh 비활성화
        //
        // 왜 필요한가?
        // - 서버: 위치를 고정해두고 싶은데 KCC가 계속 동작하면 방해됨
        // - 클라이언트: 서버에서 오는 위치 동기화가 애니메이션 덮어쓰면 안됨
        // ═══════════════════════════════════════════════════════════════

        bool wasKccEnabled = kcc.enabled;
        bool wasNavMeshEnabled = navMeshAgent.enabled;

        kcc.enabled = false;
        navMeshAgent.enabled = false;

        Vector3 originalPos = transform.position;  // 현재 위치 저장
        int frameDelay = 20;  // 프레임 간격 (밀리초)

        // ═══════════════════════════════════════════════════════════════
        // 위로 올라가기 (EaseIn)
        //
        // EaseIn: 처음엔 천천히, 점점 빨라지는 보간
        // easedT = t * t: 0→0, 0.5→0.25, 1→1
        // ═══════════════════════════════════════════════════════════════

        int liftFrames = 15;
        for (int i = 1; i <= liftFrames; i++)
        {
            if (this == null) return;  // 오브젝트 파괴 체크
            float t = (float)i / liftFrames;
            float easedT = t * t;  // EaseIn
            Vector3 newPos = originalPos + new Vector3(0, height * easedT, 0);
            transform.position = newPos;
            await UniTask.Delay(frameDelay);
        }

        // ═══════════════════════════════════════════════════════════════
        // 공중 유지
        //
        // 총 duration에서 올라가기(300ms) + 내려오기(300ms) 제외
        // ═══════════════════════════════════════════════════════════════

        int holdTime = (int)(duration * 1000) - 600;  // 올라가기 + 내려오기 = 600ms
        if (holdTime > 0)
        {
            await UniTask.Delay(holdTime);
        }

        if (this == null) return;

        // ═══════════════════════════════════════════════════════════════
        // 아래로 내려오기 (EaseIn = 가속 낙하)
        // ═══════════════════════════════════════════════════════════════

        Vector3 topPos = transform.position;
        int dropFrames = 15;
        for (int i = 1; i <= dropFrames; i++)
        {
            if (this == null) return;
            float t = (float)i / dropFrames;
            float easedT = t * t;
            Vector3 newPos = Vector3.Lerp(topPos, originalPos, easedT);
            transform.position = newPos;
            await UniTask.Delay(frameDelay);
        }

        // 정확한 위치 복귀
        transform.position = originalPos;

        // ═══════════════════════════════════════════════════════════════
        // KCC, NavMesh 다시 활성화
        //
        // KCC가 켜지면 서버의 NetworkedPosition으로 자동 동기화됨
        // → 원래 위치로 복귀 (서버는 위치를 안 바꿨으니까)
        // ═══════════════════════════════════════════════════════════════

        kcc.enabled = wasKccEnabled;
        if (HasStateAuthority)
        {
            // 서버만: NavMesh 활성화 + KCC 위치 명시적 설정
            navMeshAgent.enabled = wasNavMeshEnabled;
            kcc.SetPosition(originalPos);
        }
    }
}
