# SSL Sertifikatı Olmadan Həll Edilə Bilən Misconfigurations — Detallı Rehbər

> **Tətbiq olunacaq server:** Windows Server + IIS  
> **Tarix:** 01 Aprel 2026  
> **Qeyd:** Bütün registry dəyişikliklərindən sonra server **restart** tələb olunur.

---

## Ümumi Məlumat

Bu sənəddə aşağıdakı 7 problemin həlli təsvir olunur. Bütün dəyişikliklər Windows Registry-də `SCHANNEL` bölməsində aparılır:

```
HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL
```

> **Vacib:** Registry dəyişikliklərindən əvvəl mütləq ehtiyat nüsxəsi (backup) çıxarın:
> ```powershell
> reg export "HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL" C:\schannel_backup.reg
> ```

---

## 1. TLS 1.0 Deaktiv Etmə

### Problem
TLS 1.0 köhnəlmiş protokoldur və BEAST, POODLE kimi hücumlara həssasdır. Müasir standartlara görə deaktiv edilməlidir.

### Həllin Ümumi Təsviri
> **Windows Registry-də TLS 1.0 protokolunu həm server, həm client tərəfdə söndürmək — beləliklə serverin köhnə və təhlükəli protokol üzərindən bağlantı qəbul etməsinin qarşısını almaq.**

### Həll (PowerShell — Administrator rejimində işə salın)

```powershell
# TLS 1.0 Server tərəfi deaktiv
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Server" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Server" -Name "Enabled" -Value 0 -PropertyType DWORD -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Server" -Name "DisabledByDefault" -Value 1 -PropertyType DWORD -Force

# TLS 1.0 Client tərəfi deaktiv
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Client" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Client" -Name "Enabled" -Value 0 -PropertyType DWORD -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Client" -Name "DisabledByDefault" -Value 1 -PropertyType DWORD -Force
```

### Yoxlama
```powershell
Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Server"
# Enabled = 0, DisabledByDefault = 1 olmalıdır
```

### Registry-də nə baş verir

| Açar | Dəyər | Məna |
|---|---|---|
| `Protocols\TLS 1.0\Server\Enabled` | 0 (DWORD) | TLS 1.0 server tərəfdə söndürülür |
| `Protocols\TLS 1.0\Server\DisabledByDefault` | 1 (DWORD) | Default olaraq deaktivdir |
| `Protocols\TLS 1.0\Client\Enabled` | 0 (DWORD) | TLS 1.0 client tərəfdə söndürülür |
| `Protocols\TLS 1.0\Client\DisabledByDefault` | 1 (DWORD) | Default olaraq deaktivdir |

### Həll (Visual Interface — Registry Editor)

1. **Win + R** basın, `regedit` yazın, **OK** basın (Administrator olaraq)
2. Sol paneldə bu yola gedin:
   ```
   HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols
   ```
3. **Protocols** üzərinə sağ klik → **New → Key** → ad: `TLS 1.0`
4. **TLS 1.0** üzərinə sağ klik → **New → Key** → ad: `Server`
5. **Server** qovluğunda sağ paneldə sağ klik → **New → DWORD (32-bit) Value**:
   - Ad: `Enabled` → Dəyər: `0`
   - Ad: `DisabledByDefault` → Dəyər: `1`
6. **TLS 1.0** üzərinə yenidən sağ klik → **New → Key** → ad: `Client`
7. **Client** qovluğunda eyni DWORD-ləri yaradın:
   - `Enabled` = `0`
   - `DisabledByDefault` = `1`

### Həll (IIS Crypto GUI Aləti)

1. **IIS Crypto** yükləyin: https://www.nartac.com/Products/IISCrypto
2. Proqramı **Administrator** olaraq açın
3. Sol paneldə **"Protocols"** sekmesine keçin
4. **TLS 1.0** yanındakı işarəni (**checkbox**) götürün (uncheck edin)
5. **"Apply"** basın → Server restart edin

### Təsir
TLS 1.0 istifadə edən çox köhnə brauzerlər (IE6, IE7) qoşula bilməyəcək. Müasir brauzerlər TLS 1.2/1.3 istifadə edir, problem yaranmayacaq.

### Yoxlama Scripti (PowerShell)
```powershell
Write-Host "`n===== TLS 1.0 YOXLAMA =====" -ForegroundColor Cyan
$serverPath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Server"
$clientPath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Client"
$pass = $true

