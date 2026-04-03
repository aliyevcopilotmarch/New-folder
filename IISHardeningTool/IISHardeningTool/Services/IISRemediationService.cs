using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using IISHardeningTool.Models;
using Microsoft.Web.Administration;

namespace IISHardeningTool.Services;

public class IISRemediationService
{
    private readonly Action<string> _log;

    public IISRemediationService(Action<string> log)
    {
        _log = log;
    }

    private void Log(string message) => _log?.Invoke(message);

    // ========================================================================
    // 1. Application Pool Identity → ApplicationPoolIdentity
    // ========================================================================

    public ComplianceStatus CheckAppPoolIdentity(out string message)
    {
        try
        {
            using var mgr = new ServerManager();
            var nonCompliant = mgr.ApplicationPools
                .Where(p => (int)p.ProcessModel.IdentityType != 4) // 4 = ApplicationPoolIdentity
                .Select(p => p.Name)
                .ToList();

            if (nonCompliant.Count == 0)
            {
                message = "All application pools use ApplicationPoolIdentity.";
                return ComplianceStatus.Compliant;
            }

            message = $"Non-compliant pools: {string.Join(", ", nonCompliant)}";
            return ComplianceStatus.NonCompliant;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    public ComplianceStatus FixAppPoolIdentity(out string message)
    {
        try
        {
            using var mgr = new ServerManager();
            var fixedPools = new List<string>();

            foreach (var pool in mgr.ApplicationPools)
            {
                if ((int)pool.ProcessModel.IdentityType != 4)
                {
                    Log($"  Setting {pool.Name} identity to ApplicationPoolIdentity");
                    pool.ProcessModel.IdentityType = ProcessModelIdentityType.ApplicationPoolIdentity;
                    fixedPools.Add(pool.Name);
                }
            }

            if (fixedPools.Count == 0)
            {
                message = "All pools already compliant.";
                return ComplianceStatus.Compliant;
            }

            mgr.CommitChanges();
            message = $"Fixed pools: {string.Join(", ", fixedPools)}";
            return ComplianceStatus.Fixed;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    // ========================================================================
    // 2. Move IIS Log Location off system drive
    // ========================================================================

    public ComplianceStatus CheckLogLocation(out string message)
    {
        try
        {
            using var mgr = new ServerManager();
            var config = mgr.GetApplicationHostConfiguration();
            var sitesSection = config.GetSection("system.applicationHost/sites");
            var siteDefaults = sitesSection.GetChildElement("siteDefaults");
            var logFile = siteDefaults.GetChildElement("logFile");
            var directory = (string)logFile["directory"];

            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            if (directory.StartsWith(systemDrive, StringComparison.OrdinalIgnoreCase))
            {
                message = $"Logs on system drive: {directory}";
                return ComplianceStatus.NonCompliant;
            }

            message = $"Log location: {directory}";
            return ComplianceStatus.Compliant;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    public ComplianceStatus FixLogLocation(string newLogPath, out string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newLogPath))
            {
                newLogPath = "D:\\IISLogs";
            }

            if (!Directory.Exists(newLogPath))
            {
                Directory.CreateDirectory(newLogPath);
                Log($"  Created directory: {newLogPath}");
            }

            using var mgr = new ServerManager();
            var config = mgr.GetApplicationHostConfiguration();
            var sitesSection = config.GetSection("system.applicationHost/sites");
            var siteDefaults = sitesSection.GetChildElement("siteDefaults");
            var logFile = siteDefaults.GetChildElement("logFile");
            logFile["directory"] = newLogPath;

            mgr.CommitChanges();
            Log($"  Log directory set to: {newLogPath}");

            // Set ACL for IIS_IUSRS
            RunCommand("icacls", $"\"{newLogPath}\" /grant \"IIS_IUSRS:(OI)(CI)M\"");

            message = $"Log location moved to: {newLogPath}";
            return ComplianceStatus.Fixed;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    // ========================================================================
    // 3. Deployment Method Retail = true
    // ========================================================================

    public ComplianceStatus CheckDeploymentRetail(out string message)
    {
        try
        {
            var machineConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"Microsoft.NET\Framework64\v4.0.30319\Config\machine.config");

            if (!File.Exists(machineConfigPath))
            {
                message = "machine.config not found (64-bit .NET Framework may not be installed).";
                return ComplianceStatus.Error;
            }

            var doc = new XmlDocument();
            doc.Load(machineConfigPath);
            var deploymentNode = doc.SelectSingleNode("//configuration/system.web/deployment");

            if (deploymentNode?.Attributes?["retail"]?.Value == "true")
            {
                message = "deployment retail='true' is set.";
                return ComplianceStatus.Compliant;
            }

            message = "deployment retail is not set to 'true'.";
            return ComplianceStatus.NonCompliant;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    public ComplianceStatus FixDeploymentRetail(out string message)
    {
        try
        {
            var machineConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"Microsoft.NET\Framework64\v4.0.30319\Config\machine.config");

            if (!File.Exists(machineConfigPath))
            {
                message = "machine.config not found.";
                return ComplianceStatus.Error;
            }

            // Backup
            var backupPath = machineConfigPath + $".backup.{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(machineConfigPath, backupPath);
            Log($"  Backup created: {backupPath}");

            var doc = new XmlDocument();
            doc.Load(machineConfigPath);

            var configNode = doc.SelectSingleNode("//configuration");
            if (configNode == null)
            {
                message = "Invalid machine.config structure.";
                return ComplianceStatus.Error;
            }

            var systemWebNode = doc.SelectSingleNode("//configuration/system.web");
            if (systemWebNode == null)
            {
                systemWebNode = doc.CreateElement("system.web");
                configNode.AppendChild(systemWebNode);
            }

            var deploymentNode = doc.SelectSingleNode("//configuration/system.web/deployment");
            if (deploymentNode == null)
            {
                deploymentNode = doc.CreateElement("deployment");
                systemWebNode.AppendChild(deploymentNode);
            }

            ((XmlElement)deploymentNode).SetAttribute("retail", "true");
            doc.Save(machineConfigPath);

            message = "deployment retail='true' has been set.";
            return ComplianceStatus.Fixed;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    // ========================================================================
    // 4. Dynamic IP Address Restrictions
    // ========================================================================

    public ComplianceStatus CheckDynamicIpRestrictions(out string message)
    {
        try
        {
            // Check if the IP Security feature is installed
            var featureCheck = RunCommand("powershell", "-Command \"(Get-WindowsFeature Web-IP-Security).InstallState\"");
            if (!featureCheck.Contains("Installed"))
            {
                message = "Web-IP-Security feature is not installed.";
                return ComplianceStatus.NonCompliant;
            }

            using var mgr = new ServerManager();
            var config = mgr.GetApplicationHostConfiguration();

            var section = config.GetSection("system.webServer/security/dynamicIpSecurity");
            var denyByConcurrent = section.GetChildElement("denyByConcurrentRequests");
            var denyByRate = section.GetChildElement("denyByRequestRate");

            bool concurrentEnabled = (bool)denyByConcurrent["enabled"];
            bool rateEnabled = (bool)denyByRate["enabled"];

            if (concurrentEnabled && rateEnabled)
            {
                message = "Dynamic IP restrictions are enabled.";
                return ComplianceStatus.Compliant;
            }

            message = $"Concurrent requests: {(concurrentEnabled ? "ON" : "OFF")}, Rate limit: {(rateEnabled ? "ON" : "OFF")}";
            return ComplianceStatus.NonCompliant;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    public ComplianceStatus FixDynamicIpRestrictions(out string message)
    {
        try
        {
            // Install feature if needed
            var featureCheck = RunCommand("powershell", "-Command \"(Get-WindowsFeature Web-IP-Security).InstallState\"");
            if (!featureCheck.Contains("Installed"))
            {
                Log("  Installing Web-IP-Security feature...");
                RunCommand("powershell", "-Command \"Install-WindowsFeature Web-IP-Security\"");
            }

            using var mgr = new ServerManager();
            var config = mgr.GetApplicationHostConfiguration();

            var section = config.GetSection("system.webServer/security/dynamicIpSecurity");
            section["denyAction"] = "Forbidden";
            section["enableProxyMode"] = true;
            section["enableLoggingOnlyMode"] = false;

            var denyByConcurrent = section.GetChildElement("denyByConcurrentRequests");
            denyByConcurrent["enabled"] = true;
            denyByConcurrent["maxConcurrentRequests"] = (uint)10;

            var denyByRate = section.GetChildElement("denyByRequestRate");
            denyByRate["enabled"] = true;
            denyByRate["maxRequests"] = (uint)30;
            denyByRate["requestIntervalInMilliseconds"] = (uint)300;

            mgr.CommitChanges();
            message = "Dynamic IP restrictions enabled (concurrent: 10, rate: 30/300ms).";
            return ComplianceStatus.Fixed;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    // ========================================================================
    // 5. Global Authorization Rule
    // ========================================================================

    public ComplianceStatus CheckGlobalAuthorizationRule(out string message)
    {
        try
        {
            using var mgr = new ServerManager();
            var config = mgr.GetApplicationHostConfiguration();

            // Check server-level authorization
            var section = config.GetSection("system.webServer/security/authorization");
            var collection = section.GetCollection();

            bool hasAllowAll = collection.Any(e =>
                (string)e["accessType"] == "Allow" &&
                (string)e.Attributes["users"]?.Value == "*");

            if (hasAllowAll && collection.Count == 1)
            {
                message = "Default 'Allow All Users' rule found — no restriction in place.";
                return ComplianceStatus.NonCompliant;
            }

            message = "Authorization rules are configured.";
            return ComplianceStatus.Compliant;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    public ComplianceStatus FixGlobalAuthorizationRule(string allowedRoles, out string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(allowedRoles))
            {
                allowedRoles = "TayqaSaleAdmins,TayqaSaleServiceAccounts";
            }

            // Install URL Authorization if needed
            RunCommand("powershell", "-Command \"Install-WindowsFeature Web-Url-Auth -ErrorAction SilentlyContinue\"");

            using var mgr = new ServerManager();
            var config = mgr.GetApplicationHostConfiguration();

            var section = config.GetSection("system.webServer/security/authorization");
            var collection = section.GetCollection();

            // Clear existing rules
            collection.Clear();

            // Add allow rule for specified roles
            var allowRule = collection.CreateElement("add");
            allowRule["accessType"] = "Allow";
            allowRule["roles"] = allowedRoles;
            collection.Add(allowRule);

            // Add deny all rule
            var denyRule = collection.CreateElement("add");
            denyRule["accessType"] = "Deny";
            denyRule["users"] = "*";
            collection.Add(denyRule);

            mgr.CommitChanges();
            Log($"  Authorization: Allow roles [{allowedRoles}], Deny all others");
            message = $"Authorization restricted to roles: {allowedRoles}";
            return ComplianceStatus.Fixed;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    // ========================================================================
    // 7. Disable HTTP TRACE Method
    // ========================================================================

    public ComplianceStatus CheckTraceMethod(out string message)
    {
        try
        {
            using var mgr = new ServerManager();
            var config = mgr.GetApplicationHostConfiguration();
            var section = config.GetSection("system.webServer/security/requestFiltering");
            var verbs = section.GetChildElement("verbs").GetCollection();

            bool traceBlocked = verbs.Any(v =>
                string.Equals((string)v["verb"], "TRACE", StringComparison.OrdinalIgnoreCase) &&
                !(bool)v["allowed"]);

            bool trackBlocked = verbs.Any(v =>
                string.Equals((string)v["verb"], "TRACK", StringComparison.OrdinalIgnoreCase) &&
                !(bool)v["allowed"]);

            if (traceBlocked && trackBlocked)
            {
                message = "TRACE and TRACK methods are blocked.";
                return ComplianceStatus.Compliant;
            }

            message = $"TRACE blocked: {traceBlocked}, TRACK blocked: {trackBlocked}";
            return ComplianceStatus.NonCompliant;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    public ComplianceStatus FixTraceMethod(out string message)
    {
        try
        {
            using var mgr = new ServerManager();
            var config = mgr.GetApplicationHostConfiguration();
            var section = config.GetSection("system.webServer/security/requestFiltering");
            var verbsCollection = section.GetChildElement("verbs").GetCollection();

            var verbsToBlock = new[] { "TRACE", "TRACK" };
            foreach (var verb in verbsToBlock)
            {
                bool alreadyBlocked = verbsCollection.Any(v =>
                    string.Equals((string)v["verb"], verb, StringComparison.OrdinalIgnoreCase));

                if (!alreadyBlocked)
                {
                    var element = verbsCollection.CreateElement("add");
                    element["verb"] = verb;
                    element["allowed"] = false;
                    verbsCollection.Add(element);
                    Log($"  Blocked verb: {verb}");
                }
            }

            mgr.CommitChanges();
            message = "TRACE and TRACK methods are now blocked.";
            return ComplianceStatus.Fixed;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    // ========================================================================
    // 8. HttpCookie Mode for Session State
    // ========================================================================

    public ComplianceStatus CheckHttpCookieMode(out string message)
    {
        try
        {
            var machineConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"Microsoft.NET\Framework64\v4.0.30319\Config\machine.config");

            if (!File.Exists(machineConfigPath))
            {
                message = "machine.config not found.";
                return ComplianceStatus.Error;
            }

            var doc = new XmlDocument();
            doc.Load(machineConfigPath);
            var sessionStateNode = doc.SelectSingleNode("//configuration/system.web/sessionState");

            if (sessionStateNode?.Attributes?["cookieless"]?.Value == "UseCookies")
            {
                message = "Session state cookieless='UseCookies' is set.";
                return ComplianceStatus.Compliant;
            }

            var currentValue = sessionStateNode?.Attributes?["cookieless"]?.Value ?? "not set";
            message = $"Session state cookieless='{currentValue}' — should be 'UseCookies'.";
            return ComplianceStatus.NonCompliant;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    public ComplianceStatus FixHttpCookieMode(out string message)
    {
        try
        {
            var machineConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"Microsoft.NET\Framework64\v4.0.30319\Config\machine.config");

            if (!File.Exists(machineConfigPath))
            {
                message = "machine.config not found.";
                return ComplianceStatus.Error;
            }

            // Backup
            var backupPath = machineConfigPath + $".backup.{DateTime.Now:yyyyMMdd_HHmmss}";
            if (!File.Exists(backupPath.Substring(0, backupPath.LastIndexOf('.'))))
            {
                File.Copy(machineConfigPath, backupPath);
                Log($"  Backup created: {backupPath}");
            }

            var doc = new XmlDocument();
            doc.Load(machineConfigPath);

            var configNode = doc.SelectSingleNode("//configuration");
            var systemWebNode = doc.SelectSingleNode("//configuration/system.web");
            if (systemWebNode == null)
            {
                systemWebNode = doc.CreateElement("system.web");
                configNode!.AppendChild(systemWebNode);
            }

            var sessionStateNode = doc.SelectSingleNode("//configuration/system.web/sessionState");
            if (sessionStateNode == null)
            {
                sessionStateNode = doc.CreateElement("sessionState");
                systemWebNode.AppendChild(sessionStateNode);
            }

            ((XmlElement)sessionStateNode).SetAttribute("cookieless", "UseCookies");
            ((XmlElement)sessionStateNode).SetAttribute("regenerateExpiredSessionId", "true");

            doc.Save(machineConfigPath);
            message = "Session state set to cookieless='UseCookies'.";
            return ComplianceStatus.Fixed;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    // ========================================================================
    // 9. Block Non-ASCII Characters in URLs
    // ========================================================================

    public ComplianceStatus CheckNonAsciiCharacters(out string message)
    {
        try
        {
            using var mgr = new ServerManager();
            var config = mgr.GetApplicationHostConfiguration();
            var section = config.GetSection("system.webServer/security/requestFiltering");
            var allowHighBit = (bool)section["allowHighBitCharacters"];

            if (!allowHighBit)
            {
                message = "High-bit (non-ASCII) characters are blocked.";
                return ComplianceStatus.Compliant;
            }

            message = "High-bit characters are currently allowed in URLs.";
            return ComplianceStatus.NonCompliant;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    public ComplianceStatus FixNonAsciiCharacters(out string message)
    {
        try
        {
            using var mgr = new ServerManager();
            var config = mgr.GetApplicationHostConfiguration();
            var section = config.GetSection("system.webServer/security/requestFiltering");
            section["allowHighBitCharacters"] = false;

            mgr.CommitChanges();
            message = "Non-ASCII characters in URLs are now blocked.";
            return ComplianceStatus.Fixed;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    // ========================================================================
    // 10. Unique Application Pools per Site
    // ========================================================================

    public ComplianceStatus CheckUniqueAppPools(out string message)
    {
        try
        {
            using var mgr = new ServerManager();
            var sitePoolMap = mgr.Sites
                .Select(s => new { s.Name, Pool = s.ApplicationDefaults.ApplicationPoolName })
                .Concat(mgr.Sites.SelectMany(s => s.Applications.Select(a => new { Name = $"{s.Name}{a.Path}", Pool = a.ApplicationPoolName })))
                .GroupBy(x => x.Pool)
                .Where(g => g.Count() > 1)
                .ToList();

            if (sitePoolMap.Count == 0)
            {
                message = "All sites have unique application pools.";
                return ComplianceStatus.Compliant;
            }

            var duplicates = sitePoolMap.Select(g => $"{g.Key}: [{string.Join(", ", g.Select(x => x.Name))}]");
            message = $"Shared pools: {string.Join("; ", duplicates)}";
            return ComplianceStatus.NonCompliant;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    public ComplianceStatus FixUniqueAppPools(out string message)
    {
        try
        {
            using var mgr = new ServerManager();
            var fixedSites = new List<string>();

            // Find sites sharing pools
            var poolSiteMap = new Dictionary<string, List<string>>();
            foreach (var site in mgr.Sites)
            {
                foreach (var app in site.Applications)
                {
                    var poolName = app.ApplicationPoolName;
                    if (!poolSiteMap.ContainsKey(poolName))
                        poolSiteMap[poolName] = new List<string>();
                    poolSiteMap[poolName].Add($"{site.Name}{app.Path}");
                }
            }

            foreach (var kvp in poolSiteMap.Where(x => x.Value.Count > 1))
            {
                // Skip the first site (it keeps the original pool)
                for (int i = 1; i < kvp.Value.Count; i++)
                {
                    var siteName = kvp.Value[i].Split('/')[0];
                    var newPoolName = $"{siteName}Pool";

                    // Create new pool if it doesn't exist
                    if (mgr.ApplicationPools.All(p => p.Name != newPoolName))
                    {
                        var originalPool = mgr.ApplicationPools[kvp.Key];
                        var newPool = mgr.ApplicationPools.Add(newPoolName);
                        newPool.ManagedRuntimeVersion = originalPool.ManagedRuntimeVersion;
                        newPool.ManagedPipelineMode = originalPool.ManagedPipelineMode;
                        newPool.ProcessModel.IdentityType = ProcessModelIdentityType.ApplicationPoolIdentity;
                        Log($"  Created pool: {newPoolName}");
                    }

                    // Assign site to new pool
                    var site = mgr.Sites[siteName];
                    if (site != null)
                    {
                        foreach (var app in site.Applications)
                        {
                            if (app.ApplicationPoolName == kvp.Key)
                            {
                                app.ApplicationPoolName = newPoolName;
                                fixedSites.Add($"{siteName} -> {newPoolName}");
                            }
                        }
                    }
                }
            }

            if (fixedSites.Count == 0)
            {
                message = "All sites already have unique pools.";
                return ComplianceStatus.Compliant;
            }

            mgr.CommitChanges();
            message = $"Reassigned: {string.Join("; ", fixedSites)}";
            return ComplianceStatus.Fixed;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    // ========================================================================
    // 11. Block Unlisted File Extensions
    // ========================================================================

    public ComplianceStatus CheckUnlistedFileExtensions(out string message)
    {
        try
        {
            using var mgr = new ServerManager();
            var config = mgr.GetApplicationHostConfiguration();
            var section = config.GetSection("system.webServer/security/requestFiltering");
            var fileExtensions = section.GetChildElement("fileExtensions");
            var allowUnlisted = (bool)fileExtensions["allowUnlisted"];

            if (!allowUnlisted)
            {
                message = "Unlisted file extensions are blocked.";
                return ComplianceStatus.Compliant;
            }

            message = "Unlisted file extensions are currently allowed.";
            return ComplianceStatus.NonCompliant;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    public ComplianceStatus FixUnlistedFileExtensions(out string message)
    {
        try
        {
            using var mgr = new ServerManager();
            var config = mgr.GetApplicationHostConfiguration();
            var section = config.GetSection("system.webServer/security/requestFiltering");
            var fileExtensions = section.GetChildElement("fileExtensions");

            fileExtensions["allowUnlisted"] = false;

            var collection = fileExtensions.GetCollection();

            // Allowed extensions
            var allowedExts = new[] {
                ".aspx", ".asmx", ".svc", ".ashx", ".css", ".js", ".html", ".htm",
                ".png", ".jpg", ".gif", ".ico", ".woff", ".woff2", ".ttf", ".json", ".xml"
            };

            // Denied extensions
            var deniedExts = new[] {
                ".config", ".cs", ".vb", ".bak", ".old", ".mdb", ".mdf", ".exe", ".dll"
            };

            foreach (var ext in allowedExts)
            {
                if (!collection.Any(e => string.Equals((string)e["fileExtension"], ext, StringComparison.OrdinalIgnoreCase)))
                {
                    var element = collection.CreateElement("add");
                    element["fileExtension"] = ext;
                    element["allowed"] = true;
                    collection.Add(element);
                }
            }

            foreach (var ext in deniedExts)
            {
                if (!collection.Any(e => string.Equals((string)e["fileExtension"], ext, StringComparison.OrdinalIgnoreCase)))
                {
                    var element = collection.CreateElement("add");
                    element["fileExtension"] = ext;
                    element["allowed"] = false;
                    collection.Add(element);
                }
            }

            mgr.CommitChanges();
            message = $"Unlisted extensions blocked. Allowed: {allowedExts.Length}, Denied: {deniedExts.Length}";
            return ComplianceStatus.Fixed;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return ComplianceStatus.Error;
        }
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    public List<RemediationItem> GetAllItems()
    {
        return new List<RemediationItem>
        {
            new() { Id = 1, Title = "Application Pool Identity", Category = "Permission Management", RiskLevel = "High", CisBenchmark = "CIS IIS 10 — Section 3.5", Description = "Set all application pools to use ApplicationPoolIdentity", IsSelected = true },
            new() { Id = 2, Title = "IIS Log Location", Category = "Logging", RiskLevel = "Medium", CisBenchmark = "CIS IIS 10 — Section 5.1", Description = "Move IIS log files off system drive", IsSelected = true },
            new() { Id = 3, Title = "Deployment Method Retail", Category = "Information Disclosure", RiskLevel = "High", CisBenchmark = "CIS IIS 10 — Section 1.6", Description = "Set deployment retail='true' in machine.config", IsSelected = true },
            new() { Id = 4, Title = "Dynamic IP Restrictions", Category = "Denial of Service", RiskLevel = "High", CisBenchmark = "CIS IIS 10 — Section 6.2", Description = "Enable Dynamic IP Address Restrictions", IsSelected = true },
            new() { Id = 5, Title = "Global Authorization Rule", Category = "Permission Management", RiskLevel = "High", CisBenchmark = "CIS IIS 10 — Section 4.5", Description = "Restrict access with authorization rules", IsSelected = true },
            new() { Id = 7, Title = "HTTP TRACE Method", Category = "Dangerous Methods", RiskLevel = "High", CisBenchmark = "CIS IIS 10 — Section 7.6", Description = "Disable HTTP TRACE and TRACK methods", IsSelected = true },
            new() { Id = 8, Title = "HttpCookie Mode (Session)", Category = "Session Hijacking", RiskLevel = "High", CisBenchmark = "CIS IIS 10 — Section 7.9", Description = "Set session state to use cookies only", IsSelected = true },
            new() { Id = 9, Title = "Non-ASCII Characters in URLs", Category = "Brute Force", RiskLevel = "Medium", CisBenchmark = "CIS IIS 10 — Section 7.12", Description = "Block high-bit characters in URLs", IsSelected = true },
            new() { Id = 10, Title = "Unique Application Pools", Category = "Permission Management", RiskLevel = "High", CisBenchmark = "CIS IIS 10 — Section 3.4", Description = "Ensure each site has a unique application pool", IsSelected = true },
            new() { Id = 11, Title = "Unlisted File Extensions", Category = "Permission Management", RiskLevel = "High", CisBenchmark = "CIS IIS 10 — Section 7.7", Description = "Block unlisted file extensions (whitelist approach)", IsSelected = true },
        };
    }

    public (ComplianceStatus status, string message) CheckItem(int itemId)
    {
        string msg;
        var status = itemId switch
        {
            1 => CheckAppPoolIdentity(out msg),
            2 => CheckLogLocation(out msg),
            3 => CheckDeploymentRetail(out msg),
            4 => CheckDynamicIpRestrictions(out msg),
            5 => CheckGlobalAuthorizationRule(out msg),
            7 => CheckTraceMethod(out msg),
            8 => CheckHttpCookieMode(out msg),
            9 => CheckNonAsciiCharacters(out msg),
            10 => CheckUniqueAppPools(out msg),
            11 => CheckUnlistedFileExtensions(out msg),
            _ => throw new ArgumentException($"Unknown item ID: {itemId}")
        };
        msg ??= "";
        return (status, msg);
    }

    public (ComplianceStatus status, string message) FixItem(int itemId, string parameter = null)
    {
        string msg;
        var status = itemId switch
        {
            1 => FixAppPoolIdentity(out msg),
            2 => FixLogLocation(parameter ?? "D:\\IISLogs", out msg),
            3 => FixDeploymentRetail(out msg),
            4 => FixDynamicIpRestrictions(out msg),
            5 => FixGlobalAuthorizationRule(parameter ?? "TayqaSaleAdmins,TayqaSaleServiceAccounts", out msg),
            7 => FixTraceMethod(out msg),
            8 => FixHttpCookieMode(out msg),
            9 => FixNonAsciiCharacters(out msg),
            10 => FixUniqueAppPools(out msg),
            11 => FixUnlistedFileExtensions(out msg),
            _ => throw new ArgumentException($"Unknown item ID: {itemId}")
        };
        msg ??= "";
        return (status, msg);
    }

    private static string RunCommand(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return string.Empty;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30000);
            return output;
        }
        catch
        {
            return string.Empty;
        }
    }
}
