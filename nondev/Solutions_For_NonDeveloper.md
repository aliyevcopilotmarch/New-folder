## 1. Ensure Application Pool Identity is Configured for All Application Pools

**Kateqoriya:** Permission Management

### Problem Təsviri

IIS-də application pool-lar defolt olaraq `NetworkService` və ya `LocalSystem` kimi yüksək səlahiyyətli hesablarla işləyə bilər. Bu vəziyyətdə bir proqramda aşkar edilən boşluq digər proqramlara və hətta bütün əməliyyat sisteminə təsir edə bilər, çünki eyni identifikasiya paylaşılır. Əgər hücumçu bir application pool vasitəsilə daxil olarsa, eyni identity altında işləyən bütün saytlara və servislərə çatış əldə edir.

### Həll Yolu

Hər bir application pool üçün `ApplicationPoolIdentity` istifadə edilməlidir. Bu, IIS 7.5+ versiyalarında daxili "virtual account" mexanizmindən istifadə edir — hər pool üçün avtomatik unikal hesab yaradılır.

IIS Manager vasitəsilə tətbiq etmək üçün:

1. **IIS Manager**-i açın (Start menyusunda `inetmgr` yazın)
2. Sol paneldə **Application Pools** bölməsini seçin
3. Hər bir application pool üzərinə sağ klik edin və **Advanced Settings** seçin
4. Açılan pəncərədə **Process Model** bölməsini tapın
5. **Identity** sahəsinin yanındakı `...` düyməsinə basın
6. **Built-in account** seçimini edin, açılan siyahıdan **ApplicationPoolIdentity** seçin
7. **OK** basın
8. Bütün application pool-lar üçün bu addımları təkrarlayın

---

## 2. Ensure Default IIS Web Log Location is Moved

**Kateqoriya:** Logging

### Problem Təsviri

IIS defolt olaraq log fayllarını sistem diskində (C:\) saxlayır. Bu, aşağıdakı riskləri yaradır:

- Log faylları böyüdükcə sistem diskini doldura bilər və bu serverin tamamilə çökməsinə səbəb ola bilər
- Hücumçu sistem diskindəki faylları dəyişdirə bilərsə, log fayllarını silib izlərini gizlədə bilər
- Sistem diskinə artan yük performansı azaldır
- Log faylları itirilsə, qanunvericilik tələbləri pozula bilər

### Həll Yolu

Log fayllarını ayrı bir diskə (məsələn, `D:\IISLogs`) köçürmək lazımdır.

---

## 3. Ensure Deployment Method Retail is Set

**Kateqoriya:** Information Disclosure

### Problem Təsviri

Server "retail" (istehsal) rejimində konfiqurasiya edilmədikdə aşağıdakı ciddi məlumat sızması problemləri yaranır:

- Debug rejimi aktiv qaldığı üçün xəta mesajlarında server strukturu, fayl yolları və verilənlər bazası məlumatları göstərilir
- `Trace.axd` səhifəsi vasitəsilə tətbiqin daxili məlumatları kənar şəxslərə əlçatan olur
- Xüsusi xəta səhifələri söndürüldüyü üçün hücumçular server xətalarının tam təfərrüatını görür
- Performans azalır, çünki debug simvolları yüklənir

### Həll Yolu

Bu parametr birbaşa IIS Manager vasitəsilə deyil, .NET konfiqurasiya faylı üzərindən tənzimlənir:

- `%windir%\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config` faylının `<system.web>` bölməsində `<deployment retail="true" />` sətri əlavə edilməlidir
- Bu dəyişiklik debug rejimini, trace funksiyasını söndürür və xüsusi xəta səhifələrini aktiv edir
- Dəyişiklik edilmədən əvvəl faylın ehtiyat nüsxəsi mütləq çıxarılmalıdır

---

## 4. Ensure Dynamic IP Address Restrictions is Enabled

**Kateqoriya:** Denial of Service Attacks

### Problem Təsviri

Dynamic IP Address Restrictions (Dinamik IP Məhdudiyyətləri) olmadıqda IIS serveri aşağıdakı hücumlara açıqdır:

