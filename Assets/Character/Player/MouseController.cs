using Fusion;
using UnityEngine;

public class MouseController : NetworkBehaviour
{
    private void Start()
    {
        if (!HasInputAuthority) return;
       
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (!HasInputAuthority) return;
        LockMouseToScreenEdge();
    }

    private void LockMouseToScreenEdge()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None; 
            Cursor.visible = true; 
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Cursor.lockState = CursorLockMode.Confined; 
            Cursor.visible = true;
        }
    }
}
