using System;
using System.Collections.Generic;
using UnityEngine;

namespace Character.Player.ControlSettings
{
    /// <summary>
    /// 입력 메트릭 데이터
    /// </summary>
    [Serializable]
    public struct InputMetric
    {
        public float Timestamp;
        public string InputType;        // "Q", "W", "E", "R"
        public bool WasBuffered;        // 버퍼에서 실행되었는지
        public float ResponseTime;      // 입력부터 실행까지 시간 (ms)
        public bool WasDropped;         // 입력이 무시되었는지
    }

    /// <summary>
    /// 이동 메트릭 데이터
    /// </summary>
    [Serializable]
    public struct MovementMetric
    {
        public float Timestamp;
        public float CurrentSpeed;      // 현재 속도
        public float TargetSpeed;       // 목표 속도
        public float SpeedRatio;        // 현재/목표 비율 (0~1)
        public bool IsAccelerating;     // 가속 중인지
        public bool IsDecelerating;     // 감속 중인지
    }

    /// <summary>
    /// 캔슬 메트릭 데이터
    /// </summary>
    [Serializable]
    public struct CancelMetric
    {
        public float Timestamp;
        public string SkillName;        // 캔슬된 스킬
        public float CancelPoint;       // 캔슬 시점 (0~1)
        public float TimeSaved;         // 절약된 시간 (ms)
        public string NextSkill;        // 다음 실행된 스킬
    }

    /// <summary>
    /// 세션 통계 데이터
    /// </summary>
    [Serializable]
    public class SessionStatistics
    {
        public int TotalInputs;
        public int BufferedInputs;
        public int DroppedInputs;
        public float AverageResponseTime;
        public int TotalCancels;
        public float TotalTimeSaved;
        public float SessionDuration;

        public float BufferedInputRatio => TotalInputs > 0 ? (float)BufferedInputs / TotalInputs : 0f;
        public float DroppedInputRatio => TotalInputs > 0 ? (float)DroppedInputs / TotalInputs : 0f;
    }

    /// <summary>
    /// 조작감 메트릭 수집기
    /// 포트폴리오용 데이터 수집 및 비교 분석
    ///
    /// 사용법:
    /// 1. 씬에 빈 GameObject 생성
    /// 2. 이 컴포넌트 추가
    /// 3. 게임 플레이 후 F5로 JSON 저장 (Phase 5에서 UI 추가 예정)
    /// </summary>
    public class ControlMetricsCollector : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool enableCollection = true;
        [SerializeField] private int maxMetricsPerType = 1000;

        [Header("=== Current Session Stats (Read Only) ===")]
        [SerializeField] private int totalInputs;
        [SerializeField] private int bufferedInputs;
        [SerializeField] private int droppedInputs;
        [SerializeField] private int totalCancels;
        [SerializeField] private float totalTimeSaved;

        // 메트릭 저장소
        private List<InputMetric> _inputMetrics = new List<InputMetric>();
        private List<MovementMetric> _movementMetrics = new List<MovementMetric>();
        private List<CancelMetric> _cancelMetrics = new List<CancelMetric>();

        private float _sessionStartTime;
        private float _totalResponseTime;

        // 싱글톤 (다른 스크립트에서 접근용)
        public static ControlMetricsCollector Instance { get; private set; }

        public bool EnableCollection
        {
            get => enableCollection;
            set => enableCollection = value;
        }

        private void Awake()
        {
            // 싱글톤 설정
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            ResetSession();
        }

        private void Update()
        {
            // 임시 단축키 (Phase 5에서 UI로 대체 예정)
            if (Input.GetKeyDown(KeyCode.F5))
            {
                SaveToFile();
                Debug.Log("[ControlMetrics] 메트릭 저장 완료!");
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                ResetSession();
                Debug.Log("[ControlMetrics] 세션 초기화!");
            }
        }

        /// <summary>
        /// 세션 초기화
        /// </summary>
        public void ResetSession()
        {
            _inputMetrics.Clear();
            _movementMetrics.Clear();
            _cancelMetrics.Clear();

            totalInputs = 0;
            bufferedInputs = 0;
            droppedInputs = 0;
            totalCancels = 0;
            totalTimeSaved = 0f;
            _totalResponseTime = 0f;

            _sessionStartTime = Time.time;

            Debug.Log("[ControlMetrics] Session started");
        }

