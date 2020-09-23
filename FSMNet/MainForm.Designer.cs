namespace FSMNet
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.GPUTimer = new System.Windows.Forms.Timer(this.components);
            this.Viewport = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.Viewport)).BeginInit();
            this.SuspendLayout();
            // 
            // GPUTimer
            // 
            this.GPUTimer.Enabled = true;
            this.GPUTimer.Interval = 1;
            this.GPUTimer.Tick += new System.EventHandler(this.GPUTimer_Tick);
            // 
            // Viewport
            // 
            this.Viewport.BackColor = System.Drawing.Color.Black;
            this.Viewport.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Viewport.Location = new System.Drawing.Point(0, 0);
            this.Viewport.Name = "Viewport";
            this.Viewport.Size = new System.Drawing.Size(869, 674);
            this.Viewport.TabIndex = 0;
            this.Viewport.TabStop = false;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(869, 674);
            this.Controls.Add(this.Viewport);
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.MainForm_KeyDown);
            this.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.MainForm_PreviewKeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.Viewport)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        public System.Windows.Forms.Timer GPUTimer;
        public System.Windows.Forms.PictureBox Viewport;
    }
}

