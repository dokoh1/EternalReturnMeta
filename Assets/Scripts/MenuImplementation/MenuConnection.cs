using Fusion;
using Fusion.Menu;
using Fusion.Photon.Realtime;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EternalReturn.Menu
{
    // 메뉴에서 입력한 정보로 실제 네트워크 연결을 수정하고, Fusion의 NetworkRunner를 설정함
    public class MenuConnection : IFusionMenuConnection
    {
        // 메뉴 설정(config)과 NetworkRunner 프리팹을 전달받아 저장
        // 나중에 런타임에서 새로운 Runner 인스턴스를 만들어 연결에 사용함
        public MenuConnection(IFusionMenuConfig config, NetworkRunner runnerPrefab)
        {
            _config = config;
            _runnerPrefab = runnerPrefab;
        }
        
        // 현재 접속 중인 세션 이름
        public string SessionName { get; private set; }
        // 최대 플레이어 수
        public int MaxPlayerCount { get; private set; }
        // 연결 시 선택된 지역/앱 버전
        public string Region { get; private set; }
        public string AppVersion { get; private set; }
        
        // 접속 중인 플레이어들의 닉네임 목록
        public List<string> Usernames { get; private set; }
        // 현재 Runner가 실행 중인지 여부
        public bool IsConnected => _runner && _runner.IsRunning;
        
        // 현재 플레이어의 핑
        public int Ping => (int)(IsConnected ? _runner.GetPlayerRtt(_runner.LocalPlayer) * 1000 : 0);
        
        private NetworkRunner _runnerPrefab;
        private NetworkRunner _runner;
        private IFusionMenuConfig _config;
        private bool _connectingSafeCheck;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;
        // 사용자가 연결을 시도할 때 호출됨
        public async Task<ConnectResult> ConnectAsync(IFusionMenuConnectArgs connectArgs)
        {
            
            // 연결 중인지 확인 연결 중이면 ConnectResult()를 리턴
            if (_connectingSafeCheck)
                return new ConnectResult()
                    { CustomResultHandling = true, Success = false, FailReason = ConnectFailReason.None };
            _connectingSafeCheck = true;
            // 기존 연결 종료
            if (_runner && _runner.IsRunning)
            {
                await _runner.Shutdown();
            }
            // 러너 생성 및 설정
            _runner = CreateRunner();
            _runner.gameObject.AddComponent<RunnerSimulatePhysics3D>();
            
            // 기본 SceneManager를 부착함
            var SceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            // 씬 권한 넘겨받기 기능 비활성화
            SceneManager.IsSceneTakeOverEnabled = false;
            
            // Photon 관련 설정을 메뉴에서 전달받은 값으로 세팅
            var appSettings = CopyAppSettings(connectArgs);

            // Fusion에서 방을 시작할 때 필요한 정보
            var args = new StartGameArgs();
            args.CustomPhotonAppSettings = appSettings;
            args.GameMode = ResolveGameMode(connectArgs);
            args.SessionName = SessionName = connectArgs.Session;
            args.PlayerCount = MaxPlayerCount = connectArgs.MaxPlayerCount;
            
            // 추후 연결 취소 (DisconnectAsync)에 사용할 CancellationToken
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
            args.StartGameCancellationToken = _cancellationToken;
            
            // 사용자가 세션이름을 비워둔 경우, 자동으로 생성되는 세션 이름
            // 지역 정보를 포함한 party code를 생성
            var regionIndex = _config.AvailableRegions.IndexOf(connectArgs.Region);
            args.SessionNameGenerator = () =>
                _config.CodeGenerator.EncodeRegion(_config.CodeGenerator.Create(), regionIndex);
            
            // 두개로 나누어논 이유는 UI 시스템은 ConnectResult가 필요하기 떄문
            // StartGame() 호출 결과
            var startGameResult = default(StartGameResult);
            // 우리가 정의한 사용자용 결과
            var connectResult = new ConnectResult();
            startGameResult = await _runner.StartGame(args);

            connectResult.Success = startGameResult.Ok;
            connectResult.Runner = _runner;
            connectResult.FailReason = ResolveConnectFailReason(startGameResult.ShutdownReason);
            _connectingSafeCheck = false;
            if (connectResult.Success)
            {
                SessionName = _runner.SessionInfo.Name;
            }
            var spawner =  Object.FindAnyObjectByType<MatchingManagerSpawner>();
            spawner.Initialize(_runner);
            // spawner.SpawnMatchingManager();
            return connectResult;
        }

        // Fusion을 통해 연결된 세션에서 정상적으로 연결을 끊고, 필요 시 씬을 정리하는 함수
        public async Task DisconnectAsync(int reason)
        {
            // 현재 Runner의 NetworkMode를 가져옴
            var peerMode = _runner.Config?.PeerMode;
            // 토큰을 통해 강제로 StartGame()을 취소
            _cancellationTokenSource.Cancel();
            await _runner.Shutdown(shutdownReason: ResolveShutdownReason(reason));
            
            // 이 모드는 복수 Runner가 동시에 돌아가는 구조라, 씬 공유가 복잡하므로 return
            if (peerMode is NetworkProjectConfig.PeerModes.Multiple) return;
            // 로드된 씬 unload로 정리
            for (int i = SceneManager.sceneCount - 1; i > 0; i--)
            {
                SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            }
        }
        
        // Fusion의 메뉴 시스템에서 사용 가능한 온라인 지역(region) 목록을 요청하는 함수
        public Task<List<FusionMenuOnlineRegion>> RequestAvailableOnlineRegionsAsync(IFusionMenuConnectArgs connectArgs)
        {
            return Task.FromResult(new List<FusionMenuOnlineRegion>() { new FusionMenuOnlineRegion(){ Code = string.Empty, Ping = 0}});
        }

        // 현재 방(Session)에 접속한 유저들의 이름 목록을 저장하는 메서드
        public void SetSessionUsernames(List<string> usernames)
        {
            Usernames = usernames;
        }
        // UI 계층의 ConnectFailReason 값을,
        //     Fusion이 내부적으로 사용하는 ShutdownReason 타입으로 변환함
        private ShutdownReason ResolveShutdownReason(int reason)
        {
            switch (reason)
            {
                case ConnectFailReason.UserRequest :
                    return ShutdownReason.Ok;
                case ConnectFailReason.ApplicationQuit :
                    return ShutdownReason.Ok;
                case ConnectFailReason.Disconnect:
                    return ShutdownReason.DisconnectedByPluginLogic;
                default:
                    return ShutdownReason.Error;
            }
        }
        //Fusion의 내부 ShutdownReason을 → 메뉴 UI의 ConnectFailReason으로 변환
        private int ResolveConnectFailReason(ShutdownReason reason)
        {
            switch (reason)
            {
                case ShutdownReason.Ok:
                case ShutdownReason.OperationCanceled:
                    return ConnectFailReason.UserRequest;
                case ShutdownReason.DisconnectedByPluginLogic:
                case ShutdownReason.Error:
                    return ConnectFailReason.Disconnect;
                default:
                    return ConnectFailReason.None;
            }
        }
        // GameMode를 설정하는 함수
        private GameMode ResolveGameMode(IFusionMenuConnectArgs args)
        {
            // if (args.Creating)
            // {
            //     Debug.Log("Host");
            //     return GameMode.Host;
            // }
            //
            // if (string.IsNullOrEmpty(args.Session))
            // {
            //     Debug.Log("Quick");
            //     return GameMode.AutoHostOrClient;
            // }
            // Debug.Log("Client");
            if (args.IsServer)
            {
                return GameMode.Server;
            }
            
            return GameMode.Client;
        }
        // 네트워크 세션을 실행할 NetworkRunner 인스턴스를 Prefab으로 생성
        private NetworkRunner CreateRunner()
        {
            return _runnerPrefab ? UnityEngine.Object.Instantiate(_runnerPrefab) : new GameObject("NetworkRunner", typeof(NetworkRunner)).AddComponent<NetworkRunner>();
        }
        
        // 메뉴 UI에서 선택한 설정을 기반으로 Photon 연결 세부 설정을 구성
        private FusionAppSettings CopyAppSettings(IFusionMenuConnectArgs connectArgs)
        {
            FusionAppSettings appSettings = new FusionAppSettings();
            PhotonAppSettings.Global.AppSettings.CopyTo(appSettings);
            appSettings.FixedRegion = connectArgs.Region;
            appSettings.AppVersion = AppVersion = connectArgs.AppVersion;
            return appSettings;
        }
    }
}
