using Application.DTOs.GRE;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/pos/v0.1/[controller]")]
    [Authorize]
    public class GREController : ControllerBase
    {
        private readonly GREService _service;

        public GREController(GREService service)
        {
            _service = service;
        }

        [HttpGet("{trnNo}")]
        public async Task<IActionResult> GetGRE(int trnNo)
        {
            try
            {
                var result = await _service.GetGREAsync(trnNo);

                if (!result.success)
                    return StatusCode(result.statusCode, new { message = result.message });

                return Ok(result.data);
            }
            catch (Exception ex)
            {
                return StatusCode(500,ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddGRE([FromBody] AddGRERequestDTO dto)
        {
            try
            {
                var result = await _service.AddNewGREAsync(dto);

                if (!result.success)
                    return StatusCode(result.statusCode, new { message = result.message });

                return StatusCode(201, new { message = "GRE Added Successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500,ex.Message);
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateGRE([FromBody] UpdateGRERequestDTO dto)
        {
            var result = await _service.UpdateGREAsync(dto);

            return StatusCode(result.statusCode, new { result.message });
        }

        [HttpDelete("{trnNo}")]
        public async Task<IActionResult> DeleteGRE(int trnNo)
        {
            try
            {
                var result = await _service.DeleteGreAsync(trnNo);

                if (!result.success)
                    return StatusCode(result.statusCode, new { message = result.message });

                return Ok(new { message = "GRE deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to delete GRE",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

    }
}
