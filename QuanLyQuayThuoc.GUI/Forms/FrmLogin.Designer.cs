namespace QuanLyQuayThuoc.GUI.Forms;

partial class FrmLogin
{
    private System.ComponentModel.IContainer? components = null;
    private Panel pnlLeft = null!;
    private Label lblBrand = null!;
    private Label lblBrandSub = null!;
    private Panel pnlRight = null!;
    private Label lblTitle = null!;
    private Label lblSubtitle = null!;
    private Label lblUsername = null!;
    private TextBox txtUsername = null!;
    private Label lblPassword = null!;
    private TextBox txtPassword = null!;
    private Button btnLogin = null!;
    private LinkLabel lnkTestDb = null!;
    private Label lblFooter = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        pnlLeft = new Panel();
        lblBrand = new Label();
        lblBrandSub = new Label();
        pnlRight = new Panel();
        lblTitle = new Label();
        lblSubtitle = new Label();
        lblUsername = new Label();
        txtUsername = new TextBox();
        lblPassword = new Label();
        txtPassword = new TextBox();
        btnLogin = new Button();
        lnkTestDb = new LinkLabel();
        lblFooter = new Label();
        pnlLeft.SuspendLayout();
        pnlRight.SuspendLayout();
        SuspendLayout();
        // pnlLeft
        pnlLeft.BackColor = Color.FromArgb(29, 43, 91);
        pnlLeft.Controls.Add(lblBrandSub);
        pnlLeft.Controls.Add(lblBrand);
        pnlLeft.Dock = DockStyle.Left;
        pnlLeft.Size = new Size(340, 560);
        // lblBrand
        lblBrand.AutoSize = true;
        lblBrand.Font = new Font("Segoe UI", 25F, FontStyle.Bold);
        lblBrand.ForeColor = Color.White;
        lblBrand.Location = new Point(38, 190);
        lblBrand.Text = "QUẦY THUỐC";
        // lblBrandSub
        lblBrandSub.Font = new Font("Segoe UI", 11F);
        lblBrandSub.ForeColor = Color.FromArgb(211, 220, 242);
        lblBrandSub.Location = new Point(42, 247);
        lblBrandSub.Size = new Size(255, 70);
        lblBrandSub.Text = "HỆ THỐNG QUẢN LÝ BÁN HÀNG\nKHO THUỐC VÀ HÓA ĐƠN";
        // pnlRight
        pnlRight.BackColor = Color.White;
        pnlRight.Controls.Add(lblFooter);
        pnlRight.Controls.Add(lnkTestDb);
        pnlRight.Controls.Add(btnLogin);
        pnlRight.Controls.Add(txtPassword);
        pnlRight.Controls.Add(lblPassword);
        pnlRight.Controls.Add(txtUsername);
        pnlRight.Controls.Add(lblUsername);
        pnlRight.Controls.Add(lblSubtitle);
        pnlRight.Controls.Add(lblTitle);
        pnlRight.Dock = DockStyle.Fill;
        pnlRight.Padding = new Padding(55, 45, 55, 35);
        // lblTitle
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(35, 45, 70);
        lblTitle.Location = new Point(55, 105);
        lblTitle.Text = "Đăng nhập";
        // lblSubtitle
        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 9.5F);
        lblSubtitle.ForeColor = Color.Gray;
        lblSubtitle.Location = new Point(59, 153);
        lblSubtitle.Text = "Đăng nhập để tiếp tục sử dụng hệ thống";
        // lblUsername
        lblUsername.AutoSize = true;
        lblUsername.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        lblUsername.Location = new Point(59, 211);
        lblUsername.Text = "Tên đăng nhập";
        // txtUsername
        txtUsername.Font = new Font("Segoe UI", 11F);
        txtUsername.Location = new Point(59, 237);
        txtUsername.Size = new Size(330, 32);
        txtUsername.BorderStyle = BorderStyle.FixedSingle;
        txtUsername.Text = "admin";
        // lblPassword
        lblPassword.AutoSize = true;
        lblPassword.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        lblPassword.Location = new Point(59, 291);
        lblPassword.Text = "Mật khẩu";
        // txtPassword
        txtPassword.Font = new Font("Segoe UI", 11F);
        txtPassword.Location = new Point(59, 317);
        txtPassword.Size = new Size(330, 32);
        txtPassword.UseSystemPasswordChar = true;
        txtPassword.Text = "Admin@123";
        // btnLogin
        btnLogin.BackColor = Color.FromArgb(36, 91, 170);
        btnLogin.FlatAppearance.BorderSize = 0;
        btnLogin.FlatStyle = FlatStyle.Flat;
        btnLogin.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        btnLogin.ForeColor = Color.White;
        btnLogin.Location = new Point(59, 375);
        btnLogin.Size = new Size(330, 43);
        btnLogin.Text = "ĐĂNG NHẬP";
        btnLogin.UseVisualStyleBackColor = false;
        btnLogin.Click += btnLogin_Click;
        // lnkTestDb
        lnkTestDb.AutoSize = true;
        lnkTestDb.Font = new Font("Segoe UI", 9F);
        lnkTestDb.Location = new Point(59, 438);
        lnkTestDb.Text = "Kiểm tra kết nối cơ sở dữ liệu";
        lnkTestDb.LinkClicked += lnkTestDb_LinkClicked;
        // lblFooter
        lblFooter.AutoSize = true;
        lblFooter.Font = new Font("Segoe UI", 8F);
        lblFooter.ForeColor = Color.Gray;
        lblFooter.Location = new Point(59, 490);
        lblFooter.Text = "Quản lý quầy thuốc • Phiên bản đồ án";
        // FrmLogin
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(850, 560);
        Controls.Add(pnlRight);
        Controls.Add(pnlLeft);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Đăng nhập - Quản lý quầy thuốc";
        pnlLeft.ResumeLayout(false);
        pnlLeft.PerformLayout();
        pnlRight.ResumeLayout(false);
        pnlRight.PerformLayout();
        ResumeLayout(false);
    }
}
