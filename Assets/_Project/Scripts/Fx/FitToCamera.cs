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
    }
}