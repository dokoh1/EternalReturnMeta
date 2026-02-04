using System;
using Fusion;
using Unity.Cinemachine;
using UnityEngine;

public class CameraController : NetworkBehaviour
{
    public CinemachineCamera _CinemachineCamera;
    public Transform CameraPoint;

    public float CameraMoveSpeed = 25f; // 카메라 이동 속도
    public float ScreenEdgeThreshold = 10f;

    private bool isCameraLocked = false;  // Y키 토글용

    public override void Spawned()
    {
        if(HasInputAuthority)
        {
             GameObject obj = GameObject.FindWithTag("CinemachineCamera");
             _CinemachineCamera = obj.GetComponent<CinemachineCamera>();
             _CinemachineCamera.transform.position = CameraPoint.transform.position;
        }
    }

    private void Update()
    {
        if (!HasInputAuthority) return;

        // Y키: 카메라 고정 토글
        if (Input.GetKeyDown(KeyCode.Y))
        {
            isCameraLocked = !isCameraLocked;

            if (isCameraLocked)
            {
                _CinemachineCamera.Target.TrackingTarget = CameraPoint.transform;
            }
            else
            {
                _CinemachineCamera.Target.TrackingTarget = null;
            }
        }

        // 카메라 고정 중이면 가장자리 이동 비활성화
        if (!isCameraLocked)
        {
            CameraMoveToScreenEdge();
        }

        // Space키: 일시적으로 캐릭터 위치로 이동
        if (Input.GetKey(KeyCode.Space))
        {
            _CinemachineCamera.transform.position = CameraPoint.transform.position;
            if (_CinemachineCamera.Target.TrackingTarget == null && !isCameraLocked)
            {
                _CinemachineCamera.Target.TrackingTarget = CameraPoint.transform;
            }
        }
        if (Input.GetKeyUp(KeyCode.Space) && !isCameraLocked)
        {
            _CinemachineCamera.Target.TrackingTarget = null;
        }
    }

    private void CameraMoveToScreenEdge()
    {
        Vector3 mousePosition = Input.mousePosition;
        
        // 화면 너비와 높이 
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // 카메라 이동 벡터 초기화
        Vector3 cameraMoveDirection = Vector3.zero;

        // 마우스가 화면 왼쪽 경계에 있는지 
        if (mousePosition.x <= ScreenEdgeThreshold)
        {
            cameraMoveDirection.x = -1; 
        }
        // 마우스가 화면 오른쪽 경계에 있는지 
        else if (mousePosition.x >= screenWidth - ScreenEdgeThreshold)
        {
            cameraMoveDirection.x = 1; 
        }

        // 마우스가 화면 아래 경계에 있는지 
        if (mousePosition.y <= ScreenEdgeThreshold)
        {
            cameraMoveDirection.z = -1; 
        }
        // 마우스가 화면 위 경계에 있는지 
        else if (mousePosition.y >= screenHeight - ScreenEdgeThreshold)
        {
            cameraMoveDirection.z = 1; 
        }

        // 카메라 이동
        if (cameraMoveDirection != Vector3.zero)
        {
            Vector3 moveVector = cameraMoveDirection.normalized * (CameraMoveSpeed * Time.deltaTime);
            _CinemachineCamera.transform.Translate(moveVector, Space.World);
        }
    }
}
