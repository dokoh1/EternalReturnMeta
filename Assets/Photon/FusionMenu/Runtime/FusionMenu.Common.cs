// 	Fusion 메뉴 시스템 설정을 외부에서 커스터마이징하기 위한 인터페이스
#region IFusionMenuConfig.cs

namespace Fusion.Menu
{
    using System.Collections.Generic;

    public interface IFusionMenuConfig
    {
        //메뉴에서 보여줄 앱 버전
        List<string> AvailableAppVersions { get; }
        // Region 목록
        List<string> AvailableRegions { get; }
        //전환 가능한 씬 목록
        List<PhotonMenuSceneInfo> AvailableScenes { get; }
        // 게임 모드에서 허용할 최대 플레이어 수
        int MaxPlayerCount { get; }
        // 장치 식별자
        string MachineId { get; }
        // 파티 코드 생성기
        FusionMenuPartyCodeGenerator CodeGenerator { get; }
        //모바일 기기에서 60fps 강제 적용할지 여부
        bool AdaptFramerateForMobilePlatform { get; }
    }
    
}
#endregion

//  Fusion 메뉴 시스템에서 네트워크 접속(연결) 관련 정보를 주고받기 위한 데이터 구조
#region IFusionMenuConnectArgs.cs

namespace Fusion.Menu
{
    public interface IFusionMenuConnectArgs
    {
        string Username { get; set; }
        string Session { get; set; }
        string PreferredRegion { get; set; }
        string Region { get; set; }
        string AppVersion { get; set; }
        int MaxPlayerCount { get; set; }
        PhotonMenuSceneInfo Scene { get; set; }
        bool Creating { get; set; }
        bool IsServer { get; set; }
        void SetDefaults(IFusionMenuConfig config);
    }
}
#endregion

// 실제 네트워크 연결과 관련된 기능들을 추상화
#region IFusionMenuConnection.cs

namespace Fusion.Menu
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IFusionMenuConnection
    {
        string SessionName { get; }
        int MaxPlayerCount { get; }
        string Region { get; }
        string AppVersion { get; }
        List<string> Usernames { get; }
        bool IsConnected { get; }
        public int Ping { get; }
        Task<ConnectResult> ConnectAsync(IFusionMenuConnectArgs connectArgs);
        Task DisconnectAsync(int reason);
        Task<List<FusionMenuOnlineRegion>> RequestAvailableOnlineRegionsAsync(IFusionMenuConnectArgs connectArgs);


    }
}

#endregion
#region IFusionMenuModal.cs

namespace Fusion.Menu
{
    using System.Threading.Tasks;

    public interface IFusionMenuModal
    {
        void OpenModal(string msg, string msg1, string header);
        Task OpenModalAsync(string msg, string header);
    }
}
#endregion
//Fusion 메뉴 시스템에서 팝업 메시지를 띄울 때 사용할 수 있도록 정의된 인터페이스
#region IFusionMenuPopup.cs

namespace Fusion.Menu
{
    using System.Threading.Tasks;
    
    public interface IFusionMenuPopup
    {
        // 즉시 팝업을 띄우는 메서드
        void OpenPopup(string msg, string header);
        // 비동기 팝업 열기
        // 팝업이 열리고 닫힐 때까지 기다릴 수 있도록 await 가능한 형태
        Task OpenPopupAsync(string msg, string header);
    }
}

#endregion

// Fusion Menu UI 에서 화면 전환, 팝업 표시, 화면 참조 기능을 제공하는 인터페이스
#region IFusionMenuUIController.cs

namespace Fusion.Menu
{
    using System.Threading.Tasks;
    
    public interface IFusionMenuUIController
    {
        // FusionMenuUIScreen 타입의 화면을 보여줌.
        void Show<S>() where S : FusionMenuUIScreen;
        // 팝업창을 띄움
        void Popup(string msg, string header = default);
        // 비동기 팝업창을 띄움
        public Task PopupAsync(string msg, string header = default);
        // 특정한 화면 객체를 참조해서 가져옴
        S Get<S>() where S : FusionMenuUIScreen;
    }
}
#endregion

