#!/usr/bin/env python3
"""Build first. Run with Python + Playwright Chromium; all data stays in a temporary SQLite DB."""

import json
import os
from pathlib import Path
import re
import secrets
import signal
import socket
import subprocess
import tempfile
import time
import urllib.request
from urllib.parse import urlparse

from playwright.sync_api import sync_playwright, expect

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / ".local" / "system-check"
OUTPUT.mkdir(parents=True, exist_ok=True)
PROJECTS = [
    ("API", "ElectroShop.Api"),
    ("RazorPage", "ElectroShop.RazorPages"),
    ("BlazorWeb", "ElectroShop.Blazor"),
    ("MVC", "ElectroShop.Mvc"),
]
PASSWORD = "Test!Aa1-" + secrets.token_hex(10)
ADMIN_EMAIL = "system-admin@electroshop.test"
results = []
processes = []
logs = []


def record(app, flow):
    results.append({"app": app, "flow": flow, "result": "passed"})
    print(f"PASS {app}: {flow}", flush=True)


def available_port():
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def ready(url):
    deadline = time.monotonic() + 45
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=1) as response:
                if response.status == 200:
                    return
        except Exception:
            if any(proc.poll() is not None for proc in processes):
                raise RuntimeError("An application exited; inspect .local/system-check/*.log")
            time.sleep(0.2)
    raise RuntimeError(f"Application did not start: {url}")


def goto(page, base, route, blazor=False):
    response = page.goto(base + route)
    assert (
        response and response.status < 400
    ), f'{route}: HTTP {response.status if response else "no response"}'
    if blazor:
        page.wait_for_timeout(350)
    expect(page.locator(".loading")).to_have_count(0)


def no_error(page):
    expect(page.locator("main .alert.error")).to_have_count(0)
    (
        expect(page.locator("#blazor-error-ui")).not_to_be_visible()
        if page.locator("#blazor-error-ui").count()
        else None
    )


def assert_fits(page):
    assert page.evaluate(
        "document.documentElement.scrollWidth <= window.innerWidth"
    ), "Page overflows horizontally"


def admin_search(page, text):
    form = page.locator(".admin-content .filters").first
    form.locator('input:not([type="hidden"])').first.fill(text)
    form.get_by_role("button", name="Tìm kiếm").click()
    tables = page.locator(".admin-content .data-table")
    if tables.count():
        expect(tables.first.locator("tbody tr")).to_have_count(1)
    else:
        expect(page.locator(".admin-content > article.order-card")).to_have_count(1)


