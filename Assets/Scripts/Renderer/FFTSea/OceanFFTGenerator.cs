using UnityEngine;
using TA.OceanFFT;

/// <summary>
/// Tessendorf 风格 FFT 海面：Phillips 频谱 + 2D IFFT，将高度场写入纹理供 URP Shader 采样。
/// 将本脚本挂在海面片所在物体上，并指定材质与可选原点（默认 transform 位置）。
/// </summary>
[ExecuteAlways]
public class OceanFFTGenerator : MonoBehaviour
{
    [Header("FFT 网格")]
    [Min(8)]
    public int gridSize = 64;

    [Tooltip("频域对应的世界尺度（米），与 Shader 中 Patch Size 一致")]
    public float patchSize = 40f;

    [Header("Phillips 频谱")]
    public float gravity = 9.81f;
    [Tooltip("风速（米/秒），影响主波长")]
    public float windSpeed = 12f;
    public Vector2 windDirection = new Vector2(1f, 0.3f);
    [Tooltip("全局振幅缩放")]
    public float amplitude = 0.00035f;
    [Tooltip("抑制最短波，避免高频噪声")]
    public float smallWaveDamping = 0.001f;

    [Header("输出")]
    public Material targetMaterial;
    public string heightMapProperty = "_HeightMap";
    public string normalMapProperty = "_NormalMap";

    [Tooltip("是否每帧更新（关闭则仅用于烘焙或调试）")]
    public bool updateEveryFrame = true;

    Texture2D _heightTex;
    Texture2D _normalTex;
    ComplexF[,] _h0;
    ComplexF[,] _spectrum;
    float[] _heightScratch;
    float[] _normalScratch;

    [Tooltip("IFFT 后高度场整体放大，便于观察")]
    public float heightScale = 4f;

    static int NegIndex(int i, int n)
    {
        if (i == 0) return 0;
        return n - i;
    }

    Vector2 WaveVector(int i, int j, int n, float l)
    {
        float kx = (i <= n / 2) ? (2f * Mathf.PI * i / l) : (2f * Mathf.PI * (i - n) / l);
        float kz = (j <= n / 2) ? (2f * Mathf.PI * j / l) : (2f * Mathf.PI * (j - n) / l);
        return new Vector2(kx, kz);
    }

    float Phillips(Vector2 k, Vector2 windDirN, float g, float v, float a, float damp)
    {
        float k2 = k.x * k.x + k.y * k.y;
        if (k2 < 1e-12f) return 0f;

        float kLen = Mathf.Sqrt(k2);
        float k4 = k2 * k2;
        float lWind = (v * v) / g;
        float pk = a * Mathf.Exp(-1f / (k2 * lWind * lWind)) / k4;
        pk *= Mathf.Exp(-k2 * damp * damp);

        float dot = (k.x * windDirN.x + k.y * windDirN.y) / kLen;
        pk *= dot * dot;

        return Mathf.Max(0f, pk);
    }

