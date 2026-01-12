using Fusion;
using UnityEngine;

public struct PlayerInputData : INetworkInput
{
   public const byte MOUSEBUTTON0 = 1;
   public const byte MOUSEBUTTON1 = 2;
   
   public NetworkButtons buttons;
   public Vector3 direction;
   
   public Vector2 LookRotationDelta;
   public Vector3 MoveDirection;
}
