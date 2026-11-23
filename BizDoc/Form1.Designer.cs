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
            designSurface.Name = "designSurface";
            designSurface.TabIndex = 0;
            //
            // Form1
            //
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1570, 940);
            Controls.Add(designSurface);
            Name = "Form1";
            Text = "견적서·거래명세서 자동발행";
            ResumeLayout(false);
        }

        #endregion

        private Form1DesignSurface designSurface;
    }
}
