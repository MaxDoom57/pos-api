using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Bank
{
    public class BankDto
    {
        public short BnkKy { get; set; }
        public string BnkCd { get; set; } = string.Empty;
        public string? BnkNm { get; set; }
    }
}
