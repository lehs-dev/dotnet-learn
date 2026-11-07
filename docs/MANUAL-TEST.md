# Kịch bản kiểm thử thủ công

Tài liệu này mô tả các bước cần chạy trên trình duyệt. Việc tạo checklist không có nghĩa các bước đã được kiểm thử. Kết quả build, unit test và API integration test được báo cáo riêng; chưa có kiểm thử trình duyệt tự động cho toàn bộ luồng dưới đây.

## HTTP smoke đã chạy trong phiên triển khai

Đã chạy HTTP thật với antiforgery và cookie trên Blazor: đăng nhập Admin; đăng ký Customer; đăng xuất; SSR `/cart`, `/checkout`, `/orders`; kiểm tra Customer bị từ chối `/admin`; tải CSS/SVG chung. Đã đăng nhập Blazor bằng cùng tài khoản Customer được đăng ký ở Razor Pages và đối chiếu mã đơn đã thanh toán, trạng thái Success cùng chi tiết snapshot trong `/orders` và `/checkout?orderId=...`. Các request này gọi API và SQL Server thật.

Các kiểm tra HTTP trên xác minh đăng nhập, phân quyền và render phía server. Chúng chưa tự động thao tác các sự kiện tương tác Blazor qua trình duyệt/SignalR. Những checklist bên dưới vẫn cần chạy trên trình duyệt để xác nhận toàn bộ trải nghiệm người dùng.

## Chuẩn bị

1. Cấu hình `ConnectionStrings__DefaultConnection`, JWT và tài khoản seed Admin theo README; chỉ dùng `ElectroShopDb` của SQL Server.
2. Chạy migrations, seed và `./scripts/run-all.sh` theo README.
3. Mở bốn URL:

| Ứng dụng | URL |
|---|---|
| API | http://localhost:5100 |
| Razor Pages | http://localhost:5101 |
| Blazor Interactive Server | http://localhost:5102 |
| MVC | http://localhost:5103 |

4. Nếu truy cập từ máy ngoài Dev Container, forward các port 5100–5103 trong VS Code. Mở URL frontend được forward; frontend server gọi API qua `Api:BaseUrl`, không qua URL public của trình duyệt.
5. Chuẩn bị hai tài khoản Customer mới A, B bằng email khác nhau và tài khoản Admin lấy từ biến môi trường. Không ghi mật khẩu vào tài liệu hoặc Git.
6. Ghi lại tên, SKU, giá và tồn kho của một sản phẩm còn hàng; dùng sản phẩm này cho luồng mua hàng chung. Không dùng dữ liệu có sẵn làm bằng chứng cho đơn hàng vừa tạo.

## Luồng xác nhận dùng chung database giữa ba frontend

- [ ] Ở **Razor Pages**, bấm Đăng ký trên header. Modal mở mà không chuyển sang trang đăng ký riêng. Nhập họ tên, email A, số điện thoại, mật khẩu và xác nhận; gửi. Kiểm tra tên tài khoản xuất hiện.
- [ ] Mở **MVC** trong tab khác. Đăng nhập bằng email/mật khẩu A vừa tạo ở Razor Pages. Không đăng ký lại. Xác nhận đăng nhập thành công.
- [ ] Mở **Blazor**. Đăng nhập A, mở chi tiết sản phẩm đã chọn, thêm số lượng 2 vào giỏ. Mở `/cart`; ghi lại đơn giá, tổng tiền và tồn kho hiện tại.
- [ ] Ở Blazor, cập nhật số lượng 1 rồi 2, tải lại trang. Số lượng phải được lưu. Mở giỏ hàng ở MVC bằng A: các sản phẩm và số lượng phải trùng nhau.
- [ ] Ở Blazor, vào `/checkout`. Nhập thông tin giao hàng hợp lệ, chọn **Thanh toán thành công** và gửi. Ghi lại mã đơn, số lượng và giá trong đơn.
- [ ] Tải lại trang kết quả. Mã đơn phải giữ nguyên; không xuất hiện đơn thứ hai. Giỏ hàng không còn các sản phẩm đã thanh toán và tồn kho giảm đúng số lượng mua.
- [ ] Trở lại **Razor Pages**, mở `/orders` bằng A. Đơn hàng mới từ Blazor phải xuất hiện. Mở chi tiết và đối chiếu mã đơn, địa chỉ, tổng tiền, số lượng, đơn giá snapshot.
- [ ] Ở **MVC**, đăng xuất A rồi đăng nhập Admin, mở `/admin`, tab Đơn hàng. Tìm mã đơn vừa tạo. Kiểm tra cùng khách hàng, sản phẩm và tổng tiền; Dashboard tăng số đơn và doanh thu tương ứng.
- [ ] Ở **Razor Pages**, đăng nhập Admin, mở `/admin`, tab Sản phẩm. Sửa giá sản phẩm vừa mua sang giá khác và lưu.
- [ ] Tải lại danh sách và chi tiết sản phẩm ở **Blazor** và **MVC**. Cả hai phải hiện giá mới từ API.
- [ ] Mở lại đơn cũ của A. Đơn giá, tên, SKU và tổng tiền phải giữ giá trị tại thời điểm tạo đơn, không đổi theo giá sản phẩm hiện tại.

