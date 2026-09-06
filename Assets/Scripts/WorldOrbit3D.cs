using UnityEngine;
using UnityEngine.InputSystem;

public class WorldOrbit3D : MonoBehaviour
{
    void LateUpdate()
    {
        var game=FindAnyObjectByType<GameController>();if(game==null||game.IsModalOpen)return;
        var cam=GetComponent<Camera>();var mouse=Mouse.current;
        if(mouse!=null&&mouse.rightButton.isPressed)
        {
            var plane=new Plane(Vector3.up,Vector3.zero);var ray=cam.ViewportPointToRay(new Vector3(.5f,.5f,0));
            if(plane.Raycast(ray,out float d))
            {
                var center=ray.GetPoint(d);var delta=mouse.delta.ReadValue();
                transform.RotateAround(center,Vector3.up,delta.x*.18f);
                float pitch=transform.eulerAngles.x;float next=Mathf.Clamp(pitch-delta.y*.12f,30,72);
                transform.RotateAround(center,transform.right,next-pitch);
            }
        }
        if(Keyboard.current!=null&&Keyboard.current.homeKey.wasPressedThisFrame&&game.SelectedUnit!=null)
            GetComponent<CameraPan>().CenterOn(game.SelectedUnit.transform.position);
    }
}
