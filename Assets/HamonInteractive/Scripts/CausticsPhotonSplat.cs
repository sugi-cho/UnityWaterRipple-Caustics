using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HamonInteractive
{
    /// <summary>
    /// フォトンスプラット方式でコースティクス密度を生成し、変形メッシュで描画する。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CausticsPhotonSplat : MonoBehaviour
    {
        private enum LightTypeFlag { Directional = 0, Other = 1 }
        public enum CausticsType { Reflection = 0, Refraction = 1 }

        [Header("参照")]
        [SerializeField] private RippleSimulation ripple;
        [SerializeField] private Transform sourcePlane;
        [SerializeField] private Transform targetPlane;
        [SerializeField] private Light targetLight;
        [SerializeField] private ComputeShader photonCompute;
        [SerializeField] private Shader photonMeshShader;

        [Header("出力")]
        [SerializeField] private Vector2Int targetResolution = new Vector2Int(256, 256);

        [Header("パラメータ")]
        [SerializeField] private CausticsType causticsType = CausticsType.Reflection;
        [SerializeField] private float refractionEta = 1.0f; // n1/n2
        [SerializeField] private float energyScale = 1.0f;
        [SerializeField, Range(0f, 1f)] private float normalInfluence = 1.0f;
        [SerializeField] private Color colorTint = Color.white;
        [SerializeField] private bool autoUpdate = true;
        [SerializeField] private float densityScale = 4096f; // fixed-pointスケール

        private ComputeBuffer _densityBuffer;
        private int _kernelClear;
        private int _kernelAcc;
        private Mesh _mesh;
        private Material _material;
        private MeshFilter _mf;
        private MeshRenderer _mr;

        private const int kClearThread = 256;
        private const int kAccThread = 8;

        private void OnEnable()
        {
            if (photonMeshShader == null)
                photonMeshShader = Shader.Find("Hidden/Hamon/CausticsPhotonMesh");
            if (_material == null && photonMeshShader != null)
                _material = new Material(photonMeshShader);
            MarkDontSave(_material);

            InitKernels();
            EnsureMesh();
            EnsureBuffers();
        }

        private void OnDisable()
        {
            CleanupMesh();
            _densityBuffer?.Release();
            _densityBuffer = null;
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
                _material = null;
            }
        }

        private void Update()
        {
            if (!autoUpdate) return;
            RenderCaustics();
        }

        private void InitKernels()
        {
            if (photonCompute == null) return;
            _kernelClear = photonCompute.FindKernel("Clear");
            _kernelAcc = photonCompute.FindKernel("Accumulate");
        }

        private void EnsureMesh()
        {
            if (_mesh != null &&
                _mesh.vertexCount == targetResolution.x * targetResolution.y)
                return;

            _mesh = BuildGridMesh(targetResolution.x, targetResolution.y);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f); // カリング回避
            MarkDontSave(_mesh);

            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            if (_mf == null) _mf = gameObject.AddComponent<MeshFilter>();
            if (_mr == null) _mr = gameObject.AddComponent<MeshRenderer>();
            _mf.sharedMesh = _mesh;
            _mr.sharedMaterial = _material;
        }

        private void CleanupMesh()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
                _mesh = null;
            }
        }

        private void MarkDontSave(Object obj)
        {
            if (obj != null)
            {
                obj.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private Mesh BuildGridMesh(int w, int h)
        {
            var verts = new Vector3[w * h];
            var uvs = new Vector2[w * h];
            int idx = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Vector2 uv = new Vector2((x + 0.5f) / w, (y + 0.5f) / h);
                    verts[idx] = new Vector3(uv.x, uv.y, 0f);
                    uvs[idx] = uv;
                    idx++;
                }
            }

            var tris = new int[(w - 1) * (h - 1) * 6];
            int t = 0;
            for (int y = 0; y < h - 1; y++)
            {
                for (int x = 0; x < w - 1; x++)
                {
                    int i0 = y * w + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + w;
                    int i3 = i2 + 1;
                    tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
                    tris[t++] = i1; tris[t++] = i2; tris[t++] = i3;
                }
            }

            var mesh = new Mesh
            {
                indexFormat = IndexFormat.UInt32,
                vertices = verts,
                uv = uvs
            };
            mesh.SetIndices(tris, MeshTopology.Triangles, 0);
            return mesh;
        }

        private void EnsureBuffers()
        {
            int total = targetResolution.x * targetResolution.y;
            if (_densityBuffer != null && _densityBuffer.count == total) return;
            _densityBuffer?.Release();
            _densityBuffer = new ComputeBuffer(total, sizeof(uint));
        }

        public void RenderCaustics()
        {
            if (ripple == null || photonCompute == null || _material == null) return;
            var srcTex = ripple.ResultTexture;
            if (srcTex == null) return;

            EnsureMesh();
            EnsureBuffers();

            Vector2Int srcRes = new Vector2Int(srcTex.width, srcTex.height);
            Vector2Int tgtRes = targetResolution;
            int total = tgtRes.x * tgtRes.y;

            // Clear
            photonCompute.SetBuffer(_kernelClear, "_Density", _densityBuffer);
            photonCompute.SetInt("_TotalCount", total);
            int gxClear = Mathf.CeilToInt(total / (float)kClearThread);
            photonCompute.Dispatch(_kernelClear, gxClear, 1, 1);

            // Accumulate
            photonCompute.SetBuffer(_kernelAcc, "_Density", _densityBuffer);
            photonCompute.SetInts("_SourceResolution", srcRes.x, srcRes.y);
            photonCompute.SetInts("_TargetResolution", tgtRes.x, tgtRes.y);
            photonCompute.SetFloat("_DensityScale", densityScale);
            photonCompute.SetFloat("_EnergyScale", energyScale);
            photonCompute.SetFloat("_NormalInfluence", normalInfluence);
            photonCompute.SetFloat("_Eta", refractionEta);
            photonCompute.SetInt("_LightType", targetLight != null && targetLight.type == LightType.Directional ? 0 : 1);
            photonCompute.SetInt("_CausticsType", (int)causticsType);
            photonCompute.SetVector("_LightProp", targetLight ? (targetLight.type == LightType.Directional ? -targetLight.transform.forward : targetLight.transform.position) : Vector3.down);

            Transform src = sourcePlane != null ? sourcePlane : transform;
            Transform tgt = targetPlane != null ? targetPlane : transform;
            Vector3 srcScale = src.lossyScale;
            Vector3 tgtScale = tgt.lossyScale;

            photonCompute.SetVector("_SourcePos", src.position);
            photonCompute.SetVector("_SourceRight", src.right);
            photonCompute.SetVector("_SourceUp", src.up);
            photonCompute.SetVector("_SourceNormal", Vector3.Normalize(Vector3.Cross(src.right, src.up)));
            photonCompute.SetVector("_SourceScale", new Vector2(srcScale.x, srcScale.y));

            photonCompute.SetVector("_TargetPos", tgt.position);
            photonCompute.SetVector("_TargetRight", tgt.right);
            photonCompute.SetVector("_TargetUp", tgt.up);
            photonCompute.SetVector("_TargetNormal", Vector3.Normalize(Vector3.Cross(tgt.right, tgt.up)));
            photonCompute.SetVector("_TargetScale", new Vector2(tgtScale.x, tgtScale.y));

            photonCompute.SetTexture(_kernelAcc, "_ResultTex", srcTex);

            int gx = Mathf.CeilToInt(srcRes.x / (float)kAccThread);
            int gy = Mathf.CeilToInt(srcRes.y / (float)kAccThread);
            photonCompute.Dispatch(_kernelAcc, gx, gy, 1);

            // Draw to caustics RT
            _material.SetBuffer("_Density", _densityBuffer);
            _material.SetFloat("_InvDensityScale", 1.0f / Mathf.Max(1e-6f, densityScale));
            _material.SetColor("_ColorTint", colorTint);
            _material.SetVector("_Resolution", new Vector4(targetResolution.x, targetResolution.y, 0, 0));

            // Additive draw directly to the current render target (camera must be set up before calling)
            Graphics.DrawMesh(_mesh, Matrix4x4.identity, _material, 0);
        }
    }
}
