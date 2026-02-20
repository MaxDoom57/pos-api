using Application.DTOs.SalesReturn;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/pos/v0.1/[controller]")]
    [Authorize]
    public class SalesReturnController : ControllerBase
    {
        private readonly SalesReturnService _service;

        public SalesReturnController(SalesReturnService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> AddSalesReturn([FromBody] SalesReturnDto dto)
        {
            try
            {
                int trnKy = await _service.AddSalesReturnAsync(dto);
                return StatusCode(201, new
                {
                    message = "Sales return added successfully",
                    TrnKy = trnKy
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Sales return saving failed",
                    error = ex.Message
                });
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSalesReturn([FromBody] SalesReturnUpdateDto dto)
        {
            if (dto == null || !ModelState.IsValid)
                return BadRequest(new { message = "Invalid sales return data" });

            try
            {
                var trnKy = await _service.UpdateSalesReturnAsync(dto);
                return StatusCode(200, new { message = "Sales return updated", TrnKy = trnKy });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Sales return saving failed",
                    error = ex.Message
                });
            }
        }
    }
}
