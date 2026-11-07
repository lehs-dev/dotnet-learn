# Hợp đồng triển khai nội bộ
Namespace DTOs: ElectroShop.Contracts.DTOs; Requests / Responses / Enums tương ứng. JSON camelCase + enum STRING (JsonStringEnumConverter). Base URL config Api:BaseUrl (http://localhost:5100). Luôn parse ProblemDetails.detail/title khi lỗi.
- POST api/auth/register RegisterRequest => AuthResponse (auto login); POST api/auth/login LoginRequest => AuthResponse; GET api/auth/me => UserDto
- GET api/categories => List<CategoryDto>
- GET api/products?q=&categoryId=&sort=price-asc|price-desc&page=1&pageSize=12 => PagedResult<ProductDto>
- GET api/products/{slug} => ProductDto
- GET api/cart => CartDto; POST api/cart/items CartQuantityRequest (cộng số lượng); PUT api/cart/items/{productId} CartQuantityRequest (gán số lượng); DELETE api/cart/items/{productId}; DELETE api/cart => 204
- POST api/orders CheckoutRequest => OrderDto; GET api/orders?page=&pageSize= => PagedResult<OrderDto>; GET api/orders/{id} => OrderDto
- POST api/payments/{orderId} SandboxPaymentRequest => PaymentDto; GET api/payments/{orderId} => List<PaymentDto>. Retry thất bại cùng orderId, AttemptKey mới. Gửi lại cùng AttemptKey trả kết quả cũ. Thành công lặp không trừ kho.
- GET api/admin/dashboard => DashboardDto
- GET api/admin/products?q=&page=1&pageSize=20 => PagedResult<ProductDto> (gồm ẩn); POST/PUT api/admin/products[/{id}] ProductRequest => ProductDto; DELETE api/admin/products/{id} => 204 (soft). PUT cần RowVersion từ DTO.
- GET api/admin/categories => List<CategoryDto>; POST/PUT api/admin/categories[/{id}] CategoryRequest => CategoryDto; DELETE api/admin/categories/{id} => 204 soft
- GET api/admin/orders?q=&status=Pending&page=1&pageSize=20 => PagedResult<OrderDto>; PUT api/admin/orders/{id}/status OrderStatusRequest => OrderDto
- GET api/admin/customers?q=&page=1&pageSize=20 => PagedResult<CustomerDto>; GET api/admin/customers/{id}/orders => List<OrderDto>; PUT api/admin/customers/{id} CustomerProfileRequest => 204
- GET api/admin/accounts?q=&page=1&pageSize=20 => PagedResult<CustomerDto>; PUT api/admin/accounts/{id}/access AccountAccessRequest => 204

UI shared asset /_content/ElectroShop.UiKit/css/shop.css, /_content/ElectroShop.UiKit/js/shop.js (dialog open data-open-dialog / close data-close-dialog; admin tabs JS data-tab => data-panel), images/{laptop,phone,headphones,keyboard,mouse,monitor,accessory}.svg. Logo text ElectroShop với ký hiệu vuông E.
CSS classes: site-header, container, header-inner, logo, search-form, nav-links, account-actions, main-content, hero, eyebrow, hero-copy, hero-art, badge, filters, form-field, product-grid, product-card, product-image, product-info, product-category, product-title, product-price, product-meta, card-actions, button, button secondary, button danger, button small, empty-state, alert [success|error], page-heading, breadcrumb, detail-grid, detail-image, quantity-input, table-wrap, data-table, cart-layout, summary-card, form-grid, field, full-width, site-footer, footer-inner, auth-dialog, dialog-heading, admin-layout, admin-sidebar, admin-content, tab-button active, stats-grid, stat-card, admin-panel, pagination, order-card, order-meta. Font system; primary #153e76; bg #f5f7fa.
Auth popup login/register all three; form server POST /auth/login|register|logout + antiforgery. Cookie different name per app. JWT server dictionary keyed random session claim, expiry checked. Blazor API authentication via AuthenticationStateProvider user claim session (never circuit HttpContext). Each frontend own Services implementations. No frontend API reference/EF.
All admin stays /admin (query tab allowed); all CRUD controls real APIs. Handle API offline with visible errors. No success if API fails. Contents Vietnamese. Razor Pages PageModels+handlers, MVC controllers/views/viewmodels, Blazor interactive events. Root owns Shared/UiKit/API/Tests/docs/scripts; agents only their frontend folder.
