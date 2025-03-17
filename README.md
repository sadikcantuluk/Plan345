# Plan345 - Modern Proje Yönetim Aracı

![Project Logo](https://via.placeholder.com/150x150)

## Proje Tanıtımı

Plan345, ekiplerin projelerini verimli bir şekilde yönetmelerini sağlayan kullanıcı dostu bir proje yönetim platformudur. Gerçek zamanlı güncellemeler, sürükle-bırak görev yönetimi ve ekip işbirliği özellikleriyle projelerin zamanında ve bütçe dahilinde tamamlanmasını kolaylaştırır.

## Temel Özellikler

- **Kullanıcı Dostu Arayüz**: Sezgisel bir kullanıcı arayüzü ile projeleri ve görevleri hızlıca yönetin
- **Görev Yönetimi**: Görevleri oluşturun, düzenleyin ve farklı durumlara sürükleyip bırakın (Yapılacak, Devam Ediyor, Tamamlandı)
- **Gerçek Zamanlı Güncellemeler**: SignalR teknolojisi ile ekip üyeleri arasında anlık güncellemeler
- **Ekip İşbirliği**: Projelere ekip üyelerini ekleyin ve izinleri yönetin
- **İş Akışı Takibi**: Görsel proje ilerleme ve tamamlanma durumu takibi
- **Bildirim Sistemi**: Önemli proje etkinlikleri için bildirimler
- **Responsive Tasarım**: Masaüstü, tablet ve mobil cihazlarda kusursuz deneyim

## Teknolojiler

- **Backend**: ASP.NET Core MVC
- **Frontend**: HTML5, CSS3, JavaScript, jQuery
- **Veritabanı**: SQL Server
- **Kimlik Doğrulama**: ASP.NET Core Identity
- **Gerçek Zamanlı İletişim**: SignalR
- **UI Framework**: Tailwind CSS
- **Sürükle-Bırak**: SortableJS
- **AJAX**: Asynchronous JavaScript

## Kurulum ve Çalıştırma

### Gereksinimler

- .NET 6.0 SDK veya üzeri
- SQL Server
- Visual Studio 2022 veya VS Code

### Adımlar

1. Projeyi klonlayın:
   ```
   git clone https://github.com/kullaniciadi/Plan345.git
   cd Plan345
   ```

2. Veritabanı bağlantı dizesini `appsettings.json` dosyasında ayarlayın.

3. Veritabanını oluşturun:
   ```
   dotnet ef database update
   ```

4. Uygulamayı başlatın:
   ```
   dotnet run
   ```

5. Tarayıcınızda `https://localhost:7091` adresini açın.

## Kullanım

1. Hesap oluşturun veya mevcut hesabınızla giriş yapın.
2. Yeni bir proje oluşturun ve ekip üyelerini davet edin.
3. Projelere görevler ekleyin ve durumlarını güncelleyin.
4. Görevleri farklı durumlar arasında sürükleyip bırakarak durumlarını güncelleyin.
5. Ekip üyelerinin güncellemelerini gerçek zamanlı olarak takip edin.

## Ekran Görüntüleri

![Dashboard](https://via.placeholder.com/800x450)
*Proje Yönetim Paneli*

![Tasks](https://via.placeholder.com/800x450)
*Görev Kanban Tahtası*

![Project Details](https://via.placeholder.com/800x450)
*Proje Detayları*

## Katkıda Bulunma

1. Bu depoyu forklayın
2. Yeni özellik dalınızı oluşturun (`git checkout -b feature/AmazingFeature`)
3. Değişikliklerinizi commit edin (`git commit -m 'Add some AmazingFeature'`)
4. Dalınıza push yapın (`git push origin feature/AmazingFeature`)
5. Pull request oluşturun

## Lisans

Bu proje [MIT Lisansı](LICENSE) altında lisanslanmıştır.

## İletişim

Proje Destek - [email@example.com](mailto:plan345destek@gmail.com)
Proje İletişim - [email@example.com](mailto:sadikcantuluk@gmail.com)

Proje Linki: [https://github.com/kullaniciadi/Plan345](https://github.com/sadikcantuluk/345Backend) 