// 멀티플레이어 접속 정보를 저장하고 관리하는 핵심 클래스
#region FusionMenuConnectArgs.cs

namespace Fusion.Menu
{
    using UnityEngine;

    // PlayerPrefs에 저장해서 Unity 앱을 껐다 켜도 일부 정보(이름, 지역, 버전 등)가 유지돼.
    //
    //     이 클래스는 모든 메뉴 화면에서 공유하는 단일 인스턴스로 사용됨.
    //
    //     SetDefaults()는 누락된 정보나 잘못된 값을 자동으로 채워서 항상 올바른 상태로 유지해줌.
    //
    //     Scene, AppVersion, PreferredRegion 등은 주로 매치메이킹 조건으로 사용돼.
    public partial class FusionMenuConnectArgs : IFusionMenuConnectArgs
    {
        public virtual string Username
        {
            get => PlayerPrefs.GetString("Photon.Menu.Username");
            set => PlayerPrefs.SetString("Photon.Menu.Username", value);
        }
        
        public virtual string Session { get; set; }

        public virtual string PreferredRegion
        {
            get => PlayerPrefs.GetString("Photon.Menu.Region");
            set => PlayerPrefs.SetString("Photon.Menu.Region", string.IsNullOrEmpty(value) ? value : value.ToLower());
        }
        
        public virtual string Region { get; set; }

        public virtual string AppVersion
        {
            get => PlayerPrefs.GetString("Photon.Menu.AppVersion");
            set => PlayerPrefs.SetString("Photon.Menu.AppVersion", value);
        }

        public virtual int MaxPlayerCount
        {
            get => PlayerPrefs.GetInt("Photon.Menu.MaxPlayerCount");
            set => PlayerPrefs.SetInt("Photon.Menu.MaxPlayerCount", value);
        }
        
        // 사용자가 선택한 맵 또는 게임 씬 정보
        // JSON으로 정렬해서 PlayerPrefs에 저장
        public virtual PhotonMenuSceneInfo Scene
        {
            get
            {
                try
                {
                    return JsonUtility.FromJson<PhotonMenuSceneInfo>(PlayerPrefs.GetString("Photon.Menu.Scene"));
                }
                catch
                {
                    return default(PhotonMenuSceneInfo);
                }
            }
            set => PlayerPrefs.SetString("Photon.Menu.Scene", JsonUtility.ToJson(value));
        }
        public virtual bool Creating { get; set; }
        public virtual bool IsServer { get; set; }

        partial void SetDefaultsUser(IFusionMenuConfig config);

        // AppVersion : 설정된 MachineId가 아니거나 목록에 없으면 기본값으로 변경
        // PreferredRegion : 사용 불가능한 지역이면 비움
        // MaxPlayerCount : 설정된 값이 0 이하 또는 초과하면 최대값으로 조정
        // Username : 비어 있으면 무작위 생성
        // Scene : 유효하지 않으면 첫 번째 씬으로 대체
        public virtual void SetDefaults(IFusionMenuConfig config)
        {
            Session = null;
            Creating = false;

            if (AppVersion == null || (AppVersion != config.MachineId &&
                                       config.AvailableAppVersions.Contains(AppVersion) == false))
            {
                AppVersion = config.MachineId;
            }

            if (PreferredRegion != null && config.AvailableRegions.Contains(PreferredRegion) == false)
            {
                PreferredRegion = string.Empty;
            }

            if (MaxPlayerCount <= 0 || MaxPlayerCount > config.MaxPlayerCount)
            {
                MaxPlayerCount = config.MaxPlayerCount;
            }

            if (string.IsNullOrEmpty(Username))
            {
                Username = $"Player{config.CodeGenerator.Create(3)}";
            }

            if (string.IsNullOrEmpty(Scene.Name) && config.AvailableScenes.Count > 0)
            {
                Scene = config.AvailableScenes[0];
            }   
            else
            {
                var index = config.AvailableScenes.FindIndex(s => s.Name == Scene.Name);
                if (index >= 0)
                {
                    Scene = config.AvailableScenes[Mathf.Clamp(index, 0, config.AvailableScenes.Count - 1)];
                }
            }
            SetDefaultsUser(config);
        }
    }
}
#endregion

