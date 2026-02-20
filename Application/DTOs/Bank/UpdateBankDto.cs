using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Bank
{
    public class UpdateBankDto
    {
        public string BnkCd { get; set; } = string.Empty;
        public string BnkNm { get; set; } = string.Empty;
    }
}