- DDoS hücumları — eyni IP ünvanından minlərlə eyni vaxtlı sorğu göndərilərək server resursları tükəndilir
- Brute-force hücumları — giriş səhifələrinə saniyədə yüzlərlə sorğu göndərilərək parollar sınanır
- Application-layer flooding — çox ağır sorğular göndərərək tətbiq yavaşladılır

Bu xüsusiyyət eyni IP ünvanından gələn həddindən artıq sorğuları avtomatik bloklayır.

### Həll Yolu
   - **Deny IP Address based on the number of concurrent requests** → dəyər: `10`
   - **Deny IP Address based on the number of requests over a period of time** → dəyər: `30` sorğu, `300` millisaniyə

---

## 5. Ensure Global Authorization Rule is Set to Restrict Access

**Kateqoriya:** Permission Management

### Problem Təsviri

IIS defolt olaraq bütün istifadəçilərə bütün resurslara çatmağa icazə verir. Authorization (icazə) qaydası olmadıqda:

- Autentifikasiya olmadan həssas resurslara çatmaq mümkündür
- Anonim istifadəçilər daxili web servislərə (Core servislər) daxil ola bilir
- Ən az imtiyaz prinsipi pozulur — hər kəs eyni icazəyə malikdir
- Daxili API-lar kənar şəxslər tərəfindən çağırıla bilər

### Həll Yolu

IIS Manager vasitəsilə tətbiq etmək üçün:

1. **IIS Manager**-i açın
2. Sol paneldə **TayqaSale** saytını genişləndirin, **ServicePortal → Core** qovluğunu seçin
3. Ortadakı paneldə **Authorization Rules** ikonasına iki dəfə klik edin
4. Mövcud **Allow All Users** (Bütün İstifadəçilərə İcazə Ver) qaydasını seçin və silin
5. Sağ paneldə **Add Allow Rule** düyməsinə basın
6. **Specified roles or user groups** seçimini edin və `TayqaSaleAdmins` yazın
7. **OK** basın
8. Yenidən sağ paneldə **Add Deny Rule** basın, **All users** seçin, **OK** basın
9. Bu proseduru `TayqaSale/CampaignManagementPortal/Core` və `TayqaSale/DynamicToolManagementPortal/Core` üçün də təkrarlayın

---

## 6. Ensure Host Headers Are on All Sites

**Kateqoriya:** Denial of Service Attacks

### Problem Təsviri

Host header (sayt adı) olmadan IIS saytı yalnız IP ünvanı və port nömrəsi ilə tanınır. Bu vəziyyətdə:

- Hücumçu serverin IP ünvanına istənilən domain adı ilə sorğu göndərə bilər
- DNS rebinding hücumları vasitəsilə zərərli veb saytlar serveri öz domainləri kimi istifadə edə bilər
- Eyni IP-də birdən çox sayt varsa, düzgün sayt seçilməyə bilər
- Hücumçular qanuni serveri öz phishing səhifələri üçün istifadə edə bilər

### Həll Yolu

Hər bir IIS saytının binding-lərində host header (domain adı) təyin etmək lazımdır.

---

## 7. Ensure HTTP Trace Method is Disabled

**Kateqoriya:** Dangerous Methods Enabled

### Problem Təsviri

HTTP TRACE metodu diaqnostik məqsədlər üçün yaradılmışdır, lakin ciddi təhlükəsizlik boşluqlarına səbəb olur:

- Cross-Site Tracing (XST) hücumları vasitəsilə hücumçu istifadəçinin giriş cookie-lərini oğurlaya bilər (hətta HttpOnly ilə qorunan cookie-lər belə)
- TRACE cavabı `Authorization` başlığını əks etdirir, bu da istifadəçi məlumatlarının ifşasına gətirib çıxarır
- Oğurlanmış session token vasitəsilə istifadəçinin hesabına daxil olmaq mümkün olur

### Həll Yolu
IIS də **HTTP Verbs** -hissəsində Deny Verb -lərin icinə TRACE və TRACK əlavə etmək lazımdır.


---

## 8. Ensure HttpCookie Mode is Configured for Session State

**Kateqoriya:** Session Hijacking

### Problem Təsviri

Session idarəetməsi düzgün konfiqurasiya edilmədikdə session ID (istifadəçi sessiya kodu) URL-də görünür. Yəni belə bir URL yaranır: `http://tayqasale.az/(S(lit3py55t21z5v55vlm25s55))/orderform.aspx`. Bu, ciddi problemlər yaradır:

