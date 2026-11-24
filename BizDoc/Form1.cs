using System.Drawing.Printing;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BizDoc;

public partial class Form1 : Form
{
#if SETTINGS_DISABLED
    private static bool SettingsEnabled => false;
#else
    private static bool SettingsEnabled => true;
#endif

    private static readonly Color Navy = Color.FromArgb(23, 43, 75);
    private static readonly Color Blue = Color.FromArgb(31, 112, 211);
    private static readonly Color Pale = Color.FromArgb(232, 240, 252);
    private static readonly string DataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BizDoc", "data.json");

    private readonly TextBox number = new();
    private readonly DateTimePicker date = new() { Format = DateTimePickerFormat.Short };
    private readonly DateTimePicker expires = new() { Format = DateTimePickerFormat.Short };
    private readonly TextBox owner = new();
    private readonly ComboBox customer = new() { AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly TextBox contact = new();
    private readonly TextBox phone = new();
    private readonly TextBox email = new();
    private readonly TextBox notes = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 93 };
    private readonly DataGridView items = MakeGrid(false);
    private readonly Label heading = new() { AutoSize = true, Font = new("맑은 고딕", 18, FontStyle.Bold), ForeColor = Navy };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 28, BackColor = Pale, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Paper preview;
    private readonly bool designPreview;
    private Store data = new();
    private DocumentKind kind;
    private Guid activeId;
    private bool loading;

    public Form1() : this(designPreview: false)
    {
    }

    internal Form1(bool designPreview)
    {
        this.designPreview = designPreview;
        if (!designPreview)
        {
            InitializeComponent();
            if (System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime)
            {
                preview = null!;
                return;
            }

            Controls.Remove(designSurface);
            designSurface.Dispose();
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(890, 610);
        }

        Font = new("맑은 고딕", 10);
        BackColor = Color.FromArgb(245, 247, 251);
        preview = new Paper(() => Snapshot(), () => CurrentSupplier);
        Build();
        if (designPreview)
        {
            NewDocument(DocumentKind.Quote, sample: true);
        }
        else
        {
            LoadStore();
            RefreshCustomers();
            NewDocument(DocumentKind.Quote, sample: data.Documents.Count == 0);
        }
    }

    private const float InterfaceZoom = 0.96f;

    protected override void OnLoad(EventArgs e)
    {
        if (!designPreview && !DesignMode)
            ZoomWindow(this);
        base.OnLoad(e);
    }

    private static void ZoomWindow(Form window)
    {
        var workingArea = Screen.FromControl(window).WorkingArea;
        var originalBounds = window.Bounds;
        window.SuspendLayout();
        try
        {
            window.Scale(new SizeF(InterfaceZoom, InterfaceZoom));
            ZoomFonts(window);
            window.MinimumSize = new Size(
                Math.Min(window.MinimumSize.Width, workingArea.Width),
                Math.Min(window.MinimumSize.Height, workingArea.Height));
            var width = Math.Min(window.Width, workingArea.Width);
            var height = Math.Min(window.Height, workingArea.Height);
            var left = Math.Clamp(originalBounds.Left + (originalBounds.Width - width) / 2,
                workingArea.Left, workingArea.Right - width);
            var top = Math.Clamp(originalBounds.Top + (originalBounds.Height - height) / 2,
                workingArea.Top, workingArea.Bottom - height);
            window.SetBounds(left, top, width, height);
        }
        finally
        {
            window.ResumeLayout(true);
        }
    }

    private static void ZoomFonts(Control control)
    {
        foreach (Control child in control.Controls)
            ZoomFonts(child);

        var fontProperty = System.ComponentModel.TypeDescriptor.GetProperties(control)[nameof(Font)];
        if (control is Form || fontProperty?.ShouldSerializeValue(control) == true)
        {
            var font = control.Font;
            var enlarged = new Font(font.FontFamily, font.Size * InterfaceZoom, font.Style,
                font.Unit, font.GdiCharSet, font.GdiVerticalFont);
            control.Font = enlarged;
            control.Disposed += (_, _) => enlarged.Dispose();
        }

    }

    private static void ConfigureGridHeight(DataGridView grid)
    {
        // Let text and the editing control fit at the current font and DPI.
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.DefaultCellStyle.Padding = new Padding(0, 4, 0, 4);
    }

    private sealed class ZoomDialog : Form
    {
        protected override void OnLoad(EventArgs e)
        {
            ZoomWindow(this);
            base.OnLoad(e);
        }
    }


    private Button quoteTab = null!;
    private Button statementTab = null!;
    private Button quoteMenu = null!;
    private Button statementMenu = null!;
    private Button? selectedMenu;

    private void Build()
    {
        var frame = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Margin = Padding.Empty };
        frame.ColumnStyles.Add(new(SizeType.Absolute, 302));
        frame.ColumnStyles.Add(new(SizeType.Percent, 100));
        frame.RowStyles.Add(new(SizeType.Absolute, 43));
        frame.RowStyles.Add(new(SizeType.AutoSize));
        frame.RowStyles.Add(new(SizeType.Percent, 100));
        frame.RowStyles.Add(new(SizeType.Absolute, 34));

        var brand = new Label
        {
            Text = "견적서·거래명세서 자동발행", Dock = DockStyle.Fill, Margin = Padding.Empty,
            Padding = new Padding(18, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(32, 55, 92), ForeColor = Color.White,
            Font = new("맑은 고딕", 13, FontStyle.Bold)
        };
        frame.Controls.Add(brand, 0, 0);
        frame.SetColumnSpan(brand, 2);

        var nav = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, Margin = Padding.Empty,
            BackColor = Navy, Padding = new Padding(10, 14, 10, 10), AutoScroll = true
        };
        nav.ColumnStyles.Add(new(SizeType.Percent, 100));
        Button? selectedNav = null;
        AddNav("홈", ShowHome, MenuIcons.Home);
        AddNav("문서 발행", () => number.Focus(), MenuIcons.Quote, selected: true);
        AddNav("문서 목록", ShowDocuments, MenuIcons.Documents);
        AddNav("거래처 관리", ManageCustomers, MenuIcons.Customers);
        AddNav("상품 관리", ManageProducts, MenuIcons.Product);
        AddNav("통계 / 현황", ShowStats, MenuIcons.Statistics);
        AddNav("환경 설정", EditSupplier, MenuIcons.Settings, enabled: SettingsEnabled);
        frame.Controls.Add(nav, 0, 1);
        frame.SetRowSpan(nav, 2);