// Fusion 연결 시 실패 이유가 정의된 인터페이스
#region FusionMenuConnectFailReason.cs

namespace Fusion.Menu
{
    public partial class ConnectFailReason
    {
        public const int None = 0;
        public const int UserRequest = 1;
        public const int ApplicationQuit = 2;
        public const int Disconnect = 3;
    }
}
#endregion

// MonoBehaviour 기반의 네트워크 연결 래퍼 클래스입니다.
// 실제 네트워크 로직은 IFusionMenuConnection 인터페이스로 추상화되어 있고
// 이 클래스는 그 추상 객체를 Unity GameObject에서 다룰 수 있게 도와줍니다.
#region FusionMenuConnectionBehaviour.cs

namespace Fusion.Menu
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public abstract class FusionMenuConnectionBehaviour : FusionMonoBehaviour, IFusionMenuConnection
    {
        // 실제 네트워크 연결 객체
        public IFusionMenuConnection Connection;
        
        // 내부 Connection 객체에서 값 위임 받음
        public string SessionName => Connection.SessionName;
        public int MaxPlayerCount => Connection.MaxPlayerCount;
        public string Region => Connection.Region;
        public string AppVersion => Connection.AppVersion;
        public List<string> Usernames => Connection.Usernames;
        
        // 현재 연결이 유효하고 연결 상태인지 여부
        public bool IsConnected => Connection != null && Connection.IsConnected;
        // 현재 서버와의 지연 시간
        public int Ping => Connection.Ping;

        // 연결 직전에 연결 인자에 사용자 커스터마이징을 적용할 수 있는 콜백
        public Action<IFusionMenuConnectArgs> OnBeforeConnect;
        
        // 연결 해제 전에 사용자 정의 콜백 호출 가능
        public Action<IFusionMenuConnection, int> OnBeforeDisconnect;
        
        // 내부적으로 Connection이 null일 때 호출되어 초기화됩니다.
        public abstract IFusionMenuConnection Create();

        //실제 연결을 시도하는 비동기 메서드 Connection이 없으면 Create 호출로 생성, OnBeforeConnect 콜백을 실행, 내부 Connection.ConnectAsync() 실행
        public virtual Task<ConnectResult> ConnectAsync(IFusionMenuConnectArgs connectArgs)
        {
            if (Connection == null)
                Connection = Create();

            if (OnBeforeConnect != null)
            {
                try
                {
                    OnBeforeConnect.Invoke(connectArgs);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                    return Task.FromResult(new ConnectResult {FailReason = ConnectFailReason.Disconnect, DebugMessage = e.Message});
                }
            }

            return Connection.ConnectAsync(connectArgs);
        }

        // 연결을 끊는 메서드입니다.
        // OnBeforeDisconnect 콜백 호출
        // DisconnectAsync() 실행
        // Connection이 없을 경우 아무것도 하지 않고 완료된 Task 반환
        public virtual Task DisconnectAsync(int reason)
        {
            if (Connection != null)
            {
                if (OnBeforeDisconnect != null)
                {
                    try
                    {
                        OnBeforeDisconnect.Invoke(Connection, reason);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogException(e);
                    }
                }
                return Connection.DisconnectAsync(reason);
            }
            return Task.CompletedTask;
        }

        // 접속 가능한 서버 지역 리스트를 요청합니다.
        // Connection이 없으면 먼저 생성한 뒤 해당 메서드 실행
        public virtual Task<List<FusionMenuOnlineRegion>> RequestAvailableOnlineRegionsAsync(
            IFusionMenuConnectArgs connectionArgs)
        {
            if (Connection == null)
                Connection = Create();
            return Connection.RequestAvailableOnlineRegionsAsync(connectionArgs);
        }
        
    }
}
#endregion

// "접속 결과"를 담는 용도
#region FusionMenuConnectResult.cs

