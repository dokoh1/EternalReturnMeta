using System;
using Character.Player.ControlSettings;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Addons.SimpleKCC;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

public class HeroMovement : NetworkBehaviour
{
    private const int AirborneFrameDelayMs = 20;
    private const int AirborneFrameCount = 15;
    private const float ClickVfxHeightOffset = 0.2f;
    private const float ClickVfxLifetime = 1f;
    private const float TickRateBase = 60f;

    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private GameObject[] spawnSpots;
    [SerializeField] private GameObject ClickVFX;

    [HideInInspector] public NavMeshAgent navMeshAgent;
    private SimpleKCC kcc;

    [Networked] public NetworkButtons ButtonsPrevious { get; set; }
    [Networked] private float CurrentYaw { get; set; }
    [Networked] private Vector3 NetworkedPosition { get; set; }
    [Networked] public NetworkBool IsAirborne { get; set; }
    [Networked] private Vector3 NetworkedVelocity { get; set; }

    [Header("회전 설정")]
    [SerializeField] private float rotationDegreesPerSecond = 720f;
    [SerializeField] private float positionLerpSpeed = 15f;

    [Header("=== Control Settings ===")]
    [SerializeField] private ControlSettingsConfig _controlConfig;

    private HeroInput heroInput;
    public event Action<int> OnMoveVelocityChanged;
    public float baseSpeed;
    private NavMeshPath path;
    private Vector3 lastPos;

    public bool IsCastingSkill { get; set; }
    public bool IsDeath { get; set; }
    public bool IsAttacking { get; set; }
    public float SpeedMultiplier { get; set; } = 1.0f;
    public bool IsSlowImmune { get; set; }
    public bool IsPlayingSkillAnimation { get; set; }

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
        kcc = GetComponentInChildren<SimpleKCC>();

