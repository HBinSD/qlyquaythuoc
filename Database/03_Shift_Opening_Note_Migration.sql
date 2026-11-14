USE QuanLyQuayThuoc;
GO

IF COL_LENGTH(N'dbo.WorkShift', N'OpeningNote') IS NULL
    ALTER TABLE dbo.WorkShift ADD OpeningNote nvarchar(500) NULL;
GO
