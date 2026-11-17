using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Orders.CancelOrder;
using OrderFlow.Application.Orders.CreateOrder;
using OrderFlow.Application.Orders.SubmitOrder;
using OrderFlow.Application.Orders.UpdateOrder;
using OrderFlow.Contracts.Requests;
using OrderFlow.Contracts.Responses;
using OrderFlow.Domain.Orders;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;

namespace OrderFlow.Application.Orders;

public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly CreateOrderHandler _createOrderHandler;
    private readonly CancelOrderHandler _cancelOrderHandler;
    private readonly UpdateOrderHandler _updateOrderHandler;
    private readonly SubmitOrderHandler _submitOrderHandler;

    public OrderService(
        IOrderRepository repository,
        CreateOrderHandler createOrderHandler,
        CancelOrderHandler cancelOrderHandler,
        UpdateOrderHandler updateOrderHandler,
        SubmitOrderHandler submitOrderHandler)
    {
        _repository = repository;
        _createOrderHandler = createOrderHandler;
        _cancelOrderHandler = cancelOrderHandler;
        _updateOrderHandler = updateOrderHandler;
        _submitOrderHandler = submitOrderHandler;
    }

    public async Task<GetOrderResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(
             id, cancellationToken);

        return order is null
            ? null
            : MapToResponse(order);
    }

    public async Task<IReadOnlyList<GetOrderResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var orders = await _repository.GetAllAsync(
            cancellationToken);

        return orders
            .Select(MapToResponse)
            .ToList();
    }

    public async Task<CreateOrderResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createOrderHandler.Handle(
            new CreateOrderCommand(
                request.ProductId,
                request.Quantity),
            cancellationToken);

        return new CreateOrderResponse(
            result.Id,
           // result.CorrelationId,
            result.Status.ToString());
    }

    public Task<bool> CancelAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _cancelOrderHandler.Handle(
            new CancelOrderCommand(id),
            cancellationToken);
    }

    public Task<bool> UpdateAsync(
        int id,
        int quantity,
        CancellationToken cancellationToken)
    {
        return _updateOrderHandler.Handle(
            new UpdateOrderCommand(id, quantity),
            cancellationToken);
    }

    public async Task<SubmitOrderResponse?> SubmitAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _submitOrderHandler.Handle(
            new SubmitOrderCommand(id),
            cancellationToken);

        if (result is null)
            return null;

        return new SubmitOrderResponse(
            result.Id,
            result.CorrelationId,
            result.Status.ToString());
    }

    //public async Task<bool> DeleteAsync(
    //    int id,
    //    CancellationToken cancellationToken)
    //{
    //    var deleted = await _repository.DeleteAsync(
    //        id, cancellationToken);

    //    if (!deleted)
    //        return false;

    //    await _unitOfWork.SaveChangesAsync(
    //        cancellationToken);

    //    return true;
    //}



    private static GetOrderResponse MapToResponse(Order order)
    {
        return new GetOrderResponse(
          order.Id,
            order.Status.ToString(),
            order.ProductId,
            order.Quantity);
    }
}