foreach ($side in @(@{Name="Server"; Path=$serverPath}, @{Name="Client"; Path=$clientPath})) {
    if (Test-Path $side.Path) {
        $props = Get-ItemProperty -Path $side.Path -ErrorAction SilentlyContinue
        if ($props.Enabled -eq 0 -and $props.DisabledByDefault -eq 1) {
            Write-Host "  [PASS] TLS 1.0 $($side.Name): Enabled=0, DisabledByDefault=1" -ForegroundColor Green
        } else {
            Write-Host "  [FAIL] TLS 1.0 $($side.Name): Enabled=$($props.Enabled), DisabledByDefault=$($props.DisabledByDefault)" -ForegroundColor Red
            $pass = $false
        }
    } else {
        Write-Host "  [FAIL] TLS 1.0 $($side.Name): Registry key tapilmadi!" -ForegroundColor Red
        $pass = $false
    }
}

if ($pass) { Write-Host "  Netice: TLS 1.0 ugurla deaktiv edilib." -ForegroundColor Green }
else { Write-Host "  Netice: TLS 1.0 hele aktiv ola biler! Yeniden yoxlayin." -ForegroundColor Red }
```

---

## 2. DES və 3DES (Triple DES) Cipher Suites Deaktiv Etmə

### Problem
DES (56-bit) və 3DES (168-bit) zəif şifrələmə alqoritmləridir. Sweet32 hücumuna həssasdır.

### Həllin Ümumi Təsviri
> **Registry-də DES 56/56 və Triple DES 168 cipher-lərini deaktiv etmək — serverin zəif şifrələmə alqoritmləri ilə məlumat ötürməsini qadağan etmək.**

### Həll (PowerShell)

```powershell
# DES 56/56 deaktiv
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\DES 56/56" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\DES 56/56" -Name "Enabled" -Value 0 -PropertyType DWORD -Force

# Triple DES 168 deaktiv
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\Triple DES 168" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\Triple DES 168" -Name "Enabled" -Value 0 -PropertyType DWORD -Force
```

### Yoxlama
```powershell
Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\DES 56/56"
Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\Triple DES 168"
# Hər ikisində Enabled = 0 olmalıdır
```

### Registry-də nə baş verir

| Açar | Dəyər | Məna |
|---|---|---|
| `Ciphers\DES 56/56\Enabled` | 0 (DWORD) | DES şifrələmə deaktiv |
| `Ciphers\Triple DES 168\Enabled` | 0 (DWORD) | 3DES şifrələmə deaktiv |

### Həll (Visual Interface — Registry Editor)

1. **Win + R** → `regedit` → **OK** (Administrator)
2. Bu yola gedin:
   ```
   HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers
   ```
3. **Ciphers** üzərinə sağ klik → **New → Key** → ad: `DES 56/56`
4. **DES 56/56** qovluğunda sağ paneldə sağ klik → **New → DWORD (32-bit) Value**:
   - Ad: `Enabled` → Dəyər: `0`
5. **Ciphers** üzərinə yenə sağ klik → **New → Key** → ad: `Triple DES 168`
6. **Triple DES 168** qovluğunda eyni addımı təkrarlayın:
   - Ad: `Enabled` → Dəyər: `0`

### Həll (IIS Crypto GUI Aləti)

1. **IIS Crypto** proqramını Administrator olaraq açın
2. Sol paneldə **"Ciphers"** sekmesine keçin
3. **DES 56/56** və **Triple DES 168** yanındakı checkbox-ları götürün (uncheck)
4. **"Apply"** basın → Server restart edin

### Yoxlama Scripti (PowerShell)
```powershell
Write-Host "`n===== DES / 3DES YOXLAMA =====" -ForegroundColor Cyan
$ciphers = @("DES 56/56", "Triple DES 168")
$pass = $true

foreach ($cipher in $ciphers) {
    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\$cipher"
    if (Test-Path $regPath) {
        $props = Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue
        if ($props.Enabled -eq 0) {
            Write-Host "  [PASS] $cipher: Enabled=0" -ForegroundColor Green
        } else {
            Write-Host "  [FAIL] $cipher: Enabled=$($props.Enabled)" -ForegroundColor Red
            $pass = $false
        }
    } else {
        Write-Host "  [FAIL] $cipher: Registry key tapilmadi!" -ForegroundColor Red
        $pass = $false
    }
}

