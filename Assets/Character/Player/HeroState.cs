using Character.Player;
using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine;
using UnityEngine.AI;

public class HeroState : NetworkBehaviour
{
    [Networked] [field:SerializeField]
    protected float CurrHealth {get; set;}
    
    [Networked] [field:SerializeField]
    protected float MaxHealth {get; set;}
    
    public override void Spawned()
    {
        MaxHealth = 100;
        CurrHealth = MaxHealth;
    }

    public float GetCurrHealth()
    {
        return CurrHealth;
    }
}
