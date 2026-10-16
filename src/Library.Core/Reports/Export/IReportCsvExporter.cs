namespace Library.Core.Reports.Export;

public sealed record ReportExport(byte[] Content, string FileName);

public interface IReportCsvExporter
{
    ReportExport ExportBorrowingStatistics(BorrowingStatisticsReport report);
    ReportExport ExportFineRevenue(FineRevenueReport report);
    ReportExport ExportInventoryHealth(InventoryHealthReport report);
}
