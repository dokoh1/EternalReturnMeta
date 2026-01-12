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
    
}
