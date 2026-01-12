using System.Collections;
using TMPro;

namespace Fusion.Menu
{
#if FUSION_ENABLE_TEXTMESHPRO
    using Text = TMPro.TMP_Text;
#else
    using Text = UnityEngine.UI.Text;
#endif
    using UnityEngine;
    using UnityEngine.UI;

    //서버 연결 또는 씬 로딩 중에 보여지는 로딩 화면 UI
    // 텍스트 상태 업데이트와 연결 끊기 버튼 기능을 포함하는 화면 클래스
    public partial class FusionMenuUILoading : FusionMenuUIScreen
    {
        [SerializeField] protected TMP_Text _countText;
        [SerializeField] protected Image countGague;
        private float duration = 5f;
        private Coroutine _countCoroutine;
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
            _countCoroutine = StartCoroutine(CountCoroutine(duration));
            
            var manager = MatchingManager.Instance;
            PlayerNetworkObject myPlayer = null;
            if (manager != null)
            { 
                PlayerRef myObj = MatchingManager.Instance.Runner.LocalPlayer;
                if (MatchingManager.Instance.Runner.TryGetPlayerObject(myObj, out var networkObject))
                {
                    // myPlayer = networkObject.GetComponent<PlayerNetworkObject>();
                    myPlayer = networkObject.GetComponent<PlayerNetworkObject>();
                }
                else
                {
                    Debug.Log("TryGetPlayerObject failed");
                }
            
                if (myPlayer != null) 
                    myPlayer.Rpc_RequestSelectCharacter(CharacterDataEnum.None);
            }
            ShowUser();
        }

        public override void Hide()
        {
            base.Hide();
            HideUser();
        }
        
        private IEnumerator CountCoroutine(float duration)
        {
            float elapsed = 0f;
            countGague.fillAmount = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 게이지 fillAmount 증가
                countGague.fillAmount = t;

                // 남은 시간 표시 (초 단위, 소수점 1자리)
                float remain = Mathf.Max(0, elapsed);
                _countText.text = Mathf.CeilToInt(remain).ToString();

                yield return null;
            }

            // 최종 값 보정
            countGague.fillAmount = 1f;
            _countText.text = "5";
            _countCoroutine = null;
        }
    }
}