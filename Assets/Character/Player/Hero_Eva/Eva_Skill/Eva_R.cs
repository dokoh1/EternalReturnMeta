// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Eva_R.cs - Eva R 스킬 (궁극기) 지속 데미지 필드
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 개요 (Overview)                                                                                      │
// │                                                                                                       │
// │ 이 파일은 Eva의 R 스킬로 생성되는 지속 데미지 필드를 구현합니다.                                      │
// │ 캐릭터 주변에 장착되어 토글 형태로 유지되며, 범위 내 적에게 지속적으로 데미지를 줍니다.              │
// │                                                                                                       │
// │ 핵심 기술:                                                                                            │
// │ 1. DamageLoop - 100ms마다 지속 데미지 (초당 20 데미지)                                               │
// │ 2. CancellationToken - 범위 이탈/스킬 종료 시 루프 취소                                              │
// │ 3. 토글 시스템 - Eva_Skill에서 활성화/비활성화                                                       │
// │ 4. VFLight 연계 - 매 히트마다 E 스킬 연계                                                            │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 설계 철학 (Design Philosophy)                                                                        │
// │                                                                                                       │
// │ Q: 왜 while 루프로 지속 데미지를 구현하나요?                                                         │
// │ A: 정확한 틱 간격 제어를 위해.                                                                        │
// │    - Update()에서 시간 누적: 프레임레이트 영향 받음                                                  │
// │    - while + Delay: 정확히 100ms 간격 보장                                                           │
// │    - 취소도 CancellationToken으로 깔끔하게 처리                                                      │
// │                                                                                                       │
// │ Q: 왜 범위 이탈 시 토큰을 리프레시하나요?                                                            │
// │ A: 이전 DamageLoop를 확실히 종료하기 위해.                                                            │
// │    - Token.Cancel() → while 루프 내 Delay에서 예외/break                                             │
// │    - 다시 진입하면 새 토큰으로 새 루프 시작                                                          │
// │    - 같은 적이 나갔다 들어오면 새로 카운트                                                            │
// │                                                                                                       │
// │ Q: 왜 데미지가 2인가요? (100ms당)                                                                    │
// │ A: 초당 DPS 20을 목표로:                                                                              │
// │    - 1000ms / 100ms = 10틱/초                                                                        │
// │    - 10틱 × 2데미지 = 20 DPS                                                                         │
// │    너무 잦은 틱은 부하, 너무 드문 틱은 뚝뚝 끊기는 느낌                                              │
// │    100ms는 적절한 밸런스                                                                              │
// │                                                                                                       │
// │ Q: 왜 R 스킬 오브젝트가 캐릭터에 붙나요?                                                              │
// │ A: 토글 스킬 특성상 캐릭터와 함께 이동해야 함.                                                        │
// │    - W: 고정 위치 장판                                                                                │
// │    - R: 캐릭터 따라다니는 오라                                                                        │
// │    Eva_Skill에서 SetParent로 캐릭터에 부착                                                            │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 데미지 루프 (Damage Loop)                                                                            │
// │                                                                                                       │
// │ [적 진입 시]                                                                                          │
// │ OnTriggerEnter()                                                                                     │
// │     ↓                                                                                                 │
// │ DamageLoop(token) 시작                                                                               │
// │     ↓                                                                                                 │
// │ while (HP > 0 && !token.IsCancellationRequested)                                                     │
// │     ├─ 데미지 2                                                                                       │
// │     ├─ VFLight 연계                                                                                   │
// │     ├─ HitVFX 스폰                                                                                    │
// │     └─ 100ms 대기                                                                                     │
// │                                                                                                       │
// │ [적 이탈 시]                                                                                          │
// │ OnTriggerExit()                                                                                      │
// │     ↓                                                                                                 │
// │ Utility.RefreshToken() → 토큰 취소 → 루프 종료                                                       │
// │                                                                                                       │
// │ [R 스킬 토글 OFF]                                                                                     │
// │ Despawned()                                                                                           │
// │     ↓                                                                                                 │
// │ Utility.RefreshToken() → 모든 루프 종료                                                              │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

