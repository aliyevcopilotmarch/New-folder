
## 1. TLS 1.0 Deaktiv Etmə

### Problem
TLS 1.0 köhnəlmiş protokoldur və BEAST, POODLE kimi hücumlara həssasdır. Müasir standartlara görə deaktiv edilməlidir.

### Həllin Ümumi Təsviri
> **Windows Registry-də TLS 1.0 protokolunu həm server, həm client tərəfdə söndürmək — beləliklə serverin köhnə və təhlükəli protokol üzərindən bağlantı qəbul etməsinin qarşısını almaq.**

### Təsir
TLS 1.0 istifadə edən çox köhnə brauzerlər (IE6, IE7) qoşula bilməyəcək. Müasir brauzerlər TLS 1.2/1.3 istifadə edir, problem yaranmayacaq.

## 2. DES və 3DES (Triple DES) Cipher Suites Deaktiv Etmə

### Problem
DES (56-bit) və 3DES (168-bit) zəif şifrələmə alqoritmləridir. Sweet32 hücumuna həssasdır.

### Həllin Ümumi Təsviri
> **Registry-də DES 56/56 və Triple DES 168 cipher-lərini deaktiv etmək — serverin zəif şifrələmə alqoritmləri ilə məlumat ötürməsini qadağan etmək.**


## 3. RC4 Cipher Suites Deaktiv Etmə

### Problem
RC4 alqoritmi sındırılmış sayılır (RFC 7465 ilə qadağan edilib). Müasir skan alətləri bunu kritik boşluq kimi qeyd edir.

### Həllin Ümumi Təsviri
> **RC4 alqoritminin bütün variantlarını (40, 56, 64, 128-bit) Registry-dən deaktiv etmək — RFC 7465 standartına uyğun olaraq sındırılmış stream cipher-in tamamilə aradan qaldırılması.**

## 4. NULL Cipher Suites Deaktiv Etmə

### Problem
NULL cipher heç bir şifrələmə tətbiq etmir — məlumat açıq şəkildə ötürülür. Bu, HTTPS-in məqsədini tamamilə sıradan çıxarır.

### Həllin Ümumi Təsviri
> **Registry-də NULL cipher-i deaktiv etmək — şifrələmə olmadan bağlantı qurulmasını tamamilə qadağan etmək, bütün trafikin mütləq şifrələnməsini təmin etmək.**

## 5. MD5 Hashing Alqoritmi Deaktiv Etmə

### Problem
MD5 hash alqoritmi kriptoqrafik cəhətdən sındırılıb (collision attack mümkündür). SHA-256 və ya SHA-384 istifadə edilməlidir.

### Həllin Ümumi Təsviri
> **Registry-də MD5 hash alqoritmini deaktiv etmək — collision hücumlarına həssas olan köhnə hashing mexanizmini aradan qaldırıb, serveri yalnız SHA-256/SHA-384 kimi güclü alqoritmlərdən istifadə etməyə məcbur etmək.**

## 6. Perfect Forward Secrecy (PFS) Təmin Etmə

### Problem
PFS olmadan, server açarı sındırılarsa, əvvəlki bütün şifrəli trafik deşifrə edilə bilər. PFS hər sessiya üçün müvəqqəti açar istifadə edir.

### Həllin Ümumi Təsviri
> **Cipher suite sırasını dəyişdirərək yalnız ECDHE (Elliptic Curve Diffie-Hellman Ephemeral) əsaslı key exchange alqoritmlərini aktiv saxlamaq — hər sessiyaya unikal müvəqqəti açar təyin edərək, server açarı oğurlansa belə keçmiş trafikin qorunmasını təmin etmək.**

## 7. IIS Default Cipher Suites Dəyişdirilməsi

### Problem
IIS default cipher suite sırasına zəif alqoritmlər (RC4, DES, 3DES, NULL, MD5) daxildir. Bu, yuxarıdakı bütün problemlərin kök səbəbidir.

### Həllin Ümumi Təsviri
> **IIS-in standart olaraq gələn zəif konfiqurasiyasını tamamilə dəyişdirmək — köhnə protokolları (SSL 2.0, SSL 3.0, TLS 1.0, TLS 1.1) söndürmək, yalnız TLS 1.2 aktiv saxlamaq və bütün zəif cipher/hash alqoritmlərini aradan qaldırmaq. Bu addım əslində 1-6 addımların yekun tamamlayıcısıdır.**

## Tətbiq Sırası (Tövsiyə Olunan)

```
Addım 1: Registry backup 
Addım 2: TLS 1.0 deaktiv edin (#1)
Addım 3: DES/3DES deaktiv edin (#2)
Addım 4: RC4 deaktiv edin (#3)
Addım 5: NULL cipher deaktiv edin (#4)
Addım 6: MD5 deaktiv edin (#5)
Addım 7: PFS cipher suite sırasını tətbiq edin (#6)
Addım 8: TLS 1.1, SSL 2.0, SSL 3.0 deaktiv edin (#7)
