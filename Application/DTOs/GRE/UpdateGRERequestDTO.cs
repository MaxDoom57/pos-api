using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.GRE
{
    public class UpdateGREItemDTO
    {
        public int? ItmTrnKy { get; set; }      // null means new row
        public int ItemKey { get; set; }
        public double Qty { get; set; }
        public decimal TrnPri { get; set; }
        public decimal GSTAmt { get; set; }
        public decimal NSLAmt { get; set; }
        public bool ToDelete { get; set; }      // like VSF.ValueMatrix(vsfrows, 16) 
        public bool IsValid { get; set; }       // like VSF.ValueMatrix(vsfrows, 15)
    }

    public class UpdateGRERequestDTO
    {
        public int TrnNo { get; set; }
        public DateTime TrnDt { get; set; }
        public string? YurRef { get; set; }
        public string DocNo { get; set; } = string.Empty;

        public int AdrKy { get; set; }
        public int AccKy { get; set; }
        public short LocKy { get; set; }
        public string PmtTrm { get; set; }
        public List<UpdateGREItemDTO> Items { get; set; } = new();
    }
}
