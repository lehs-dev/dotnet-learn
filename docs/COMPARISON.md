# So sánh Razor Pages, MVC và Blazor bằng source ElectroShop

Ba frontend sử dụng cùng API, DTO, SQL Server và CSS. Mỗi frontend tự viết phần hiển thị và điều phối HTTP theo framework của mình. Quy tắc tồn kho, quyền sở hữu giỏ/đơn, tính tiền và xử lý thanh toán nằm trong `API/Services/`; sửa quy tắc ở backend sẽ áp dụng cho cả ba website.

Để học, mở cùng một hàng trong bảng dưới đây ở ba cột và chạy ba website cạnh nhau.

## Bản đồ source tương đương

| Chức năng | Razor Pages — `RazorPage/` | MVC — `MVC/` | Blazor — `BlazorWeb/` |
|---|---|---|---|
| Danh sách và tìm kiếm | `Pages/Index.cshtml`, `Pages/Index.cshtml.cs` | `Controllers/HomeController.cs`, `Views/Home/Index.cshtml`, `ViewModels/ShopViewModels.cs` | `Components/Pages/Home.razor` |
| Card sản phẩm | `Pages/Shared/_ProductCard.cshtml` | `Views/Shared/_ProductCard.cshtml` | `Components/Shared/ProductCard.razor` |
| Chi tiết theo slug | `Pages/Products/Detail.cshtml`, `.cshtml.cs` | `Controllers/ProductsController.cs`, `Views/Products/Detail.cshtml` | `Components/Pages/Product.razor` |
| Giỏ hàng | `Pages/Cart/Index.cshtml`, `.cshtml.cs` | `Controllers/CartController.cs`, `Views/Cart/Index.cshtml` | `Components/Pages/Cart.razor` |
| Checkout và thử lại thanh toán | `Pages/Checkout/Index.cshtml`, `.cshtml.cs` | `Controllers/CheckoutController.cs`, `Views/Checkout/Index.cshtml` | `Components/Pages/Checkout.razor` |
| Lịch sử đơn hàng | `Pages/Orders/Index.cshtml`, `.cshtml.cs` | `Controllers/OrdersController.cs`, `Views/Orders/Index.cshtml` | `Components/Pages/Orders.razor` |
| Một trang quản trị, 5 tab | `Pages/Admin/Index.cshtml`, `.cshtml.cs` | `Controllers/AdminController.cs`, `Views/Admin/Index.cshtml` | `Components/Pages/Admin.razor`, `Components/Admin/*Panel.razor` |
| Layout và modal đăng nhập | `Pages/Shared/_Layout.cshtml` | `Views/Shared/_Layout.cshtml` | `Components/Layout/MainLayout.razor` |
| Đăng nhập/đăng ký/đăng xuất | `Pages/Auth/SignIn.cshtml.cs` | `Controllers/AuthController.cs` | `Services/AuthEndpoints.cs` |
| Gọi API | `Services/ShopApiClient.cs` | `Services/ShopApiClient.cs` | `Services/ShopApiClient.cs` |
| Lưu JWT trên server | `Services/TokenStore.cs` | `Services/TokenStore.cs` | `Services/SessionTokenStore.cs` |
| Đăng ký DI, cookie, routes | `Program.cs` | `Program.cs` | `Program.cs`, `Components/Routes.razor` |

Các đường dẫn trong bảng tính từ thư mục gốc workspace. Tên `Index` xuất hiện nhiều lần vì mỗi thư mục Razor Pages tương ứng một trang.

## 1. Render danh sách sản phẩm

**Razor Pages:** PageModel lấy dữ liệu trước, `.cshtml` duyệt `Model.Products.Items`. Ví dụ từ `Pages/Index.cshtml`:

```cshtml
@foreach (var product in Model.Products.Items)
{
    <partial name="_ProductCard" model="product" />
}
```

**MVC:** `HomeController.Index` tạo `CatalogViewModel` rồi gọi `return View(model)`. View đọc `@model CatalogViewModel`; card vẫn là partial Razor, nhưng việc điều phối request nằm trong controller riêng.

**Blazor:** `Home.razor` có dữ liệu `products` và gọi component con:

```razor
<ProductCard Product="product" OnAdd="AddToCart" Disabled="busy" />
```

`ProductCard` nhận tham số và `EventCallback<int>`. Khi component thay đổi state sau event, Blazor cập nhật UI qua kết nối Interactive Server.

## 2. Tìm kiếm, lọc, sắp xếp và phân trang

Cả ba gọi `GET /api/products` bằng `q`, `categoryId`, `sort`, `page`, `pageSize`. API thực hiện lọc/phân trang trong SQL; frontend nhận `PagedResult<ProductDto>`.

Razor Pages dùng query binding ngay trong PageModel:

```csharp
[BindProperty(SupportsGet = true)]
public string? Q { get; set; }
```

Form `method="get"` submit tới `/`, rồi `OnGetAsync` chạy lại.

MVC nhận query qua tham số action:

```csharp
public async Task<IActionResult> Index(
    string? q, int? categoryId, string? sort,
    int page = 1, CancellationToken ct = default)
```