## Giao diện và danh mục trên cả ba website

Lặp các bước này lần lượt ở Razor Pages, Blazor và MVC.

- [ ] So sánh logo, header, footer, màu, font, product card và nội dung hero. Kiểm tra màn hình desktop và chiều rộng 375px; bảng có thể cuộn ngang nhưng trang không bị tràn.
- [ ] Danh sách mặc định hiển thị dữ liệu API. Tìm một phần tên tiếng Việt, lọc danh mục, sắp xếp giá tăng/giảm. Kiểm tra số kết quả và nút chuyển trang.
- [ ] Tìm từ khóa không tồn tại; hiển thị trạng thái trống, không báo thành công giả.
- [ ] Mở `/products/{slug}` hợp lệ; đối chiếu SKU, giá, mô tả, tồn kho và sản phẩm liên quan. Slug sai phải hiển thị lỗi phù hợp.
- [ ] Chọn số lượng lớn hơn tồn kho hoặc 0; không được thêm giỏ. Sản phẩm hết hàng có nút mua bị vô hiệu hóa.
- [ ] Khi chưa đăng nhập, bấm thêm giỏ hoặc truy cập `/cart`, `/checkout`, `/orders`; giao diện yêu cầu đăng nhập bằng modal, không hiển thị dữ liệu người khác.

## Authentication, phân quyền và phiên

- [ ] Mật khẩu xác nhận không khớp, email sai định dạng, thiếu họ tên hoặc số điện thoại: không tạo tài khoản.
- [ ] Đăng ký email trùng, đăng nhập sai mật khẩu: hiển thị thông báo lỗi, không tạo phiên đăng nhập.
- [ ] Kiểm tra DevTools: không có JWT trong `localStorage`/`sessionStorage`, HTML, URL hoặc JavaScript. Frontend dùng cookie HttpOnly riêng cho từng website; JWT chỉ lưu ở server.
- [ ] Đăng nhập A ở Razor Pages và B ở MVC. Giỏ hàng và lịch sử của hai user khác nhau, không bị trộn token giữa ứng dụng/circuit.
- [ ] Trong Blazor, mở trình duyệt ẩn danh đăng nhập B, trình duyệt thường đăng nhập A. Thêm giỏ ở từng cửa sổ; mỗi circuit gọi API bằng user đúng.
- [ ] Đăng xuất một frontend. Frontend đó trở về trang chủ và mất quyền truy cập dữ liệu riêng; các phiên ở hai frontend khác vẫn hoạt động độc lập.
- [ ] Khởi động lại frontend. Phiên trong server token store bị mất; người dùng được yêu cầu đăng nhập lại.
- [ ] Customer truy cập `/admin` phải bị từ chối. Gửi token Customer trực tiếp đến `api/admin/*` phải nhận 403, không chỉ bị ẩn menu.
- [ ] Gửi POST auth/cart/admin bằng cookie nhưng thiếu hoặc sai antiforgery token ở Razor Pages/MVC hoặc auth endpoint Blazor; backend frontend phải từ chối. Event Blazor dùng circuit được framework xác thực, không dùng form HTTP thay cho event.
- [ ] Lấy `orderId` của A, dùng token B gọi `GET /api/orders/{id}` và thanh toán đơn đó. API phải từ chối mà không lộ chi tiết đơn hoặc sửa tồn kho.

