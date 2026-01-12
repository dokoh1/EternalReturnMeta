namespace Fusion.Menu
{
    using System;
    using Fusion.Menu;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UIElements;
    public class MatchingModal : FusionMenuUIScreen
    {
        [SerializeField] 
        private TMP_Text _headerText;
        [SerializeField]
        private TMP_Text _timerText;
        [SerializeField]
        private TMP_Text _playerCountText;
        [SerializeField] 
        private TMP_Text _sessionText;
        [SerializeField]
        private Button _disconnectButton;

        private float elapsedTime = 0f;
        private bool isTiming = false;

        public void UpdatePlayerCount(int ready, int max)
        {
            _headerText.text = "코발트 대전";
            _playerCountText.text = $"매칭 대기 인원 {ready}/{max}";
        }
        public override void Awake()
        {
            base.Awake();
        }

        public override void Init()
        {
            base.Init();
        }

        public override void Show()
        {
            base.Show();
            _sessionText.text = Connection.SessionName;
            elapsedTime = 0f;
            isTiming = true;
            UpdateTimerText(0f);
        }

        public override void Hide()
        {
            base.Hide();
        }

        private void Update()
        {
            if (isTiming)
            {
                elapsedTime += Time.deltaTime;
                UpdateTimerText(elapsedTime);
            }

            
        }
        protected virtual async void OnDisconnectionPressed()
        {
            await Connection.DisconnectAsync(ConnectFailReason.UserRequest);
        }

        private void UpdateTimerText(float time)
        {
            int minutes = (int)(time / 60f);
            int seconds = (int)(time % 60f);
            _timerText.text = $"매칭 중 {minutes:00}:{seconds:00}";
        }
    }
    
}