Blazor bind ô nhập bằng `@bind`, bind query bằng `[SupplyParameterFromQuery]` và xử lý form bằng event:

```razor
<form class="filters" @onsubmit="ApplyFilters" @onsubmit:preventDefault>
```

`GoToPage` cập nhật URL qua `Navigation.NavigateTo(...)`; `OnParametersSetAsync` nạp dữ liệu phù hợp. URL giữ các bộ lọc nên có thể lưu bookmark hoặc gửi cho người khác.

## 3. Submit form và validation

| | Razor Pages | MVC | Blazor Interactive Server |
|---|---|---|---|
| Điểm tiếp nhận | `OnPostCreateAsync`, `OnPostUpdateAsync`… | Action có `[HttpPost]` | Event `Submit`, `Pay`, `AddToCart`… |
| Lấy dữ liệu | `[BindProperty]`, tham số handler | Model binding vào request/ViewModel | `@bind` hoặc `@bind-Value` vào model của component |
| Kiểm tra annotations | `ModelState.IsValid` | `ModelState.IsValid` | `DataAnnotationsValidator`, `OnValidSubmit` |
| Hiển thị validation | Tag Helpers / thông báo của PageModel | `asp-validation-summary` / lỗi trong ViewModel, TempData | `ValidationSummary`, `ValidationMessage` |
| Sau khi thành công | Redirect về GET | Redirect về GET | Cập nhật state hoặc điều hướng tới đơn vừa tạo |

DTO form dùng chung trong `Shared/Requests/Requests.cs`. Ví dụ `CheckoutRequest.ShippingAddress` có `[Required]`, `[StringLength]`. API luôn kiểm tra lại; ràng buộc HTML như `required`, `min`, `max` hỗ trợ thao tác trên trình duyệt.

Ví dụ checkout Blazor:

```razor
<EditForm Model="checkout" OnValidSubmit="Submit" FormName="checkout">
    <DataAnnotationsValidator />
    <ValidationSummary />
    ...
</EditForm>
```

Form cookie qua HTTP có antiforgery token. Razor Pages bảo vệ form theo cơ chế mặc định; MVC bật `AutoValidateAntiforgeryTokenAttribute` toàn ứng dụng. Endpoint đăng nhập Blazor gọi `IAntiforgery.ValidateRequestAsync` trước khi xử lý form. Event của Interactive Server chạy trong circuit đã được framework quản lý.

## 4. Gọi Web API và giữ token đúng người dùng

Mỗi frontend có `Services/ShopApiClient.cs`. Client tạo `HttpRequestMessage`, serialize DTO bằng `System.Text.Json`, xử lý lỗi ProblemDetails rồi ném `ApiException` chứa thông báo cho UI. `Api:BaseUrl` ở configuration xác định địa chỉ backend.

Razor Pages và MVC đang xử lý request HTTP nên client lấy session claim từ `IHttpContextAccessor.HttpContext.User`.

Blazor lấy user từ `AuthenticationStateProvider` của circuit hiện tại:

```csharp
var user = (await authentication.GetAuthenticationStateAsync()).User;
var session = tokens.Get(user.FindFirst("session_id")?.Value);
```

Sau đó client đặt bearer token trên **request hiện tại**:

```csharp
request.Headers.Authorization =
    new AuthenticationHeaderValue("Bearer", session.AccessToken);
```

Không đặt token của một khách hàng vào `DefaultRequestHeaders` dùng chung. Không frontend nào truy cập `AppDbContext`, EF entity hoặc SQL Server trực tiếp.

## 5. State ở đâu?

| Dữ liệu | Razor Pages / MVC | Blazor |
|---|---|---|
| Bộ lọc, số trang | Query string | Query string và field component |
| Dữ liệu để render | PageModel / ViewModel tạo cho mỗi HTTP request | Field component trong circuit |
| Thông báo sau POST | TempData rồi redirect GET | Field `error`, `message`; tự render lại sau event |
| Đang chờ API | Request HTTP đang xử lý | Field `busy`, `loading`, nút tạm khóa |
| Giỏ hàng, đơn hàng, tồn kho | SQL Server qua API | SQL Server qua API |
| JWT | TokenStore trên server | SessionTokenStore trên server |

Nếu frontend khởi động lại, token store mất và người dùng cần đăng nhập lại. Giỏ hàng/đơn hàng vẫn còn trong SQL Server. Đây là lựa chọn đơn giản cho demo học tập, chưa phải kho phiên dùng chung cho nhiều server.

## 6. Đăng nhập và đăng xuất

Luồng chung:

1. Browser mở modal trong layout, gửi form POST có antiforgery tới frontend hiện tại.
2. Frontend gọi `/api/auth/login` hoặc `/api/auth/register`.
3. API dùng Identity và trả JWT cùng thông tin user/role.
4. Frontend lưu JWT trong token store, tạo session id ngẫu nhiên và authentication cookie HttpOnly.
5. Sau redirect/reload, các API call lấy token theo đúng session id.

