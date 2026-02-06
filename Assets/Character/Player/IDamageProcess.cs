// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// IDamageProcess.cs - 데미지 처리 인터페이스
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 개요 (Overview)                                                                                      │
// │                                                                                                       │
// │ 이 인터페이스는 데미지를 받을 수 있는 모든 대상이 구현합니다.                                         │
// │ 영웅, 미니언, 타워, 정글 몬스터 등 다양한 대상을 통일된 방식으로 처리합니다.                         │
// │                                                                                                       │
// │ 핵심 기술:                                                                                            │
// │ 1. Interface - 구현을 강제하는 계약                                                                  │
// │ 2. 다형성 - 다양한 타입을 같은 방식으로 처리                                                         │
// │ 3. 느슨한 결합 - 스킬이 대상 타입을 몰라도 됨                                                        │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 설계 철학 (Design Philosophy)                                                                        │
// │                                                                                                       │
// │ Q: 왜 interface를 사용하나요?                                                                        │
// │ A: 스킬 코드에서 대상 타입을 몰라도 데미지를 줄 수 있게 하기 위해.                                    │
// │                                                                                                       │
// │    [인터페이스 없이]                                                                                  │
// │    void OnHit(Collider target) {                                                                     │
// │        if (target.GetComponent<Eva_State>()) { ... }        // Eva 체크                              │
// │        else if (target.GetComponent<Aya_State>()) { ... }   // Aya 체크                              │
// │        else if (target.GetComponent<Minion>()) { ... }      // 미니언 체크                           │
// │        else if (target.GetComponent<Tower>()) { ... }       // 타워 체크                             │
// │        // 새 대상 추가 때마다 else if 추가... 유지보수 악몽                                          │
// │    }                                                                                                  │
// │                                                                                                       │
// │    [인터페이스 사용]                                                                                  │
// │    void OnHit(Collider target) {                                                                     │
// │        var damageProcess = target.GetComponent<IDamageProcess>();                                    │
// │        damageProcess?.OnTakeDamage(damage);  // 끝! 타입 상관없이 동작                               │
// │    }                                                                                                  │
// │                                                                                                       │
// │ Q: 왜 OnTakeDamage만 있나요? OnHeal, OnShield 등은?                                                  │
// │ A: 현재 구현에서는 데미지만 필요. YAGNI 원칙.                                                         │
// │    필요할 때 IHealable, IShieldable 등 별도 인터페이스 추가.                                          │
// │    또는 IDamageProcess를 확장하여 추가 메서드 정의.                                                  │
// │                                                                                                       │
// │ Q: float damage만 받는 이유는?                                                                       │
// │ A: 현재는 단순화를 위해 숫자만.                                                                       │
// │    추후 DamageInfo 구조체로 확장 가능:                                                                │
// │    - damage: 데미지량                                                                                 │
// │    - type: 물리/마법/고정                                                                            │
// │    - source: 공격자                                                                                   │
// │    - isCrit: 치명타 여부                                                                             │
// │    - effects: 적용할 상태이상                                                                        │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 구현 예시 (Implementation Examples)                                                                  │
// │                                                                                                       │
// │ [영웅 - Eva_State.cs]                                                                                │
// │ public class Eva_State : HeroState, IDamageProcess {                                                 │
// │     public void OnTakeDamage(float damage) {                                                         │
// │         CurrHealth -= damage;                                                                        │
// │         if (CurrHealth <= 0) Die();                                                                  │
// │     }                                                                                                 │
// │ }                                                                                                     │
// │                                                                                                       │
// │ [미니언 - Minion.cs] (예시)                                                                          │
// │ public class Minion : NetworkBehaviour, IDamageProcess {                                             │
// │     public void OnTakeDamage(float damage) {                                                         │
// │         health -= damage;                                                                            │
// │         if (health <= 0) {                                                                           │
// │             GiveGold();  // 골드 지급                                                                │
// │             Die();                                                                                   │
// │         }                                                                                             │
// │     }                                                                                                 │
// │ }                                                                                                     │
// │                                                                                                       │
// │ [타워 - Tower.cs] (예시)                                                                             │
// │ public class Tower : NetworkBehaviour, IDamageProcess {                                              │
// │     public void OnTakeDamage(float damage) {                                                         │
// │         // 일정 데미지 이하 무시                                                                      │
// │         if (damage < minDamageThreshold) return;                                                     │
// │         health -= damage;                                                                            │
// │         if (health <= 0) Collapse();                                                                 │
// │     }                                                                                                 │
// │ }                                                                                                     │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

using System.Collections;
using Cysharp.Threading.Tasks;

namespace Character.Player
{
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // IDamageProcess 인터페이스
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    //
    // 역할:
    // - 데미지를 받을 수 있는 대상의 계약 정의
    // - OnTakeDamage() 메서드 구현 강제
    //
    // 구현자:
    // - Eva_State, Aya_State 등 영웅 State 클래스
    // - Minion, Tower 등 데미지를 받는 모든 엔티티
    //
    // 사용 패턴:
    // var target = collision.GetComponent<IDamageProcess>();
    // target?.OnTakeDamage(damage);  // null이면 호출 안 함
    //
    // GetComponent 대상:
    // - GetComponent<IDamageProcess>(): 같은 GameObject에서 찾기
    // - GetComponentInChildren<IDamageProcess>(): 자식 포함 찾기
    // - GetComponentInParent<IDamageProcess>(): 부모 포함 찾기
    //
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    public interface IDamageProcess
    {
        // ═══════════════════════════════════════════════════════════════════════════════════════════════
        // OnTakeDamage() - 데미지 받기
        // ═══════════════════════════════════════════════════════════════════════════════════════════════
        //
        // 파라미터:
        // - damage: 받을 데미지량 (float)
        //
        // 구현 시 처리해야 할 것들:
        // 1. HP 감소 (damage만큼)
        // 2. 사망 체크 (HP <= 0)
        // 3. 사망 처리 (애니메이션, 비활성화 등)
        // 4. (선택) 데미지 표시 UI
        // 5. (선택) 피격 이펙트/사운드
        //
        // 호출자 (예시):
        // - Eva_Q.OnTriggerEnter(): 투사체 적중 시
        // - Eva_W.ApplyDamage(): 범위 스킬 적중 시
        // - Eva_VFLight.OnTriggerEnter(): E 스킬 빛 적중 시
        // - BasicAttack.OnHit(): 기본 공격 적중 시
        //
        // 서버 권한:
        // - 반드시 서버(StateAuthority)에서만 호출해야 함
        // - 클라이언트에서 호출하면 동기화 문제 발생
        // - 호출 전 HasStateAuthority 체크 권장
        //
        // ═══════════════════════════════════════════════════════════════════════════════════════════════
        public void OnTakeDamage(float damage);
    }
}
