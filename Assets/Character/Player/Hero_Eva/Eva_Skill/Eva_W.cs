// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Eva_W.cs - Eva W 스킬 범위 장판
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 개요 (Overview)                                                                                      │
// │                                                                                                       │
// │ 이 파일은 Eva의 W 스킬로 생성되는 범위 장판을 구현합니다.                                             │
// │ 장판 위의 적은 이동 속도가 감소하고, 종료 시 중심부 적은 에어본됩니다.                               │
// │                                                                                                       │
// │ 핵심 기술:                                                                                            │
// │ 1. TickTimer - 지속 시간 관리                                                                        │
// │ 2. OnTriggerEnter/Exit - 범위 진입/이탈 감지                                                         │
// │ 3. Physics.OverlapSphere - 범위 내 적 검색                                                           │
// │ 4. HashSet - 영향받는 대상 추적 (중복 방지)                                                          │
// │ 5. 에어본 시스템 연동                                                                                │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 설계 철학 (Design Philosophy)                                                                        │
// │                                                                                                       │
// │ Q: 왜 HashSet으로 대상을 추적하나요?                                                                 │
// │ A: 슬로우 효과를 정확히 관리하기 위해.                                                                │
// │    - 진입 시 슬로우 적용, 이탈 시 해제                                                               │
// │    - 중복 진입 방지 (Contains 체크)                                                                  │
// │    - 스킬 종료 시 모든 대상 슬로우 해제                                                              │
// │    List보다 HashSet이 Contains 성능 O(1)로 효율적.                                                    │
// │                                                                                                       │
// │ Q: 왜 두 개의 반경이 있나요? (skillRadius, centerRadius)                                             │
// │ A: 외곽과 중심부 효과가 다름.                                                                         │
// │    - skillRadius (3.5): 전체 범위 - 슬로우 적용                                                       │
// │    - centerRadius (1.5): 중심부 - 에어본 추가 적용                                                   │
// │    중심을 피하면 에어본은 안 당하는 플레이 전략.                                                      │
// │                                                                                                       │
// │ Q: 왜 생성 시와 종료 시 모두 데미지를 주나요?                                                        │
// │ A: 두 번의 데미지 타이밍 제공.                                                                        │
// │    - 생성 시: 설치 순간 데미지 (반응이 느린 적 처벌)                                                  │
// │    - 종료 시: 도망 실패 데미지 (장판에 머문 적 처벌)                                                  │
// │    전략적 선택: 설치 + 이탈 vs 설치 + 잔류                                                           │
// │                                                                                                       │
// │ Q: SpeedMultiplier 방식의 장점은?                                                                    │
// │ A: 기존 이동 속도에 배율 적용.                                                                        │
// │    - 1.0f: 정상 속도                                                                                  │
// │    - 0.65f: 35% 감소 (현재 설정)                                                                     │
// │    - 중첩 슬로우는 곱연산으로 처리 가능                                                              │
// │    - 슬로우 면역 체크도 간단 (IsSlowImmune)                                                          │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 스킬 타임라인 (Skill Timeline)                                                                       │
// │                                                                                                       │
// │ 0ms ─────────────────────────────────────────────────────────── 1000ms                               │
// │  ├─ 스폰 & 생성 데미지 (40)                                        │                                 │
// │  │     ↓                                                           │                                 │
// │  │  진입한 적 슬로우 35%                                           │                                 │
// │  │     ↓                                                           │                                 │
// │  │  이탈한 적 슬로우 해제                                          │                                 │
// │  │     ↓                                                           │                                 │
// │  └─────────────────────────────────────────────────────────────────┤                                 │
// │                                                              종료 데미지 (40) + 중심부 에어본        │
// │                                                                    │                                 │
// │                                                              모든 슬로우 해제 & Despawn              │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Threading;
using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Eva_W 클래스 - W 스킬 범위 장판
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
//
// 역할:
// - 범위 내 적 이동 속도 35% 감소
// - 생성/종료 시 데미지
// - 종료 시 중심부 적 에어본
//
// 스탯:
// - 생성 데미지: 40
// - 종료 데미지: 40
// - 슬로우: 35%
// - 전체 반경: 3.5
// - 중심 반경: 1.5
// - 지속 시간: 1초
// - 에어본: 1초
//
// 관련 파일:
// - Eva_Skill.cs: 이 장판을 스폰
// - HeroMovement.cs: SpeedMultiplier, ApplyAirborne
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
public class Eva_W : NetworkBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // [Networked] 변수
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    [Networked] private TickTimer lifeTimer { get; set; }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 참조 및 상태 변수
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private PlayerRef owner;
    private Eva_Skill ownerSkill;
    private CancellationTokenSource _cts;

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 스킬 설정 (Inspector)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 데미지:
    // - spawnDamage: 생성 시 데미지
    // - endDamage: 종료 시 데미지
    //
    // 효과:
    // - slowAmount: 슬로우 강도 (0.35 = 35% 감소)
    // - airborneDuration: 에어본 지속 시간
    //
    // 범위:
    // - skillRadius: 전체 범위 (슬로우 적용)
    // - centerRadius: 중심부 (에어본 적용)
    // - duration: 스킬 지속 시간
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    [SerializeField] private GameObject HitVFX;
    [SerializeField] private float spawnDamage = 40f;
    [SerializeField] private float endDamage = 40f;
    [SerializeField] private float slowAmount = 0.35f;
    [SerializeField] private float skillRadius = 3.5f;
    [SerializeField] private float centerRadius = 1.5f;
    [SerializeField] private float duration = 1.0f;
    [SerializeField] private float airborneDuration = 1.0f;

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // affectedTargets - 현재 슬로우 적용 중인 대상들
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // HashSet 사용 이유:
    // - Contains() O(1) 성능
    // - 중복 자동 방지
    // - 빠른 Add/Remove
    //
    // 관리 시점:
    // - OnTriggerEnter: Add
    // - OnTriggerExit: Remove
    // - OnSkillEnd: 전체 Clear
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private HashSet<HeroMovement> affectedTargets = new HashSet<HeroMovement>();

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Awake() - Unity 초기화
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void Awake()
    {
        Utility.RefreshToken(ref _cts);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Init() - 스폰 후 초기화
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public void Init(PlayerRef player, Eva_Skill skill = null)
    {
        owner = player;
        ownerSkill = skill;
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Spawned() - 네트워크 오브젝트 생성 시
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public override void Spawned()
    {
        lifeTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // ActiveInit() - 비동기 활성화
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 50ms 딜레이 후:
    // 1. 파티클 재생 (시각 효과)
    // 2. 콜라이더 활성화 + 반경 설정
    // 3. 범위 내 적에게 생성 데미지
    //
    // null 체크:
    // - this == null: 딜레이 중 Despawn되었을 수 있음
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public async UniTaskVoid ActiveInit()
    {
        await UniTask.Delay(50);

        if (this == null || gameObject == null) return;

        // 파티클 재생
        var ps = GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Play();

        // 콜라이더 활성화
        var col = GetComponent<SphereCollider>();
        if (col != null)
        {
            col.enabled = true;
            col.radius = skillRadius;
        }

        // 생성 시 범위 내 적에게 데미지 (서버에서만)
        if (HasStateAuthority)
        {
            DealDamageToAllInRange(spawnDamage);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // FixedUpdateNetwork() - 매 네트워크 틱
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 서버에서만 타이머 체크 및 종료 처리
    // 지속 시간 종료 시:
    // 1. OnSkillEnd() 호출
    // 2. Despawn
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (lifeTimer.Expired(Runner))
        {
            OnSkillEnd();
            Runner.Despawn(Object);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // OnTriggerEnter() - 범위 진입
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 진입한 적에게:
    // 1. affectedTargets에 추가
    // 2. SpeedMultiplier 적용 (슬로우)
    //
    // 슬로우 면역 체크:
    // - IsSlowImmune이면 적용 안 함
    // - 예: 특정 스킬 사용 중, 아이템 효과 등
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;
        if (other.GetComponentInParent<NetworkObject>() == null) return;

        // 자기 자신 제외
        if (other.GetComponentInParent<NetworkObject>().InputAuthority == owner) return;

        var targetMovement = other.GetComponentInParent<HeroMovement>();
        if (targetMovement != null && !affectedTargets.Contains(targetMovement))
        {
            affectedTargets.Add(targetMovement);
            // 이동 속도 35% 감소 (0.65 배율) - 슬로우 면역이면 적용 안함
            if (!targetMovement.IsSlowImmune)
            {
                targetMovement.SpeedMultiplier = 1.0f - slowAmount;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // OnTriggerExit() - 범위 이탈
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 이탈한 적:
    // 1. affectedTargets에서 제거
    // 2. SpeedMultiplier 복구 (1.0f)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void OnTriggerExit(Collider other)
    {
        if (!HasStateAuthority) return;
        if (other.GetComponentInParent<NetworkObject>() == null) return;

        // 자기 자신 제외
        if (other.GetComponentInParent<NetworkObject>().InputAuthority == owner) return;

        var targetMovement = other.GetComponentInParent<HeroMovement>();
        if (targetMovement != null && affectedTargets.Contains(targetMovement))
        {
            affectedTargets.Remove(targetMovement);
            // 이동 속도 복구
            targetMovement.SpeedMultiplier = 1.0f;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // OnSkillEnd() - 스킬 종료 처리
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 종료 시 처리:
    // 1. 범위 내 모든 적에게 종료 데미지
    // 2. 중심부 적에게 에어본
    // 3. 모든 슬로우 해제
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void OnSkillEnd()
    {
        // 종료 시 범위 내 적에게 데미지
        DealDamageToAllInRange(endDamage);

        // 중심부 적들 에어본
        ApplyAirborneToCenter();

        // 모든 슬로우 해제
        foreach (var target in affectedTargets)
        {
            if (target != null)
            {
                target.SpeedMultiplier = 1.0f;
            }
        }
        affectedTargets.Clear();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // DealDamageToAllInRange() - 범위 내 모든 적에게 데미지
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // Physics.OverlapSphere:
    // - 구 범위 내 모든 콜라이더 검색
    // - LayerMask로 "Character" 레이어만 검색
    //
    // 각 대상 처리:
    // 1. 자기 자신 제외
    // 2. IDamageProcess로 데미지
    // 3. VFLight 연계
    // 4. HitVFX 스폰
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void DealDamageToAllInRange(float damage)
    {
        // 범위 내 모든 콜라이더 검색
        Collider[] hits = Physics.OverlapSphere(transform.position, skillRadius, LayerMask.GetMask("Character"));

        foreach (var hit in hits)
        {
            var targetNO = hit.GetComponentInParent<NetworkObject>();
            if (targetNO == null) continue;

            // 자기 자신 제외
            if (targetNO.InputAuthority == owner) continue;

            var damageProcess = hit.GetComponentInParent<IDamageProcess>();
            var heroState = hit.GetComponentInParent<HeroState>();

            if (damageProcess != null && heroState != null && heroState.GetCurrHealth() > 0f)
            {
                damageProcess.OnTakeDamage(damage);

                // VF 라이트 투사체 발사 (E 스킬 사용 후 3초 내)
                if (ownerSkill != null && targetNO != null)
                {
                    ownerSkill.TryApplyVFLight(targetNO);
                }

                // 히트 이펙트
                if (Runner.IsServer && HitVFX != null)
                {
                    var vfx = Runner.Spawn(HitVFX, hit.transform.position + Vector3.up, Quaternion.identity);
                    HitVFXDestroy(vfx).Forget();
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // ApplyAirborneToCenter() - 중심부 적 에어본
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // centerRadius (1.5) 범위 내 적만:
    // - 에어본 높이: 1
    // - 에어본 시간: airborneDuration (1초)
    //
    // 전략적 의미:
    // - 중심을 피하면 에어본 회피 가능
    // - 슬로우만 먹고 도망 vs 중심에서 에어본
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void ApplyAirborneToCenter()
    {
        // 중심부 범위 내 적들 검색
        Collider[] hits = Physics.OverlapSphere(transform.position, centerRadius, LayerMask.GetMask("Character"));

        foreach (var hit in hits)
        {
            var targetNO = hit.GetComponentInParent<NetworkObject>();
            if (targetNO == null) continue;

            // 자기 자신 제외
            if (targetNO.InputAuthority == owner) continue;

            var targetMovement = hit.GetComponentInParent<HeroMovement>();
            if (targetMovement != null)
            {
                targetMovement.ApplyAirborne(1f, airborneDuration);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // HitVFXDestroy() - 히트 이펙트 자동 제거
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private async UniTaskVoid HitVFXDestroy(NetworkObject no)
    {
        await UniTask.Delay(1000);
        if (this == null || no == null) return;
        if (Runner.IsServer)
        {
            Runner.Despawn(no);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Despawned() - 네트워크 오브젝트 제거 시
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 정리 작업:
    // - 모든 슬로우 해제 (비정상 종료 대비)
    // - CTS 리프레시
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // 슬로우 해제
        foreach (var target in affectedTargets)
        {
            if (target != null)
            {
                target.SpeedMultiplier = 1.0f;
            }
        }
        affectedTargets.Clear();

        Utility.RefreshToken(ref _cts);
    }
}
