using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Cudafy;
using Cudafy.Host;
using Cudafy.Translator;
using System.Windows.Forms;
using Mono.CSharp;

namespace FSMNet
{
    //[Cudafy]
    //[StructLayout(LayoutKind.Sequential, Pack = 1)]
    //public struct ChunkData
    //{
    //    public byte valid; // struct is populated
    //    public byte generated; // voxel gen occured
    //    public byte empty; // only air voxels
    //    public byte size; // cubic side length

    //    public int chunkX; // absolute world coordinates divided by chunk size, in other words chunk coordinates
    //    public int chunkY;
    //    public int chunkZ;

    //    public int voxelBlockIndex;

    //    [CudafyIgnore]
    //    public override string ToString()
    //    {
    //        return $"({chunkX}, {chunkY}, {chunkZ}) v={valid} g={generated} e={empty} size={size} voxelBlockIndex={voxelBlockIndex}";
    //    }
    //}

    public class GPUController
    {
        [Cudafy]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Vector3
        {
            public float x;
            public float y;
            public float z;
        }

        [Cudafy]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Camera
        {
            public float x;
            public float y;
            public float z;

            public float hRotation;
            public float vRotation;

            public float rightX;
            public float rightY;
            public float rightZ;
            public float upX;
            public float upY;
            public float upZ;
            public float forwardX;
            public float forwardY;
            public float forwardZ;
        }

        [Cudafy]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Color3
        {
            public float red;
            public float green;
            public float blue;
        }

        [Cudafy]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ChunkData
        {
            public byte valid; // struct is populated
            public byte generated; // voxel gen occured
            public byte empty; // only air voxels
            public byte size; // cubic side length

            public int chunkX; // absolute world coordinates divided by chunk size, in other words chunk coordinates
            public int chunkY;
            public int chunkZ;

            public int voxelDataIndex;
        }

        [Cudafy]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe public struct FSMUnit
        {
            public fixed int values[4];
        }

        public static FSMUnit[] units;
        public static FSMUnit[] dev_units;

        public static int totalCycleCount = 0;

        public static GPGPU gpu;

        public static byte[] imageBytes;
        public static byte[] dev_imageBytes;
        public static IntPtr imageBytesAddress;

        public static Camera[] camera = new Camera[2];
        public static Camera[] dev_camera;

        public static Color3[,,] voxelMap;
        public static Color3[,,] dev_voxelMap;

        public static Color3[,] pixelMap;
        public static Color3[,] dev_pixelMap;

        public static ChunkData[,,] chunkData;
        public static ChunkData[,,] dev_chunkData;

        public static Color3[] voxelData;
        public static Color3[] dev_voxelData;

        public static ChunkHashTable chunkHashTable = new ChunkHashTable(0.7f);

        public static PictureBox viewport;

        public static bool InitGPU(PictureBox passedViewport)
        {
            viewport = passedViewport;

            CudafyModes.Target = eGPUType.OpenCL; // To use OpenCL, change this enum
            CudafyModes.DeviceId = 0;
            CudafyTranslator.Language = CudafyModes.Target == eGPUType.OpenCL ? eLanguage.OpenCL : eLanguage.Cuda;

            CudafyModule km = null;

            try
            {
                int deviceCount = CudafyHost.GetDeviceCount(CudafyModes.Target);
                if (deviceCount == 0)
                {
                    Console.WriteLine("No suitable {0} devices found.", CudafyModes.Target);
                    return false;
                }

                gpu = CudafyHost.GetDevice(CudafyModes.Target, CudafyModes.DeviceId);
                Console.WriteLine("Device Name: {0}", gpu.GetDeviceProperties(false).Name);

                var result = gpu.GetDeviceProperties(true); // diagnostic data

                km = CudafyTranslator.Cudafy();
                gpu.LoadModule(km);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine(km.SourceCode);
                Debugger.Break();
                return false;
            }

            InitDevicePointers();

            return true;
        }

        public static void FreeGPU()
        {
            gpu.Synchronize();
            gpu.FreeAll(); // free the memory allocated on the GPU
        }

