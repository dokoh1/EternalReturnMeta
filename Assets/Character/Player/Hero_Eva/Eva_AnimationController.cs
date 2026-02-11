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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_Skill_E()
    {
        animator.SetTrigger("tSkill03");
        animator.SetBool("bSkill03", true);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_Skill_E_End()
    {
        animator.SetBool("bSkill03", false);
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_CancelBasicAttack()
    {
        animator.ResetTrigger("tBasicAttack");
        animator.SetTrigger("tCancelAttack");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_Multi_CancelSkillAnimation()
    {
        animator.ResetTrigger("tSkill01");
        animator.ResetTrigger("tSkill02");
        animator.CrossFade("Idle", 0.05f);
    }
}