    void EnsureBuffers()
    {
        int n = gridSize;
        if (_h0 != null && _h0.GetLength(0) != n)
            _h0 = null;
        if (_h0 != null && _h0.GetLength(0) == n) return;

        _h0 = new ComplexF[n, n];
        _spectrum = new ComplexF[n, n];
        _heightScratch = new float[n * n];

        Vector2 wn = windDirection.sqrMagnitude > 1e-6f ? windDirection.normalized : Vector2.right;

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                int ni = NegIndex(i, n);
                int nj = NegIndex(j, n);

                if (i == 0 && j == 0)
                {
                    _h0[i, j] = ComplexF.Zero;
                    continue;
                }

                if (i == ni && j == nj)
                {
                    Vector2 k = WaveVector(i, j, n, patchSize);
                    float pk = Phillips(k, wn, gravity, windSpeed, amplitude, smallWaveDamping);
                    float s = Mathf.Sqrt(pk * 0.5f);
                    float r = GaussianRandom.StandardNormal();
                    _h0[i, j] = new ComplexF(r * s, 0f);
                    continue;
                }

                if (i < ni || (i == ni && j < nj))
                {
                    Vector2 k = WaveVector(i, j, n, patchSize);
                    float pk = Phillips(k, wn, gravity, windSpeed, amplitude, smallWaveDamping);
                    if (pk < 1e-18f)
                    {
                        _h0[i, j] = ComplexF.Zero;
                        _h0[ni, nj] = ComplexF.Zero;
                        continue;
                    }

                    float s = Mathf.Sqrt(pk * 0.5f);
                    GaussianRandom.BoxMuller(out float g1, out float g2);
                    var c = new ComplexF(g1 * s, g2 * s);
                    _h0[i, j] = c;
                    _h0[ni, nj] = ComplexF.Conjugate(c);
                }
            }
        }

        if (_heightTex == null || _heightTex.width != n)
        {
            if (_heightTex != null)
            {
                if (Application.isPlaying) Destroy(_heightTex);
                else DestroyImmediate(_heightTex);
            }
            if (_normalTex != null)
            {
                if (Application.isPlaying) Destroy(_normalTex);
                else DestroyImmediate(_normalTex);
            }

            _heightTex = new Texture2D(n, n, TextureFormat.RFloat, false, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            _normalTex = new Texture2D(n, n, TextureFormat.RGFloat, false, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
        }

        if (_normalScratch == null || _normalScratch.Length != n * n * 2)
            _normalScratch = new float[n * n * 2];
    }

    void OnEnable() => EnsureBuffers();

    void OnValidate()
    {
        if (gridSize < 8) gridSize = 8;
        if ((gridSize & (gridSize - 1)) != 0)
        {
            int p = 8;
            while (p < gridSize) p <<= 1;
            gridSize = p;
        }
    }

    void LateUpdate()
    {
        if (!updateEveryFrame) return;
        Simulate(Time.time);
    }

    /// <summary>外部可调用：t 为秒。</summary>
    public void Simulate(float time)
    {
        EnsureBuffers();
        int n = gridSize;
        float t = time;

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                Vector2 k = WaveVector(i, j, n, patchSize);
                float k2 = k.x * k.x + k.y * k.y;
                if (k2 < 1e-12f)
                {
                    _spectrum[i, j] = ComplexF.Zero;
                    continue;
                }

                float kLen = Mathf.Sqrt(k2);
                float omega = Mathf.Sqrt(gravity * kLen);

                int ni = NegIndex(i, n);
                int nj = NegIndex(j, n);

                ComplexF e1 = ComplexF.ExpImag(omega * t);
                ComplexF e2 = ComplexF.ExpImag(-omega * t);

                ComplexF h0k = _h0[i, j];
                ComplexF h0NegConj = ComplexF.Conjugate(_h0[ni, nj]);

                _spectrum[i, j] = h0k * e1 + h0NegConj * e2;
            }
        }

        FFT2D.IFFT2D(_spectrum);

        float du = patchSize / n;

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                float h = _spectrum[i, j].Re * heightScale;
                _heightScratch[i * n + j] = h;
            }
        }

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                int ip = (i + 1) % n;
                int im = (i - 1 + n) % n;
                int jp = (j + 1) % n;
                int jm = (j - 1 + n) % n;

                float hx = (_heightScratch[ip * n + j] - _heightScratch[im * n + j]) / (2f * du);
                float hz = (_heightScratch[i * n + jp] - _heightScratch[i * n + jm]) / (2f * du);

                Vector3 nrm = new Vector3(-hx, 1f, -hz).normalized;
                int idx = (i * n + j) * 2;
                _normalScratch[idx] = nrm.x;
                _normalScratch[idx + 1] = nrm.z;
            }
        }

        _heightTex.SetPixelData(_heightScratch, 0);
        _heightTex.Apply(false, false);
        _normalTex.SetPixelData(_normalScratch, 0);
        _normalTex.Apply(false, false);

        if (targetMaterial != null)
        {
            if (targetMaterial.HasProperty(heightMapProperty))
                targetMaterial.SetTexture(heightMapProperty, _heightTex);
            if (targetMaterial.HasProperty(normalMapProperty))
                targetMaterial.SetTexture(normalMapProperty, _normalTex);
            if (targetMaterial.HasProperty("_OceanOrigin"))
                targetMaterial.SetVector("_OceanOrigin", transform.position);
            if (targetMaterial.HasProperty("_PatchSize"))
                targetMaterial.SetFloat("_PatchSize", patchSize);
        }
    }

    void OnDestroy()
    {
        if (_heightTex != null)
        {
            if (Application.isPlaying) Destroy(_heightTex);
            else DestroyImmediate(_heightTex);
        }
        if (_normalTex != null)
        {
            if (Application.isPlaying) Destroy(_normalTex);
            else DestroyImmediate(_normalTex);
        }
    }
}
