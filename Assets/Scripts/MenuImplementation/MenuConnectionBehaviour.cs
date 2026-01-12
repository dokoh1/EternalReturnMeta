using Fusion;
using Fusion.Menu;
using UnityEngine;

namespace EternalReturn.Menu
{
    // Fusion 메뉴 시스템에서 네트워크 연결 객체를 생성해주는 역할
    // Fusion UI와 실제 네트워크 연결 사이의 **"생성자 역할 브릿지"*
    public class MenuConnectionBehaviour : FusionMenuConnectionBehaviour
    {
        // Fusion의 설정 데이터 객체
        [SerializeField] private FusionMenuConfig config;
        [Space]
        [Header("Provide a NetworkRunner prefab to be instantiated.\nIf no prefab is provided, a simple one will be created.")]
        [SerializeField] private NetworkRunner networkRunnerPrefab;

        private void Awake()
        {
            if (!config)
                Log.Error("Fusion menu configuration file not provided.");
        }

        // 메뉴 시스템에서 실제 네트워크 연결 객체인 MenuConnection을 생성해서 반환함
        public override IFusionMenuConnection Create()
        {
            return new MenuConnection(config, networkRunnerPrefab);
        }
    }
}