namespace Fusion.Menu
{
    using System.Threading.Tasks;

    public partial class ConnectResult
    {
        // 접속 성공 여부
        public bool Success;
        //실패 코드
        public int FailReason;
        // 끊김 원인 코드
        public int DisconnectCause;
        // 디버깅용 메시지
        public string DebugMessage;
        // 직접 처리 여부 결정
        public bool CustomResultHandling;
        // 비동기 후처리 작업 대기
        public Task WaitForCleanup;
        public NetworkRunner Runner;
    }
}
#endregion

//유저가 메뉴에서 선택한 그래픽 설정을 PlayerPrefs에 저장하고, 게임 실행 중에 해당 설정을 적용해주는 역할을 하는 설정 관리자 클래스
#region FusionMenuGraphicsSettings.cs

namespace Fusion.Menu
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    
    public partial class FusionMenuGraphicsSettings
    {
        // 설정 가능한 프레임 속도 목록, '-1'은 제한 없음
        protected static int[] PossibleFramerates = new int[] { -1, 30, 60, 75, 90, 120, 144, 165, 240, 360 };

        // 저장된 프레임레이트를 가져오고, 유효하지 않으면 -1을 반환
        public virtual int Framerate
        {
            get
            {
                var f = PlayerPrefs.GetInt("Photon.Menu.Framerate", -1);
                if (PossibleFramerates.Contains(f) == false)
                    return PossibleFramerates[0];
                return f;
            }
            set => PlayerPrefs.SetInt("Photon.Menu.Framerate", value);
        }
        // 전체화면 여부 저장/적용
        public virtual bool Fullscreen
        {
            get => PlayerPrefs.GetInt("Photon.Menu.Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            set => PlayerPrefs.SetInt("Photon.Menu.Fullscreen", value ? 1 : 0);
        }
        // 유저가 메뉴에서 선택한 화면 해상도 설정을 저장하고 불러오는데 사용(선택된 해상도 인덱스를 관리)
        public virtual int Resolution
        {
            // 해상도를 값을 가져오고 없으면 현재 사용 중인 해상도에 가장 가까운 인덱스를 찾는 GetCurrentResolutionIndex를 기본값으로 사용함
            get => Math.Clamp(PlayerPrefs.GetInt("Photon.Menu.Resoultion", GetCurrentResolutionIndex()), 0,
                Math.Max(0, Screen.resolutions.Length - 1));
            set => PlayerPrefs.SetInt("Photon.Menu.Resoultion", value);
        }
        // 수직 동기화 활성화 여부 설정
        public virtual bool VSync
        {
            // 설정된 저장 값 또는 QualitySettings.vSyncCount에서 가져오고 보정
            get => PlayerPrefs.GetInt("Photon.Menu.VSync", Math.Clamp(QualitySettings.vSyncCount, 0, 1)) == 1;
            set => PlayerPrefs.SetInt("Photon.Menu.VSync", value ? 1 : 0);
        }
        // Unity에서 지원하는 그래픽 품질 레벨(Low ~ Ultra) 인덱스 저장/ 불러오기
        public virtual int QualityLevel
        {
            // 설정된 저장 값 또는 QualitySettings.GetQualityLevel()에서 가져오고 보정
            get
            {
                var q = PlayerPrefs.GetInt("Photon.Menu.QualityLevel", QualitySettings.GetQualityLevel());
                q = Math.Clamp(q, 0, QualitySettings.names.Length - 1);
                return q;
            }
            set => PlayerPrefs.SetInt("Photon.Menu.QualityLevel", value);
        }
        //PossibleFramerates에서 현재 모니터 주사율 이하의 프레임만 추림
        // 즉 75Hz 모니터면 120fps 같은 건 목록에서 제외함
        // Unity 2022.2 이상 -> refreshRateRatio 사용
        // 이하 버전 -> 기존 refreshRate 사용
        public virtual List<int> CreateFramerateOptions => PossibleFramerates.Where(f => f <=
#if UNITY_2022_2_OR_NEWER
                (int)Math.Round(Screen.currentResolution.refreshRateRatio.value)
#else
        Screen.CurrentResolution.refreshRateRatio
#endif
        ).ToList();
        
        // 현재 PC에서 사용 가능한 모든 해상도 인덱스를 리스트로 반환
        // 예: Screen.resolutions = [1280x720, 1920x1080, 2560x1440]
        // → CreateResolutionOptions = [0, 1, 2]
        public virtual List<int> CreateResolutionOptions => Enumerable.Range(0, Screen.resolutions.Length).ToList();
        
        // Unity에서 설정한 품질 레벨의 인덱스 목록 반환
        // 예: "Low", "Medium", "High" → [0, 1, 2]
        public virtual List<int> CreateGraphicsQualityOptions => Enumerable.Range(0, QualitySettings.names.Length).ToList();
        
        // 이건 부분 메서드로, 다른 SDK 수준에서 추가 커스터마이징이 가능하도록 열어놓은 확장 포인트야
        partial void ApplyUser();
        
        // 그래픽 설정 적용
        public virtual void Apply()
        {
            // 모바일 플랫폼에서는 해상도 설정 불가
#if !UNITY_IOS && !UNITY_ANDROID
            if (Screen.resolutions.Length > 0)
            {
                var resolution = Screen.resolutions[Resolution < 0 ? Screen.resolutions.Length - 1 : Resolution];
                if (Screen.currentResolution.width != resolution.width ||
                    Screen.currentResolution.height != resolution.height || 
                    Screen.fullScreen != Fullscreen)
                {
                    Screen.SetResolution(resolution.width, resolution.height, Fullscreen);
                }
            }
#endif
            if (QualitySettings.GetQualityLevel() != QualityLevel)
            {
                QualitySettings.SetQualityLevel(QualityLevel);
            }

            if (QualitySettings.vSyncCount != (VSync ? 1 : 0))
            {
                QualitySettings.vSyncCount = VSync ? 1 : 0;
            }

            if (Application.targetFrameRate != Framerate)
            {
                Application.targetFrameRate = Framerate;
            }
            ApplyUser();
        }
        // 현재 Unity 화면 해상도와 일치하는 해상도 인덱스를 Screen.resolutions 목록에서 찾아서 반환
        // PlayerPrefs에 저장된 해상도 인덱스가 없을 경우의 기본값으로 사용됨
        private int GetCurrentResolutionIndex()
        {
            var resolutions = Screen.resolutions;
            if (resolutions == null || resolutions.Length == 0)
                return -1;
            int currentWidth = Mathf.RoundToInt(Screen.width);
            int currentHeight = Mathf.RoundToInt(Screen.height);

#if UNITY_2020_2_OR_NEWER
            var defaultRefreshRate = resolutions[^1].refreshRateRatio;
#else
            var defaultRefreshRate = resolutions[^1].refreshRate;
#endif
            for (int i = 0; i < resolutions.Length; i++)
            {
                var resolution = resolutions[i];

                if (resolution.width == currentWidth
                    && resolution.height == currentHeight
#if UNITY_2022_2_OR_NEWER
                    && resolution.refreshRateRatio.denominator == defaultRefreshRate.denominator
                    && resolution.refreshRateRatio.numerator == defaultRefreshRate.numerator)
#else
                    && resolution.refreashRate == defaultRefreshRate)
#endif
                    return i;
            }

            return -1;
        }
        
    }
}
#endregion

