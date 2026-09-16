using TallyPrimeConnector.Contracts;
namespace TallyPrimeConnector.Processing;
public interface IProcessor { Task<ProcessingResult> ProcessAsync(IReadOnlyList<VoucherInfo> vouchers, CancellationToken cancellationToken); }
public interface IProcessingPipeline : IProcessor { }
public interface IValidationProcessor : IProcessor { }
public interface IReconciliationProcessor : IProcessor { }
public interface IAggregationProcessor : IProcessor { }
public interface IAnomalyProcessor : IProcessor { }
public sealed record ProcessingResult(IReadOnlyList<VoucherInfo> Vouchers, IReadOnlyList<string> Warnings, decimal DebitTotal, decimal CreditTotal);
public sealed class BasicValidationProcessor : IValidationProcessor
{ public Task<ProcessingResult> ProcessAsync(IReadOnlyList<VoucherInfo> vouchers, CancellationToken cancellationToken) { var entries = vouchers.SelectMany(x => x.Entries); return Task.FromResult(new ProcessingResult(vouchers, [], entries.Where(x => x.Amount.Direction == DebitCredit.Debit).Sum(x => x.Amount.Value), entries.Where(x => x.Amount.Direction == DebitCredit.Credit).Sum(x => x.Amount.Value))); } }
