using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Algorithms;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends;
using ILGPU.Backends.OpenCL;
using ILGPU.IR.Values;
using ILGPU.IR.Intrinsics;
using System.Security.Cryptography;

namespace FSMNet
{
    public struct Color3
    {
        public float red;
        public float green;
        public float blue;
    }

    public struct Neuron
    {
        public int value;
        public int newValue;
    }

    public static class GPU
    {
        public static byte[] imageBytes;
        public static Color3[,] pixelMap;
        public static Neuron[,] nrn;

        public static bool running = true;

        public static void InitGPU()
        {
            (int sx, int sy) = MainForm.form.GetDisplaySize(); 

            pixelMap = new Color3[sx, sy];
            imageBytes = new byte[sx * sy * 4];

            Random rnd = new Random();

            nrn = new Neuron[sx, sy];

            for (int x = 0; x < nrn.GetLength(0); x++)
            {
                for (int y = 0; y < nrn.GetLength(1); y++)
                {
                    nrn[x, y].value = rnd.Next(4); // 4 state network
                    nrn[x, y].newValue = 0;
                }
            }
        }

        public static void FreeGPU()
        {
            running = false;
        }

        public static void PixelKernel(Index2 index, ArrayView2D<Color3> pixelMap, ArrayView<byte> imageBytes, ArrayView2D<Neuron> nrn)
        {
            int tx = index.X;
            int ty = index.Y;

            int gridSizeX = (int)pixelMap.Extent.X;
            int gridSizeY = (int)pixelMap.Extent.Y;

            // ArrayView2D<float> mem = SharedMemory.Allocate2D<float>(new Index2(64, 64)); // test            

            if(tx > 0 && tx < gridSizeX - 1)
            {
                if (ty > 0 && ty < gridSizeY - 1)
                {
                    int sum0 = 0;
                    int sum1 = 0;
                    int sum2 = 0;
                    int sum3 = 0;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int otherState = nrn[tx + dx, ty + dy].value;

                            if (otherState == 0) { sum0++; }
                            if (otherState == 1) { sum1++; }
                            if (otherState == 2) { sum2++; }
                            if (otherState == 3) { sum3++; }
                        }
                    }

                    if (sum0 > 6) { nrn[tx, ty].newValue = 1; }
                    if (sum1 > 4) { nrn[tx, ty].newValue = 2; }
                    if (sum2 > 3) { nrn[tx, ty].newValue = 3; }
                    if (sum3 > 2) { nrn[tx, ty].newValue = 0; }
                }
            }

            Group.Barrier();

            nrn[tx, ty].value = nrn[tx, ty].newValue;

            if (tx == gridSizeX / 2 && ty == gridSizeY / 2)
            {
                // nrn[tx, ty].value = 0;
            }
            
            if(nrn[tx, ty].value == 0)
            {
                pixelMap[tx, ty].red = 1.0f;
                pixelMap[tx, ty].blue = 0.0f;
                pixelMap[tx, ty].green = 0.0f;
            }
            else if (nrn[tx, ty].value == 1)
            {
                pixelMap[tx, ty].red = 0.0f;
                pixelMap[tx, ty].blue = 1.0f;
                pixelMap[tx, ty].green = 0.0f;
            }
            else if (nrn[tx, ty].value == 2)
            {
                pixelMap[tx, ty].red = 0.0f;
                pixelMap[tx, ty].blue = 0.0f;
                pixelMap[tx, ty].green = 1.0f;
            }
            else if (nrn[tx, ty].value == 3)
            {
                pixelMap[tx, ty].red = 1.0f;
                pixelMap[tx, ty].blue = 1.0f;
                pixelMap[tx, ty].green = 0.0f;
            }

            pixelMap[tx, ty].red = XMath.Clamp(pixelMap[tx, ty].red, 0f, 1f);
            pixelMap[tx, ty].blue = XMath.Clamp(pixelMap[tx, ty].blue, 0f, 1f);
            pixelMap[tx, ty].green = XMath.Clamp(pixelMap[tx, ty].green, 0f, 1f);

