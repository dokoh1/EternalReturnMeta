using Fusion;
using UnityEngine;

public class Eva_AnimationController : HeroAnimationController
{
    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_Skill_Q()
    {
        animator.SetTrigger("tSkill01");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_Skill_W()
    {
        animator.SetTrigger("tSkill02");
    }

    // E 스킬 시작 (Loop 애니메이션)
    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_Skill_E()
    {
        animator.SetTrigger("tSkill03");
        animator.SetBool("bSkill03", true);  // Loop용 bool
    }

    // E 스킬 종료
    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_Skill_E_End()
    {
        animator.SetBool("bSkill03", false);  // Loop 해제
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_Skill_R_Activate_Animation()
    {
        animator.SetTrigger("tSkill04");
        animator.SetBool("bSkill04", true);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_Skill_R_Deactivate_Animation()
    {
        animator.SetBool("bSkill04", false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_BasicAttack()
    {
        animator.SetTrigger("tBasicAttack");
    }

    // 기본 공격 캔슬 (이동 시 호출)
    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_CancelBasicAttack()
    {
        animator.ResetTrigger("tBasicAttack");
        animator.SetTrigger("tCancelAttack");  // 캔슬용 트리거 (선택적)
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Phase 4: 스킬 애니메이션 캔슬 (Animation Canceling 동기화용)
    //
    // 왜 필요한가?
    // - 서버에서 캔슬이 발생하면 다른 클라이언트도 알아야 함
    // - 안 그러면 서버/클라이언트 애니메이션 타이밍이 다름
    // - 상대방 화면에서 내 캐릭터가 "버벅거림"
    //
    // 작동 방식:
    // 1. 서버에서 캔슬 발생 → 이 RPC 호출
    // 2. 모든 클라이언트에서 현재 스킬 애니메이션 즉시 중단
    // 3. 빠르게 Idle 또는 다음 스킬로 블렌딩
    // ═══════════════════════════════════════════════════════════════════════════
    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_CancelSkillAnimation()
    {
        // 현재 재생 중인 스킬 트리거 리셋
        animator.ResetTrigger("tSkill01");
        animator.ResetTrigger("tSkill02");

        // 빠른 블렌딩으로 자연스럽게 전환 (0.05초 = 50ms)
        // CrossFade: 현재 애니메이션 → 목표 애니메이션을 부드럽게 연결
        animator.CrossFade("Idle", 0.05f);
    }
}
