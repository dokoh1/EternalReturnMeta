namespace Fusion.Menu {
  using System;
  using System.Collections.Generic;
#if FUSION_ENABLE_TEXTMESHPRO
  using Dropdown = TMPro.TMP_Dropdown;
  using InputField = TMPro.TMP_InputField;
  using Text = TMPro.TMP_Text;
#else
  using Dropdown = UnityEngine.UI.Dropdown;
  using InputField = UnityEngine.UI.InputField;
  using Text = UnityEngine.UI.Text;
#endif
  using UnityEngine;
  using UnityEngine.UI;
  // 사용자로부터 다양한 설정 값을 받아서 PlayerPrefs와 ConnectionArgs, 그리고 GraphicSettings 등에 적용하는 UI 화면을 제공한다.
  public partial class FusionMenuUISettings : FusionMenuUIScreen {
    
    // 앱 버전을 선택할 드롭다운
    [InlineHelp, SerializeField] protected Dropdown _uiAppVersion;
    
    // 서버 지역 드롭다운
    [InlineHelp, SerializeField] protected GameObject _goAppVersion;
    
    // 프레임레이트 설정 드롭다운
    [InlineHelp, SerializeField] protected Toggle _uiFullscreen;
    // IOS, Android 환경에서 필요 없는 옵션 ON/OFF
    [InlineHelp, SerializeField] protected GameObject _goFullscreen;
    [InlineHelp, SerializeField] protected GameObject _goResolution;
    [InlineHelp, SerializeField] protected GameObject _goRegion;
    
    // 프레임레이트 설정 드롭다운
    [InlineHelp, SerializeField] protected Dropdown _uiFramerate;
    // Unity 퀄리티 레벨 드롭다운
    [InlineHelp, SerializeField] protected Dropdown _uiGraphicsQuality;
    // 최대 플레이어 수 입력 필드
    [InlineHelp, SerializeField] protected InputField _uiMaxPlayers;
    // 서버 지역 드롭다운
    [InlineHelp, SerializeField] protected Dropdown _uiRegion;
    // 화면 해상도 드롭다운
    [InlineHelp, SerializeField] protected Dropdown _uiResolution;
    // VSync 여부
    [InlineHelp, SerializeField] protected Toggle _uiVSyncCount;
    // 뒤로 가기 버튼
    [InlineHelp, SerializeField] protected Button _backButton;
    // SDK 또는 빌드 정보 표시용 텍스트
    [InlineHelp, SerializeField] protected Text _sdkLabel;
    
    // 드롭다운과 실제 값 리스트를 매핑함
    protected FusionMenuSettingsEntry<string> _entryRegion;
    protected FusionMenuSettingsEntry<string> _entryAppVersion;
    protected FusionMenuSettingsEntry<int> _entryFramerate;
    protected FusionMenuSettingsEntry<int> _entryResolution;
    protected FusionMenuSettingsEntry<int> _entryGraphicsQuality;
    
    // 유저 설정을 읽고 저장하는 클래스
    protected FusionMenuGraphicsSettings _graphicsSettings;
    // 사용 가능한 앱 버전 문자열 리스트
    protected List<string> _appVersions;

    partial void AwakeUser();
    partial void InitUser();
    partial void ShowUser();
    partial void HideUser();
    partial void SaveChangesUser();
    
    public override void Awake() {
      base.Awake();

      _appVersions = new List<string>();
      // 현재 빌드의 고유 ID, 접속 가능한 호환 버전 목록
      // 이 둘을 합쳐서 appVersion 리스트에 저장
      if (Config.MachineId != null) {
        _appVersions.Add(Config.MachineId);
      }
      _appVersions.AddRange(Config.AvailableAppVersions);
      // FusinMenuSettingsEntry<T> 는 드롭다운을 값 리스트와 연결해주는 클래스
      // 값이 바뀌면 자동으로 SaveChages를 호출해줌
      _entryRegion = new FusionMenuSettingsEntry<string>(_uiRegion, SaveChanges);
      _entryAppVersion = new FusionMenuSettingsEntry<string>(_uiAppVersion, SaveChanges);
      _entryFramerate = new FusionMenuSettingsEntry<int>(_uiFramerate, SaveChanges);
      _entryResolution = new FusionMenuSettingsEntry<int>(_uiResolution, SaveChanges);
      _entryGraphicsQuality = new FusionMenuSettingsEntry<int>(_uiGraphicsQuality, SaveChanges);

      // 최대 플레이어 수 입력 필드 이벤트 등록
      // 사용자가 인원수를 입력 한 후 포커스를 벗어나면 실행
      // 문자열 -> int 파싱, 숫자가 유효하지 않거나 범위를 벗어나면 자동으로 클램프, 최종 숫자를 텍스트 필드에 반영
      // 이후 SaveChanges를 호출하여 설정 저장
      _uiMaxPlayers.onEndEdit.AddListener(s => {
        if (Int32.TryParse(s, out var maxPlayers) == false || maxPlayers <= 0 || maxPlayers > Config.MaxPlayerCount) {
          maxPlayers = Math.Clamp(maxPlayers, 1, Config.MaxPlayerCount);
          _uiMaxPlayers.text = maxPlayers.ToString();
        }
        SaveChanges();
      });

      //전체화면 / VSync 토글 이벤트 연결
      _uiVSyncCount.onValueChanged.AddListener(_ => SaveChanges());
      _uiFullscreen.onValueChanged.AddListener(_ => SaveChanges());

      _graphicsSettings = new FusionMenuGraphicsSettings();

      _goAppVersion.SetActive(Config.AvailableAppVersions.Count > 0);
      _goRegion.SetActive(Config.AvailableRegions.Count > 0);

#if UNITY_IOS || UNITY_ANDROID
      _goResolution.SetActive(false);
      _goFullscreenn.SetActive(false);
#endif

      AwakeUser();
    }
    
    public override void Init() {
      base.Init();
      InitUser();
    }
    // 드롭다운과 입력 필드를 실제 값으로 채움
    public override void Show() {
      base.Show();
      // 설정된 지역 목록, 현재 선택된 지역, UI에 표시될 이름 "kr" -> "kr", 빈 값 -> "Best"
      _entryRegion.SetOptions(Config.AvailableRegions, ConnectionArgs.PreferredRegion, s => string.IsNullOrEmpty(s) ? "Best" : s);
      // 실행 가능한 앱 버전 목록, 현재 선택된 앱 버전, 현재 빌드 버전 표시
      _entryAppVersion.SetOptions(_appVersions, ConnectionArgs.AppVersion, s => s.Equals(Config.MachineId) ? $"Build ({Config.MachineId})" : s);
      // 프레임 목록, 현재 설정된 앱 버전, -1 이면 Platform Default로 표시, 숫자 그대로 60 등으로 표시
      _entryFramerate.SetOptions(_graphicsSettings.CreateFramerateOptions, _graphicsSettings.Framerate, s => (s == -1 ? "Platform Default" : s.ToString()));
      // 사용 가능한 해상도 인덱스를 리스트로 가져와 드롭다운에 표시
      _entryResolution.SetOptions(_graphicsSettings.CreateResolutionOptions, _graphicsSettings.Resolution, s =>
#if UNITY_2022_2_OR_NEWER
        $"{Screen.resolutions[s].width} x {Screen.resolutions[s].height} @ {Mathf.RoundToInt((float)Screen.resolutions[s].refreshRateRatio.value)}");
#else
        Screen.resolutions[s].ToString());
#endif
      // Low, Medium, High 등의 퀄리티 레벨 표시
      _entryGraphicsQuality.SetOptions(_graphicsSettings.CreateGraphicsQualityOptions, _graphicsSettings.QualityLevel, s => QualitySettings.names[s]);
     // 최대 인원 수 입력 필드
      _uiMaxPlayers.SetTextWithoutNotify(Math.Clamp(ConnectionArgs.MaxPlayerCount, 1, Config.MaxPlayerCount).ToString());
      // 전체 화면 및 VSync 필드
      _uiFullscreen.isOn = _graphicsSettings.Fullscreen;
      _uiVSyncCount.isOn = _graphicsSettings.VSync;

      ShowUser();
    }
    
    public override void Hide() {
      base.Hide();
      HideUser();
    }
    
    // 현재 UI 상태 값을 실제 ConnectionArgs 및 GraphicsSettings에 반영
    // Player
    protected virtual void SaveChanges() {
      if (IsShowing == false) {
        return;
      }

      if (Int32.TryParse(_uiMaxPlayers.text, out var maxPlayers)) {
        ConnectionArgs.MaxPlayerCount = Math.Clamp(maxPlayers, 1, Config.MaxPlayerCount);
        _uiMaxPlayers.SetTextWithoutNotify(ConnectionArgs.MaxPlayerCount.ToString());
      }

      ConnectionArgs.PreferredRegion = _entryRegion.Value;
      ConnectionArgs.AppVersion = _entryAppVersion.Value;

      _graphicsSettings.Fullscreen = _uiFullscreen.isOn;
      _graphicsSettings.Framerate = _entryFramerate.Value;
      _graphicsSettings.Resolution = _entryResolution.Value;
      _graphicsSettings.QualityLevel = _entryGraphicsQuality.Value;
      _graphicsSettings.VSync = _uiVSyncCount.isOn;
      _graphicsSettings.Apply();

      SaveChangesUser();
    }
    
    public virtual void OnBackButtonPressed() {
      Controller.Show<FusionMenuUIMain>();
    }
  }
}