// Photon 서버의 지역과 그 지역에 대한 핑 정보를 담기 위한 간단한 데이터 구조
#region FusionMenuOnlineRegion.cs

namespace Fusion.Menu
{
    using System;

    [Serializable]
    public struct FusionMenuOnlineRegion
    {
        public string Code;
        public int Ping;
    }
}

#endregion

// Fusion의 멀티플레이어 메뉴 시스템에서 사용자가 선택할 수 있도록 제공하는 게임 씬(Scene) 정보를 저장하는 용도로 사용
#region FusionMenuSceneInfo.cs

namespace Fusion.Menu
{
    using System;
    using System.IO;
    using UnityEngine;

    // 게임 씬 목록 UI 에서 보여줄 이름, 프리뷰 이미지, 씬 경로 정보를 포함해서 사용자가 씬을 선택하거나 게임 시작 시 어떤 씬을
    // 로드할지 결정할 때 사용되는 데이터 구조
    [Serializable]
    public partial struct PhotonMenuSceneInfo
    {
        public string Name;
        
        [ScenePath] public string ScenePath;
        public string SceneName => ScenePath == null ? null : Path.GetFileNameWithoutExtension(ScenePath);
        public Sprite Preview;
    }
}
#endregion

// Dropdown UI 요소에 옵션 리스트를 설정하고, 사용자가 선택한 값을 Value로 쉽게 가져오도록 도와주는 중간 계층 헬퍼 클래스
#region FusionMenuSettingsEntry.cs