## Thanh toán thất bại, thử lại và gửi lặp

- [ ] Thêm một sản phẩm vào giỏ A. Tạo đơn với **Thanh toán thất bại**. Kiểm tra Payment Failed, Order chưa được thanh toán, tồn kho không đổi và giỏ hàng giữ nguyên.
- [ ] Bấm thử lại thành công trên **cùng đơn hàng**; mã đơn không đổi, thanh toán chuyển Success và tồn kho giảm đúng một lần.
- [ ] Tải lại trang kết quả, bấm quay lại, hoặc gửi lại cùng `CheckoutKey`/`AttemptKey` qua API. Số đơn, tổng tiền, số lần trừ kho không được tăng trùng.
- [ ] Gửi lại payment thành công với `AttemptKey` mới cho đơn đã Success. Không trừ kho lần thứ hai.
- [ ] Mô phỏng hai tài khoản mua cùng sản phẩm còn 1 chiếc, tạo đơn trước rồi thanh toán gần đồng thời. Tối đa một đơn được Success; tồn kho không âm. Đơn còn lại nhận thông báo hết hàng/xung đột và giỏ không bị xóa nhầm.
- [ ] Tạo đơn rồi Admin ẩn sản phẩm hoặc giảm tồn kho trước khi thanh toán. API phải kiểm tra lại khi payment; không bán vượt kho.
- [ ] Thay đổi giỏ hàng sau khi tạo một đơn chưa thanh toán. Khi thanh toán đơn cũ, chỉ số lượng sản phẩm thuộc đơn được xử lý; mặt hàng mới trong giỏ không mất ngoài ý muốn.
- [ ] Mất kết nối API khi gửi thanh toán: giao diện phải báo lỗi thật. Sau khi API hoạt động lại, thử lại phải nhận đúng kết quả đã xử lý mà không nhân đôi tồn kho/đơn.

## Admin: chỉ một trang `/admin`, đủ năm tab

- [ ] **Dashboard:** đối chiếu tổng sản phẩm, khách hàng, đơn, đơn Pending và doanh thu các đơn Success. Có tối đa 5 đơn mới nhất, không cộng tiền các payment Failed.
- [ ] **Đơn hàng:** tìm theo mã đơn/khách hàng, lọc trạng thái, chuyển trang và mở chi tiết. Đơn Success đi theo Pending → Confirmed → Shipping → Completed. Chỉ đơn Pending chưa Success được hủy. Không cho nhảy trạng thái hoặc hủy đơn đã thanh toán khi chưa có nghiệp vụ hoàn tiền.
- [ ] **Sản phẩm:** thêm một sản phẩm SKU/slug duy nhất, sửa giá/tồn kho/mô tả/ảnh/danh mục, ẩn rồi mở lại hiển thị. Không chấp nhận giá/tồn kho âm, SKU/slug trùng hoặc danh mục sai. Cập nhật từ hai phiên phải phát hiện RowVersion cũ.
- [ ] **Danh mục trong tab Sản phẩm:** thêm, sửa và ẩn danh mục; không chuyển sang trang admin riêng. Sản phẩm và danh mục đã xuất hiện trong lịch sử không bị xóa cascade.
- [ ] **Khách hàng:** tìm, phân trang, xem thông tin liên hệ/ngày tạo/số đơn/tổng chi tiêu, mở lịch sử và sửa họ tên/số điện thoại. Không hiển thị password/hash.
- [ ] **Tài khoản:** tìm, phân trang, đổi role Customer/Admin, khóa/mở khóa. Đăng nhập lại để cập nhật role trên cookie. Tài khoản bị khóa không thể tiếp tục dùng API.
- [ ] Khi chỉ còn một Admin hoạt động, thử hạ role hoặc khóa tài khoản đó; API phải từ chối. Dùng hai phiên cập nhật đồng thời để xác nhận không làm mất Admin cuối cùng.

## Ghi nhận kết quả

Với mỗi lỗi, ghi frontend, URL, tài khoản theo vai trò (không ghi mật khẩu/token), bước tái hiện, kết quả mong đợi/thực tế và mã HTTP. Khi hoàn tất, đánh dấu checklist theo kết quả thực tế; không đánh dấu các mục chỉ dựa trên việc build thành công.
