using UnityEngine;

[ExecuteAlways]
public class FitToCamera : MonoBehaviour
{
    [SerializeField] Camera cam;

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        float height = cam.orthographicSize * 2f;
        transform.localScale = new Vector3(height * cam.aspect, height, 1f);

        // Centrado en la cámara: el tamaño solo no alcanza si la cámara no está en el origen.
        Vector3 c = cam.transform.position;
        transform.position = new Vector3(c.x, c.y, transform.position.z);
    }   
}