if ($pass) { Write-Host "  Netice: DES/3DES ugurla deaktiv edilib." -ForegroundColor Green }
else { Write-Host "  Netice: DES/3DES hele aktiv ola biler! Yeniden yoxlayin." -ForegroundColor Red }
```

---

## 3. RC4 Cipher Suites Deaktiv Etmə

### Problem
RC4 alqoritmi sındırılmış sayılır (RFC 7465 ilə qadağan edilib). Müasir skan alətləri bunu kritik boşluq kimi qeyd edir.

### Həllin Ümumi Təsviri
> **RC4 alqoritminin bütün variantlarını (40, 56, 64, 128-bit) Registry-dən deaktiv etmək — RFC 7465 standartına uyğun olaraq sındırılmış stream cipher-in tamamilə aradan qaldırılması.**

### Həll (PowerShell)

```powershell
# RC4 40/128 deaktiv
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\RC4 40/128" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\RC4 40/128" -Name "Enabled" -Value 0 -PropertyType DWORD -Force

# RC4 56/128 deaktiv
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\RC4 56/128" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\RC4 56/128" -Name "Enabled" -Value 0 -PropertyType DWORD -Force

# RC4 64/128 deaktiv
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\RC4 64/128" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\RC4 64/128" -Name "Enabled" -Value 0 -PropertyType DWORD -Force

# RC4 128/128 deaktiv
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\RC4 128/128" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\RC4 128/128" -Name "Enabled" -Value 0 -PropertyType DWORD -Force
```

### Yoxlama
```powershell
Get-ChildItem -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers" | Where-Object { $_.PSChildName -like "RC4*" } | ForEach-Object { Get-ItemProperty $_.PSPath }
# Bütün RC4 variantlarında Enabled = 0 olmalıdır
```

### Registry-də nə baş verir

| Açar | Dəyər | Məna |
|---|---|---|
| `Ciphers\RC4 40/128\Enabled` | 0 | RC4 40-bit deaktiv |
| `Ciphers\RC4 56/128\Enabled` | 0 | RC4 56-bit deaktiv |
| `Ciphers\RC4 64/128\Enabled` | 0 | RC4 64-bit deaktiv |
| `Ciphers\RC4 128/128\Enabled` | 0 | RC4 128-bit deaktiv |

### Həll (Visual Interface — Registry Editor)

1. **Win + R** → `regedit` → **OK** (Administrator)
2. Bu yola gedin:
   ```
   HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers
   ```
3. Aşağıdakı hər biri üçün: **Ciphers** → sağ klik → **New → Key** → sonra içində DWORD yaradın:

   | Yaradılacaq Key adı | DWORD adı | Dəyər |
   |---|---|---|
   | `RC4 40/128` | `Enabled` | `0` |
   | `RC4 56/128` | `Enabled` | `0` |
   | `RC4 64/128` | `Enabled` | `0` |
   | `RC4 128/128` | `Enabled` | `0` |

### Həll (IIS Crypto GUI Aləti)

1. **IIS Crypto** proqramını Administrator olaraq açın
2. Sol paneldə **"Ciphers"** sekmesine keçin
3. Bütün **RC4** ilə başlayan cipher-lərin yanındakı checkbox-ları götürün (uncheck)
4. **"Apply"** basın → Server restart edin

### Yoxlama Scripti (PowerShell)
```powershell
Write-Host "`n===== RC4 YOXLAMA =====" -ForegroundColor Cyan
$rc4Variants = @("RC4 40/128", "RC4 56/128", "RC4 64/128", "RC4 128/128")
$pass = $true

foreach ($cipher in $rc4Variants) {
    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\$cipher"
    if (Test-Path $regPath) {
        $props = Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue
        if ($props.Enabled -eq 0) {
            Write-Host "  [PASS] $cipher: Enabled=0" -ForegroundColor Green
        } else {
            Write-Host "  [FAIL] $cipher: Enabled=$($props.Enabled)" -ForegroundColor Red
            $pass = $false
        }
    } else {
        Write-Host "  [FAIL] $cipher: Registry key tapilmadi!" -ForegroundColor Red
        $pass = $false
    }
}

