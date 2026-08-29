using Domain.Abstract.Service;
using Domain.Models;
using Microsoft.AspNetCore.Mvc;
using API.Infrastructure;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController(IOrderService service) : ControllerBase
{
    private readonly IOrderService _service = service;

    [HttpGet("GenerateIdempotencyKey")]
    public ActionResult GenerateIdempotencyKey()
    {
        var idempotencyKey = _service.GenerateIdempotencyKey();

        return Ok(new
        {
            data = new { idempotencyKey }
        });
    }


    [HttpGet("GetRows")]
    public async Task<ActionResult> GetRows( string? keyword, int offset = 0, int limit = 10)
    {
        try
        {
            var data = await _service.GetRows(keyword, offset, limit);
            return Ok(new { data, count = data.Count });
        }
        catch (Exception ex)
        {
            return ResponseError(ex);
        }
    }

    [HttpGet("GetRow")]
    public async Task<ActionResult> GetRow(string id)
    {
        try
        {
            var data = await _service.GetRow(id);

            if (data is null)
            {
                return NotFound(ApiErrors.Create(HttpContext, "not_found", $"Order dengan ID '{id}' tidak ditemukan."));
            }

            return Ok(new { data });
        }
        catch (Exception ex)
        {
            return ResponseError(ex);
        }
    }

    [HttpPost("Insert")]
    public async Task<ActionResult> Insert([FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromBody] Order order)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                throw new ArgumentException("Header Idempotency-Key wajib diisi.");
            }

            var affectedRows = await _service.Insert(order, idempotencyKey);

            return Ok(new
            {
                data = new { order.ID },
                affectedRows
            });
        }
        catch (Exception ex)
        {
            return ResponseError(ex);
        }
    }

    [HttpPut("Update")]
    public async Task<ActionResult> Update([FromBody] Order order)
    {
        try
        {
            var affectedRows = await _service.Update(order);

            if (affectedRows == 0)
            {
                throw new KeyNotFoundException($"Order dengan ID '{order.ID}' tidak ditemukan.");
            }

            return Ok(new {affectedRows });
        }
        catch (Exception ex)
        {
            return ResponseError(ex);
        }
    }

    [HttpDelete("Delete")]
    public async Task<ActionResult> Delete([FromBody] string[] ids)
    {
        try
        {
            if (ids.Length == 0)
            {
                return BadRequest(ApiErrors.Create(HttpContext, "validation_error", "Minimal satu ID order wajib diisi."));
            }

            var affectedRows = await _service.Delete(ids);
            return Ok(new { data = (object?)null, affectedRows });
        }
        catch (Exception ex)
        {
            return ResponseError(ex);
        }
    }

    [HttpPut("Confirm")]
    public async Task<ActionResult> Confirm([FromBody] Order model)
    {
        try
        {
            var affectedRows = await _service.Confirm(model);
            return Ok(new { affectedRows });
        }
        catch (Exception ex)
        {
            return ResponseError(ex);
        }
    }

    [HttpPut("Ship")]
    public async Task<ActionResult> Ship([FromBody] Order model)
    {
        try
        {
            var affectedRows = await _service.Ship(model);
            return Ok(new { affectedRows });
        }
        catch (Exception ex)
        {
            return ResponseError(ex);
        }
    }

    [HttpPut("Deliver")]
    public async Task<ActionResult> Deliver([FromBody] Order model)
    {
        try
        {
            var affectedRows = await _service.Deliver(model);
            return Ok(new { affectedRows });
        }
        catch (Exception ex)
        {
            return ResponseError(ex);
        }
    }

    [HttpPut("Cancel")]
    public async Task<ActionResult> Cancel([FromBody] Order model)
    {
        try
        {
            var affectedRows = await _service.Cancel(model);
            return Ok(new { affectedRows });
        }
        catch (Exception ex)
        {
            return ResponseError(ex);
        }
    }

    private ObjectResult ResponseError(Exception exception)
    {
        var (status, code) = ApiErrors.Classify(exception);
        var message = status == StatusCodes.Status500InternalServerError
            ? "Terjadi kesalahan internal server."
            : exception.Message;
        return StatusCode(status, ApiErrors.Create(HttpContext, code, message));
    }
}
