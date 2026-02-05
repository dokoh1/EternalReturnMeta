using System;
using System.Collections.Generic;
using UnityEngine;

namespace Character.Player.ControlSettings
{
    // ═══════════════════════════════════════════════════════════════
    // 개별 스킬의 캔슬 정보
    //
    // 스킬 타임라인:
    // [0ms]──────[DamagePointMs]──────[CancelWindowStartMs]──────[TotalDurationMs]
    //   │              │                      │                        │
    //   │              │                      │                        │
    //  시작         데미지 적용            캔슬 가능 시작              종료
    //              (투사체 발사)          (여기서부터 후딜 스킵 가능)
    // ═══════════════════════════════════════════════════════════════
    [Serializable]
    public class SkillCancelInfo
    {
        [Tooltip("스킬 이름 (Q, W, E, R)")]
        public string SkillName;

        [Tooltip("데미지/효과 적용 시점 (ms) - 투사체가 발사되는 시점")]
        public int DamagePointMs = 100;

        [Tooltip("캔슬 가능 시작 시점 (ms) - 이 시점부터 다음 스킬로 캔슬 가능")]
        public int CancelWindowStartMs = 120;

        [Tooltip("전체 스킬 길이 (ms) - 캔슬 없이 끝까지 재생될 때")]
        public int TotalDurationMs = 150;

        // ┌─────────────────────────────────────────────────────────┐
        // │  계산된 값들                                             │
        // └─────────────────────────────────────────────────────────┘

        /// <summary>
        /// 데미지 적용 후 캔슬 가능까지 대기 시간
        /// 예: DamagePointMs=100, CancelWindowStartMs=120 → 20ms 대기
        /// </summary>
        public int WaitAfterDamageMs => CancelWindowStartMs - DamagePointMs;

        /// <summary>
        /// 캔슬 시 스킵되는 후딜레이 시간 (= 절약되는 시간)
        /// 예: TotalDurationMs=150, CancelWindowStartMs=120 → 30ms 절약
        /// </summary>
        public int SkippableDelayMs => TotalDurationMs - CancelWindowStartMs;
    }

    // ═══════════════════════════════════════════════════════════════
    // 스킬 캔슬 데이터 ScriptableObject
    //
    // 사용법:
    // 1. Project 창에서 우클릭 > Create > EternalReturn > Skill Cancel Data
    // 2. 스킬별 타이밍 설정
    // 3. Eva_Skill 컴포넌트에 할당
    // ═══════════════════════════════════════════════════════════════
    [CreateAssetMenu(fileName = "SkillCancelData", menuName = "EternalReturn/Skill Cancel Data")]
    public class SkillCancelData : ScriptableObject
    {
        [Header("스킬별 캔슬 데이터")]
        [Tooltip("각 스킬의 캔슬 타이밍 정보")]
        public List<SkillCancelInfo> SkillInfos = new List<SkillCancelInfo>
        {
            // 기본값 - 실제 게임에 맞게 조정 필요
            new SkillCancelInfo
            {
                SkillName = "Q",
                DamagePointMs = 100,      // 100ms에 투사체 발사
                CancelWindowStartMs = 120, // 120ms부터 캔슬 가능
                TotalDurationMs = 150      // 전체 150ms (캔슬 시 30ms 절약)
            },
            new SkillCancelInfo
            {
                SkillName = "W",
                DamagePointMs = 50,       // 50ms에 스킬 발동
                CancelWindowStartMs = 70,  // 70ms부터 캔슬 가능
                TotalDurationMs = 100      // 전체 100ms (캔슬 시 30ms 절약)
            }
        };

        /// <summary>
        /// 스킬 이름으로 캔슬 정보 가져오기
        /// </summary>
        public SkillCancelInfo GetSkillInfo(string skillName)
        {
            return SkillInfos.Find(info => info.SkillName == skillName);
        }
    }
}