        quoteMenu = MenuButton("견적서 발행", () => NewDocument(DocumentKind.Quote), MenuIcons.Quote, selectOnClick: false);
        statementMenu = MenuButton("거래명세서 발행", () => NewDocument(DocumentKind.Statement), MenuIcons.Statement, selectOnClick: false);
        var commands = new MenuToolbar
        {
            Dock = DockStyle.Top, AutoSize = true, Margin = Padding.Empty,
            Padding = new Padding(26, 12, 14, 12), BackColor = Color.FromArgb(242, 246, 252)
        };
        commands.Controls.AddRange([quoteMenu, statementMenu, MenuButton("문서목록", ShowDocuments, MenuIcons.Documents),
            MenuButton("거래처관리", ManageCustomers, MenuIcons.Customers), MenuButton("상품관리", ManageProducts, MenuIcons.Product),
            MenuButton("통계/현황", ShowStats, MenuIcons.Statistics), MenuButton("환경설정", EditSupplier, MenuIcons.Settings, enabled: SettingsEnabled)]);
        frame.Controls.Add(commands, 1, 1);

        var columns = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single, BackColor = Color.FromArgb(204, 215, 231)
        };
        columns.ColumnStyles.Add(new(SizeType.Percent, 48));
        columns.ColumnStyles.Add(new(SizeType.Percent, 52));
        columns.Controls.Add(BuildEditor(), 0, 0);

        var output = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty,
            BackColor = Color.FromArgb(244, 247, 252)
        };
        output.RowStyles.Add(new(SizeType.AutoSize));
        output.RowStyles.Add(new(SizeType.Percent, 100));
        var outputTools = Toolbar(Button("저장", SaveDocument, true), Button("미리보기", () => Print(true)), Button("인쇄 / PDF", () => Print(false)),
            Button("엑셀용 CSV", ExportCsv));
        outputTools.Padding = new Padding(12, 8, 8, 8);
        output.Controls.Add(outputTools, 0, 0);
        output.Controls.Add(preview, 0, 1);
        columns.Controls.Add(output, 1, 0);
        frame.Controls.Add(columns, 1, 2);

        status.Dock = DockStyle.Fill;
        status.Margin = Padding.Empty;
        frame.Controls.Add(status, 0, 3);
        frame.SetColumnSpan(status, 2);
        Controls.Add(frame);

        void AddNav(string label, Action action, Action<Graphics, Pen> drawIcon, bool selected = false, bool enabled = true)
        {
            var button = Button(label, null, true);
            button.Dock = DockStyle.Top;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.MinimumSize = new Size(0, 46);
            button.Margin = new Padding(0, 0, 0, 24);
            button.Padding = new Padding(12, 4, 8, 4);
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = Blue;
            button.Enabled = enabled;
            if (!enabled) button.ForeColor = Color.Silver;
            StyleNav(button, selected);
            if (selected) selectedNav = button;
            button.Click += (_, _) =>
            {
                if (selectedNav is not null) StyleNav(selectedNav, selected: false);
                selectedNav = button;
                StyleNav(button, selected: true);
                action();
            };
            AddMenuIcon(button, drawIcon);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            var row = nav.RowCount++;
            nav.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            nav.Controls.Add(button, 0, row);
        }

        static void StyleNav(Button button, bool selected)
        {
            button.BackColor = selected ? Blue : Navy;
            button.FlatAppearance.MouseOverBackColor = selected ? Blue : Color.FromArgb(40, 64, 101);
        }
    }

    private sealed class MenuToolbar : FlowLayoutPanel
    {
        public MenuToolbar()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            var gaps = Controls.Count - 1;
            if (gaps > 0)
            {
                var preferredSizes = Controls.Cast<Control>().Select(button => button.GetPreferredSize(Size.Empty)).ToArray();
                var gap = Math.Max(6, 6 * DeviceDpi / 96);
                var extraWidth = Math.Max(0, ClientSize.Width - Padding.Horizontal
                    - preferredSizes.Sum(size => size.Width) - gap * gaps);

                SuspendLayout();
                try
                {
                    for (var index = 0; index < Controls.Count; index++)
                    {
                        var button = Controls[index];
                        var margin = button.Margin;
                        var right = index == gaps ? 0 : gap;
                        var newMargin = new Padding(0, margin.Top, right, margin.Bottom);
                        if (margin != newMargin)
                            button.Margin = newMargin;
                        var preferred = preferredSizes[index];
                        var width = preferred.Width + extraWidth / Controls.Count
                            + (index < extraWidth % Controls.Count ? 1 : 0);
                        var size = new Size(width, preferred.Height);
                        if (button.Size != size)
                            button.Size = size;
                    }
                }
                finally
                {
                    ResumeLayout(false);
                }
            }
            base.OnLayout(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var divider = new Pen(Color.FromArgb(217, 225, 238));
            for (var index = 1; index < Controls.Count - 1; index++)
            {
                var left = Controls[index];
                var right = Controls[index + 1];
                if (left.Top != right.Top || right.Left <= left.Right) continue;
                var x = left.Right + (right.Left - left.Right) / 2;
                var inset = Math.Max(3, left.Height / 8);
                e.Graphics.DrawLine(divider, x, left.Top + inset, x, left.Bottom - inset);
            }
        }
    }

    private Button MenuButton(string label, Action action, Action<Graphics, Pen> drawIcon, bool selectOnClick = true, bool enabled = true)
    {
        var button = Button(label, null);
        button.AutoSize = false;
        const float menuTextScale = 1.5f;
        var menuFont = new Font(Font.FontFamily, Font.Size * menuTextScale, FontStyle.Bold, Font.Unit);
        button.Font = menuFont;
        button.Disposed += (_, _) => menuFont.Dispose();
        button.MinimumSize = new Size(0, 39);
        button.Padding = new Padding(6, 3, 6, 3);
        button.Click += (_, _) =>
        {
            if (selectOnClick) SelectMenu(button);
            action();
        };
        StyleMenu(button, selected: false);
        button.Enabled = enabled;
        if (!enabled) button.ForeColor = SystemColors.GrayText;
        AddMenuIcon(button, drawIcon);
        RoundMenuButton(button);
        return button;
    }

    private static void RoundMenuButton(Button button)
    {
        button.FlatAppearance.BorderSize = 0;
        button.SizeChanged += (_, _) => UpdateRegion();
        button.DpiChangedAfterParent += (_, _) => UpdateRegion();
        button.Paint += (_, e) =>
        {
            if (button.Width < 2 || button.Height < 2) return;
            using var outline = RoundedPath(button.Width - 1, button.Height - 1);
            using var pen = new Pen(button.FlatAppearance.BorderColor);
            var smoothing = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, outline);
            e.Graphics.SmoothingMode = smoothing;
        };
        UpdateRegion();

        void UpdateRegion()
        {
            if (button.Width < 2 || button.Height < 2) return;
            using var shape = RoundedPath(button.Width, button.Height);
            button.Region = new Region(shape);
        }

        System.Drawing.Drawing2D.GraphicsPath RoundedPath(int width, int height)
        {
            var diameter = Math.Min(Math.Min(width, height), 16f * button.DeviceDpi / 96);
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(0, 0, diameter, diameter, 180, 90);
            path.AddArc(width - diameter, 0, diameter, diameter, 270, 90);
            path.AddArc(width - diameter, height - diameter, diameter, diameter, 0, 90);
            path.AddArc(0, height - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private static void AddMenuIcon(Button button, Action<Graphics, Pen> drawIcon)
    {
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.ImageAlign = ContentAlignment.MiddleCenter;
        button.FontChanged += (_, _) => RefreshIcon();
        button.ForeColorChanged += (_, _) => RefreshIcon();
        button.DpiChangedAfterParent += (_, _) => RefreshIcon();
        button.Disposed += (_, _) => button.Image?.Dispose();
        RefreshIcon();

        void RefreshIcon()
        {
            var size = Math.Max(16, button.Font.Height);
            var icon = new Bitmap(size + size / 4, size);
            using (var graphics = Graphics.FromImage(icon))
            using (var pen = new Pen(button.ForeColor, 1.6f))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.ScaleTransform(size / 24f, size / 24f);
                pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                drawIcon(graphics, pen);
            }
            var previous = button.Image;
            button.Image = icon;
            previous?.Dispose();
        }
    }

    private void SelectMenu(Button button)
    {
        if (selectedMenu is not null)
            StyleMenu(selectedMenu, selected: false);
        selectedMenu = button;
        StyleMenu(button, selected: true);
    }

    private static void StyleMenu(Button button, bool selected)
    {
        button.BackColor = selected ? Blue : Color.Ivory;
        button.ForeColor = selected ? Color.White : Navy;
        button.FlatAppearance.BorderColor = selected ? Blue : Color.FromArgb(185, 200, 222);
        button.FlatAppearance.MouseOverBackColor = selected ? Blue : Color.FromArgb(205, 239, 166);
        button.FlatAppearance.MouseDownBackColor = Blue;
    }


    private static void StyleTab(Button button, bool selected)
    {
        button.BackColor = selected ? Color.FromArgb(39, 129, 226) : Color.FromArgb(240, 244, 249);
        button.ForeColor = selected ? Color.White : Navy;
        button.FlatAppearance.BorderColor = selected ? Blue : Color.FromArgb(185, 200, 222);
    }



    private Control BuildEditor()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16, 12, 15, 12), Margin = Padding.Empty, BackColor = Color.FromArgb(248, 250, 253) };
        var editor = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        editor.ColumnStyles.Add(new(SizeType.Percent, 100));
        var titleRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, RowCount = 1,
            Margin = Padding.Empty, Padding = new Padding(7, 3, 6, 5)
        };
        titleRow.ColumnStyles.Add(new(SizeType.AutoSize));
        titleRow.ColumnStyles.Add(new(SizeType.Percent, 100));
        titleRow.ColumnStyles.Add(new(SizeType.AutoSize));
        titleRow.ColumnStyles.Add(new(SizeType.AutoSize));
        var documentIcon = new Panel { Size = new Size(28, 28), Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) };
        documentIcon.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.ScaleTransform(documentIcon.ClientSize.Width / 24f, documentIcon.ClientSize.Height / 24f);
            using var pen = new Pen(Navy, 1.5f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
            MenuIcons.Quote(e.Graphics, pen);
        };
        titleRow.Controls.Add(documentIcon, 0, 0);
        heading.Anchor = AnchorStyles.Left;
        heading.Margin = new Padding(0, 0, 8, 0);
        titleRow.Controls.Add(heading, 1, 0);
        quoteTab = Button("견적서", () => { if (kind != DocumentKind.Quote) NewDocument(DocumentKind.Quote); });
        statementTab = Button("거래명세서", () => { if (kind != DocumentKind.Statement) NewDocument(DocumentKind.Statement); });
        foreach (var tab in new[] { quoteTab, statementTab })
        {
            tab.Dock = DockStyle.Fill;
            tab.MinimumSize = new Size(92, 33);
            tab.Margin = Padding.Empty;
            tab.Font = new("맑은 고딕", 10, FontStyle.Bold);
            RoundMenuButton(tab);
        }
        titleRow.Controls.Add(quoteTab, 2, 0);
        titleRow.Controls.Add(statementTab, 3, 0);
        editor.Controls.Add(titleRow);
        editor.Controls.Add(Section("기본 정보"));
        editor.Controls.Add(Fields(("문서번호", number), ("작성일자", date), ("유효기간", expires), ("담당자", owner)));
        editor.Controls.Add(Section("거래처 정보"));
        editor.Controls.Add(Fields(("거래처", customer), ("담당자", contact), ("연락처", phone), ("이메일", email)));
        var itemHeader = Toolbar(Button("+ 품목 추가", AddItem, true),
            Button("상품 선택", PickProduct), Button("복사", CopyItem), Button("삭제", DeleteItem),
            Button("↑", () => MoveItem(-1)), Button("↓", () => MoveItem(1)));
        itemHeader.Controls.Add(new Label { Text = "품목 정보", AutoSize = true, ForeColor = Navy, Margin = new Padding(7, 8, 10, 0) });
        itemHeader.Controls.SetChildIndex(itemHeader.Controls[itemHeader.Controls.Count - 1], 0);
        itemHeader.BackColor = Pale;
        itemHeader.Margin = new Padding(0, 10, 0, 4);
        editor.Controls.Add(itemHeader);
        items.Dock = DockStyle.Top;
        items.Height = 195;
        ConfigureGridHeight(items);
        items.GridColor = Color.FromArgb(190, 202, 221);
        items.RowHeadersVisible = true;
        items.RowHeadersWidth = 44;
        items.RowPostPaint += (_, e) =>
        {
            var bounds = new Rectangle(e.RowBounds.X, e.RowBounds.Y, items.RowHeadersWidth - 4, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(), items.Font, bounds, Navy,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        items.DefaultCellStyle.SelectionBackColor = Color.FromArgb(205, 228, 255);
        items.DefaultCellStyle.SelectionForeColor = Navy;
        items.AllowUserToAddRows = false;
        foreach (var name in new[] { "품목명", "규격/모델", "수량", "단가(원)", "금액(원)" })
            items.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, FillWeight = name == "수량" ? 48 : 100 });
        items.Columns[4].ReadOnly = true;
        for (var column = 2; column <= 4; column++)
            items.Columns[column].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        items.Columns[3].DefaultCellStyle.Format = "#,0.##";
        items.Columns[4].DefaultCellStyle.Format = "N0";
        items.CellValidating += (_, e) =>
        {
            if (e.ColumnIndex == 0 && items.IsCurrentCellDirty &&
                string.IsNullOrWhiteSpace(Convert.ToString(e.FormattedValue)))
            {
                e.Cancel = true;
                status.Text = "  품목명을 입력하세요.";
                return;
            }
            if (e.ColumnIndex is not (2 or 3)) return;
            if (TryNumber(Convert.ToString(e.FormattedValue), out decimal _)) return;
            e.Cancel = true;
            status.Text = "  수량과 단가는 0 이상 1조 이하의 숫자를 입력하세요.";
        };
        items.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex == 4 || loading) return;
            UpdateAmounts();
        };
        items.RowsAdded += (_, _) => UpdateAmounts();
        items.RowsRemoved += (_, _) => UpdateAmounts();
        editor.Controls.Add(items);
        editor.Controls.Add(Section("비고"));
        notes.Dock = DockStyle.Top;
        editor.Controls.Add(notes);
        foreach (var input in new Control[] { number, owner, customer, contact, phone, email, notes })
            input.TextChanged += (_, _) => preview.Invalidate();
        date.ValueChanged += (_, _) => preview.Invalidate();
        expires.ValueChanged += (_, _) => preview.Invalidate();
        customer.SelectionChangeCommitted += (_, _) =>
        {
            if (customer.SelectedItem is not string name) return;
            var match = data.Customers.FirstOrDefault(c => c.Name == name);
            if (match is null) return;
            contact.Text = match.Contact;
            phone.Text = match.Phone;
            email.Text = match.Email;
        };
        scroll.Controls.Add(editor);
        return scroll;
    }

    private static Label Section(string text) => new()
    {
        Text = "  " + text, Dock = DockStyle.Top, Height = 29, BackColor = Pale,
        ForeColor = Navy, TextAlign = ContentAlignment.MiddleLeft, Font = new("맑은 고딕", 10, FontStyle.Bold),
        Margin = new Padding(0, 8, 0, 4)
    };

    private static Button Button(string text, Action? action, bool primary = false)
    {
        var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 33), Padding = new(8, 3, 8, 3), FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Blue : Color.White, ForeColor = primary ? Color.White : Navy, Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = primary ? Blue : Color.FromArgb(185, 200, 222);
        if (action is not null) button.Click += (_, _) => action();
        return button;
    }

    private static FlowLayoutPanel Toolbar(params Control[] controls)
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new(3, 4, 3, 3) };
        bar.Controls.AddRange(controls);
        return bar;
    }

    private static TableLayoutPanel Fields(params (string Label, Control Input)[] fields)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
        panel.ColumnStyles.Add(new(SizeType.AutoSize));
        panel.ColumnStyles.Add(new(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new(SizeType.AutoSize));
        panel.ColumnStyles.Add(new(SizeType.Percent, 50));
        for (var i = 0; i < fields.Length; i++)
        {
            var (label, input) = fields[i];
            var column = i % 2 * 2;
            var row = i / 2;
            panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 5, 8, 5), Anchor = AnchorStyles.Left }, column, row);
            input.Dock = DockStyle.Fill;
            panel.Controls.Add(input, column + 1, row);
        }
        return panel;
    }

    private static DataGridView MakeGrid(bool readOnly) => new()
    {
        BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        ReadOnly = readOnly, AllowUserToResizeRows = false, RowTemplate = { Height = 29 },
        EnableHeadersVisualStyles = false,
        ColumnHeadersDefaultCellStyle = { BackColor = Pale, ForeColor = Navy, SelectionBackColor = Pale, SelectionForeColor = Navy },
        AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(246, 249, 255) }
    };

    private void NewDocument(DocumentKind type, bool sample = false)
    {
        if (!items.EndEdit()) return;
        if (!loading && number.Text.Length > 0 && HasChanges() &&
            MessageBox.Show(this, "저장하지 않은 변경 사항을 버리고 새 문서를 만들까요?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var today = DateTime.Today;
        var prefix = (type == DocumentKind.Quote ? "QT" : "ST") + $"-{today:yyyyMMdd}-";
        var sequence = 1;
        while (data.Documents.Any(d => d.Number == prefix + sequence.ToString("D4"))) sequence++;
        var document = new Document
        {
            Id = Guid.NewGuid(), Kind = type, Number = prefix + sequence.ToString("D4"),
            Date = today, Expires = today.AddDays(14), Owner = data.Supplier.Contact
        };
        if (sample)
        {
            document.Customer = "(주)대구테크";
            document.Contact = "김민수";
            document.Phone = "053-123-4567";
            document.Email = "minsu@example.com";
            document.Items = [new("산업용 PC", "IPC-5000", 2, 1500000),
                new("모니터 27인치", "DM-27F", 2, 280000), new("설치 및 세팅", "", 1, 200000)];
            document.Notes = "납기: 발주 후 7일 이내\n기타 문의사항은 담당자에게 연락 바랍니다.";
        }
        OpenDocument(document);
        status.Text = sample ? "  예시 견적서" : "  새 문서";
    }

    private string baseline = "";
    private Supplier? documentSupplier;
    private Supplier CurrentSupplier => documentSupplier ?? data.Supplier;
    private bool HasChanges() => JsonSerializer.Serialize(Snapshot()) != baseline;

    private void OpenDocument(Document document)
    {
        loading = true;
        items.CancelEdit();
        kind = document.Kind;
        activeId = document.Id;
        documentSupplier = document.Issuer;
        heading.Text = TitleOf(kind) + " 발행";
        StyleTab(quoteTab, kind == DocumentKind.Quote);
        StyleTab(statementTab, kind == DocumentKind.Statement);
        SelectMenu(kind == DocumentKind.Quote ? quoteMenu : statementMenu);
        expires.Enabled = kind == DocumentKind.Quote;
        number.Text = document.Number;
        date.Value = document.Date;
        expires.Value = document.Expires;
        owner.Text = document.Owner;
        customer.Text = document.Customer;
        contact.Text = document.Contact;
        phone.Text = document.Phone;
        email.Text = document.Email;
        notes.Text = document.Notes.ReplaceLineEndings(Environment.NewLine);
        items.Rows.Clear();
        foreach (var item in document.Items) items.Rows.Add(item.Name, item.Model, item.Quantity, item.Price, item.Amount);
        loading = false;
        UpdateAmounts();
        baseline = JsonSerializer.Serialize(Snapshot());
    }

    private Document Snapshot() => new()
    {
        Id = activeId, Kind = kind, Number = number.Text.Trim(), Date = date.Value.Date,
        Expires = expires.Value.Date, Owner = owner.Text, Customer = customer.Text,
        Contact = contact.Text, Phone = phone.Text, Email = email.Text, Notes = notes.Text,
        Items = items.Rows.Cast<DataGridViewRow>().Select(ReadItem).ToList()
    };

    private static bool TryNumber(string? text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) && value is >= 0 and <= 1_000_000_000_000m;

    private static Line ReadItem(DataGridViewRow row)
    {
        TryNumber(Convert.ToString(row.Cells[2].Value), out var quantity);
        TryNumber(Convert.ToString(row.Cells[3].Value), out var price);
        return new(Convert.ToString(row.Cells[0].Value) ?? "", Convert.ToString(row.Cells[1].Value) ?? "", quantity, price);
    }

    private void UpdateAmounts()
    {
        if (loading) return;
        foreach (DataGridViewRow row in items.Rows) row.Cells[4].Value = ReadItem(row).Amount;
        preview.Invalidate();
    }

    private void AddItem()
    {
        if (!items.EndEdit()) return;
        using var dialog = Dialog("품목 추가", 420, 150);
        var name = new TextBox();
        var add = Button("추가", () =>
        {
            var itemName = name.Text.Trim();
            if (itemName.Length == 0)
            {
                name.Focus();
                return;
            }

            items.Rows.Add(itemName, "", 1, 0, 0);
            dialog.Close();
        }, true);
        add.Enabled = false;
        name.TextChanged += (_, _) => add.Enabled = !string.IsNullOrWhiteSpace(name.Text);
        dialog.Controls.Add(Toolbar(add));
        dialog.Controls.Add(Fields(("품목명", name)));
        dialog.AcceptButton = add;
        dialog.ActiveControl = name;
        dialog.ShowDialog(this);
    }

    private void CopyItem()
    {
        if (!items.EndEdit() || items.CurrentRow is not { } row) return;
        var item = ReadItem(row);
        if (string.IsNullOrWhiteSpace(item.Name)) return;
        items.Rows.Add(item.Name, item.Model, item.Quantity, item.Price, item.Amount);
    }

    private void DeleteItem()
    {
        if (items.CurrentRow is not { } row) return;
        items.CancelEdit();
        items.Rows.Remove(row);
    }

    private void MoveItem(int direction)
    {
        if (!items.EndEdit() || items.CurrentRow is not { } row) return;
        var target = row.Index + direction;
        if (target < 0 || target >= items.Rows.Count) return;
        items.Rows.Remove(row);
        items.Rows.Insert(target, row);
        items.CurrentCell = row.Cells[0];
    }

    private void LoadStore()
    {
        try
        {
            if (File.Exists(DataPath)) data = JsonSerializer.Deserialize<Store>(File.ReadAllText(DataPath)) ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            storageAvailable = false;
            MessageBox.Show(this, $"저장 데이터를 읽을 수 없습니다.\n{ex.Message}");
        }
    }

    private bool storageAvailable = true;
    private bool Persist(Store candidate)
    {
        if (designPreview) return false;
        if (!storageAvailable)
        {
            MessageBox.Show(this, "기존 저장 데이터를 읽을 수 없어 저장이 중지되었습니다.");
            return false;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
            var pending = DataPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(pending, JsonSerializer.Serialize(candidate));
                File.Move(pending, DataPath, true);
            }
            finally
            {
                if (File.Exists(pending)) File.Delete(pending);
            }
            data = candidate;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"저장에 실패했습니다.\n{ex.Message}");
            return false;
        }
    }

    private void SaveDocument()
    {
        if (!items.EndEdit()) return;
        var document = Snapshot() with { Issuer = CurrentSupplier };
        if (document.Number.Length == 0 || string.IsNullOrWhiteSpace(document.Customer) ||
            document.Items.Count == 0 || document.Items.Any(i => string.IsNullOrWhiteSpace(i.Name)))
        {
            MessageBox.Show(this, "문서번호, 거래처, 품목명을 입력하세요.");
            return;
        }
        if (data.Documents.Any(d => d.Id != document.Id && d.Number.Equals(document.Number, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "이미 사용 중인 문서번호입니다.");
            return;
        }
        var documents = data.Documents.Where(d => d.Id != document.Id).Append(document).ToList();
        if (!Persist(data with { Documents = documents })) return;
        documentSupplier = document.Issuer;
        baseline = JsonSerializer.Serialize(Snapshot());
        status.Text = $"  저장 완료 · {document.Number}";
    }

    private void ShowDocuments()
    {
        using var dialog = Dialog("문서 목록", 760, 470);
        var grid = MakeGrid(true);
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        foreach (var title in new[] { "구분", "문서번호", "작성일자", "거래처", "합계" }) grid.Columns.Add(title, title);
        foreach (var doc in data.Documents.OrderByDescending(d => d.Date).ThenByDescending(d => d.Number))
        {
            var index = grid.Rows.Add(TitleOf(doc.Kind), doc.Number, doc.Date.ToString("yyyy-MM-dd"), doc.Customer, doc.Total.ToString("N0"));
            grid.Rows[index].Tag = doc;
        }
        dialog.Controls.Add(grid);
        dialog.Controls.Add(Toolbar(Button("열기", OpenSelected, true)));
        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OpenSelected(); };
        dialog.ShowDialog(this);

        void OpenSelected()
        {
            if (grid.CurrentRow?.Tag is not Document selected) return;
            if (!items.EndEdit()) return;
            if (HasChanges() && MessageBox.Show(dialog, "저장하지 않은 변경 사항을 버릴까요?", Text,
                MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            OpenDocument(selected);
            status.Text = $"  {selected.Number}";
            dialog.Close();
        }
    }

    private static Form Dialog(string title, int width, int height) => new ZoomDialog()
    {
        Text = title, Size = new(width, height), StartPosition = FormStartPosition.CenterParent,
        Font = new("맑은 고딕", 10), MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false
    };

    private void RefreshCustomers()
    {
        var current = customer.Text;
        customer.Items.Clear();
        customer.Items.AddRange(data.Customers.Select(c => c.Name).Distinct().Cast<object>().ToArray());
        customer.Text = current;
    }

    private void ManageCustomers()
    {
        EditCatalog("거래처 관리", ["회사명", "담당자", "연락처", "이메일"],
            data.Customers.Select(c => new[] { c.Name, c.Contact, c.Phone, c.Email }), rows =>
            {
                if (rows.Select(r => r[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Count)
                    return "회사명이 중복되었습니다.";
                if (!Persist(data with { Customers = rows.Select(r => new Client(r[0], r[1], r[2], r[3])).ToList() }))
                    return "거래처를 저장하지 못했습니다.";
                RefreshCustomers();
                return null;
            });
    }

    private void ManageProducts()
    {
        EditCatalog("상품 관리", ["품목명", "규격/모델", "단가"],
            data.Products.Select(p => new[] { p.Name, p.Model, p.Price.ToString(CultureInfo.CurrentCulture) }), rows =>
            {
                var products = new List<Line>();
                foreach (var row in rows)
                {
                    if (!TryNumber(row[2], out var price)) return "단가는 0 이상 1조 이하의 숫자를 입력하세요.";
                    products.Add(new(row[0], row[1], 1, price));
                }
                return Persist(data with { Products = products }) ? null : "상품을 저장하지 못했습니다.";
            });
    }

    private void EditCatalog(string title, string[] columns, IEnumerable<string[]> values, Func<List<string[]>, string?> save)
    {
        using var dialog = Dialog(title, 680, 430);
        var grid = MakeGrid(false);
        grid.Dock = DockStyle.Fill;
        foreach (var column in columns) grid.Columns.Add(column, column);
        foreach (var row in values) grid.Rows.Add(row.Cast<object>().ToArray());
        dialog.Controls.Add(grid);
        dialog.Controls.Add(Toolbar(Button("저장", () =>
        {
            if (!grid.EndEdit()) return;
            var rows = grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow)
                .Select(r => r.Cells.Cast<DataGridViewCell>().Select(c => Convert.ToString(c.Value)?.Trim() ?? "").ToArray())
                .Where(row => row.Any(value => value.Length != 0)).ToList();
            var error = rows.Any(row => row[0].Length == 0) ? "이름을 입력하세요." : save(rows);
            if (error is null) dialog.Close();
            else MessageBox.Show(dialog, error);
        }, true), Button("선택 행 삭제", () =>
        {
            if (grid.CurrentRow is { IsNewRow: false } row) grid.Rows.Remove(row);
        })));
        dialog.ShowDialog(this);
    }

    private void PickProduct()
    {
        if (data.Products.Count == 0)
        {
            MessageBox.Show(this, "상품 관리에서 상품을 먼저 등록하세요.");
            return;
        }
        using var dialog = Dialog("상품 선택", 480, 340);
        var list = new ListBox { Dock = DockStyle.Fill };
        foreach (var p in data.Products) list.Items.Add($"{p.Name}  {p.Model}  ({p.Price:N0}원)");
        list.SelectedIndex = 0;
        dialog.Controls.Add(list);
        dialog.Controls.Add(Toolbar(Button("추가", AddSelected, true)));
        list.DoubleClick += (_, _) => AddSelected();
        dialog.ShowDialog(this);

        void AddSelected()
        {
            if (list.SelectedIndex < 0) return;
            var item = data.Products[list.SelectedIndex];
            items.Rows.Add(item.Name, item.Model, 1, item.Price, item.Price);
            dialog.Close();
        }
    }

    private void EditSupplier()
    {
        if (!SettingsEnabled) return;

        using var dialog = Dialog("공급자 정보", 640, 250);
        TextBox name = new() { Text = data.Supplier.Name };
        TextBox registration = new() { Text = data.Supplier.Registration };
        TextBox person = new() { Text = data.Supplier.Contact };
        TextBox address = new() { Text = data.Supplier.Address };
        TextBox tel = new() { Text = data.Supplier.Phone };
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(Toolbar(Button("저장", () =>
        {
            if (!Persist(data with { Supplier = new(name.Text, registration.Text, person.Text, address.Text, tel.Text) })) return;
            preview.Invalidate();
            dialog.Close();
        }, true)));
        panel.Controls.Add(Fields(("회사명", name), ("사업자번호", registration), ("대표자", person), ("주소", address), ("연락처", tel)));
        dialog.Controls.Add(panel);
        dialog.ShowDialog(this);
    }

    private void ShowHome()
    {
        var quotes = data.Documents.Count(d => d.Kind == DocumentKind.Quote);
        var statements = data.Documents.Count(d => d.Kind == DocumentKind.Statement);
        MessageBox.Show(this,
            $"저장한 견적서  {quotes:N0}건\n저장한 거래명세서  {statements:N0}건\n등록 거래처  {data.Customers.Count:N0}곳\n등록 상품  {data.Products.Count:N0}개",
            "홈");
    }

    private void ShowStats()
    {
        MessageBox.Show(this, string.Join("\n", Enum.GetValues<DocumentKind>().Select(type =>
        {
            var docs = data.Documents.Where(d => d.Kind == type).ToList();
            return $"{TitleOf(type)}  {docs.Count:N0}건  ·  합계 {docs.Sum(d => d.Total):N0}원";
        })), "저장 문서 현황");
    }

    private void ExportCsv()
    {
        if (designPreview) return;
        if (!items.EndEdit()) return;
        var doc = Snapshot();
        using var dialog = new SaveFileDialog { Filter = "CSV 파일 (*.csv)|*.csv", FileName = SafeFileName(doc.Number) + ".csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var csv = new StringBuilder();
        Row("문서종류", TitleOf(doc.Kind), "문서번호", doc.Number);
        Row("작성일자", doc.Date.ToString("yyyy-MM-dd"), "유효기간", doc.Kind == DocumentKind.Quote ? doc.Expires.ToString("yyyy-MM-dd") : "", "담당자", doc.Owner);
        Row("거래처", doc.Customer, "담당자", doc.Contact, "연락처", doc.Phone, "이메일", doc.Email);
        Row("공급자", CurrentSupplier.Name, "사업자번호", CurrentSupplier.Registration, "대표자", CurrentSupplier.Contact,
            "주소", CurrentSupplier.Address, "연락처", CurrentSupplier.Phone);
        Row("품목명", "규격/모델", "수량", "단가", "금액");
        foreach (var item in doc.Items) Row(item.Name, item.Model, Numeric(item.Quantity), Numeric(item.Price), Numeric(item.Amount));
        Row("공급가액", Numeric(doc.Subtotal), "부가세", Numeric(doc.Tax), "합계", Numeric(doc.Total));
        Row("비고", doc.Notes);
        try
        {
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            status.Text = "  CSV 내보내기 완료";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"파일을 저장할 수 없습니다.\n{ex.Message}");
        }

        void Row(params string[] cells) => csv.AppendLine(string.Join(",", cells.Select(CsvCell)));
        static string Numeric(decimal amount) => amount.ToString(CultureInfo.InvariantCulture);
        static string CsvCell(string value)
        {
            var leading = value.TrimStart();
            if ((leading.Length > 0 && "=+-@".Contains(leading[0])) || value.StartsWith('\t') || value.StartsWith('\r'))
                value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }

    private static string SafeFileName(string value) =>
        string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private void Print(bool showPreview)
    {
        if (designPreview) return;
        if (!items.EndEdit()) return;
        var doc = Snapshot();
        var supplier = CurrentSupplier;
        using var print = new PrintDocument { DocumentName = SafeFileName(doc.Number) };
        var page = 0;
        print.BeginPrint += (_, _) => page = 0;
        print.PrintPage += (_, e) =>
        {
            if (e.Graphics is null) return;
            Paper.Draw(e.Graphics, e.MarginBounds, doc, supplier, page++);
            e.HasMorePages = page < Paper.PageCount(doc);
        };
        try
        {
            if (showPreview)
            {
                using var dialog = new PrintPreviewDialog { Document = print, Width = 960, Height = 760 };
                dialog.ShowDialog(this);
            }
            else
            {
                using var dialog = new PrintDialog { Document = print, UseEXDialog = true };
                if (dialog.ShowDialog(this) == DialogResult.OK) print.Print();
            }
        }
        catch (Exception ex) when (ex is InvalidPrinterException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(this, $"인쇄 장치를 사용할 수 없습니다.\n{ex.Message}");
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!designPreview && preview is not null && (!items.EndEdit() || HasChanges()) &&
            MessageBox.Show(this, "저장하지 않은 변경 사항을 버리고 닫을까요?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) e.Cancel = true;
        base.OnFormClosing(e);
    }

    private static string TitleOf(DocumentKind type) => type == DocumentKind.Quote ? "견적서" : "거래명세서";

    public enum DocumentKind { Quote, Statement }
    public record Client(string Name, string Contact, string Phone, string Email);
    public record Supplier(string Name, string Registration, string Contact, string Address, string Phone);
    public record Line(string Name, string Model, decimal Quantity, decimal Price)
    {
        public decimal Amount => decimal.Round(Quantity * Price, 0, MidpointRounding.AwayFromZero);
    }

    public sealed record Document
    {
        public Guid Id { get; init; }
        public DocumentKind Kind { get; init; }
        public string Number { get; init; } = "";
        public DateTime Date { get; init; }
        public DateTime Expires { get; init; }
        public string Owner { get; init; } = "";
        public string Customer { get; set; } = "";
        public string Contact { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Notes { get; set; } = "";
        public Supplier? Issuer { get; init; }
        public List<Line> Items { get; set; } = [];
        public decimal Subtotal => Items.Sum(i => i.Amount);
        public decimal Tax => decimal.Round(Subtotal * 0.1m, 0, MidpointRounding.AwayFromZero);
        public decimal Total => Subtotal + Tax;
    }

    public sealed record Store
    {
        public List<Document> Documents { get; init; } = [];
        public List<Client> Customers { get; init; } = [];
        public List<Line> Products { get; init; } = [];
        public Supplier Supplier { get; init; } = new("", "", "", "", "");
    }

    private sealed class Paper : Panel
    {
        private const int RowsPerPage = 18;
        private const float PageWidth = 680;
        private const float PageHeight = 880;
        private const int NotesPerPage = 960;

        public Paper(Func<Document> document, Func<Supplier> supplier)
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(234, 239, 246);
            Paint += (_, e) => Render(e.Graphics, document(), supplier());
            Resize += (_, _) => Invalidate();
            Scroll += (_, _) => Invalidate();
        }

        private void Render(Graphics graphics, Document document, Supplier supplier)
        {
            var gutter = 24 * DeviceDpi / 96;
            var widthScale = Math.Max(1, ClientSize.Width - gutter - SystemInformation.VerticalScrollBarWidth) / PageWidth;
            var heightScale = Math.Max(1, ClientSize.Height - gutter) / PageHeight;
            var scale = Math.Min(widthScale, Math.Max(0.72f * DeviceDpi / 96, heightScale) * InterfaceZoom);
            var width = (int)(PageWidth * scale);
            var height = (int)(PageHeight * scale);
            var pages = PageCount(document);
            var required = new Size(0, 12 + pages * (height + 14));
            if (AutoScrollMinSize != required) AutoScrollMinSize = required;
            var x = Math.Max(9, (ClientSize.Width - width) / 2);
            for (var page = 0; page < pages; page++)
            {
                var y = 12 + AutoScrollPosition.Y + page * (height + 14);
                if (y >= ClientSize.Height || y + height <= 0) continue;
                var bounds = new Rectangle(x, y, width, height);
                graphics.FillRectangle(Brushes.LightSlateGray, x + 3, y + 4, width, height);
                graphics.FillRectangle(Brushes.White, bounds);
                graphics.DrawRectangle(Pens.LightSlateGray, bounds);
                Draw(graphics, bounds, document, supplier, page);
            }
        }

        private static List<string> SplitNotes(string notes)
        {
            var pages = new List<string>();
            var current = new StringBuilder();
            var lineCount = 0;
            var lines = notes.Replace("\r\n", "\n").Split('\n');
            foreach (var line in lines)
            {
                for (var offset = 0; offset < Math.Max(1, line.Length); offset += 43)
                {
                    var count = Math.Min(43, line.Length - offset);
                    if (current.Length + count > NotesPerPage || lineCount >= 21)
                    {
                        pages.Add(current.ToString());
                        current.Clear();
                        lineCount = 0;
                    }
                    current.Append(line, offset, count).Append('\n');
                    lineCount++;
                }
            }
            pages.Add(current.ToString());
            return pages;
        }
        public static int PageCount(Document doc) => ItemPages(doc) + (doc.Notes.Length == 0 || InlineNotes(doc) ? 0 : SplitNotes(doc.Notes).Count);

        private static bool InlineNotes(Document doc)
        {
            var lastPageRows = doc.Items.Count - (ItemPages(doc) - 1) * RowsPerPage;
            var notes = SplitNotes(doc.Notes);
            return notes.Count == 1 && notes[0].Count(c => c == '\n') * 20 <= 390 - lastPageRows * 22;
        }

        private static int ItemPages(Document doc) => Math.Max(1, (doc.Items.Count + RowsPerPage - 1) / RowsPerPage);

        public static void Draw(Graphics graphics, Rectangle bounds, Document doc, Supplier supplier, int page)
        {
            var state = graphics.Save();
            try
            {
                var scale = Math.Min(bounds.Width / PageWidth, bounds.Height / PageHeight);
                graphics.TranslateTransform(bounds.X + (bounds.Width - PageWidth * scale) / 2,
                    bounds.Y + (bounds.Height - PageHeight * scale) / 2);
                graphics.ScaleTransform(scale, scale);
                using var title = new Font("맑은 고딕", 36, FontStyle.Bold, GraphicsUnit.Pixel);
                using var normal = new Font("맑은 고딕", 13, GraphicsUnit.Pixel);
                using var bold = new Font("맑은 고딕", 13, FontStyle.Bold, GraphicsUnit.Pixel);
                using var shade = new SolidBrush(Pale);
                using var ink = new SolidBrush(Navy);
                using var rule = new Pen(Color.FromArgb(159, 176, 199), 0.8f);
                using var left = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                using var centered = new StringFormat(left) { Alignment = StringAlignment.Center };
                using var right = new StringFormat(left) { Alignment = StringAlignment.Far };

                graphics.DrawString(TitleOf(doc.Kind), title, ink, new RectangleF(24, 28, 360, 72), centered);
                Cell(doc.Kind == DocumentKind.Quote ? "견적번호" : "문서번호", 400, 25, 79, 25, true);
                Cell(doc.Number, 479, 25, 177, 25);
                Cell("작성일자", 400, 50, 79, 25, true);
                Cell(doc.Date.ToString("yyyy-MM-dd"), 479, 50, 177, 25);
                Cell(doc.Kind == DocumentKind.Quote ? "유효기간" : "담당자", 400, 75, 79, 25, true);
                Cell(doc.Kind == DocumentKind.Quote ? doc.Expires.ToString("yyyy-MM-dd") : doc.Owner, 479, 75, 177, 25);
                Party(doc.Kind == DocumentKind.Quote ? "수신 (견적 요청사)" : "수신 (거래처)", 24, 304, [("회사명", doc.Customer), ("담당자", doc.Contact), ("연락처", doc.Phone), ("이메일", doc.Email)]);
                Party("공급자 (문서 발행사)", 340, 316, [("회사명", supplier.Name), ("사업자번호", supplier.Registration), ("대표자", supplier.Contact),
                    ("주소", supplier.Address), ("연락처", supplier.Phone)]);
                var itemPages = ItemPages(doc);
                var notes = SplitNotes(doc.Notes);
                if (page < itemPages)
                {
                    graphics.DrawString(doc.Kind == DocumentKind.Quote ? "아래와 같이 견적합니다." : "아래와 같이 거래 내역을 확인합니다.",
                        normal, ink, 24, 272);
                    string[] headers = ["No", "품목명", "규격/모델", "수량", "단가(원)", "금액(원)"];
                    int[] widths = [38, 175, 115, 72, 108, 124];
                    DrawRow(headers, 300, true);
                    var y = 325;
                    var index = page * RowsPerPage;
                    foreach (var line in doc.Items.Skip(index).Take(RowsPerPage))
                    {
                        DrawRow([(++index).ToString(), line.Name, line.Model, line.Quantity.ToString("0.##"),
                            line.Price.ToString("#,0.##"), line.Amount.ToString("N0")], y, false);
                        y += 22;
                    }
                    if (page == itemPages - 1)
                    {
                        y += 9;
                        Total("공급가액", doc.Subtotal, y);
                        Total("부가세 (10%)", doc.Tax, y + 24);
                        Total("합계금액", doc.Total, y + 48, true);
                        if (doc.Notes.Length > 0 && InlineNotes(doc))
                        {
                            var notesY = Math.Max(y + 85, 838 - 35 - notes[0].Count(c => c == '\n') * 20);
                            DrawNotes(notes[0], notesY, 844 - notesY);
                        }
                    }

                    void DrawRow(string[] values, int y, bool header)
                    {
                        var x = 24;
                        for (var col = 0; col < values.Length; col++)
                        {
                            Cell(values[col], x, y, widths[col], header ? 25 : 22, header,
                                header || col == 0 ? centered : col >= 3 ? right : left);
                            x += widths[col];
                        }
                    }
                }
                else
                {
                    DrawNotes(notes[page - itemPages], 278, 550);
                }
                graphics.DrawString($"{page + 1} / {PageCount(doc)}", normal, ink, new RectangleF(24, 847, 632, 24), centered);

                void Cell(string value, int x, int y, int width, int height, bool shaded = false, StringFormat? alignment = null)
                {
                    if (shaded) graphics.FillRectangle(shade, x, y, width, height);
                    graphics.DrawRectangle(rule, x, y, width, height);
                    graphics.DrawString(value, shaded ? bold : normal, ink,
                        new RectangleF(x + 6, y, width - 12, height), alignment ?? left);
                }

                void Party(string label, int x, int width, (string Name, string Value)[] fields)
                {
                    Cell(label, x, 122, width, 25, true);
                    var y = 147;
                    foreach (var field in fields)
                    {
                        Cell(field.Name, x, y, 88, 23, true);
                        Cell(field.Value, x + 88, y, width - 88, 23);
                        y += 23;
                    }
                }

                void Total(string label, decimal amount, int y, bool final = false)
                {
                    Cell(label, 380, y, 110, 24, true);
                    Cell(amount.ToString("N0"), 490, y, 166, 24, final, right);
                }

                void DrawNotes(string value, int y, int height)
                {
                    Cell("비고", 24, y, 632, 24, true);
                    graphics.DrawRectangle(rule, 24, y + 24, 632, height - 24);
                    using var format = new StringFormat { Trimming = StringTrimming.Word };
                    graphics.DrawString(value, normal, ink, new RectangleF(30, y + 30, 620, Math.Max(1, height - 30)), format);
                }
            }
            finally { graphics.Restore(state); }
        }
    }
}
