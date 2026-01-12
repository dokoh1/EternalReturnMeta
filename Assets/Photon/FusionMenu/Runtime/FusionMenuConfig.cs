namespace Fusion.Menu
{
    using System.Collections.Generic;
    using UnityEngine;
    
    // 시스템 전반에 걸쳐 공통으로 사용하는 설정 값을 저장하는 ScriptableObject
    [ScriptHelp(BackColor = ScriptHeaderBackColor.Blue)]
    [CreateAssetMenu(menuName = "Fusion/Menu/Menu Config")]
    public class FusionMenuConfig : FusionScriptableObject, IFusionMenuConfig
    {
        [InlineHelp, SerializeField] protected int _maxPlayers = 6;
        [InlineHelp, SerializeField] protected bool _adaptFramerateForMobilePlatform = true;
        [InlineHelp, SerializeField] protected List<string> _availableAppVersions;
        [InlineHelp, SerializeField] protected List<string> _availableRegions;
        [InlineHelp, SerializeField] protected List<PhotonMenuSceneInfo> _availableScenes;
        [InlineHelp, SerializeField] protected FusionMenuMachineId _machineId;
        [InlineHelp, SerializeField] protected FusionMenuPartyCodeGenerator _codeGenerator;
        
        public List<string> AvailableAppVersions => _availableAppVersions;
        public List<string> AvailableRegions => _availableRegions;
        public List<PhotonMenuSceneInfo> AvailableScenes => _availableScenes;
        public int MaxPlayerCount => _maxPlayers;
        public virtual string MachineId => _machineId?.Id;
        public FusionMenuPartyCodeGenerator CodeGenerator => _codeGenerator;
        public bool AdaptFramerateForMobilePlatform => _adaptFramerateForMobilePlatform;
    }
}