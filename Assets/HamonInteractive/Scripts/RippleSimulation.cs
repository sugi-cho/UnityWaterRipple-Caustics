using UnityEngine;

namespace HamonInteractive
{
    /// <summary>
    /// RenderTexture ベースの波紋シミュレーション管理。
    /// 状態: R=高さ, G=速度 / 結果: RGB=法線(0-1), A=高さ
    /// </summary>
    [DisallowMultipleComponent]
    public class RippleSimulation : MonoBehaviour
    {
        private const int MaxSize = 4096;
        private const int ThreadGroup = 8;

        [Header("基本設定")]
        [SerializeField] private ComputeShader rippleCompute;
        [SerializeField] private Vector2Int resolution = new Vector2Int(512, 512);
        [SerializeField, Range(0f, 10f)] private float waveSpeed = 2.0f;
        [SerializeField, Range(0f, 1f)] private float damping = 0.02f;
        [SerializeField] private float depthScale = 1.0f;
        [SerializeField] private float flowScale = 1.0f;
        [SerializeField] private float boundaryBounce = 1.0f;
        [SerializeField] private float forceToVelocity = 1.0f;

        [Header("入力テクスチャ")]
        [Tooltip("白=水面, 黒=地面の境界テクスチャ")]
        [SerializeField] private Texture boundaryTexture;
        [Tooltip("0-1 の水底起伏マップ")]
        [SerializeField] private Texture depthTexture;
        [Tooltip("RG で XY 方向のフロー")]
        [SerializeField] private Texture flowTexture;
        [Tooltip("外部から与える力テクスチャ (任意)")]
        [SerializeField] private RenderTexture externalForce;
        [SerializeField] private bool useExternalForce = false;

        [Header("デバッグ入力 (マウス)")]
        [SerializeField] private bool enableMouseInput = true;
        [SerializeField, Range(0.001f, 0.25f)] private float mouseRadius = 0.03f;
        [SerializeField] private float mouseStrength = 2.0f;
        [SerializeField, Range(0.1f, 8f)] private float mouseFalloff = 2.0f;
        [SerializeField] private Camera mouseCamera;

        [Header("プレビュー")]
        [SerializeField] private bool showPreviews = true;

        public RenderTexture ResultTexture => _result;
        public RenderTexture ForceTexture => _force;
        public RenderTexture StateTexture => _stateRead;

        private RenderTexture _stateA;
        private RenderTexture _stateB;
        private RenderTexture _force;
        private RenderTexture _result;
        private bool _useAasRead = true;

        private int _kernelSim;
        private int _kernelNormals;
        private int _kernelBrush;
        private int _kernelClear;

        private void OnEnable()
        {
            InitKernels();
            ResizeIfNeeded(true);
            ClearState();
        }

        private void OnDisable()
        {
            ReleaseAll();
        }

        private void OnValidate()
        {
            resolution.x = Mathf.Clamp(resolution.x, 16, MaxSize);
            resolution.y = Mathf.Clamp(resolution.y, 16, MaxSize);
            if (Application.isPlaying)
            {
                ResizeIfNeeded(false);
            }
        }

        private void Update()
        {
            if (rippleCompute == null) return;
            if (!EnsureResources()) return;

            HandleMouseInput();
            Simulate(Time.deltaTime);
        }

        public void ClearForceTexture()
        {
            if (!EnsureResources()) return;
            rippleCompute.SetInts("_SimSize", resolution.x, resolution.y);
            rippleCompute.SetFloats("_InvSimSize", 1f / resolution.x, 1f / resolution.y);
            rippleCompute.SetTexture(_kernelClear, "_ForceTarget", _force);
            DispatchByTexture(_kernelClear, resolution);
        }

        public void ResetSimulation()
        {
            ClearState();
        }

        private void InitKernels()
        {
            if (rippleCompute == null) return;
            _kernelSim = rippleCompute.FindKernel("SimStep");
            _kernelNormals = rippleCompute.FindKernel("MakeNormals");
            _kernelBrush = rippleCompute.FindKernel("Brush");
            _kernelClear = rippleCompute.FindKernel("ClearForce");
        }

        private bool EnsureResources()
        {
            if (rippleCompute == null) return false;
            if (_stateA != null && _stateA.width == resolution.x && _stateA.height == resolution.y) return true;
            ResizeIfNeeded(true);
            return true;
        }

        private void ResizeIfNeeded(bool force)
        {
            if (!force && _stateA != null && _stateA.width == resolution.x && _stateA.height == resolution.y) return;
            ReleaseAll();
            _stateA = CreateRT(RenderTextureFormat.RGHalf, "Ripple_StateA");
            _stateB = CreateRT(RenderTextureFormat.RGHalf, "Ripple_StateB");
            _force = CreateRT(RenderTextureFormat.RHalf, "Ripple_Force");
            _result = CreateRT(RenderTextureFormat.ARGBHalf, "Ripple_Result");
            _useAasRead = true;
            ClearState();
        }

        private RenderTexture CreateRT(RenderTextureFormat format, string name)
        {
            var rt = new RenderTexture(resolution.x, resolution.y, 0, format)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                name = name
            };
            rt.Create();
            return rt;
        }

