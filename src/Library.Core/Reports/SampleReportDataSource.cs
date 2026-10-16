namespace Library.Core.Reports;

public sealed class SampleReportDataSource : IReportDataSource
{
    private readonly InMemoryReportDataSource source = new(SampleReportData.Data);

    public Task<ReportData> ReadAsync(ReportFilter filter, CancellationToken ct = default) => source.ReadAsync(filter, ct);
}
