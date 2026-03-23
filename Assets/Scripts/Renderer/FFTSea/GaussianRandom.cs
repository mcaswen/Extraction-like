using UnityEngine;

/// <summary>高斯随机数（Box–Muller），供 Phillips 频谱海面等使用。</summary>
public static class GaussianRandom
{
    public static float Next01() => Random.value;

    /// <summary>标准正态分布 N(0,1)。</summary>
    public static float StandardNormal()
    {
        float u1 = Mathf.Max(1e-7f, Random.value);
        float u2 = Random.value;
        return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
    }

    /// <summary>一次 Box–Muller 生成两个独立 N(0,1)。</summary>
    public static void BoxMuller(out float z0, out float z1)
    {
        float u1 = Mathf.Max(1e-7f, Random.value);
        float u2 = Random.value;
        float r = Mathf.Sqrt(-2f * Mathf.Log(u1));
        float theta = 2f * Mathf.PI * u2;
        z0 = r * Mathf.Cos(theta);
        z1 = r * Mathf.Sin(theta);
    }
}
