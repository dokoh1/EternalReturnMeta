using System.Collections.Generic;
using UnityEngine;

namespace Character.Player.ControlSettings
{
    // ═══════════════════════════════════════════════════════════════
    // 버퍼에 저장되는 입력 하나의 데이터
    // ═══════════════════════════════════════════════════════════════
    public struct BufferedInput
    {
        public InputButton Button;      // 어떤 버튼인지 (Q, W, E, R)
        public float Timestamp;         // 언제 눌렸는지
        public Vector3 HitPosition;     // 스킬 방향/위치 (마우스 위치)
        public Vector3 MousePosition;   // 마우스 월드 좌표

        public BufferedInput(InputButton button, Vector3 hitPosition, Vector3 mousePosition)
        {
            Button = button;
            Timestamp = Time.time;
            HitPosition = hitPosition;
            MousePosition = mousePosition;
        }

        /// <summary>
        /// 이 입력이 만료되었는지 확인
        /// 예: 0.3초 전에 눌린 입력 → 너무 오래됨 → 만료
        /// </summary>
        public bool IsExpired(float bufferDuration)
        {
            return Time.time - Timestamp > bufferDuration;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Input Buffer 본체
    //
    // 작동 원리:
    // 1. 스킬 시전 중 → 입력이 들어오면 버퍼에 저장 (AddInput)
    // 2. 스킬 시전 끝 → 버퍼에서 입력 꺼내서 실행 (TryGetNextInput)
    // 3. 오래된 입력은 자동 삭제 (CleanExpiredInputs)
    // ═══════════════════════════════════════════════════════════════
    public class InputBuffer
    {
        // Queue = 선입선출 (먼저 들어온 입력이 먼저 나감)
        private Queue<BufferedInput> _buffer = new Queue<BufferedInput>();

        // 설정 참조 (버퍼 시간, 최대 크기 등)
        private readonly ControlSettingsConfig _config;

        public InputBuffer(ControlSettingsConfig config)
        {
            _config = config;
        }

        // ┌─────────────────────────────────────────────────────────┐
        // │  AddInput - 버퍼에 입력 추가                             │
        // │                                                          │
        // │  호출 시점: 스킬 시전 중에 새 스킬 버튼이 눌렸을 때       │
        // │  예: Q 시전 중 W 누름 → AddInput(W, ...)                 │
        // └─────────────────────────────────────────────────────────┘
        public bool AddInput(InputButton button, Vector3 hitPosition, Vector3 mousePosition)
        {
            // 설정이 없거나 버퍼링 비활성화면 저장 안 함
            if (_config == null || !_config.EnableInputBuffering)
            {
                return false;
            }

            // 버퍼가 가득 찼으면 가장 오래된 입력 제거
            // 예: 최대 2개인데 3번째 입력 → 첫 번째 입력 버림
            while (_buffer.Count >= _config.MaxBufferSize)
            {
                var dropped = _buffer.Dequeue();

                // 메트릭 기록: 입력이 드롭됨
                ControlMetricsCollector.Instance?.RecordInput(
                    dropped.Button.ToString(),
                    wasBuffered: true,
                    responseTimeMs: 0f,
                    wasDropped: true
                );
            }

            // 만료된 입력 정리
            CleanExpiredInputs();

            // 새 입력 추가
            var bufferedInput = new BufferedInput(button, hitPosition, mousePosition);
            _buffer.Enqueue(bufferedInput);

            Debug.Log($"[InputBuffer] {button} 버퍼에 저장됨 (현재 {_buffer.Count}개)");

            return true;
        }

        // ┌─────────────────────────────────────────────────────────┐
        // │  TryGetNextInput - 버퍼에서 입력 꺼내기                  │
        // │                                                          │
        // │  호출 시점: 스킬 시전이 끝났을 때                        │
        // │  반환: 버퍼에 입력이 있으면 true + 입력 데이터           │
        // └─────────────────────────────────────────────────────────┘
        public bool TryGetNextInput(out BufferedInput input)
        {
            // 만료된 입력 먼저 정리
            CleanExpiredInputs();

            if (_buffer.Count > 0)
            {
                input = _buffer.Dequeue();

                // 응답 시간 계산: 입력 시점 → 지금까지 걸린 시간
                float responseTime = (Time.time - input.Timestamp) * 1000f; // ms로 변환

                // 메트릭 기록: 버퍼에서 실행됨
                ControlMetricsCollector.Instance?.RecordInput(
                    input.Button.ToString(),
                    wasBuffered: true,
                    responseTimeMs: responseTime
                );

                Debug.Log($"[InputBuffer] {input.Button} 버퍼에서 실행 (대기시간: {responseTime:F0}ms)");

                return true;
            }

            input = default;
            return false;
        }

        // ┌─────────────────────────────────────────────────────────┐
        // │  HasPendingInput - 버퍼에 대기 중인 입력이 있는지 확인   │
        // │                                                          │
        // │  용도: Animation Canceling에서 사용                      │
        // │  "버퍼에 다음 스킬이 있으면 후딜레이 스킵"               │
        // └─────────────────────────────────────────────────────────┘
        public bool HasPendingInput()
        {
            CleanExpiredInputs();
            return _buffer.Count > 0;
        }

        /// <summary>
        /// 만료된 입력 제거
        /// 예: 0.3초 지난 입력은 버림 (너무 오래 기다린 입력은 의미 없음)
        /// </summary>
        public void CleanExpiredInputs()
        {
            if (_config == null) return;

            while (_buffer.Count > 0 && _buffer.Peek().IsExpired(_config.BufferDuration))
            {
                var expired = _buffer.Dequeue();

                // 메트릭 기록: 시간 만료로 드롭
                ControlMetricsCollector.Instance?.RecordInput(
                    expired.Button.ToString(),
                    wasBuffered: true,
                    responseTimeMs: 0f,
                    wasDropped: true
                );

                Debug.Log($"[InputBuffer] {expired.Button} 시간 만료로 제거됨");
            }
        }

        /// <summary>
        /// 버퍼 비우기
        /// </summary>
        public void Clear()
        {
            _buffer.Clear();
        }

        /// <summary>
        /// 현재 버퍼에 있는 입력 개수
        /// </summary>
        public int Count => _buffer.Count;
    }
}
