using UnityEngine;
using UnityEngine.UI;

public class GameLoadingUI : MonoBehaviour
{
    public static GameLoadingUI Instance { get; private set; }

    private Canvas _canvas;
    private GameObject _loadingPanel;
    private Text _loadingText;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateLoadingUI();
        Hide();
    }

    private void CreateLoadingUI()
    {
        // Canvas 생성
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999; // 최상위에 표시

        // Canvas Scaler 추가
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Raycaster 추가
        gameObject.AddComponent<GraphicRaycaster>();

        // 검은 배경 패널
        _loadingPanel = new GameObject("LoadingPanel");
        _loadingPanel.transform.SetParent(transform, false);

        var panelRect = _loadingPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelImage = _loadingPanel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.9f);

        // Loading 텍스트
        var textObj = new GameObject("LoadingText");
        textObj.transform.SetParent(_loadingPanel.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(400, 100);

        _loadingText = textObj.AddComponent<Text>();
        _loadingText.text = "Loading...";
        _loadingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _loadingText.fontSize = 48;
        _loadingText.color = Color.white;
        _loadingText.alignment = TextAnchor.MiddleCenter;
    }

    public void Show(string message = "Loading...")
    {
        _loadingText.text = message;
        _loadingPanel.SetActive(true);
        Debug.Log($"[GameLoadingUI] Show: {message}");
    }

    public void Hide()
    {
        _loadingPanel.SetActive(false);
        Debug.Log("[GameLoadingUI] Hide");
    }

    public void UpdateText(string message)
    {
        _loadingText.text = message;
    }
}