        public static void InitDevicePointers()
        {
            // GPU kernal inputs //

            Random rnd = new Random();

            voxelMap = new Color3[32, 32, 32];

            for (int x = 0; x < 32; x++)
            {
                for (int y = 0; y < 32; y++)
                {
                    for (int z = 0; z < 32; z++)
                    {
                        if (rnd.NextDouble() <- 0.03)
                        {
                            voxelMap[x, y, z].red = Math.Abs((float)x - 16) / 16;
                            voxelMap[x, y, z].green = Math.Abs((float)y - 16) / 16;
                            voxelMap[x, y, z].blue = Math.Abs((float)z - 16) / 16;
                        }
                        else
                        {
                            voxelMap[x, y, z].red = 0f;
                            voxelMap[x, y, z].blue = 0f;
                            voxelMap[x, y, z].green = 0f;
                        }

                        if (x == 0 || x == 31 || y == 0 || y == 31 || z == 0 || z == 31 )
                        {
                            voxelMap[x, y, z].red = Math.Abs((float)x - 16) / 16;
                            voxelMap[x, y, z].green = Math.Abs((float)y - 16) / 16;
                            voxelMap[x, y, z].blue = Math.Abs((float)z - 16) / 16;
                        }
                    }
                }
            }

            List<Color3> voxels = new List<Color3>();

            chunkData = new ChunkData[33, 33, 33];

            // init chunks
            for (int x = 0; x <= 32; x++)
            {
                for (int y = 0; y <= 32; y++)
                {
                    for (int z = 0; z <= 32; z++)
                    {
                        chunkData[x, y, z].empty = 1;
                        chunkData[x, y, z].valid = 1;
                        chunkData[x, y, z].generated = 1;
                        chunkData[x, y, z].size = 8;
                        chunkData[x, y, z].chunkX = x - 16;
                        chunkData[x, y, z].chunkY = y - 16;
                        chunkData[x, y, z].chunkZ = z - 16;
                        chunkData[x, y, z].voxelDataIndex = voxels.Count;

                        for (int vx = 0; vx < 8; vx++)
                        {
                            for (int vy = 0; vy < 8; vy++)
                            {
                                for (int vz = 0; vz < 8; vz++)
                                {
                                    Color3 newVoxel = new Color3();

                                    if (rnd.NextDouble() < 0.02)
                                    {
                                        newVoxel.red = (float)rnd.NextDouble() * 0.7f + 0.3f;
                                        newVoxel.green = (float)rnd.NextDouble() * 0.7f + 0.3f;
                                        newVoxel.blue = (float)rnd.NextDouble() * 0.7f + 0.3f;
                                        chunkData[x, y, z].empty = 0;
                                    }
                                    else
                                    {
                                        // 
                                    }

                                    voxels.Add(newVoxel);
                                }
                            }
                        }

                        chunkHashTable.Insert(x, y, z, chunkData[x, y, z]);
                    }
                }
            }

            voxelData = voxels.ToArray();

            pixelMap = new Color3[viewport.ClientSize.Width, viewport.ClientSize.Height];
            imageBytes = new byte[pixelMap.GetLength(0) * pixelMap.GetLength(1) * 4];

            camera[0].x = 0f;
            camera[0].y = 0f;
            camera[0].z = 0f;

            camera[1].x = 0f;
            camera[1].y = 0f;
            camera[1].z = 0f;

            units = new FSMUnit[64];
            dev_units = gpu.Allocate(units);

            dev_voxelMap = gpu.Allocate(voxelMap);
            dev_pixelMap = gpu.Allocate(pixelMap);
            dev_camera = gpu.Allocate(camera);
            dev_imageBytes = gpu.Allocate(imageBytes);
            dev_chunkData = gpu.Allocate(chunkData);
            dev_voxelData = gpu.Allocate(voxelData);

            Stopwatch sw = new Stopwatch();
            sw.Start();

            gpu.CopyToDevice(voxelMap, dev_voxelMap);
            gpu.CopyToDevice(pixelMap, dev_pixelMap);
            gpu.CopyToDevice(camera, dev_camera);
            gpu.CopyToDevice(imageBytes, dev_imageBytes);
            gpu.CopyToDevice(chunkData, dev_chunkData);
            gpu.CopyToDevice(voxelData, dev_voxelData);

            GCHandle pinned = GCHandle.Alloc(imageBytes, GCHandleType.Pinned);
            imageBytesAddress = pinned.AddrOfPinnedObject();

            double t1 = sw.ElapsedMilliseconds;
        }

        public static void SetData()
        {
            // gpu.CopyToDevice(voxelMap, dev_voxelMap);

            gpu.CopyToDevice(units, dev_units);

            gpu.CopyToDevice(camera, dev_camera);
        }