        IsCastingSkill = false;
        IsDeath = false;
    }

    public override void Spawned()
    {
        navMeshAgent.enabled = false;

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

            CurrentYaw = kcc.GetLookRotation(true, false).y;
        }
    }

    private void Update()
    {
        if (!HasInputAuthority)
            return;

        if (Input.GetKeyDown(KeyCode.Mouse1))
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, LayerMask.GetMask("Ground")))
            {
                var hitPosition = hit.point;
                var go = Instantiate(ClickVFX, hitPosition + new Vector3(0, ClickVfxHeightOffset, 0), Quaternion.identity);
                Destroy(go, ClickVfxLifetime);
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        PathCalculateAndMove();

        if (kcc.enabled)
        {
            NetworkedPosition = kcc.Position;
        }
    }

    private void PathCalculateAndMove()
    {
        if (IsDeath)
        {
            if (kcc.enabled) kcc.ResetVelocity();
            NetworkedVelocity = Vector3.zero;
            OnMoveVelocityChanged?.Invoke(0);
            return;
        }

        if (IsAirborne)
        {
            OnMoveVelocityChanged?.Invoke(0);
            return;
        }

        if (!navMeshAgent.enabled)
        {
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

        navMeshAgent.CalculatePath(lastPos, path);

        if (path.corners.Length <= 0)
        {
            OnMoveVelocityChanged?.Invoke(0);
        }

        if (path.corners != null && path.corners.Length > 0)
        {
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

            if (dist <= navMeshAgent.stoppingDistance && path.corners.Length <= 2)
            {
                NetworkedVelocity = Vector3.zero;
                OnMoveVelocityChanged?.Invoke(0);
                return;
            }

            var speed = baseSpeed * SpeedMultiplier * (TickRateBase / Runner.TickRate);
            var direction = (nextWaypoint - kcc.Position).normalized;

            if (!IsCastingSkill && !IsAttacking && !IsAirborne && kcc.enabled)
            {
                kcc.Move(direction * speed);
                NetworkedVelocity = direction * speed;
            }
            else
            {
                if (kcc.enabled) kcc.ResetVelocity();
                NetworkedVelocity = Vector3.zero;
                OnMoveVelocityChanged?.Invoke(0);
                return;
            }

            if (!IsPlayingSkillAnimation)
            {
                OnMoveVelocityChanged?.Invoke(1);
            }

            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            CurrentYaw = Mathf.MoveTowardsAngle(CurrentYaw, targetYaw, rotationDegreesPerSecond * Runner.DeltaTime);

            if (kcc.enabled)
            {
                float currentPitch = kcc.GetLookRotation(true, false).x;
                kcc.SetLookRotation(currentPitch, CurrentYaw);
            }
        }
    }

    public override void Render()
    {
        if (HasStateAuthority)
            return;

        if (!kcc.enabled)
            return;

        if (IsAirborne)
            return;

        if (NetworkedPosition != Vector3.zero)
        {
            Vector3 targetPos;
            bool useExtrapolation = _controlConfig != null && _controlConfig.EnableExtrapolation
                                    && NetworkedVelocity.sqrMagnitude > 0.01f;

            if (useExtrapolation)
            {
                float extTime = Mathf.Min(Runner.DeltaTime, _controlConfig.MaxExtrapolationTime);
                targetPos = NetworkedPosition + NetworkedVelocity * extTime;
            }
            else
            {
                targetPos = NetworkedPosition;
            }

            Vector3 smoothedPosition = Vector3.Lerp(
                kcc.Position,
                targetPos,
                positionLerpSpeed * Time.deltaTime
            );
            kcc.SetPosition(smoothedPosition);
        }

        float currentPitch = kcc.GetLookRotation(true, false).x;
        kcc.SetLookRotation(currentPitch, CurrentYaw);
    }

    public SimpleKCC GetKcc()
    {
        return kcc;
    }

    public void SetMovePosition(Vector3 position)
    {
        lastPos = position;
    }

    public void SetLookDirection(Vector3 direction)
    {
        if (!HasStateAuthority)
            return;
        if (direction == Vector3.zero)
            return;

        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        CurrentYaw = targetYaw;

        float currentPitch = kcc.GetLookRotation(true, false).x;
        kcc.SetLookRotation(currentPitch, CurrentYaw);
    }

    public void SetLookRotationFromQuaternion(Quaternion rotation)
    {
        if (!HasStateAuthority)
            return;

        Vector3 euler = rotation.eulerAngles;
        CurrentYaw = euler.y;

        float currentPitch = kcc.GetLookRotation(true, false).x;
        kcc.SetLookRotation(currentPitch, CurrentYaw);
    }

    public void ApplyAirborne(float height, float duration)
    {
        if (!HasStateAuthority)
            return;
        if (IsAirborne)
            return;

        SetAirborneState(duration).Forget();
        RPC_PlayAirborneVisual(height, duration);
    }

    private async UniTaskVoid SetAirborneState(float duration)
    {
        IsAirborne = true;

        await UniTask.Delay((int)(duration * 1000));

        if (this == null)
            return;

        IsAirborne = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAirborneVisual(float height, float duration)
    {
        PlayAirborneAnimation(height, duration).Forget();
    }

    private async UniTaskVoid PlayAirborneAnimation(float height, float duration)
    {
        bool wasKccEnabled = kcc.enabled;
        bool wasNavMeshEnabled = navMeshAgent.enabled;

        kcc.enabled = false;
        navMeshAgent.enabled = false;

        Vector3 originalPos = transform.position;

        for (int i = 1; i <= AirborneFrameCount; i++)
        {
            if (this == null) return;
            float t = (float)i / AirborneFrameCount;
            float easedT = t * t;
            Vector3 newPos = originalPos + new Vector3(0, height * easedT, 0);
            transform.position = newPos;
            await UniTask.Delay(AirborneFrameDelayMs);
        }

        int holdTime = (int)(duration * 1000) - 600;
        if (holdTime > 0)
        {
            await UniTask.Delay(holdTime);
        }

        if (this == null) return;

        Vector3 topPos = transform.position;
        for (int i = 1; i <= AirborneFrameCount; i++)
        {
            if (this == null) return;
            float t = (float)i / AirborneFrameCount;
            float easedT = t * t;
            Vector3 newPos = Vector3.Lerp(topPos, originalPos, easedT);
            transform.position = newPos;
            await UniTask.Delay(AirborneFrameDelayMs);
        }

        transform.position = originalPos;

        kcc.enabled = wasKccEnabled;
        if (HasStateAuthority)
        {
            navMeshAgent.enabled = wasNavMeshEnabled;
            kcc.SetPosition(originalPos);
        }
    }
}
