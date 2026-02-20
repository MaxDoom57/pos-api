using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.GRE
{
    public class GREResponseDTO
    {
        public GREHeaderDTO Header { get; set; } = null!;
        public List<GREDetailDTO> Details { get; set; } = new();
    }
}
