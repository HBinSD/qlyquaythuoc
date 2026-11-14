# MediStore – Quản lý quầy thuốc (đợt 1)

## Công nghệ
- C# WinForms, .NET 10
- SQL Server 2022
- ADO.NET qua `Microsoft.Data.SqlClient`
- Kiến trúc 3 lớp: GUI – BLL – DAL

## Đợt này đã có
- Form đăng nhập.
- Kiểm tra kết nối SQL Server.
- Xác thực tài khoản bằng PBKDF2-SHA256.
- Form chính theo phong cách MISA eShop: Sidebar xanh đậm, thanh tiêu đề, vùng nội dung.
- Phân quyền hiển thị menu theo 3 vai trò.
- Dashboard đọc số liệu thực từ SQL Server.
- Quản lý thuốc và nhà cung cấp; tra cứu tồn kho, lô thuốc, hạn dùng và phiếu nhập.
- Mở/chốt ca bán hàng với tiền bàn giao, đối soát tiền mặt, chặn hóa đơn nháp còn tồn và xem trước phiếu kết toán.
- POS tra cứu/tạo khách hàng, tiếp nhận đơn thuốc từ giỏ hàng, áp dụng điểm hoặc voucher, giữ hàng theo FEFO tối đa 30 phút, thanh toán đủ tiền và hủy hóa đơn nháp.
- Tra cứu hóa đơn, báo cáo doanh thu.
- Màn hình Quản lý để cấp/khóa tài khoản và phân vai trò nhân viên, cấu hình tích điểm/voucher, duyệt kiểm kê/xuất hủy và hủy hóa đơn đã thanh toán.
- Hủy hóa đơn đã thanh toán có lưu lý do/nhật ký, hoàn tồn theo đúng lô, xử lý điểm và cập nhật sổ quỹ.
- Dược sĩ/Quản lý có mục Đổi / Trả hàng để tra cứu hóa đơn trong 7 ngày, xác nhận điều kiện hàng trả, chọn số lượng/phương thức hoàn và ghi nhận hoàn kho, tiền, điểm, ca và nhật ký trong transaction.
- Nhân viên kho lập đề xuất kiểm kê/xuất hủy; tồn kho chỉ đổi sau khi Quản lý duyệt.

## Cấu hình
Mở `QuanLyQuayThuoc.GUI/appsettings.json`, sửa `ConnectionStrings:Default` nếu SQL Server của bạn không dùng `localhost` hoặc không dùng Windows Authentication.

Ví dụ dùng SQL Login:
`Server=localhost;Database=QuanLyQuayThuoc;User Id=sa;Password=YourPassword;TrustServerCertificate=True;`

## Khởi tạo database
1. Với database mới, chạy `Database/01_QuanLyQuayThuoc_Schema_Seed.sql` để tạo database và dữ liệu mẫu. **Không chạy lại script này trên database đang sử dụng** vì script xóa và tạo lại các bảng.
2. Chạy `Database/00_Set_Demo_Passwords.sql` để đặt mật khẩu cho tài khoản demo.
3. Chạy `Database/02_Admin_Features_Migration.sql` để tạo cấu hình tích điểm mặc định (1% tích điểm, 1 điểm = 100 VND). Có thể chạy lại migration này an toàn.
4. Chạy `Database/03_Shift_Opening_Note_Migration.sql` để thêm ghi chú mở ca. Có thể chạy lại migration này an toàn.
5. Tài khoản demo:
   - `admin` / `Admin@123`
   - `duocsi01` / `Ds@123`
   - `kho01` / `Kho@123`

## Mở dự án
Mở `QuanLyQuayThuoc.sln` bằng Visual Studio 2026 (cài workload .NET desktop development và .NET 10 SDK), sau đó đặt `QuanLyQuayThuoc.GUI` làm Startup Project và nhấn F5.

## Ghi chú
- Hash demo dùng PBKDF2 với salt riêng cho từng tài khoản. Khi phát hành thật, bắt buộc đổi mật khẩu demo.
- Dashboard query giả định cấu trúc bảng đúng với schema Giai đoạn 1.
- Hủy hóa đơn quản lý chỉ áp dụng cho hóa đơn `PAID`; mọi thao tác tồn kho/điểm/voucher/sổ quỹ liên quan được thực hiện trong transaction.
- Quy trình đổi thuốc hiện được thao tác thành phiếu trả và hóa đơn bán mới riêng; database chưa có liên kết/đối trừ tự động giữa hai chứng từ.
- Thanh toán chuyển khoản/QR hiện ghi nhận xác nhận của nhân viên và mã giao dịch tùy chọn; chưa kết nối cổng thanh toán hoặc tự sinh QR ngân hàng.
