using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FSMNet
{
    public partial class MainForm : Form
    {
        public static MainForm form;

        Stopwatch stopwatch = new Stopwatch();
        Bitmap bitmap = null;
        
        public int counter = 0;
        public bool drawing = false;

        public MainForm()
        {
            form = this;
            InitializeComponent();
        }

        public (int, int) GetDisplaySize()
        {
            return (Viewport.Size.Width, Viewport.Size.Height);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.Show();
            GPU.InitGPU();

            GPU.Main2();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            GPU.FreeGPU();
        }

        private void GPUTimer_Tick(object sender, EventArgs e)
        {            
            return;

            (int sx, int sy) = MainForm.form.GetDisplaySize();

            Color3[,] pixelMap = new Color3[sx, sy];

            for (int px = 0; px < sx; px++)
            {
                for (int py = 0; py < sy; py++)
                {
                    pixelMap[px, py].red = (float)px / (float)sx;
                    pixelMap[px, py].blue = (float)py / (float)sy;
                }
            }

            // MainForm.form.DrawPixels(pixelMap);
        }

        //public void DrawPixels(Color3[,] pixelMap)
        
        public void DrawPixels(byte[] imageBytes, int sx, int sy)
        {
            if (bitmap == null)
            {
                bitmap = new Bitmap(sx, sy);
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();

            drawing = true;

            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, sx, sy), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            IntPtr bitmapPtr = bitmapData.Scan0;
            Marshal.Copy(imageBytes, 0, bitmapPtr, imageBytes.Length);
            bitmap.UnlockBits(bitmapData);
            Viewport.Image = bitmap;

            sw.Stop();
            drawing = false;

            counter++;

            if (counter % 10 == 0)
            {
                double ms = (double)sw.ElapsedTicks / (double)Stopwatch.Frequency * 1000.0;
                //this.Text = ms.ToString();
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }

            if (e.KeyCode == Keys.Up)
            {
                //
            }
            else if (e.KeyCode == Keys.Down)
            {
                //
            }
            else if (e.KeyCode == Keys.Left)
            {
                //
            }
            else if (e.KeyCode == Keys.Right)
            {
                //
            }

            if (e.KeyCode == Keys.W)
            {
            }
            else if (e.KeyCode == Keys.S)
            {
            }
            else if (e.KeyCode == Keys.A)
            {
            }
            else if (e.KeyCode == Keys.D)
            {
            }
        }

        private void MainForm_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            e.IsInputKey = true;
        }
    }
}