if ($pass) { Write-Host "  Netice: Butun RC4 variantlari ugurla deaktiv edilib." -ForegroundColor Green }
else { Write-Host "  Netice: RC4 hele aktiv ola biler! Yeniden yoxlayin." -ForegroundColor Red }
```

---

## 4. NULL Cipher Suites Deaktiv Etmə

### Problem
NULL cipher heç bir şifrələmə tətbiq etmir — məlumat açıq şəkildə ötürülür. Bu, HTTPS-in məqsədini tamamilə sıradan çıxarır.

### Həllin Ümumi Təsviri
> **Registry-də NULL cipher-i deaktiv etmək — şifrələmə olmadan bağlantı qurulmasını tamamilə qadağan etmək, bütün trafikin mütləq şifrələnməsini təmin etmək.**

### Həll (PowerShell)

```powershell
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\NULL" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\NULL" -Name "Enabled" -Value 0 -PropertyType DWORD -Force
```

### Yoxlama
```powershell
Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\NULL"
# Enabled = 0 olmalıdır
```

### Registry-də nə baş verir

| Açar | Dəyər | Məna |
|---|---|---|
| `Ciphers\NULL\Enabled` | 0 (DWORD) | Şifrələməsiz bağlantı qadağandır |

### Həll (Visual Interface — Registry Editor)

1. **Win + R** → `regedit` → **OK** (Administrator)
2. Bu yola gedin:
   ```
   HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers
   ```
3. **Ciphers** üzərinə sağ klik → **New → Key** → ad: `NULL`
4. **NULL** qovluğunda sağ paneldə sağ klik → **New → DWORD (32-bit) Value**:
   - Ad: `Enabled` → Dəyər: `0`

### Həll (IIS Crypto GUI Aləti)

1. **IIS Crypto** proqramını Administrator olaraq açın
2. Sol paneldə **"Ciphers"** sekmesine keçin
3. **NULL** cipher-in yanındakı checkbox-u götürün (uncheck)
4. **"Apply"** basın → Server restart edin

### Yoxlama Scripti (PowerShell)
```powershell
Write-Host "`n===== NULL CIPHER YOXLAMA =====" -ForegroundColor Cyan
$regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\NULL"
$pass = $true

if (Test-Path $regPath) {
    $props = Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue
    if ($props.Enabled -eq 0) {
        Write-Host "  [PASS] NULL Cipher: Enabled=0" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] NULL Cipher: Enabled=$($props.Enabled)" -ForegroundColor Red
        $pass = $false
    }
} else {
    Write-Host "  [FAIL] NULL Cipher: Registry key tapilmadi!" -ForegroundColor Red
    $pass = $false
}

if ($pass) { Write-Host "  Netice: NULL cipher ugurla deaktiv edilib." -ForegroundColor Green }
else { Write-Host "  Netice: NULL cipher hele aktiv ola biler! Yeniden yoxlayin." -ForegroundColor Red }
```

---

## 5. MD5 Hashing Alqoritmi Deaktiv Etmə

### Problem
MD5 hash alqoritmi kriptoqrafik cəhətdən sındırılıb (collision attack mümkündür). SHA-256 və ya SHA-384 istifadə edilməlidir.

### Həllin Ümumi Təsviri
> **Registry-də MD5 hash alqoritmini deaktiv etmək — collision hücumlarına həssas olan köhnə hashing mexanizmini aradan qaldırıb, serveri yalnız SHA-256/SHA-384 kimi güclü alqoritmlərdən istifadə etməyə məcbur etmək.**

### Həll (PowerShell)

```powershell
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Hashes\MD5" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Hashes\MD5" -Name "Enabled" -Value 0 -PropertyType DWORD -Force
```

### Yoxlama
```powershell
Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Hashes\MD5"
# Enabled = 0 olmalıdır
```

### Registry-də nə baş verir

| Açar | Dəyər | Məna |
|---|---|---|
| `Hashes\MD5\Enabled` | 0 (DWORD) | MD5 hash deaktiv edilir |

### Həll (Visual Interface — Registry Editor)

1. **Win + R** → `regedit` → **OK** (Administrator)
2. Bu yola gedin:
   ```
   HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Hashes
   ```
3. **Hashes** üzərinə sağ klik → **New → Key** → ad: `MD5`
4. **MD5** qovluğunda sağ paneldə sağ klik → **New → DWORD (32-bit) Value**:
   - Ad: `Enabled` → Dəyər: `0`

### Həll (IIS Crypto GUI Aləti)

1. **IIS Crypto** proqramını Administrator olaraq açın
2. Sol paneldə **"Hashes"** sekmesine keçin
3. **MD5** yanındakı checkbox-u götürün (uncheck)
4. **"Apply"** basın → Server restart edin

### Yoxlama Scripti (PowerShell)
```powershell
Write-Host "`n===== MD5 HASH YOXLAMA =====" -ForegroundColor Cyan
$regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Hashes\MD5"
$pass = $true

