using TMPro;

namespace Fusion.Menu
{
    using System.Threading.Tasks;
#if FUSION_ENABLE_TEXTMESHPRO
    using Text = TMPro.TMP_Text;
    using InputField = TMPro.TMP_InputField;
#else
    using Text = UnityEngine.UI.Text;
    using InputField = UnityEngine.UI.InputField;
#endif
    using UnityEngine;

    // 로비 / 메인 메뉴 UI를 구성하는 화면 : 닉네임 설정, 씬 썸네일 표시, 버튼 인터랙션
    public partial class FusionMenuUIMain : FusionMenuUIScreen
    {
        [SerializeField] protected TMP_Text _usernameLabel;
        // [SerializeField] protected UnityEngine.UI.Image _sceneThumbnail;
        [SerializeField] protected GameObject _usernameView;
        [SerializeField] protected TMP_InputField _usernameInput;
        [SerializeField] protected UnityEngine.UI.Button _usernameConfirmButton;
        [SerializeField] protected UnityEngine.UI.Button _usernameButton;
        // [SerializeField] protected UnityEngine.UI.Button _characterButton;
        // [SerializeField] protected UnityEngine.UI.Button _partyButton;
        [SerializeField] protected UnityEngine.UI.Button _playButton;
        [SerializeField] protected UnityEngine.UI.Button _quitButton;
        // [SerializeField] protected UnityEngine.UI.Button _sceneButton;
        [SerializeField] protected UnityEngine.UI.Button _settingsButton;
        
        partial void AwakeUser();
        partial void InitUser();
        partial void ShowUser();
        partial void HideUser();

        public override void Awake()    
        {
            base.Awake();
            // 그래픽 설정 반영
            new FusionMenuGraphicsSettings().Apply();
            
#if UNITY_STANDALONE
            _quitButton.gameObject.SetActive(true);
#else
            _quitButton.gameObject.SetActive(false);
#endif
            AwakeUser();
        }

        // UI 초기화 시 1회 호출
        public override void Init()
        {
            base.Init();
            ConnectionArgs.SetDefaults(Config);
            InitUser();
        }

        // 메인 메뉴 화면이 열릴 떄 실행
        public override async void Show()
        {
            base.Show();
            // 네임 설정 View false
            _usernameView.SetActive(false);
            _usernameLabel.text = ConnectionArgs.Username;
        
            // 현재 선택된 씬의 정보의 Name이 비어있다면
            if (string.IsNullOrEmpty(ConnectionArgs.Scene.Name))
            {
                // 버튼들 비활성화
                _playButton.interactable = false;
                
                Debug.LogWarning("No valid scene to start found. Configure the menu config.");
            }
            ShowUser();
#if UNITY_SERVER            
            ConnectionArgs.Session = null;
            ConnectionArgs.Creating = false;
            ConnectionArgs.Region = ConnectionArgs.PreferredRegion;
            ConnectionArgs.IsServer = true;
            
            var result = await Connection.ConnectAsync(ConnectionArgs);
            await HandleConnectionResult(result, this.Controller);
#endif
        }

        public override void Hide()
        {
            base.Hide();
            HideUser();
        }

        protected virtual void OnFinishUsernameEdit()
        {
            OnFinishUsernameEdit(_usernameInput.text);
        }
        // 닉네임 편집 완료 버튼에 들어가는 Action 편집 종료 시 닉네임 저장 및 화면 반영
        protected virtual void OnFinishUsernameEdit(string username)
        {
            _usernameView.SetActive(false);

            if (string.IsNullOrEmpty(username) == false)
            {
                _usernameLabel.text = username;
                ConnectionArgs.Username = username;
            }
        }
        // 닉네임 편집창 열기
        protected virtual void OnUsernameButtonPressed()
        {
            _usernameView.SetActive(true);
            _usernameInput.text = _usernameLabel.text;
        }
        // 로딩 화면 보여주고, Photon 연결 시도
        // 성공 시 게임 플레이 화면으로 전환, 실패 시 팝업 후 메인으로 복귀
        protected virtual async void OnPlayButtonPressed()
        { 
            ConnectionArgs.Session = null;
            ConnectionArgs.Creating = false;
            ConnectionArgs.Region = ConnectionArgs.PreferredRegion;
            ConnectionArgs.IsServer = false;
            
            var result = await Connection.ConnectAsync(ConnectionArgs);
            await HandleConnectionResult(result, this.Controller);
        }

        // 연결 성공 시 GamePlay 화면으로 이동
        // 실패 시 오류 팝업 표시
        public static async Task HandleConnectionResult(ConnectResult result, IFusionMenuUIController controller)
        {
            if (result.CustomResultHandling == false)
            {
                if (result.Success) {
                    controller.Show<MatchingModal>();
                } 
                else if (result.FailReason != ConnectFailReason.ApplicationQuit)
                {
                    var popup = controller.PopupAsync(result.DebugMessage, "Connection Failed");
                    if (result.WaitForCleanup != null)
                        await Task.WhenAll(result.WaitForCleanup, popup);
                    else
                        await popup;
                    Debug.Log("실패?");
                    controller.Show<FusionMenuUIMain>();
                }
            }
        }
        // 파티 메뉴 UI로 전환
        protected virtual void OnPartyButtonPressed()
        {
            Controller.Show<FusionMenuUIParty>();
        }
        // 씬 선택 UI로 전환
        protected virtual void OnSceneButtonPressed()
        {
            Controller.Show<FusionMenuUIScenes>();
        }
        // 세팅 설정 UI로 전환
        protected virtual void OnSettingsButtonPressed()
        {
            Controller.Show<FusionMenuUISettings>();
        }
        // 캐릭터 선택 UI로 전환
        protected virtual void OnCharacterButtonPressed()
        {
            
        }
        // 종료 버튼 클릭 시 앱 종료
        protected virtual void OnQuitButtonPressed()
        {
            Application.Quit();
        }
    }
}