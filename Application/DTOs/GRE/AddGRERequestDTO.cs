using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.GRE
{
    public class AddGREItemDTO
    {
        public int ItemKey { get; set; }
        public double Qty { get; set; }
        public decimal SlsPri { get; set; }
        public decimal CosPri { get; set; }
        public decimal TrnPri { get; set; }
        public decimal GSTAmt { get; set; }
        public decimal NSLAmt { get; set; }
    }

    public class AddGRERequestDTO
    {
        public DateTime TrnDt { get; set; }
        public string? YurRef { get; set; }
        public string DocNo { get; set; } = string.Empty;

        public int AdrKy { get; set; }
        public int AccKy { get; set; }
        public short LocKy { get; set; }
        public string PmtTrm { get; set; }

        public List<AddGREItemDTO> Items { get; set; } = new();
    }
}
