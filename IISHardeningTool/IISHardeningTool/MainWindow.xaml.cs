using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using IISHardeningTool.Models;
using IISHardeningTool.Services;
using Microsoft.Win32;

namespace IISHardeningTool;

public partial class MainWindow : Window
{
    private readonly IISRemediationService _service;
    private ObservableCollection<RemediationItem> _items;

    public MainWindow()
    {
        InitializeComponent();
        _service = new IISRemediationService(LogMessage);
        _items = new ObservableCollection<RemediationItem>(_service.GetAllItems());
        DgItems.ItemsSource = _items;
        LogMessage("TayqaSale IIS Hardening Tool initialized.");
        LogMessage("Click 'Scan All' to check current compliance status.");
    }

    private void LogMessage(string message)
    {
        Dispatcher.Invoke(() =>
        {
            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            TxtLog.ScrollToEnd();
        });
    }

    private async void BtnScanAll_Click(object sender, RoutedEventArgs e)
    {
        SetButtonsEnabled(false);
        LogMessage("═══ Starting compliance scan... ═══");

        await Task.Run(() =>
        {
            foreach (var item in _items)
            {
                try
                {
                    LogMessage($"Checking #{item.Id}: {item.Title}...");
                    var (status, message) = _service.CheckItem(item.Id);
                    Dispatcher.Invoke(() =>
                    {
                        item.Status = status;
                        item.StatusMessage = message;
                    });
                    LogMessage($"  → {status}: {message}");
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        item.Status = ComplianceStatus.Error;
                        item.StatusMessage = ex.Message;
                    });
                    LogMessage($"  → Error: {ex.Message}");
                }
            }
        });

        DgItems.Items.Refresh();
        var compliant = _items.Count(i => i.Status == ComplianceStatus.Compliant);
        var nonCompliant = _items.Count(i => i.Status == ComplianceStatus.NonCompliant);
        var errors = _items.Count(i => i.Status == ComplianceStatus.Error);

        LogMessage($"═══ Scan complete: {compliant} compliant, {nonCompliant} non-compliant, {errors} errors ═══");
        SetButtonsEnabled(true);
    }

    private async void BtnScanSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = _items.Where(i => i.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            MessageBox.Show("No items selected. Please check the items you want to scan.",
                "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetButtonsEnabled(false);
        LogMessage($"═══ Scanning {selectedItems.Count} selected item(s)... ═══");

        await Task.Run(() =>
        {
            foreach (var item in selectedItems)
            {
                try
                {
                    LogMessage($"Checking #{item.Id}: {item.Title}...");
                    var (status, message) = _service.CheckItem(item.Id);
                    Dispatcher.Invoke(() =>
                    {
                        item.Status = status;
                        item.StatusMessage = message;
                    });
                    LogMessage($"  → {status}: {message}");
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        item.Status = ComplianceStatus.Error;
                        item.StatusMessage = ex.Message;
                    });
                    LogMessage($"  → Error: {ex.Message}");
                }
            }
        });

        DgItems.Items.Refresh();
        var compliant = selectedItems.Count(i => i.Status == ComplianceStatus.Compliant);
        var nonCompliant = selectedItems.Count(i => i.Status == ComplianceStatus.NonCompliant);
        var errors = selectedItems.Count(i => i.Status == ComplianceStatus.Error);
        LogMessage($"═══ Scan complete: {compliant} compliant, {nonCompliant} non-compliant, {errors} errors ═══");
        SetButtonsEnabled(true);
    }

    private async void BtnFixSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = _items.Where(i => i.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            MessageBox.Show("No items selected. Please check the items you want to fix.", 
                "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"This will apply fixes for {selectedItems.Count} selected item(s):\n\n" +
            string.Join("\n", selectedItems.Select(i => $"  • #{i.Id} {i.Title}")) +
            "\n\nThis modifies IIS server configuration. Continue?",
            "Confirm Fix", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        SetButtonsEnabled(false);
        LogMessage($"═══ Fixing {selectedItems.Count} selected item(s)... ═══");

        await FixItems(selectedItems);

        DgItems.Items.Refresh();
        SetButtonsEnabled(true);
    }

    private async void BtnFixAll_Click(object sender, RoutedEventArgs e)
    {
        var nonCompliantItems = _items.Where(i => i.Status == ComplianceStatus.NonCompliant).ToList();
        if (nonCompliantItems.Count == 0)
        {
            MessageBox.Show("No non-compliant items found. Run a scan first.", 
                "Nothing to Fix", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"This will apply fixes for {nonCompliantItems.Count} non-compliant item(s):\n\n" +
            string.Join("\n", nonCompliantItems.Select(i => $"  • #{i.Id} {i.Title}")) +
            "\n\nThis modifies IIS server configuration. Continue?",
            "Confirm Fix All", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        SetButtonsEnabled(false);
        LogMessage($"═══ Fixing {nonCompliantItems.Count} non-compliant item(s)... ═══");

        await FixItems(nonCompliantItems);

        DgItems.Items.Refresh();
        SetButtonsEnabled(true);
    }

    private async Task FixItems(List<RemediationItem> items)
    {
        await Task.Run(() =>
        {
            foreach (var item in items)
            {
                try
                {
                    LogMessage($"Fixing #{item.Id}: {item.Title}...");
                    var (status, message) = _service.FixItem(item.Id);
                    Dispatcher.Invoke(() =>
                    {
                        item.Status = status;
                        item.StatusMessage = message;
                    });
                    LogMessage($"  → {status}: {message}");
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        item.Status = ComplianceStatus.Error;
                        item.StatusMessage = ex.Message;
                    });
                    LogMessage($"  → Error: {ex.Message}");
                }
            }
        });

        var fixedCount = items.Count(i => i.Status == ComplianceStatus.Fixed);
        var errorCount = items.Count(i => i.Status == ComplianceStatus.Error);
        LogMessage($"═══ Fix complete: {fixedCount} fixed, {errorCount} errors ═══");
    }

    private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
    {
        TxtLog.Clear();
        LogMessage("Log cleared.");
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        bool allSelected = _items.All(i => i.IsSelected);
        foreach (var item in _items)
        {
            item.IsSelected = !allSelected;
        }
        DgItems.Items.Refresh();
    }

    private void BtnExportReport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|CSV files (*.csv)|*.csv",
            FileName = $"IIS_Hardening_Report_{DateTime.Now:yyyyMMdd_HHmmss}",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("TayqaSale IIS Hardening Tool — Compliance Report");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('=', 80));
        sb.AppendLine();

        foreach (var item in _items)
        {
            sb.AppendLine($"[{item.Status}] #{item.Id} — {item.Title}");
            sb.AppendLine($"  Category: {item.Category} | Risk: {item.RiskLevel}");
            sb.AppendLine($"  CIS Benchmark: {item.CisBenchmark}");
            sb.AppendLine($"  Details: {item.StatusMessage}");
            sb.AppendLine();
        }

        sb.AppendLine(new string('=', 80));
        sb.AppendLine("Summary:");
        sb.AppendLine($"  Compliant:     {_items.Count(i => i.Status == ComplianceStatus.Compliant)}");
        sb.AppendLine($"  Non-Compliant: {_items.Count(i => i.Status == ComplianceStatus.NonCompliant)}");
        sb.AppendLine($"  Fixed:         {_items.Count(i => i.Status == ComplianceStatus.Fixed)}");
        sb.AppendLine($"  Errors:        {_items.Count(i => i.Status == ComplianceStatus.Error)}");
        sb.AppendLine($"  Not Scanned:   {_items.Count(i => i.Status == ComplianceStatus.Unknown)}");

        sb.AppendLine();
        sb.AppendLine("Operation Log:");
        sb.AppendLine(new string('-', 40));
        Dispatcher.Invoke(() => sb.AppendLine(TxtLog.Text));

        File.WriteAllText(dialog.FileName, sb.ToString());
        LogMessage($"Report exported to: {dialog.FileName}");
    }

    private void SetButtonsEnabled(bool enabled)
    {
        Dispatcher.Invoke(() =>
        {
            BtnScanAll.IsEnabled = enabled;
            BtnScanSelected.IsEnabled = enabled;
            BtnFixSelected.IsEnabled = enabled;
            BtnFixAll.IsEnabled = enabled;
            BtnExportReport.IsEnabled = enabled;
        });
    }
}
