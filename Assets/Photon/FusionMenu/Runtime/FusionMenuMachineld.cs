namespace Fusion.Menu
{
    // usionMenuMachineId는 로컬 머신마다 고유한 앱 버전 ID를 갖는 ScriptableObject로,
    // Photon Matchmaking에서 서로 다른 개발 환경/버전 간의 연결을 방지
    [ScriptHelp(BackColor = ScriptHeaderBackColor.Blue)]
    public class FusionMenuMachineId : FusionScriptableObject
    {
        [InlineHelp] public string Id;
    }
}