- Hücumçu qurbana bu tip URL göndərərək onun adından sistemə daxil ola bilir (Session Fixation)
- Session ID referrer başlığı, brauzer tarixçəsi və log faylları vasitəsilə ifşa olunur
- İstifadəçi URL-i başqası ilə paylaşanda session ID-ni də paylaşmış olur
- URL bookmark edilərsə, session ID bookmark-da saxlanır

### Həll Yolu

Bu parametr web.config faylı vasitəsilə tənzimlənir.bütün servislərin `web.config` fayllarında `<sessionState cookieless="UseCookies" />` parametri əlavə edilməlidir:

---

## 9. Ensure Non-ASCII Characters in URLs are Not Allowed

**Kateqoriya:** Brute Force Attacks

### Problem Təsviri

URL-lərdə standart olmayan (non-ASCII) simvollara icazə verilməsi aşağıdakı hücumlara yol açır:

- Hücumçular zərərli URL-ləri kodlaşdırılmış simvollarla gizlədə bilir
- `%c0%ae%c0%ae` kimi xüsusi simvol ardıcıllıqları ilə `../` əvəzinə istifadə edilərək qovluqlar aşılır (directory traversal)
- Təhlükəsizlik filtrləri bu simvollar vasitəsilə keçilə bilir
- Geniş simvol dəsti ilə URL-ləri sınamaq hücumçular üçün daha asan olur

### Həll Yolu
 
IIS -də Allow high-bit characters checkbox-unun işarəsini götürmək lazımdır.
---

## 10. Ensure Unique Application Pools is Set for Sites

**Kateqoriya:** Permission Management

### Problem Təsviri

Birdən çox saytın eyni application pool-u paylaşması ciddi təhlükəsizlik riski yaradır:

- Bir saytdakı boşluq istifadə edilərək eyni pool-dakı digər saytlara çatmaq mümkün olur
- Bir sayt digər saytların yaddaşına və fayllarına çatışa bilər
- Bir saytın yüksək yükü digər bütün saytları yavaşladır
- Hətta bir tətbiq çöksə, eyni pool-dakı bütün saytlar da çökür

### Həll Yolu

Hər bir TayqaSale web servisi üçün ayrı application pool yaradılmalıdır.

## 11. Ensure Unlisted File Extensions are Not Allowed

**Kateqoriya:** Permission Management

### Problem Təsviri

IIS defolt olaraq hər cür fayl uzantısına sorğu göndərməyə icazə verir. Bu, ciddi problemlər yaradır:

- `.aspx`, `.php`, `.exe` kimi fayllar yüklənib icra oluna bilər
- `.config`, `.xml`, `.ini` kimi konfiqurasiya faylları oxuna bilər
- `.cs`, `.vb` kimi mənbə kod faylları ictimai ola bilər
- `.old`, `.bak`, `.swp` kimi ehtiyat faylları vasitəsilə köhnə məlumatlar əldə edilə bilər

### Həll Yolu

Yalnız lazım olan fayl uzantılarına icazə vermək (whitelist yanaşması) lazımdır.


---

## 12. Ensure Web Content is on Non-System Partition

**Kateqoriya:** Sensitive File Access

### Problem Təsviri

Web məzmunun sistem diskində (C:\) saxlanması aşağıdakı riskləri yaradır:

- Directory traversal hücumları vasitəsilə hücumçu `../../windows/system32/` kimi yollarla sistem fayllarına çatışa bilər
- Web məzmun və ya log faylları böyüyərək sistem diskini doldursa, əməliyyat sistemi çökə bilər
- Web kataloqunda yaradılmış simvolik keçid (symlink) vasitəsilə sistem faylları oxuna bilər
- Web prosesi vasitəsilə sistem fayllarına çatışma ehtimalı artır

Hal-hazırda TayqaSale servislərinin bəzilərinin `%SystemDrive%\inetpub\wwwroot` yolunda olması bu riski artırır.

### Həll Yolu

Bütün web məzmunun başqa bir diskə (məsələn, `D:\WebContent\TayqaSale`) köçürülməsi lazımdır.
