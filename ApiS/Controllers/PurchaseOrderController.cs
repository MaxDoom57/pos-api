using Application.DTOs.PurchaseOrder;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace POS.API.Controllers
{
    [ApiController]
    [Route("api/pos/v0.1/PurchaseOrder")]
    public class PurchaseOrderController : ControllerBase
    {
        private readonly PurchaseOrderService _service;

        public PurchaseOrderController(PurchaseOrderService service)
        {
            _service = service;
        }

        [HttpGet("{orderNo}")]
        public async Task<IActionResult> GetPurchaseOrder(
            int orderNo,
            [FromQuery] int ordTypKy)
        {
            var result = await _service.GetPurchaseOrderAsync(orderNo, ordTypKy);

            if (result == null)
                return NotFound(new { message = "Purchase order not found" });

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(PurchaseOrderSaveDto dto)
        {
            var orderNo = await _service.CreatePurchaseOrderAsync(dto);
            return Ok(new { orderNo });
        }

        [HttpPut("{orderNo}")]
        public async Task<IActionResult> Update(int orderNo, PurchaseOrderSaveDto dto)
        {
            await _service.UpdatePurchaseOrderAsync(orderNo, dto);
            return Ok(new { message = "Purchase order updated successfully" });
        }

        [HttpDelete("{orderNo}")]
        public async Task<IActionResult> Delete(int orderNo)
        {
            await _service.DeletePurchaseOrderAsync(orderNo);
            return Ok(new { message = "Purchase order deleted successfully" });
        }

    }
}
