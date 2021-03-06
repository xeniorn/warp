﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Accord.Math.Optimization;
using Warp.Tools;

namespace Warp
{
    /// <summary>
    /// Implements Piecewise Cubic Hermite Interpolation exactly as in Matlab.
    /// </summary>
    public class Cubic1D
    {
        public readonly float2[] Data;
        readonly float[] Breaks;
        readonly float4[] Coefficients;

        public Cubic1D(float2[] data)
        {
            // Sort points to go strictly from left to right.
            List<float2> DataList = data.ToList();
            DataList.Sort((p1, p2) => p1.X.CompareTo(p2.X));
            data = DataList.ToArray();

            Data = data;
            Breaks = data.Select(i => i.X).ToArray();
            Coefficients = new float4[data.Length - 1];

            float[] h = MathHelper.Diff(data.Select(i => i.X).ToArray());
            float[] del = MathHelper.Div(MathHelper.Diff(data.Select(i => i.Y).ToArray()), h);
            float[] slopes = GetPCHIPSlopes(data, del);

            float[] dzzdx = new float[del.Length];
            for (int i = 0; i < dzzdx.Length; i++)
                dzzdx[i] = (del[i] - slopes[i]) / h[i];

            float[] dzdxdx = new float[del.Length];
            for (int i = 0; i < dzdxdx.Length; i++)
                dzdxdx[i] = (slopes[i + 1] - del[i]) / h[i];

            for (int i = 0; i < Coefficients.Length; i++)
                Coefficients[i] = new float4((dzdxdx[i] - dzzdx[i]) / h[i],
                                             2f * dzzdx[i] - dzdxdx[i],
                                             slopes[i],
                                             data[i].Y);
        }

