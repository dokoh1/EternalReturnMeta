namespace Fusion.Menu
{
    using System.Collections;
    using System.Text;
#if FUSION_ENABLE_TEXTMESHPRO
    using Text = TMPro.TMP_Text;
#else
    using Text = UnityEngine.UI.Text;
#endif
    using UnityEngine;
    using UnityEngine.UI;

    // 게임 플레이 중 표시되는 실시간 세션 정보 및 플레이어 관리 UI를 구현한 클래스입니다.
    public partial class FusionMenuUIGameplay : FusionMenuUIScreen
    {
        // 세션 코드 표시용 텍스트
        [SerializeField] protected Text _codeText;
        // 플레이어 이름 목록 표시용 텍스트
        [SerializeField] protected Text _playersText;
        // 현재 플레이어 수 표시
        [SerializeField] protected Text _playersCountText;
        // 최대 플레이어 수 표시
        [SerializeField] protected Text _playersMaxCountText;
        
        [SerializeField] protected Text _headerText;
        // 세션 정보 UI 영역
        [SerializeField] protected GameObject _sessionGameObject;
        // 플레이어 목록 UI 영역
        [SerializeField] protected GameObject _playersGameObject;
        // 세션 코드 복사 버튼
        [SerializeField] protected Button _copySessionButton;
        // 연결 해제 버튼
        [SerializeField] protected Button _disconnectButton;
        public float UpdateUsernameRateInSeconds = 2;
        protected Coroutine _updateUsernameCoroutine;

        partial void AwakeUser();
        partial void InitUser();
        partial void ShowUser();
        partial void HideUser();

        public override void Awake()
        {
            base.Awake();
            AwakeUser();
        }
        // 화면이 열릴 때 자동 호출
        // 세션 코드가 유효하면 텍스트에 표시하고 UI 활성화
        // 플레이어 리스트 갱신 시작 (UpdateUsernamesCoroutine() 실행)
        public override void Show()
        {
            base.Show();
            ShowUser();

            if (Config.CodeGenerator != null && Config.CodeGenerator.IsValid(Connection.SessionName))
            {
                _codeText.text = Connection.SessionName;
                _sessionGameObject.SetActive(true);
            }
            else
            {
                _codeText.text = string.Empty;
                _sessionGameObject.SetActive(false);
            }

            UpdateUsernames();

            if (UpdateUsernameRateInSeconds > 0)
            {
                _updateUsernameCoroutine = StartCoroutine((UpdateUsernamesCoroutine()));
            }
        }

        // 화면이 닫힐 때 호출
        // 플레이어 목록 갱신을 위한 코루틴 중단
        public override void Hide()
        {
            base.Hide();
            HideUser();

            if (_updateUsernameCoroutine != null)
            {
                StopCoroutine(_updateUsernameCoroutine);
                _updateUsernameCoroutine = null;
            }
        }

        // 연결 끊기 버튼이 눌렸을 때 실행됨
        // 연결을 끊고 메인 메뉴 화면 으로 전환
        protected virtual async void OnDisconnectPressed()
        {
            await Connection.DisconnectAsync(ConnectFailReason.UserRequest);
            Controller.Show<FusionMenuUIMain>();
        }
        // 세션 코드 복사 버튼 클릭 시 호출됨
        protected virtual void OnCopySessionPressed()
        {
            GUIUtility.systemCopyBuffer = _codeText.text;
        }
        
        // UpdateUsernameRateInSeconds 주기로 플레이어 목록 갱신
        protected virtual IEnumerator UpdateUsernamesCoroutine()
        {
            while (UpdateUsernameRateInSeconds > 0)
            {
                yield return new WaitForSeconds(UpdateUsernameRateInSeconds);
                UpdateUsernames();
            }
        }
        // 현재 접속 중인 플레이어 이름을 표시
        // 총 접속자 수와 최대 인원 수를 함께 표시
        // 플레이어가 없으면 해당 UI 전체 비활성화
        protected virtual void UpdateUsernames()
        {
            if (Connection.Usernames != null && Connection.Usernames.Count > 0)
            {
                _playersGameObject.SetActive(true);
                var sBuilder = new StringBuilder();
                var playerCount = 0;
                foreach (var username in Connection.Usernames)
                {
                    sBuilder.AppendLine(username);
                    playerCount += string.IsNullOrEmpty(username) ? 0 : 1;
                }
                _playersText.text = sBuilder.ToString();
                _playersCountText.text = $"{playerCount}";
                _playersMaxCountText.text = $"/{Connection.MaxPlayerCount}";
            }
            else
                _playersGameObject.SetActive(false);
        }
    }
}