namespace Fusion.Menu
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
#if FUSION_ENABLE_TEXTMESHPRO
    using Dropdown = TMPro.TMP_Dropdown;
#else
    using Dropdown = UnityEngine.UI.Dropdown;
#endif
    
    public class FusionMenuSettingsEntry<T> where T : IEquatable<T>
    {
        //연결된 UI 드롭다운 요소
        private Dropdown _dropdown;
        // 드롭다운에 표시할 실제 값들의 목록
        private List<T> _options;
        // default(T)는 T 타입의 기본값을 반환함 int면 0, string이면 null
        public T Value => _options == null || _options.Count == 0 ? default(T) : _options[_dropdown.value];
        
        // 드롭다운과 이벤트 콜백을 연결해줌
        // 기존 이벤트 리스너 제거 -> 새로 지정한 onValueChanged 연결
        // onValueChanged.Invoke()가 드롭다운 선택이 바뀔 때 실행됨
        public FusionMenuSettingsEntry(Dropdown dropdown, Action onValueChanged)
        {
            _dropdown = dropdown;
            _dropdown.onValueChanged.RemoveAllListeners();
            _dropdown.onValueChanged.AddListener(_ => onValueChanged.Invoke());
        }

        // 드롭다운에 표시할 옵션 리스트와 현재 선택 값을 설정하는 함수
        // Tostring은 각 값들을 드롭다운에표시할 문자열로 바꾸는 함수
        public void SetOptions(List<T> options, T current, Func<T, string> Tostring = null)
        {
            _options = options;
            _dropdown.ClearOptions();
            _dropdown.AddOptions(options.Select(o => Tostring != null ? Tostring(o) : o.ToString()).ToList());

            var index = _options.FindIndex(0, o => o.Equals(current));
            if (index >= 0)
            {
                _dropdown.SetValueWithoutNotify(index);
            }
            else
            {
                _dropdown.SetValueWithoutNotify(0);
            }
        }
    }
    
}
#endregion

// Fusion 메뉴 UI 시스템에서 전체 화면 전환과 팝업 처리를 담당하는 클래스
#region FusionMenuUIController.cs

