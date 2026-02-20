using Application.DTOs.GRN;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/pos/v0.1/[controller]")]
    [Authorize]
    public class GRNController : ControllerBase
    {
        private readonly GRNService _service;

        public GRNController(GRNService service)
        {
            _service = service;
        }

        [HttpGet("{trnNo}")]
        public async Task<IActionResult> GetGRN(int trnNo)
        {
            try
            {
                var result = await _service.GetGRNAsync(trnNo);

                if (!result.success)
                    return NotFound(new { message = result.message });

                return Ok(result.data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateGRN([FromBody] GRNCreateDTO dto)
        {
            try
            {
                if (dto == null || dto.items == null || !dto.items.Any())
                    return BadRequest(new { message = "Invalid request payload" });

                var result = await _service.CreateGRNAsync(dto);

                return StatusCode(result.statusCode, new
                {
                    message = result.message,
                    trnNo = result.trnNo,
                    trnKy = result.trnKy
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateGRN([FromBody] GRNUpdateDTO dto)
        {
            if (dto == null || dto.items == null)
                return BadRequest(new { message = "Invalid data" });

            var result = await _service.UpdateGRNAsync(dto);

            return StatusCode(result.statusCode, new
            {
                message = result.message
            });
        }

        [HttpDelete("{trnNo}")]
        public async Task<IActionResult> DeleteGRN(int trnNo)
        {
            var result = await _service.DeleteGRNAsync(trnNo);

            return StatusCode(result.statusCode, new
            {
                message = result.message
            });
        }

    }
}
