# Kiểm chứng các luồng ElectroShop

Ngày kiểm chứng: **07/11/2026**. Nền tảng: .NET 10, SQLite thật, Chromium qua Playwright.

## Kết quả

- `dotnet build ElectroShop.sln`: **0 lỗi, 0 cảnh báo**.
- `dotnet test ElectroShop.sln --no-build`: **51/51 đạt, không bỏ qua test**.
- `scripts/check-system.py`: **61/61 nhóm luồng trình duyệt đạt** — Razor Pages, Blazor Interactive Server và MVC.
- Kiểm tra responsive tại **390px, 768px, 1440px**, xác nhận trang không tràn ngang. Bảng dữ liệu có vùng cuộn riêng trên màn hình hẹp.

Các con số này thể hiện phạm vi đã kiểm chứng dưới đây, không phải thước đo bao phủ toàn bộ dòng code hay mọi tổ hợp dữ liệu.

## Ma trận chức năng

| Luồng | Kiểm chứng API / nghiệp vụ | Kiểm chứng giao diện trên cả ba frontend |
|---|---|---|
| Đăng ký | Customer mặc định, không có quyền Admin | Xác nhận mật khẩu, gửi form, quay lại giỏ hàng |
| Đăng nhập / đăng xuất | JWT, tách cổng khách / quản trị, 401 / 403 | Báo mật khẩu sai ngay trong popup, giữ trang cần quay lại, đăng xuất / đăng nhập lại |
| Catalog | Giá tăng / giảm trên SQLite, tìm tên / SKU, danh mục ẩn | Ảnh thật từ UiKit, tìm kiếm, trạng thái không có kết quả, phân trang |
| Chi tiết sản phẩm | Sản phẩm đang bán, không hiển thị sản phẩm / danh mục đã ẩn | Mở chi tiết, chọn số lượng, thêm giỏ |
| Giỏ hàng | Cộng / thay số lượng, tổng decimal, kiểm tra tồn và quyền sở hữu | Thêm, cập nhật, xóa một sản phẩm, xóa toàn bộ, trạng thái trống |
| Checkout | Snapshot giá / tên / SKU, kiểm tra tồn, key chống tạo đơn lặp | Điền thông tin nhận hàng, chọn kết quả mô phỏng |
| Thanh toán thất bại / thử lại | Giữ giỏ và kho khi thất bại, thử lại cùng đơn | Mô phỏng thất bại rồi thành công; không tạo đơn thứ hai |
| Thanh toán đồng thời | Không trừ kho hai lần; hai người tranh sản phẩm cuối chỉ một người thành công | Gửi form giữ key và giá trị nút thanh toán |
| Lịch sử đơn | Cách ly tài khoản, snapshot không đổi khi sửa giá | Xem đơn đã mua; quay lại lịch sử sau đăng nhập |
| Dashboard | Tổng sản phẩm, khách, đơn, doanh thu từ database | Tải đủ năm chỉ số |
| Quản trị sản phẩm | CRUD, RowVersion, ẩn / khôi phục | Tạo, sửa giá / tồn, ẩn, khôi phục và phân trang |
| Quản trị danh mục | CRUD và ẩn / khôi phục cả nhóm sản phẩm | Tạo, ẩn, đổi tên và khôi phục |
| Quản trị khách hàng | Truy vấn SQLite, role, số đơn / tổng chi tiêu, tìm theo điện thoại, phân trang | Tìm email, sửa hồ sơ, mở lịch sử mua hàng |
| Xử lý đơn hàng | Chặn chuyển trạng thái không hợp lệ; hủy đơn chưa trả tiền không đổi kho / giỏ | Xác nhận → Đang giao → Hoàn thành |
| Quản trị tài khoản | Khóa / mở, đổi role, thu hồi token cũ, bảo vệ Admin cuối cùng | Khóa / mở, nâng / hạ quyền, cổng đăng nhập tương ứng, chặn khóa Admin cuối |
| Lưu quyền không thay đổi | Giữ nguyên SecurityStamp và phiên đang dùng | Lưu chính tài khoản Admin rồi tiếp tục dùng quản trị |
| API ngừng hoạt động | Client bắt lỗi kết nối | Cả ba frontend hiển thị lỗi có thể xử lý, không vỡ trang |
| Mua lượt mới trên Blazor | CheckoutKey riêng theo lượt | Sau thanh toán, thêm giỏ và mở checkout mới trong cùng circuit |

## Các lỗi đã sửa

1. `/api/admin/customers` và `/api/admin/accounts` trả 500 do SQLite không hỗ trợ `ORDER BY DateTimeOffset`. Danh sách hiện sắp tên và ID ổn định, phân trang ngay tại database.
2. Sắp xếp giá trả lỗi vì SQLite lưu `decimal` dạng TEXT. Chỉ khóa sắp xếp được ép kiểu số; giá trả về và toàn bộ tính toán tiền vẫn dùng `decimal`.
3. Lưu quyền Admin dù không đổi gì vẫn thay SecurityStamp, làm mất hiệu lực phiên hiện tại. Thao tác không đổi dữ liệu hiện giữ phiên.
4. `<select>` bool của Blazor không áp dụng đúng lựa chọn thanh toán thất bại. Lựa chọn hiện dùng giá trị rõ ràng rồi chuyển thành kết quả thanh toán khi gửi API.
5. Blazor giữ dữ liệu đơn cũ khi vào checkout mới trong cùng circuit. Trạng thái được tạo lại khi tham số trang đổi.
6. Đăng nhập không trở về trang được bảo vệ; lỗi đăng nhập Razor nằm sau popup; MVC báo thanh toán thất bại bằng thông báo thành công. Các trạng thái này đã được chỉnh và kiểm tra trên trình duyệt.
7. Lưu dữ liệu quản trị làm mất bộ lọc / trang đang xem. Form Razor Pages và MVC hiện giữ đường dẫn quản trị hợp lệ khi quay lại.
8. Giỏ có sản phẩm ngừng bán hoặc không đủ tồn vẫn hiện link checkout ở Razor Pages / MVC. Giao diện hiện yêu cầu sửa giỏ trước; API tiếp tục kiểm tra độc lập.
9. Test cũ có thể dùng database trong environment. Test mới luôn dùng SQLite tạm riêng, không sửa dữ liệu đang sử dụng.

## Chạy lại

```bash
dotnet build ElectroShop.sln
dotnet test ElectroShop.sln --no-build
python3 -m venv .local/venv
.local/venv/bin/pip install -r scripts/requirements-e2e.txt
.local/venv/bin/python -m playwright install --with-deps chromium
.local/venv/bin/python scripts/check-system.py
```

Script trình duyệt tự tạo database, tài khoản và cổng riêng; không đọc `.env`, không dùng dữ liệu vận hành. Sau khi chạy, script dừng đúng những process nó tạo và xóa database tạm. Bước kiểm tra API offline chỉ dừng API của lượt test đó.

- Kết quả từng nhóm: `.local/system-check/results.json`.
- Log xUnit và lượt trình duyệt trong workspace hiện tại: `.local/logs/tests.log`, `.local/logs/browser-check.log`.
- Ảnh: `.local/system-check/{RazorPage,BlazorWeb,MVC}-customers-desktop.png`, `-customers-390.png`, `-customers-768.png`, `-catalog.png`, `-catalog-mobile.png`.
- Phạm vi hiện có của sản phẩm: thanh toán sandbox; không gửi email hay xử lý thanh toán / hoàn tiền thật.
