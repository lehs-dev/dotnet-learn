# Kết quả kiểm chứng thực tế

Ngày kiểm tra: **26/10/2026 (Asia/Ho_Chi_Minh)**, trong Dev Container hiện tại. Đây là kết quả đã chạy, không phải danh sách dự kiến.

## Build và database

| Kiểm tra | Kết quả |
|---|---|
| `dotnet --info` | SDK10.0.401, runtime10.0.12, Linux Ubuntu24.04 |
| `dotnet build ElectroShop.sln --no-restore` | Thành công,0 warning,0 error |
| SQL hiện có | TCP1433 qua db/localhost; EF kết nối thành công bằng cấu hình container |
| Database | ElectroShopDb trên SQL Server hiện có; không tạo SQL container khác |
| `dotnet ef database update --project API --startup-project API` | InitialCreate đã áp dụng thành công |
| `dotnet ef migrations has-pending-model-changes ...` | Không có thay đổi model chưa migrate |
| Seed |7 danh mục,22 sản phẩm mẫu,2 role, Admin Development từ `.env` cục bộ |
| Chạy seed lại | Catalog trước/sau giống nhau; không reset giá/tồn và không trùng dữ liệu |
| API `/health` |200, status Healthy, database ElectroShopDb |
| OpenAPI `/openapi/v1.json` |200; JSON endpoint được tạo thực tế |
| Static UiKit | CSS/JS/SVG trả200 ở cả5101,5102,5103 |
| NuGet API audit gồm transitive | Không báo package có advisory sau khi nâng dependency Pkcs lên10.0.14 |

Thư mục `.devcontainer` được giữ nguyên. Connection string lấy từ cấu hình SQL có sẵn và đặt trong `.env` gitignore, quyền600; JWT key và mật khẩu Admin được sinh cho cục bộ, không đưa vào source.

## Kiểm thử tự động

Lệnh chạy cuối:

```bash
set -a; source .env; set +a
dotnet build ElectroShop.sln --no-restore --verbosity quiet
dotnet test ElectroShop.sln --no-build --no-restore \
  --logger trx --results-directory .local/test-results --verbosity quiet
```

**36 passed,0 failed,0 skipped**, gồm28 unit/theory cases và8 integration scenarios chạy SQL Server thật. TRX lưu trong `.local/test-results/` (ignored).

Các trường hợp đã xác minh:

- Cộng/thay số lượng giỏ; từ chối số lượng sai/vượt tồn; tổng decimal đúng.
- Giỏ riêng theo user, JWT/me đúng user, Customer nhận403 từ toàn bộ nhóm Admin.
- Tạo đơn snapshot và tổng tiền, chủ khác nhận404 cho đơn/payment.
- Payment Failed giữ kho/giỏ; thử lại cùng đơn có thể Success.
- Repeated CheckoutKey/AttemptKey không tạo thêm đơn/payment hoặc trừ kho thêm.
- Hai request trả tiền cùng đơn chỉ tạo một Success.
- Hai đơn tranh đơn vị hàng cuối chỉ có một đơn được thanh toán; tồn không âm.
- Mất kho trước khi trả tiền được lưu Failed mà không xóa giỏ.
- Admin sửa giá; catalog đổi, đơn cũ giữ tên/giá snapshot; RowVersion cũ nhận409.
- Trạng thái đơn bất hợp lệ bị từ chối; unit guard bảo vệ quản trị viên cuối cùng.

Hai bài concurrency còn được chạy riêng thêm một lần và đều pass. Trong quá trình kiểm tra đã phát hiện EF bọc SQL deadlock1205 trong InvalidOperationException; middleware nay tìm inner exception và trả409 để retry an toàn thay vì500. Không bật retry database tự động quanh transaction thủ công.

## Kiểm tra frontend bằng HTTP thật

Dùng HTTP client có cookie jar, lấy antiforgery từ HTML trước khi POST và gọi API/SQL thật. Đây là kiểm tra form và nội dung render, **không phải tự động hóa trình duyệt**.

| Phạm vi | Đã đạt |
|---|---|
| Trang chủ cả3 |200; danh sách12 slug trang đầu khớp nhau |
| Razor Pages | Register/login/logout, CSRF400, phân trang, cart add/update/oversell, checkout thất bại→retry Success, orders, Admin5 tabs, product/category create/edit/soft-hide, profile/history/lock và chuyển trạng thái đơn |
| MVC | Register/login/logout, CSRF400, cart, failed→same-order success, orders, Admin5 tabs, category/product CRUD, giá decimal, profile, lock→token cũ401/unlock, role Admin↔Customer, guard Admin cuối cùng409, trạng thái hợp lệ |
| Blazor HTTP auth và SSR | Register/login/logout, cookie riêng, bảo vệ cart/checkout/orders/admin, Customer bị chặn admin; cả5 panel query tab render200, không có lỗi API |
| Dùng chung tài khoản/đơn | Đăng ký và mua thật bằng form Razor Pages; cùng credentials đăng nhập MVC và Blazor; cả hai `/orders` render đúng mã đơn đã paid cùng snapshot sản phẩm và thông tin giao hàng |

Script/report QA riêng nằm `.local/` và được gitignore; credentials kiểm tra không nằm trong tài liệu. Các fixture xUnit được dọn theo chính ID test, không drop database. HTTP smoke có tạo đơn sandbox thật để kiểm tra Dashboard; một số sản phẩm/danh mục QA đã được ẩn và tài khoản QA đã khóa. Tài khoản dùng kiểm chứng liên thông được giữ active cục bộ.

## Phần chưa kiểm chứng bằng trình duyệt

Môi trường phiên này không có công cụ browser automation. Chưa chạy click/keyboard/viewport/screenshot hay thao tác event Blazor qua browser/SignalR. Vì vậy không tuyên bố đã kiểm thử UI hoàn chỉnh hoặc so sánh hình ảnh pixel. Responsive CSS và native interactive events đã được triển khai, nhưng cần chạy checklist thủ công `MANUAL-TEST.md`, đặc biệt bước mua hàng bằng Blazor rồi cập nhật giá từ Admin Razor và xem lại ở cả ba website.

Các giới hạn thiết kế: token store một instance ở RAM, hết phiên sau restart/60 phút, không refresh token; sandbox không có hoàn tiền; không hủy đơn paid; pending không giữ kho/tự hết hạn; ảnh SVG minh họa. Xem README và ARCHITECTURE.md để hiểu lựa chọn cho dự án học tập.
