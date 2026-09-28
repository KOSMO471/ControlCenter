# ⚡ Control Center & Mascot Assistant

**Control Center**, Windows işletim sisteminiz için donanım takibi, sistem yönetimi, hızlı erişim araçları ve etkileşimli bir masaüstü maskotunu tek bir çatı altında toplayan kapsamlı bir WPF (.NET 8) masaüstü uygulamasıdır.

![Windows](https://img.shields.io/badge/OS-Windows-blue.svg)
![Framework](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Language](https://img.shields.io/badge/C%23-WPF-brightgreen.svg)
![License](https://img.shields.io/badge/License-MIT-yellow.svg)

---

## ✨ Öne Çıkan Özellikler

### 📊 Sistem & Donanım İzleme
* **Anlık Sistem Metrikleri:** CPU, RAM, GPU kullanımı/sıcaklıkları, fan hızı (RPM) ve disk durumlarını anlık takip edin.
* **Ağ Hızı İzleme:** Anlık indirme (Download) ve yükleme (Upload) hızlarınızı görüntüleyin.
* **Ses Kontrolü:** Sistem ana ses seviyesini uygulama üzerinden kolayca yönetin.
* **İşlem Yöneticisi (Task Manager):** Çalışan sistem süreçlerini görün ve yüksek kaynak tüketen uygulamaları sonlandırın.

### 🐣 Etkileşimli Masaüstü Maskotu (Kosmo)
* **Dinamik Durum ve Animasyonlar:** Tıklamalara, duruma (uyku, şaşırma, zıplama vb.) ve sistem olaylarına tepki veren animasyonlu maskot.
* **Gece Gece / Uyku Modu:** Gece saatlerinde (20:00 - 06:00) otomatik olarak uyku moduna geçer.
* **Sistem Tepkileri:** Ekran görüntüsü alma, dilsiz/sessiz mod, düşük pil ve hava durumu güncellemelerine özel animasyonlar.
* **Paskalya Yumurtaları (Easter Eggs):** Özel klavye kombinasyonları (Konami kodu vb.) ile açılan gizli modlar (Nyan mode, Darkness vb.).

### 🧰 Bütünleşik Araçlar & Widget'lar
* **Dosya Gezgini:** Metin, görsel, PDF ve medya dosyalarını uygulama içerisinden önizleyin ve düzenleyin.
* **Entegre Terminal (CMD & PowerShell):** Hızlı komut çalıştırma ve gelişmiş "Hızlı İşlemler" desteği.
* **Pomodoro Zamanlayıcı & Not Defteri:** Odaklanma sürelerinizi yönetin ve hızlı notlar alın.
* **Hesap Makinesi & Pano Geçmişi:** Matematiksel işlemlerinizi yapın ve kopyaladığınız içeriklerin geçmişine erişin.
* **Bluetooth & Medya Entegrasyonu:** Bağlı Bluetooth cihazlarını kontrol edin ve çalan medyayı yönetin.

### 🏆 Başarım Sistemleri (Achievements)
* 30'dan fazla kazanılabilir başarım ile sistem kullanımınızı eğlenceli hale getirin (Gece Kuşu, Maratoncu, Terminal Çaylağı vb.).

---

## 🛠️ Teknolojiler ve Kütüphaneler

* **Framework:** .NET 8.0 (WPF & Windows Forms entegrasyonu)
* **Donanım Takibi:** `LibreHardwareMonitorLib`, `System.Diagnostics.PerformanceCounter`
* **Ses & Medya:** `NAudio`, `Windows.Media.Control`
* **UI Entegrasyonu:** `Hardcodet.NotifyIcon.Wpf` (Sistem Tepsi İkonu)
* **Veri Hazırlama:** `Newtonsoft.Json`, `DocumentFormat.OpenXml`

---

## 🚀 Kurulum ve Çalıştırma

### Gereksinimler
* Windows 10 (Sürüm 19041 veya üzeri)
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Derleme Adımları

1. Repoyu bilgisayarınıza klonlayın:
   ```bash
   git clone [https://github.com/KOSMO471/ControlCenter.git](https://github.com/KOSMO471/ControlCenter.git)
   cd ControlCenter
