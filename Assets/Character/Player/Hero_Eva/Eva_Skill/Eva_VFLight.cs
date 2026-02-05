using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

public class Eva_VFLight : NetworkBehaviour
{
    [Header("VF 라이트 설정")]
    [SerializeField] public float speed = 20f;           // 이동 속도 (조절 가능)
    [SerializeField] public float arcHeight = 2f;        // 포물선 높이 (조절 가능)
    [SerializeField] private float damage = 20f;         // 데미지
    [SerializeField] private float maxLifeTime = 3f;     // 최대 생존 시간

    [Networked] private TickTimer lifeTimer { get; set; }
    [Networked] private Vector3 startPos { get; set; }
    [Networked] private Vector3 targetPos { get; set; }
    [Networked] private float progress { get; set; }     // 0~1 진행도
    [Networked] private NetworkId targetId { get; set; }
    [Networked] private NetworkBool isInitialized { get; set; }  // 네트워크 동기화

    private NetworkObject targetObject;

    public void Init(Vector3 start, NetworkObject target, float customSpeed = 0f, float customArcHeight = 0f)
    {
        startPos = start;
        targetObject = target;
        targetId = target.Id;

        // 타겟의 현재 위치 저장
        var targetMovement = target.GetComponentInChildren<HeroMovement>();
        if (targetMovement != null)
        {
            targetPos = targetMovement.GetKcc().Position + Vector3.up;  // 몸통 높이
        }
        else
        {
            targetPos = target.transform.position + Vector3.up;
        }

        // 커스텀 값이 있으면 적용
        if (customSpeed > 0f) speed = customSpeed;
        if (customArcHeight > 0f) arcHeight = customArcHeight;

        progress = 0f;
        isInitialized = true;

        // 즉시 초기 위치 적용
        transform.position = start;
        Vector3 dir = (targetPos - start).normalized;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    public override void Spawned()
    {
        lifeTimer = TickTimer.CreateFromSeconds(Runner, maxLifeTime);
    }

    public override void FixedUpdateNetwork()
    {
        if (!isInitialized) return;

        // === 서버(Host) 전용 로직 ===
        if (HasStateAuthority)
        {
            // 수명 만료
            if (lifeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
                return;
            }

            // 타겟 오브젝트 찾기 (추적용)
            if (targetObject == null && targetId.IsValid)
            {
                Runner.TryFindObject(targetId, out targetObject);
            }

            // 타겟 위치 업데이트 (추적)
            if (targetObject != null)
            {
                var targetMovement = targetObject.GetComponentInChildren<HeroMovement>();
                if (targetMovement != null)
                {
                    targetPos = targetMovement.GetKcc().Position + Vector3.up;  // 몸통 높이
                }
                else
                {
                    targetPos = targetObject.transform.position + Vector3.up;
                }
            }

            // 진행도 계산
            float distance = Vector3.Distance(startPos, targetPos);
            if (distance < 0.1f) distance = 0.1f;  // 0으로 나누기 방지
            float travelTime = distance / speed;
            progress += Runner.DeltaTime / travelTime;

            if (progress >= 1f)
            {
                // 도착 - 데미지 적용
                ApplyDamage();
                Runner.Despawn(Object);
                return;
            }
        }

        // 위치/회전 업데이트 (서버에서)
        if (HasStateAuthority)
        {
            UpdateVisualPosition();
        }
    }

    // Render()는 매 프레임 호출됨 - 클라이언트에서 부드러운 비주얼
    public override void Render()
    {
        if (!isInitialized) return;
        if (startPos == Vector3.zero && targetPos == Vector3.zero) return;  // 아직 동기화 안됨

        UpdateVisualPosition();
    }

    private void UpdateVisualPosition()
    {
        // 포물선 위치 계산
        Vector3 currentPos = CalculateParabolicPosition(progress);
        transform.position = currentPos;

        // 이동 방향으로 회전
        Vector3 nextPos = CalculateParabolicPosition(Mathf.Min(progress + 0.05f, 1f));
        Vector3 direction = (nextPos - currentPos).normalized;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private Vector3 CalculateParabolicPosition(float t)
    {
        // 선형 보간 위치
        Vector3 linearPos = Vector3.Lerp(startPos, targetPos, t);

        // 포물선 높이 추가 (sin 곡선으로 부드러운 아치)
        float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;

        return linearPos + new Vector3(0, arc, 0);
    }

    private void ApplyDamage()
    {
        if (targetObject == null) return;

        var damageProcess = targetObject.GetComponentInChildren<IDamageProcess>();
        var heroState = targetObject.GetComponentInChildren<HeroState>();

        if (damageProcess != null && heroState != null && heroState.GetCurrHealth() > 0f)
        {
            damageProcess.OnTakeDamage(damage);
        }
    }
}
