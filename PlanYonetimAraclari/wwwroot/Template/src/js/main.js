/**
 * Plan345 - Modern Proje Yönetimi
 * ana JavaScript dosyası
 */

// DOM içeriği yüklendiğinde
document.addEventListener('DOMContentLoaded', function() {
    initTestimonialsSlider();
    initSmoothScrolling();
    initMobileMenu();
    initAnimations();
    initTeamSlider();
});

/**
 * Testimonial slider işlevselliği
 */
function initTestimonialsSlider() {
    const sliderContainer = document.querySelector('.testimonials-container');
    if (!sliderContainer) return;
    
    const slider = document.querySelector('.testimonials-slider');
    const prevBtn = document.querySelector('.prev-btn');
    const nextBtn = document.querySelector('.next-btn');
    
    if (!slider || !prevBtn || !nextBtn) return;
    
    const testimonials = slider.querySelectorAll('.testimonial-card');
    if (testimonials.length === 0) return;
    
    // Kaç tane testimonial gösterilecek (ekran boyutuna göre değişebilir)
    let visibleCount = 3;
    const totalTestimonials = testimonials.length;
    let currentIndex = 0;
    
    // Her bir testimonial'ı 3 kez klonlayarak sürekli dönmesini sağla
    for (let i = 0; i < 3; i++) {
        testimonials.forEach(testimonial => {
            const clone = testimonial.cloneNode(true);
            slider.appendChild(clone);
        });
    }
    
    // Ekran genişliğine göre görünür testimonial sayısını ayarla
    function updateVisibleCount() {
        if (window.innerWidth < 768) {
            visibleCount = 1;
        } else if (window.innerWidth < 1024) {
            visibleCount = 2;
        } else {
            visibleCount = 3;
        }
    }
    
    // Slider pozisyonunu güncelle
    function updateSlider() {
        updateVisibleCount();
        
        const testimonialWidth = testimonials[0].offsetWidth;
        const margin = parseInt(window.getComputedStyle(testimonials[0]).marginLeft) + 
                      parseInt(window.getComputedStyle(testimonials[0]).marginRight);
        
        const offset = -currentIndex * (testimonialWidth + margin);
        slider.style.transform = `translateX(${offset}px)`;
    }
    
    // Önceki butonu işlevi
    prevBtn.addEventListener('click', () => {
        if (currentIndex > 0) {
            currentIndex--;
        } else {
            // Başa döndüyse, sondan bir önceki gruba atla (döngü efekti için)
            currentIndex = totalTestimonials - 1;
        }
        updateSlider();
    });
    
    // Sonraki butonu işlevi
    nextBtn.addEventListener('click', () => {
        if (currentIndex < totalTestimonials * 3 - visibleCount) {
            currentIndex++;
        } else {
            // Sona geldiyse, başa dön
            currentIndex = 0;
        }
        updateSlider();
    });
    
    // Slider'ı başlangıç durumuna getir
    updateSlider();
    
    // Otomatik kaydırma için interval ayarla (5 saniyede bir)
    const autoSlideInterval = setInterval(() => {
        if (currentIndex < totalTestimonials * 3 - visibleCount) {
            currentIndex++;
        } else {
            // Sıfırlamadan önce yumuşak geçiş için
            slider.style.transition = 'none';
            currentIndex = 0;
            slider.style.transform = `translateX(0px)`;
            // Force reflow
            slider.offsetHeight;
            slider.style.transition = 'transform 300ms ease-in-out';
        }
        updateSlider();
    }, 5000);
    
    // Ekran boyutu değiştiğinde slider'ı güncelle
    window.addEventListener('resize', updateSlider);
    
    // Sayfa kapatıldığında interval'i temizle
    window.addEventListener('beforeunload', () => {
        clearInterval(autoSlideInterval);
    });
}

/**
 * Sayfa içi linklerin yumuşak kaydırması
 */
function initSmoothScrolling() {
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function(e) {
            e.preventDefault();
            
            const targetId = this.getAttribute('href');
            if (targetId === '#') return;
            
            const targetElement = document.querySelector(targetId);
            if (targetElement) {
                window.scrollTo({
                    top: targetElement.offsetTop - 100,
                    behavior: 'smooth'
                });
            }
        });
    });
}

/**
 * Mobil menü kontrolü (hamburger menü)
 */
function initMobileMenu() {
    const mobileMenuBtn = document.querySelector('.mobile-menu-btn');
    const mobileMenu = document.querySelector('.mobile-menu');
    
    if (mobileMenuBtn && mobileMenu) {
        mobileMenuBtn.addEventListener('click', () => {
            mobileMenu.classList.toggle('hidden');
        });
    }
}

/**
 * Sayfa animasyonları
 */
function initAnimations() {
    // Scroll ile açığa çıkan animasyonlar burada eklenebilir
    // Gelecekteki geliştirmeler için yer tutucu
}

// Ekip Üyeleri Slider
function initTeamSlider() {
    const teamSlider = document.querySelector('.team-slider');
    const teamPrevBtn = document.querySelector('.team-prev-btn');
    const teamNextBtn = document.querySelector('.team-next-btn');
    let teamCurrentSlide = 0;
    const teamCards = document.querySelectorAll('.team-card');
    const teamTotalSlides = teamCards.length;

    function updateTeamSlider() {
        const slideWidth = document.querySelector('.team-card').offsetWidth + 16; // card width + margin
        teamSlider.style.transform = `translateX(-${teamCurrentSlide * slideWidth}px)`;
    }

    function nextSlide() {
        if (teamCurrentSlide < teamTotalSlides - (window.innerWidth >= 768 ? 3 : 1)) {
            teamCurrentSlide++;
        } else {
            // Sona geldiğinde başa dön
            teamCurrentSlide = 0;
        }
        updateTeamSlider();
    }

    function prevSlide() {
        if (teamCurrentSlide > 0) {
            teamCurrentSlide--;
        } else {
            // Başa geldiğinde sona git
            teamCurrentSlide = teamTotalSlides - (window.innerWidth >= 768 ? 3 : 1);
        }
        updateTeamSlider();
    }

    teamPrevBtn.addEventListener('click', () => {
        prevSlide();
        // Kullanıcı tıkladığında otomatik kaydırmayı sıfırla
        clearInterval(autoSlideInterval);
        autoSlideInterval = setInterval(nextSlide, 3000);
    });

    teamNextBtn.addEventListener('click', () => {
        nextSlide();
        // Kullanıcı tıkladığında otomatik kaydırmayı sıfırla
        clearInterval(autoSlideInterval);
        autoSlideInterval = setInterval(nextSlide, 3000);
    });

    // Otomatik kaydırma için interval başlat (3 saniyede bir)
    let autoSlideInterval = setInterval(nextSlide, 3000);

    // Mouse slider üzerine geldiğinde otomatik kaydırmayı durdur
    teamSlider.addEventListener('mouseenter', () => {
        clearInterval(autoSlideInterval);
    });

    // Mouse slider üzerinden ayrıldığında otomatik kaydırmayı tekrar başlat
    teamSlider.addEventListener('mouseleave', () => {
        autoSlideInterval = setInterval(nextSlide, 3000);
    });

    // Responsive handling
    window.addEventListener('resize', () => {
        // Ekran boyutu değiştiğinde slider'ı başa al ve güncelle
        teamCurrentSlide = 0;
        updateTeamSlider();
    });

    // Sayfa kapatıldığında interval'i temizle
    window.addEventListener('beforeunload', () => {
        clearInterval(autoSlideInterval);
    });
}