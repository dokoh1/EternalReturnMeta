using Character.Player;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class Eva_State : HeroState, IDamageProcess
{
    public void OnTakeDamage(float damage)
    {
        CurrHealth -= damage;

        if (CurrHealth <= 0)
        {
            var navMeshAgent = GetComponent<NavMeshAgent>();
            navMeshAgent.isStopped = true;
            var heroMovement = GetComponent<HeroMovement>();
            heroMovement.IsDeath = true;
            
            GetComponent<Eva_AnimationController>().RPC_DeadProcess();
        }
    }
}
