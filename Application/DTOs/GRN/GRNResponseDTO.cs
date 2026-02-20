using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.GRN
{
    public class GRNResponseDTO
    {
        public GRNHeaderDTO Header { get; set; }
        public List<GRNDetailDTO> Details { get; set; }
    }
}
