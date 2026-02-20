using Application.DTOs.Bank;
using Application.DTOs.Invoice;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/pos/v0.1/banks")]
    [Authorize]
    public class BanksController : ControllerBase
    {
        private readonly BankService _service;

        public BanksController(BankService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetBanks()
        {
            try
            {
                var result = await _service.GetBanksAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateBank([FromBody] CreateBankDto dto)
        {
            try
            {
                await _service.CreateBankAsync(dto);
                return Ok(new { message = "Bank created successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{bankKey:int}")]
        public async Task<IActionResult> UpdateBank(
         int bankKey,
         [FromBody] UpdateBankDto request)
        {
            if (bankKey <= 0)
                return BadRequest("Invalid bank key");

            try
            {
                await _service.UpdateBankAsync(bankKey, request);
                return Ok(new { message = "Bank updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{bankKey:int}")]
        public async Task<IActionResult> DeleteBank(int bankKey)
        {
            if (bankKey <= 0)
                return BadRequest("Invalid bank key");

            try
            {
                await _service.DeleteBankAsync(bankKey);
                return Ok(new { message = "Bank deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
