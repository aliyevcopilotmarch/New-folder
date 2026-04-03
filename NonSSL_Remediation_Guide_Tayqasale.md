# TayqaSale IIS Web Server — Non-SSL Misconfiguration Remediation Guide
**Tarix:** 31 Mart 2026  
**Mənbə fayl:** NonSSL_Misconfigurations_Tayqasale_Deduplicated.csv  
**Ümumi problem sayı:** 12

---

## İçindəkilər

1. [Application Pool Identity konfiqurasiyası](#1-ensure-application-pool-identity-is-configured-for-all-application-pools)
2. [IIS Log yeri dəyişdirilməsi](#2-ensure-default-iis-web-log-location-is-moved)
3. [Deployment Method Retail](#3-ensure-deployment-method-retail-is-set)
4. [Dynamic IP Address Restrictions](#4-ensure-dynamic-ip-address-restrictions-is-enabled)
5. [Global Authorization Rule](#5-ensure-global-authorization-rule-is-set-to-restrict-access)
6. [Host Headers](#6-ensure-host-headers-are-on-all-sites)
7. [HTTP Trace Method](#7-ensure-http-trace-method-is-disabled)
8. [HttpCookie Mode for Session State](#8-ensure-httpcookie-mode-is-configured-for-session-state)
9. [Non-ASCII Characters in URLs](#9-ensure-non-ascii-characters-in-urls-are-not-allowed)
10. [Unique Application Pools](#10-ensure-unique-application-pools-is-set-for-sites)
11. [Unlisted File Extensions](#11-ensure-unlisted-file-extensions-are-not-allowed)
12. [Web Content on Non-System Partition](#12-ensure-web-content-is-on-non-system-partition)

---

## 1. Ensure Application Pool Identity is Configured for All Application Pools

**Kateqoriya:** Permission Management  
**Fayl Yolu:** IIS Server  
**Risk Səviyyəsi:** 🔴 Yüksək  
**CIS Benchmark:** CIS IIS 10 Benchmark — Section 3.5

### Problem Təsviri

IIS-də application pool-lar defolt olaraq `NetworkService` və ya `LocalSystem` kimi yüksək səlahiyyətli hesablarla işləyə bilər. Bu vəziyyətdə bir proqramda aşkar edilən boşluq digər proqramlara və hətta bütün əməliyyat sisteminə təsir edə bilər, çünki eyni identifikasiya paylaşılır. Əgər hücumçu bir application pool vasitəsilə daxil olarsa, eyni identity altında işləyən bütün saytlara və servislərə çatış əldə edir.

### Həll Yolu

Hər bir application pool üçün `ApplicationPoolIdentity` istifadə edin. Bu, IIS 7.5+ versiyalarında daxili "virtual account" mexanizmindən istifadə edir — hər pool üçün avtomatik unikal hesab yaradılır.

#### Metod 1: IIS Manager vasitəsilə

1. **IIS Manager** açın (`inetmgr.exe`)
2. Sol paneldə **Application Pools** seçin
3. Hər bir application pool üzərinə sağ klik → **Advanced Settings**
4. **Process Model** bölməsində **Identity** sahəsini tapın
5. `...` düyməsinə basın
6. **Built-in account** seçin → **ApplicationPoolIdentity** seçin
7. **OK** basın

#### Metod 2: AppCmd.exe vasitəsilə (hər pool üçün)

```cmd
%windir%\system32\inetsrv\appcmd.exe set AppPool "TayqaSaleServicePortal" -processModel.identityType:ApplicationPoolIdentity
%windir%\system32\inetsrv\appcmd.exe set AppPool "TayqaSaleLicenseService" -processModel.identityType:ApplicationPoolIdentity
%windir%\system32\inetsrv\appcmd.exe set AppPool "TayqaSaleCampaignPortal" -processModel.identityType:ApplicationPoolIdentity
```

#### Metod 3: PowerShell vasitəsilə (bütün pool-lara tətbiq)

```powershell
Import-Module WebAdministration
Get-ChildItem IIS:\AppPools | ForEach-Object {
    Set-ItemProperty "IIS:\AppPools\$($_.Name)" -Name processModel.identityType -Value 4
    Write-Host "ApplicationPoolIdentity tətbiq edildi: $($_.Name)" -ForegroundColor Green
}
```

> **Qeyd:** `identityType` dəyərləri: `0`=LocalSystem, `1`=LocalService, `2`=NetworkService, `3`=SpecificUser, **`4`=ApplicationPoolIdentity** (Tövsiyə olunan)

#### Fayl sistemində icazə vermək

ApplicationPoolIdentity istifadə edəndə fayl icazələrini belə verin:

```cmd
ICACLS "C:\TayqaSale\WebServices" /grant "IIS AppPool\TayqaSaleServicePortal:(OI)(CI)RX"
```

### Yoxlama

```powershell
Get-ChildItem IIS:\AppPools | Select-Object Name, @{N='Identity';E={$_.processModel.identityType}} | Format-Table -AutoSize
```

Bütün pool-lar `ApplicationPoolIdentity` (4) göstərməlidir.

---

## 2. Ensure Default IIS Web Log Location is Moved

**Kateqoriya:** Logging  
**Fayl Yolu:** IIS Server  
**Risk Səviyyəsi:** 🟡 Orta  
**CIS Benchmark:** CIS IIS 10 Benchmark — Section 5.1

### Problem Təsviri

IIS defolt olaraq log fayllarını `%SystemDrive%\inetpub\logs\LogFiles` yolunda saxlayır. Bu, sistem diskindədir (adətən C:\). Problemlər:

- **Disk dolması riski:** Log faylları böyüdükcə sistem diskini doldura bilər və bu serverin çökməsinə səbəb ola bilər
- **Hücum səthi:** Hücumçu sistem diskindəki faylları dəyişdirə bilərsə, logları silərək izlərini gizlədə bilər
- **Performans:** Sistem diskinin I/O yükü artır
- **Audit trail itkisi:** Log faylları itirilsə, qanunvericilik tələbləri pozulur

### Həll Yolu

Log fayllarını ayrı diskə (məsələn, `D:\IISLogs`) köçürün.

#### Metod 1: IIS Manager vasitəsilə

1. **IIS Manager** açın
2. Sol paneldə **server adı** seçin
3. **Logging** ikona üzərinə iki dəfə klik edin
4. **Directory** sahəsini dəyişdirin: `D:\IISLogs`
5. Sağ paneldə **Apply** basın

#### Metod 2: AppCmd.exe vasitəsilə

```cmd
:: Əvvəlcə qovluğu yaradın
mkdir D:\IISLogs

:: Server səviyyəsində log yerini dəyişdirin
%windir%\system32\inetsrv\appcmd.exe set config -section:system.applicationHost/sites -siteDefaults.logFile.directory:"D:\IISLogs" /commit:apphost

:: Hər bir sayt üçün ayrıca tətbiq edin (əgər override etmisinizsə)
%windir%\system32\inetsrv\appcmd.exe set config -section:system.applicationHost/log -centralBinaryLogFile.directory:"D:\IISLogs" /commit:apphost
%windir%\system32\inetsrv\appcmd.exe set config -section:system.applicationHost/log -centralW3CLogFile.directory:"D:\IISLogs" /commit:apphost
```

#### Metod 3: PowerShell vasitəsilə

```powershell
Import-Module WebAdministration

# Qovluq yaradın
New-Item -Path "D:\IISLogs" -ItemType Directory -Force

# Server default log yerini dəyişdirin
Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
    -filter "system.applicationHost/sites/siteDefaults/logFile" `
    -name "directory" -value "D:\IISLogs"

# IIS AppPool hesablarına yazma icazəsi verin
$acl = Get-Acl "D:\IISLogs"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS","Modify","ContainerInherit,ObjectInherit","None","Allow")
$acl.SetAccessRule($rule)
Set-Acl "D:\IISLogs" $acl
```

#### ApplicationHost.config-da nəticə

```xml
<system.applicationHost>
    <sites>
        <siteDefaults>
            <logFile directory="D:\IISLogs" />
        </siteDefaults>
    </sites>
</system.applicationHost>
```

### Yoxlama

```powershell
(Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.applicationHost/sites/siteDefaults/logFile" -name "directory").Value
```

Nəticə `D:\IISLogs` (və ya sistem diskindən fərqli disk) olmalıdır.

---

## 3. Ensure Deployment Method Retail is Set

**Kateqoriya:** Information Disclosure  
**Fayl Yolu:** IIS Server  
**Risk Səviyyəsi:** 🔴 Yüksək  
**CIS Benchmark:** CIS IIS 10 Benchmark — Section 1.6

### Problem Təsviri

`<deployment retail="true" />` parametri **machine.config** faylında qoyulmalıdır. Bu parametr `false` olduqda (və ya olmadıqda):

- **Debug rejimi aktiv qalır** — detal xəta mesajları istifadəçilərə göstərilir (stack trace, fayl yolları, verilənlər bazası bağlantı sətrləri)
- **Trace output aktiv qalır** — `Trace.axd` vasitəsilə daxili məlumatlar əlçatan olur
- **Custom errors söndürülür** — hücumçular daxili server xətalarını görə bilir
- **Performans azalır** — debug simvolları yüklənir, optimallaşdırmalar söndürülür

Bu, **Information Disclosure** hücumlarına açıq qapı yaradır — hücumçular server strukturu, verilənlər bazası, kod haqqında məlumat əldə edir.

### Həll Yolu

#### Addım 1: machine.config faylını tapın

```
# .NET Framework 4.x (64-bit):
%windir%\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config

# .NET Framework 4.x (32-bit):
%windir%\Microsoft.NET\Framework\v4.0.30319\Config\machine.config
```

#### Addım 2: machine.config faylını redaktə edin

machine.config faylında `<system.web>` bölməsinə aşağıdakını əlavə edin:

```xml
<configuration>
    <system.web>
        <deployment retail="true" />
    </system.web>
</configuration>
```

#### Addım 3: PowerShell ilə avtomatik tətbiq

```powershell
# 64-bit .NET Framework üçün
$machineConfigPath = "$env:windir\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config"

# Backup yaradın!
Copy-Item $machineConfigPath "$machineConfigPath.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"

[xml]$xml = Get-Content $machineConfigPath
$systemWeb = $xml.configuration.'system.web'

if (-not $systemWeb) {
    $systemWeb = $xml.CreateElement('system.web')
    $xml.configuration.AppendChild($systemWeb) | Out-Null
}

$deployment = $systemWeb.SelectSingleNode('deployment')
if (-not $deployment) {
    $deployment = $xml.CreateElement('deployment')
    $systemWeb.AppendChild($deployment) | Out-Null
}

$deployment.SetAttribute('retail', 'true')
$xml.Save($machineConfigPath)
Write-Host "deployment retail='true' tətbiq edildi." -ForegroundColor Green
```

### `retail="true"` nə edir?

| Parametr | Effekt |
|---|---|
| `<compilation debug="true">` | **Ləğv edilir** — debug söndürülür |
| `<trace enabled="true">` | **Ləğv edilir** — trace söndürülür |
| `<customErrors mode="Off">` | **Ləğv edilir** — custom errors `RemoteOnly` olur |

### Yoxlama

```powershell
$machineConfig = [xml](Get-Content "$env:windir\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config")
$retail = $machineConfig.configuration.'system.web'.deployment.retail
if ($retail -eq 'true') {
    Write-Host "COMPLIANCE: deployment retail=true" -ForegroundColor Green
} else {
    Write-Host "NON-COMPLIANT: deployment retail=$retail" -ForegroundColor Red
}
```

---

## 4. Ensure Dynamic IP Address Restrictions is Enabled

**Kateqoriya:** Denial of Service Attacks  
**Fayl Yolu:** C:\TayqaSale\WebServices\ServicePortal\WebUI  
**Risk Səviyyəsi:** 🔴 Yüksək  
**CIS Benchmark:** CIS IIS 10 Benchmark — Section 6.2

### Problem Təsviri

Dynamic IP Address Restrictions olmadıqda IIS serveri aşağıdakı hücumlara qarşı müdafiəsizdir:

- **DDoS hücumları** — bir IP-dən minlərlə eyni vaxtlı sorğu vasitəsilə server resursları tükəndilir
- **Brute-force hücumları** — giriş səhifələrinə saniyədə yüzlərlə sorğu göndərilir
- **Application-layer flooding** — tətbiqi yavaşlatmaq üçün ağır sorğular göndərilir

Bu xüsusiyyət IIS 8.0+ versiyalarında mövcuddur və eyni IP-dən gələn həddindən artıq sorğuları avtomatik bloklayır.

### Həll Yolu

#### Ön şərt: IP Security modulunun quraşdırılması

```powershell
# Windows Server üçün
Install-WindowsFeature Web-IP-Security

# Yoxlama
Get-WindowsFeature Web-IP-Security
```

#### Metod 1: IIS Manager vasitəsilə

1. **IIS Manager** açın
2. TayqaSale saytını seçin
3. **IP Address and Domain Restrictions** üzərinə iki dəfə klik edin
4. Sağ paneldə **Edit Dynamic Restriction Settings...** basın
5. Aşağıdakıları aktiv edin:
   - ✅ **Deny IP Address based on the number of concurrent requests** → `maxConcurrentRequests: 10`
   - ✅ **Deny IP Address based on the number of requests over a period of time** → `maxRequests: 30`, `requestIntervalInMilliseconds: 300`
6. **Deny Action Type:** `Forbidden` (403) seçin
7. **OK** basın

#### Metod 2: AppCmd.exe vasitəsilə

```cmd
:: Bütün TayqaSale saytları üçün tətbiq edin
appcmd.exe set config "TayqaSale" -section:system.webServer/security/dynamicIpSecurity /denyAction:"Forbidden" /enableProxyMode:"True" /enableLoggingOnlyMode:"False" /commit:apphost

appcmd.exe set config "TayqaSale" -section:system.webServer/security/dynamicIpSecurity /denyByConcurrentRequests.enabled:"True" /denyByConcurrentRequests.maxConcurrentRequests:"10" /commit:apphost

appcmd.exe set config "TayqaSale" -section:system.webServer/security/dynamicIpSecurity /denyByRequestRate.enabled:"True" /denyByRequestRate.maxRequests:"30" /denyByRequestRate.requestIntervalInMilliseconds:"300" /commit:apphost
```

#### Metod 3: PowerShell vasitəsilə

```powershell
Import-Module WebAdministration

# Bütün TayqaSale saytları üçün
$sites = @("TayqaSaleServicePortal", "TayqaSaleDynamicToolManagement", "TayqaSaleCampaignManagement", "TayqaSaleLicenseService")

foreach ($site in $sites) {
    Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location $site `
        -filter "system.webServer/security/dynamicIpSecurity" `
        -name "denyAction" -value "Forbidden"

    Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location $site `
        -filter "system.webServer/security/dynamicIpSecurity" `
        -name "enableProxyMode" -value "True"

    Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location $site `
        -filter "system.webServer/security/dynamicIpSecurity/denyByConcurrentRequests" `
        -name "enabled" -value "True"

    Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location $site `
        -filter "system.webServer/security/dynamicIpSecurity/denyByConcurrentRequests" `
        -name "maxConcurrentRequests" -value 10

    Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location $site `
        -filter "system.webServer/security/dynamicIpSecurity/denyByRequestRate" `
        -name "enabled" -value "True"

    Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location $site `
        -filter "system.webServer/security/dynamicIpSecurity/denyByRequestRate" `
        -name "maxRequests" -value 30

    Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location $site `
        -filter "system.webServer/security/dynamicIpSecurity/denyByRequestRate" `
        -name "requestIntervalInMilliseconds" -value 300
}
```

#### web.config nəticəsi

```xml
<system.webServer>
    <security>
        <dynamicIpSecurity denyAction="Forbidden" enableProxyMode="true">
            <denyByConcurrentRequests enabled="true" maxConcurrentRequests="10" />
            <denyByRequestRate enabled="true" maxRequests="30" requestIntervalInMilliseconds="300" />
        </dynamicIpSecurity>
    </security>
</system.webServer>
```

> **Vacib:** `enableProxyMode="true"` qoyun əgər serverin qarşısında load balancer və ya reverse proxy varsa. Bu, `X-Forwarded-For` header-ını oxuyaraq əsl client IP-ni müəyyən edir.

### Tövsiyə olunan parametrlər

| Parametr | Dəyər | Açıqlama |
|---|---|---|
| `maxConcurrentRequests` | 10 | Eyni IP-dən eyni anda maksimum 10 sorğu |
| `maxRequests` | 30 | Müəyyən müddət ərzində maksimum 30 sorğu |
| `requestIntervalInMilliseconds` | 300 | 300ms ərzində 30 sorğu = saniyədə 100 sorğu limiti |
| `denyAction` | Forbidden | Bloklanan sorğulara 403 cavabı |

### Yoxlama

```powershell
Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location 'TayqaSaleServicePortal' `
    -filter "system.webServer/security/dynamicIpSecurity/denyByConcurrentRequests" -name "enabled"
```

---

## 5. Ensure Global Authorization Rule is Set to Restrict Access

**Kateqoriya:** Permission Management  
**Fayl Yolu:** C:\TayqaSale\WebServices\ServicePortal\Core  
**Risk Səviyyəsi:** 🔴 Yüksək  
**CIS Benchmark:** CIS IIS 10 Benchmark — Section 4.5

### Problem Təsviri

IIS defolt olaraq bütün istifadəçilərə çatışa icazə verir. Authorization rule olmadıqda:

- **Autentifikasiya olmadan** resurslar əlçatan olur
- **Anonim istifadəçilər** həssas web servislərə (Core servislər) daxil ola bilir
- **Ən az imtiyaz prinsipi** pozulur — hər kəs eyni icazəyə malikdir
- **Daxili API-lar** kənar istifadəçilər tərəfindən çağırıla bilər

### Həll Yolu

#### Metod 1: IIS Manager vasitəsilə

1. **IIS Manager** açın
2. Sol paneldə **TayqaSale** saytını genişləndirin → **ServicePortal/Core** seçin
3. **Authorization Rules** üzərinə iki dəfə klik edin
4. Mövcud **Allow All Users** qaydasını silin
5. **Add Allow Rule** basın:
   - **Specified roles or user groups:** `TayqaSaleAdmins` yazın
   - **OK** basın

#### Metod 2: web.config vasitəsilə (TayqaSale hər Core service üçün)

```xml
<configuration>
    <system.webServer>
        <security>
            <authorization>
                <remove users="*" roles="" verbs="" />
                <add accessType="Allow" roles="TayqaSaleAdmins,TayqaSaleServiceAccounts" />
                <add accessType="Deny" users="*" />
            </authorization>
        </security>
    </system.webServer>
</configuration>
```

#### Metod 3: AppCmd.exe vasitəsilə

```cmd
:: Əvvəlcə bütün istifadəçilər üçün default Allow qaydasını silin
appcmd.exe set config "TayqaSale/ServicePortal/Core" -section:system.webServer/security/authorization /-"[users='*',roles='',verbs='']"

:: Yalnız müəyyən rola icazə verin
appcmd.exe set config "TayqaSale/ServicePortal/Core" -section:system.webServer/security/authorization /+"[accessType='Allow',roles='TayqaSaleAdmins']"

:: Qalan hər kəsi bloklayın
appcmd.exe set config "TayqaSale/ServicePortal/Core" -section:system.webServer/security/authorization /+"[accessType='Deny',users='*']"
```

#### Metod 4: PowerShell vasitəsilə

```powershell
# İlk öncə URL Authorization modulunun quraşdırılmasını yoxlayın
Install-WindowsFeature Web-Url-Auth

$corePaths = @(
    "TayqaSale/ServicePortal/Core",
    "TayqaSale/CampaignManagementPortal/Core",
    "TayqaSale/DynamicToolManagementPortal/Core"
)

foreach ($path in $corePaths) {
    # Defolt "allow all" qaydasını silin
    Remove-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location $path `
        -filter "system.webServer/security/authorization" -name "." `
        -AtElement @{users='*';roles='';verbs=''}

    # Yalnız icazəli rollara giriş
    Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location $path `
        -filter "system.webServer/security/authorization" -name "." `
        -value @{accessType='Allow';roles='TayqaSaleAdmins,TayqaSaleServiceAccounts'}

    # Digər hər kəsi bloklayın
    Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location $path `
        -filter "system.webServer/security/authorization" -name "." `
        -value @{accessType='Deny';users='*'}

    Write-Host "Authorization rule tətbiq edildi: $path" -ForegroundColor Green
}
```

### Yoxlama

```powershell
Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location 'TayqaSale/ServicePortal/Core' `
    -filter "system.webServer/security/authorization" -name "." | 
    Select-Object accessType, users, roles | Format-Table
```

---

## 6. Ensure Host Headers Are on All Sites

**Kateqoriya:** Denial of Service Attacks  
**Fayl Yolu:** C:\TayqaSale\WebServices\DynamicToolManagementPortal\Core  
**Risk Səviyyəsi:** 🟡 Orta  
**CIS Benchmark:** CIS IIS 10 Benchmark — Section 6.1

### Problem Təsviri

Host header olmadan IIS saytı **yalnız IP ünvanı və port nömrəsi** ilə tanınır. Bu vəziyyətdə:

- **Host header hücumları** — hücumçu serverin IP-sinə istənilən domain adı ilə sorğu göndərə bilər
- **DNS rebinding hücumları** — zərərli veb saytlar serveri öz domainləri kimi istifadə edə bilər
- **IP vaxtəsilə sayt identifikasiyası** — eyni IP-də birdən çox sayt varsa, düzgün sayt seçilmir
- **Phishing** — hücumçular qanuni serveri öz phishing səhifələri üçün istifadə edə bilər

### Həll Yolu

Hər bir IIS saytının binding-lərində **host header** (host name) təyin edin.

#### Metod 1: IIS Manager vasitəsilə

1. **IIS Manager** açın
2. Sol paneldə **Sites** genişləndirin
3. Hər bir TayqaSale saytına sağ klik → **Edit Bindings...**
4. Hər binding üçün **Edit** basın:
   - **Host name** sahəsinə domain adını yazın (məs: `serviceportal.tayqasale.az`)
5. **OK** basın

#### Metod 2: AppCmd.exe vasitəsilə

```cmd
:: Hər sayt üçün binding-ə host header əlavə edin
appcmd.exe set site /site.name:"TayqaSaleServicePortal" /bindings:"http/*:80:serviceportal.tayqasale.az"
appcmd.exe set site /site.name:"TayqaSaleDynamicToolManagement" /bindings:"http/*:80:dtm.tayqasale.az"
appcmd.exe set site /site.name:"TayqaSaleCampaignManagement" /bindings:"http/*:80:campaign.tayqasale.az"
appcmd.exe set site /site.name:"TayqaSaleLicenseService" /bindings:"http/*:80:license.tayqasale.az"
```

#### Metod 3: PowerShell vasitəsilə

```powershell
Import-Module WebAdministration

# Bütün saytların binding-lərini yoxlayın
Get-Website | ForEach-Object {
    $siteName = $_.Name
    $bindings = Get-WebBinding -Name $siteName
    
    foreach ($binding in $bindings) {
        $info = $binding.bindingInformation
        # Format: IP:Port:HostHeader
        $parts = $info -split ':'
        if ($parts.Count -ge 3 -and [string]::IsNullOrEmpty($parts[2])) {
            Write-Host "PROBLEM: $siteName binding-ində host header yoxdur: $info" -ForegroundColor Red
        }
    }
}

# Host header əlavə etmək nümunəsi
Set-WebBinding -Name "TayqaSaleServicePortal" -BindingInformation "*:80:" -PropertyName HostHeader -Value "serviceportal.tayqasale.az"
```

#### ApplicationHost.config nəticəsi

```xml
<site name="TayqaSaleServicePortal" id="2">
    <bindings>
        <binding protocol="http" bindingInformation="*:80:serviceportal.tayqasale.az" />
        <binding protocol="https" bindingInformation="*:443:serviceportal.tayqasale.az" />
    </bindings>
</site>
```

### Yoxlama

```powershell
Get-Website | Select-Object Name, @{N='Bindings';E={($_ | Get-WebBinding | Select-Object -ExpandProperty bindingInformation) -join '; '}} | Format-Table -AutoSize -Wrap
```

Heç bir binding-də host header boş olmamalıdır (format: `IP:Port:HostHeader` — üçüncü hissə boş olmamalıdır).

---

## 7. Ensure HTTP Trace Method is Disabled

**Kateqoriya:** Dangerous Methods Enabled  
**Fayl Yolu:** C:\TayqaSale\WebServices\CampaignManagementPortal\WebUI  
**Risk Səviyyəsi:** 🔴 Yüksək  
**CIS Benchmark:** CIS IIS 10 Benchmark — Section 7.6

### Problem Təsviri

HTTP **TRACE** metodu diaqnostik məqsədlər üçün yaradılmışdır — server gələn sorğunu olduğu kimi cavab olaraq qaytarır. Bu, ciddi təhlükəsizlik boşluqlarına səbəb olur:

- **Cross-Site Tracing (XST) hücumları** — hücumçu JavaScript vasitəsilə TRACE sorğusu göndərir və istifadəçinin autentifikasiya cookie-lərini (HttpOnly ilə qorunan cookie-lər daxil) oğurlayır
- **Credential oğurluğu** — TRACE cavabı Authorization header-ını əks etdirir
- **Session hijacking** — oğurlanmış session token ilə istifadəçinin hesabına daxil olunur

### Həll Yolu

#### Metod 1: IIS Manager vasitəsilə (Request Filtering)

1. **IIS Manager** açın
2. Hər bir TayqaSale saytını seçin
3. **Request Filtering** üzərinə iki dəfə klik edin
4. **HTTP Verbs** tabına keçin
5. Sağ paneldə **Deny Verb...** basın
6. `TRACE` yazın → **OK**
7. Təkrar **Deny Verb...** → `TRACK` yazın → **OK**

#### Metod 2: web.config vasitəsilə (bütün TayqaSale servislərə tətbiq)

```xml
<configuration>
    <system.webServer>
        <security>
            <requestFiltering>
                <verbs>
                    <add verb="TRACE" allowed="false" />
                    <add verb="TRACK" allowed="false" />
                </verbs>
            </requestFiltering>
        </security>
    </system.webServer>
</configuration>
```

#### Metod 3: AppCmd.exe vasitəsilə

```cmd
:: Server səviyyəsində TRACE-i bloklayın (bütün saytlara aiddir)
appcmd.exe set config -section:system.webServer/security/requestFiltering /+"verbs.[verb='TRACE',allowed='False']"
appcmd.exe set config -section:system.webServer/security/requestFiltering /+"verbs.[verb='TRACK',allowed='False']"
```

#### Metod 4: PowerShell vasitəsilə

```powershell
Import-Module WebAdministration

# Server səviyyəsində TRACE və TRACK-i bloklayın
Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
    -filter "system.webServer/security/requestFiltering/verbs" -name "." `
    -value @{verb='TRACE';allowed='false'}

Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
    -filter "system.webServer/security/requestFiltering/verbs" -name "." `
    -value @{verb='TRACK';allowed='false'}
```

> **Qeyd:** `TRACK` metodu da bloklayın — bu Microsoft-un TRACE analogudur.

### Yoxlama

```powershell
# PowerShell ilə test edin
Invoke-WebRequest -Uri "http://serviceportal.tayqasale.az/" -Method TRACE -ErrorAction SilentlyContinue | Select-Object StatusCode

# Nəticə 404.6 (Verb Denied) olmalıdır
```

```cmd
:: curl ilə test edin
curl -X TRACE http://serviceportal.tayqasale.az/ -v
```

Cavab **404** və ya **405 Method Not Allowed** olmalıdır. Əgər 200 qaytarılırsa — problem hələ həll olunmayıb.

---

## 8. Ensure HttpCookie Mode is Configured for Session State

**Kateqoriya:** Session Hijacking  
**Fayl Yolu:** C:\TayqaSale\WebServices\LicenseService  
**Risk Səviyyəsi:** 🔴 Yüksək  
**CIS Benchmark:** CIS IIS 10 Benchmark — Section 7.9

### Problem Təsviri

ASP.NET session state idarəetməsində `cookieless` mode aktiv olduqda, session ID **URL-də** daşınır:

```
http://tayqasale.az/(S(lit3py55t21z5v55vlm25s55))/orderform.aspx
```

Bu, ciddi təhlükəsizlik problemləri yaradır:

- **Session Fixation** — hücumçu qurbana session ID olan URL göndərir
- **Session Hijacking** — session ID referrer header, loglar, browser tarixçəsi vasitəsilə ifşa olunur
- **URL paylaşma** — istifadəçi URL-i paylaşanda session ID-ni də paylaşır
- **Bookmarking** — session ID bookmark-larda saxlanır

### Həll Yolu

Session ID-ni **yalnız cookie** vasitəsilə daşıyın (`UseCookies` mode).

#### Metod 1: web.config vasitəsilə (hər TayqaSale servisi üçün)

```xml
<configuration>
    <system.web>
        <sessionState cookieless="UseCookies" />
        <httpCookies httpOnlyCookies="true" requireSSL="true" />
    </system.web>
</configuration>
```

#### Metod 2: machine.config vasitəsilə (server səviyyəsində)

```xml
<configuration>
    <system.web>
        <sessionState cookieless="UseCookies" regenerateExpiredSessionId="true" />
    </system.web>
</configuration>
```

#### Metod 3: PowerShell vasitəsilə — machine.config (Global, tövsiyə olunan)

Bütün servislərə tək bir dəyişikliklə tətbiq etmək üçün `machine.config` faylını yeniləyin:

```powershell
$machineConfigPath = "$env:windir\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config"

# Backup yaradın
Copy-Item $machineConfigPath "$machineConfigPath.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"

[xml]$xml = Get-Content $machineConfigPath

$systemWeb = $xml.configuration.'system.web'
if (-not $systemWeb) {
    $systemWeb = $xml.CreateElement('system.web')
    $xml.configuration.AppendChild($systemWeb) | Out-Null
}

$sessionState = $systemWeb.SelectSingleNode('sessionState')
if (-not $sessionState) {
    $sessionState = $xml.CreateElement('sessionState')
    $systemWeb.AppendChild($sessionState) | Out-Null
}

$sessionState.SetAttribute('cookieless', 'UseCookies')
$sessionState.SetAttribute('regenerateExpiredSessionId', 'true')
$xml.Save($machineConfigPath)
Write-Host "machine.config-da sessionState tətbiq edildi — bütün servislərə aiddir." -ForegroundColor Green

# Yoxlama
$check = [xml](Get-Content $machineConfigPath)
$val = $check.configuration.'system.web'.sessionState.cookieless
if ($val -eq 'UseCookies') {
    Write-Host "COMPLIANT: sessionState cookieless=UseCookies" -ForegroundColor Green
} else {
    Write-Host "NON-COMPLIANT: cookieless=$val" -ForegroundColor Red
}
```

> **Üstünlük:** `machine.config`-ə yazıldığında serverdəki **bütün** ASP.NET tətbiqlərinə avtomatik tətbiq olunur. Gələcəkdə əlavə olunan yeni servislər üçün ayrıca konfiqurasiya tələb olunmur.

#### `cookieless` parametrinin mümkün dəyərləri

| Dəyər | Təhlükəsizlik | Açıqlama |
|---|---|---|
| **`UseCookies`** | ✅ Təhlükəsiz | Session ID yalnız cookie-də — **Tövsiyə olunan** |
| `UseUri` | ❌ Təhlükəsiz deyil | Session ID URL-də |
| `AutoDetect` | ⚠️ Riskli | Brauzer cookie dəstəkləmirsə URL-ə keçir |
| `UseDeviceProfile` | ⚠️ Riskli | Cihaz profilinə əsaslanır |

### Yoxlama

```powershell
$webConfigPaths = Get-ChildItem -Path "C:\TayqaSale\WebServices" -Recurse -Filter "web.config"
foreach ($config in $webConfigPaths) {
    [xml]$xml = Get-Content $config.FullName
    $cookieless = $xml.configuration.'system.web'.sessionState.cookieless
    $status = if ($cookieless -eq 'UseCookies') { "COMPLIANT" } else { "NON-COMPLIANT ($cookieless)" }
    Write-Host "$($config.FullName): $status"
}
```

---

## 9. Ensure Non-ASCII Characters in URLs are Not Allowed

**Kateqoriya:** Brute Force Attacks  
**Fayl Yolu:** C:\TayqaSale\WebServices\TayqaMasterDataSyncService  
**Risk Səviyyəsi:** 🟡 Orta  
**CIS Benchmark:** CIS IIS 10 Benchmark — Section 7.12

### Problem Təsviri

URL-lərdə non-ASCII (yüksək bit) simvollara icazə verilməsi aşağıdakı hücumlara yol açır:

- **URL encoding hücumları** — hücumçular zərərli URL-ləri kodlaşdırılmış simvollarla gizlədir
- **Directory traversal** — `%c0%ae%c0%ae` kimi sequenceslarla `../` əvəzinə istifadə
- **Input validation bypass** — security filtrləri non-ASCII simvollarla keçilir
- **Brute-force exploitation** — geniş simvol dəsti ilə URL-ləri sınamaq daha asandır
- **Homoglyph hücumları** — Unicode simvollarla saxta URL-lər yaradılır

### Həll Yolu

#### Metod 1: IIS Manager vasitəsilə

1. **IIS Manager** açın
2. Server və ya sayt səviyyəsini seçin
3. **Request Filtering** üzərinə iki dəfə klik edin
4. Sağ paneldə **Edit Feature Settings...** basın
5. **Allow high-bit characters** checkbox-unu **söndürün** (uncheck)
6. **OK** basın

#### Metod 2: web.config vasitəsilə

```xml
<configuration>
    <system.webServer>
        <security>
            <requestFiltering allowHighBitCharacters="false" />
        </security>
    </system.webServer>
</configuration>
```

#### Metod 3: AppCmd.exe vasitəsilə

```cmd
:: Server səviyyəsində — bütün saytlara aiddir
appcmd.exe set config -section:system.webServer/security/requestFiltering /allowHighBitCharacters:"False"

:: Yalnız müəyyən sayt üçün
appcmd.exe set config "TayqaSaleMasterDataSync" -section:system.webServer/security/requestFiltering /allowHighBitCharacters:"False"
```

#### Metod 4: PowerShell vasitəsilə

```powershell
# Server səviyyəsində
Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
    -filter "system.webServer/security/requestFiltering" `
    -name "allowHighBitCharacters" -value "False"
```

> **Xəbərdarlıq:** Əgər tətbiqləriniz URL-lərdə Unicode/UTF-8 simvollar istifadə edirsə (məs: Azərbaycan dili səhifə adları), bu parametr onları bloklayar. TayqaSale servisləri API-lar olduğundan, non-ASCII URL-lərə ehtiyac yoxdur.

### Yoxlama

```powershell
(Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
    -filter "system.webServer/security/requestFiltering" -name "allowHighBitCharacters").Value
# Nəticə: False olmalıdır
```

```cmd
:: Test — non-ASCII simvol olan URL
curl -o /dev/null -s -w "%%{http_code}" "http://serviceportal.tayqasale.az/test%C0%AE"
:: Nəticə: 404 olmalıdır (404.12 — URL Has High Bit Chars)
```

---

## 10. Ensure Unique Application Pools is Set for Sites

**Kateqoriya:** Permission Management  
**Fayl Yolu:** IIS Server  
**Risk Səviyyəsi:** 🔴 Yüksək  
**CIS Benchmark:** CIS IIS 10 Benchmark — Section 3.4

### Problem Təsviri

Birdən çox saytın eyni application pool-u paylaşması ciddi təhlükəsizlik riski yaradır:

- **İzolyasiya yoxluğu** — bir saytdakı boşluq digər saytlara təsir edir
- **Resurs izolyasiyası yoxluğu** — bir sayt digər saytın yaddaşına, fayllarına çatışa bilər
- **Performans izolyasiyası yoxluğu** — bir saytın yüksək yükü hamını yavaşladır
- **Səlahiyyət artırma** — hücumçu bir zəif sayt vasitəsilə eyni pool-dakı digər saytlara çatışır
- **Crash izolyasiyası yoxluğu** — bir tətbiq çöksə, eyni pool-dakı bütün saytlar çökür

### Həll Yolu

Hər bir TayqaSale web servisi üçün **ayrı application pool** yaradın.

#### Metod 1: IIS Manager vasitəsilə

1. **IIS Manager** açın → **Application Pools**
2. Sağ paneldə **Add Application Pool...** basın
3. Hər servis üçün ayrı pool yaradın:
   - Ad: `TayqaSaleServicePortalPool`
   - .NET CLR Version: uyğun versiya
   - Managed Pipeline Mode: `Integrated`
4. Sonra hər saytı öz pool-una təyin edin:
   - Sol paneldə **Sites** → Sayta sağ klik → **Manage Application** → **Advanced Settings**
   - **Application Pool** sahəsini uyğun pool-a dəyişdirin

#### Metod 2: AppCmd.exe vasitəsilə

```cmd
:: Hər servis üçün ayrı application pool yaradın
appcmd.exe add apppool /name:"TayqaSaleServicePortalPool" /managedRuntimeVersion:"v4.0" /managedPipelineMode:"Integrated"
appcmd.exe add apppool /name:"TayqaSaleDTMPool" /managedRuntimeVersion:"v4.0" /managedPipelineMode:"Integrated"
appcmd.exe add apppool /name:"TayqaSaleCampaignPool" /managedRuntimeVersion:"v4.0" /managedPipelineMode:"Integrated"
appcmd.exe add apppool /name:"TayqaSaleLicensePool" /managedRuntimeVersion:"v4.0" /managedPipelineMode:"Integrated"
appcmd.exe add apppool /name:"TayqaSaleMasterDataPool" /managedRuntimeVersion:"v4.0" /managedPipelineMode:"Integrated"

:: Hər saytı öz pool-una təyin edin
appcmd.exe set app "TayqaSaleServicePortal/" /applicationPool:"TayqaSaleServicePortalPool"
appcmd.exe set app "TayqaSaleDTM/" /applicationPool:"TayqaSaleDTMPool"
appcmd.exe set app "TayqaSaleCampaign/" /applicationPool:"TayqaSaleCampaignPool"
appcmd.exe set app "TayqaSaleLicense/" /applicationPool:"TayqaSaleLicensePool"
appcmd.exe set app "TayqaSaleMasterData/" /applicationPool:"TayqaSaleMasterDataPool"

:: Hər pool üçün ApplicationPoolIdentity təyin edin
appcmd.exe set apppool "TayqaSaleServicePortalPool" /processModel.identityType:ApplicationPoolIdentity
appcmd.exe set apppool "TayqaSaleDTMPool" /processModel.identityType:ApplicationPoolIdentity
appcmd.exe set apppool "TayqaSaleCampaignPool" /processModel.identityType:ApplicationPoolIdentity
appcmd.exe set apppool "TayqaSaleLicensePool" /processModel.identityType:ApplicationPoolIdentity
appcmd.exe set apppool "TayqaSaleMasterDataPool" /processModel.identityType:ApplicationPoolIdentity
```

#### Metod 3: PowerShell vasitəsilə (Avtomatik — bütün saytlar üçün)

### Yoxlama

```powershell
# Hər saytın unikal pool-da olduğunu yoxlayın
$poolUsage = @{}
Get-Website | ForEach-Object {
    $pool = $_.applicationPool
    if ($poolUsage.ContainsKey($pool)) {
        $poolUsage[$pool] += ", $($_.Name)"
        Write-Host "PROBLEM: Pool '$pool' paylaşılır: $($poolUsage[$pool])" -ForegroundColor Red
    } else {
        $poolUsage[$pool] = $_.Name
    }
}

# Problem yoxdursa
if ($poolUsage.Values | Where-Object { $_ -like '*,*' }) {
    Write-Host "NON-COMPLIANT: Bəzi pool-lar paylaşılır!" -ForegroundColor Red
} else {
    Write-Host "COMPLIANT: Hər saytın unikal pool-u var." -ForegroundColor Green
}
```

---

## 11. Ensure Unlisted File Extensions are Not Allowed

**Kateqoriya:** Permission Management  
**Fayl Yolu:** C:\TayqaSale\WebServices\CampaignManagementPortal\Core  
**Risk Səviyyəsi:** 🔴 Yüksək  
**CIS Benchmark:** CIS IIS 10 Benchmark — Section 7.7

### Problem Təsviri

İIS defolt olaraq `allowUnlisted="true"` ilə gəlir — bu o deməkdir ki, **hər cür fayl uzantısına** sorğu göndərmək olar. Bu, ciddi problemlər yaradır:

- **Zərərli fayl yükləmə** — `.aspx`, `.php`, `.exe` kimi fayllar yüklənib icra oluna bilər
- **Konfiqurasiya fayllarına giriş** — `.config`, `.xml`, `.ini` faylları oxuna bilər
- **Source code ifşası** — `.cs`, `.vb`, `.bak` faylları ictimai olur
- **Backup faylları** — `.old`, `.bak`, `.swp` faylları vasitəsilə köhnə məlumatlar əldə edilir

### Həll Yolu

**Whitelist yanaşması** tətbiq edin: yalnız lazım olan uzantılara icazə verin, qalanları bloklayın.

#### Metod 1: web.config vasitəsilə

```xml
<configuration>
    <system.webServer>
        <security>
            <requestFiltering>
                <!-- Siyahıda olmayanları bloklayın -->
                <fileExtensions allowUnlisted="false">
                    <!-- Yalnız lazım olan uzantılara icazə -->
                    <clear />
                    <add fileExtension=".aspx" allowed="true" />
                    <add fileExtension=".asmx" allowed="true" />
                    <add fileExtension=".svc" allowed="true" />
                    <add fileExtension=".ashx" allowed="true" />
                    <add fileExtension=".css" allowed="true" />
                    <add fileExtension=".js" allowed="true" />
                    <add fileExtension=".mjs" allowed="true" />
                    <add fileExtension=".html" allowed="true" />
                    <add fileExtension=".htm" allowed="true" />
                    <add fileExtension=".png" allowed="true" />
                    <add fileExtension=".jpg" allowed="true" />
                    <add fileExtension=".gif" allowed="true" />
                    <add fileExtension=".ico" allowed="true" />
                    <add fileExtension=".woff" allowed="true" />
                    <add fileExtension=".woff2" allowed="true" />
                    <add fileExtension=".ttf" allowed="true" />
                    <add fileExtension=".eot" allowed="true" />
                    <add fileExtension=".bcmap" allowed="true" />
                    <add fileExtension=".wasm" allowed="true" />
                    <add fileExtension=".json" allowed="true" />
                    <add fileExtension=".xml" allowed="true" />
                    <!-- Images -->
                    <add fileExtension=".jpeg" allowed="true" />
                    <add fileExtension=".bmp" allowed="true" />
                    <add fileExtension=".webp" allowed="true" />
                    <add fileExtension=".tiff" allowed="true" />
                    <add fileExtension=".tif" allowed="true" />
                    <add fileExtension=".svg" allowed="true" />
                    <add fileExtension=".heic" allowed="true" />
                    <!-- PDF & Text -->
                    <add fileExtension=".pdf" allowed="true" />
                    <add fileExtension=".txt" allowed="true" />
                    <add fileExtension=".csv" allowed="true" />
                    <!-- Microsoft Office -->
                    <add fileExtension=".doc" allowed="true" />
                    <add fileExtension=".docx" allowed="true" />
                    <add fileExtension=".xls" allowed="true" />
                    <add fileExtension=".xlsx" allowed="true" />
                    <add fileExtension=".ppt" allowed="true" />
                    <add fileExtension=".pptx" allowed="true" />
                    <add fileExtension=".odt" allowed="true" />
                    <add fileExtension=".ods" allowed="true" />
                    <add fileExtension=".odp" allowed="true" />
                    <add fileExtension=".vsd" allowed="true" />
                    <add fileExtension=".vsdx" allowed="true" />
                    <add fileExtension=".mpp" allowed="true" />
                    <add fileExtension=".pub" allowed="true" />
                    <add fileExtension=".accdb" allowed="true" />
                    <!-- Audio -->
                    <add fileExtension=".mp3" allowed="true" />
                    <add fileExtension=".wav" allowed="true" />
                    <add fileExtension=".wma" allowed="true" />
                    <add fileExtension=".aac" allowed="true" />
                    <add fileExtension=".ogg" allowed="true" />
                    <add fileExtension=".flac" allowed="true" />
                    <add fileExtension=".m4a" allowed="true" />
                    <add fileExtension=".aiff" allowed="true" />
                    <add fileExtension=".amr" allowed="true" />
                    <!-- Video -->
                    <add fileExtension=".mp4" allowed="true" />
                    <add fileExtension=".avi" allowed="true" />
                    <add fileExtension=".mov" allowed="true" />
                    <add fileExtension=".wmv" allowed="true" />
                    <add fileExtension=".mkv" allowed="true" />
                    <add fileExtension=".flv" allowed="true" />
                    <add fileExtension=".webm" allowed="true" />
                    <add fileExtension=".m4v" allowed="true" />
                    <add fileExtension=".3gp" allowed="true" />
                    <add fileExtension=".mpeg" allowed="true" />
                    <add fileExtension=".mpg" allowed="true" />
                    <!-- Archives & Logs -->
                    <add fileExtension=".zip" allowed="true" />
                    <add fileExtension=".rar" allowed="true" />
                    <add fileExtension=".log" allowed="true" />
                    <!-- Həmişə bloklanan uzantılar -->
                    <add fileExtension=".config" allowed="false" />
                    <add fileExtension=".cs" allowed="false" />
                    <add fileExtension=".vb" allowed="false" />
                    <add fileExtension=".bak" allowed="false" />
                    <add fileExtension=".old" allowed="false" />
                    <add fileExtension=".mdb" allowed="false" />
                    <add fileExtension=".mdf" allowed="false" />
                    <add fileExtension=".pdb" allowed="false" />
                </fileExtensions>
            </requestFiltering>
        </security>
    </system.webServer>
</configuration>
```

#### Metod 2: AppCmd.exe vasitəsilə

```cmd
:: Siyahıda olmayanları bloklayın
appcmd.exe set config "TayqaSale" -section:system.webServer/security/requestFiltering /fileExtensions.allowUnlisted:"False"

:: İcazə verilən uzantıları əlavə edin
appcmd.exe set config "TayqaSale" -section:system.webServer/security/requestFiltering /+"fileExtensions.[fileExtension='.aspx',allowed='True']"
appcmd.exe set config "TayqaSale" -section:system.webServer/security/requestFiltering /+"fileExtensions.[fileExtension='.svc',allowed='True']"
appcmd.exe set config "TayqaSale" -section:system.webServer/security/requestFiltering /+"fileExtensions.[fileExtension='.asmx',allowed='True']"
```

#### Metod 3: PowerShell vasitəsilə

```powershell
Import-Module WebAdministration

# allowUnlisted-i false edin (server səviyyəsi)
Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
    -filter "system.webServer/security/requestFiltering/fileExtensions" `
    -name "allowUnlisted" -value "False"

# İcazə verilən uzantılar
$allowedExtensions = @(
    # Web
    '.aspx', '.asmx', '.svc', '.ashx', '.css', '.js', '.mjs', '.html', '.htm',
    '.woff', '.woff2', '.ttf', '.eot', '.json', '.xml', '.bcmap', '.wasm',
    # Images
    '.png', '.jpg', '.jpeg', '.bmp', '.gif', '.webp', '.tiff', '.tif', '.ico', '.svg', '.heic',
    # PDF & Text
    '.pdf', '.txt', '.csv',
    # Microsoft Office
    '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx',
    '.odt', '.ods', '.odp', '.vsd', '.vsdx', '.mpp', '.pub', '.accdb',
    # Audio
    '.mp3', '.wav', '.wma', '.aac', '.ogg', '.flac', '.m4a', '.aiff', '.amr',
    # Video
    '.mp4', '.avi', '.mov', '.wmv', '.mkv', '.flv', '.webm', '.m4v', '.3gp', '.mpeg', '.mpg',
    # Archives & Logs
    '.zip', '.rar', '.log'
)

foreach ($ext in $allowedExtensions) {
    Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
        -filter "system.webServer/security/requestFiltering/fileExtensions" -name "." `
        -value @{fileExtension=$ext;allowed='true'}
}

# Bloklanan uzantılar
$deniedExtensions = @('.config', '.cs', '.vb', '.bak', '.old', '.mdb', '.exe', '.dll', '.pdb')

foreach ($ext in $deniedExtensions) {
    Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
        -filter "system.webServer/security/requestFiltering/fileExtensions" -name "." `
        -value @{fileExtension=$ext;allowed='false'}
}
```

> **Vacib:** Bu dəyişikliyi etmədən əvvəl bütün TayqaSale servislərinin istifadə etdiyi fayl uzantılarının siyahısını çıxarın. Yanlış uzantı bloklanması servisi yararsız edə bilər.

> **Xəbərdarlıq — Uzantısız URL-lər:** `allowUnlisted="false"` aktiv edildikdə IIS uzantısız URL-ləri (`/apps`, `/api/users` və s.) bloklayır. IIS Request Filtering boş `fileExtension=""` dəyərini şema səviyyəsində qəbul etmir (nə PowerShell, nə appcmd vasitəsilə). Bu problem üçün **URL Rewrite** qaydası lazımdır:

```xml
<!-- Her saytın web.config-inə əlavə edin — allowUnlisted="false" ilə birlikdə -->
<system.webServer>
    <rewrite>
        <rules>
            <rule name="Extensionless URLs - SPA and API routing" stopProcessing="true">
                <!-- Nöqtəsiz (uzantısız) URL-lər üçün -->
                <match url="^[^.]*$" />
                <conditions>
                    <!-- Fiziki fayl və ya qovluq deyilsə -->
                    <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
                    <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
                </conditions>
                <!-- Kök URL-ə yönləndir (SPA entry point) -->
                <action type="Rewrite" url="/" />
            </rule>
        </rules>
    </rewrite>
</system.webServer>
```

> **Qeyd:** URL Rewrite modulu quraşdırılmamışdırsa: `Install-WindowsFeature Web-Url-Rewrite` və ya IIS Manager → Modules yoxlayın.

### Test proseduru

```powershell
# Əvvəlcə mövcud fayl uzantılarını analiz edin
Get-ChildItem -Path "C:\TayqaSale\WebServices" -Recurse -File | 
    Group-Object Extension | 
    Sort-Object Count -Descending | 
    Select-Object Name, Count | 
    Format-Table
```

### Yoxlama

```powershell
(Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
    -filter "system.webServer/security/requestFiltering/fileExtensions" -name "allowUnlisted").Value
# Nəticə: False olmalıdır
```

---

## 12. Ensure Web Content is on Non-System Partition

**Kateqoriya:** Sensitive File Access  
**Fayl Yolu:** %SystemDrive%\inetpub\wwwroot  
**Risk Səviyyəsi:** 🟡 Orta  
**CIS Benchmark:** CIS IIS 10 Benchmark — Section 3.1

### Problem Təsviri

Web məzmunun sistem diskində (C:\) saxlanması aşağıdakı riskləri yaradır:

- **Directory traversal hücumları** — hücumçu `../../windows/system32/` kimi yollarla sistem fayllarına çatışa bilər
- **Disk dolması** — web məzmun və ya log faylları böyüyərək sistem diskini doldursa, əməliyyat sistemi çökə bilər
- **Symbolic link hücumları** — web kataloqunda yaradılmış symlink vasitəsilə sistem faylları oxuna bilər
- **Səlahiyyət artırma** — web prosesi vasitəsilə sistem fayllarına çatışma ehtimalı artır

Hal-hazırda TayqaSale servislərinin bəzilərinin `%SystemDrive%\inetpub\wwwroot` yolunda olması bu riski artırır.

### Həll Yolu

Bütün web məzmunu başqa bir diskə (məs: `D:\`) köçürün.

#### Addım 1: Yeni disk bölməsini hazırlayın

```powershell
# D: diskinin mövcud olduğunu yoxlayın
Get-Volume | Select-Object DriveLetter, FileSystemLabel, SizeRemaining | Format-Table

# Web məzmun üçün qovluq yaradın
New-Item -Path "D:\WebContent\TayqaSale" -ItemType Directory -Force
```

#### Addım 2: Məzmunu köçürün

```powershell
# Xidməti dayandırın
Stop-Service W3SVC

# Robocopy ilə köçürmə (icazələri qoruyur)
robocopy "C:\inetpub\wwwroot" "D:\WebContent\wwwroot" /E /COPYALL /R:3 /W:5
robocopy "C:\TayqaSale\WebServices" "D:\WebContent\TayqaSale\WebServices" /E /COPYALL /R:3 /W:5
```

#### Addım 3: IIS saytlarını yeni yola yönləndirin

```cmd
:: Defolt saytın fiziki yolunu dəyişdirin
appcmd.exe set vdir "Default Web Site/" /physicalPath:"D:\WebContent\wwwroot"

:: TayqaSale servislərinin fiziki yollarını dəyişdirin
appcmd.exe set vdir "TayqaSaleServicePortal/" /physicalPath:"D:\WebContent\TayqaSale\WebServices\ServicePortal"
appcmd.exe set vdir "TayqaSaleDTM/" /physicalPath:"D:\WebContent\TayqaSale\WebServices\DynamicToolManagementPortal"
appcmd.exe set vdir "TayqaSaleCampaign/" /physicalPath:"D:\WebContent\TayqaSale\WebServices\CampaignManagementPortal"
```

#### Addım 4: PowerShell ilə toplu köçürmə

```powershell
Import-Module WebAdministration

# Bütün saytların fiziki yollarını göstərin
Get-Website | ForEach-Object {
    $site = $_
    Get-WebApplication -Site $site.Name | ForEach-Object {
        [PSCustomObject]@{
            Site = $site.Name
            App = $_.path
            PhysicalPath = $_.PhysicalPath
            OnSystemDrive = $_.PhysicalPath -like "$env:SystemDrive*"
        }
    }
} | Where-Object { $_.OnSystemDrive } | Format-Table
```

#### Addım 5: İcazələri tənzimləyin

```powershell
# Yeni qovluğa IIS icazələri verin
$acl = Get-Acl "D:\WebContent"
$rules = @(
    New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS","ReadAndExecute","ContainerInherit,ObjectInherit","None","Allow"),
    New-Object System.Security.AccessControl.FileSystemAccessRule("IUSR","ReadAndExecute","ContainerInherit,ObjectInherit","None","Allow")
)
foreach ($rule in $rules) { $acl.AddAccessRule($rule) }
Set-Acl "D:\WebContent" $acl
```

#### Addım 6: Xidməti yenidən başladın

```powershell
Start-Service W3SVC
# Saytları test edin
Get-Website | ForEach-Object { 
    $response = Invoke-WebRequest -Uri "http://localhost:$($_.bindings.Collection[0].bindingInformation.Split(':')[1])/" -UseBasicParsing -ErrorAction SilentlyContinue
    Write-Host "$($_.Name): $($response.StatusCode)"
}
```

### Yoxlama

```powershell
$systemDrive = $env:SystemDrive
$nonCompliant = Get-Website | Where-Object {
    $_.PhysicalPath -like "$systemDrive*"
}

if ($nonCompliant) {
    Write-Host "NON-COMPLIANT: Aşağıdakı saytlar sistem diskindədir:" -ForegroundColor Red
    $nonCompliant | Select-Object Name, PhysicalPath | Format-Table
} else {
    Write-Host "COMPLIANT: Bütün saytlar sistem diskindən kənardadır." -ForegroundColor Green
}
```

---

## Ümumi Yoxlama Skripti

Bütün 12 problemi bir dəfəyə yoxlamaq üçün:

```powershell
Import-Module WebAdministration

Write-Host "`n===== TayqaSale IIS Security Compliance Check =====" -ForegroundColor Cyan
Write-Host "Tarix: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n"

# 1. Application Pool Identity
Write-Host "1. Application Pool Identity:" -NoNewline
$pools = Get-ChildItem IIS:\AppPools | Where-Object { $_.processModel.identityType -ne 'ApplicationPoolIdentity' }
if ($pools) { Write-Host " NON-COMPLIANT" -ForegroundColor Red } else { Write-Host " COMPLIANT" -ForegroundColor Green }

# 2. Log Location
Write-Host "2. IIS Log Location:" -NoNewline
$logDir = (Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.applicationHost/sites/siteDefaults/logFile" -name "directory").Value
if ($logDir -like "$env:SystemDrive*") { Write-Host " NON-COMPLIANT ($logDir)" -ForegroundColor Red } else { Write-Host " COMPLIANT ($logDir)" -ForegroundColor Green }

# 3. Deployment Retail
Write-Host "3. Deployment Retail:" -NoNewline
$mc = [xml](Get-Content "$env:windir\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config" -ErrorAction SilentlyContinue)
$retail = $mc.configuration.'system.web'.deployment.retail
if ($retail -eq 'true') { Write-Host " COMPLIANT" -ForegroundColor Green } else { Write-Host " NON-COMPLIANT ($retail)" -ForegroundColor Red }

# 4. Dynamic IP Restrictions
Write-Host "4. Dynamic IP Restrictions:" -NoNewline
try {
    $dip = Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/security/dynamicIpSecurity/denyByConcurrentRequests" -name "enabled" -ErrorAction SilentlyContinue
    if ($dip.Value) { Write-Host " COMPLIANT" -ForegroundColor Green } else { Write-Host " NON-COMPLIANT" -ForegroundColor Red }
} catch { Write-Host " NON-COMPLIANT (modul quraşdırılmayıb)" -ForegroundColor Red }

# 7. TRACE Method
Write-Host "7. HTTP TRACE Disabled:" -NoNewline
$verbs = Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/security/requestFiltering/verbs" -name "." -ErrorAction SilentlyContinue
$traceBlocked = $verbs | Where-Object { $_.verb -eq 'TRACE' -and $_.allowed -eq $false }
if ($traceBlocked) { Write-Host " COMPLIANT" -ForegroundColor Green } else { Write-Host " NON-COMPLIANT" -ForegroundColor Red }

# 9. Non-ASCII Characters
Write-Host "9. Non-ASCII Blocked:" -NoNewline
$highBit = (Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/security/requestFiltering" -name "allowHighBitCharacters").Value
if (-not $highBit) { Write-Host " COMPLIANT" -ForegroundColor Green } else { Write-Host " NON-COMPLIANT" -ForegroundColor Red }

# 10. Unique Application Pools
Write-Host "10. Unique Application Pools:" -NoNewline
$poolNames = (Get-Website).applicationPool
$duplicates = $poolNames | Group-Object | Where-Object { $_.Count -gt 1 }
if ($duplicates) { Write-Host " NON-COMPLIANT (paylaşılan pool-lar var)" -ForegroundColor Red } else { Write-Host " COMPLIANT" -ForegroundColor Green }

# 11. Unlisted File Extensions
Write-Host "11. Unlisted Extensions Blocked:" -NoNewline
$unlisted = (Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/security/requestFiltering/fileExtensions" -name "allowUnlisted").Value
if (-not $unlisted) { Write-Host " COMPLIANT" -ForegroundColor Green } else { Write-Host " NON-COMPLIANT" -ForegroundColor Red }

# 12. Web Content Partition
Write-Host "12. Non-System Partition:" -NoNewline
$sysDriveSites = Get-Website | Where-Object { $_.PhysicalPath -like "$env:SystemDrive*" }
if ($sysDriveSites) { Write-Host " NON-COMPLIANT" -ForegroundColor Red } else { Write-Host " COMPLIANT" -ForegroundColor Green }

Write-Host "`n===== Yoxlama Tamamlandı =====" -ForegroundColor Cyan
```

---

## Tətbiq Sırası (Prioritet)

| Sıra | Problem | Risk | Mürəkkəblik | Downtime |
|---|---|---|---|---|
| 1 | Deployment Method Retail | 🔴 Yüksək | Aşağı | Yoxdur |
| 2 | HTTP Trace Method Disabled | 🔴 Yüksək | Aşağı | Yoxdur |
| 3 | Non-ASCII Characters | 🟡 Orta | Aşağı | Yoxdur |
| 4 | HttpCookie Mode (Session) | 🔴 Yüksək | Aşağı | Yoxdur |
| 5 | Application Pool Identity | 🔴 Yüksək | Orta | Minimal |
| 6 | Unique Application Pools | 🔴 Yüksək | Orta | Bəli (restart) |
| 7 | Global Authorization Rule | 🔴 Yüksək | Orta | Yoxdur |
| 8 | Dynamic IP Restrictions | 🔴 Yüksək | Orta | Yoxdur |
| 9 | Unlisted File Extensions | 🔴 Yüksək | Yüksək | Test lazım |
| 10 | Host Headers | 🟡 Orta | Aşağı | Minimal |
| 11 | IIS Log Location | 🟡 Orta | Orta | Minimal |
| 12 | Web Content Partition | 🟡 Orta | Yüksək | Bəli (köçürmə) |

---

**Hazırlandı:** TayqaSale Security Audit — Mart 2026  
**Mənbələr:** CIS Microsoft IIS 10 Benchmark v1.1.1, Microsoft IIS Documentation