if (Test-Path $regPath) {
    $props = Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue
    if ($props.Enabled -eq 0) {
        Write-Host "  [PASS] MD5 Hash: Enabled=0" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] MD5 Hash: Enabled=$($props.Enabled)" -ForegroundColor Red
        $pass = $false
    }
} else {
    Write-Host "  [FAIL] MD5 Hash: Registry key tapilmadi!" -ForegroundColor Red
    $pass = $false
}

if ($pass) { Write-Host "  Netice: MD5 hash ugurla deaktiv edilib." -ForegroundColor Green }
else { Write-Host "  Netice: MD5 hele aktiv ola biler! Yeniden yoxlayin." -ForegroundColor Red }
```

---

## 6. Perfect Forward Secrecy (PFS) Təmin Etmə

### Problem
PFS olmadan, server açarı sındırılarsa, əvvəlki bütün şifrəli trafik deşifrə edilə bilər. PFS hər sessiya üçün müvəqqəti açar istifadə edir.

### Həllin Ümumi Təsviri
> **Cipher suite sırasını dəyişdirərək yalnız ECDHE (Elliptic Curve Diffie-Hellman Ephemeral) əsaslı key exchange alqoritmlərini aktiv saxlamaq — hər sessiyaya unikal müvəqqəti açar təyin edərək, server açarı oğurlansa belə keçmiş trafikin qorunmasını təmin etmək.**

### Həll (PowerShell)

PFS təmin etmək üçün yalnız **ECDHE** (Elliptic Curve Diffie-Hellman Ephemeral) əsaslı key exchange alqoritmlərindən istifadə edilməlidir. Bunun üçün cipher suite sırasını dəyişmək lazımdır:

```powershell
# Yalnız PFS dəstəkləyən cipher suite-ləri aktiv et
$cipherSuites = @(
    "TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384",
    "TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256",
    "TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384",
    "TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256",
    "TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA384",
    "TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256",
    "TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384",
    "TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256"
)

$cipherSuitesString = $cipherSuites -join ","

# Group Policy vasitəsilə cipher suite sırasını təyin et
New-Item -Path "HKLM:\SOFTWARE\Policies\Microsoft\Cryptography\Configuration\SSL\00010002" -Force
New-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Cryptography\Configuration\SSL\00010002" -Name "Functions" -Value $cipherSuitesString -PropertyType String -Force
```

### Yoxlama
```powershell
Get-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Cryptography\Configuration\SSL\00010002" -Name "Functions"
# Yalnız ECDHE əsaslı cipher suite-lər siyahıda olmalıdır
```

### Niyə bu suite-lər?

| Cipher Suite | Key Exchange | Şifrələmə | Hash | PFS |
|---|---|---|---|---|
| TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384 | ECDHE (PFS) | AES-256 GCM | SHA-384 | ✅ |
| TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 | ECDHE (PFS) | AES-128 GCM | SHA-256 | ✅ |
| TLS_RSA_WITH_AES_256_GCM_SHA384 | RSA (PFS yox) | AES-256 GCM | SHA-384 | ❌ |
| TLS_RSA_WITH_AES_128_CBC_SHA | RSA (PFS yox) | AES-128 CBC | SHA-1 | ❌ |

> Yuxarıdakı siyahıda yalnız **ECDHE** olan suite-lər aktiv edilir.

### Həll (Visual Interface — Group Policy Editor)

1. **Win + R** → `gpedit.msc` → **OK**
2. Bu yola gedin:
   ```
   Computer Configuration → Administrative Templates → Network → SSL Configuration Settings
   ```
3. Sağ paneldə **"SSL Cipher Suite Order"** üzərinə iki dəfə klikləyin
4. **"Enabled"** seçin
5. **SSL Cipher Suites** sahəsinə yalnız ECDHE əsaslı suite-ləri yazın (vergüllə ayırın):
   ```
   TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384,TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA384,TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256,TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384,TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256
   ```
6. **OK** basın → Server restart edin

### Həll (IIS Crypto GUI Aləti)

1. **IIS Crypto** proqramını Administrator olaraq açın
2. Sol paneldə **"Cipher Suites"** sekmesine keçin
3. Siyahıda yalnız **ECDHE** ilə başlayan suite-ləri aktiv saxlayın, qalanlarını uncheck edin
4. Yuxarı/aşağı oxlarla prioritet sırasını düzəldin (AES_256_GCM birinci olsun)
5. **"Apply"** basın → Server restart edin

### Təsir
RSA key exchange əsaslı bağlantılar qəbul edilməyəcək. Müasir brauzerlər ECDHE dəstəkləyir, problem yaranmayacaq.

### Yoxlama Scripti (PowerShell)
```powershell
Write-Host "`n===== PFS (Perfect Forward Secrecy) YOXLAMA =====" -ForegroundColor Cyan
$regPath = "HKLM:\SOFTWARE\Policies\Microsoft\Cryptography\Configuration\SSL\00010002"
$pass = $true

