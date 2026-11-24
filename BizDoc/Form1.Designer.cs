namespace BizDoc
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            designSurface = new Form1DesignSurface();
            SuspendLayout();
            // 
            // designSurface
            // 
            designSurface.Dock = DockStyle.Fill;
            designSurface.Location = new Point(0, 0);
            designSurface.Margin = new Padding(4, 4, 4, 4);
            designSurface.Name = "designSurface";
            designSurface.Size = new Size(2355, 1410);
            designSurface.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(144F, 144F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(2355, 1410);
            Controls.Add(designSurface);
            Margin = new Padding(4, 4, 4, 4);
            Name = "Form1";
            Text = "견적서·거래명세서 자동발행";
            ResumeLayout(false);
        }

        #endregion

        private Form1DesignSurface designSurface;
    }
}
