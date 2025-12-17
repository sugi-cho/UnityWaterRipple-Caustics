using UnityEngine;

namespace HamonInteractive
{
    /// <summary>
    /// Debug component: raycast against a MeshCollider, map hit UV to RippleSimulation.
    /// </summary>
    [DisallowMultipleComponent]
    public class RippleMouseDebug : MonoBehaviour
    {
        [SerializeField] private RippleSimulation simulation;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private MeshCollider targetMeshCollider;
        [SerializeField, Tooltip("Raycast max distance")] private float rayDistance = 200f;
        [SerializeField, Range(0.001f, 0.25f)] private float brushRadius = 0.03f;
        [SerializeField] private float brushStrength = 2.0f;
        [SerializeField, Range(0.1f, 8f)] private float brushFalloff = 2.0f;
        [SerializeField] private bool clearEachFrame = true;

        private void Reset()
        {
            if (simulation == null)
            {
                simulation = GetComponent<RippleSimulation>();
            }
        }

        private void OnValidate()
        {
            brushRadius = Mathf.Clamp(brushRadius, 0.001f, 0.25f);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (simulation == null) return;

            if (clearEachFrame)
            {
                simulation.ClearForceTexture();
            }

            bool isDown = Input.GetMouseButton(0) || Input.GetMouseButton(1);
            if (!isDown) return;

            Camera cam = inputCamera != null ? inputCamera : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance)) return;
            if (targetMeshCollider != null && hit.collider != targetMeshCollider) return;

            // Use mesh UV from the hit
            Vector2 uv = hit.textureCoord;
            // As a fallback, compute from mesh data if unavailable
            if (uv == Vector2.zero && hit.collider is MeshCollider mc && mc.sharedMesh != null)
            {
                var mesh = mc.sharedMesh;
                int tri = hit.triangleIndex * 3;
                if (mesh.uv != null && mesh.uv.Length > tri + 2)
                {
                    Vector2 uv0 = mesh.uv[mesh.triangles[tri]];
                    Vector2 uv1 = mesh.uv[mesh.triangles[tri + 1]];
                    Vector2 uv2 = mesh.uv[mesh.triangles[tri + 2]];
                    uv = uv0 * hit.barycentricCoordinate.x +
                         uv1 * hit.barycentricCoordinate.y +
                         uv2 * hit.barycentricCoordinate.z;
                }
            }

            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) return;

            float sign = Input.GetMouseButton(0) ? 1f : -1f;
            simulation.AddForceBrush(uv, brushRadius, brushStrength * sign, brushFalloff);
        }
    }
}