        /// <summary>
        /// 입력 메트릭 기록
        /// </summary>
        public void RecordInput(string inputType, bool wasBuffered, float responseTimeMs, bool wasDropped = false)
        {
            if (!enableCollection) return;

            var metric = new InputMetric
            {
                Timestamp = Time.time - _sessionStartTime,
                InputType = inputType,
                WasBuffered = wasBuffered,
                ResponseTime = responseTimeMs,
                WasDropped = wasDropped
            };

            if (_inputMetrics.Count >= maxMetricsPerType)
            {
                _inputMetrics.RemoveAt(0);
            }
            _inputMetrics.Add(metric);

            // 통계 업데이트
            totalInputs++;
            if (wasBuffered) bufferedInputs++;
            if (wasDropped) droppedInputs++;
            _totalResponseTime += responseTimeMs;
        }

        /// <summary>
        /// 이동 메트릭 기록
        /// </summary>
        public void RecordMovement(float currentSpeed, float targetSpeed, bool isAccelerating, bool isDecelerating)
        {
            if (!enableCollection) return;

            var metric = new MovementMetric
            {
                Timestamp = Time.time - _sessionStartTime,
                CurrentSpeed = currentSpeed,
                TargetSpeed = targetSpeed,
                SpeedRatio = targetSpeed > 0 ? currentSpeed / targetSpeed : 0f,
                IsAccelerating = isAccelerating,
                IsDecelerating = isDecelerating
            };

            if (_movementMetrics.Count >= maxMetricsPerType)
            {
                _movementMetrics.RemoveAt(0);
            }
            _movementMetrics.Add(metric);
        }

        /// <summary>
        /// 캔슬 메트릭 기록
        /// </summary>
        public void RecordCancel(string skillName, float cancelPoint, float timeSavedMs, string nextSkill = "")
        {
            if (!enableCollection) return;

            var metric = new CancelMetric
            {
                Timestamp = Time.time - _sessionStartTime,
                SkillName = skillName,
                CancelPoint = cancelPoint,
                TimeSaved = timeSavedMs,
                NextSkill = nextSkill
            };

            if (_cancelMetrics.Count >= maxMetricsPerType)
            {
                _cancelMetrics.RemoveAt(0);
            }
            _cancelMetrics.Add(metric);

            // 통계 업데이트
            totalCancels++;
            totalTimeSaved += timeSavedMs;
        }

        /// <summary>
        /// 세션 통계 가져오기
        /// </summary>
        public SessionStatistics GetSessionStatistics()
        {
            return new SessionStatistics
            {
                TotalInputs = totalInputs,
                BufferedInputs = bufferedInputs,
                DroppedInputs = droppedInputs,
                AverageResponseTime = totalInputs > 0 ? _totalResponseTime / totalInputs : 0f,
                TotalCancels = totalCancels,
                TotalTimeSaved = totalTimeSaved,
                SessionDuration = Time.time - _sessionStartTime
            };
        }

        /// <summary>
        /// JSON 형식으로 내보내기 (포트폴리오용)
        /// </summary>
        public string ExportToJson()
        {
            var exportData = new MetricsExportData
            {
                ExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                SessionDuration = Time.time - _sessionStartTime,
                Statistics = GetSessionStatistics(),
                InputMetrics = _inputMetrics.ToArray(),
                MovementMetrics = _movementMetrics.ToArray(),
                CancelMetrics = _cancelMetrics.ToArray()
            };

            return JsonUtility.ToJson(exportData, true);
        }

        /// <summary>
        /// 파일로 저장
        /// </summary>
        public void SaveToFile(string fileName = null)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"ControlMetrics_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            }

            string path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
            System.IO.File.WriteAllText(path, ExportToJson());
            Debug.Log($"[ControlMetrics] Saved to: {path}");
        }

        // 읽기 전용 프로퍼티
        public int TotalInputs => totalInputs;
        public int BufferedInputs => bufferedInputs;
        public int DroppedInputs => droppedInputs;
        public int TotalCancels => totalCancels;
        public float TotalTimeSaved => totalTimeSaved;
        public float SessionDuration => Time.time - _sessionStartTime;
        public IReadOnlyList<InputMetric> InputMetrics => _inputMetrics;
        public IReadOnlyList<MovementMetric> MovementMetrics => _movementMetrics;
        public IReadOnlyList<CancelMetric> CancelMetrics => _cancelMetrics;
    }

    /// <summary>
    /// JSON 내보내기용 데이터 구조
    /// </summary>
    [Serializable]
    public class MetricsExportData
    {
        public string ExportTime;
        public float SessionDuration;
        public SessionStatistics Statistics;
        public InputMetric[] InputMetrics;
        public MovementMetric[] MovementMetrics;
        public CancelMetric[] CancelMetrics;
    }
}