namespace Fusion.Menu
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    
    public class FusionMenuUIController<T> : FusionMonoBehaviour, IFusionMenuUIController where T : IFusionMenuConnectArgs, new()
    {
        [InlineHelp, SerializeField] protected FusionMenuConfig _config;
        [InlineHelp, SerializeField] protected FusionMenuConnectionBehaviour _connection;
        [InlineHelp, SerializeField] protected FusionMenuUIScreen[] _screens;

        // 화면 타입을 기준으로 빠르게 찾기위한 맵
        protected Dictionary<Type, FusionMenuUIScreen> _screenLookup;
        
        // 팝업 띄우는 기능을 구현한 스크린
        protected IFusionMenuPopup _popupHandler;
        
        // 현재 보여지고 있는 화면
        protected FusionMenuUIScreen _activeScreen;

        // 연결 정보를 담는 객체를 새로 만들어서 반환
        public virtual T CreateConnectArgs => new T();

        protected virtual void Awake()
        {
            var connectionArgs = CreateConnectArgs;
            _screenLookup = new Dictionary<Type, FusionMenuUIScreen>();

            foreach (var screen in _screens)
            {
                screen.Config = _config;
                screen.Connection = _connection;
                screen.ConnectionArgs = connectionArgs;
                screen.Controller = this;

                // screen 객체의 런타임 타입을 가져옴
                // JoinScreen : FusionMenuUIScreen이면 t 는 joinScreen
                var t = screen.GetType();
                while (true)
                {
                    // baseType을 통해 부모 타입까지 다 등록함
                    // 부모가 FusionMenuUIScreen 일 경우, 이 이상은 상위 UI 기반이 아님
                    // 이 과정을 통해 다양한 타입으로 접근이 가능해짐
                    _screenLookup.Add(t, screen);
                    if (t.BaseType == null || typeof(FusionMenuUIScreen).IsAssignableFrom(t) == false ||
                        t.BaseType == typeof(FusionMenuUIScreen))
                    {
                        break;
                    }
        
                    t = t.BaseType;
                }
                // 이 Scene이 IFusionMenuPopup을 구현한 경우 PopupHandler에 담음
                if (typeof(IFusionMenuPopup).IsAssignableFrom(t))
                {
                    _popupHandler = (IFusionMenuPopup)screen;
                }
            }

            foreach (var screen in _screens)
            {
                screen.Init();
            }
        }
        // 게임 시작 시 첫 화면을 자동으로 보여줌
        protected virtual void Start()
        {
            if (_screens != null && _screens.Length > 0)
            {
                _screens[0].Show();
                _activeScreen = _screens[0];
            }
        }
        
        public virtual void Show<S>() where S : FusionMenuUIScreen
        {
            // screenLookup에서 해당 타입 S에 해당하는 화면 찾기
            if (_screenLookup.TryGetValue(typeof(S), out var result))
            {
                // 그 화면이 모달이 아니고, 현재 화면과 다르고, 현재 화면이 null이 아니면 Hide()
                // 모달 화면은 기존 화면 위에 겹쳐서 표시돼야 하므로 Hide 하면 안됨
                if (result.IsModal == false && _activeScreen != result && _activeScreen)
                {
                    _activeScreen.Hide();
                }
                // 현재 화면과 다르면 새 화면 실행
                if (_activeScreen != result)
                {
                    result.Show();
                }
                // 모달이 아닌 경우, _activeScreen을 새화면으로 설정
                if (result.IsModal == false)
                {
                    _activeScreen = result;
                }
            }
            else
            {
                Debug.LogError($"Show() - Screen type '{typeof(S).Name}' not found");
            }
        }
        // _screenLookup에서 타입 S에 해당하는 화면이 있으면 그것을 반환
        public virtual S Get<S>() where S : FusionMenuUIScreen
        {
            if (_screenLookup.TryGetValue(typeof(S), out var result))
            {
                return result as S;
            }
            else
            {
                Debug.LogError($"Show() - Screen type '{typeof(S).Name}' not found");
                return null;
            }
        }
        // _popupHandler가 null이 아니면 → 팝업 메시지 띄움, 즉시 팝업
        public void Popup(string msg, string header = default)
        {
            if (_popupHandler == null)
            {
                Debug.LogError($"Popup() - Popup handler is null");
            }
            else
            {
                _popupHandler.OpenPopup(msg, header);
            }
        }
        // 팝업이 닫힐 때까지 기다려야 하는 시나리오에서 사용 (ex. 경고 후 다음 진행), 비동기 팝업
        public Task PopupAsync(string msg, string header = default)
        {
            Debug.LogError($"Error Msg : {msg}");
            Debug.LogError($"Error Header : {header}");
            if (_popupHandler == null)
            {
                Debug.LogError("Popup() - no popup handler found");
                return Task.CompletedTask;
            }
            else
                return _popupHandler.OpenPopupAsync(msg, header);
        }
    }
}
#endregion
// UI 시스템의 기본 화면 뼈대 클래스로, Start, Show, Hide, Init 등의 생명주기 메서드와 UI 애니메이션 처리를 포함
// 외부에서 이 화면을 컨트롤 하기 위한 컨트롤러, 설정, 연결 정보도 들어 있음
#region FusionMenuUIScreen.cs

