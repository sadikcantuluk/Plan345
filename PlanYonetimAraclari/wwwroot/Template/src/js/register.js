document.addEventListener('DOMContentLoaded', function() {
    const registerForm = document.getElementById('registerForm');
    const password = document.getElementById('password');
    const confirmPassword = document.getElementById('confirmPassword');

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

    registerForm.addEventListener('submit', function(e) {
        e.preventDefault();

        // Temel form doğrulaması
        if (password.value !== confirmPassword.value) {
            alert('Şifreler eşleşmiyor!');
            return;
        }

        // Form verilerini topla
        const formData = {
            firstName: document.getElementById('firstName').value,
            lastName: document.getElementById('lastName').value,
            email: document.getElementById('email').value,
            password: password.value
        };

        // Normalde bu verileri backend'e gönderirsiniz
        console.log('Form data:', formData);
        
        // Demo amaçlı başarı mesajı göster
        alert('Kayıt başarılı! Giriş sayfasına yönlendiriliyorsunuz...');
        window.location.href = 'login.html';
    });

    // Sosyal medya giriş butonlarını yönet
    document.querySelector('button[type="button"]:first-of-type').addEventListener('click', function() {
        console.log('Google ile kayıt ol');
    });

    document.querySelector('button[type="button"]:last-of-type').addEventListener('click', function() {
        console.log('GitHub ile kayıt ol');
    });
});