if (Test-Path $regPath) {
    $props = Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue
    $suites = $props.Functions
    if ($suites) {
        $suiteList = $suites -split ","
        $nonPFS = $suiteList | Where-Object { $_ -notmatch "ECDHE" }
        if ($nonPFS.Count -eq 0) {
            Write-Host "  [PASS] Butun cipher suite-ler ECDHE (PFS) esaslidir." -ForegroundColor Green
            Write-Host "  Aktiv suite-ler ($($suiteList.Count) eded):" -ForegroundColor Gray
            $suiteList | ForEach-Object { Write-Host "    - $_" -ForegroundColor Gray }
        } else {
            Write-Host "  [FAIL] PFS olmayan suite-ler tapildi:" -ForegroundColor Red
            $nonPFS | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
            $pass = $false
        }
    } else {
        Write-Host "  [FAIL] Functions deyeri bos ve ya tapilmadi!" -ForegroundColor Red
        $pass = $false
    }
} else {
    Write-Host "  [FAIL] Cipher suite policy registry key tapilmadi!" -ForegroundColor Red
    $pass = $false
}

if ($pass) { Write-Host "  Netice: PFS ugurla temin edilib." -ForegroundColor Green }
else { Write-Host "  Netice: PFS temin olunmayib! Cipher suite siyahisini yeniden yoxlayin." -ForegroundColor Red }
```

---

## 7. IIS Default Cipher Suites Dəyişdirilməsi

### Problem
IIS default cipher suite sırasına zəif alqoritmlər (RC4, DES, 3DES, NULL, MD5) daxildir. Bu, yuxarıdakı bütün problemlərin kök səbəbidir.

### Həllin Ümumi Təsviri
> **IIS-in standart olaraq gələn zəif konfiqurasiyasını tamamilə dəyişdirmək — köhnə protokolları (SSL 2.0, SSL 3.0, TLS 1.0, TLS 1.1) söndürmək, yalnız TLS 1.2 aktiv saxlamaq və bütün zəif cipher/hash alqoritmlərini aradan qaldırmaq. Bu addım əslində 1-6 addımların yekun tamamlayıcısıdır.**

### Həll — Variant A: PowerShell (Əl ilə)

Bu addım əslində **addım 1-6-nı birləşdirir.** Yuxarıdakı bütün dəyişiklikləri etdikdən sonra bu addım avtomatik həll olunur.

Əlavə olaraq, TLS 1.1-i də deaktiv edin (tövsiyə olunur):

```powershell
# TLS 1.1 Server deaktiv
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Server" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Server" -Name "Enabled" -Value 0 -PropertyType DWORD -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Server" -Name "DisabledByDefault" -Value 1 -PropertyType DWORD -Force

# TLS 1.1 Client deaktiv
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Client" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Client" -Name "Enabled" -Value 0 -PropertyType DWORD -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Client" -Name "DisabledByDefault" -Value 1 -PropertyType DWORD -Force