using System.Threading;
using Character.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Eva_R 클래스 - R 스킬 지속 데미지 필드
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
//
// 역할:
// - 캐릭터 주변 범위 내 적에게 지속 데미지
// - 100ms마다 2 데미지 (초당 20 DPS)
// - 토글 형태로 유지 (Eva_Skill에서 관리)
//
// 스탯:
// - 틱 데미지: 2
// - 틱 간격: 100ms
// - DPS: 20
//
// 관련 파일:
// - Eva_Skill.cs: 이 필드를 스폰/Despawn (토글)
// - HeroMovement.cs: 캐릭터에 부착
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
public class Eva_R : NetworkBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // 참조 변수
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private PlayerRef owner;
    private Eva_Skill ownerSkill;
    private CancellationTokenSource _cts;
    [SerializeField] private GameObject HitVFX;

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Awake() - CTS 초기화
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
    // ActiveInit() - 비동기 활성화
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 200ms 딜레이 후:
    // 1. 파티클 재생 (R 스킬 오라 이펙트)
    // 2. BoxCollider 활성화 (충돌 감지)
    //
    // null 체크:
    // - 딜레이 중 토글 OFF되어 Despawn될 수 있음
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public async UniTaskVoid ActiveInit()
    {
        await UniTask.Delay(200);

        if (this == null || gameObject == null) return;

        var ps = GetComponent<ParticleSystem>();
        ps.Play();

        var bc = GetComponent<BoxCollider>();
        bc.enabled = true;
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // OnTriggerEnter() - 범위 진입 시 데미지 루프 시작
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 서버 권한 체크 후:
    // - 자기 자신 제외
    // - DamageLoop 시작 (현재 토큰 전달)
    //
    // 왜 DamageLoop를 바로 시작?
    // - 적이 범위에 들어오면 즉시 데미지 시작
    // - 첫 틱 데미지도 바로 적용
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void OnTriggerEnter(Collider other)
    {
        // 서버에서만 데미지 처리
        if (!HasStateAuthority) return;

        if (other.GetComponentInParent<NetworkObject>() == null) return;

        // 자기 자신이 맞았다면 무시
        if (other.GetComponentInParent<NetworkObject>().InputAuthority == owner)
        {
            return;
        }

        // 데미지 루프 시작
        DamageLoop(other, _cts.Token).Forget();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // OnTriggerExit() - 범위 이탈 시 데미지 루프 종료
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // RefreshToken:
    // - 기존 토큰 Cancel → DamageLoop 종료
    // - 새 토큰 생성 → 다음 진입 대비
    //
    // 왜 그냥 Cancel만 안 하나요?
    // - CTS는 한 번 Cancel하면 재사용 불가
    // - RefreshToken으로 새 CTS 생성해야 다음 적 진입 처리 가능
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<NetworkObject>() == null) return;

        // 자기 자신이 맞았다면 무시
        if (other.GetComponentInParent<NetworkObject>().InputAuthority == owner) return;

        // 토큰 취소 → 데미지 루프 종료
        Utility.RefreshToken(ref _cts);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Despawned() - 네트워크 오브젝트 제거 시
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // R 스킬 토글 OFF 시 호출
    // RefreshToken으로 모든 진행 중인 DamageLoop 종료
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Utility.RefreshToken(ref _cts);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // DamageLoop() - 지속 데미지 루프
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // while 루프:
    // - HP > 0: 죽은 적은 더 이상 데미지 안 함
    // - !token.IsCancellationRequested: 취소되면 종료
    //
    // 매 틱:
    // 1. 2 데미지
    // 2. VFLight 연계 (E 스킬 사용 후면 추가 투사체)
    // 3. HitVFX 스폰
    // 4. 100ms 대기
    //
    // SuppressCancellationThrow():
    // - 취소 시 예외 대신 false 반환
    // - 깔끔하게 루프 종료 처리
    //
    // 유효성 체크:
    // - this == null: Despawn되었을 수 있음
    // - other == null: 대상이 사라졌을 수 있음
    // - Runner == null: 세션 종료
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    private async UniTaskVoid DamageLoop(Collider other, CancellationToken token)
    {
        if (other == null) return;

        IDamageProcess damageProcess = other.GetComponent<IDamageProcess>();
        var targetNO = other.GetComponentInParent<NetworkObject>();
        var heroState = other.GetComponent<HeroState>();

        // null 체크
        if (damageProcess == null || heroState == null) return;

        while (heroState != null && heroState.GetCurrHealth() > 0f)
        {
            // 오브젝트 유효성 체크
            if (this == null || other == null || Runner == null) break;

            // 2 데미지 (100ms마다 = 초당 20 DPS)
            damageProcess.OnTakeDamage(2f);

            // VF 라이트 투사체 발사 (매 히트마다)
            if (ownerSkill != null && targetNO != null)
            {
                ownerSkill.TryApplyVFLight(targetNO);
            }

            // 히트 이펙트
            if (Runner != null && Runner.IsServer && HitVFX != null)
            {
                var no = Runner.Spawn(HitVFX, other.transform.position + new Vector3(0, 1, 0), Quaternion.identity);
                if (no != null)
                {
                    HitVFXDestroy(no).Forget();
                }
            }

            // 100ms 대기 (취소 시 throw 안 함)
            await UniTask.Delay(100, cancellationToken: token).SuppressCancellationThrow();

            // 취소되면 루프 종료
            if (token.IsCancellationRequested)
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // HitVFXDestroy() - 히트 이펙트 자동 제거
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public async UniTaskVoid HitVFXDestroy(NetworkObject no)
    {
        await UniTask.Delay(1000);

        if (this == null || gameObject == null) return;
        Runner.Despawn(no);
    }
}
