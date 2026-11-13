USE QuanLyQuayThuoc;
GO
/* Demo credentials. PBKDF2-SHA256 hashes generated for the PasswordHasher in BLL. */
UPDATE dbo.Employee SET PasswordHash = 'PBKDF2$120000$UVQtRGVtby1BZG1pbi1TYWx0$fbEAp7Pfw/A1jNQJERY3KhL9uS5UmquXWunveYd8AKY=' WHERE Username = 'admin';
UPDATE dbo.Employee SET PasswordHash = 'PBKDF2$120000$UVQtRGVtby1EdW9jU2ktU2FsdA==$mde+TbRduut1wjwHtNulKy349DI3yp6xwRkXmDCHQYU=' WHERE Username = 'duocsi01';
UPDATE dbo.Employee SET PasswordHash = 'PBKDF2$120000$UVQtRGVtby1LaG8tU2FsdA==$+UNpfsmEvUu49XNM7QwB+K+XwGnTJZKiJnBq+omaNP0=' WHERE Username = 'kho01';
GO
SELECT Username, FullName, RoleId FROM dbo.Employee;
GO