# TLS 1.2 aktiv olduğunu təmin et
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Server" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Server" -Name "Enabled" -Value 1 -PropertyType DWORD -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Server" -Name "DisabledByDefault" -Value 0 -PropertyType DWORD -Force

# SSL 2.0 deaktiv
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 2.0\Server" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 2.0\Server" -Name "Enabled" -Value 0 -PropertyType DWORD -Force

# SSL 3.0 deaktiv
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 3.0\Server" -Force
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 3.0\Server" -Name "Enabled" -Value 0 -PropertyType DWORD -Force
```

### Həll — Variant B: IIS Crypto Aləti (Ən Asan Yol)

1. **IIS Crypto** yükləyin: https://www.nartac.com/Products/IISCrypto
2. Proqramı Administrator olaraq açın
3. **"Best Practices"** düyməsinə basın — avtomatik olaraq:
   - Zəif protokolları deaktiv edir (SSL 2.0, SSL 3.0, TLS 1.0, TLS 1.1)
   - Zəif cipher-ləri deaktiv edir (DES, 3DES, RC4, NULL)
   - Zəif hash-ləri deaktiv edir (MD5)
   - PFS dəstəkləyən cipher suite-ləri prioritetə alır
4. **"Apply"** basın
5. Server **restart** edin

> **Qeyd:** IIS Crypto GUI aləti yuxarıdakı bütün PowerShell əmrlərini bir kliklə edir.

### Yoxlama Scripti (PowerShell)
```powershell
Write-Host "`n===== IIS DEFAULT KONFIQURASIYA — TAM YOXLAMA =====" -ForegroundColor Cyan
$allPass = $true

# --- Protokollar ---
$protocols = @(
    @{ Name="SSL 2.0"; Expected=0 },
    @{ Name="SSL 3.0"; Expected=0 },
    @{ Name="TLS 1.0"; Expected=0 },
    @{ Name="TLS 1.1"; Expected=0 },
    @{ Name="TLS 1.2"; Expected=1 }
)

Write-Host "`n  -- Protokollar --" -ForegroundColor Yellow
foreach ($proto in $protocols) {
    $serverPath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\$($proto.Name)\Server"
    if (Test-Path $serverPath) {
        $props = Get-ItemProperty -Path $serverPath -ErrorAction SilentlyContinue
        if ($props.Enabled -eq $proto.Expected) {
            Write-Host "  [PASS] $($proto.Name): Enabled=$($props.Enabled)" -ForegroundColor Green
        } else {
            Write-Host "  [FAIL] $($proto.Name): Enabled=$($props.Enabled) (gozlenilen: $($proto.Expected))" -ForegroundColor Red
            $allPass = $false
        }
    } else {
        if ($proto.Expected -eq 0) {
            Write-Host "  [INFO] $($proto.Name): Key yoxdur (default davranis)" -ForegroundColor Yellow
        } else {
            Write-Host "  [FAIL] $($proto.Name): Key tapilmadi!" -ForegroundColor Red
            $allPass = $false
        }
    }
}

# --- Cipher-ler ---
$ciphers = @("DES 56/56", "Triple DES 168", "RC4 40/128", "RC4 56/128", "RC4 64/128", "RC4 128/128", "NULL")

Write-Host "`n  -- Cipher-ler --" -ForegroundColor Yellow
foreach ($cipher in $ciphers) {
    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Ciphers\$cipher"
    if (Test-Path $regPath) {
        $props = Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue
        if ($props.Enabled -eq 0) {
            Write-Host "  [PASS] $cipher: Enabled=0" -ForegroundColor Green
        } else {
            Write-Host "  [FAIL] $cipher: Enabled=$($props.Enabled)" -ForegroundColor Red
            $allPass = $false
        }
    } else {
        Write-Host "  [FAIL] $cipher: Registry key tapilmadi!" -ForegroundColor Red
        $allPass = $false
    }
}

# --- Hash ---
Write-Host "`n  -- Hash Alqoritmleri --" -ForegroundColor Yellow
$md5Path = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Hashes\MD5"
if (Test-Path $md5Path) {
    $props = Get-ItemProperty -Path $md5Path -ErrorAction SilentlyContinue
    if ($props.Enabled -eq 0) {
        Write-Host "  [PASS] MD5: Enabled=0" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] MD5: Enabled=$($props.Enabled)" -ForegroundColor Red
        $allPass = $false
    }
} else {
    Write-Host "  [FAIL] MD5: Registry key tapilmadi!" -ForegroundColor Red
    $allPass = $false
}

