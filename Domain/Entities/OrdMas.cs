using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities
{
    public class OrdMas
    {
        [Key]
        public int OrdKy { get; set; }

        public short OrdTypKy { get; set; }

        public int? OrdNo { get; set; }

        public bool fInAct { get; set; }

        public byte fApr { get; set; }

        public short CKy { get; set; }

        [MaxLength(10)]
        public string? OrdTyp { get; set; }

        public short OrdFrqKy { get; set; }

        public short PmtTrmKy { get; set; }

        [MaxLength(15)]
        public string? DocNo { get; set; }

        [MaxLength(15)]
        public string? YurRef { get; set; }

        public DateTime? OrdDt { get; set; }

        [MaxLength(60)]
        public string? Des { get; set; }

        public short OrdStsKy { get; set; }

        public int OrdRelKy { get; set; }

        public int PrntKy { get; set; }

        public int AdrKy { get; set; }

        public int? AccKy { get; set; }

        public int? RepAdrKy { get; set; }

        public int? DistAdrKy { get; set; }

        public short BUKy { get; set; }

        public short LocKy { get; set; }

        public DateTime? DlryDt { get; set; }

        public DateTime? OrdFinDt { get; set; }

        public short OrdCat1Ky { get; set; }

        public short OrdCat2Ky { get; set; }

        public short OrdCat3Ky { get; set; }

        public float? DisPer { get; set; }

        [Column(TypeName = "money")]
        public decimal SlsPri { get; set; }

        [Column(TypeName = "money")]
        public decimal? Amt1 { get; set; }

        [Column(TypeName = "money")]
        public decimal? Amt2 { get; set; }

        public float MarPer { get; set; }

        public bool fInv { get; set; }

        public byte tOrdSetOff { get; set; }

        [MaxLength(2)]
        public string? Status { get; set; }

        public short SKy { get; set; }

        public int? EntUsrKy { get; set; }

        public DateTime? EntDtm { get; set; }

        public string? OrdRem { get; set; }
    }
}
