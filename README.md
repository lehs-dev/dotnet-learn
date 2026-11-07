# ElectroShop — học ASP.NET Core qua ba website

Solution .NET 10 / C# 14 gồm Razor Pages, Blazor Web App **Interactive Server**, MVC, dùng chung một ASP.NET Core Web API và cơ sở dữ liệu SQLite (**ElectroShop.db**) gọn nhẹ. Nội dung tiếng Việt, tiền VND, giao diện responsive thống nhất bằng UiKit. Không dùng React, Tailwind, EF InMemory hoặc dữ liệu giả ở frontend.

## Bắt đầu nhanh trong workspace hiện tại

.NET SDK 10 và Dev Container đã được cấu hình với SQLite. Tệp **`.env` cục bộ đã được tạo**, dùng cấu hình SQLite cùng JWT key/mật khẩu Admin. Xem `Seed__AdminEmail` và `Seed__AdminPassword` trong `.env` bằng IDE để đăng nhập; không đưa tệp này lên Git hoặc chia sẻ.

```bash
./scripts/run-all.sh
```

Script nạp `.env`, build toàn solution, khởi động API (Development tự migrate/seed), chờ `/health`, rồi mở ba frontend. Ctrl+C dừng cả bốn process. Log nằm `.local/logs/`. Nếu cổng bị chiếm, dừng phiên chạy cũ trước.

| Ứng dụng | URL |
|---|---|
| API | http://localhost:5100 |
| Razor Pages | http://localhost:5101 |
| Blazor Interactive Server | http://localhost:5102 |
| MVC | http://localhost:5103 |
| OpenAPI JSON | http://localhost:5100/openapi/v1.json |
| SQL health | http://localhost:5100/health |

VS Code Dev Container: mở tab **Ports**, forward5100,5101,5102,5103. Mở URL forwarded từ máy của bạn; typed clients vẫn gọi API từ bên trong container qua `Api:BaseUrl`. Không cần đổi `.devcontainer/devcontainer.json`. Script bind0.0.0.0 để hỗ trợ forward; launch profiles mặc định dùng localhost.

## Cấu trúc và vai trò

```text
.devcontainer/                  Cấu hình có sẵn, giữ nguyên
RazorPage/                      PageModel + .cshtml + handlers
MVC/                            Controller + View + ViewModel
BlazorWeb/                      Razor Components + interactive events
API/                            Identity/JWT, controllers, services, EF/SQL
  Authentication/               Cấp JWT
  Data/Configurations/          Schema/ràng buộc
  Data/Migrations/              InitialCreate + model snapshot
  Data/Seed/                     Seed idempotent
  Entities/                     Các thực thể database
  Interfaces/                   Hợp đồng service nghiệp vụ
  Middleware/                   Lỗi thống nhất
Shared/                         DTOs, requests, responses, enums
UiKit/wwwroot/                  CSS, JS nhỏ, SVG sản phẩm chung
Tests/                          xUnit unit + integration SQL thật
scripts/run-all.sh              Chạy/dừng bốn ứng dụng
ElectroShop.sln                  Một solution, bảy projects trực tiếp tại root
Mo-ta-du-an-ElectroShop.pdf     Tài liệu mô tả dự án đầy đủ (kiến trúc, database, API, kiểm thử)
DatabaseFirst/                  Minh chứng Database First: schema.sql + project scaffold từ DB
```

Frontend chỉ tham chiếu Shared+UiKit, gọi API qua HTTP; không tham chiếu API/DbContext/Entities. Tests tham chiếu API. Luồng tổng quát: **Browser → Frontend server → API → EF Core → SQLite**.

## Chức năng