Razor Pages dùng PageModel `Pages/Auth/SignIn.cshtml.cs`. MVC dùng `AuthController`. Blazor dùng các HTTP endpoint trong `Services/AuthEndpoints.cs` để gọi `SignInAsync` khi response HTTP còn có thể ghi cookie; component không gọi `SignInAsync` trong `@onclick`.

Ba authentication cookie có tên riêng. Không dùng `localStorage` hoặc `sessionStorage` để lưu JWT. Đăng xuất xóa phiên frontend hiện tại rồi trở về `/`; tài khoản và dữ liệu ở API vẫn dùng chung giữa ba website.

## 7. Thêm sản phẩm vào giỏ

**Razor Pages:** card submit form tới page handler `OnPostAddAsync` của trang chủ. Handler gọi `api.PostAsync(..., new CartQuantityRequest { ... })` rồi redirect về trang hợp lệ.

**MVC:** card submit `POST /cart/add`. `CartController.Add` nhận `CartQuantityRequest`, kiểm tra `ModelState`, gọi API rồi redirect. Route trở về được kiểm tra bằng `Url.IsLocalUrl`.

**Blazor:** nút gọi `AddToCart` qua `EventCallback`; trong `Home.razor`:

```csharp
await Api.PostAsync("api/cart/items",
    new CartQuantityRequest { ProductId = id, Quantity = 1 });
message = "Đã thêm sản phẩm vào giỏ hàng.";
```

API tự lấy UserId từ JWT và kiểm tra tồn kho. Không frontend nào truyền UserId để chọn giỏ người khác. Số lượng, đơn giá và tổng tiền hiển thị lại từ API.

## 8. Modal và giao diện dùng chung

Modal là phần tử HTML `<dialog>` nằm trong layout, không có trang đăng nhập riêng cho người dùng. Razor Pages/MVC dùng `data-open-dialog`, `data-close-dialog`; `UiKit/wwwroot/js/shop.js` xử lý đóng/mở. Blazor gọi JS interop trong `MainLayout.razor` để mở/đóng dialog từ event component.

CSS tại `UiKit/wwwroot/css/shop.css`; frontend dùng đường dẫn static web asset `/_content/ElectroShop.UiKit/css/shop.css`. Card dùng chung design tokens, ảnh SVG và class CSS nhưng được viết riêng bằng partial `.cshtml` hoặc `.razor` component. `UiKit` không render toàn bộ website thay cho ba framework.

## 9. Routing và phân quyền

| Trang | Razor Pages | MVC | Blazor |
|---|---|---|---|
| `/products/{slug}` | `@page "/products/{slug}"` trong page | `[HttpGet("/products/{slug}")]` trên action | `@page "/products/{Slug}"` trong component |
| `/cart`, `/checkout`, `/orders` | `[Authorize]` trên PageModel | `[Authorize]` trên controller | `@attribute [Authorize]` + `AuthorizeRouteView` |
| `/admin` | `[Authorize(Roles="Admin")]` | `[Authorize(Roles="Admin")]` | `@attribute [Authorize(Roles="Admin")]` |
| 5 tab Admin | Query/tab trong một page | Query `tab` trong một view | Component con trong một page |

Tất cả API `/api/admin/*` cũng kiểm tra role Admin tại backend. Việc ẩn link Quản trị hoặc từ chối frontend giúp trải nghiệm nhất quán; quyền truy cập dữ liệu được quyết định tại API.

## 10. Checkout, tính tiền và idempotency

Frontend gửi `CheckoutRequest` có `CheckoutKey`; thử thanh toán gửi `SandboxPaymentRequest` có `AttemptKey`. Các key đại diện một yêu cầu nghiệp vụ, giúp API nhận biết POST bị gửi lặp.

Razor Pages và MVC giữ key trong form/hidden input qua HTTP. Blazor giữ model/key trong field của component trong khi xử lý event. Khi đã có đơn hàng, retry dùng chính `orderId`, thay vì tạo lại đơn hàng cho mỗi lần thanh toán thất bại.

Hãy đọc tiếp `API/Services/OrderService.cs` và `API/Services/BusinessRules.cs`: API tạo snapshot tên/SKU/giá, kiểm tra quyền sở hữu, tính tổng, thực hiện transaction, kiểm soát cập nhật đồng thời và ngăn trừ kho hai lần. Ba frontend chỉ hiển thị kết quả API.

## Gợi ý thứ tự học

1. Bắt đầu `RazorPage/Pages/Index.cshtml.cs` và `.cshtml`: một request GET → một PageModel → một trang HTML.
2. Đọc `MVC/Controllers/HomeController.cs`, `ViewModels/ShopViewModels.cs`, `Views/Home/Index.cshtml`: theo cùng dữ liệu qua controller, ViewModel, view.
3. Đọc `BlazorWeb/Components/Pages/Home.razor` và `Components/Shared/ProductCard.razor`: xem binding, lifecycle và callback.
4. So sánh giỏ hàng và checkout bằng bản đồ source ở đầu tài liệu.
5. Theo `ShopApiClient` xuống backend để hiểu vì sao ba cách viết UI vẫn dùng chung dữ liệu và nghiệp vụ.
