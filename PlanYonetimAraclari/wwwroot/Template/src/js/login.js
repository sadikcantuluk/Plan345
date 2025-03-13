document.addEventListener('DOMContentLoaded', function() {
    const loginForm = document.getElementById('loginForm');
    const emailInput = document.getElementById('email');
    const passwordInput = document.getElementById('password');

    // Password visibility toggle function
    window.togglePasswordVisibility = function(inputId) {
        const input = document.getElementById(inputId);
        const icon = document.getElementById(`${inputId}-toggle-icon`);

        if (input.type === 'password') {
            input.type = 'text';
            icon.classList.remove('fa-eye');
            icon.classList.add('fa-eye-slash');
        } else {
            input.type = 'password';
            icon.classList.remove('fa-eye-slash');
            icon.classList.add('fa-eye');
        }
    }

    // E-posta doğrulama fonksiyonu
    function isValidEmail(email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return emailRegex.test(email);
    }

    // Şifre doğrulama fonksiyonu
    function isValidPassword(password) {
        return password.length >= 6;
    }

    // Form gönderme işleyicisi
    loginForm.addEventListener('submit', function(e) {
        e.preventDefault();

        // Mevcut hata stillerini sıfırla
        emailInput.classList.remove('border-danger');
        passwordInput.classList.remove('border-danger');

        let isValid = true;
        let errorMessage = '';

        // E-posta doğrula
        if (!isValidEmail(emailInput.value)) {
            emailInput.classList.add('border-danger');
            errorMessage = 'Lütfen geçerli bir e-posta adresi girin.';
            isValid = false;
        }

        // Şifre doğrula
        if (!isValidPassword(passwordInput.value)) {
            passwordInput.classList.add('border-danger');
            errorMessage = errorMessage || 'Şifre en az 6 karakter olmalıdır.';
            isValid = false;
        }

        if (!isValid) {
            // Hata mesajını göster
            alert(errorMessage);
            return;
        }

        // Doğrulama başarılı ise, giriş işlemini burada yapabilirsiniz
        // Gösterim amaçlı, sadece başarı mesajı gösterelim
        alert('Giriş başarılı! Yönlendiriliyorsunuz...');
        
        // Başarılı girişten sonra yönlendirmeyi simüle et
        setTimeout(() => {
            window.location.href = 'index.html';
        }, 1500);
    });

    // Gerçek zamanlı doğrulama geri bildirimi
    emailInput.addEventListener('input', function() {
        if (this.value && !isValidEmail(this.value)) {
            this.classList.add('border-danger');
        } else {
            this.classList.remove('border-danger');
        }
    });

    passwordInput.addEventListener('input', function() {
        if (this.value && !isValidPassword(this.value)) {
            this.classList.add('border-danger');
        } else {
            this.classList.remove('border-danger');
        }
    });
});