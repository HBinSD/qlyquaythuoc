namespace QuanLyQuayThuoc.GUI.Forms;

partial class FrmMain
{
    private System.ComponentModel.IContainer? components = null;
    private Panel pnlSidebar = null!;
    private Panel pnlBrand = null!;
    private Label lblBrand = null!;
    private Label lblBrandSub = null!;
    private FlowLayoutPanel flpMenu = null!;
    private Button btnDashboard = null!;
    private Button btnMedicine = null!;
    private Button btnWarehouse = null!;
    private Button btnSuppliers = null!;
    private Button btnSales = null!;
    private Button btnInvoices = null!;
    private Button btnEmployees = null!;
    private Button btnReports = null!;
    private Panel pnlTop = null!;
    private Label lblPageTitle = null!;
    private Label lblUserName = null!;
    private Label lblRole = null!;
    private Button btnLogout = null!;
    private Panel pnlContent = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private Button CreateMenuButton(string text, string page)
    {
        var button = new Button
        {
            Text = "   " + text,
            Tag = page,
            Width = 224,
            Height = 48,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(224, 231, 245),
            BackColor = Color.FromArgb(29, 43, 91),
            Font = new Font("Segoe UI", 10F),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            Margin = new Padding(0)
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += MenuButton_Click;
        return button;
    }

    private void InitializeComponent()
    {
        pnlSidebar = new Panel();
        pnlBrand = new Panel();
        lblBrand = new Label();
        lblBrandSub = new Label();
        flpMenu = new FlowLayoutPanel();
        btnDashboard = CreateMenuButton("Tổng quan", "dashboard");
        btnMedicine = CreateMenuButton("Quản lý thuốc", "medicine");
        btnWarehouse = CreateMenuButton("Quản lý kho", "warehouse");
        btnSuppliers = CreateMenuButton("Nhà cung cấp", "suppliers");
        btnSales = CreateMenuButton("Bán thuốc", "sales");
        btnInvoices = CreateMenuButton("Hóa đơn", "invoices");
        btnEmployees = CreateMenuButton("Nhân viên", "employees");
        btnReports = CreateMenuButton("Báo cáo", "reports");
        pnlTop = new Panel();
        lblPageTitle = new Label();
        lblUserName = new Label();
        lblRole = new Label();
        btnLogout = new Button();
        pnlContent = new Panel();
        pnlSidebar.SuspendLayout();
        pnlBrand.SuspendLayout();
        flpMenu.SuspendLayout();
        pnlTop.SuspendLayout();
        SuspendLayout();
        // pnlSidebar
        pnlSidebar.BackColor = Color.FromArgb(29, 43, 91);
        pnlSidebar.Controls.Add(flpMenu);
        pnlSidebar.Controls.Add(pnlBrand);
        pnlSidebar.Dock = DockStyle.Left;
        pnlSidebar.Width = 245;
        // pnlBrand
        pnlBrand.Controls.Add(lblBrandSub);
        pnlBrand.Controls.Add(lblBrand);
        pnlBrand.Dock = DockStyle.Top;
        pnlBrand.Height = 88;
        // lblBrand
        lblBrand.AutoSize = true;
        lblBrand.Font = new Font("Segoe UI", 17F, FontStyle.Bold);
        lblBrand.ForeColor = Color.White;
        lblBrand.Location = new Point(20, 13);
        lblBrand.Text = "MediStore";
        // lblBrandSub
        lblBrandSub.AutoSize = true;
        lblBrandSub.Font = new Font("Segoe UI", 8F);
        lblBrandSub.ForeColor = Color.FromArgb(190, 203, 231);
        lblBrandSub.Location = new Point(23, 49);
        lblBrandSub.Text = "QUẢN LÝ QUẦY THUỐC";
        // flpMenu
        flpMenu.Dock = DockStyle.Fill;
        flpMenu.FlowDirection = FlowDirection.TopDown;
        flpMenu.WrapContents = false;
        flpMenu.AutoScroll = true;
        flpMenu.Padding = new Padding(10, 14, 10, 10);
        flpMenu.Controls.AddRange(new Control[] { btnDashboard, btnMedicine, btnWarehouse, btnSuppliers, btnSales, btnInvoices, btnEmployees, btnReports });
        // pnlTop
        pnlTop.BackColor = Color.White;
        pnlTop.Controls.Add(btnLogout);
        pnlTop.Controls.Add(lblRole);
        pnlTop.Controls.Add(lblUserName);
        pnlTop.Controls.Add(lblPageTitle);
        pnlTop.Dock = DockStyle.Top;
        pnlTop.Height = 70;
        // lblPageTitle
        lblPageTitle.AutoSize = true;
        lblPageTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblPageTitle.ForeColor = Color.FromArgb(38, 48, 69);
        lblPageTitle.Location = new Point(28, 19);
        lblPageTitle.Text = "Tổng quan";
        // lblUserName
        lblUserName.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lblUserName.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        lblUserName.ForeColor = Color.FromArgb(48, 58, 78);
        lblUserName.Location = new Point(770, 15);
        lblUserName.Size = new Size(220, 22);
        lblUserName.TextAlign = ContentAlignment.MiddleRight;
        // lblRole
        lblRole.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lblRole.Font = new Font("Segoe UI", 8.5F);
        lblRole.ForeColor = Color.Gray;
        lblRole.Location = new Point(770, 37);
        lblRole.Size = new Size(220, 20);
        lblRole.TextAlign = ContentAlignment.MiddleRight;
        // btnLogout
        btnLogout.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnLogout.BackColor = Color.FromArgb(245, 247, 251);
        btnLogout.FlatStyle = FlatStyle.Flat;
        btnLogout.FlatAppearance.BorderColor = Color.FromArgb(225, 230, 238);
        btnLogout.Font = new Font("Segoe UI", 9F);
        btnLogout.Location = new Point(1005, 17);
        btnLogout.Size = new Size(105, 36);
        btnLogout.Text = "Đăng xuất";
        btnLogout.Click += btnLogout_Click;
        // pnlContent
        pnlContent.BackColor = Color.FromArgb(245, 247, 251);
        pnlContent.Dock = DockStyle.Fill;
        // FrmMain
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1360, 820);
        Controls.Add(pnlContent);
        Controls.Add(pnlTop);
        Controls.Add(pnlSidebar);
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "MediStore - Quản lý quầy thuốc";
        WindowState = FormWindowState.Maximized;
        Load += FrmMain_Load;
        pnlSidebar.ResumeLayout(false);
        pnlBrand.ResumeLayout(false);
        pnlBrand.PerformLayout();
        flpMenu.ResumeLayout(false);
        pnlTop.ResumeLayout(false);
        pnlTop.PerformLayout();
        ResumeLayout(false);
    }

    private void FrmMain_Load(object? sender, EventArgs e) => RegisterMenuButtons();
}
