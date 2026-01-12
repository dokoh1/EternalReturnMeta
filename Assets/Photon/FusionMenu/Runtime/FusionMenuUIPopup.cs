namespace Fusion.Menu {
  using System.Threading.Tasks;
#if FUSION_ENABLE_TEXTMESHPRO
  using Text = TMPro.TMP_Text;
  using InputField = TMPro.TMP_InputField;
#else 
  using Text = UnityEngine.UI.Text;
  using InputField = UnityEngine.UI.InputField;
#endif
  using UnityEngine;
  using UnityEngine.UI;

  // 텍스트 메시지와 확인 버튼이 있는 팝업 UI 화면
  public partial class FusionMenuUIPopup : FusionMenuUIScreen, IFusionMenuPopup {
    // 메시지 본문을 표시하는 텍스트 필드
    [InlineHelp, SerializeField] protected Text _text;
    // 팝업 상단 제목을 표시하는 텍스트 필드
    [InlineHelp, SerializeField] protected Text _header;
    // 확인 버튼
    [InlineHelp, SerializeField] protected Button _button;
    // OpenPopupAsync() 시 사용되는 비동기 완료 트리거 객체
    protected TaskCompletionSource<bool> _taskCompletionSource;

    partial void AwakeUser();
    partial void InitUser();
    partial void ShowUser();
    partial void HideUser();

    public override void Awake() {
      base.Awake();
      AwakeUser();
    }

    public override void Init() {
      base.Init();
      InitUser();
    }
    
    public override void Show() {
      base.Show();
      ShowUser();
    }
    
    public override void Hide() {
      base.Hide();

      HideUser();

      // OpenPopupAsync()로 대기 중이던 Task를 완료 시킴 -> await 가능하게 만듬
      var completionSource = _taskCompletionSource;
      _taskCompletionSource = null;
      completionSource?.TrySetResult(true);
    }

    // 동기 방식
    // UI 만 보여주고 비동기 처리는 하지 않음
    public virtual void OpenPopup(string msg, string header) {
      _header.text = header;
      _text.text = msg;

      Show();
    }

    // 비동기 방식
    // 팝업이 Hide() 될 때 Task가 완료됨
    // 사용자가 확인 버튼을 눌러 닫힐 때 까지 기다림
    public virtual Task OpenPopupAsync(string msg, string header) {
      _taskCompletionSource?.TrySetResult(true);
      _taskCompletionSource = new TaskCompletionSource<bool>();

      OpenPopup(msg, header);

      return _taskCompletionSource.Task;
    }
  }
}
