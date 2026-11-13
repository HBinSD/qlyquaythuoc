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
- Các mục thuốc, kho, nhà cung cấp, bán thuốc, hóa đơn, nhân viên, báo cáo có khung điều hướng ban đầu.

## Cấu hình
Mở `QuanLyQuayThuoc.GUI/appsettings.json`, sửa `ConnectionStrings:Default` nếu SQL Server của bạn không dùng `localhost` hoặc không dùng Windows Authentication.

Ví dụ dùng SQL Login:
`Server=localhost;Database=QuanLyQuayThuoc;User Id=sa;Password=YourPassword;TrustServerCertificate=True;`

## Khởi tạo database
1. Chạy file `01_QuanLyQuayThuoc_Schema_Seed.sql` trong gói Giai đoạn 1 để tạo database `QuanLyQuayThuoc`.
2. Chạy `Database/00_Set_Demo_Passwords.sql` trong gói dự án này.
3. Tài khoản demo:
   - `admin` / `Admin@123`
   - `duocsi01` / `Ds@123`
   - `kho01` / `Kho@123`

## Mở dự án
Mở `QuanLyQuayThuoc.sln` bằng Visual Studio 2026 (cài workload .NET desktop development và .NET 10 SDK), sau đó đặt `QuanLyQuayThuoc.GUI` làm Startup Project và nhấn F5.

## Ghi chú
- Chưa triển khai CRUD thuốc, nhập kho, bán hàng hoặc in hóa đơn trong đợt 1; các mục đó đang là khung màn hình.
- Hash demo dùng PBKDF2 với salt riêng cho từng tài khoản. Khi phát hành thật, bắt buộc đổi mật khẩu demo.
- Dashboard query giả định cấu trúc bảng đúng với schema Giai đoạn 1.