        private void ReleaseAll()
        {
            ReleaseRT(ref _stateA);
            ReleaseRT(ref _stateB);
            ReleaseRT(ref _force);
            ReleaseRT(ref _result);
        }

        private void ReleaseRT(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            DestroyImmediate(rt);
            rt = null;
        }

        private RenderTexture StateRead => _useAasRead ? _stateA : _stateB;
        private RenderTexture StateWrite => _useAasRead ? _stateB : _stateA;

        private void Simulate(float deltaTime)
        {
            deltaTime = Mathf.Min(deltaTime, 1f / 30f); // 安定性のため上限

            rippleCompute.SetFloat("_DeltaTime", deltaTime);
            rippleCompute.SetFloat("_Damping", damping);
            rippleCompute.SetFloat("_WaveSpeed", waveSpeed);
            rippleCompute.SetFloat("_DepthScale", depthScale);
            rippleCompute.SetFloat("_FlowScale", flowScale);
            rippleCompute.SetFloat("_BoundaryBounce", boundaryBounce);
            rippleCompute.SetFloat("_ForceToVelocity", forceToVelocity);
            rippleCompute.SetFloats("_InvSimSize", 1f / resolution.x, 1f / resolution.y);
            rippleCompute.SetInts("_SimSize", resolution.x, resolution.y);
            rippleCompute.SetInt("_UseBoundary", boundaryTexture ? 1 : 0);
            rippleCompute.SetInt("_UseDepth", depthTexture ? 1 : 0);
            rippleCompute.SetInt("_UseFlow", flowTexture ? 1 : 0);
            rippleCompute.SetInt("_UseExternalForce", useExternalForce && externalForce ? 1 : 0);

            rippleCompute.SetTexture(_kernelSim, "_StateRead", StateRead);
            rippleCompute.SetTexture(_kernelSim, "_StateWrite", StateWrite);
            rippleCompute.SetTexture(_kernelSim, "_Boundary", boundaryTexture ? boundaryTexture : Texture2D.whiteTexture);
            rippleCompute.SetTexture(_kernelSim, "_DepthTex", depthTexture ? depthTexture : Texture2D.blackTexture);
            rippleCompute.SetTexture(_kernelSim, "_FlowTex", flowTexture ? flowTexture : Texture2D.blackTexture);
            rippleCompute.SetTexture(_kernelSim, "_ForceTex", _force);
            rippleCompute.SetTexture(_kernelSim, "_ExternalForceTex", externalForce ? externalForce : Texture2D.blackTexture);

            DispatchByTexture(_kernelSim, resolution);
            SwapStates();

            rippleCompute.SetTexture(_kernelNormals, "_StateRead", StateRead);
            rippleCompute.SetTexture(_kernelNormals, "_Result", _result);
            rippleCompute.SetInts("_SimSize", resolution.x, resolution.y);
            rippleCompute.SetFloats("_InvSimSize", 1f / resolution.x, 1f / resolution.y);
            DispatchByTexture(_kernelNormals, resolution);
        }

        private void DispatchByTexture(int kernel, Vector2Int size)
        {
            int gx = Mathf.CeilToInt(size.x / (float)ThreadGroup);
            int gy = Mathf.CeilToInt(size.y / (float)ThreadGroup);
            rippleCompute.Dispatch(kernel, gx, gy, 1);
        }

        private void SwapStates()
        {
            _useAasRead = !_useAasRead;
        }

        private void ClearState()
        {
            if (_stateA != null) Graphics.Blit(Texture2D.blackTexture, _stateA);
            if (_stateB != null) Graphics.Blit(Texture2D.blackTexture, _stateB);
            if (_force != null) Graphics.Blit(Texture2D.blackTexture, _force);
            if (_result != null) Graphics.Blit(Texture2D.blackTexture, _result);
        }

        private void HandleMouseInput()
        {
            if (!enableMouseInput) return;
            if (!Application.isPlaying) return;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) return;

            var cam = mouseCamera != null ? mouseCamera : Camera.main;
            Vector3 mouse = Input.mousePosition;
            Vector2 uv;

            if (cam != null)
            {
                uv = (Vector2)cam.ScreenToViewportPoint(mouse);
            }
            else
            {
                uv = new Vector2(mouse.x / Screen.width, mouse.y / Screen.height);
            }

            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) return;

            float sign = Input.GetMouseButton(0) ? 1f : -1f;
            StampForce(uv, mouseRadius, mouseStrength * sign, mouseFalloff);
        }

        private void StampForce(Vector2 uv, float radius, float strength, float falloff)
        {
            rippleCompute.SetTexture(_kernelBrush, "_ForceTarget", _force);
            rippleCompute.SetFloats("_InvSimSize", 1f / resolution.x, 1f / resolution.y);
            rippleCompute.SetInts("_SimSize", resolution.x, resolution.y);
            rippleCompute.SetVector("_Brush", new Vector4(uv.x, uv.y, radius, strength));
            rippleCompute.SetFloat("_BrushFalloff", falloff);
            DispatchByTexture(_kernelBrush, resolution);
        }

#if UNITY_EDITOR
        // プレビュー用に Editor からアクセス
        public RenderTexture DebugStateTexture => StateRead;
        public RenderTexture DebugResultTexture => _result;
        public RenderTexture DebugForceTexture => _force;
#endif
    }
}
