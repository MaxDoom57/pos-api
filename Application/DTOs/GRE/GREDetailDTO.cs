using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.GRE
{
    public class GREDetailDTO
    {
        public int ItmKy { get; set; }
        public string ItmCd { get; set; } = string.Empty;
        public string? ItmNm { get; set; }
        public string? Unit { get; set; }
        public decimal? CosPri { get; set; }
        public decimal? TrnPri { get; set; }
        public decimal SlsPri { get; set; }
        public decimal Amt1 { get; set; }
        public decimal Amt2 { get; set; }
        public int ItmTrnKy { get; set; }
        public double Qty { get; set; }
    }
}
