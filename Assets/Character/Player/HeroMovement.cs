using System;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Addons.SimpleKCC;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class HeroMovement : NetworkBehaviour
{
    [SerializeField] private LayerMask groundLayer; // 바닥 레이어

    [SerializeField] private GameObject[] spawnSpots;

    [HideInInspector] public NavMeshAgent navMeshAgent;
    private SimpleKCC kcc;

    [Networked] public NetworkButtons ButtonsPrevious { get; set; }

    private HeroInput heroInput;
    public event Action<int> OnMoveVelocityChanged;

    public float baseSpeed;
    private NavMeshPath path;
    private Vector3 lastPos;

    // ═══════════════════════════════════════════════════════════════
    // 부드러운 회전 시스템
    //
    // 왜 CurrentYaw를 직접 관리하나?
    // - kcc.GetLookRotation()이 SetLookRotation()과 다른 값을 반환할 수 있음
    // - 그러면 MoveTowardsAngle이 매번 처음부터 계산해서 회전이 안됨
    // - 직접 CurrentYaw를 관리하면 이 문제 해결
    // ═══════════════════════════════════════════════════════════════
    [Header("회전 설정")]
    [SerializeField] private float rotationDegreesPerSecond = 720f;  // 초당 회전 각도 (높을수록 빠름)

    [Networked] private float CurrentYaw { get; set; }  // 현재 Yaw (서버에서 관리, 네트워크 동기화)

    public bool IsCastingSkill { get; set; }
    public bool IsDeath { get; set; }
    public bool IsAttacking { get; set; }  // 기본 공격 중 (이동 정지)
    public float SpeedMultiplier { get; set; } = 1.0f;  // 이동 속도 배율 (슬로우 등)
    [Networked] public NetworkBool IsAirborne { get; set; }  // 공중에 띄워진 상태 (네트워크 동기화)
    public bool IsSlowImmune { get; set; }  // 슬로우 면역 (E 스킬)
    public bool IsPlayingSkillAnimation { get; set; }  // 스킬 애니메이션 재생 중 (이동 애니메이션 차단)

    // 에어본 적용 (외부에서 호출 - 서버에서만)
    public void ApplyAirborne(float height, float duration)
    {
        if (!HasStateAuthority) return;  // 서버에서만 실행
        if (IsAirborne) return;  // 이미 에어본 중이면 무시

        // 서버에서 에어본 실행
        DoAirborne(height, duration).Forget();

        // 모든 클라이언트에 에어본 시각 효과 전송
        RPC_PlayAirborneVisual(height, duration);
    }

    // 모든 클라이언트에서 에어본 시각 효과 재생
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAirborneVisual(float height, float duration)
    {
        // 서버는 이미 DoAirborne에서 처리하므로 클라이언트만 실행
        if (HasStateAuthority) return;

        DoAirborneVisual(height, duration).Forget();
    }

    // 서버용: 실제 상태 + 시각 효과
    private async UniTaskVoid DoAirborne(float height, float duration)
    {
        IsAirborne = true;

        // NavMeshAgent 비활성화 (지면 고정 방지)
        bool wasNavMeshEnabled = navMeshAgent.enabled;
        navMeshAgent.enabled = false;

        // KCC 비활성화 (위치 덮어쓰기 방지)
        bool wasKccEnabled = kcc.enabled;
        Vector3 originalPos = kcc.Position;
        kcc.enabled = false;

        int frameDelay = 20;

        // 위로 올라가기 (0.3초)
        int liftFrames = 15;
        for (int i = 1; i <= liftFrames; i++)
        {
            if (this == null) return;
            float t = (float)i / liftFrames;
            float easedT = t * t;  // EaseIn
            Vector3 newPos = originalPos + new Vector3(0, height * easedT, 0);
            transform.position = newPos;
            await UniTask.Delay(frameDelay);
        }

        // 공중 유지 (짧게)
        int holdTime = (int)(duration * 1000) - 550;  // 올라가기 + 내려오기 시간 제외
        if (holdTime > 50)
        {
            await UniTask.Delay(50);  // 최소 체류 시간
        }

        if (this == null) return;

        // 아래로 내려오기 (0.3초)
        Vector3 topPos = transform.position;
        int dropFrames = 15;
        for (int i = 1; i <= dropFrames; i++)
        {
            if (this == null) return;
            float t = (float)i / dropFrames;
            float easedT = t * t;  // EaseIn (가속 낙하)
            Vector3 newPos = Vector3.Lerp(topPos, originalPos, easedT);
            transform.position = newPos;
            await UniTask.Delay(frameDelay);
        }

        // 정확한 위치 복귀
        transform.position = originalPos;

        // KCC 다시 활성화 및 위치 동기화
        kcc.enabled = wasKccEnabled;
        kcc.SetPosition(originalPos);

        // NavMeshAgent 다시 활성화
        navMeshAgent.enabled = wasNavMeshEnabled;

        IsAirborne = false;
    }

    // 클라이언트용: 시각 효과만 (로컬 위치 애니메이션)
    private async UniTaskVoid DoAirborneVisual(float height, float duration)
    {
        // 클라이언트에서도 KCC 비활성화해야 위치 애니메이션이 덮어쓰이지 않음
        bool wasKccEnabled = kcc.enabled;
        kcc.enabled = false;

        Vector3 originalPos = transform.position;
        int frameDelay = 20;

        // 위로 올라가기 (0.3초)
        int liftFrames = 15;
        for (int i = 1; i <= liftFrames; i++)
        {
            if (this == null) return;
            float t = (float)i / liftFrames;
            float easedT = t * t;
            Vector3 newPos = originalPos + new Vector3(0, height * easedT, 0);
            transform.position = newPos;
            await UniTask.Delay(frameDelay);
        }

        // 공중 유지
        int holdTime = (int)(duration * 1000) - 550;
        if (holdTime > 50)
        {
            await UniTask.Delay(50);
        }

        if (this == null) return;

        // 아래로 내려오기 (0.3초)
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

        transform.position = originalPos;

        // KCC 다시 활성화
        kcc.enabled = wasKccEnabled;
    }

    [SerializeField] private GameObject ClickVFX;
    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();

        kcc = GetComponentInChildren<SimpleKCC>();
        IsCastingSkill = false;
        IsDeath = false;
    }

    public SimpleKCC GetKcc()
    {
        return kcc;
    }

    // 이동 목표 위치 설정 (기본 공격 등에서 사용)
    public void SetMovePosition(Vector3 position)
    {
        lastPos = position;
    }

    public override void Spawned()
    {
        // 클라이언트 agent 비활성화
        navMeshAgent.enabled = false;

        // 로컬 플레이어가 아닌 경우 AudioListener 비활성화 (중복 방지)
        var audioListener = GetComponentInChildren<AudioListener>();
        if (audioListener != null && !HasInputAuthority)
        {
            audioListener.enabled = false;
        }

        if (HasInputAuthority)
        {
            var cameraController = GetComponent<CameraController>();
            var heroCameraPoint = cameraController.CameraPoint.GetComponent<HeroCameraPoint>();

            var playerInfo = Runner.LocalPlayer.ToString();
            var initPos = spawnSpots[int.Parse(playerInfo[playerInfo.Length - 2].ToString()) % 2].transform.position;
            heroCameraPoint.InitPos(initPos);

            GameObject obj = GameObject.FindWithTag("CinemachineCamera");
            cameraController._CinemachineCamera = obj.GetComponent<CinemachineCamera>();
            cameraController._CinemachineCamera.Target.TrackingTarget = heroCameraPoint.transform;
            cameraController._CinemachineCamera.transform.position = heroCameraPoint.transform.position;
            cameraController._CinemachineCamera.Target.TrackingTarget = null;
        }

        if (HasStateAuthority)
        {
            var playerInfo = GetComponentInParent<NetworkObject>().InputAuthority.ToString();
            var initPos = spawnSpots[int.Parse(playerInfo[playerInfo.Length - 2].ToString()) % 2].transform.position;
            lastPos = initPos;
            kcc.SetPosition(initPos);
            navMeshAgent.enabled = true;

            // 초기 회전값 설정
            CurrentYaw = kcc.GetLookRotation(true, false).y;
        }
    }

    private void Update()
    {
        if (!HasInputAuthority) return;

        if (Input.GetKeyDown(KeyCode.Mouse1))
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, LayerMask.GetMask("Ground")))
            {
                var hitPosition      = hit.point;
                var go = Instantiate(ClickVFX, hitPosition + new Vector3(0, 0.2f, 0), Quaternion.identity);
                Destroy(go, 1f);
            }
        }
    }
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        PathCalculateAndMove();
    }

    private void PathCalculateAndMove()
    {
        if (IsDeath)
        {
            kcc.ResetVelocity();
            OnMoveVelocityChanged?.Invoke(0);

            return;
        }

        // 에어본 중에는 경로 계산 스킵
        if (IsAirborne)
        {
            OnMoveVelocityChanged?.Invoke(0);
            return;
        }

        if (GetInput(out heroInput))
        {
            if (heroInput.HitPosition_RightClick != Vector3.zero)
            {
                lastPos = heroInput.HitPosition_RightClick;
            }
        }
        ButtonsPrevious = heroInput.Buttons;

        // 길찾기 경로 계산
        navMeshAgent.CalculatePath(lastPos, path);

        if (path.corners.Length <= 0)
        {   //정지 애니메이션
            OnMoveVelocityChanged?.Invoke(0);
        }

        if (path.corners != null && path.corners.Length > 0)
        {
            Vector3 nextWaypoint;//
            if (path.corners.Length == 1)
            {
                nextWaypoint = path.corners[0];
            }
            else
            {
                nextWaypoint = path.corners[1];
            }

            var dist = Vector3.Distance(kcc.Position, nextWaypoint);

            if (dist <= navMeshAgent.stoppingDistance && path.corners.Length <= 2)
            {
                OnMoveVelocityChanged?.Invoke(0);
                return;
            }

            var speed = baseSpeed * SpeedMultiplier * (60f / Runner.TickRate);
            var direction = (nextWaypoint - kcc.Position).normalized;


            if (!IsCastingSkill && !IsAttacking && !IsAirborne)
            {
                kcc.Move(direction * speed);
            }
            else  // 스킬 사용, 공격 중, 또는 에어본이면 이동 중지
            {
                kcc.ResetVelocity();
                OnMoveVelocityChanged?.Invoke(0);
                return;
            }

            // 스킬 애니메이션 재생 중이면 이동 애니메이션 차단
            if (!IsPlayingSkillAnimation)
            {
                OnMoveVelocityChanged?.Invoke(1);
            }

            // ═══════════════════════════════════════════════════════════════
            // 부드러운 회전 (Smooth Rotation)
            //
            // CurrentYaw를 직접 관리하는 이유:
            // - kcc.GetLookRotation()이 우리가 설정한 값과 다를 수 있음
            // - 직접 관리하면 매 프레임 정확한 보간 가능
            // ═══════════════════════════════════════════════════════════════
            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float currentPitch = kcc.GetLookRotation(true, false).x;

            // 현재 Yaw에서 목표 Yaw로 일정 속도로 회전
            CurrentYaw = Mathf.MoveTowardsAngle(CurrentYaw, targetYaw, rotationDegreesPerSecond * Runner.DeltaTime);

            kcc.SetLookRotation(currentPitch, CurrentYaw);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Render: 클라이언트에서 회전 적용
    //
    // CurrentYaw가 [Networked]이므로 서버 값이 자동 동기화됨
    // 클라이언트는 동기화된 CurrentYaw를 KCC에 적용
    // ═══════════════════════════════════════════════════════════════
    public override void Render()
    {
        // 서버는 FixedUpdateNetwork에서 이미 처리
        if (HasStateAuthority) return;

        // 에어본 중에는 회전 스킵
        if (IsAirborne) return;

        // 클라이언트: 네트워크에서 받은 CurrentYaw 적용
        float currentPitch = kcc.GetLookRotation(true, false).x;

        kcc.SetLookRotation(currentPitch, CurrentYaw);
    }
}