- Popup đăng ký/đăng nhập, Identity hash mật khẩu; Customer mặc định, Admin cho quản trị; logout riêng từng frontend.
- Catalog trang chủ, tìm kiếm, lọc danh mục, sắp giá, phân trang/số kết quả; chi tiết, tồn kho, số lượng và sản phẩm liên quan.
- Giỏ hàng thật theo UserId trong SQL, thêm/cập nhật/xóa/xóa hết, tổng tiền; backend kiểm tra tồn kho/quyền sở hữu.
- Checkout thông tin nhận hàng và mô phỏng thành công/thất bại; order lưu snapshot tên/SKU/giá, lịch sử riêng tài khoản.
- Một trang `/admin`,5 tab: Dashboard; Đơn hàng; Sản phẩm và danh mục; Khách hàng; Tài khoản. Có tìm kiếm/phân trang, CRUD/ẩn sản phẩm, sửa hồ sơ, lịch sử khách, role và khóa/mở khóa.
- Chỉ Admin được gọi toàn bộ admin APIs. Không thể khóa/hạ quyền quản trị viên active cuối cùng. Token cũ bị vô hiệu khi khóa/đổi quyền.
- Transaction Serializable + RowVersion + unique indexes chống vượt kho/thanh toán hai lần; request lặp checkout/payment có key chống trùng.

## Cấu hình trên một máy/workspace mới

Cần .NET SDK 10, Bash/curl. CSDL SQLite được tích hợp sẵn dưới dạng file, không cần cài thêm SQL Server. EF CLI được quản lý bằng local tool manifest `dotnet-tools.json`.

```bash
cp .env.example .env
dotnet restore ElectroShop.sln
dotnet tool restore
```

Biến cấu hình quan trọng:

| Biến | Ý nghĩa |
|---|---|
| `ConnectionStrings__DefaultConnection` | Đường dẫn SQLite database, mặc định `Data Source=ElectroShop.db` |
| `Jwt__Key` | Bí mật ngẫu nhiên tối thiểu32 byte; `openssl rand -hex 48` tạo một giá trị riêng |
| `Jwt__Issuer` / `Jwt__Audience` | Mặc định ElectroShop.Api / ElectroShop.Frontends |
| `Seed__AdminEmail` / `Seed__AdminPassword` | Chỉ seed Admin trong Development; password >=8 gồm hoa/thường/số/ký tự đặc biệt |
| `ASPNETCORE_ENVIRONMENT` | Development để seed Admin và bật OpenAPI |
| `Api__BaseUrl` | API base URL cho frontend; mặc định http://localhost:5100/ |
| `Database__Initialize` | Development mặc định true, migrate+seed khi khởi động; production mặc định false |

`.env` dùng cú pháp Bash; đặt connection string/mật khẩu trong dấu nháy đơn (escape dấu nháy nếu giá trị có chứa nó). .NET **không tự đọc `.env`**: script run-all nạp giúp bạn. Khi chạy lệnh riêng phải nạp biến trước:

```bash
set -a
source .env
set +a
```

Có thể thay environment bằng ASP.NET Core user secrets cho API (`dotnet user-secrets init --project API`, sau đó `dotnet user-secrets set ... --project API`), không commit secrets. Hostname `db` phù hợp service hiện có trong docker-compose; chỉ dùng localhost khi mạng/container hiện tại cho phép. `TrustServerCertificate=true` phục vụ SQL development có chứng chỉ tự ký.

## Migrations và seed

```bash
set -a; source .env; set +a
dotnet tool restore
dotnet ef database update --project API --startup-project API
dotnet run --project API --no-launch-profile -- --seed-only
```

Migration đã được tạo trong `API/Data/Migrations`. Chỉ API quản lý schema. Seed chạy nhiều lần không trùng:7 danh mục,22 sản phẩm với SVG tĩnh từ UiKit,2 role, Admin Development từ cấu hình. Không ghi đè giá/tồn kho hiện có. Không seed đơn hàng giả; Dashboard dựa trên mua hàng thật. `--seed-only` tự migrate rồi seed và thoát.

Ứng dụng và integration tests dùng SQLite, không cần SQL Server credentials. Mỗi lần chạy test tạo một database SQLite riêng trong thư mục tạm và xóa sau khi hoàn tất; không dùng database hay tài khoản Admin trong `.env`.

## Chạy riêng từng ứng dụng

Mở bốn terminal, nạp `.env` tại mỗi terminal như trên rồi chạy:

