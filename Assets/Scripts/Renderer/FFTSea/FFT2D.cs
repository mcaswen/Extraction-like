using UnityEngine;

namespace TA.OceanFFT
{
    /// <summary>
    /// 复数与基-2 Cooley–Tukey FFT（用于 Tessendorf 海面频域 IFFT）。
    /// </summary>
    public struct ComplexF
    {
        public float Re;
        public float Im;

        public ComplexF(float re, float im)
        {
            Re = re;
            Im = im;
        }

        public static ComplexF Zero => new ComplexF(0f, 0f);

        public static ComplexF operator +(ComplexF a, ComplexF b) =>
            new ComplexF(a.Re + b.Re, a.Im + b.Im);

        public static ComplexF operator -(ComplexF a, ComplexF b) =>
            new ComplexF(a.Re - b.Re, a.Im - b.Im);

        public static ComplexF operator *(ComplexF a, ComplexF b) =>
            new ComplexF(a.Re * b.Re - a.Im * b.Im, a.Re * b.Im + a.Im * b.Re);

        public static ComplexF operator *(ComplexF a, float s) =>
            new ComplexF(a.Re * s, a.Im * s);

        public static ComplexF Conjugate(ComplexF a) => new ComplexF(a.Re, -a.Im);

        public static ComplexF ExpImag(float phase) =>
            new ComplexF(Mathf.Cos(phase), Mathf.Sin(phase));
    }

    public static class FFT2D
    {
        /// <summary>一维 FFT：inverse=false 为正向；inverse=true 为逆变换并乘以 1/N。</summary>
        public static void FFT1D(ComplexF[] data, bool inverse)
        {
            int n = data.Length;
            int j = 0;
            for (int i = 1; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                    j &= ~bit;
                j |= bit;
                if (i < j)
                    (data[i], data[j]) = (data[j], data[i]);
            }

            float sign = inverse ? 1f : -1f;
            for (int len = 2; len <= n; len <<= 1)
            {
                float ang = 2f * Mathf.PI / len * sign;
                var wlen = new ComplexF(Mathf.Cos(ang), Mathf.Sin(ang));

                for (int i = 0; i < n; i += len)
                {
                    var w = new ComplexF(1f, 0f);
                    for (int k = 0; k < len / 2; k++)
                    {
                        int idx1 = i + k;
                        int idx2 = idx1 + len / 2;

                        var u = data[idx1];
                        var v = data[idx2] * w;
                        data[idx1] = u + v;
                        data[idx2] = u - v;
                        w = w * wlen;
                    }
                }
            }

            if (inverse)
            {
                float invN = 1f / n;
                for (int i = 0; i < n; i++)
                    data[i] = data[i] * invN;
            }
        }

        /// <summary>二维复数 IFFT：先对行再对列做逆 FFT（与 Tessendorf 海面一致）。</summary>
        public static void IFFT2D(ComplexF[,] field)
        {
            int n = field.GetLength(0);
            if (field.GetLength(1) != n)
                throw new System.ArgumentException("IFFT2D 需要方阵。");

            var row = new ComplexF[n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    row[j] = field[i, j];
                FFT1D(row, true);
                for (int j = 0; j < n; j++)
                    field[i, j] = row[j];
            }

            var col = new ComplexF[n];
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                    col[i] = field[i, j];
                FFT1D(col, true);
                for (int i = 0; i < n; i++)
                    field[i, j] = col[i];
            }
        }
    }
}
