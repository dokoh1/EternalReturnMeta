namespace Fusion.Menu {
  using System.Linq;
  using System.Threading.Tasks;
#if FUSION_ENABLE_TEXTMESHPRO
  using Dropdown = TMPro.TMP_Dropdown;
#else 
  using Dropdown = UnityEngine.UI.Dropdown;
#endif
  using UnityEngine;
  
  // 사용자가 드롭다운에서 맵(씬)을 선택하고, 그에 따른 프리뷰 이미지를 보여준 뒤, 선택 결과를 ConnectionArgs.Scene에 저장하는 UI 화면
  public partial class FusionMenuUIScenes : FusionMenuUIScreen {
    // 드롭다운 UI로 씬 리스트 선택
    [InlineHelp, SerializeField] protected Dropdown _availableScenes;
    // 선택한 씬의 미리보기 이미지
    [InlineHelp, SerializeField] protected UnityEngine.UI.Image _preview;
    // 씬에 프리뷰가 없을 때 표시할 기본 이미지
    [InlineHelp, SerializeField] protected Sprite _defaultSprite;
    // 뒤로 가기 버튼
    [InlineHelp, SerializeField] protected UnityEngine.UI.Button _backButton;
    // 씬 선택 후 몇 ms 뒤 자동으로 메인 메뉴로 복귀
    public int WaitAfterSelectionAndReturnToMainMenuInMs = 0;

    partial void AwakeUser();
    partial void InitUser();
    partial void ShowUser();
    partial void HideUser();
    partial void SaveChangesUser();

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

      _availableScenes.ClearOptions();
      // 설정 파일의 씬 목록을 드롭다운에 보여줌
      _availableScenes.AddOptions(Config.AvailableScenes.Select(m => m.Name).ToList());

      // 현재 연결 설정과 동일한 씬이  리스트에 있는지 찾음
      var sceneIndex = Config.AvailableScenes.FindIndex(m => m.ScenePath == ConnectionArgs.Scene.ScenePath);
      
      if (sceneIndex >= 0) {
        // 씬의 찾았으면 드롭 다운에서 그 씬을 선택 상태로 표시
        _availableScenes.SetValueWithoutNotify(sceneIndex);
        // 해당 씬의 프리뷰 이미지도 갱신
        RefreshPreviewSprite();
        
      }
      else if (Config.AvailableScenes.Count > 0) 
      {
        // 씬을 못찾았지만 씬 리스트가 비어있지 않다면, 첫 번째 씬을 선택
        _availableScenes.SetValueWithoutNotify(0);
        ConnectionArgs.Scene = Config.AvailableScenes[0];
      }

      ShowUser();
    }

    public override void Hide() {
      base.Hide();
      HideUser();
    }
    
    public virtual void OnBackButtonPressed() {
      Controller.Show<FusionMenuUIMain>();
    }
    // 씬을 선택했을 때 호출되는 메서드
    protected virtual async void OnSaveChanges() {
      // 선택한 씬의 프리뷰 이미지 갱신
      RefreshPreviewSprite();

      // 드롭다운에서 현재 선택된 항목을 실제 연결 설정에 저장
      ConnectionArgs.Scene = Config.AvailableScenes[_availableScenes.value];

      SaveChangesUser();

#if !UNITY_WEBGL
      if (WaitAfterSelectionAndReturnToMainMenuInMs > 0) {
        await Task.Delay(WaitAfterSelectionAndReturnToMainMenuInMs);
        Controller.Show<FusionMenuUIMain>();
      }
#endif
    }
    
    // 선택한 씬의 이미지 미리보기를 갱신
    protected void RefreshPreviewSprite() {
      // 선택한 씬에 프리뷰 이미지가 있으면 표시, 없으면 _defaultSprite 사용
      if (Config.AvailableScenes[_availableScenes.value].Preview == null) {
        _preview.sprite = _defaultSprite;
      } else {
        _preview.sprite = Config.AvailableScenes[_availableScenes.value].Preview;
      }
      // FusionMenuImageFitter가 붙어 있을 경우, 이미지 크기를 다시 맞춤
      _preview.gameObject.SendMessage("OnResolutionChanged");
    }
  }
}
