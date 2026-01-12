using Fusion;
using UnityEngine;

public class HeroAnimationController : NetworkBehaviour
{
    [SerializeField] protected Animator animator;
    protected HeroMovement movement;
    
    [Networked] protected int MoveVelocity {get; set;}
   
    public override void Spawned()
    {
        movement = GetComponent<HeroMovement>();
        movement.OnMoveVelocityChanged += OnChangedVelocity;

        MoveVelocity = 0;
    }

    public override void Render()
    {
        if (animator)
        {
            if (movement)
            {
                animator.SetFloat("MoveSpeed", MoveVelocity);
            }
        }
    }

    private void OnChangedVelocity(int v)
    {
        MoveVelocity = v;
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_DeadProcess()
    {
        if (animator)
        {
            MoveVelocity = 0;
            animator.SetTrigger("IsDead");
        }
    }
}
