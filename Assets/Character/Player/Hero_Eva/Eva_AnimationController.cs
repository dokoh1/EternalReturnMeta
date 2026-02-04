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
}
