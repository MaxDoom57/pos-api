using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.GRN
{
    public class GRNDetailDTO
    {
        public int ItmKy { get; set; }
        public string ItmCd { get; set; }
        public string? ItmNm { get; set; }
        public string? Unit { get; set; }
        public decimal? CosPri { get; set; }
        public decimal? TrnPri { get; set; }
        public decimal SlsPri { get; set; }
        public double Qty { get; set; }
        public decimal Amt { get; set; }
        public int ItmTrnKy { get; set; }
        public DateTime? ExpirDt { get; set; }
        public string? BatchNo { get; set; }
    }
}
