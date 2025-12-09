using UnityEngine;

namespace HamonInteractive
{
    /// <summary>
    /// Ripple ResultRT を元に簡易反射コースティクスを生成し、RenderTexture へ加算描画する。
    /// 1テクセル=1ポイントで反射先へスプラットするため軽量・低ノイズ。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class CausticsRenderer : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private RippleSimulation ripple;
        [SerializeField] private Transform sourceQuad;   // 水面。未指定なら ripple の Transform
        [SerializeField] private Transform targetQuad;   // 受け面。未指定なら sourceQuad
        [SerializeField] private Light directionalLight;
        [SerializeField] private ComputeShader causticsCompute;
        [SerializeField] private Shader causticsAddShader;

        [Header("出力")]
        [SerializeField] private Vector2Int outputResolution = new Vector2Int(512, 512);
        [SerializeField] private RenderTexture causticsRT;

        [Header("調整")]
        [SerializeField] private float energyScale = 1.0f;
        [SerializeField, Range(0f, 1f)] private float normalInfluence = 1.0f;
        [SerializeField] private Color colorTint = Color.white;
        [SerializeField] private bool autoRender = true;

        [Header("可視化用メッシュ生成")]
        [SerializeField] private bool autoCreateMeshes = true;
        [SerializeField] private Vector2 floorSize = new Vector2(4, 4);
        [SerializeField] private Vector2 wallSize = new Vector2(4, 4);
        [SerializeField] private float wallOffset = 2.0f;
        [SerializeField] private float wallHeightOffset = 0.0f;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;

        private Transform _generatedFloor;
        private Transform _generatedWall;
        private const string FloorName = "Caustics_Floor";
        private const string WallName = "Caustics_Wall";
        private ComputeBuffer _hitBuffer;
        private int _hitCount;
        private int _kernel;
        private Material _material;

        private void OnEnable()
        {
            Init();
            if (autoCreateMeshes) EnsureMeshes();
        }

        private void OnValidate()
        {
            if (autoCreateMeshes && isActiveAndEnabled)
            {
                EnsureMeshes();
            }
        }

        private void OnDisable()
        {
            Release();
            if (autoCreateMeshes)
            {
                SafeDestroyGenerated(_generatedFloor);
                SafeDestroyGenerated(_generatedWall);
                _generatedFloor = null;
                _generatedWall = null;
            }
        }

        private void Update()
        {
            if (autoCreateMeshes) EnsureMeshes();
            if (autoRender)
            {
                RenderCaustics();
            }
        }

        public RenderTexture CausticsRT => causticsRT;

        private void Init()
        {
            if (causticsCompute != null)
            {
                _kernel = causticsCompute.FindKernel("GenerateHits");
            }
            if (causticsAddShader != null)
            {
                _material = new Material(causticsAddShader);
            }
            EnsureOutputRT();
        }

        private void EnsureMeshes()
        {
            if (_generatedFloor == null) _generatedFloor = FindChild(transform, FloorName);
            if (_generatedWall == null) _generatedWall = FindChild(transform, WallName);

            if (_generatedFloor == null)
            {
                _generatedFloor = CreateQuadGO(FloorName, floorSize, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), floorMaterial);
            }
            if (_generatedWall == null)
            {
                var wallPos = new Vector3(0, wallHeightOffset, wallOffset);
                _generatedWall = CreateQuadGO(WallName, wallSize, wallPos, Quaternion.identity, wallMaterial);
            }

            UpdateQuadMesh(_generatedFloor, floorSize);
            UpdateQuadMesh(_generatedWall, wallSize);

