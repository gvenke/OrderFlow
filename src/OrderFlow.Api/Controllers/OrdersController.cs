
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Orders;
using OrderFlow.Application.Orders.CreateOrder;
using OrderFlow.Contracts.Requests;
using OrderFlow.Contracts.Responses;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
public class OrdersController : ControllerBase
{
    private readonly CreateOrderHandler _createOrder;
    private readonly OrderService _service;

    public OrdersController(
        CreateOrderHandler createOrder,
        OrderService service)
    {
        _createOrder = createOrder;
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<CreateOrderResponse>> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty ||
            request.Quantity <= 0)
        {
            return BadRequest(
                "A valid product ID and positive quantity are required.");
        }

        var command = new CreateOrderCommand(
            request.ProductId,
            request.Quantity);

        var order = await _createOrder.Handle(
            command,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = order.Id },
            new CreateOrderResponse(order.Id, order.Status.ToString()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GetOrderResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var order = await _service.GetByIdAsync(
            id, cancellationToken);

        return order is null
            ? NotFound()
            : Ok(order);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GetOrderResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var orders = await _service.GetAllAsync(
            cancellationToken);

        return Ok(orders);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var deleted = await _service.CancelAsync(
            id, cancellationToken);

        return deleted
            ? NoContent()
            : NotFound();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(
            id,
            request.Quantity,
            cancellationToken);

        return updated
            ? NoContent()
            : NotFound();
    }

    [HttpPut("{id:int}/submit")]
    public async Task<ActionResult<SubmitOrderResponse>> Submit(
        int id,
        CancellationToken cancellationToken)
    {

        var response = await _service.SubmitAsync(
            id, cancellationToken);

        if (response is null)
            return NotFound();

        return AcceptedAtAction(
            nameof(GetById),
            new { id = response.Id },
            response);

    }
}


