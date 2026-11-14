using System.Drawing;
using System.Windows.Forms;
using QuanLyQuayThuoc.BLL.Models;
using QuanLyQuayThuoc.BLL.Services;
using QuanLyQuayThuoc.DAL.Repositories;
using QuanLyQuayThuoc.GUI.Session;

namespace QuanLyQuayThuoc.GUI.Controls;

public sealed class UcEmployees : UserControl
{
    private static readonly RoleOption[] Roles =
    [
        new("ADMIN", "Quản lý (Admin)"),
        new("PHARMACIST", "Dược sĩ"),
        new("WAREHOUSE", "Nhân viên kho")
    ];

    private readonly EmployeeService _service = new();
    private readonly DataGridView _employeesGrid = CreateGrid();
    private readonly DataGridView _vouchersGrid = CreateGrid();
    private readonly TextBox _fullName = new();
    private readonly TextBox _username = new();
    private readonly TextBox _password = new();
    private readonly ComboBox _role = new();
    private readonly CheckBox _isActive = new();
    private readonly NumericUpDown _earnPercent = new();
    private readonly NumericUpDown _moneyPerPoint = new();
    private readonly Label _employeeMode = new();
    private string? _selectedEmployeeId;
    private bool _loading;

    public UcEmployees()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 9F);
        BuildLayout();
        Load += async (_, _) => await LoadAllAsync();
    }

    private void BuildLayout()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Size = new Size(1050, 700),
            SplitterWidth = 8,
            BackColor = BackColor
        };
        ControlLayout.SetSplitRatio(split, 0.62);
        split.Panel1.Padding = new Padding(12);
        split.Panel2.Padding = new Padding(8, 12, 12, 12);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BackColor
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        var employeeHeader = new Panel { Dock = DockStyle.Fill };
        employeeHeader.Controls.Add(Heading("DANH SÁCH NHÂN VIÊN & PHÂN QUYỀN"));
        var addButton = Button("+ Cấp tài khoản mới", Color.FromArgb(35, 112, 190));
        addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        addButton.Location = new Point(420, 2);
        addButton.Click += (_, _) => BeginNewEmployee();
        employeeHeader.Controls.Add(addButton);

        ConfigureEmployeeGrid();
        _employeesGrid.Dock = DockStyle.Fill;
        _employeesGrid.SelectionChanged += (_, _) => SelectEmployee();

        var editor = BuildEmployeeEditor();
        left.Controls.Add(employeeHeader, 0, 0);
        left.Controls.Add(_employeesGrid, 0, 1);
        left.Controls.Add(editor, 0, 2);
        split.Panel1.Controls.Add(left);
        split.Panel2.Controls.Add(BuildLoyaltyAndVoucherPanel());
        Controls.Add(split);
    }

    private Control BuildEmployeeEditor()
    {
        var group = new GroupBox
        {
            Text = "THÔNG TIN TÀI KHOẢN & PHÂN QUYỀN",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(5, 10, 5, 5)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 4; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        ConfigureTextBox(_fullName);
        ConfigureTextBox(_username);
        ConfigureTextBox(_password);
        _password.UseSystemPasswordChar = true;
        _password.PlaceholderText = "Để trống nếu không đổi mật khẩu";
        _role.DropDownStyle = ComboBoxStyle.DropDownList;
        _role.DataSource = Roles;
        _role.DisplayMember = nameof(RoleOption.Label);
        _role.ValueMember = nameof(RoleOption.Id);
        _role.Dock = DockStyle.Top;
        _role.Font = new Font("Segoe UI", 9F);
        _isActive.Text = "Đang hoạt động";
        _isActive.Dock = DockStyle.Top;

        layout.Controls.Add(Labeled("Họ tên nhân viên", _fullName), 0, 0);
        layout.Controls.Add(Labeled("Tên đăng nhập", _username), 1, 0);
        layout.Controls.Add(Labeled("Vai trò đảm nhiệm", _role), 0, 1);
        layout.Controls.Add(Labeled("Đặt lại mật khẩu mới", _password), 1, 1);
        layout.Controls.Add(_isActive, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var save = Button("Lưu cập nhật tài khoản", Color.FromArgb(16, 63, 117));
        var toggle = Button("Khóa / Mở khóa", Color.FromArgb(100, 110, 125));
        save.Click += async (_, _) => await SaveEmployeeAsync();
        toggle.Click += async (_, _) => await ToggleEmployeeAsync();
        buttons.Controls.Add(save);
        buttons.Controls.Add(toggle);
        layout.Controls.Add(buttons, 1, 3);

        _employeeMode.Text = "Chọn một nhân viên hoặc cấp tài khoản mới.";
        _employeeMode.ForeColor = Color.DimGray;
        _employeeMode.Dock = DockStyle.Fill;
        _employeeMode.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(_employeeMode, 0, 3);

        group.Controls.Add(layout);
        return group;
    }

    private Control BuildLoyaltyAndVoucherPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = BackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 226));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var loyalty = new GroupBox
        {
            Text = "CẤU HÌNH TÍCH ĐIỂM THÀNH VIÊN",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(12)
        };
        var loyaltyLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(5, 12, 5, 5)
        };
        loyaltyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        loyaltyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        loyaltyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        loyaltyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        loyaltyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        ConfigureDecimal(_earnPercent, 0, 100, 1, 2);
        ConfigureDecimal(_moneyPerPoint, 0.01M, 1000000000, 100, 2);
        loyaltyLayout.Controls.Add(Labeled("Tỷ lệ tích điểm (% giá trị thanh toán)", _earnPercent), 0, 0);
        loyaltyLayout.Controls.Add(Labeled("Giá trị quy đổi (VND / điểm)", _moneyPerPoint), 1, 0);
        var earnHint = Hint("Ví dụ: 1% giá trị thanh toán = 1 điểm trên mỗi 10.000đ.");
        var redeemHint = Hint("Số tiền được giảm khi khách đổi 1 điểm.");
        loyaltyLayout.Controls.Add(earnHint, 0, 1);
        loyaltyLayout.Controls.Add(redeemHint, 1, 1);

        var saveSettings = Button("Lưu quy tắc điểm thưởng", Color.FromArgb(35, 112, 190));
        saveSettings.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        saveSettings.Click += async (_, _) => await SaveLoyaltySettingsAsync();
        loyaltyLayout.Controls.Add(saveSettings, 1, 2);
        loyalty.Controls.Add(loyaltyLayout);

        var vouchers = new GroupBox
        {
            Text = "QUẢN LÝ MÃ GIẢM GIÁ (VOUCHER)",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(10)
        };
        var voucherLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        voucherLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        voucherLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var voucherActions = new Panel { Dock = DockStyle.Fill };
        var addVoucher = Button("+ Tạo Voucher", Color.FromArgb(16, 63, 117));
        addVoucher.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        addVoucher.Location = new Point(245, 2);
        addVoucher.Click += async (_, _) => await AddVoucherAsync();
        var toggleVoucher = Button("Đổi trạng thái", Color.FromArgb(100, 110, 125));
        toggleVoucher.Location = new Point(4, 2);
        toggleVoucher.Click += async (_, _) => await ToggleVoucherAsync();
        voucherActions.Controls.Add(addVoucher);
        voucherActions.Controls.Add(toggleVoucher);
        ConfigureVoucherGrid();
        _vouchersGrid.Dock = DockStyle.Fill;
        voucherLayout.Controls.Add(voucherActions, 0, 0);
        voucherLayout.Controls.Add(_vouchersGrid, 0, 1);
        vouchers.Controls.Add(voucherLayout);

        layout.Controls.Add(loyalty, 0, 0);
        layout.Controls.Add(vouchers, 0, 1);
        return layout;
    }

    private void ConfigureEmployeeGrid()
    {
        AddTextColumn(_employeesGrid, nameof(EmployeeListRow.EmployeeId), "Mã NV", 82);
        AddTextColumn(_employeesGrid, nameof(EmployeeListRow.FullName), "Họ và tên", 155);
        AddTextColumn(_employeesGrid, nameof(EmployeeListRow.Username), "Tên đăng nhập", 125);
        AddTextColumn(_employeesGrid, nameof(EmployeeListRow.RoleName), "Vai trò hệ thống", 120);
        AddTextColumn(_employeesGrid, nameof(EmployeeListRow.IsActive), "Trạng thái", 85);
        _employeesGrid.CellFormatting += (_, e) =>
        {
            if (_employeesGrid.Columns[e.ColumnIndex].DataPropertyName == nameof(EmployeeListRow.IsActive)
                && e.Value is bool active)
            {
                e.Value = active ? "Hoạt động" : "Đã khóa";
                e.FormattingApplied = true;
            }
        };
    }

    private void ConfigureVoucherGrid()
    {
        AddTextColumn(_vouchersGrid, nameof(VoucherRow.VoucherCode), "Mã Voucher", 100);
        AddTextColumn(_vouchersGrid, nameof(VoucherRow.DiscountValue), "Mức giảm", 85);
        AddTextColumn(_vouchersGrid, nameof(VoucherRow.MinimumOrder), "Đơn tối thiểu", 95);
        AddTextColumn(_vouchersGrid, nameof(VoucherRow.EndDate), "Hạn sử dụng", 100);
        AddTextColumn(_vouchersGrid, nameof(VoucherRow.IsActive), "Trạng thái", 95);
        _vouchersGrid.CellFormatting += (_, e) =>
        {
            string property = _vouchersGrid.Columns[e.ColumnIndex].DataPropertyName;
            if (property == nameof(VoucherRow.DiscountValue)
                && _vouchersGrid.Rows[e.RowIndex].DataBoundItem is VoucherRow voucher)
            {
                e.Value = voucher.DiscountType == "PERCENT"
                    ? $"{voucher.DiscountValue:0.##}%"
                    : $"{voucher.DiscountValue:N0} đ";
                e.FormattingApplied = true;
            }
            else if (property == nameof(VoucherRow.MinimumOrder) && e.Value is decimal minimum)
            {
                e.Value = $"{minimum:N0} đ";
                e.FormattingApplied = true;
            }
            else if (property == nameof(VoucherRow.EndDate) && e.Value is DateTime endDate)
            {
                e.Value = endDate.ToString("dd/MM/yyyy");
                e.FormattingApplied = true;
            }
            else if (property == nameof(VoucherRow.IsActive)
                     && _vouchersGrid.Rows[e.RowIndex].DataBoundItem is VoucherRow row)
            {
                e.Value = row.EndDate < DateTime.Today
                    ? "Hết hạn"
                    : row.IsActive ? "Đang áp dụng" : "Đã tắt";
                e.FormattingApplied = true;
            }
        };
    }

    private async Task LoadAllAsync()
    {
        if (_loading)
            return;

        _loading = true;
        try
        {
            var user = RequireManager();
            var employees = await _service.GetEmployeesAsync(user.EmployeeId, user.RoleId);
            var vouchers = await _service.GetVouchersAsync(user.EmployeeId, user.RoleId);
            var settings = await _service.GetLoyaltySettingsAsync(user.EmployeeId, user.RoleId);
            _employeesGrid.DataSource = employees;
            _vouchersGrid.DataSource = vouchers;
            _earnPercent.Value = Math.Clamp(settings.EarnPercent, _earnPercent.Minimum, _earnPercent.Maximum);
            _moneyPerPoint.Value = Math.Clamp(settings.MoneyPerPoint, _moneyPerPoint.Minimum, _moneyPerPoint.Maximum);
            BeginNewEmployee();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _loading = false;
        }
    }

    private void BeginNewEmployee()
    {
        _selectedEmployeeId = null;
        _employeesGrid.CurrentCell = null;
        _employeesGrid.ClearSelection();
        _fullName.Clear();
        _username.Clear();
        _password.Clear();
        _role.SelectedValue = "PHARMACIST";
        _isActive.Checked = true;
        _employeeMode.Text = "Cấp tài khoản mới";
        _password.PlaceholderText = "Bắt buộc với tài khoản mới";
    }

    private void SelectEmployee()
    {
        if (_loading || _employeesGrid.CurrentRow?.DataBoundItem is not EmployeeListRow employee)
            return;

        _selectedEmployeeId = employee.EmployeeId;
        _fullName.Text = employee.FullName;
        _username.Text = employee.Username;
        _password.Clear();
        _role.SelectedValue = employee.RoleId;
        _isActive.Checked = employee.IsActive;
        _employeeMode.Text = $"{employee.EmployeeId} ({employee.Username})";
        _password.PlaceholderText = "Để trống nếu không đổi mật khẩu";
    }

    private async Task SaveEmployeeAsync()
    {
        try
        {
            var user = RequireManager();
            string id = await _service.SaveEmployeeAsync(new EmployeeSaveModel
            {
                EmployeeId = _selectedEmployeeId,
                FullName = _fullName.Text,
                Username = _username.Text,
                RoleId = Convert.ToString(_role.SelectedValue) ?? "",
                Password = _password.Text,
                IsActive = _isActive.Checked
            }, user.EmployeeId, user.RoleId);
            MessageBox.Show($"Đã lưu tài khoản {id}.", "Nhân viên",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task ToggleEmployeeAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedEmployeeId))
        {
            MessageBox.Show("Hãy chọn một nhân viên trong danh sách.", "Nhân viên",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool newStatus = !_isActive.Checked;
        var confirm = MessageBox.Show(
            $"{(newStatus ? "Mở khóa" : "Khóa")} tài khoản {_selectedEmployeeId}?",
            "Xác nhận",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
            return;

        _isActive.Checked = newStatus;
        await SaveEmployeeAsync();
    }

    private async Task SaveLoyaltySettingsAsync()
    {
        try
        {
            var user = RequireManager();
            await _service.SaveLoyaltySettingsAsync(
                _earnPercent.Value,
                _moneyPerPoint.Value,
                user.EmployeeId,
                user.RoleId);
            MessageBox.Show("Đã lưu quy tắc tích điểm.", "Tích điểm",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task AddVoucherAsync()
    {
        var model = VoucherDialog.Create(this);
        if (model is null)
            return;

        try
        {
            var user = RequireManager();
            string id = await _service.AddVoucherAsync(model, user.EmployeeId, user.RoleId);
            MessageBox.Show($"Đã tạo voucher {model.VoucherCode} ({id}).", "Voucher",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            _vouchersGrid.DataSource = await _service.GetVouchersAsync(user.EmployeeId, user.RoleId);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task ToggleVoucherAsync()
    {
        if (_vouchersGrid.CurrentRow?.DataBoundItem is not VoucherRow voucher)
        {
            MessageBox.Show("Hãy chọn một voucher.", "Voucher",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool active = !voucher.IsActive;
        var confirm = MessageBox.Show(
            $"{(active ? "Bật" : "Tắt")} voucher {voucher.VoucherCode}?",
            "Xác nhận",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
            return;

        try
        {
            var user = RequireManager();
            await _service.SetVoucherActiveAsync(voucher.VoucherId, active, user.EmployeeId, user.RoleId);
            _vouchersGrid.DataSource = await _service.GetVouchersAsync(user.EmployeeId, user.RoleId);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static LoggedInUser RequireManager()
    {
        var user = SessionManager.CurrentUser;
        if (user is null || user.RoleId != "ADMIN")
            throw new UnauthorizedAccessException("Chức năng này chỉ dành cho Quản lý.");
        return user;
    }

    private static void ShowError(Exception ex) =>
        MessageBox.Show(ex.Message, "Không thể hoàn tất thao tác",
            MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static DataGridView CreateGrid() => new()
    {
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        ReadOnly = true,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        ColumnHeadersHeight = 34,
        RowTemplate = { Height = 30 }
    };

    private static void AddTextColumn(DataGridView grid, string property, string title, int minimumWidth)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = property,
            HeaderText = title,
            MinimumWidth = minimumWidth,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
    }

    private static Label Heading(string text) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        ForeColor = Color.FromArgb(35, 55, 80),
        AutoSize = true,
        Location = new Point(2, 9)
    };

    private static Button Button(string text, Color color) => new()
    {
        Text = text,
        BackColor = color,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
        Size = new Size(172, 34),
        Cursor = Cursors.Hand
    };

    private static void ConfigureTextBox(TextBox textBox)
    {
        textBox.Dock = DockStyle.Top;
        textBox.Font = new Font("Segoe UI", 9F);
    }

    private static void ConfigureDecimal(
        NumericUpDown input,
        decimal minimum,
        decimal maximum,
        decimal increment,
        int decimals)
    {
        input.Minimum = minimum;
        input.Maximum = maximum;
        input.Increment = increment;
        input.DecimalPlaces = decimals;
        input.Dock = DockStyle.Top;
        input.ThousandsSeparator = true;
    }

    private static Control Labeled(string label, Control control)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(3)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = Color.FromArgb(65, 75, 90)
        }, 0, 0);
        panel.Controls.Add(control, 0, 1);
        return panel;
    }

    private static Label Hint(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        ForeColor = Color.DimGray,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private sealed record RoleOption(string Id, string Label);

    private sealed class VoucherDialog : Form
    {
        private readonly TextBox _code = new();
        private readonly TextBox _description = new();
        private readonly ComboBox _type = new();
        private readonly NumericUpDown _value = new();
        private readonly NumericUpDown _minimum = new();
        private readonly NumericUpDown _maximumDiscount = new();
        private readonly NumericUpDown _usageLimit = new();
        private readonly CheckBox _hasMaximum = new() { Text = "Giới hạn mức giảm tối đa" };
        private readonly CheckBox _hasUsageLimit = new() { Text = "Giới hạn lượt sử dụng" };
        private readonly DateTimePicker _start = new();
        private readonly DateTimePicker _end = new();

        private VoucherDialog()
        {
            Text = "Tạo Voucher";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 540);
            Font = new Font("Segoe UI", 9F);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 2,
                RowCount = 10
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 10; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            ConfigureTextBox(_code);
            ConfigureTextBox(_description);
            _type.DropDownStyle = ComboBoxStyle.DropDownList;
            _type.Items.AddRange(["Số tiền", "Phần trăm"]);
            _type.SelectedIndex = 0;
            ConfigureDecimal(_value, 0.01M, 1000000000, 10000, 2);
            ConfigureDecimal(_minimum, 0, 1000000000, 10000, 2);
            ConfigureDecimal(_maximumDiscount, 0.01M, 1000000000, 10000, 2);
            ConfigureDecimal(_usageLimit, 1, 1000000000, 1, 0);
            _start.Format = DateTimePickerFormat.Short;
            _start.Value = DateTime.Today;
            _end.Format = DateTimePickerFormat.Short;
            _end.Value = DateTime.Today.AddDays(30);
            _hasMaximum.CheckedChanged += (_, _) => _maximumDiscount.Enabled = _hasMaximum.Checked;
            _hasUsageLimit.CheckedChanged += (_, _) => _usageLimit.Enabled = _hasUsageLimit.Checked;
            _maximumDiscount.Enabled = false;
            _usageLimit.Enabled = false;

            AddRow(layout, 0, "Mã Voucher", _code);
            AddRow(layout, 1, "Mô tả", _description);
            AddRow(layout, 2, "Kiểu giảm giá", _type);
            AddRow(layout, 3, "Giá trị giảm", _value);
            AddRow(layout, 4, "Đơn tối thiểu (VND)", _minimum);
            AddRow(layout, 5, "", _hasMaximum);
            AddRow(layout, 6, "Mức giảm tối đa", _maximumDiscount);
            AddRow(layout, 7, "Hạn sử dụng", _end);
            var usageLimitPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            usageLimitPanel.Controls.Add(_hasUsageLimit);
            usageLimitPanel.Controls.Add(_usageLimit);
            AddRow(layout, 8, "Lượt sử dụng", usageLimitPanel);
            AddRow(layout, 9, "Ngày bắt đầu", _start);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10, 5, 10, 5)
            };
            var save = Button("Tạo Voucher", Color.FromArgb(16, 63, 117));
            save.DialogResult = DialogResult.OK;
            var cancel = Button("Hủy", Color.FromArgb(100, 110, 125));
            cancel.DialogResult = DialogResult.Cancel;
            actions.Controls.Add(save);
            actions.Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;
            Controls.Add(layout);
            Controls.Add(actions);
        }

        public static VoucherSaveModel? Create(IWin32Window owner)
        {
            using var dialog = new VoucherDialog();
            if (dialog.ShowDialog(owner) != DialogResult.OK)
                return null;

            return new VoucherSaveModel
            {
                VoucherCode = dialog._code.Text,
                Description = dialog._description.Text,
                DiscountType = dialog._type.SelectedIndex == 0 ? "AMOUNT" : "PERCENT",
                DiscountValue = dialog._value.Value,
                MinimumOrder = dialog._minimum.Value,
                MaximumDiscount = dialog._hasMaximum.Checked ? dialog._maximumDiscount.Value : null,
                UsageLimit = dialog._hasUsageLimit.Checked ? decimal.ToInt32(dialog._usageLimit.Value) : null,
                StartDate = dialog._start.Value.Date,
                EndDate = dialog._end.Value.Date.AddDays(1).AddTicks(-1)
            };
        }

        private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, 0, row);
            layout.Controls.Add(control, 1, row);
        }
    }
}
