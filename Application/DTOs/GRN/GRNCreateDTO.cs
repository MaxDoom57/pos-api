using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.GRN
{
    public class GRNItemCreateDTO
    {
        public int itemKey { get; set; }         
        public double qty { get; set; }          
        public decimal cosPri { get; set; }       
        public decimal trnPri { get; set; }       
        public decimal slsPri { get; set; }       
        public string? batchNo { get; set; }      
        public DateTime? expirDt { get; set; }    
        public int? itmBatchKy { get; set; }      
    }

    public class GRNCreateDTO
    {
        public DateTime trnDate { get; set; }         
        public int? purAccKy { get; set; }            
        public int accKy { get; set; }               
        public int adrKy { get; set; }                
        public short locKy { get; set; }              
        public string? yurRef { get; set; }           
        public string? des { get; set; }              
        public short pmtTrmKy { get; set; }           
        public List<GRNItemCreateDTO> items { get; set; } = new();
    }
}
