-- ============================================================
-- DATABASE FIRST - Bước 1: tạo database bằng SQL thuần (DB có trước, chưa có entity C# nào).
-- Chạy: sqlcmd -S localhost -U sa -i DatabaseFirst/schema.sql
-- Sau đó dùng dotnet-ef scaffold để sinh ngược DbContext + entity (xem Program.cs).
-- ============================================================
IF DB_ID(N'ElectroShopDbFirst') IS NULL
    CREATE DATABASE ElectroShopDbFirst;
GO
USE ElectroShopDbFirst;
GO
IF OBJECT_ID(N'dbo.Products', N'U') IS NOT NULL DROP TABLE dbo.Products;
IF OBJECT_ID(N'dbo.Categories', N'U') IS NOT NULL DROP TABLE dbo.Categories;
GO
CREATE TABLE dbo.Categories (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Slug NVARCHAR(100) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1)
);
CREATE UNIQUE INDEX IX_Categories_Slug ON dbo.Categories(Slug);
CREATE TABLE dbo.Products (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    CategoryId INT NOT NULL CONSTRAINT FK_Products_Categories FOREIGN KEY REFERENCES dbo.Categories(Id),
    Name NVARCHAR(200) NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    StockQuantity INT NOT NULL CONSTRAINT DF_Products_Stock DEFAULT (0),
    ImageUrl NVARCHAR(500) NOT NULL CONSTRAINT DF_Products_Image DEFAULT (''),
    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT (SYSDATETIMEOFFSET())
);
CREATE INDEX IX_Products_CategoryId ON dbo.Products(CategoryId);
GO
INSERT INTO dbo.Categories (Name, Slug) VALUES (N'Laptop', N'laptop'), (N'Điện thoại', N'dien-thoai');
INSERT INTO dbo.Products (CategoryId, Name, Price, StockQuantity, ImageUrl) VALUES
 (1, N'Laptop Demo Database First', 12345000, 7, '/_content/ElectroShop.UiKit/images/products/asus-vivobook-15.svg'),
 (2, N'Điện thoại Demo Database First', 6789000, 9, '/_content/ElectroShop.UiKit/images/products/samsung-galaxy-a56.svg');
GO