def check_management(page, api, name, base, email):
    blazor = name == "BlazorWeb"
    marker = name.lower() + "-" + secrets.token_hex(5)
    category_name = "Danh mục kiểm thử " + marker
    product_name = "Sản phẩm kiểm thử " + marker
    sku = "CHECK-" + marker
    goto(page, base, "/admin?tab=products", blazor)
    if name == "RazorPage":
        page.get_by_text("Thêm danh mục mới", exact=True).click()
        form = page.locator("details[open] form").first
    elif name == "MVC":
        form = page.locator("#category-form")
    else:
        page.get_by_role("button", name="+ Thêm danh mục", exact=True).click()
        form = page.locator(".admin-edit form")
    form.get_by_label("Tên danh mục", exact=True).fill(category_name)
    form.get_by_label("Slug", exact=True).fill("check-category-" + marker)
    form.get_by_role("button", name=re.compile("^(Thêm danh mục|Lưu danh mục)$")).click()
    expect(page.locator(".admin-content")).to_contain_text(category_name)
    no_error(page)

    if name == "RazorPage":
        page.get_by_text("Thêm sản phẩm mới", exact=True).click()
    elif blazor:
        page.get_by_role("button", name="+ Thêm sản phẩm", exact=True).click()
    else:
        page.get_by_role("link", name="Thêm sản phẩm", exact=True).click()
    form = page.locator('form:visible:has(input[name$=".SKU"])')
    form.get_by_label("Tên sản phẩm", exact=True).fill(product_name)
    form.get_by_label("Slug", exact=True).fill("check-product-" + marker)
    form.locator('input[name$=".SKU"]').fill(sku)
    form.locator('select[name$=".CategoryId"]').select_option(label=category_name)
    form.locator('input[name$=".Price"]').fill("123456")
    form.locator('input[name$=".StockQuantity"]').fill("8")
    form.locator('textarea[name$=".Description"]').fill("Mô tả sản phẩm tạo qua trình duyệt.")
    form.get_by_role("button", name=re.compile("^(Thêm sản phẩm|Lưu sản phẩm)$")).click()
    expect(page.locator("main .alert.success")).to_contain_text("Đã lưu sản phẩm")
    no_error(page)
    data = api.get("/api/products?q=" + sku).json()
    assert data["totalCount"] == 1, data
    product = data["items"][0]
    assert product["price"] == 123456 and product["stockQuantity"] == 8
    record(name, "create category and product through management forms")

    def open_product():
        goto(page, base, "/admin?tab=products", blazor)
        admin_search(page, sku)
        row = page.locator(".data-table tbody tr").filter(has_text=sku).first
        expect(row).to_be_visible()
        if name == "RazorPage":
            row.get_by_text("Sửa sản phẩm", exact=True).click()
        else:
            row.get_by_role("button" if blazor else "link", name="Sửa", exact=True).click()
        return page.locator('form:visible:has(input[name$=".SKU"])')

    form = open_product()
    form.locator('input[name$=".Price"]').fill("234567")
    form.locator('input[name$=".StockQuantity"]').fill("7")
    form.get_by_role("button", name="Lưu sản phẩm", exact=True).click()
    expect(page.locator("main .alert.success")).to_contain_text("Đã lưu sản phẩm")
    no_error(page)
    current = api.get("/api/products/" + product["slug"]).json()
    assert current["price"] == 234567 and current["stockQuantity"] == 7, current
    goto(page, base, "/admin?tab=products", blazor)
    admin_search(page, sku)
    page.locator(".data-table tbody tr").filter(has_text=sku).first.get_by_role(
        "button", name="Ẩn", exact=True
    ).click()
    expect(page.locator("main")).to_contain_text("Đã ẩn")
    assert api.get("/api/products/" + product["slug"]).status == 404
    form = open_product()
    form.locator('input[type="checkbox"]').check()
    form.get_by_role("button", name="Lưu sản phẩm", exact=True).click()
    expect(page.locator("main .alert.success")).to_contain_text("Đã lưu sản phẩm")
    no_error(page)
    assert api.get("/api/products/" + product["slug"]).status == 200
    record(name, "edit product price/stock, hide and restore product")

    goto(page, base, "/admin?tab=products", blazor)
    if name == "RazorPage":
        category_panel = page.locator("details.summary-card").filter(
            has=page.locator("summary", has_text=category_name)
        )
        category_panel.locator("summary").first.click()
        category_panel.get_by_role("button", name="Ẩn danh mục", exact=True).click()
    else:
        row = page.locator(".data-table tbody tr").filter(has_text=category_name).first
        row.get_by_role("button", name="Ẩn", exact=True).click()
    expect(page.locator("main")).to_contain_text("Đã ẩn")
    # Wait for the category mutation to finish before checking visibility through API.
    expect(page.locator("main .alert.success")).to_contain_text("Đã ẩn danh mục")
    assert api.get("/api/products/" + product["slug"]).status == 404
    if name == "RazorPage":
        category_panel = page.locator("details.summary-card").filter(
            has=page.locator("summary", has_text=category_name)
        )
        category_panel.locator("summary").first.click()
        form = category_panel.locator("form").first
    else:
        row = page.locator(".data-table tbody tr").filter(has_text=category_name).first
        row.get_by_role("button" if blazor else "link", name="Sửa", exact=True).click()
        form = page.locator(".admin-edit form") if blazor else page.locator("#category-form")
    form.locator('input[type="checkbox"]').check()
    form.get_by_label("Tên danh mục", exact=True).fill(category_name + " cập nhật")
    form.get_by_role("button", name="Lưu danh mục", exact=True).click()
    expect(page.locator("main .alert.success")).to_contain_text("Đã lưu danh mục")
    no_error(page)
    assert (
        api.get("/api/products/" + product["slug"]).json()["categoryName"]
        == category_name + " cập nhật"
    )
    record(name, "hide, edit and restore category with its products")

    goto(page, base, "/admin?tab=customers", blazor)
    admin_search(page, email)
    if name == "RazorPage":
        page.get_by_text("Sửa hồ sơ", exact=True).click()
    elif name == "MVC":
        page.get_by_text("Sửa hồ sơ khách hàng", exact=True).click()
    else:
        page.get_by_role("button", name="Hồ sơ & đơn hàng").click()
    form = page.locator('form:visible:has(input[name$="FullName"])')
    form.locator('input[name$="FullName"]').fill("Hồ sơ cập nhật " + name)
    form.locator('input[name$="PhoneNumber"]').fill("0987654321")
    form.get_by_role("button", name="Lưu hồ sơ", exact=True).click()
    expect(page.locator(".admin-content")).to_contain_text("Hồ sơ cập nhật " + name)
    no_error(page)
    if not blazor:
        page.get_by_role(
            "link",
            name="Lịch sử đơn" if name == "RazorPage" else "Xem lịch sử mua hàng",
            exact=True,
        ).click()
        expect(page.locator("#customer-orders .order-card").first).to_be_visible()
    else:
        expect(page.locator(".admin-edit .order-card").first).to_be_visible()
    record(name, "edit customer profile and inspect purchase history")

    # The selected order can be found through the API using the known customer email.
    for next_status in ["Confirmed", "Shipping", "Completed"]:
        goto(page, base, "/admin?tab=orders&q=" + email, blazor)
        if blazor:
            page.locator(".order-card details summary").first.click()
        select = page.locator(".admin-panel select, .order-card select").first
        select.select_option(next_status)
        page.get_by_role("button", name=re.compile("^Cập nhật")).click()
        expect(page.locator("main .alert.success")).to_contain_text("Đã cập nhật")
        no_error(page)
    record(name, "fulfill paid order: confirm, ship, complete")

    goto(page, base, "/admin?tab=accounts", blazor)
    admin_search(page, ADMIN_EMAIL)
    page.get_by_role("button", name=re.compile("^(Lưu quyền|Lưu thay đổi)$")).click()
    expect(page.locator("main .alert.success")).to_contain_text("Đã cập nhật")
    no_error(page)
    goto(page, base, "/admin?tab=accounts", blazor)
    no_error(page)
    record(name, "saving unchanged administrator access preserves session")

    def account_form():
        goto(page, base, "/admin?tab=accounts", blazor)
        admin_search(page, email)

    def save_access():
        page.get_by_role("button", name=re.compile("^(Lưu quyền|Lưu thay đổi)$")).click()
        expect(page.locator("main .alert.success")).to_contain_text("Đã cập nhật")
        no_error(page)

    account_form()
    page.locator('.admin-content input[type="checkbox"]').uncheck()
    save_access()
    assert api.post("/api/auth/login", data={"email": email, "password": PASSWORD}).status == 401
    account_form()
    page.locator('.admin-content input[type="checkbox"]').check()
    save_access()
    assert api.post("/api/auth/login", data={"email": email, "password": PASSWORD}).status == 200
    record(name, "lock and unlock account through management form")

    account_form()
    page.locator(".admin-content select").select_option("Admin")
    save_access()
    assert api.post("/api/auth/login", data={"email": email, "password": PASSWORD}).status == 403
    assert (
        api.post("/api/auth/admin/login", data={"email": email, "password": PASSWORD}).status == 200
    )
    account_form()
    page.locator(".admin-content select").select_option("Customer")
    save_access()
    assert (
        api.post("/api/auth/admin/login", data={"email": email, "password": PASSWORD}).status == 403
    )
    record(name, "change roles and enforce the separate login gates")

    goto(page, base, "/admin?tab=accounts", blazor)
    admin_search(page, ADMIN_EMAIL)
    page.locator('.admin-content input[type="checkbox"]').uncheck()
    page.get_by_role("button", name=re.compile("^(Lưu quyền|Lưu thay đổi)$")).click()
    expect(page.locator("main .alert.error")).to_contain_text("quản trị cuối cùng")
    goto(page, base, "/admin?tab=accounts", blazor)
    no_error(page)
    record(name, "prevent locking the last active administrator")


