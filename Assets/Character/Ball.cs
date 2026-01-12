using UnityEngine;
using Fusion;

public class Ball : NetworkBehaviour
{
    [Networked] private TickTimer life { get; set; }
    
    public const byte MOUSEBUTTON0 = 1;

    public NetworkButtons buttons;
    public Vector3 direction;
    
    public void Init()
    {
        life = TickTimer.CreateFromSeconds(Runner, 5.0f);
    }
    public override void FixedUpdateNetwork()
    {
        if(life.Expired(Runner))
            Runner.Despawn(Object);
        else
            transform.position += 5 * transform.forward * Runner.DeltaTime;
    }
}