        public float[] Interp(float[] x)
        {
            float[] y = new float[x.Length];

            float[] b = Breaks;
            float4[] c = Coefficients;

            int[] indices = new int[x.Length];
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] < b[1])
                    indices[i] = 0;
                else if (x[i] >= b[b.Length - 2])
                    indices[i] = b.Length - 2;
                else
                    for (int j = 2; j < b.Length - 1; j++)
                        if (x[i] < b[j])
                        {
                            indices[i] = j - 1;
                            break;
                        }
            }

            float[] xs = new float[x.Length];
            for (int i = 0; i < xs.Length; i++)
                xs[i] = x[i] - b[indices[i]];

            for (int i = 0; i < x.Length; i++)
            {
                int index = indices[i];
                float v = c[index].X;
                v = xs[i] * v + c[index].Y;
                v = xs[i] * v + c[index].Z;
                v = xs[i] * v + c[index].W;

                y[i] = v;
            }

            return y;
        }

        public float Interp(float x)
        {
            float[] b = Breaks;
            float4[] c = Coefficients;

            int index = 0;

            if (x < b[1])
                index = 0;
            else if (x >= b[b.Length - 2])
                index = b.Length - 2;
            else
                for (int j = 2; j < b.Length - 1; j++)
                    if (x < b[j])
                    {
                        index = j - 1;
                        break;
                    }

            float xs = x - b[index];
            
            float v = c[index].X;
            v = xs * v + c[index].Y;
            v = xs * v + c[index].Z;
            v = xs * v + c[index].W;

            float y = v;

            return y;
        }

        private static float[] GetPCHIPSlopes(float2[] data, float[] del)
        {
            if (data.Length == 2)
                return new[] { del[0], del[0] };   // Do only linear

            float[] d = new float[data.Length];
            float[] h = MathHelper.Diff(data.Select(i => i.X).ToArray());
            for (int k = 0; k < del.Length - 1; k++)
            {
                if (del[k] * del[k + 1] <= 0f)
                    continue;

                float hs = h[k] + h[k + 1];
                float w1 = (h[k] + hs) / (3f * hs);
                float w2 = (hs + h[k + 1]) / (3f * hs);
                float dmax = Math.Max(Math.Abs(del[k]), Math.Abs(del[k + 1]));
                float dmin = Math.Min(Math.Abs(del[k]), Math.Abs(del[k + 1]));
                d[k + 1] = dmin / (w1 * (del[k] / dmax) + w2 * (del[k + 1] / dmax));
            }

            d[0] = ((2f * h[0] + h[1]) * del[0] - h[0] * del[1]) / (h[0] + h[1]);
            if (Math.Sign(d[0]) != Math.Sign(del[0]))
                d[0] = 0;
            else if (Math.Sign(del[0]) != Math.Sign(del[1]) && Math.Abs(d[0]) > Math.Abs(3f * del[0]))
                d[0] = 3f * del[0];

            int n = d.Length - 1;
            d[n] = ((2 * h[n - 1] + h[n - 2]) * del[n - 1] - h[n - 1] * del[n - 2]) / (h[n - 1] + h[n - 2]);
            if (Math.Sign(d[n]) != Math.Sign(del[n - 1]))
                d[n] = 0;
            else if (Math.Sign(del[n - 1]) != Math.Sign(del[n - 2]) && Math.Abs(d[n]) > Math.Abs(3f * del[n - 1]))
                d[n] = 3f * del[n - 1];

            return d;
        }

        public static Cubic1D Fit(float2[] data, int numnodes)
        {
            List<float2> Fitted = new List<float2>();

            int Extent = Math.Max(data.Length / (numnodes - 1) / 2, 1);
            for (int n = 0; n < numnodes; n++)
            {
                float Sum = 0;
                int Samples = 0;
                int Middle = Math.Min(data.Length - 1, n * (data.Length / (numnodes - 1)));
                int Start = Math.Max(0, Middle - Extent);
                int Finish = Math.Min(data.Length, Middle + Extent);

                for (int i = Start; i < Finish; i++, Samples++)
                    Sum += data[i].Y;

                Fitted.Add(new float2(data[Middle].X, Sum / Samples));
            }

            return new Cubic1D(Fitted.ToArray());

            /*float MinX = MathHelper.Min(data.Select(p => p.X)), MaxX = MathHelper.Max(data.Select(p => p.X)), ScaleX = 1f / (MaxX - MinX);
            float MinY = MathHelper.Min(data.Select(p => p.Y)), MaxY = MathHelper.Max(data.Select(p => p.Y)), ScaleY = 1f / (MaxY - MinY);
            if (float.IsNaN(ScaleY))
                ScaleY = 1f;

            float2[] ScaledData = data.Select(p => new float2((p.X - MinX) * ScaleX, (p.Y - MinY) * ScaleY)).ToArray();

            double[] Start = new double[numnodes];
            double[] NodeX = new double[numnodes];
            for (int i = 0; i < numnodes; i++)
            {
                NodeX[i] = Math.Pow(i, 0.75) / Math.Pow(numnodes - 1, 0.75) * (MaxX - MinX) * ScaleX;
                Start[i] = ScaledData[(int)(Math.Pow(i, 0.75) / Math.Pow(numnodes - 1, 0.75) * (data.Length - 1))].Y;
                //NodeX[i] = (double)i / (numnodes - 1) * (MaxX - MinX) * ScaleX;
                //Start[i] = ScaledData[(int)((double)i / (numnodes - 1) * (data.Length - 1))].Y;
            }

            float[] DataX = ScaledData.Select(p => p.X).ToArray();

            Func<double[], double> Eval = input =>
            {
                float2[] Nodes = new float2[numnodes];
                for (int i = 0; i < numnodes; i++)
                    Nodes[i] = new float2((float)NodeX[i], (float)input[i]);
                Cubic1D Splines = new Cubic1D(Nodes);
                float[] Interpolated = Splines.Interp(DataX);

                float Sum = 0f;
                for (int i = 0; i < ScaledData.Length; i++)
                {
                    float Diff = ScaledData[i].Y - Interpolated[i];
                    Sum += Diff * Diff;
                }

                return Math.Sqrt(Sum / data.Length) * 1000;
            };

            Func<double[], double[]> Gradient = input =>
            {
                double[] Result = new double[input.Length];

                for (int i = 0; i < input.Length; i++)
                {
                    double[] UpperInput = new double[input.Length];
                    input.CopyTo(UpperInput, 0);
                    UpperInput[i] += 0.01;
                    double UpperValue = Eval(UpperInput);

                    double[] LowerInput = new double[input.Length];
                    input.CopyTo(LowerInput, 0);
                    LowerInput[i] -= 0.01;
                    double LowerValue = Eval(LowerInput);

                    Result[i] = (UpperValue - LowerValue) / 0.02;
                }

                return Result;
            };

            BroydenFletcherGoldfarbShanno Optimizer = new BroydenFletcherGoldfarbShanno(Start.Length, Eval, Gradient);
            Optimizer.Minimize(Start);

            {
                float2[] Nodes = new float2[numnodes];
                for (int i = 0; i < numnodes; i++)
                    Nodes[i] = new float2((float)NodeX[i] / ScaleX + MinX, (float)Optimizer.Solution[i] / ScaleY + MinY);

                return new Cubic1D(Nodes);
            }*/
        }

        public static void FitCTF(float2[] data, Func<float[], float[]> approximation, float[] zeros, float[] peaks, out Cubic1D background, out Cubic1D scale)
        {
            float MinX = MathHelper.Min(data.Select(p => p.X)), MaxX = MathHelper.Max(data.Select(p => p.X)), ScaleX = 1f / (MaxX - MinX);
            float MinY = MathHelper.Min(data.Select(p => p.Y)), MaxY = MathHelper.Max(data.Select(p => p.Y)), ScaleY = 1f / (MaxY - MinY);
            if (float.IsNaN(ScaleY))
                ScaleY = 1f;

            peaks = peaks.Where(v => v >= MinX && v <= MaxX).Where((v, i) => i % 1 == 0).ToArray();
            zeros = zeros.Where(v => v >= MinX && v <= MaxX).Where((v, i) => i % 1 == 0).ToArray();

            float2[] ScaledData = data.Select(p => new float2((p.X - MinX) * ScaleX, (p.Y - MinY) * ScaleY)).ToArray();
            float StdY = MathHelper.StdDev(data.Select(p => p.Y).ToArray());

            double[] Start = new double[zeros.Length + peaks.Length];
            double[] NodeX = new double[zeros.Length + peaks.Length];
            Cubic1D DataSpline = new Cubic1D(data);
            for (int i = 0; i < zeros.Length; i++)
            {
                NodeX[i] = (zeros[i] - MinX) * ScaleX;
                Start[i] = DataSpline.Interp(zeros[i]) - MinY;
            }
            {
                Cubic1D PreliminaryBackground = new Cubic1D(Helper.Zip(NodeX.Take(zeros.Length).Select(v => (float)v).ToArray(),
                                                                       Start.Take(zeros.Length).Select(v => (float)v).ToArray()));
                float[] PreliminaryInterpolated = PreliminaryBackground.Interp(data.Select(v => (v.X - MinX) * ScaleX).ToArray());
                float2[] BackgroundSubtracted = data.Select((v, i) => new float2(v.X, v.Y - MinY - PreliminaryInterpolated[i])).ToArray();
                Cubic1D BackgroundSpline = new Cubic1D(BackgroundSubtracted);

                for (int i = 0; i < peaks.Length; i++)
                {
                    NodeX[i + zeros.Length] = (peaks[i] - MinX) * ScaleX;
                    float PeakValue = BackgroundSpline.Interp(peaks[i]);
                    Start[i + zeros.Length] = Math.Max(0.0001f, PeakValue);
                }
            }

            float[] DataX = ScaledData.Select(p => p.X).ToArray();
            float[] OriginalDataX = data.Select(p => p.X).ToArray();
            float[] SimulatedCTF = approximation(OriginalDataX);

            float2[] NodesBackground = new float2[zeros.Length];
            for (int i = 0; i < NodesBackground.Length; i++)
                NodesBackground[i] = new float2((float)NodeX[i], 0f);
            float2[] NodesScale = new float2[peaks.Length];
            for (int i = 0; i < NodesScale.Length; i++)
                NodesScale[i] = new float2((float)NodeX[i + zeros.Length], 0f);

            Func<double[], double> Eval = input =>
            {
                float2[] NodesBackgroundCopy = new float2[NodesBackground.Length];
                for (int i = 0; i < zeros.Length; i++)
                    NodesBackgroundCopy[i] = new float2(NodesBackground[i].X, (float)input[i]);

                float2[] NodesScaleCopy = new float2[NodesScale.Length];
                for (int i = 0; i < peaks.Length; i++)
                    NodesScaleCopy[i] = new float2(NodesScale[i].X, (float)input[i + zeros.Length]);

                float[] InterpolatedBackground = new Cubic1D(NodesBackgroundCopy).Interp(DataX);
                float[] InterpolatedScale = new Cubic1D(NodesScaleCopy).Interp(DataX);

                double Sum = 0f;
                for (int i = 0; i < ScaledData.Length; i++)
                {
                    double Diff = ScaledData[i].Y - (InterpolatedBackground[i] + SimulatedCTF[i] * (double)InterpolatedScale[i]) * ScaleY;
                    Sum += Diff * Diff;
                    if (InterpolatedScale[i] < 0.0005f)
                        Sum += (0.0005 - InterpolatedScale[i]) * 10;
                }

                //return Math.Sqrt(Sum / data.Length) * 10;
                return 0;
            };

            Func<double[], double[]> Gradient = input =>
            {
                double[] Result = new double[input.Length];

                //Parallel.For(0, input.Length, i =>
                for (int i = 0; i < input.Length; i++)
                {
                    double[] UpperInput = new double[input.Length];
                    input.CopyTo(UpperInput, 0);
                    UpperInput[i] += 0.0001;
                    double UpperValue = Eval(UpperInput);

                    double[] LowerInput = new double[input.Length];
                    input.CopyTo(LowerInput, 0);
                    LowerInput[i] -= 0.0001;
                    double LowerValue = Eval(LowerInput);

                    Result[i] = (UpperValue - LowerValue) / 0.0002;
                }//);

                return Result;
            };

            BroydenFletcherGoldfarbShanno Optimizer = new BroydenFletcherGoldfarbShanno(Start.Length, Eval, Gradient);
            Optimizer.Minimize(Start);

            {
                for (int i = 0; i < zeros.Length; i++)
                    NodesBackground[i] = new float2((float) NodeX[i] / ScaleX + MinX, (float) Optimizer.Solution[i] + MinY);
                for (int i = 0; i < peaks.Length; i++)
                    NodesScale[i] = new float2((float)NodeX[i + zeros.Length] / ScaleX + MinX, Math.Max(0.001f, (float)Optimizer.Solution[i + zeros.Length]));

                background = new Cubic1D(NodesBackground);
                scale = new Cubic1D(NodesScale);
            }
        }
    }
}