def check_images(page):
    page.locator("img").evaluate_all("images => images.forEach(image => image.loading = 'eager')")
    page.wait_for_function(
        "Array.from(document.images).every(image => image.complete && image.naturalWidth > 0)",
        timeout=12000,
    )
    sources = page.locator("img").evaluate_all("images => images.map(image => image.currentSrc)")
    for source in set(sources):
        for encoding in ["identity", "gzip, br"]:
            response = page.request.get(source, headers={"Accept-Encoding": encoding})
            assert response.status == 200, f"Image request failed: {source} ({encoding})"
            assert response.headers.get("content-type", "").startswith("image/"), source
            # A stale compressed asset endpoint can return HTTP 200 with an empty body.
            assert response.body(), f"Empty image response: {source} ({encoding})"


def check_app(browser, api, name, base):
    blazor = name == "BlazorWeb"
    context = browser.new_context(viewport={"width": 1440, "height": 1000}, locale="vi-VN")
    page = context.new_page()
    page.set_default_timeout(12000)
    errors = []
    page.on("pageerror", lambda error: errors.append(str(error)))
    page.on("dialog", lambda dialog: dialog.accept())
    try:
        goto(page, base, "/")
        expect(page.locator(".product-card")).to_have_count(12)
        no_error(page)
        check_images(page)
        page.screenshot(path=str(OUTPUT / f"{name}-catalog.png"), full_page=True)
        for sort in ["price-asc", "price-desc"]:
            goto(page, base, f"/?sort={sort}", blazor)
            expect(page.locator(".product-card")).to_have_count(12)
            no_error(page)
        goto(page, base, "/?page=2", blazor)
        expected = api.get("/api/products?page=2&pageSize=12").json()["items"]
        expect(page.locator(".product-card")).to_have_count(len(expected))
        assert (
            page.locator(".product-card .product-image").first.get_attribute("href")
            == "/products/" + expected[0]["slug"]
        )
        check_images(page)
        goto(page, base, "/?q=not-a-real-product", blazor)
        expect(page.locator(".empty-state")).to_be_visible()
        record(name, "catalog, images, numeric price sorting, empty search")

        goto(page, base, "/cart", blazor)
        expect(page.locator("#login-dialog")).to_be_visible()
        # Registration preserves the original protected destination.
        (
            page.locator('#login-dialog [data-open-dialog="register-dialog"]').click()
            if not blazor
            else page.locator("#login-dialog").get_by_role("button", name="Đăng ký ngay").click()
        )
        registration = page.locator("#register-dialog")
        expect(registration).to_be_visible()
        email = f"{name.lower()}-{secrets.token_hex(5)}@electroshop.test"
        registration.locator('[name="FullName"]').fill("Khách kiểm thử " + name)
        registration.locator('[name="Email"]').fill(email)
        registration.locator('[name="PhoneNumber"]').fill("0901234567")
        registration.locator('[name="Password"]').fill(PASSWORD)
        registration.locator('[name="ConfirmPassword"]').fill("Mismatch!Aa123")
        registration.locator('button[type="submit"]').click()
        expect(registration).to_be_visible()
        registration.locator('[name="ConfirmPassword"]').fill(PASSWORD)
        registration.locator('button[type="submit"]').click()
        page.wait_for_url(base + "/cart")
        expect(page.locator(".empty-state")).to_be_visible()
        record(name, "registration, password confirmation, return to cart")

        goto(page, base, "/admin", blazor)
        page.wait_for_url(re.compile(r".*/admin/login.*"))
        expect(page.locator("h1")).to_have_text("Đăng nhập quản trị")
        record(name, "customer denied administrator area")

        goto(page, base, "/", blazor)
        product_link = page.locator(".product-card .product-image").first.get_attribute("href")
        goto(page, base, product_link, blazor)
        page.locator('.detail-grid input[type="number"]').fill("2")
        page.locator(".detail-grid").get_by_role("button", name="Thêm vào giỏ hàng").click()
        expect(page.locator("main .alert.success")).to_be_visible()
        goto(page, base, "/cart", blazor)
        expect(page.locator(".data-table tbody tr")).to_have_count(1)
        quantity = page.locator('.data-table input[type="number"]')
        expect(quantity).to_have_value("2")
        quantity.fill("3")
        page.get_by_role("button", name="Cập nhật", exact=True).click()
        expect(page.locator('.data-table input[type="number"]')).to_have_value("3")
        no_error(page)
        record(name, "product details, add to cart, update quantity")

        goto(page, base, "/checkout", blazor)
        (
            page.locator(
                'input[name="Input.ReceiverName"], input[name="checkout.ReceiverName"]'
            ).fill("Người nhận kiểm thử")
            if not blazor
            else page.locator(".form-grid input").nth(0).fill("Người nhận kiểm thử")
        )
        if blazor:
            page.locator(".form-grid input").nth(1).fill("0901234567")
            page.locator(".form-grid textarea").first.fill("123 Nguyễn Huệ, Thành phố Hồ Chí Minh")
            page.locator(".summary-card select").select_option("failure")
        else:
            page.locator('[name="Input.ReceiverPhone"]').fill("0901234567")
            page.locator('[name="Input.ShippingAddress"]').fill(
                "123 Nguyễn Huệ, Thành phố Hồ Chí Minh"
            )
            page.locator('[name="success"][value="false"]').check()
        page.get_by_role("button", name="Tạo đơn và thanh toán").click()
        expect(page.locator(".order-card").first).to_be_visible()
        (
            expect(page.locator("main")).to_contain_text("Thất bại")
            if not blazor
            else expect(page.locator("main")).to_contain_text("thất bại")
        )
        page.get_by_role(
            "button", name=re.compile("^(Thanh toán thành công|Mô phỏng thành công)$")
        ).click()
        expect(page.locator("main")).to_contain_text("thành công")
        goto(page, base, "/cart", blazor)
        expect(page.locator(".empty-state")).to_be_visible()
        goto(page, base, "/orders", blazor)
        expect(page.locator(".order-card")).to_have_count(1)
        no_error(page)
        record(name, "checkout failure, retry success, cleared cart, order history")
        for action in ["remove", "clear"]:
            goto(page, base, product_link, blazor)
            page.locator(".detail-grid").get_by_role("button", name="Thêm vào giỏ hàng").click()
            expect(page.locator("main .alert.success")).to_be_visible()
            goto(page, base, "/cart", blazor)
            page.get_by_role(
                "button", name="Xóa" if action == "remove" else "Xóa toàn bộ giỏ", exact=True
            ).click()
            expect(page.locator(".empty-state")).to_be_visible()
        record(name, "remove cart item and clear the entire cart")

        # Revisit checkout in the same interactive Blazor circuit: no stale order.
        if blazor:
            page.locator('.nav-links a[href="/"]').click()
            expect(page.locator(".product-card").first).to_be_visible()
            page.locator(".product-card").first.get_by_role("button", name="Thêm vào giỏ").click()
            expect(page.locator("main .alert.success")).to_be_visible()
            page.locator('.nav-links a[href="/cart"]').click()
            page.get_by_role("link", name=re.compile("Tiến hành thanh toán")).click()
            expect(page.get_by_role("button", name="Tạo đơn và thanh toán")).to_be_visible()
            record(name, "new checkout after previous payment in the same circuit")

        page.locator(".account-actions button").click()
        expect(page.locator(".account-actions")).to_contain_text("Đăng nhập")
        goto(page, base, "/orders", blazor)
        login = page.locator("#login-dialog")
        expect(login).to_be_visible()
        login.locator('[name="Email"]').fill(email)
        login.locator('[name="Password"]').fill("Wrong!Aa123456")
        login.locator('button[type="submit"]').click()
        expect(page.locator("#login-dialog .alert.error")).to_be_visible()
        login = page.locator("#login-dialog")
        login.locator('[name="Email"]').fill(email)
        login.locator('[name="Password"]').fill(PASSWORD)
        login.locator('button[type="submit"]').click()
        page.wait_for_url(base + "/orders")
        expect(page.locator(".order-card")).to_have_count(1)
        record(name, "logout, customer login, return to order history")

        # A separate context exercises the administrator login and all tabs.
        admin_context = browser.new_context(
            viewport={"width": 1440, "height": 1000}, locale="vi-VN"
        )
        admin = admin_context.new_page()
        admin.set_default_timeout(12000)
        admin.on("dialog", lambda dialog: dialog.accept())
        admin.on("pageerror", lambda error: errors.append(str(error)))
        goto(admin, base, "/admin/login", blazor)
        admin.locator("#admin-email").fill(ADMIN_EMAIL)
        admin.locator("#admin-password").fill(PASSWORD)
        admin.locator('.auth-form button[type="submit"]').click()
        admin.wait_for_url(base + "/admin")
        expect(admin.locator(".stat-card")).to_have_count(5)
        for tab in ["orders", "products", "customers", "accounts"]:
            goto(admin, base, "/admin?tab=" + tab, blazor)
            no_error(admin)
            if tab in ["customers", "accounts"]:
                search = admin.locator('.admin-content .filters input:not([type="hidden"])').first
                search.fill(email)
                admin.locator(".admin-content .filters").first.get_by_role(
                    "button", name="Tìm kiếm"
                ).click()
                expect(admin.locator(".admin-content")).to_contain_text(email)
                no_error(admin)
        record(name, "administrator login, dashboard and all five management tabs")
        check_management(admin, api, name, base, email)
        goto(admin, base, "/admin?tab=products&page=2", blazor)
        size = 20 if name == "MVC" else 10
        expected = api.get(f"/api/products?pageSize=100").json()["totalCount"] - size
        expect(admin.locator(".data-table").first.locator("tbody tr")).to_have_count(
            min(size, expected)
        )
        no_error(admin)
        record(name, "administrator product pagination")
        goto(admin, base, "/admin?tab=customers", blazor)
        admin.screenshot(path=str(OUTPUT / f"{name}-customers-desktop.png"), full_page=True)
        for width in [390, 768]:
            admin.set_viewport_size({"width": width, "height": 900})
            assert_fits(admin)
            admin.screenshot(path=str(OUTPUT / f"{name}-customers-{width}.png"), full_page=True)
        page.set_viewport_size({"width": 390, "height": 844})
        goto(page, base, "/", blazor)
        assert_fits(page)
        page.screenshot(path=str(OUTPUT / f"{name}-catalog-mobile.png"), full_page=True)
        record(name, "responsive layout at 390px, 768px and 1440px")
        assert not errors, errors
        admin_context.close()
    except Exception:
        (admin if "admin" in locals() else page).screenshot(
            path=str(OUTPUT / f"{name}-failure.png"), full_page=True
        )
        raise
    finally:
        context.close()


