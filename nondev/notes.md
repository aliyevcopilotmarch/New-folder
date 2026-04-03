    <deployment retail="true" />
    <sessionState cookieless="UseCookies" regenerateExpiredSessionId="true" />

# IIS 500 Xəta Log Skripti

```powershell
# Log qovluğunu tap
$logPath = (Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
    -filter "system.applicationHost/sites/siteDefaults/logFile" -name "directory").Value

Write-Host "Log qovluğu: $logPath"

# 500 xətalarını göstər
Get-ChildItem "$logPath" -Recurse -Filter "u_ex*.log" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 3 |
    ForEach-Object {
        Write-Host "--- $($_.FullName) ---" -ForegroundColor Yellow
        Get-Content $_.FullName | Where-Object { $_ -match " 500 " } | Select-Object -Last 5
    }
```

---

# 500.19 — Dublikat Konfiqurasiya Xətası

## Dublikatları Tap

```powershell
$appHostConfig = "$env:windir\system32\inetsrv\config\applicationHost.config"
[xml]$xml = Get-Content $appHostConfig

# Dublikat verb girişlərini yoxla
$verbs = $xml.SelectNodes("//verbs/add")
$verbs | Group-Object { $_.verb } | Where-Object { $_.Count -gt 1 } |
    ForEach-Object { Write-Host "DUBLIKAT: $($_.Name) — $($_.Count) dəfə" -ForegroundColor Red }

# Dublikat fileExtension girişlərini yoxla
$exts = $xml.SelectNodes("//fileExtensions/add")
$exts | Group-Object { $_.fileExtension } | Where-Object { $_.Count -gt 1 } |
    ForEach-Object { Write-Host "DUBLIKAT extension: $($_.Name) — $($_.Count) dəfə" -ForegroundColor Red }
```

## Dublikatları Təmizlə

```cmd
:: TRACE/TRACK dublikatlarını sil, yalnız bir dənə qalsın
appcmd.exe clear config -section:system.webServer/security/requestFiltering /verbs

:: Yenidən bir dəfə əlavə et
appcmd.exe set config -section:system.webServer/security/requestFiltering /+"verbs.[verb='TRACE',allowed='False']"
appcmd.exe set config -section:system.webServer/security/requestFiltering /+"verbs.[verb='TRACK',allowed='False']"
```

```powershell
# fileExtensions dublikatlarını yoxla və təmizlə
$appHostConfig = "$env:windir\system32\inetsrv\config\applicationHost.config"
[xml]$xml = Get-Content $appHostConfig
$exts = $xml.SelectNodes("//fileExtensions/add") | Group-Object { $_.fileExtension } | Where-Object { $_.Count -gt 1 }
if ($exts) {
    appcmd.exe clear config -section:system.webServer/security/requestFiltering /fileExtensions
    Write-Host "fileExtensions təmizləndi — yenidən əlavə edin" -ForegroundColor Yellow
}
```

Təmizlikdən sonra:

```cmd
iisreset
```

---

# 500.19 — web.config Section Lock Xətası

## Problematik Section-ları Tap

```powershell
# Hər web.config-i yoxla — 500.19-a səbəb olan section-ı tap
Get-ChildItem "C:\TayqaSale\WebServices" -Recurse -Filter "web.config" | ForEach-Object {
    $path = $_.FullName
    try {
        [xml]$xml = Get-Content $path -ErrorAction Stop
        # Integrated mode-da problem yaradan section-lar
        $problematic = @('system.web/httpHandlers', 'system.web/httpModules')
        foreach ($section in $problematic) {
            $parts = $section -split '/'
            $node = $xml.configuration
            foreach ($part in $parts) { $node = $node.$part }
            if ($node) {
                Write-Host "PROBLEM: $path — [$section]" -ForegroundColor Red
            }
        }
    } catch {
        Write-Host "XML xətası: $path" -ForegroundColor Yellow
    }
}
```

## Avtomatik Miqrasiya (httpHandlers/httpModules → system.webServer)

```cmd
%windir%\system32\inetsrv\appcmd migrate config "ProxyWebServiceWS/"
%windir%\system32\inetsrv\appcmd migrate config "RequestProcessorWS/"
```

## Bütün saytlar üçün avtomatik miqrasiya

```powershell
Import-Module WebAdministration
Get-Website | ForEach-Object {
    $siteName = $_.Name
    Write-Host "Miqrasiya edilir: $siteName" -ForegroundColor Cyan
    & "$env:windir\system32\inetsrv\appcmd.exe" migrate config "$siteName/"
}
```


