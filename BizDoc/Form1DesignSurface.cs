namespace BizDoc;

public sealed class Form1DesignSurface : UserControl
{
    private Form1? preview;

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        if (preview is not null) return;

        preview = new Form1(designPreview: true)
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill
        };
        Controls.Add(preview);
        preview.Show();
    }
}
