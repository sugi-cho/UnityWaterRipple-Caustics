using UnityEngine;

namespace HamonInteractive
{
    /// <summary>
    /// RenderTexture ベースの波紋シミュレーション管理。
    /// 状態: R=現在高さ, G=前フレーム高さ / 結果: RGB=Y-up ワールド法線(非パック), A=高さ
    /// </summary>
    [ExecuteAlways] // エディットモードでも結果RTを更新・プレビューできるようにする
    [DisallowMultipleComponent]
    public class RippleSimulation : MonoBehaviour
    {
        private const int MaxSize = 4096;
        private const int ThreadGroup = 8;

        [Header("基本設定")]
        [SerializeField] private ComputeShader rippleCompute;
        [SerializeField] private Vector2Int resolution = new Vector2Int(512, 512);
        [SerializeField, Range(0f, 50f)] private float waveSpeed = 8.0f;
        [SerializeField, Range(0.1f, 5f)] private float timeScale = 1.0f;
        [SerializeField, Range(0f, 1f)] private float damping = 0.02f;
        [SerializeField, Range(0f, 2f)] private float amplitudeDecay = 0.05f;
        [SerializeField] private float depthScale = 1.0f;
        [SerializeField] private float flowScale = 1.0f;
        [SerializeField] private float boundaryBounce = 1.0f;
        [SerializeField] private float forceToVelocity = 1.0f;
        [Header("ノーマル")]
        [SerializeField, Range(0.1f, 4f)] private float normalGradScale = 1.0f;
        [SerializeField, Range(0, 3)] private int normalBlurRadius = 1;
        [SerializeField, Range(0.1f, 3f)] private float normalBlurSigma = 1.0f;

        public enum EdgeMode { Bounce = 0, Absorb = 1, Wrap = 2 }

        [Header("端の処理")]
        [SerializeField] private EdgeMode horizontalEdge = EdgeMode.Bounce;
        [SerializeField] private EdgeMode verticalEdge = EdgeMode.Bounce;

        [Header("時間ステップ")]
        [SerializeField] private bool useFixedTimeStep = true;
        [SerializeField, Range(1f / 240f, 1f / 15f)] private float fixedTimeStep = 1f / 90f;
        [SerializeField, Range(1, 16)] private int maxSubSteps = 4;
        [SerializeField, Tooltip("accumulator が暴走しないための上限秒数")] private float maxAccumulatedTime = 0.25f;

        [Header("出力")]
        [Tooltip("シミュレーション結果をBlitで書き出す先。未設定なら内部RTのみ。")]
        [SerializeField] private RenderTexture outputTexture;
        [SerializeField] private bool autoBlitResult = true;

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

        [Header("プレビュー")]
        [SerializeField] private bool showPreviews = true;

        public RenderTexture ResultTexture => _result;
        public RenderTexture ForceTexture => _force;
        public RenderTexture StateTexture => StateRead;
        public RenderTexture OutputTexture { get => outputTexture; set => outputTexture = value; }

        private RenderTexture _stateA;
        private RenderTexture _stateB;
        private RenderTexture _force;
        private RenderTexture _result;
        private RenderTexture _resultTemp;
        private bool _useAasRead = true;
        private float _timeAccumulator = 0f;

        private int _kernelSim;
        private int _kernelNormals;
        private int _kernelBlurNormals;
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

            SimulateWithTime(Time.deltaTime * timeScale);
            BlitResultIfNeeded();
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
            _kernelBlurNormals = rippleCompute.FindKernel("BlurNormals");
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
            _stateA = CreateRT(RenderTextureFormat.RGFloat, "Ripple_StateA");
            _stateB = CreateRT(RenderTextureFormat.RGFloat, "Ripple_StateB");
            _force = CreateRT(RenderTextureFormat.RFloat, "Ripple_Force");
            _result = CreateRT(RenderTextureFormat.ARGBFloat, "Ripple_Result");
            _resultTemp = CreateRT(RenderTextureFormat.ARGBFloat, "Ripple_ResultTemp");
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
            ReleaseRT(ref _resultTemp);
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