def main():
    with tempfile.TemporaryDirectory(prefix="electroshop-browser-") as directory:
        env = os.environ.copy()
        env.update(
            {
                "ASPNETCORE_ENVIRONMENT": "Development",
                "DOTNET_GCServer": "0",
                "ConnectionStrings__DefaultConnection": "Data Source="
                + str(Path(directory) / "shop.db"),
                "Jwt__Key": secrets.token_hex(48),
                "Seed__AdminEmail": ADMIN_EMAIL,
                "Seed__AdminPassword": PASSWORD,
                "Database__Initialize": "true",
            }
        )
        urls = {}
        try:
            for name, assembly in PROJECTS:
                urls[name] = f"http://127.0.0.1:{available_port()}"
                if name != "API":
                    env["Api__BaseUrl"] = urls["API"] + "/"
                log = (OUTPUT / f"{name}.log").open("w")
                logs.append(log)
                proc = subprocess.Popen(
                    [
                        "dotnet",
                        str(ROOT / name / "bin/Debug/net10.0" / (assembly + ".dll")),
                        "--urls",
                        urls[name],
                    ],
                    cwd=ROOT / name,
                    env=env,
                    stdout=log,
                    stderr=subprocess.STDOUT,
                    start_new_session=True,
                )
                processes.append(proc)
                ready(urls[name] + ("/health" if name == "API" else "/"))
            with sync_playwright() as playwright:
                browser = playwright.chromium.launch()
                api = playwright.request.new_context(base_url=urls["API"])
                for name, _ in PROJECTS[1:]:
                    check_app(browser, api, name, urls[name])
                processes[0].terminate()
                processes[0].wait(timeout=10)
                for name, _ in PROJECTS[1:]:
                    offline_page = browser.new_page()
                    goto(offline_page, urls[name], "/", name == "BlazorWeb")
                    expect(offline_page.locator("main .alert.error")).to_be_visible()
                    expect(offline_page.locator("main")).to_contain_text(re.compile("kết nối.*API"))
                    record(name, "API offline displays a recoverable error")
                    offline_page.close()
                browser.close()
        finally:
            for proc in processes:
                if proc.poll() is None:
                    os.killpg(proc.pid, signal.SIGTERM)
            for proc in processes:
                try:
                    proc.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    os.killpg(proc.pid, signal.SIGKILL)
            for log in logs:
                log.close()
            (OUTPUT / "results.json").write_text(json.dumps(results, ensure_ascii=False, indent=2))
    print(f"{len(results)} browser flow groups passed. Screenshots: {OUTPUT}", flush=True)


if __name__ == "__main__":
    main()
