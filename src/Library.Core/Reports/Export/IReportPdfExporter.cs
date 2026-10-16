namespace Library.Core.Reports.Export;

public interface IReportPdfExporter
{
    ReportExport ExportBorrowingStatistics(BorrowingStatisticsReport report);
    ReportExport ExportFineRevenue(FineRevenueReport report);
    ReportExport ExportInventoryHealth(InventoryHealthReport report);
}