```bash
dotnet run --project API --launch-profile http
dotnet run --project RazorPage --launch-profile http
dotnet run --project BlazorWeb --launch-profile http
dotnet run --project MVC --launch-profile http
```

Mỗi lệnh ở một terminal riêng; các launch profile `http` cố định cổng theo bảng. Hoặc dùng `--no-launch-profile --urls http://localhost:510X` với `ASPNETCORE_ENVIRONMENT=Development` đã export. Không chạy thêm các lệnh riêng khi run-all đang giữ cổng.

## Đăng nhập Admin và thử thanh toán

Mở popup **Đăng nhập**, dùng credentials Admin từ `.env`/secrets đã cấu hình. Link **Quản trị** xuất hiện sau đăng nhập; cả5 khu vực giữ trong `/admin`. Khách hàng tự đăng ký bằng popup, có thể đăng nhập cùng tài khoản ở cả ba website.

Thêm sản phẩm còn hàng vào giỏ → Thanh toán → nhập người nhận, điện thoại, địa chỉ → chọn mô phỏng:

1. **Thất bại**: ghi Payment Failed, giữ giỏ và tồn kho. Trang kết quả cho thử lại đúng đơn cũ.
2. **Thành công**: transaction trừ kho, Payment Success, cập nhật đơn và bỏ số lượng đã mua khỏi giỏ; hiển thị mã đơn.
3. Xem `/orders` ở frontend khác bằng cùng tài khoản để thấy cùng đơn và giá snapshot.

Không nhập thông tin thẻ; không có tiền thật. `CheckoutKey` và `AttemptKey` do frontend giữ cho lần submit. Nếu chưa nhận response, thử lại với key cũ; sau một lần Failed đã nhận kết quả, lần thử mới dùng AttemptKey mới trên cùng order. Đơn đã paid không trừ kho lại.

Trạng thái: Pending đã paid → Confirmed → Shipping → Completed. Chỉ Pending chưa paid được Cancelled. Dự án không có hoàn tiền, vì vậy không hủy đơn paid.

## Kiểm thử

```bash
dotnet build ElectroShop.sln
dotnet test ElectroShop.sln --no-build
```

Không cần nạp `.env` khi chạy kiểm thử. Unit và integration tests chạy trên SQLite thật, với database tạm riêng cho mỗi lượt chạy. Tên fixture/attribute `SqlServerFixture` / `SqlServerFact` được giữ từ phiên bản cũ; provider thực tế là SQLite và các test không bị bỏ qua khi thiếu connection string.

Bộ xUnit bao phủ giỏ hàng, tồn kho, tiền decimal, snapshot đơn, quyền sở hữu, phân quyền, thanh toán thất bại/thử lại, chống lặp và thanh toán đồng thời, RowVersion, truy vấn Khách hàng/Tài khoản trên SQLite, sắp xếp giá, CRUD danh mục/sản phẩm, khóa/mở khóa và thu hồi token. Xem phạm vi và cách kiểm chứng trong [KIEM-THU.md](KIEM-THU.md).

Kiểm tra trình duyệt thật trên cả ba frontend (Chromium qua Playwright):

```bash
python3 -m venv .local/venv
.local/venv/bin/pip install -r scripts/requirements-e2e.txt
.local/venv/bin/python -m playwright install --with-deps chromium
dotnet build ElectroShop.sln
.local/venv/bin/python scripts/check-system.py
```

Script tự chạy API và ba frontend ở các cổng trống, tạo database SQLite tạm cùng tài khoản riêng, rồi dừng các process do nó tạo. Không dùng `.env` hay thay đổi dữ liệu trong các ứng dụng đang chạy. Kết quả JSON, log và ảnh desktop/mobile nằm trong `.local/system-check/`.

## File nên mở để học

