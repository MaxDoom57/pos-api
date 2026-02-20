using System.Collections.Generic;

namespace Application.DTOs.SalesReturn
{
    public class SalesReturnUpdateDto
    {
        public int TrnNo { get; set; }
        public int AdrKy { get; set; }
        public int AccKy { get; set; }
        public string? DocNo { get; set; }
        public string? YurRef { get; set; }
        public int RepAdrKy { get; set; }
        public string? Description { get; set; }
        public decimal DisPer { get; set; }
        public decimal DisAmt { get; set; }
        public string PmtTrm { get; set; } = string.Empty;
        public string? PmtMode { get; set; }
        public int InvTrnKy { get; set; } 
        public List<SalesReturnUpdateItemDto> Items { get; set; } = new();
    }

    public class SalesReturnUpdateItemDto
    {
        public int ItemTrnKy { get; set; }      // existing ItmTrn primary key (0 if new)
        public int ItemKey { get; set; }        // ItmKy
        public decimal Quantity { get; set; }   // Qty
        public decimal? OriginalQuantity { get; set; } // optional support
        public decimal DiscountAmount { get; set; }   // DisAmt (frontend passes)
        public decimal CostPrice { get; set; }        // CosPri
        public decimal SalesPrice { get; set; }       // SlsPri
        public decimal TranPrice { get; set; }        // TrnPri
        public bool IsDeleted { get; set; }           // if true -> delete row
        public bool IsUpdated { get; set; }           // if true and ItemTrnKy>0 -> update
    }
}
