using Domain.Abstract.Service;
using Domain.Models;
using Microsoft.AspNetCore.Mvc;
using API.Infrastructure;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderDetailController(IOrderDetailService service) : ControllerBase
{
    private readonly IOrderDetailService _service = service;

    [HttpGet("GetRows")]
    public async Task<ActionResult> GetRows(string? keyword, int offset = 0, int limit = 10)
    {
        try
        {
            var data = await _service.GetRows(keyword, offset, limit);
            return Ok(new { data, count = data.Count });
        }
        catch (Exception ex) { return ResponseError(ex); }
    }

    [HttpGet("GetRow")]
    public async Task<ActionResult> GetRow(string id)
    {
        try
        {
            var data = await _service.GetRow(id);
            if (data is null) return NotFound(ApiErrors.Create(HttpContext, "not_found", $"OrderDetail dengan ID '{id}' tidak ditemukan."));
            return Ok(new { data });
        }
        catch (Exception ex) { return ResponseError(ex); }
    }

    [HttpPost("Insert")]
    public async Task<ActionResult> Insert([FromBody] OrderDetail orderDetail)
    {
        try
        {
            var affectedRows = await _service.Insert(orderDetail);
            return Ok(new { data = new { orderDetail.ID }, affectedRows });
        }
        catch (Exception ex) { return ResponseError(ex); }
    }

    [HttpPut("Update")]
    public async Task<ActionResult> Update([FromBody] OrderDetail orderDetail)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(orderDetail.ID)) throw new ArgumentException("ID order detail wajib diisi.");
            var affectedRows = await _service.Update(orderDetail);
            if (affectedRows == 0) throw new KeyNotFoundException($"OrderDetail dengan ID '{orderDetail.ID}' tidak ditemukan.");
            return Ok(new { affectedRows });
        }
        catch (Exception ex) { return ResponseError(ex); }
    }

    [HttpDelete("Delete")]
    public async Task<ActionResult> Delete([FromBody] string[] ids)
    {
        try
        {
            if (ids.Length == 0) return BadRequest(ApiErrors.Create(HttpContext, "validation_error", "Minimal satu ID order detail wajib diisi."));
            var affectedRows = await _service.Delete(ids);
            return Ok(new { data = (object?)null, affectedRows });
        }
        catch (Exception ex) { return ResponseError(ex); }
    }

    private ObjectResult ResponseError(Exception exception)
    {
        var (status, code) = ApiErrors.Classify(exception);
        return StatusCode(status, ApiErrors.Create(HttpContext, code,
            status == 500 ? "Terjadi kesalahan internal server." : exception.Message));
    }
}
