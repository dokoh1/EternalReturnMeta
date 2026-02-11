using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

public class Eva_VFLight : NetworkBehaviour
{
    private const float LookAheadOffset = 0.05f;
    private const float MinDistance = 0.1f;

    [Header("VF 라이트 설정")]
    [SerializeField] public float speed = 20f;
    [SerializeField] public float arcHeight = 2f;
    [SerializeField] private float damage = 20f;
    [SerializeField] private float maxLifeTime = 3f;

    [Networked] private TickTimer lifeTimer { get; set; }
    [Networked] private Vector3 startPos { get; set; }
    [Networked] private Vector3 targetPos { get; set; }
    [Networked] private float progress { get; set; }
    [Networked] private NetworkId targetId { get; set; }
    [Networked] private NetworkBool isInitialized { get; set; }

    private NetworkObject targetObject;

    public void Init(Vector3 start, NetworkObject target, float customSpeed = 0f, float customArcHeight = 0f)
    {
        startPos = start;
        targetObject = target;
        targetId = target.Id;

        var targetMovement = target.GetComponentInChildren<HeroMovement>();
        if (targetMovement != null)
        {
            targetPos = targetMovement.GetKcc().Position + Vector3.up;
        }
        else
        {
            targetPos = target.transform.position + Vector3.up;
        }

        if (customSpeed > 0f) speed = customSpeed;
        if (customArcHeight > 0f) arcHeight = customArcHeight;

        progress = 0f;
        isInitialized = true;

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

        if (HasStateAuthority)
        {
            if (lifeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
                return;
            }

            if (targetObject == null && targetId.IsValid)
            {
                Runner.TryFindObject(targetId, out targetObject);
            }

            if (targetObject != null)
            {
                var targetMovement = targetObject.GetComponentInChildren<HeroMovement>();
                if (targetMovement != null)
                {
                    targetPos = targetMovement.GetKcc().Position + Vector3.up;
                }
                else
                {
                    targetPos = targetObject.transform.position + Vector3.up;
                }
            }

            float distance = Vector3.Distance(startPos, targetPos);
            if (distance < MinDistance) distance = MinDistance;
            float travelTime = distance / speed;
            progress += Runner.DeltaTime / travelTime;

            if (progress >= 1f)
            {
                ApplyDamage();
                Runner.Despawn(Object);
                return;
            }
        }

        if (HasStateAuthority)
        {
            UpdateVisualPosition();
        }
    }

    public override void Render()
    {
        if (!isInitialized) return;
        if (startPos == Vector3.zero && targetPos == Vector3.zero) return;

        UpdateVisualPosition();
    }

    private void UpdateVisualPosition()
    {
        Vector3 currentPos = CalculateParabolicPosition(progress);
        transform.position = currentPos;

        Vector3 nextPos = CalculateParabolicPosition(Mathf.Min(progress + LookAheadOffset, 1f));
        Vector3 direction = (nextPos - currentPos).normalized;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private Vector3 CalculateParabolicPosition(float t)
    {
        Vector3 linearPos = Vector3.Lerp(startPos, targetPos, t);
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
