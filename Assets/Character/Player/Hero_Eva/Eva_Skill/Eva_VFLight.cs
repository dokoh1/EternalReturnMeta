// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Eva_VFLight.cs - E 스킬 추가 데미지 투사체
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 개요 (Overview)                                                                                      │
// │                                                                                                       │
// │ VF 라이트는 Eva의 E 스킬 효과로 발동되는 추가 데미지 투사체입니다.                                    │
// │ E 스킬 사용 후 일정 시간 내에 Q/W/R 스킬이 적중하면 자동으로 발사됩니다.                              │
// │                                                                                                       │
// │ 핵심 기술:                                                                                            │
// │ 1. [Networked] 변수 - 서버-클라이언트 간 투사체 상태 동기화                                           │
// │ 2. 포물선 이동 (Parabolic Movement) - 자연스러운 투사체 궤적                                          │
// │ 3. 타겟 추적 (Homing) - 대상을 따라가는 유도 투사체                                                   │
// │ 4. FixedUpdateNetwork + Render 분리 - 서버 로직 / 클라이언트 렌더링 분리                             │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 설계 철학 (Design Philosophy)                                                                        │
// │                                                                                                       │
// │ Q: 왜 타겟을 추적하나요?                                                                             │
// │ A: E 스킬의 추가 데미지가 확정적으로 들어가야 스킬 가치가 있음.                                       │
// │    회피 불가능한 추가 데미지 = E 스킬을 쓸 이유                                                       │
// │                                                                                                       │
// │ Q: 왜 포물선으로 이동하나요?                                                                         │
// │ A: 직선 이동은 밋밋함. 포물선은 시각적으로 아름답고 게임 느낌이 남.                                   │
// │    참고: 리그 오브 레전드의 TF W 카드도 포물선                                                        │
// │                                                                                                       │
// │ Q: 왜 progress를 0~1로 관리하나요?                                                                   │
// │ A: 거리와 무관하게 동일한 로직으로 처리 가능.                                                         │
// │    progress 0 = 시작, progress 1 = 도착                                                               │
// │    Vector3.Lerp(start, end, progress)로 쉽게 위치 계산                                                │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Eva_VFLight 클래스
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
//
// 상속: NetworkBehaviour
// - Photon Fusion 네트워크 동기화 기본 클래스
// - [Networked] 변수, FixedUpdateNetwork(), Render() 등 사용 가능
//
// 생명주기:
// 1. Runner.Spawn() → Spawned() 호출
// 2. Init() 호출 → 시작/타겟 위치 설정
// 3. FixedUpdateNetwork() → 서버에서 progress 업데이트
// 4. Render() → 모든 클라이언트에서 비주얼 업데이트
// 5. progress >= 1 → 도착, 데미지 적용, Despawn
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
public class Eva_VFLight : NetworkBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 1. 투사체 설정값 (Inspector에서 조절 가능)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // [Header]: Inspector에서 그룹 제목 표시
    // [SerializeField]: private 변수도 Inspector에서 편집 가능
    //
    // speed: 이동 속도 (m/s)
    // - 높을수록 빠르게 도착
    // - travelTime = distance / speed로 도착 시간 계산
    //
    // arcHeight: 포물선 최대 높이 (m)
    // - 높을수록 높이 올라갔다 내려옴
    // - sin 곡선으로 부드러운 아치 형성
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    [Header("VF 라이트 설정")]
    [SerializeField] public float speed = 20f;           // 이동 속도 (조절 가능)
    [SerializeField] public float arcHeight = 2f;        // 포물선 높이 (조절 가능)
    [SerializeField] private float damage = 20f;         // 데미지
    [SerializeField] private float maxLifeTime = 3f;     // 최대 생존 시간

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 2. 네트워크 동기화 변수 (핵심!)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // [Networked] 변수의 특징:
    // 1. 서버에서 값 변경 → 자동으로 모든 클라이언트에 동기화
    // 2. 새 플레이어 접속 시 현재 값 바로 알 수 있음
    // 3. 값 변경 시 네트워크 대역폭 사용 (최소화 필요)
    //
    // lifeTimer: 최대 생존 시간 타이머
    // - 타겟에 도달하지 못하면 자동 삭제 (무한 루프 방지)
    //
    // startPos: 투사체 시작 위치
    // - Init()에서 설정, 이후 변경 없음
    //
    // targetPos: 타겟 위치 (추적 시 업데이트)
    // - 매 틱 타겟의 현재 위치로 업데이트
    // - 타겟이 움직여도 따라감
    //
    // progress: 이동 진행도 (0~1)
    // - 0: 시작 지점
    // - 1: 도착 지점
    // - Lerp 계산에 사용
    //
    // targetId: 타겟의 NetworkId
    // - NetworkObject 직접 참조 불가 → NetworkId로 저장
    // - TryFindObject()로 실제 오브젝트 찾음
    //
    // isInitialized: 초기화 완료 플래그
    // - Init() 호출 전까지 false
    // - 초기화 전 로직 실행 방지
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    [Networked] private TickTimer lifeTimer { get; set; }
    [Networked] private Vector3 startPos { get; set; }
    [Networked] private Vector3 targetPos { get; set; }
    [Networked] private float progress { get; set; }     // 0~1 진행도
    [Networked] private NetworkId targetId { get; set; }
    [Networked] private NetworkBool isInitialized { get; set; }  // 네트워크 동기화

    /// <summary>
    /// 타겟 오브젝트 참조 (로컬 캐시)
    /// NetworkId로 찾은 결과를 캐싱하여 매 틱 TryFindObject() 호출 방지
    /// </summary>
    private NetworkObject targetObject;

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Init() - 투사체 초기화 (서버에서 호출)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 호출 시점: Eva_Skill.TryApplyVFLight()에서 Spawn 직후
    //
    // 매개변수:
    // - start: 투사체 시작 위치 (캐릭터 위치)
    // - target: 타겟 NetworkObject
    // - customSpeed/customArcHeight: 옵션 (0이면 기본값 사용)
    //
    // 초기 위치 설정:
    // - transform.position: 렌더링 위치 (즉시 적용)
    // - startPos: 네트워크 동기화 (다음 틱부터 적용)
    //
    // 회전 설정:
    // - 타겟 방향을 바라보게 설정
    // - Quaternion.LookRotation(): 방향 벡터 → 회전값 변환
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public void Init(Vector3 start, NetworkObject target, float customSpeed = 0f, float customArcHeight = 0f)
    {
        // 네트워크 동기화 변수 설정
        startPos = start;
        targetObject = target;
        targetId = target.Id;

        // ═══════════════════════════════════════════════════════════════
        // 타겟의 현재 위치 저장
        //
        // HeroMovement의 KCC 위치 사용 이유:
        // - transform.position은 루트 오브젝트 위치
        // - KCC.Position은 실제 캐릭터 충돌체 위치
        // - 더 정확한 위치 추적
        //
        // Vector3.up 추가:
        // - 발 위치가 아닌 몸통 높이로 조준
        // - 바닥에 박히는 현상 방지
        // ═══════════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════════
        // 즉시 초기 위치 적용
        //
        // 왜 여기서도 설정하나?
        // - [Networked] 변수는 다음 틱에 동기화됨
        // - 첫 프레임에 (0,0,0)에서 시작 위치로 순간이동하는 현상 방지
        // - 즉시 올바른 위치에 배치
        // ═══════════════════════════════════════════════════════════════
        transform.position = start;
        Vector3 dir = (targetPos - start).normalized;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Spawned() - 네트워크 오브젝트 생성 시 호출
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 생명 타이머 설정:
    // - maxLifeTime 후 자동 삭제
    // - 타겟에 도달하지 못하는 경우 대비 (버그 방지)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public override void Spawned()
    {
        lifeTimer = TickTimer.CreateFromSeconds(Runner, maxLifeTime);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // FixedUpdateNetwork() - 네트워크 게임 로직 (서버에서 실행)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 호출 타이밍:
    // - 서버: 매 네트워크 틱 (기본 60Hz)
    // - 클라이언트: 예측 모드에서 호출될 수 있음
    //
    // 처리 내용:
    // 1. 수명 체크 → 만료 시 삭제
    // 2. 타겟 위치 업데이트 (추적)
    // 3. progress 업데이트 (이동)
    // 4. 도착 체크 → 데미지 적용 후 삭제
    //
    // HasStateAuthority 체크:
    // - 게임 로직은 서버에서만 처리
    // - 클라이언트는 Render()에서 비주얼만 처리
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public override void FixedUpdateNetwork()
    {
        // 초기화 전이면 무시
        if (!isInitialized) return;

        // ═══════════════════════════════════════════════════════════════
        // 서버(Host) 전용 로직
        //
        // HasStateAuthority가 true인 경우:
        // - 서버(Host): 항상 true
        // - 클라이언트: 예측 모드일 때만 true (투사체는 보통 예측 안 함)
        // ═══════════════════════════════════════════════════════════════
        if (HasStateAuthority)
        {
            // ═══════════════════════════════════════════════════════════
            // 수명 만료 체크
            //
            // Expired(): 타이머가 만료되었는지 확인
            // - true: maxLifeTime 경과 → 삭제
            // - 무한 존재 방지 (메모리 누수, 버그 방지)
            // ═══════════════════════════════════════════════════════════
            if (lifeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
                return;
            }

            // ═══════════════════════════════════════════════════════════
            // 타겟 오브젝트 찾기 (추적용)
            //
            // 왜 매 틱 찾나?
            // - targetObject는 로컬 캐시 (네트워크 동기화 안 됨)
            // - 첫 틱에서 아직 못 찾았을 수 있음
            // - 타겟이 디스폰되면 null이 됨
            //
            // TryFindObject():
            // - NetworkId로 NetworkObject 찾기
            // - 성공 시 targetObject에 저장
            // ═══════════════════════════════════════════════════════════
            if (targetObject == null && targetId.IsValid)
            {
                Runner.TryFindObject(targetId, out targetObject);
            }

            // ═══════════════════════════════════════════════════════════
            // 타겟 위치 업데이트 (추적)
            //
            // 타겟이 이동하면 targetPos도 업데이트
            // → 유도 미사일처럼 따라감
            //
            // 왜 매 틱 업데이트?
            // - 타겟이 빠르게 움직이면 놓칠 수 있음
            // - 정확한 추적을 위해 실시간 위치 반영
            // ═══════════════════════════════════════════════════════════
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

            // ═══════════════════════════════════════════════════════════
            // 진행도 계산
            //
            // 공식: progress += deltaTime / travelTime
            //
            // travelTime = distance / speed
            // - 거리가 멀수록 오래 걸림
            // - 속도가 빠를수록 빨리 도착
            //
            // distance < 0.1f 체크:
            // - 0으로 나누기 방지
            // - 아주 가까우면 즉시 도착
            // ═══════════════════════════════════════════════════════════
            float distance = Vector3.Distance(startPos, targetPos);
            if (distance < 0.1f) distance = 0.1f;  // 0으로 나누기 방지
            float travelTime = distance / speed;
            progress += Runner.DeltaTime / travelTime;

            // ═══════════════════════════════════════════════════════════
            // 도착 체크 (progress >= 1)
            //
            // 도착 시:
            // 1. 데미지 적용 (ApplyDamage)
            // 2. 투사체 삭제 (Despawn)
            // ═══════════════════════════════════════════════════════════
            if (progress >= 1f)
            {
                // 도착 - 데미지 적용
                ApplyDamage();
                Runner.Despawn(Object);
                return;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 위치/회전 업데이트 (서버에서)
        //
        // 왜 FixedUpdateNetwork에서도 업데이트?
        // - 서버에서 정확한 위치 계산
        // - transform.position은 네트워크 동기화 안 됨
        // - 서버 측 시각적 확인용
        // ═══════════════════════════════════════════════════════════════
        if (HasStateAuthority)
        {
            UpdateVisualPosition();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Render() - 클라이언트 렌더링 (매 프레임)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 호출 타이밍:
    // - 모든 클라이언트에서 매 렌더링 프레임 호출
    // - FixedUpdateNetwork()와 독립적 (주사율 다를 수 있음)
    //
    // 역할:
    // - [Networked] 변수(startPos, targetPos, progress)를 읽어서
    // - 시각적 위치 계산 및 적용
    //
    // 왜 Render()에서 처리하나?
    // - 부드러운 시각적 표현
    // - 네트워크 틱(60Hz)과 렌더링 프레임(144Hz 등) 분리
    // - 끊김 없는 이동 표현
    //
    // 안전 체크:
    // - isInitialized: 초기화 전 방지
    // - startPos/targetPos == Zero: 동기화 전 방지
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public override void Render()
    {
        if (!isInitialized) return;

        // ═══════════════════════════════════════════════════════════════
        // 동기화 대기 체크
        //
        // [Networked] 변수는 Spawned() 직후에는 기본값(0)
        // 서버에서 Init() 후 다음 틱에 동기화됨
        //
        // startPos와 targetPos가 둘 다 Zero면:
        // - 아직 서버 데이터가 도착 안 함
        // - 렌더링 스킵 (잘못된 위치에 그리기 방지)
        // ═══════════════════════════════════════════════════════════════
        if (startPos == Vector3.zero && targetPos == Vector3.zero) return;  // 아직 동기화 안됨

        UpdateVisualPosition();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // UpdateVisualPosition() - 시각적 위치/회전 업데이트
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 포물선 위치 계산 후 transform에 적용
    //
    // 회전 계산:
    // - 현재 위치에서 다음 위치로의 방향
    // - progress + 0.05f로 "약간 앞" 위치 계산
    // - 이동 방향을 바라보게 설정
    //
    // 왜 0.05f?
    // - 너무 작으면 회전이 불안정
    // - 너무 크면 방향이 부정확
    // - 0.05f는 경험적으로 적절한 값
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void UpdateVisualPosition()
    {
        // 포물선 위치 계산
        Vector3 currentPos = CalculateParabolicPosition(progress);
        transform.position = currentPos;

        // ═══════════════════════════════════════════════════════════════
        // 이동 방향으로 회전
        //
        // 현재 위치와 "약간 앞" 위치의 차이로 방향 계산
        // - progress + 0.05f: 약간 앞 위치
        // - Mathf.Min(..., 1f): 1을 넘지 않게 (도착 지점 초과 방지)
        //
        // Quaternion.LookRotation():
        // - 방향 벡터 → 회전값 변환
        // - 투사체가 진행 방향을 바라보게 함
        // ═══════════════════════════════════════════════════════════════
        Vector3 nextPos = CalculateParabolicPosition(Mathf.Min(progress + 0.05f, 1f));
        Vector3 direction = (nextPos - currentPos).normalized;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // CalculateParabolicPosition() - 포물선 위치 계산 (핵심 수학!)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 포물선 궤적 = 선형 보간 + 높이 오프셋
    //
    // [1] 선형 보간 (Linear Interpolation)
    // - Vector3.Lerp(start, end, t)
    // - t=0: start 위치
    // - t=1: end 위치
    // - t=0.5: 중간 지점
    //
    // [2] 포물선 높이 (Parabolic Arc)
    // - sin(t * π) 곡선 사용
    // - t=0: sin(0) = 0 (시작 - 높이 없음)
    // - t=0.5: sin(π/2) = 1 (중간 - 최대 높이)
    // - t=1: sin(π) = 0 (끝 - 높이 없음)
    //
    // 최종 위치 = 선형 위치 + (0, arc, 0)
    //
    // ┌──────────────────────────────────────────┐
    // │ 시각적 표현:                              │
    // │                                          │
    // │         ●  ← 최대 높이 (t=0.5)           │
    // │       /   \                              │
    // │      /     \                             │
    // │     /       \                            │
    // │    ●─────────●                           │
    // │  시작       도착                          │
    // │ (t=0)       (t=1)                        │
    // └──────────────────────────────────────────┘
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private Vector3 CalculateParabolicPosition(float t)
    {
        // [1] 선형 보간 위치
        Vector3 linearPos = Vector3.Lerp(startPos, targetPos, t);

        // [2] 포물선 높이 추가 (sin 곡선으로 부드러운 아치)
        // Mathf.Sin(t * Mathf.PI):
        // - t=0 → sin(0) = 0
        // - t=0.5 → sin(π/2) = 1
        // - t=1 → sin(π) = 0
        float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;

        return linearPos + new Vector3(0, arc, 0);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // ApplyDamage() - 데미지 적용 (서버에서만 호출)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 호출 시점: progress >= 1 (도착)
    //
    // 조건 체크:
    // 1. targetObject != null: 타겟이 아직 존재
    // 2. GetCurrHealth() > 0: 타겟이 아직 살아있음
    //
    // IDamageProcess 인터페이스:
    // - 모든 데미지 받는 오브젝트가 구현
    // - OnTakeDamage(float damage) 메서드로 데미지 적용
    //
    // 왜 인터페이스 사용?
    // - 캐릭터, 미니언, 타워 등 다양한 대상에 동일 로직 적용
    // - 타입 체크 없이 데미지 적용 가능
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void ApplyDamage()
    {
        if (targetObject == null) return;

        // IDamageProcess와 HeroState 컴포넌트 찾기
        var damageProcess = targetObject.GetComponentInChildren<IDamageProcess>();
        var heroState = targetObject.GetComponentInChildren<HeroState>();

        // 타겟이 살아있을 때만 데미지 적용
        if (damageProcess != null && heroState != null && heroState.GetCurrHealth() > 0f)
        {
            damageProcess.OnTakeDamage(damage);
        }
    }
}
