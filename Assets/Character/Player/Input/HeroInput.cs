using Fusion;
using UnityEngine;

public enum InputButton
{
    SkillQ,
    SkillW,
    SkillE,
    SkillR,
    LeftClick,
    RightClick,
}

public struct HeroInput : INetworkInput
{
    public NetworkButtons Buttons;
    public Vector3 HitPosition_RightClick;
    public Vector3 HitPosition_Skill;

    public PlayerRef Owner;
    public Vector3 MousePosition;
}