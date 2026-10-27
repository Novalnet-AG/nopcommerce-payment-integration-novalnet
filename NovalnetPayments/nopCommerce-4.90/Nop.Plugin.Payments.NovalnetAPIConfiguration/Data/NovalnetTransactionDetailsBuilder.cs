using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Customers;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Domain;

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration.Data;

/// <summary>
/// Represents the payment token entity builder
/// </summary>
public class NovalnetTransactionDetailsBuilder : NopEntityBuilder<NovalnetTransactionDetails>
{
    #region Methods

    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(NovalnetTransactionDetails.Tid)).AsString().Nullable()
            .WithColumn(nameof(NovalnetTransactionDetails.TidStatus)).AsString(100).Nullable()
            .WithColumn(nameof(NovalnetTransactionDetails.OrderNo)).AsString(100).Nullable()
            .WithColumn(nameof(NovalnetTransactionDetails.OrderAmount)).AsInt32().Nullable()
            .WithColumn(nameof(NovalnetTransactionDetails.CallbackAmount)).AsInt32().Nullable();
    }

    #endregion
}
