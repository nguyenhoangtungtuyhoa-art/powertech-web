// PowerTech Storefront Global Scripts

// Helper to set cookie in JS
function setCartCookie(count) {
    var d = new Date();
    d.setTime(d.getTime() + (30*24*60*60*1000));
    var expires = "expires="+ d.toUTCString();
    // Đặt path=/ là cực kỳ quan trọng để tất cả các trang đều dùng chung 1 cookie
    document.cookie = "PT_CartCount=" + count + ";" + expires + ";path=/";
    localStorage.setItem('PT_CartCount', count);
    
    // Cập nhật tất cả các badge hiển thị trên trang (Desktop, Mobile, etc.)
    $('.cart-count').each(function() {
        $(this).text(count);
    });
}

// Update Cart Badge Count
function updateHeaderCartCount() {
    $.ajax({
        url: '/Store/Cart/GetCartCount',
        type: 'GET',
        cache: false,
        success: function (count) {
            $('.cart-count').text(count);
            setCartCookie(count);
        }
    });
}

// Global Add to Cart function for Product Cards
function quickAddToCart(productId) {
    // Optimistic Update: Tăng số lượng ngay lập tức trên giao diện
    var $CartBadge = $('.cart-count');
    var currentCount = parseInt($CartBadge.text()) || 0;
    var newCount = currentCount + 1;
    $CartBadge.text(newCount);
    setCartCookie(newCount);
    
    // Hiệu ứng "pop" nhẹ để thông báo có thay đổi
    $CartBadge.addClass('animate-pop');
    setTimeout(function() {
        $CartBadge.removeClass('animate-pop');
    }, 300);

    $.ajax({
        url: '/Store/Cart/Add',
        type: 'POST',
        data: {
            productId: productId,
            quantity: 1,
            __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
        },
        success: function (res) {
            if (res.success) {
                // Đồng bộ lại con số chính xác từ Server trả về
                $('.cart-count').text(res.count);
                setCartCookie(res.count);
                
                Swal.fire({
                    title: 'Thành công!',
                    text: res.message,
                    icon: 'success',
                    toast: true,
                    position: 'top-end',
                    showConfirmButton: false,
                    timer: 3000,
                    timerProgressBar: true
                });
            } else {
                // Nếu lỗi, trả lại số cũ
                $('.cart-count').text(currentCount);
                setCartCookie(currentCount);
                
                Swal.fire({
                    title: 'Thông báo',
                    text: res.message,
                    icon: 'info',
                    showCancelButton: true,
                    confirmButtonText: 'Đăng nhập ngay',
                    confirmButtonColor: '#D7262E'
                }).then((result) => {
                    if (result.isConfirmed) {
                        window.location.href = '/Identity/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
                    }
                });
            }
        },
        error: function () {
            // Nếu lỗi mạng, trả lại số cũ
            $('.cart-count').text(currentCount);
            setCartCookie(currentCount);
            Swal.fire('Lỗi', 'Không thể thêm sản phẩm, vui lòng thử lại!', 'error');
        }
    });
}

// Initialize and handle back navigation
$(document).ready(function () {
    // Luôn lưu số lượng hiện tại vào storage/cookie khi vừa load trang
    var currentDisplayCount = $('.cart-count').text();
    setCartCookie(currentDisplayCount);
});

// Xử lý khi người dùng nhấn nút Quay lại (Back) của trình duyệt
window.addEventListener('pageshow', function (event) {
    // Phục hồi số lượng từ Storage NGAY LẬP TỨC để tránh flicker
    var savedCount = localStorage.getItem('PT_CartCount');
    if (savedCount !== null) {
        $('.cart-count').text(savedCount);
    }
    
    // Sau đó vẫn gọi Server để đồng bộ lại dữ liệu thực tế nhất
    if (event.persisted || (typeof window.performance != "undefined" && window.performance.navigation.type === 2)) {
        updateHeaderCartCount();
    }
});