        private void SimulateWithTime(float deltaTime)
        {
            if (useFixedTimeStep)
            {
                _timeAccumulator = Mathf.Min(_timeAccumulator + deltaTime, maxAccumulatedTime);

                int steps = 0;
                while (_timeAccumulator >= fixedTimeStep && steps < maxSubSteps)
                {
                    StepSim(fixedTimeStep);
                    _timeAccumulator -= fixedTimeStep;
                    steps++;
                }

                // 低FPSで積み残しが小さい場合は一度だけ処理して遅延を防ぐ
                
            }
            else
            {
                // 可変Δtモードは既存挙動：極端な大Δtを制限
                float clamped = Mathf.Min(deltaTime, 1f / 20f);
                StepSim(clamped);
            }
        }

        private void StepSim(float deltaTime)
        {
            rippleCompute.SetFloat("_DeltaTime", deltaTime);
            rippleCompute.SetFloat("_Damping", damping);
            rippleCompute.SetFloat("_AmplitudeDecay", amplitudeDecay);
            rippleCompute.SetFloat("_WaveSpeed", waveSpeed);
            rippleCompute.SetFloat("_DepthScale", depthScale);
            rippleCompute.SetFloat("_FlowScale", flowScale);
            rippleCompute.SetFloat("_BoundaryBounce", boundaryBounce);
            rippleCompute.SetFloat("_ForceToVelocity", forceToVelocity);
            rippleCompute.SetFloat("_NormalGradScale", normalGradScale);
            rippleCompute.SetInt("_NormalBlurRadius", normalBlurRadius);
            rippleCompute.SetFloat("_NormalBlurSigma", normalBlurSigma);
            rippleCompute.SetInt("_EdgeModeHorizontal", (int)horizontalEdge);
            rippleCompute.SetInt("_EdgeModeVertical", (int)verticalEdge);
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
            rippleCompute.SetTexture(_kernelNormals, "_ResultTemp", _resultTemp);
            rippleCompute.SetInts("_SimSize", resolution.x, resolution.y);
            rippleCompute.SetFloats("_InvSimSize", 1f / resolution.x, 1f / resolution.y);
            DispatchByTexture(_kernelNormals, resolution);

            rippleCompute.SetTexture(_kernelBlurNormals, "_ResultSrc", _resultTemp);
            rippleCompute.SetTexture(_kernelBlurNormals, "_Result", _result);
            rippleCompute.SetInts("_SimSize", resolution.x, resolution.y);
            DispatchByTexture(_kernelBlurNormals, resolution);
        }

        private void BlitResultIfNeeded()
        {
            if (!autoBlitResult) return;
            if (outputTexture == null) return;
            BlitResult(outputTexture);
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
            if (_resultTemp != null) Graphics.Blit(Texture2D.blackTexture, _resultTemp);
        }

        public void AddForceBrush(Vector2 uv, float radius, float strength, float falloff)
        {
            rippleCompute.SetTexture(_kernelBrush, "_ForceTarget", _force);
            rippleCompute.SetFloats("_InvSimSize", 1f / resolution.x, 1f / resolution.y);
            rippleCompute.SetInts("_SimSize", resolution.x, resolution.y);
            rippleCompute.SetVector("_Brush", new Vector4(uv.x, uv.y, radius, strength));
            rippleCompute.SetFloat("_BrushFalloff", falloff);
            DispatchByTexture(_kernelBrush, resolution);
        }

        public void BlitResult(RenderTexture target)
        {
            if (target == null || _result == null) return;
            Graphics.Blit(_result, target);
        }

#if UNITY_EDITOR
        // プレビュー用に Editor からアクセス
        public RenderTexture DebugStateTexture => StateRead;
        public RenderTexture DebugResultTexture => _result;
        public RenderTexture DebugForceTexture => _force;
#endif
    }
}




