using System;
using System.Collections.Generic;
using System.Text;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Abstractions
{
    public interface IOrderRepository
    {
        Task AddAsync(
              Order order,
              CancellationToken cancellationToken);

        Task<Order?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);


        Task<Order?> GetByIdForUpdateAsync(
            int id,
            CancellationToken cancellationToken);

        Task<Order?> GetByCorrelationIdAsync(
            Guid correlationId,
            CancellationToken cancellationToken);

        Task<Order?> GetByCorrelationIdForUpdateAsync(
            Guid correlationId,
            CancellationToken cancellationToken);

        Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken);

        Task<bool> UpdateAsync(
            int id,
            int quantity,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<Order>> GetAllAsync(
            CancellationToken cancellationToken);
    }
}
