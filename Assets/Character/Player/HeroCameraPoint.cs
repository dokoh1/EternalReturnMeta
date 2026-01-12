using System;
using Fusion;
using UnityEngine;

public class HeroCameraPoint : MonoBehaviour
{    
    [SerializeField] private Transform player;
    
    [SerializeField] private float FixedYPosition = 9;

    [SerializeField] private Vector3 DistanceOffset;

    public void InitPos(Vector3 pos)
    {
        Vector3 targetPosition = new Vector3(pos.x + DistanceOffset.x, FixedYPosition, pos.z + DistanceOffset.z);
        transform.position = targetPosition;
    }

    private void LateUpdate()
    {
        if (player != null)
        {
            // 플레이어의 X, Z 좌표를 기반으로 카메라 위치를 업데이트하되, Y는 고정
            Vector3 targetPosition = new Vector3(player.position.x + DistanceOffset.x, FixedYPosition, player.position.z + DistanceOffset.z);
            transform.position = targetPosition;
        }
    }
}
