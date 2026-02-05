using UnityEngine;

namespace Character.Player.ControlSettings
{
    /// <summary>
    /// 조작감 개선 시스템 설정 ScriptableObject
    /// 포트폴리오용 ON/OFF 비교 테스트를 위해 모든 기능을 토글 가능
    ///
    /// 사용법:
    /// 1. Project 창에서 우클릭 > Create > EternalReturn > Control Settings Config
    /// 2. 생성된 에셋을 캐릭터 프리팹에 할당
    /// </summary>
    [CreateAssetMenu(fileName = "ControlSettingsConfig", menuName = "EternalReturn/Control Settings Config")]
    public class ControlSettingsConfig : ScriptableObject
    {
        [Header("=== Feature Toggles ===")]
        [Tooltip("입력 버퍼링 활성화 (스킬 시전 중 입력을 저장하여 연속 시전)")]
        public bool EnableInputBuffering = true;

        [Tooltip("애니메이션 캔슬 활성화 (데미지 적용 후 후딜레이 스킵 가능)")]
        public bool EnableAnimationCanceling = true;

        [Tooltip("가속/감속 커브 활성화 (부드러운 이동 시작/정지)")]
        public bool EnableAccelerationCurves = true;

        [Header("=== Input Buffering Settings ===")]
        [Tooltip("버퍼에 저장된 입력이 유지되는 시간 (초)")]
        [Range(0.1f, 0.5f)]
        public float BufferDuration = 0.3f;

        [Tooltip("최대 버퍼 크기 (저장할 수 있는 입력 개수)")]
        [Range(1, 4)]
        public int MaxBufferSize = 2;

        [Header("=== Acceleration Curve Settings ===")]
        [Tooltip("정지 상태에서 최대 속도까지 도달하는 시간 (초)")]
        [Range(0.05f, 0.3f)]
        public float AccelerationTime = 0.15f;

        [Tooltip("최대 속도에서 정지까지 감속하는 시간 (초)")]
        [Range(0.05f, 0.2f)]
        public float DecelerationTime = 0.1f;

        [Tooltip("가속 커브 (0=정지, 1=최대속도)")]
        public AnimationCurve AccelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("감속 커브 (0=최대속도, 1=정지)")]
        public AnimationCurve DecelerationCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("=== Animation Canceling Settings ===")]
        [Tooltip("캔슬 가능 시작 시점 (스킬 시전 시간 대비 비율)")]
        [Range(0.3f, 0.7f)]
        public float CancelWindowStartRatio = 0.4f;

        /// <summary>
        /// 기본 설정값으로 초기화
        /// </summary>
        public void ResetToDefaults()
        {
            EnableInputBuffering = true;
            EnableAnimationCanceling = true;
            EnableAccelerationCurves = true;

            BufferDuration = 0.3f;
            MaxBufferSize = 2;

            AccelerationTime = 0.15f;
            DecelerationTime = 0.1f;
            AccelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            DecelerationCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

            CancelWindowStartRatio = 0.4f;
        }

        /// <summary>
        /// 모든 기능 끄기 (비교 테스트용)
        /// </summary>
        public void DisableAllFeatures()
        {
            EnableInputBuffering = false;
            EnableAnimationCanceling = false;
            EnableAccelerationCurves = false;
        }

        /// <summary>
        /// 모든 기능 켜기 (비교 테스트용)
        /// </summary>
        public void EnableAllFeatures()
        {
            EnableInputBuffering = true;
            EnableAnimationCanceling = true;
            EnableAccelerationCurves = true;
        }
    }
}