        public static void GetData()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            gpu.CopyFromDevice(dev_units, units);

            //gpu.CopyFromDevice(dev_pixelMap, pixelMap);
            gpu.CopyFromDevice(dev_camera, camera);
            gpu.CopyFromDevice(dev_imageBytes, imageBytes);

            //gpu.CopyFromDeviceAsync(dev_imageBytes, 0, imageBytesAddress, 0, imageBytes.Length);

            //pinned.Free();

            double t2 = sw.ElapsedMilliseconds; // 8-9ms
        }

        public static void ExecuteKernal()
        {
            SetData();

            dim3 threadsPerBlock = new dim3(16, 16);
            dim3 numBlocks = new dim3(pixelMap.GetLength(0) / threadsPerBlock.x + 1, pixelMap.GetLength(1) / threadsPerBlock.y + 1);

            MultiStepTimer.MainTimer.Stamp("pre-kernal");

            Random random = new Random();

            PhaseTimer.Record("before LaunchAsync()");

            gpu.LaunchAsync(numBlocks, threadsPerBlock, 1, "RayTraceMain", dev_voxelMap, dev_pixelMap, dev_imageBytes, dev_camera, dev_chunkData, dev_voxelData, dev_units);
            // gpu.Launch(numBlocks, threadsPerBlock).RayTraceMain(dev_voxelMap, dev_pixelMap, dev_imageBytes, dev_camera, (float)random.NextDouble());

            PhaseTimer.Record("LaunchAsync()");

            // draw previous frame's pixels while new frame is calculating
            //MainForm.form.DrawPixels();

            PhaseTimer.Record("DrawPixels()");

            gpu.SynchronizeStream(1);
            //gpu.Synchronize();

            PhaseTimer.Record("SynchronizeStream()");

            MultiStepTimer.MainTimer.Stamp("post-kernal");

            GetData();

            PhaseTimer.Record("GetData()");

            MultiStepTimer.MainTimer.Stamp("post-pixel-copy");

            //camera[0].hRotation = (camera[0].hRotation + 0.12f) % 360;
            //camera[0].vRotation = (camera[0].vRotation + 0.1f) % 360;


            //camera[0].x += 0.001f;
            //Debugger.Break();
        }

        [Cudafy]
        public static float Clamp(float value, float min, float max)
        {
            if (value > max)
            {
                return max;
            }
            else if (value < min)
            {
                return min;
            }
            else
            {
                return value;
            }
        }

        [Cudafy]
        public static float degsin(float angle)
        {
            return GMath.Sin(angle / (180 / GMath.PI));
        }

        [Cudafy]
        public static float degcos(float angle)
        {
            return GMath.Cos(angle / (180 / GMath.PI));
        }

        [Cudafy]
        public static float degtan(float angle)
        {
            return GMath.Tan(angle / (180 / GMath.PI));
        }

        [Cudafy]
        public static float flabs(float value)
        {
            if (value < 0f)
            {
                return -value;
            }
            else
            {
                return value;
            }
        }

