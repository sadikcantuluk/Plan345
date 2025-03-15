# Plan Yönetim Araçları

Plan Yönetim Araçları, projelerinizi ve görevlerinizi kolayca yönetmenize yardımcı olan modern, kullanıcı dostu bir web uygulamasıdır. Trello benzeri bir arayüz ile görevlerinizi sürükle-bırak şeklinde düzenleyebilir ve proje iş akışınızı verimli bir şekilde organize edebilirsiniz.

## Özellikler

### Proje Yönetimi
- Sınırsız proje oluşturma ve yönetme
- Projeler için durumlar: Planlama, Devam Ediyor, Tamamlandı, Beklemede, İptal Edildi
- Proje açıklamaları, bitiş tarihleri ve tamamlanma yüzdeleri
- Dashboard üzerinden proje istatistiklerini görüntüleme

### Görev Yönetimi
- Her proje için görevler oluşturma
- Trello benzeri kanban görünümü: Yapılacak, Devam Ediyor, Tamamlandı
- Görevleri sürükle-bırak ile kolayca taşıma
- Öncelik seviyeleri: Düşük, Orta, Yüksek, Acil
- Görev açıklamaları ve bitiş tarihleri

### Kullanıcı Dostu Arayüz
- Responsive tasarım - mobil cihazlarda da çalışır
- Modern ve temiz arayüz
- Gerçek zamanlı güncellemeler
- Kullanıcı profili ve ayarları
- Kolay gezinme için sidebar menüsü

## Kullanılan Teknolojiler

- **Backend**: ASP.NET Core 6.0 MVC
- **Veritabanı**: Entity Framework Core & MSSQL Server
- **Frontend**: HTML, CSS, JavaScript, jQuery
- **CSS Framework**: Tailwind CSS
- **İkonlar**: Font Awesome
- **Sürükle-Bırak**: SortableJS
- **Kimlik Doğrulama**: ASP.NET Core Identity

## Kurulum

### Gereksinimler
- .NET 6.0 SDK veya üzeri
- Veritabanı (SQLite veya SQL Server)
- Güncel bir web tarayıcısı

### Adımlar

1. Repo'yu klonlayın:
   ```
   git clone https://github.com/sadikcantuluk/345Backend.git
   cd 345Backend
   ```

2. Bağımlılıkları yükleyin:
   ```
   dotnet restore
   ```

3. Veritabanını oluşturun:
   ```
   dotnet ef database update
   ```

4. Uygulamayı başlatın:
   ```
   dotnet run
   ```

5. Tarayıcınızda aşağıdaki URL'i açın:
   ```
   http://localhost:7091
   ```

## Kullanım

### Hesap Oluşturma ve Giriş
1. "Kayıt Ol" sayfasından yeni bir hesap oluşturun
2. E-posta ve şifrenizle giriş yapın
3. Profil sayfasından kişisel bilgilerinizi ve profil resminizi güncelleyin

### Proje Oluşturma
1. Dashboard üzerinden "Yeni Proje Oluştur" butonuna tıklayın
2. Proje adı, açıklaması, durumu ve bitiş tarihini girin
3. "Oluştur" butonuna tıklayarak projenizi kaydedin

### Görev Yönetimi
1. Proje sayfasına giderek görevlerinizi yönetin
2. "Yeni Görev Ekle" butonuyla görev oluşturun
3. Görevleri kanban paneli üzerinde sürükleyerek durumlarını değiştirin
4. Görev kartlarındaki butonları kullanarak görevleri farklı durumlara taşıyın

### Proje İstatistikleri
1. Dashboard üzerinden projelerinizin istatistiklerini görüntüleyin
2. Tamamlanmış, devam eden ve bekleyen projelerin sayılarını takip edin

## Katkıda Bulunma

Katkılarınızı bekliyoruz! Eğer projeye katkıda bulunmak isterseniz:

1. Repo'yu forklayın
2. Yeni bir feature branch oluşturun (`git checkout -b feature/amazing-feature`)
3. Değişikliklerinizi commit edin (`git commit -m 'Add some amazing feature'`)
4. Branch'inizi push edin (`git push origin feature/amazing-feature`)
5. Pull Request açın

## Lisans

Bu proje MIT lisansı altında lisanslanmıştır. Daha fazla bilgi için `LICENSE` dosyasına bakınız.

## İletişim

Destek - [Plan345 Destek](mailto:plan345destek@gmail.com)
Destek - [Sadık Can Tuluk](mailto:sadikcantuluk@gmail.com)

Proje Linki: [https://github.com/sadikcantuluk/345Backend](https://github.com/sadikcantuluk/345Backend) 