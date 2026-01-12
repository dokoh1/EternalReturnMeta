using System;
using Fusion;
using Unity.Cinemachine;
using UnityEngine;

public class CameraController : NetworkBehaviour
{
    public CinemachineCamera _CinemachineCamera;
    public Transform CameraPoint;

    public float CameraMoveSpeed = 10f; // 카메라 이동 속도
    public float ScreenEdgeThreshold = 10f;
    
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
        CameraMoveToScreenEdge();

        if (Input.GetKey(KeyCode.Space))
        {
            _CinemachineCamera.transform.position = CameraPoint.transform.position;
            if (_CinemachineCamera.Target.TrackingTarget == null)
            {
                _CinemachineCamera.Target.TrackingTarget = CameraPoint.transform;
            }
        }
        if(Input.GetKeyUp(KeyCode.Space))
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