| Chức năng | Razor Pages | Blazor | MVC |
|---|---|---|---|
| Danh sách | `RazorPage/Pages/Index.cshtml.cs` + `.cshtml` | `BlazorWeb/Components/Pages/Home.razor` | `MVC/Controllers/HomeController.cs` + `Views/Home/Index.cshtml` |
| Giỏ | `RazorPage/Pages/Cart/Index.cshtml.cs` | `BlazorWeb/Components/Pages/Cart.razor` | `MVC/Controllers/CartController.cs` |
| Checkout | `RazorPage/Pages/Checkout/Index.cshtml.cs` | `BlazorWeb/Components/Pages/Checkout.razor` | `MVC/Controllers/CheckoutController.cs` |
| Gọi API | `RazorPage/Services/ShopApiClient.cs` | `BlazorWeb/Services/ShopApiClient.cs` | `MVC/Services/ShopApiClient.cs` |

Đọc tiếp tài liệu `Mo-ta-du-an-ElectroShop.pdf` ở root repo (kiến trúc, database Code First/Database First, danh sách API, kiểm thử), rồi `API/Services/OrderService.cs` cho transaction/idempotency.

## Admin tách riêng và Database First

- **Admin có cổng riêng `/admin`**: cả ba frontend đều có trang đăng nhập quản trị tại `/admin/login`, gọi endpoint riêng `POST api/auth/admin/login`. Tài khoản khách đăng nhập ở cổng thường (`/auth/login`); tài khoản Admin bị từ chối ở cổng khách và khách bị từ chối (403) ở cổng Admin. Đăng ký công khai luôn tạo vai trò Customer.
- **Tạo tài khoản Admin riêng** (không dùng chung dữ liệu khách): đặt `Seed__AdminEmail`/`Seed__AdminPassword` trong `.env` rồi chạy `dotnet run --project API -- --create-admin` — lệnh tạo/gán duy nhất vai trò Admin cho tài khoản đó rồi thoát.
- **Database First**: thư mục `DatabaseFirst/` chứa `schema.sql` (tạo database `ElectroShopDbFirst` bằng SQL thuần) và project console đã scaffold ngược thành entity + DbContext bằng `dotnet ef dbcontext scaffold`; chạy `dotnet run --project DatabaseFirst` để đọc dữ liệu thật từ database đó. Song song, luồng chính vẫn là **Code First** (migration `InitialCreate` + `dotnet ef database update`).

## Lỗi thường gặp và giới hạn

- **SQLite không mở được**: kiểm tra đường dẫn `Data Source`, thư mục cha và quyền đọc/ghi file database. `/health` trả503 khi database không sẵn sàng.
- **Thiếu Jwt__Key**: API cố ý từ chối khởi động nếu key dưới32 byte. Nạp `.env` ở đúng terminal hoặc cấu hình user secrets.
- **Không có Admin**: kiểm tra Development và cả hai biến Seed__Admin..., chạy seed-only. Seed không tự đổi mật khẩu của tài khoản đã tồn tại.
- **API-offline trên trang**: khởi động API trước; xác minh Api:BaseUrl trong appsettings/environment. Frontend hiển thị lỗi thay vì giả dữ liệu.
- **401 sau restart/đổi role/khóa**: đăng nhập lại. Store JWT ở RAM từng frontend mất khi restart; không có refresh token/distributed cache. Phiên kéo dài tối đa60 phút.
- **400 antiforgery**: tải lại trang/form, không xóa cookie thủ công giữa GET và POST. Các app dùng tên cookie riêng để cùng chạy localhost.
- **409 RowVersion/tồn kho/deadlock**: tải dữ liệu mới và thử lại đúng key thao tác khi chưa biết kết quả; không thay key để cố tạo lại đơn.
- **Blazor ngắt kết nối**: reload rồi đăng nhập lại nếu session hết hạn. Không gọi SignInAsync từ interactive event.
- **Ảnh là SVG minh họa** theo danh mục, không phải ảnh hãng/ảnh sản phẩm thực. Giá sản phẩm là dữ liệu học tập.
- **Phạm vi demo**: một instance mỗi frontend, không refresh token, không gửi email, không thanh toán/hoàn tiền thật, không lưu file upload. Lịch sử khách hàng admin lấy100 đơn gần nhất. Đơn Pending không giữ hàng hoặc tự hết hạn; giá snapshot được giữ cho lần trả tiền sau.
