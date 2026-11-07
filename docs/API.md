# Web API dùng chung

Development: `http://localhost:5100`, OpenAPI JSON: `/openapi/v1.json`. Frontend gọi server-to-server; không cần bật CORS hay lưu JWT ở browser. JSON camelCase, enum dạng chuỗi. Không expose EF entities hoặc password hash.

Quyền: Public không cần token; User cần `Authorization: Bearer <accessToken>`; Admin cần JWT role Admin. Người dùng trong cart/orders/payments luôn lấy từ token đã xác thực.

| Method | Route dưới /api | Quyền | Input / kết quả |
|---|---|---|---|
| POST | /auth/register | Public | RegisterRequest →201 AuthResponse, tự đăng nhập Customer |
| POST | /auth/login | Public | Email, password →200 AuthResponse |
| GET | /auth/me | User | UserDto |
| GET | /categories | Public | List CategoryDto đang active |
| GET | /products | Public | q, categoryId, sort, page, pageSize →PagedResult ProductDto |
| GET | /products/{slug} | Public | ProductDto còn hiển thị |
| GET | /cart | User | CartDto items + totalAmount |
| POST | /cart/items | User | productId, quantity cộng vào giỏ →204 |
| PUT | /cart/items/{productId} | User | productId, quantity gán số lượng →204 |
| DELETE | /cart/items/{productId} | User | Xóa sản phẩm của chính user →204 |
| DELETE | /cart | User | Xóa toàn bộ giỏ hiện tại →204 |
| POST | /orders | User | CheckoutRequest →201 OrderDto snapshot |
| GET | /orders | User | page, pageSize →PagedResult OrderDto của user |
| GET | /orders/{id} | User | OrderDto,404 nếu không sở hữu |
| POST | /payments/{orderId} | User | attemptKey, success →PaymentDto |
| GET | /payments/{orderId} | User | List PaymentDto của đơn thuộc user |
| GET | /admin/dashboard | Admin | DashboardDto: counts, revenue,5 recent orders |
| GET | /admin/products | Admin | q,page,pageSize →PagedResult ProductDto kể cả sản phẩm ẩn |
| POST | /admin/products | Admin | ProductRequest →ProductDto |
| PUT | /admin/products/{id} | Admin | ProductRequest có rowVersion →ProductDto |
| DELETE | /admin/products/{id} | Admin | Xóa mềm →204 |
| GET | /admin/categories | Admin | List CategoryDto kể cả ẩn |
| POST / PUT | /admin/categories[/{id}] | Admin | CategoryRequest →CategoryDto |
| DELETE | /admin/categories/{id} | Admin | Xóa mềm →204 |
| GET | /admin/orders | Admin | q (mã/tên/email),status,page,pageSize →PagedResult OrderDto |
| PUT | /admin/orders/{id}/status | Admin | status →OrderDto |
| GET | /admin/customers | Admin | q,page,pageSize →PagedResult CustomerDto |
| GET | /admin/customers/{id}/orders | Admin | List OrderDto (tối đa100 đơn gần nhất) |
| PUT | /admin/customers/{id} | Admin | fullName,phoneNumber →204 |
| GET | /admin/accounts | Admin | q,page,pageSize →PagedResult CustomerDto có roles/isActive |
| PUT | /admin/accounts/{id}/access | Admin | role Admin/Customer,isActive →204 |

Danh sách phân trang có `{items,totalCount,page,pageSize,totalPages}`. Server kẹp page>=1, pageSize1..100. Sắp xếp catalog: `price-asc`, `price-desc`; mặc định theo Id. SQL tìm theo tên, không tải tất cả sản phẩm rồi lọc RAM.

## Ví dụ

Đăng ký (mật khẩu minh họa bên dưới chỉ dùng mô tả, không phải tài khoản seed):

```http
POST /api/auth/register
Content-Type: application/json

{"fullName":"Nguyễn An","email":"an@example.test","phoneNumber":"0901234567","password":"Example!123","confirmPassword":"Example!123"}
```

Kết quả chứa `accessToken`, `expiresAt`, `user` có id/fullName/email/phoneNumber/roles/isActive/createdAt. API không cấp refresh token; hết hạn thì đăng nhập lại.

```http
GET /api/products?q=Logitech&sort=price-asc&page=1&pageSize=12
```

```json
{"items":[],"totalCount":0,"page":1,"pageSize":12,"totalPages":0}
```

Ví dụ trên minh họa hình dạng kết quả; số lượng thực tế phụ thuộc dữ liệu SQL.

```http
POST /api/orders
Authorization: Bearer <token>
Content-Type: application/json

{"checkoutKey":"a395242d-3bdc-4a85-b48b-ebfa63b641a3","receiverName":"Nguyễn An","receiverPhone":"0901234567","shippingAddress":"123 Nguyễn Trãi, Thành phố Hồ Chí Minh","note":"Giao giờ hành chính"}
```

Gửi lại cùng checkoutKey trả cùng đơn, không snapshot/ghi đơn lần nữa. Checkout chỉ tạo đơn Pending; tồn kho được trừ lúc payment Success. Giá lấy từ database, không nhận total/price từ client.

```http
POST /api/payments/123
Authorization: Bearer <token>
Content-Type: application/json

{"attemptKey":"08d6492f-cebe-4410-89a2-3a6fb72831dd","success":false}
```

Payment Failed vẫn trả200 với `status:"Failed"` vì đây là kết quả mô phỏng hợp lệ; giỏ/tồn kho giữ nguyên. Thử lại success=true trên **cùng orderId** với AttemptKey mới. Nếu mất response và chưa biết kết quả, gửi lại **cùng AttemptKey**. Gửi lại mã của lần thất bại vẫn trả kết quả thất bại cũ. Đơn đã paid trả payment Success cũ kể cả khi gửi AttemptKey khác, không trừ kho thêm.

## Lỗi và bảo vệ dữ liệu

- 400: validation; 401: chưa đăng nhập/hết hạn/khóa hoặc security stamp thay đổi; 403: không có role.
- 404: không tồn tại hoặc không phải chủ đơn; không tiết lộ đơn của người khác.
- 409: thiếu hàng, request trùng SKU/slug, RowVersion cũ, trạng thái đơn sai, admin cuối cùng hoặc SQL deadlock/concurrency. Tải lại dữ liệu; nếu retry payment/checkout giữ key cũ khi chưa biết kết quả.
- 503: SQL chưa sẵn sàng; 500: thông báo chung, exception chi tiết chỉ ghi server log.

Lỗi theo ProblemDetails: `{status,title,detail,traceId}`; validation có thêm `errors`. Không trả stack trace/connection string. Form HTTP ở frontend dùng antiforgery; API chỉ nhận bearer token, không dùng cookie xác thực API. Sau đổi role/khóa, security stamp thay đổi khiến mọi token cũ của tài khoản mất hiệu lực. Khóa đăng nhập Identity tự động sau5 lần sai trong10 phút.
