// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// System_Test.cs - 개발 초기 테스트 코드 (DEPRECATED)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ ⚠️ 주의: 이 파일은 더 이상 사용되지 않습니다 (DEPRECATED)                                           │
// │                                                                                                       │
// │ 이 파일은 Photon Fusion 네트워킹 초기 테스트 시 사용했던 코드입니다.                                 │
// │ 대부분의 코드가 주석 처리되어 있으며, 실제 게임에서는 사용되지 않습니다.                             │
// │                                                                                                       │
// │ 현재 네트워킹 구현:                                                                                   │
// │ - 세션 관리: 별도의 SessionManager 또는 Lobby 시스템                                                 │
// │ - 플레이어 스폰: NetworkSpawnManager 등                                                              │
// │ - 입력 처리: HeroMovement.cs의 OnInput()                                                             │
// │                                                                                                       │
// │ 남아있는 활성 코드:                                                                                   │
// │ - SelectPrefab(): 캐릭터 선택 시 프리팹 반환 (현재는 항상 _playerPrefab 반환)                        │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
// │ 주석 처리된 코드 설명 (참고용)                                                                       │
// │                                                                                                       │
// │ 1. Start() / OnGUI() / StartGame()                                                                  │
// │    - Host/Client 모드로 세션 시작                                                                   │
// │    - #if UNITY_SERVER로 서버/클라이언트 분기                                                        │
// │                                                                                                       │
// │ 2. OnPlayerJoined() / OnPlayerLeft()                                                                │
// │    - INetworkRunnerCallbacks 인터페이스 구현                                                        │
// │    - 플레이어 접속/퇴장 시 캐릭터 스폰/디스폰                                                       │
// │                                                                                                       │
// │ 3. OnInput()                                                                                         │
// │    - 키보드/마우스 입력을 HeroInput 구조체로 변환                                                   │
// │    - 현재는 HeroMovement.cs에서 처리                                                                │
// └─────────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Addons.SimpleKCC;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class System_Test : MonoBehaviour
{
    [SerializeField] private NetworkPrefabRef _playerPrefab;

    private NetworkRunner _Runner;
    
    private HeroInput _heroInput;
    
    private bool resetInput;
    
    private bool _mouseButton0;
    
    public NetworkPrefabRef SelectPrefab(CharacterDataEnum character)
    {
        return _playerPrefab;
    }

// #if UNITY_SERVER
//     
//     private async void Start()
//     {
//         Debug.unityLogger.logEnabled = false;
//
//         // // Create the Fusion runner and let it know that we will be providing user input
//         _Runner = gameObject.AddComponent<NetworkRunner>();
//         _Runner.ProvideInput = true;
//         gameObject.AddComponent<RunnerSimulatePhysics3D>();
//         
//         
//         // Create the NetworkSceneInfo from the current scene
//         var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
//         var sceneInfo = new NetworkSceneInfo();
//         if (scene.IsValid) {
//             sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
//         }
//
//         // Start or join (depends on gamemode) a session with a specific name
//         await _Runner.StartGame(new StartGameArgs()
//         {
//             GameMode = GameMode.Server,
//             SessionName = "TestRoom",
//             SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
//         });
//
//     }
//     
//     #else
//     
//     private void OnGUI()
//     {
//         if (_Runner == null)
//         {
//             if (GUI.Button(new Rect(0,0,200,40), "Host"))
//             {
//                 StartGame(GameMode.Host);
//             }
//             if (GUI.Button(new Rect(0,40,200,40), "Join"))
//             {
//                 StartGame(GameMode.Client);
//             }
//         }
//     }
//     
//     async void StartGame(GameMode mode)
//     {
//         // // Create the Fusion runner and let it know that we will be providing user input
//         _Runner = gameObject.AddComponent<NetworkRunner>();
//         _Runner.ProvideInput = true;
//         gameObject.AddComponent<RunnerSimulatePhysics3D>();
//         
//         
//         // Create the NetworkSceneInfo from the current scene
//         var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
//         var sceneInfo = new NetworkSceneInfo();
//         if (scene.IsValid) {
//             sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
//         }
//
//         // Start or join (depends on gamemode) a session with a specific name
//         var result = await _Runner.StartGame(new StartGameArgs()
//         {
//             GameMode = mode,
//             SessionName = "TestRoom",
//             Scene = scene,
//             SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
//         });
//
//         if (!result.Ok)
//         {
//             Debug.LogError("서버 접속 실패: " + result.ShutdownReason);
//         }
//     }
//
//    #endif

//     public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
//     {
//         // if (runner.IsServer)
//         // {
//         //     // Create a unique position for the player
//         //     NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity, player);
//         //      
//         //     runner.SetPlayerObject(player, networkPlayerObject);
//         //     
//         //     // Keep track of the player avatars for easy access
//         //     _spawnedCharacters.Add(player, networkPlayerObject);
//         // }
//     }
//
//     public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
//     {
//         // if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
//         // {
//         //     runner.Despawn(networkObject);
//         //     _spawnedCharacters.Remove(player);
//         // }
//     }
//     
//     public void OnInput(NetworkRunner runner, NetworkInput input)
//     {
// // #if UNITY_SERVER
// //         return;
// // #endif
// //         if (runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject networkObject))
// //         {
// //             if (networkObject.GetComponentInChildren<HeroState>().GetCurrHealth() <= 0f)
// //             {
// //                 return;
// //             }
// //         }
// //         
// //         if (resetInput)
// //         {
// //             resetInput = false;
// //             _heroInput = default;
// //         }
// //
// //         Keyboard keyboard = Keyboard.current;
// //         
// //         NetworkButtons buttons = default;
// //         
// //         Mouse mouse = Mouse.current;
// //         if (mouse != null)
// //         {
// //             buttons.Set(InputButton.LeftClick, mouse.leftButton.isPressed);
// //             buttons.Set(InputButton.RightClick, mouse.rightButton.isPressed);
// //         }
// //         
// //         if (keyboard != null)
// //         {
// //             buttons.Set(InputButton.SkillQ, keyboard.qKey.isPressed);
// //             buttons.Set(InputButton.SkillW, keyboard.wKey.isPressed);
// //             buttons.Set(InputButton.SkillE, keyboard.eKey.isPressed);
// //             buttons.Set(InputButton.SkillR, keyboard.rKey.isPressed);
// //         }
// //         
// //         if (mouse.rightButton.isPressed)
// //         {
// //             var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
// //             if (Physics.Raycast(ray, out var hit, Mathf.Infinity, LayerMask.GetMask("Ground")))
// //             {
// //                 _heroInput.HitPosition_RightClick = hit.point;
// //             }
// //         }
// //
// //         if (keyboard.qKey.isPressed || keyboard.wKey.isPressed || keyboard.eKey.isPressed || keyboard.rKey.isPressed)
// //         {
// //             var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
// //             if (Physics.Raycast(ray, out var hit, Mathf.Infinity, LayerMask.GetMask("Ground")))
// //             {
// //                 _heroInput.HitPosition_Skill = hit.point;
// //             }
// //
// //             _heroInput.Owner = _Runner.LocalPlayer;
// //         }
// //         
// //         var loopRay = Camera.main.ScreenPointToRay(Input.mousePosition);
// //         if (Physics.Raycast(loopRay, out var hit2, Mathf.Infinity, LayerMask.GetMask("Ground")))
// //         {
// //             _heroInput.MousePosition = new Vector3(hit2.point.x, 0, hit2.point.z);
// //         }
// //
// //         
// //         _heroInput.Buttons = new NetworkButtons(_heroInput.Buttons.Bits | buttons.Bits);
// //
// //         
// //         input.Set(_heroInput);
// //         resetInput = true;
//     }
//     
//     public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
//     {
//     }
//
//     public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
//     {
//     }
//     
//     public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
//     {
//     }
//
//     public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
//     {
//     }
//
//     public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
//     {
//     }
//
//     public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
//     {
//     }
//
//     public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
//     {
//     }
//
//     public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
//     {
//     }
//
//     public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
//     
//
//     public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
//
//     public void OnConnectedToServer(NetworkRunner runner) { }
//
//     public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
//
//     public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
//
//     public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
//
//     public void OnSceneLoadDone(NetworkRunner runner) { }
//
//     public void OnSceneLoadStart(NetworkRunner runner) { }
}