        [Cudafy]
        public static void RayTraceMain(GThread thread, Color3[,,] voxelMap, Color3[,] pixelMap, byte[] imageBytes, Camera[] camera, ChunkData[,,] chunkData, Color3[] voxelData, FSMUnit[] units)
        {
            // int threadIndex = thread.threadIdx.x + (thread.blockIdx.x * thread.blockDim.x);a
            // int strideLength = (thread.blockDim.x * thread.gridDim.x);
            
            int tx = thread.threadIdx.x + (thread.blockIdx.x * thread.blockDim.x);
            int ty = thread.threadIdx.y + (thread.blockIdx.y * thread.blockDim.y);

            if(tx > pixelMap.GetLength(0) || ty > pixelMap.GetLength(1))
            {
                return; // out of bounds, do no work
            }

            if (tx < 64 && ty == 0)
            {
                // test
                unsafe
                {
                    units[tx].values[0] = tx + 0;
                    units[tx].values[1] = tx + 1;
                    units[tx].values[2] = tx + 2;
                    units[tx].values[3] = tx + 3;
                }
            }

            // camera
            float hRot = camera[0].hRotation;
            float vRot = camera[0].vRotation;
            vRot = Clamp(vRot, -90.0f, 90.0f);

            float yaw = hRot;
            float pitch = vRot;
            float cosPitch = degcos(pitch);
            float sinPitch = degsin(pitch);
            float cosYaw = degcos(yaw);
            float sinYaw = degsin(yaw);

            camera[0].rightX = cosYaw;
            camera[0].rightY = 0f;
            camera[0].rightZ = -sinYaw;

            camera[0].upX = sinYaw * sinPitch;
            camera[0].upY = cosPitch;
            camera[0].upZ = cosYaw * sinPitch;

            camera[0].forwardX = sinYaw * cosPitch;
            camera[0].forwardY = -sinPitch;
            camera[0].forwardZ = cosPitch * cosYaw;

            // raster coordinates (0..1, 0..1)
            float px = ((float)(tx + 0.5f) / (float)pixelMap.GetLength(0));
            float py = ((float)(ty + 0.5f) / (float)pixelMap.GetLength(1));
            float ratio = (float)pixelMap.GetLength(0) / (float)pixelMap.GetLength(1); // should be > 1.0, normalized to Y-axis of screen

            float FOV = 90.0f;
            float halfFOV = FOV / 2f;

            // middle of screen is 0,0 in this frame
            px = (px - 0.5f) * 2; // normalize: -1...+1
            py = (py - 0.5f) * 2; // normalize: -1...+1

            float vx = px * degtan(halfFOV) * ratio;
            float vy = py * degtan(halfFOV);
            float vz = -1.0f;

            float vlength = GMath.Sqrt(vx * vx + vy * vy + vz * vz);  
            float norm_starting_x = vx / vlength;
            float norm_starting_y = vy / vlength;
            float norm_starting_z = vz / vlength;
            // normalized vector to rotate

            // normalized rotation axis
            float x = 0f;
            float y = 1f;
            float z = 0f;            

            float rho_deg = hRot;
            float c = degcos(rho_deg);
            float s = degsin(rho_deg);
            float t = (1 - degcos(rho_deg));

            float norm_final_x = norm_starting_x * (t * x * x + c) + norm_starting_y * (t * x * y - s * z) + norm_starting_z * (t * x * z + s * y);
            float norm_final_y = norm_starting_x * (t * x * y + s * z) + norm_starting_y * (t * y * y + c) + norm_starting_z * (t * y * z - s * x);
            float norm_final_z = norm_starting_x * (t * x * z - s * y) + norm_starting_y * (t * y * z + s * x) + norm_starting_z * (t * z * z + c);

            norm_starting_x = norm_final_x;
            norm_starting_y = norm_final_y;
            norm_starting_z = norm_final_z;

            // rotate relative to NEW local 'right' vector
            x = camera[0].rightX;
            y = camera[0].rightY;
            z = camera[0].rightZ;

            rho_deg = vRot; // rot_angle;
            c = degcos(rho_deg);
            s = degsin(rho_deg);
            t = (1 - degcos(rho_deg));

            norm_final_x = norm_starting_x * (t * x * x + c) + norm_starting_y * (t * x * y - s * z) + norm_starting_z * (t * x * z + s * y);
            norm_final_y = norm_starting_x * (t * x * y + s * z) + norm_starting_y * (t * y * y + c) + norm_starting_z * (t * y * z - s * x);
            norm_final_z = norm_starting_x * (t * x * z - s * y) + norm_starting_y * (t * y * z + s * x) + norm_starting_z * (t * z * z + c);

            vx = norm_final_x;
            vy = norm_final_y;
            vz = norm_final_z;

            // normalize
            //vlength = GMath.Sqrt(vx * vx + vy * vy + vz * vz);
            //vx /= vlength;
            //vy /= vlength;
            //vz /= vlength;

            float rayx = camera[0].x;
            float rayy = camera[0].y;
            float rayz = camera[0].z;

            float red = 0f;
            float green = 0f;
            float blue = 0f;

            float maxDistance = 64.0f * 2f;
            float currentDistance = 0.0f;

            float rayStartX = camera[0].x;
            float rayStartY = camera[0].y;
            float rayStartZ = camera[0].z;

            float rayEndX = rayStartX + vx * maxDistance;
            float rayEndY = rayStartY + vy * maxDistance;
            float rayEndZ = rayStartZ + vz * maxDistance;

            // Bresenham3D algorithm
            if(false)
            {
                float x1 = GMath.Floor(rayStartX);
                float x2 = GMath.Floor(rayEndX);
                float y1 = GMath.Floor(rayStartY);
                float y2 = GMath.Floor(rayEndY);
                float z1 = GMath.Floor(rayStartZ);
                float z2 = GMath.Floor(rayEndZ);

                float dx = flabs(x2 - x1);
                float dy = flabs(y2 - y1);
                float dz = flabs(z2 - z1);

                float xs = 0;
                float ys = 0;
                float zs = 0;

                if (x2 > x1) xs = 1;
                else xs = -1;

                if (y2 > y1) ys = 1;
                else ys = -1;

                if (z2 > z1) zs = 1;
                else zs = -1;

                float p1 = 0;
                float p2 = 0;

                // Driving axis is X-axis
                if (dx >= dy && dx >= dz)
                {
                    p1 = 2 * dy - dx;
                    p2 = 2 * dz - dx;

                    while (x1 != x2)
                    {
                        x1 += xs;

                        if (p1 >= 0)
                        {
                            y1 += ys;
                            p1 -= 2 * dx;
                        }

                        if (p2 >= 0)
                        {
                            z1 += zs;
                            p2 -= 2 * dx;
                        }

                        p1 += 2 * dy;
                        p2 += 2 * dz;

                        // ListOfPoints.append((x1, y1, z1))
                        // check voxel x1, y1, z1
                        // chunkData = new ChunkData[17, 17, 17]; // -8 to +8, and 0, adding +8 offset; chunk size = 8 voxels (512 blocks)

                        unsafe
                        {
                            int rawX = (int)(GMath.Floor(x1));
                            int rawY = (int)(GMath.Floor(y1));
                            int rawZ = (int)(GMath.Floor(z1));

                        }
                    }
                }
                // Driving axis is Y-axis 
                else if (dy >= dx && dy >= dz)
                {
                    p1 = 2 * dx - dy;
                    p2 = 2 * dz - dy;

                    while (y1 != y2)
                    {
                        y1 += ys;

                        if (p1 >= 0)
                        {
                            x1 += xs;
                            p1 -= 2 * dy;
                        }

                        if (p2 >= 0)
                        {
                            z1 += zs;
                            p2 -= 2 * dy;
                        }

                        p1 += 2 * dx;
                        p2 += 2 * dz;

                        //ListOfPoints.append((x1, y1, z1))
                        // check voxel x1, y1, z1

                        unsafe
                        {
                            int rawX = (int)(GMath.Floor(x1));
                            int rawY = (int)(GMath.Floor(y1));
                            int rawZ = (int)(GMath.Floor(z1));

                        }
                    }
                }
                // Driving axis is Z-axis
                else
                {
                    p1 = 2 * dy - dz;
                    p2 = 2 * dx - dz;

                    while (z1 != z2)
                    {
                        z1 += zs;

                        if (p1 >= 0)
                        {
                            y1 += ys;
                            p1 -= 2 * dz;
                        }

                        if (p2 >= 0)
                        {
                            x1 += xs;
                            p2 -= 2 * dz;
                        }

                        p1 += 2 * dy;
                        p2 += 2 * dx;

                        //ListOfPoints.append((x1, y1, z1))
                        // check voxel x1, y1, z1

                        unsafe
                        {
                            int rawX = (int)(GMath.Floor(x1));
                            int rawY = (int)(GMath.Floor(y1));
                            int rawZ = (int)(GMath.Floor(z1));

                        }
                    }
                }
            }

            if (true)
            {
                // ray cast
                while (currentDistance < maxDistance)
                {
                    // voxel map is 0...31 index
                    int voxelx = (int)GMath.Floor(rayx);
                    int voxely = (int)GMath.Floor(rayy);
                    int voxelz = (int)GMath.Floor(rayz);

                    int chunkX = voxelx / 8 + 16; // +16 in the array dimension
                    int chunkY = voxely / 8 + 16;
                    int chunkZ = voxelz / 8 + 16;

                    int chunkInternalX = voxelx & 7; // 0,0,0 to 7,7,7
                    int chunkInternalY = voxely & 7;
                    int chunkInternalZ = voxelz & 7;

                    if (chunkX < 0 || chunkX > 32 || chunkY < 0 || chunkY > 32 || chunkZ < 0 || chunkZ > 32)
                    {
                        // out of camera bounds, ignore
                    }
                    else
                    {
                        if (chunkData[chunkX, chunkY, chunkZ].empty == 0)
                        {
                            int index = chunkData[chunkX, chunkY, chunkZ].voxelDataIndex;
                            int adjIndex = index + (chunkInternalX + chunkInternalY * 8 + chunkInternalZ * 64);

                            if (voxelData[adjIndex].red > 0f || voxelData[adjIndex].green > 0f || voxelData[adjIndex].blue > 0f)
                            {
                                red = voxelData[adjIndex].red;
                                green = voxelData[adjIndex].green;
                                blue = voxelData[adjIndex].blue;
                                break;
                            }
                        }
                    }

                    // inner x/y/z of cube volume
                    float ix = rayx - GMath.Floor(rayx);
                    float iy = rayy - GMath.Floor(rayy);
                    float iz = rayz - GMath.Floor(rayz);

                    // get dist remaining in cube axis
                    if (vx > 0) { ix = 1f - ix; }
                    if (vy > 0) { iy = 1f - iy; }
                    if (vz > 0) { iz = 1f - iz; }

                    ix = flabs(ix / vx);
                    iy = flabs(iy / vy);
                    iz = flabs(iz / vz);

                    float nextDistance = GMath.Min(iz, GMath.Min(ix, iy)) + 0.01f; // step just over boundary

                    rayx += vx * nextDistance;
                    rayy += vy * nextDistance;
                    rayz += vz * nextDistance;

                    currentDistance += nextDistance; // add step length
                }
            }

            // render a pixel to buffer
            int imageByteIndex = (tx + (ty * pixelMap.GetLength(0))) * 4;
            imageBytes[imageByteIndex + 0] = (byte)(blue * 255f); // blue
            imageBytes[imageByteIndex + 1] = (byte)(green * 255f); // green
            imageBytes[imageByteIndex + 2] = (byte)(red * 255f); // red
            imageBytes[imageByteIndex + 3] = 255; // alpha channel

            int L = pixelMap.GetLength(0);

            pixelMap[tx, ty].red = vx;
            pixelMap[tx, ty].green = vy;
            pixelMap[tx, ty].blue = vz;

            return;

            pixelMap[tx, ty].red = 1.0f * ((float)tx / (float)pixelMap.GetLength(0));
            pixelMap[tx, ty].blue = 1.0f * ((float)ty / (float)pixelMap.GetLength(1));

            imageByteIndex = (tx + (ty * pixelMap.GetLength(0))) * 4;
            imageBytes[imageByteIndex + 0] = (byte)(pixelMap[tx, ty].red * 255); // + (rnd * 255)); test dynamic
            imageBytes[imageByteIndex + 1] = (byte)(pixelMap[tx, ty].green * 255);
            imageBytes[imageByteIndex + 2] = (byte)(pixelMap[tx, ty].blue * 255);
            imageBytes[imageByteIndex + 3] = 255; // alpha channel

            thread.SyncThreads();

            return;
        }
    }

    public class MultiStepTimer
    {
        public static MultiStepTimer MainTimer = new MultiStepTimer();

        public List<double> timestamps = new List<double>();
        public List<string> timestampNames = new List<string>();
        public Stopwatch sw = new Stopwatch();

        public void Start()
        {
            sw.Start();
            // timestamps.Add((double)sw.ElapsedTicks / (double)Stopwatch.Frequency);
        }

        public void Stamp(string name = "")
        {
            timestamps.Add((double)sw.ElapsedTicks / (double)Stopwatch.Frequency);
            timestampNames.Add(name);
        }

        public void Reset()
        {
            sw.Reset();
            timestamps.Clear();
            timestampNames.Clear();
        }

        public override string ToString()
        {
            string text = "";

            for (int n = 0; n < timestamps.Count; n++)
            {
                text += timestampNames[n] + " : " + (timestamps[n] * 1_000_000).ToString("N0") + " μs \n";
            }

            return text;
        }
    }

    public static class PhaseTimer
    {
        public static List<(string phaseName, double ms)> timeRecords = new List<(string, double)>();

        public static Stopwatch sw = new Stopwatch();

        public static void Start()
        {
            timeRecords.Clear();
            sw.Restart();
        }

        public static void Record(string phaseName)
        {
            double ms = ((double)sw.ElapsedTicks / (double)Stopwatch.Frequency) * 1000.0;
            timeRecords.Add((phaseName, ms));
            sw.Restart();
        }

        public static string Dump()
        {
            string text = "";

            foreach (var item in timeRecords)
            {
                text += item.phaseName + " : " + (int)(item.ms) + " ms;  ";
            }

            return text;
        }
    }
}
