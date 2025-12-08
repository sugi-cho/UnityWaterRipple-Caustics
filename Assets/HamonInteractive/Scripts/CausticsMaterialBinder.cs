using UnityEngine;

namespace HamonInteractive
{
    /// <summary>
    /// Caustics 用マテリアルにライト/壁情報を流し込む。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CausticsMaterialBinder : MonoBehaviour
    {
        public enum CausticsType
        {
            Reflection = 0,
            Refraction = 1,
        }

        private enum LightTypeFlag
        {
            Directional = 0,
            Other = 1,
        }

        [Header("参照")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Light targetLight;
        [SerializeField] private Transform wallTransform;

        [Header("設定")]
        [SerializeField] private CausticsType causticsType = CausticsType.Reflection;
        [SerializeField] private bool autoUpdate = true;

        private static readonly int IdLightProp = Shader.PropertyToID("_LightProp");
        private static readonly int IdLightType = Shader.PropertyToID("_LightType");
        private static readonly int IdWallPosition = Shader.PropertyToID("_WallPosition");
        private static readonly int IdWallNormal = Shader.PropertyToID("_WallNormal");
        private static readonly int IdCausticsType = Shader.PropertyToID("_CausticsType");

        private MaterialPropertyBlock _mpb;

        private void OnEnable()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            Apply();
        }

        private void Update()
        {
            if (autoUpdate) Apply();
        }

        private void OnValidate()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            Apply();
        }

        public void Apply()
        {
            if (targetRenderer == null) return;

            // LightProp と LightType
            Vector3 lightProp = Vector3.zero;
            LightTypeFlag lightType = LightTypeFlag.Other;
            if (targetLight != null && targetLight.type == LightType.Directional)
            {
                // Directional Light の光線進行方向は -transform.forward（ライトへの方向は +forward）
                lightProp = -targetLight.transform.forward.normalized;
                lightType = LightTypeFlag.Directional;
            }
            else if (targetLight != null)
            {
                lightProp = targetLight.transform.position;
                lightType = LightTypeFlag.Other;
            }

            // 壁座標と法線
            Vector3 wallPos = wallTransform ? wallTransform.position : Vector3.zero;
            Vector3 wallNormal = wallTransform ? wallTransform.up : Vector3.up;

            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetVector(IdLightProp, lightProp);
            _mpb.SetFloat(IdLightType, (float)lightType);
            _mpb.SetVector(IdWallPosition, wallPos);
            _mpb.SetVector(IdWallNormal, wallNormal.normalized);
            _mpb.SetFloat(IdCausticsType, (float)causticsType);
            targetRenderer.SetPropertyBlock(_mpb);
        }
    }
}
