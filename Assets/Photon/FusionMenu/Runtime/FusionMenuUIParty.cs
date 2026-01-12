namespace Fusion.Menu
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
#if FUSION_ENABLE_TEXTMESHPRO
    using InputField = TMPro.TMP_InputField;
#else
    using InputField = UnityEngine.UI.InputField;
#endif
    using UnityEngine;
    using UnityEngine.UI;
    //세션 코드로 파티에 참여하거나, 새 파티를 생성하고 서버에 접속하는 화면 UI를 담당
    public partial class FusionMenuUIParty : FusionMenuUIScreen
    {
        [SerializeField] protected InputField _sessionCodeField;
        [SerializeField] protected Button _createButton;
        [SerializeField] protected Button _joinButton;
        [SerializeField] protected Button _backButton;

        protected Task<List<FusionMenuOnlineRegion>> _regionRequest;

        partial void AwakeUser();
        partial void InitUser();
        partial void ShowUser();
        partial void HideUser();

        public override void Awake()
        {
            base.Awake();
            AwakeUser();
        }

        public override void Init()
        {
            base.Init();
            InitUser();
        }

        public override void Show()
        {
            base.Show();
            // 코드 생성기 없으면 에러
            if (Config.CodeGenerator == null)
            {
                Debug.LogError("Add a CodeGenerator to FusionMenuConfig");
            }
            // 세션 코드 입력창 초기화
            _sessionCodeField.SetTextWithoutNotify("".PadLeft(Config.CodeGenerator.Length, '-'));
            _sessionCodeField.characterLimit = Config.CodeGenerator.Length;
            // 지역 정보 비동기 요청
            if (_regionRequest == null || _regionRequest.IsFaulted)
            {
                _regionRequest = Connection.RequestAvailableOnlineRegionsAsync(ConnectionArgs);
            }
            ShowUser();
        }

        public override void Hide()
        {
            base.Hide();
            HideUser();
        }
        // 파티 생성 버튼이 눌리면 실행
        protected virtual async void OnCreateButtonPressed()
        {
            await ConnectAsync(true);
        }
        // 뒤로가기 버튼 -> 메인메뉴로 복귀
        public virtual void OnBackBurttonPressed()
        {
            Controller.Show<FusionMenuUIMain>();
        }
        // creating == true 파티 생성, creating == false 파티 참가
        protected virtual async Task ConnectAsync(bool creating)
        {
            // 세션 코드 유효성 검사
            // 참가 모드인 경우 false
            // 사용자가 입력한 세션 코드가 CodeGenerator 기준에 부합하는지 확인
            // 유효하지 않으면 팝업으로 오류 메시지 표시 후 함수 종료
            var inputRegionCode = _sessionCodeField.text.ToUpper();
            if (creating == false && Config.CodeGenerator.IsValid(inputRegionCode) == false)
            {
                await Controller.PopupAsync(
                    $"The session code '{inputRegionCode}' is not a valid session code. Please enter {Config.CodeGenerator.Length} characters or digits.",
                    "Invalid Session Code");
                return;
            }
            // _regionRequest는 가능한 Photon 서버 지역 목록을 요청하는 Task
            if (_regionRequest.IsCompleted == false)
            {
                // 아직 완료되지 않았다면 로딩 화면을 보여주고 Fetching Regions 메시지 표시
                // Controller.Show<FusionMenuUILoading>();
                // Controller.Get<FusionMenuUILoading>().SetStatusText("Fetching Regions");

                try
                {
                    await _regionRequest;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    // Error is handled in next section
                }
            }
            // 네트워크 장애 또는 응답 실패 시 팝업을 띄우고 메인 메뉴로 돌아감
            if (_regionRequest.IsCompletedSuccessfully == false && _regionRequest.Result.Count == 0)
            {
                await Controller.PopupAsync($"Failed to request regions.", "Connection Failed");
                Controller.Show<FusionMenuUIMain>();
                return;
            }
            // Ping이 가장 낮은 지역을 자동 선택하거나, PreferredRegion에 기반해 선택
            // 파티 생성
            if (creating)
            {
                var regionIndex = -1;
                if (string.IsNullOrEmpty(ConnectionArgs.PreferredRegion))
                {
                    // Select a best region now
                    regionIndex = FindBestAvailableOnlineRegionIndex(_regionRequest.Result);
                }
                else
                {
                    regionIndex = _regionRequest.Result.FindIndex(r => r.Code == ConnectionArgs.PreferredRegion);
                }

                if (regionIndex == -1)
                {
                    await Controller.PopupAsync($"Selected region is not available.", "Connection Failed");
                    Controller.Show<FusionMenuUIMain>();
                    return;
                }
                // 선택된 지역 인덱스와 랜덤 문자열을 조합해 세션 코드 생성
                ConnectionArgs.Session = Config.CodeGenerator.EncodeRegion(Config.CodeGenerator.Create(), regionIndex);
                ConnectionArgs.Region = _regionRequest.Result[regionIndex].Code;
            }
            // 파티 참가
            else
            {
                // 지역 인덱스가 비정상 -> 세션 코드 디코딩 실패 -> 팝업
                var regionIndex = Config.CodeGenerator.DecodeRegion(inputRegionCode);
                if (regionIndex < 0 || regionIndex > Config.AvailableRegions.Count)
                {
                    await Controller.PopupAsync(
                        $"The session code '{inputRegionCode}' is not a valid session code (cannot decode the region).",
                        "Invalid Session Code");
                    return;
                }

                ConnectionArgs.Session = _sessionCodeField.text.ToUpper();
                ConnectionArgs.Region = Config.AvailableRegions[regionIndex];
            }
            // 실제 서버 연결
            ConnectionArgs.Creating = creating;
            Controller.Show<FusionMenuUILoading>();

            var result = await Connection.ConnectAsync(ConnectionArgs);
            // await FusionMenuUIMain.HandleConnectionResult(result, this.Controller);
        }
        
        // Ping이 가장 낮은 지역의 인덱스를 반환
        // 빠른 응답을 기대할 수 있는 지역 자동 선택
        protected static int FindBestAvailableOnlineRegionIndex(List<FusionMenuOnlineRegion> regions) {
            var lowestPing = int.MaxValue;
            var index = -1;
            for (int i = 0; regions != null && i < regions.Count; i++) {
                if (regions[i].Ping < lowestPing) {
                    lowestPing = regions[i].Ping;
                    index = i;
                }
            }

            return index;
        }


    }
    
}