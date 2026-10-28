using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class ArenaOverviewCamera : MonoBehaviour
{
    [SerializeField] private Vector2 arenaSize = new Vector2(25.2f, 17.2f);
    [SerializeField, Min(0f)] private float padding = 1.8f;

    private Camera overviewCamera;

    private void OnEnable()
    {
        overviewCamera = GetComponent<Camera>();
        FitArena();
    }

    private void LateUpdate()
    {
        FitArena();
    }

    private void FitArena()
    {
        if (overviewCamera == null || !overviewCamera.orthographic)
            return;

        float halfWidth = Mathf.Max(0f, arenaSize.x) * 0.5f + padding;
        float halfDepth = Mathf.Max(0f, arenaSize.y) * 0.5f + padding;
        float aspect = Mathf.Max(0.01f, overviewCamera.aspect);
        overviewCamera.orthographicSize = Mathf.Max(halfDepth, halfWidth / aspect);
    }
}
