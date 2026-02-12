using UnityEngine;

namespace Character.Player.ControlSettings
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════════════════
    /// Phase 5: 조작감 설정 테스트 UI
    ///
    /// 목적:
    /// - 포트폴리오용 ON/OFF 비교 테스트
    /// - 실시간 메트릭 표시 (절약된 시간, 버퍼 사용률 등)
    /// - 플레이 중 기능 토글로 즉시 비교 가능
    ///
    /// 사용법:
    /// 1. 게임 씬에 빈 GameObject 생성
    /// 2. 이 컴포넌트 추가
    /// 3. ControlSettingsConfig 할당
    /// 4. F1 키로 UI 토글
    /// ═══════════════════════════════════════════════════════════════════════════
    /// </summary>
    public class ControlSettingsUI : MonoBehaviour
    {
        // ┌─────────────────────────────────────────────────────────────────────┐
        // │  Inspector 설정                                                      │
        // └─────────────────────────────────────────────────────────────────────┘

        [Header("=== 필수 참조 ===")]
        [Tooltip("조작감 설정 ScriptableObject (ON/OFF 토글용)")]
        [SerializeField] private ControlSettingsConfig _config;

        [Header("=== UI 설정 ===")]
        [Tooltip("UI 토글 키")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        [Tooltip("UI 표시 여부")]
        [SerializeField] private bool showUI = false;

        // ┌─────────────────────────────────────────────────────────────────────┐
        // │  내부 변수                                                           │
        // └─────────────────────────────────────────────────────────────────────┘

        // UI 창 위치/크기
        private Rect _windowRect = new Rect(20, 20, 350, 450);

        // 스크롤 위치 (메트릭 목록용)
        private Vector2 _scrollPosition;

        // 스타일 캐싱 (OnGUI 성능 최적화)
        private GUIStyle _headerStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _boxStyle;
        private bool _stylesInitialized = false;

        // ┌─────────────────────────────────────────────────────────────────────┐
        // │  Unity 생명주기                                                      │
        // └─────────────────────────────────────────────────────────────────────┘

        private void Update()
        {
            // F1 키로 UI 토글
            if (Input.GetKeyDown(toggleKey))
            {
                showUI = !showUI;
                Debug.Log($"[ControlSettingsUI] UI {(showUI ? "표시" : "숨김")}");
            }
        }

        private void OnGUI()
        {
            // UI 숨김 상태면 그리지 않음
            if (!showUI) return;

            // 스타일 초기화 (한 번만)
            InitializeStyles();

            // 드래그 가능한 창 그리기
            _windowRect = GUI.Window(0, _windowRect, DrawWindow, "조작감 설정 (F1로 토글)");
        }

        // ┌─────────────────────────────────────────────────────────────────────┐
        // │  스타일 초기화                                                       │
        // │                                                                      │
        // │  왜 필요한가?                                                        │
        // │  - OnGUI는 매 프레임 호출됨                                          │
        // │  - 스타일을 매번 생성하면 GC 부담                                    │
        // │  - 한 번만 생성하고 재사용                                           │
        // └─────────────────────────────────────────────────────────────────────┘
        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            // 헤더 스타일 (섹션 제목용)
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };

            // 값 표시 스타일 (숫자 강조용)
            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };

            // 박스 스타일 (섹션 구분용)
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };

            _stylesInitialized = true;
        }

        // ┌─────────────────────────────────────────────────────────────────────┐
        // │  메인 창 그리기                                                      │
        // └─────────────────────────────────────────────────────────────────────┘
        private void DrawWindow(int windowId)
        {
            // 창 드래그 가능하게
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            // ═══════════════════════════════════════════════════════════════
            // 섹션 1: 기능 토글
            //
            // 의도: 플레이 중 ON/OFF로 즉시 비교 가능
            // 예: Animation Canceling OFF → 콤보 느림
            //     Animation Canceling ON → 콤보 빠름 (체감 비교)
            // ═══════════════════════════════════════════════════════════════
            DrawFeatureToggles();

            GUILayout.Space(10);

            // ═══════════════════════════════════════════════════════════════
            // 섹션 2: 실시간 메트릭
            //
            // 의도: 육안으로 안 보이는 효과를 수치로 증명
            // 예: "총 절약 시간: 1.2초" → 포트폴리오에 스크린샷
            // ═══════════════════════════════════════════════════════════════
            DrawMetrics();

            GUILayout.Space(10);

            // ═══════════════════════════════════════════════════════════════
            // 섹션 3: 유틸리티 버튼
            // ═══════════════════════════════════════════════════════════════
            DrawUtilityButtons();

            GUILayout.EndScrollView();
        }

        // ┌─────────────────────────────────────────────────────────────────────┐
        // │  섹션 1: 기능 토글                                                   │
        // │                                                                      │
        // │  각 기능을 체크박스로 ON/OFF                                         │
        // │  변경 즉시 적용됨 (ScriptableObject 직접 수정)                       │
        // └─────────────────────────────────────────────────────────────────────┘
        private void DrawFeatureToggles()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("기능 토글", _headerStyle);
            GUILayout.Space(5);

            if (_config != null)
            {
                // ───────────────────────────────────────────────────────────
                // Input Buffering 토글
                //
                // ON: 스킬 시전 중 입력 저장 → 시전 끝나면 자동 실행
                // OFF: 스킬 시전 중 입력 무시 (기존 동작)
                // ───────────────────────────────────────────────────────────
                _config.EnableInputBuffering = GUILayout.Toggle(
                    _config.EnableInputBuffering,
                    " Input Buffering (스킬 중 입력 저장)"
                );

                // ───────────────────────────────────────────────────────────
                // Animation Canceling 토글
                //
                // ON: 데미지 적용 후 후딜레이 스킵 가능
                // OFF: 전체 애니메이션 재생 (기존 동작)
                //
                // 왜 필요한가?
                // - 30ms 차이는 육안으로 안 보임
                // - 하지만 10회 콤보 = 300ms 차이 = 체감 가능
                // - 숙련자는 "손맛"으로 느낌
                // ───────────────────────────────────────────────────────────
                _config.EnableAnimationCanceling = GUILayout.Toggle(
                    _config.EnableAnimationCanceling,
                    " Animation Canceling (후딜레이 스킵)"
                );

                // Acceleration Curves는 롤백되어서 비활성화 표시
                GUI.enabled = false;
                GUILayout.Toggle(false, " Acceleration Curves (롤백됨 - MOBA 부적합)");
                GUI.enabled = true;

                GUILayout.Space(5);

                _config.EnableLocalFeedback = GUILayout.Toggle(
                    _config.EnableLocalFeedback, " Local Feedback (스킬 애니메이션 즉시 재생)");

                _config.EnableExtrapolation = GUILayout.Toggle(
                    _config.EnableExtrapolation, " Extrapolation (원격 플레이어 위치 예측)");

                _config.EnableLagCompensation = GUILayout.Toggle(
                    _config.EnableLagCompensation, " Lag Compensation (지연 보상 히트 판정)");

                GUILayout.Space(10);

                // 전체 ON/OFF 버튼
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("모두 켜기"))
                {
                    _config.EnableInputBuffering = true;
                    _config.EnableAnimationCanceling = true;
                    _config.EnableLocalFeedback = true;
                    _config.EnableExtrapolation = true;
                    _config.EnableLagCompensation = true;
                }
                if (GUILayout.Button("모두 끄기"))
                {
                    _config.EnableInputBuffering = false;
                    _config.EnableAnimationCanceling = false;
                    _config.EnableLocalFeedback = false;
                    _config.EnableExtrapolation = false;
                    _config.EnableLagCompensation = false;
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                // Config 미할당 경고
                GUILayout.Label("⚠ ControlSettingsConfig가 할당되지 않음!");
                GUILayout.Label("Inspector에서 Config를 할당하세요.");
            }

            GUILayout.EndVertical();
        }

        // ┌─────────────────────────────────────────────────────────────────────┐
        // │  섹션 2: 실시간 메트릭                                               │
        // │                                                                      │
        // │  ControlMetricsCollector에서 데이터 가져와서 표시                    │
        // │                                                                      │
        // │  왜 필요한가?                                                        │
        // │  - 30ms 차이는 눈으로 안 보임                                        │
        // │  - "총 절약 시간: 2.4초" → 수치로 증명                              │
        // │  - 포트폴리오에 스크린샷 첨부 가능                                   │
        // └─────────────────────────────────────────────────────────────────────┘
        private void DrawMetrics()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("실시간 메트릭", _headerStyle);
            GUILayout.Space(5);

            var collector = ControlMetricsCollector.Instance;

            if (collector != null)
            {
                // ───────────────────────────────────────────────────────────
                // 입력 통계
                // ───────────────────────────────────────────────────────────
                DrawMetricRow("총 입력 횟수", $"{collector.TotalInputs}회");
                DrawMetricRow("버퍼 사용", $"{collector.BufferedInputs}회 ({GetBufferRatio(collector):F1}%)");
                DrawMetricRow("드롭된 입력", $"{collector.DroppedInputs}회");

                GUILayout.Space(10);

                // ───────────────────────────────────────────────────────────
                // 캔슬 통계 (Animation Canceling 효과)
                //
                // 이게 핵심!
                // "총 캔슬 횟수: 15회, 절약 시간: 0.45초"
                // → Animation Canceling의 가치를 수치로 증명
                // ───────────────────────────────────────────────────────────
                DrawMetricRow("총 캔슬 횟수", $"{collector.TotalCancels}회");
                DrawMetricRow("절약된 시간", $"{collector.TotalTimeSaved:F0}ms ({collector.TotalTimeSaved / 1000f:F2}초)");

                GUILayout.Space(10);

                // ───────────────────────────────────────────────────────────
                // 세션 정보
                // ───────────────────────────────────────────────────────────
                DrawMetricRow("세션 시간", $"{collector.SessionDuration:F1}초");

                // DPS 향상률 계산 (추정)
                // 절약 시간 / 세션 시간 = 효율 향상률
                if (collector.SessionDuration > 0 && collector.TotalTimeSaved > 0)
                {
                    float efficiency = (collector.TotalTimeSaved / 1000f) / collector.SessionDuration * 100f;
                    DrawMetricRow("효율 향상", $"+{efficiency:F1}%");
                }
            }
            else
            {
                GUILayout.Label("⚠ ControlMetricsCollector가 씬에 없음!");
                GUILayout.Label("씬에 ControlMetricsCollector를 추가하세요.");
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 메트릭 한 줄 그리기 (라벨: 값 형식)
        /// </summary>
        private void DrawMetricRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":");
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, _valueStyle);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 버퍼 사용률 계산
        /// </summary>
        private float GetBufferRatio(ControlMetricsCollector collector)
        {
            if (collector.TotalInputs == 0) return 0f;
            return (float)collector.BufferedInputs / collector.TotalInputs * 100f;
        }

        // ┌─────────────────────────────────────────────────────────────────────┐
        // │  섹션 3: 유틸리티 버튼                                               │
        // └─────────────────────────────────────────────────────────────────────┘
        private void DrawUtilityButtons()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("유틸리티", _headerStyle);
            GUILayout.Space(5);

            // ───────────────────────────────────────────────────────────────
            // JSON 내보내기 버튼
            //
            // 포트폴리오용!
            // 1. 모든 기능 OFF로 플레이 → JSON 저장
            // 2. 모든 기능 ON으로 플레이 → JSON 저장
            // 3. 두 JSON 비교 → "기능 적용 시 효율 30% 향상" 증명
            // ───────────────────────────────────────────────────────────────
            if (GUILayout.Button("메트릭 JSON 저장 (F5)"))
            {
                ControlMetricsCollector.Instance?.SaveToFile();
                Debug.Log("[ControlSettingsUI] 메트릭 저장됨!");
            }

            // 세션 초기화
            if (GUILayout.Button("메트릭 초기화 (F6)"))
            {
                ControlMetricsCollector.Instance?.ResetSession();
                Debug.Log("[ControlSettingsUI] 세션 초기화됨!");
            }

            GUILayout.Space(10);

            // 저장 경로 표시
            GUILayout.Label($"저장 경로:\n{Application.persistentDataPath}",
                new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 10 });

            GUILayout.EndVertical();
        }
    }
}
