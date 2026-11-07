using System.ComponentModel.DataAnnotations;
using ElectroShop.Contracts.Enums;

namespace ElectroShop.Contracts.Requests;

public class LoginRequest
{
    [
        Required(ErrorMessage = "Vui lòng nhập email."),
        EmailAddress(ErrorMessage = "Email không hợp lệ.")
    ]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    public string Password { get; set; } = "";
}

public class RegisterRequest : LoginRequest
{
    [
        Required(ErrorMessage = "Vui lòng nhập họ tên."),
        StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên cần từ 2 đến 100 ký tự.")
    ]
    public string FullName { get; set; } = "";

    [
        Required(ErrorMessage = "Vui lòng nhập số điện thoại."),
        Phone(ErrorMessage = "Số điện thoại không hợp lệ."),
        StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")
    ]
    public string PhoneNumber { get; set; } = "";

    [
        Required(ErrorMessage = "Vui lòng xác nhận mật khẩu."),
        Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")
    ]
    public string ConfirmPassword { get; set; } = "";
}

public class CartQuantityRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã sản phẩm không hợp lệ.")]
    public int ProductId { get; set; }

    [Range(1, 999, ErrorMessage = "Số lượng cần từ 1 đến 999.")]
    public int Quantity { get; set; } = 1;
}

public class CheckoutRequest
{
    public Guid CheckoutKey { get; set; } = Guid.NewGuid();

    [
        Required(ErrorMessage = "Vui lòng nhập người nhận."),
        StringLength(100, ErrorMessage = "Tên người nhận tối đa 100 ký tự.")
    ]
    public string ReceiverName { get; set; } = "";

    [
        Required(ErrorMessage = "Vui lòng nhập số điện thoại."),
        Phone(ErrorMessage = "Số điện thoại không hợp lệ."),
        StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")
    ]
    public string ReceiverPhone { get; set; } = "";

    [
        Required(ErrorMessage = "Vui lòng nhập địa chỉ."),
        StringLength(500, MinimumLength = 10, ErrorMessage = "Địa chỉ cần từ 10 đến 500 ký tự.")
    ]
    public string ShippingAddress { get; set; } = "";

    [StringLength(1000, ErrorMessage = "Ghi chú tối đa 1000 ký tự.")]
    public string? Note { get; set; }
}

public class SandboxPaymentRequest
{
    public Guid AttemptKey { get; set; } = Guid.NewGuid();
    public bool Success { get; set; } = true;
}

public class ProductRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục.")]
    public int CategoryId { get; set; }

    [
        Required(ErrorMessage = "Vui lòng nhập tên sản phẩm."),
        StringLength(200, ErrorMessage = "Tên sản phẩm tối đa 200 ký tự.")
    ]
    public string Name { get; set; } = "";

    [
        Required(ErrorMessage = "Vui lòng nhập slug."),
        RegularExpression(
            "^[a-z0-9]+(?:-[a-z0-9]+)*$",
            ErrorMessage = "Slug chỉ gồm chữ thường không dấu, số và dấu gạch nối."
        ),
        StringLength(200, ErrorMessage = "Slug tối đa 200 ký tự.")
    ]
    public string Slug { get; set; } = "";

    [
        Required(ErrorMessage = "Vui lòng nhập SKU."),
        StringLength(50, ErrorMessage = "SKU tối đa 50 ký tự.")
    ]
    public string SKU { get; set; } = "";

    [
        Required(ErrorMessage = "Vui lòng nhập mô tả."),
        StringLength(4000, ErrorMessage = "Mô tả tối đa 4000 ký tự.")
    ]
    public string Description { get; set; } = "";

    [Range(
        typeof(decimal),
        "0",
        "999999999999",
        ErrorMessage = "Giá phải nằm trong khoảng 0 đến 999.999.999.999."
    )]
    public decimal Price { get; set; }

    [Range(0, 1000000, ErrorMessage = "Tồn kho phải nằm trong khoảng 0 đến 1.000.000.")]
    public int StockQuantity { get; set; }

    [
        Required(ErrorMessage = "Vui lòng nhập đường dẫn ảnh."),
        StringLength(500, ErrorMessage = "Đường dẫn ảnh tối đa 500 ký tự.")
    ]
    public string ImageUrl { get; set; } = "/_content/ElectroShop.UiKit/images/laptop.svg";
    public bool IsActive { get; set; } = true;
    public string RowVersion { get; set; } = "";
}

public class CategoryRequest
{
    [
        Required(ErrorMessage = "Vui lòng nhập tên danh mục."),
        StringLength(100, ErrorMessage = "Tên danh mục tối đa 100 ký tự.")
    ]
    public string Name { get; set; } = "";

    [
        Required(ErrorMessage = "Vui lòng nhập slug."),
        RegularExpression(
            "^[a-z0-9]+(?:-[a-z0-9]+)*$",
            ErrorMessage = "Slug chỉ gồm chữ thường không dấu, số và dấu gạch nối."
        ),
        StringLength(100, ErrorMessage = "Slug tối đa 100 ký tự.")
    ]
    public string Slug { get; set; } = "";

    [StringLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class OrderStatusRequest
{
    [EnumDataType(typeof(OrderStatus), ErrorMessage = "Trạng thái đơn không hợp lệ.")]
    public OrderStatus Status { get; set; }
}

public class CustomerProfileRequest
{
    [
        Required(ErrorMessage = "Vui lòng nhập họ tên."),
        StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên cần từ 2 đến 100 ký tự.")
    ]
    public string FullName { get; set; } = "";

    [
        Required(ErrorMessage = "Vui lòng nhập số điện thoại."),
        Phone(ErrorMessage = "Số điện thoại không hợp lệ."),
        StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")
    ]
    public string PhoneNumber { get; set; } = "";
}

public class AccountAccessRequest
{
    [
        Required(ErrorMessage = "Vui lòng chọn vai trò."),
        RegularExpression(
            "^(Admin|Customer)$",
            ErrorMessage = "Vai trò phải là Admin hoặc Customer."
        )
    ]
    public string Role { get; set; } = "Customer";
    public bool IsActive { get; set; } = true;
}