# --- PFS ---
Write-Host "`n  -- PFS Cipher Suites --" -ForegroundColor Yellow
$suitePath = "HKLM:\SOFTWARE\Policies\Microsoft\Cryptography\Configuration\SSL\00010002"
if (Test-Path $suitePath) {
    $funcs = (Get-ItemProperty -Path $suitePath).Functions
    $nonPFS = ($funcs -split ",") | Where-Object { $_ -notmatch "ECDHE" }
    if ($nonPFS.Count -eq 0) {
        Write-Host "  [PASS] Butun cipher suite-ler PFS (ECDHE) esaslidir." -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] PFS olmayan suite-ler var: $($nonPFS -join ', ')" -ForegroundColor Red
        $allPass = $false
    }
} else {
    Write-Host "  [FAIL] Cipher suite policy tapilmadi!" -ForegroundColor Red
    $allPass = $false
}

# --- Yekun ---
Write-Host "`n========================================" -ForegroundColor Cyan
if ($allPass) {
    Write-Host "  YEKUN: Butun konfiqurasiyalar DOGRU tetbiq edilib!" -ForegroundColor Green
} else {
    Write-Host "  YEKUN: Bezi konfiqurasiyalarda problem var. Yuxaridaki [FAIL] satirlarini yoxlayin." -ForegroundColor Red
}
Write-Host "========================================`n" -ForegroundColor Cyan
```

---

## Tətbiq Sırası (Tövsiyə Olunan)

```
Addım 1: Registry backup çıxarın
Addım 2: TLS 1.0 deaktiv edin (#1)
Addım 3: DES/3DES deaktiv edin (#2)
Addım 4: RC4 deaktiv edin (#3)
Addım 5: NULL cipher deaktiv edin (#4)
Addım 6: MD5 deaktiv edin (#5)
Addım 7: PFS cipher suite sırasını tətbiq edin (#6)
Addım 8: TLS 1.1, SSL 2.0, SSL 3.0 deaktiv edin (#7)
Addım 9: Server RESTART edin
Addım 10: Yoxlama aparın
```

---

## Bütün Dəyişikliklərdən Sonra Yoxlama

### PowerShell ilə aktiv protokolları yoxlayın:
```powershell
# Bütün SCHANNEL konfiqurasiyasını göstər
Get-ChildItem -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL" -Recurse | ForEach-Object {
    $path = $_.PSPath
    $properties = Get-ItemProperty -Path $path -ErrorAction SilentlyContinue
    if ($properties) {
        Write-Host "`n$($_.PSChildName):" -ForegroundColor Yellow
        $properties.PSObject.Properties | Where-Object { $_.Name -notlike "PS*" } | ForEach-Object {
            Write-Host "  $($_.Name) = $($_.Value)"
        }
    }
}
```

### Onlayn test (SSL sertifikatı əlavə etdikdən sonra):
- https://www.ssllabs.com/ssltest/
- A+ reytinq almaq hədəflənməlidir

---

## Geri Qaytarma (Rollback)

Əgər problem yaranarsa, əvvəlcədən çıxardığınız backup-ı bərpa edin:

```powershell
reg import C:\schannel_backup.reg
# Sonra server restart edin
Restart-Computer -Force
```

---

## Xülasə Cədvəli

| # | Problem | Həll Metodu | Restart Tələb Edir |
|---|---|---|---|
| 1 | TLS 1.0 aktiv | Registry: Protocols | ✅ |
| 2 | DES/3DES aktiv | Registry: Ciphers | ✅ |
| 3 | RC4 aktiv | Registry: Ciphers | ✅ |
| 4 | NULL cipher aktiv | Registry: Ciphers | ✅ |
| 5 | MD5 hash aktiv | Registry: Hashes | ✅ |
| 6 | PFS yoxdur | Registry/GPO: Cipher Suite sırası | ✅ |
| 7 | Default cipher suites | Yuxarıdakıların hamısı + IIS Crypto | ✅ |

> **Bütün dəyişiklikləri bir dəfəyə tətbiq edin, sonra bir dəfə restart edin.**
