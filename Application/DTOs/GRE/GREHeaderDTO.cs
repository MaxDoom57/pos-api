using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.GRE
{
    public class GREHeaderDTO
    {
        public int TrnKy { get; set; }
        public DateTime TrnDt { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? YurRef { get; set; }
        public string? AdrNm { get; set; }
        public string? AdrCd { get; set; }
        public short LocKy { get; set; }
        public int AdrKy { get; set; }
        public int AccKy { get; set; }
        public int PurAccKy { get; set; }
        public string? PurAccCd { get; set; }
        public int AccTrnKy { get; set; }
        public string? AccCd { get; set; }
        public string? AccNm { get; set; }
        public short PmtTrmKy { get; set; }
        public string? DocNo { get; set; }
    }
}