            int i = (tx + (ty * gridSizeX)) * 4;
            imageBytes[i + 0] = (byte)(pixelMap[tx, ty].blue * 255f);  // blue
            imageBytes[i + 1] = (byte)(pixelMap[tx, ty].green * 255f); // green
            imageBytes[i + 2] = (byte)(pixelMap[tx, ty].red * 255f);   // red
            imageBytes[i + 3] = 255; // alpha channel
        }

        public static void Main2()
        {
            using (var context = new Context())
            {
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    if (acceleratorId.AcceleratorType == AcceleratorType.CPU)
                    {
                        continue;
                    }

                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        CompiledKernel compiledKernel;

                        using (Backend b = new CLBackend(context, ILGPU.Runtime.OpenCL.CLAcceleratorVendor.AMD))
                        {
                            MethodInfo methodInfo = typeof(GPU).GetMethod("PixelKernel");
                            KernelSpecialization spec = KernelSpecialization.Empty;
                            compiledKernel = b.Compile(EntryPointDescription.FromImplicitlyGroupedKernel(methodInfo), spec);
                            // debug: check kernel.Source for source text
                        }

                        var kernel = accelerator.LoadAutoGroupedKernel(compiledKernel);

                        // var kernel = accelerator.LoadAutoGroupedStreamKernel<Index2, ArrayView2D<FSMUnit>>(MathKernel);
                        // kernel = accelerator.LoadAutoGroupedStreamKernel<Index2, ArrayView2D<Color3>, ArrayView<byte>, ArrayView2D<Neuron>>(PixelKernel);

                        MemoryBuffer2D<Color3> buffer = accelerator.Allocate<Color3>(pixelMap.GetLength(0), pixelMap.GetLength(1));
                        MemoryBuffer<byte> buffer2 = accelerator.Allocate<byte>(imageBytes.Length);
                        MemoryBuffer2D<Neuron> buffer3 = accelerator.Allocate<Neuron>(nrn.GetLength(0), nrn.GetLength(1));

                        buffer3.CopyFrom(nrn, new LongIndex2(0, 0), new LongIndex2(0, 0), new LongIndex2(nrn.GetLength(0), nrn.GetLength(1)));

                        while(running == true)
                        {
                            Stopwatch sw = new Stopwatch();
                            sw.Start();

                            Index2 gridSize = new Index2(pixelMap.GetLength(0), pixelMap.GetLength(1));

                            //kernel(gridSize, buffer.View, buffer2.View, buffer3.View);

                            sw.OutputDelta("Kernel");

                            accelerator.Synchronize();

                            sw.OutputDelta("Sync");

                            // imageBytes = buffer2.GetAsArray();
                            buffer2.CopyTo(imageBytes, 0, 0, imageBytes.Length);

                            sw.OutputDelta("Copy ImageBytes");

                            // Resolve and verify data
                            //pixelMap = buffer.GetAs2DArray();                        
                            // buffer.CopyTo(pixelMap, new LongIndex2(0, 0), new LongIndex2(0, 0), new LongIndex2(pixelMap.GetLength(0), pixelMap.GetLength(1)));

                            // Color3[] pixelMap1D = buffer.GetAsArray();
                            //Copy1DTo2DArray(pixelMap1D, pixelMap); // ~36ms, a bit faster

                            //Array.Copy(pixelMap1D, imageBytes, pixelMap1D.Length); // fails                            
                            //Buffer.BlockCopy(pixelMap1D, 0, pixelMap, 0, pixelMap1D.Length * Marshal.SizeOf(typeof(Color3))); // fails
                            // pixelMap = Make2DArray(pixelMap1D, pixelMap.GetLength(0), pixelMap.GetLength(1)); // still slow

                            //sw.OutputDelta("Copy PixelMap");

                            // MainForm.form.DrawPixels(pixelMap);                            
                            MainForm.form.DrawPixels(imageBytes, pixelMap.GetLength(0), pixelMap.GetLength(1));
                            Application.DoEvents();

                            //Debugger.Break();

                            sw.OutputDelta("DrawPixels");
                        }

                        buffer.Dispose();
                        buffer2.Dispose();
                        buffer3.Dispose();

                        Application.Exit();

                        //Debugger.Break();
                    }
                }
            }
        }

        public static float EllapsedMs(this Stopwatch sw)
        {
            return (float)sw.ElapsedTicks / (float)Stopwatch.Frequency * 1000.0f;
        }

        public static void OutputDelta(this Stopwatch sw, string phaseText)
        {
            Debug.WriteLine(phaseText + " : " + ((float)sw.ElapsedTicks / (float)Stopwatch.Frequency * 1000.0f));
            sw.Restart();
        }

        public static T[,] Make2DArray<T>(T[] input, int height, int width)
        {
            T[,] output = new T[height, width];
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    output[i, j] = input[i * width + j];
                }
            }
            return output;
        }

        public static void Copy1DTo2DArray<T>(T[] input, T[,] output)
        {
            int height = output.GetLength(0);
            int width = output.GetLength(1);

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    output[i, j] = input[i * width + j];
                }
            }
        }
    }
}
