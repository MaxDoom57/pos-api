using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.SalesReturn
{
    public class SalesReturnItemDto
    {
        public int ItmKy { get; set; }
        public decimal Qty { get; set; }
        public decimal CosPri { get; set; }
        public decimal SlsPri { get; set; }
        public decimal TrnPri { get; set; }
        public decimal DisPer { get; set; }
    }

    public class SalesReturnDto
    {
        public string? DocNo { get; set; }
        public string? YurRef { get; set; }
        public int AccKy { get; set; }
        public string Description { get; set; } = "";
        public int RepAdrKy { get; set; }
        public int AdrKy { get; set; }
        public string PmtTrm { get; set; }
        public decimal DisPer { get; set; }
        public int TrnNo { get; set; }

        public List<SalesReturnItemDto> Items { get; set; }
    }
}
