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

    public bool IsCastingSkill { get; set; }
    public bool IsDeath { get; set; }
    public bool IsAttacking { get; set; }  // 기본 공격 중 (이동 정지)
    public float SpeedMultiplier { get; set; } = 1.0f;  // 이동 속도 배율 (슬로우 등)
    [Networked] public NetworkBool IsAirborne { get; set; }  // 공중에 띄워진 상태 (네트워크 동기화)
    public bool IsSlowImmune { get; set; }  // 슬로우 면역 (E 스킬)
    public bool IsPlayingSkillAnimation { get; set; }  // 스킬 애니메이션 재생 중 (이동 애니메이션 차단)

    // 에어본 적용 (외부에서 호출)
    public void ApplyAirborne(float height, float duration)
    {
        if (IsAirborne) return;  // 이미 에어본 중이면 무시
        DoAirborne(height, duration).Forget();
    }

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

            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float currentPitch = kcc.GetLookRotation(true, false).x;

            kcc.SetLookRotation(currentPitch, targetYaw);

        }
    }
}
