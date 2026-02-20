using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.GRN
{
    public class GRNUpdateDTO
    {
        public int trnNo { get; set; }
        public DateTime trnDate { get; set; }
        public int accKy { get; set; }
        public int adrKy { get; set; }
        public short locKy { get; set; }
        public string? yurRef { get; set; }
        public string? des { get; set; }
        public short pmtTrmKy { get; set; }

        public int? purAccKy { get; set; }

        public List<GRNItemCreateDTO> items { get; set; } = new();
    }
}