            if (sourceQuad == null) sourceQuad = _generatedFloor;
            if (targetQuad == null) targetQuad = _generatedWall;
        }

        private Transform FindChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c;
            }
            return null;
        }

        private Transform CreateQuadGO(string name, Vector2 size, Vector3 localPos, Quaternion localRot, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = Vector3.one;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mf.sharedMesh = BuildQuadMesh(size);
            return go.transform;
        }

        private void UpdateQuadMesh(Transform t, Vector2 size)
        {
            if (t == null) return;
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null) return;
            mf.sharedMesh = BuildQuadMesh(size);
        }

        private Mesh BuildQuadMesh(Vector2 size)
        {
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-hx, -hy, 0),
                new Vector3( hx, -hy, 0),
                new Vector3( hx,  hy, 0),
                new Vector3(-hx,  hy, 0)
            };
            mesh.uv = new[]
            {
                new Vector2(0,0),
                new Vector2(1,0),
                new Vector2(1,1),
                new Vector2(0,1)
            };
            mesh.normals = new[]
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward
            };
            mesh.triangles = new[] { 0,1,2, 0,2,3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Release()
        {
            _hitBuffer?.Release();
            _hitBuffer = null;
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
        }

        private void SafeDestroyGenerated(Transform t)
        {
            if (t == null) return;
            if (t.parent == transform && (t.name == FloorName || t.name == WallName))
            {
                if (Application.isPlaying)
                    Destroy(t.gameObject);
                else
                    DestroyImmediate(t.gameObject);
            }
        }

        private bool EnsureOutputRT()
        {
            if (causticsRT != null &&
                causticsRT.width == outputResolution.x &&
                causticsRT.height == outputResolution.y)
            {
                return true;
            }

            if (causticsRT != null) causticsRT.Release();
            causticsRT = new RenderTexture(outputResolution.x, outputResolution.y, 0, RenderTextureFormat.ARGBHalf)
            {
                enableRandomWrite = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                name = "CausticsRT"
            };
            causticsRT.Create();
            return true;
        }

        private bool EnsureBuffers(int count)
        {
            if (_hitBuffer != null && _hitCount == count) return true;
            _hitBuffer?.Release();
            _hitCount = count;
            _hitBuffer = new ComputeBuffer(_hitCount, sizeof(float) * 4);
            return true;
        }

        public void RenderCaustics()
        {
            if (ripple == null || causticsCompute == null || causticsAddShader == null) return;
            var resultRT = ripple.ResultTexture;
            if (resultRT == null) return;

            EnsureOutputRT();

            int srcW = resultRT.width;
            int srcH = resultRT.height;
            int count = srcW * srcH;
            EnsureBuffers(count);

            Transform src = sourceQuad != null ? sourceQuad : ripple.transform;
            Transform tgt = targetQuad != null ? targetQuad : src;
            Vector3 lightDir = directionalLight ? directionalLight.transform.forward : Vector3.down;
            Vector3 srcScale = src.lossyScale;
            Vector3 tgtScale = tgt.lossyScale;
            Vector3 tgtNormal = Vector3.Normalize(Vector3.Cross(tgt.right, tgt.up));

            causticsCompute.SetInts("_SourceResolution", srcW, srcH);
            causticsCompute.SetFloat("_EnergyScale", energyScale);
            causticsCompute.SetFloat("_NormalInfluence", normalInfluence);
            causticsCompute.SetVector("_LightDirWS", lightDir.normalized);

            causticsCompute.SetVector("_SourcePos", src.position);
            causticsCompute.SetVector("_SourceRight", src.right);
            causticsCompute.SetVector("_SourceUp", src.up);
            causticsCompute.SetVector("_SourceScale", new Vector2(srcScale.x, srcScale.y));

            causticsCompute.SetVector("_TargetPos", tgt.position);
            causticsCompute.SetVector("_TargetRight", tgt.right);
            causticsCompute.SetVector("_TargetUp", tgt.up);
            causticsCompute.SetVector("_TargetNormal", tgtNormal);
            causticsCompute.SetVector("_TargetScale", new Vector2(tgtScale.x, tgtScale.y));

            causticsCompute.SetTexture(_kernel, "_ResultTex", resultRT);
            causticsCompute.SetBuffer(_kernel, "_HitBuffer", _hitBuffer);

            int gx = Mathf.CeilToInt(srcW / (float)8);
            int gy = Mathf.CeilToInt(srcH / (float)8);
            causticsCompute.Dispatch(_kernel, gx, gy, 1);

            if (_material == null) return;

            _material.SetBuffer("_HitBuffer", _hitBuffer);
            _material.SetVector("_ColorTint", colorTint);

            var prev = RenderTexture.active;
            RenderTexture.active = causticsRT;
            GL.Clear(true, true, Color.black);
            _material.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Points, _hitCount);
            RenderTexture.active = prev;
        }
    }
}