namespace Fusion.Menu
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public abstract class FusionMenuUIScreen : FusionMonoBehaviour
    {
        protected static readonly int HideAnimHash = Animator.StringToHash("Hide");
        protected static readonly int ShowAnimHash = Animator.StringToHash("Show");
        // 이 화면이 모달인지 여부를 나타냄
        [InlineHelp, SerializeField] private bool _isModal;
        
        // 이 화면과 함께 작동할 플러그인들(애니메이션 효과, 사운드, 전환 처리 등)을 연결
        [InlineHelp, SerializeField] private List<FusionMenuScreenPlugin> _plugins;
        
        private Animator _animator;
        private Coroutine _hideCoroutine;
        
        public List<FusionMenuScreenPlugin> Plugins => _plugins;
        public bool IsModal => _isModal;
        public bool IsShowing { get; private set; }
        
        // 전체 메뉴 설정값
        public IFusionMenuConfig Config { get; set; }
        
        // 현재 연결 상태를 나타내는 객체
        public IFusionMenuConnection Connection { get; set; }
        
        // 연결 시 사용할 유저/세션 정보
        public IFusionMenuConnectArgs ConnectionArgs { get; set; }
        
        // UI 흐름을 제어하는 FusionMenuUIController
        public IFusionMenuUIController Controller { get; set; }
        
        // 시작 시, 자신에게 붙은 Animator 컴포넌트를 찾아 _animator에 캐싱
        public virtual void Start()
        {
            TryGetComponent(out _animator);
        }

        public virtual void Awake()
        {
            
        }

        // 메뉴 시스템이 Awake 되고 모든 스크린이 준비된 뒤에 초기화되는 메서드
        public virtual void Init()
        {
            
        }
        // 화면을 숨기는 메서드
        // 애니메이터가 있으면 코루틴으로 애니메이션 재생 -> 완료 후 SetActive(false)
        // 플러그인에도 Hide(this) 호출
        public virtual void Hide()
        {
            if (_animator)
            {
                if (_hideCoroutine != null)
                    StopCoroutine(_hideCoroutine);
                _hideCoroutine = StartCoroutine(HideAnimCoroutine());
                return;
            }
            
            IsShowing = false;

            foreach (var p in _plugins)
            {
                p.Hide(this);
            }
            gameObject.SetActive(false);
        }
        // 화면을 표시하는 메서드
        // Hide 코루틴 중이었다면 코루틴 중지하고 Show 애니메이션 재생
        public virtual void Show()
        {
            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
                if (_animator.gameObject.activeInHierarchy && _animator.HasState(0, ShowAnimHash))
                {
                    Debug.Log("애니메이션 재생 성공");
                    _animator.Play(ShowAnimHash, 0, 0);
                }
                else
                {
                    Debug.Log("애니메이션 재생 실패");
                }
            }
            gameObject.SetActive(true);
            IsShowing = true;
            foreach (var p in _plugins)
            {
                p.Show(this);
            }
        }
        // Hide 애니메이션 재생
        // 끝날 때까지 대기
        private IEnumerator HideAnimCoroutine()
        {
#if UNITY_IOS || UNITY_ANDROID
            var changedFramerate = false;
            if (Config.AdaptFramerateForMobilePlatform)
            {
                if (Application.targetFrameRate < 60) {
                    Application.targetFrameRate = 60;
                    changedFramerate = true;
                }
            }
#endif
            _animator.Play(HideAnimHash);
            yield return null;
            // 애니메이터는 여러 "레이어"를 가질 수 있는데, 레이어 0은 기본 레이어, normalizedTime은 레이어 인덱스를 비율로 바꿔줌
            //레이어 0에서 100% 재생될 때까지 계속 기다림
            while (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1)
                yield return null;
#if UNITY_IOS || UNITY_ANDROID
            if (changedFramerate)
            {
                new FusionMenuGraphicsSettings().Apply();
            }
#endif
            gameObject.SetActive(false);
        }
    }
}

#endregion