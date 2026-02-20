using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.GRN
{
    public class GRNHeaderDTO
    {
        public int TrnKy { get; set; }
        public DateTime TrnDt { get; set; }
        public int? PurAccKy { get; set; }
        public string? PurAccCd { get; set; }
        public string? PurAccNm { get; set; }
        public string? AccNm { get; set; }
        public string? AccTyp { get; set; }
        public int AccKy { get; set; }
        public int AdrKy { get; set; }
        public string? Code { get; set; }
        public string? YurRef { get; set; }
        public string? Des { get; set; }
        public short PmtTrmKy { get; set; }
    }
}
