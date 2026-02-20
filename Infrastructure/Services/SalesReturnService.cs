using Application.DTOs.SalesReturn;
using Application.Interfaces;
using Infrastructure.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Infrastructure.Services
{
    public class SalesReturnService
    {
        private readonly IDynamicDbContextFactory _factory;
        private readonly IUserRequestContext _userContext;
        private readonly CommonLookupService _lookup;
        private readonly IUserKeyService _userKeyService;

        public SalesReturnService(
            IDynamicDbContextFactory factory,
            IUserRequestContext userContext,
            CommonLookupService lookup,
            IUserKeyService userKeyService)
        {
            _factory = factory;
            _userContext = userContext;
            _lookup = lookup;
            _userKeyService = userKeyService;
        }

        public async Task<int> AddSalesReturnAsync(SalesReturnDto dto)
        {
            using var db = await _factory.CreateDbContextAsync();
            using var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                decimal totalDisAmt = 0;
                decimal totalNSLAmt = 0;
                decimal totalGSTAmt = 0;
                decimal totalInvAmt = 0;

                var userKey = await _userKeyService.GetUserKeyAsync(_userContext.UserId, _userContext.CompanyKey)
                    ?? throw new Exception("User key not found");

                short trnTypKy = await _lookup.GetTranTypeKeyAsync("SLSRTN");
                int trnNo = await _lookup.GetTranNumberLastAsync(_userContext.CompanyKey, "SLSRTN");

                short pmtTrmKy = await _lookup.GetPaymentTermKeyAsync(dto.PmtTrm);
                short pmtModeKy = await _lookup.GetPaymentModeKeyAsync(pmtTrmKy);
                int locKy = 276; //TODO: Get location key from url
                int invTrnKy = await _lookup.GetTrnKyByTrnNoAsync(dto.TrnNo);

                // -------------------------------------------------------------------
                // 1) Insert into TrnMas
                // -------------------------------------------------------------------
                int trnKy;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO TrnMas
                        (
                            CKy, TrnDt, TrnNo, TrnTypKy, OurCd, AdrKy, AccKy, fApr,
                            LocKy, PmtTrmKy, PmtModeKy, YurRef, RepAdrKy, DocNo,
                            Des, DisPer, TrnTrfLnkKy, EntUsrKy, EntDtm
                        )
                        OUTPUT INSERTED.TrnKy
                        VALUES
                        (
                            @CKy, @TrnDt, @TrnNo, @TrnTypKy, 'SLSRTN', @AdrKy, @AccKy, 1,
                            @LocKy, @PmtTrmKy, @PmtModeKy, @YurRef, @RepAdrKy, @DocNo,
                            @Des, @DisPer, @InvTrnKy, @EntUsrKy, @EntDtm
                        )";

                    cmd.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));
                    cmd.Parameters.Add(new SqlParameter("@TrnDt", DateTime.Now));
                    cmd.Parameters.Add(new SqlParameter("@TrnNo", trnNo));
                    cmd.Parameters.Add(new SqlParameter("@TrnTypKy", trnTypKy));
                    cmd.Parameters.Add(new SqlParameter("@AdrKy", dto.AdrKy));
                    cmd.Parameters.Add(new SqlParameter("@AccKy", dto.AccKy));
                    cmd.Parameters.Add(new SqlParameter("@LocKy", locKy));
                    cmd.Parameters.Add(new SqlParameter("@PmtTrmKy", pmtTrmKy));
                    cmd.Parameters.Add(new SqlParameter("@PmtModeKy", pmtModeKy));
                    cmd.Parameters.Add(new SqlParameter("@YurRef", dto.YurRef ?? " "));
                    cmd.Parameters.Add(new SqlParameter("@RepAdrKy", dto.RepAdrKy));
                    cmd.Parameters.Add(new SqlParameter("@DocNo", dto.DocNo ?? " "));
                    cmd.Parameters.Add(new SqlParameter("@Des", dto.Description ?? " "));
                    cmd.Parameters.Add(new SqlParameter("@DisPer", dto.DisPer));
                    cmd.Parameters.Add(new SqlParameter("@InvTrnKy", invTrnKy));
                    cmd.Parameters.Add(new SqlParameter("@EntUsrKy", userKey));
                    cmd.Parameters.Add(new SqlParameter("@EntDtm", DateTime.Now));

                    trnKy = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // -------------------------------------------------------------------
                // 2) Insert Item Lines
                // -------------------------------------------------------------------
                int lineNo = 1;

                foreach (var item in dto.Items)
                {
                    decimal disAmt = item.Qty * item.TrnPri * item.DisPer * 0.01m;

                    totalDisAmt += disAmt;
                    totalInvAmt += item.Qty * item.TrnPri;

                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;

                    cmd.CommandText = @"
                        INSERT INTO ItmTrn
                        (TrnKy, LiNo, CKy, ItmKy, Qty, CosPri, SlsPri,
                         TrnPri, LocKy, DisPer, DisAmt)
                        VALUES
                        (@TrnKy, @LiNo, @CKy, @ItmKy, @Qty, @CosPri, @SlsPri,
                         @TrnPri, @LocKy, @DisPer, @DisAmt)";

                    cmd.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                    cmd.Parameters.Add(new SqlParameter("@LiNo", lineNo++));
                    cmd.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));
                    cmd.Parameters.Add(new SqlParameter("@ItmKy", item.ItmKy));
                    cmd.Parameters.Add(new SqlParameter("@Qty", item.Qty));
                    cmd.Parameters.Add(new SqlParameter("@CosPri", item.CosPri));
                    cmd.Parameters.Add(new SqlParameter("@SlsPri", item.SlsPri));
                    cmd.Parameters.Add(new SqlParameter("@TrnPri", item.TrnPri));
                    cmd.Parameters.Add(new SqlParameter("@LocKy", locKy));
                    cmd.Parameters.Add(new SqlParameter("@DisPer", item.DisPer));
                    cmd.Parameters.Add(new SqlParameter("@DisAmt", disAmt));

                    await cmd.ExecuteNonQueryAsync();
                }

                decimal grossTotal = (totalInvAmt - totalDisAmt) + (totalGSTAmt + totalNSLAmt);

                // -------------------------------------------------------------------
                // 3) Insert into AccTrn
                // -------------------------------------------------------------------
                int salesAccKy = await _lookup.GetDefaultSalesAccountKeyAsync((short)_userContext.CompanyKey);

                // Customer (Negative)
                await InsertAccTrn(conn, tx, trnKy, dto.AccKy, 1, -grossTotal, pmtModeKy);

                // Sales account (Positive)
                await InsertAccTrn(conn, tx, trnKy, salesAccKy, 2, (totalInvAmt - totalDisAmt), pmtModeKy);

                await _lookup.IncrementTranNumberLastAsync(_userContext.CompanyKey, "SLSRTN");

                await tx.CommitAsync();
                return trnKy;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private async Task InsertAccTrn(DbConnection conn, DbTransaction tx, int trnKy, int accKy, int liNo, decimal amt, short pmtModeKy)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            cmd.CommandText = @"
                INSERT INTO AccTrn (TrnKy, AccKy, LiNo, Amt, PmtModeKy)
                VALUES (@TrnKy, @AccKy, @LiNo, @Amt, @PmtModeKy)";

            cmd.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
            cmd.Parameters.Add(new SqlParameter("@AccKy", accKy));
            cmd.Parameters.Add(new SqlParameter("@LiNo", liNo));
            cmd.Parameters.Add(new SqlParameter("@Amt", amt));
            cmd.Parameters.Add(new SqlParameter("@PmtModeKy", pmtModeKy));

            await cmd.ExecuteNonQueryAsync();
        }


        public async Task<int> UpdateSalesReturnAsync(SalesReturnUpdateDto dto)
        {
            using var db = await _factory.CreateDbContextAsync();
            using var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                // 1) Resolve TrnKy from TrnNo
                int trnKy;
                short pmtModeKy = 0;
                using (var cmdFind = conn.CreateCommand())
                {
                    cmdFind.Transaction = tx;
                    cmdFind.CommandText = @"
                        SELECT TrnKy
                        FROM vewTrnNo
                        WHERE OurCd = 'SLSRTN' AND TrnNo = @TrnNo";
                    cmdFind.Parameters.Add(new SqlParameter("@TrnNo", dto.TrnNo));

                    var res = await cmdFind.ExecuteScalarAsync();
                    if (res == null || res == DBNull.Value)
                        throw new Exception($"Transaction not found for TrnNo {dto.TrnNo}");

                    trnKy = Convert.ToInt32(res);
                }

                // 2) Get userKey
                var userKeyObj = await _userKeyService.GetUserKeyAsync(_userContext.UserId, _userContext.CompanyKey);
                if (userKeyObj == null)
                    throw new Exception("User key not found");
                int userKey = userKeyObj.Value;

                // fixed location as requested
                const int locKy = 276;

                // 3) Update TrnMas (set Status='U' and header fields)
                using (var cmdUpdHeader = conn.CreateCommand())
                {
                    cmdUpdHeader.Transaction = tx;
                    cmdUpdHeader.CommandText = @"
                        UPDATE TrnMas
                        SET Status = 'U',
                            TrnDt = @TrnDt,
                            AdrKy = @AdrKy,
                            AccKy = @AccKy,
                            Des = @Des,
                            LocKy = @LocKy,
                            RepAdrKy = @RepAdrKy,
                            DocNo = @DocNo,
                            YurRef = @YurRef,
                            PmtTrmKy = @PmtTrmKy,
                            PmtModeKy = @PmtModeKy,
                            DisPer = @DisPer,
                            TrnTrfLnkKy = @InvTrnKy
                        WHERE TrnKy = @TrnKy";

                    cmdUpdHeader.Parameters.Add(new SqlParameter("@TrnDt", DateTime.Now));
                    cmdUpdHeader.Parameters.Add(new SqlParameter("@AdrKy", dto.AdrKy));
                    cmdUpdHeader.Parameters.Add(new SqlParameter("@AccKy", dto.AccKy));
                    cmdUpdHeader.Parameters.Add(new SqlParameter("@Des", dto.Description ?? string.Empty));
                    cmdUpdHeader.Parameters.Add(new SqlParameter("@LocKy", locKy));
                    cmdUpdHeader.Parameters.Add(new SqlParameter("@RepAdrKy", dto.RepAdrKy));
                    cmdUpdHeader.Parameters.Add(new SqlParameter("@DocNo", (object?)dto.DocNo ?? DBNull.Value));
                    cmdUpdHeader.Parameters.Add(new SqlParameter("@YurRef", (object?)dto.YurRef ?? DBNull.Value));

                    // Accept that PmtTrm may be a code string; we try to fetch the key if provided
                    short pmtTrmKy = 0;
                    if (!string.IsNullOrWhiteSpace(dto.PmtTrm))
                    {
                        pmtTrmKy = await _lookup.GetPaymentTermKeyAsync(dto.PmtTrm);
                    }
                    cmdUpdHeader.Parameters.Add(new SqlParameter("@PmtTrmKy", pmtTrmKy));

                    if (pmtTrmKy != 0)
                        pmtModeKy = await _lookup.GetPaymentModeKeyAsync(pmtTrmKy);

                    cmdUpdHeader.Parameters.Add(new SqlParameter("@PmtModeKy", pmtModeKy));
                    cmdUpdHeader.Parameters.Add(new SqlParameter("@DisPer", dto.DisPer));
                    cmdUpdHeader.Parameters.Add(new SqlParameter("@InvTrnKy", dto.InvTrnKy));
                    cmdUpdHeader.Parameters.Add(new SqlParameter("@TrnKy", trnKy));

                    await cmdUpdHeader.ExecuteNonQueryAsync();
                }

                // 4) Process item lines (delete / insert / update)
                decimal totalInvAmt = 0m;
                decimal totalDisAmt = 0m;
                int lineNo = 1; // will be used to set LiNo for new inserts and sequence

                foreach (var it in dto.Items)
                {
                    // compute disAmt as provided by frontend; fallback to calculation if zero
                    decimal disAmt = it.DiscountAmount;
                    if (disAmt == 0m)
                    {
                        disAmt = it.Quantity * it.TranPrice * 0.0m; // if frontend doesn't provide, keep 0
                    }

                    if (it.IsDeleted)
                    {
                        if (it.ItemTrnKy > 0)
                        {
                            using var cmdDel = conn.CreateCommand();
                            cmdDel.Transaction = tx;
                            cmdDel.CommandText = "DELETE FROM ItmTrn WHERE ItmTrnKy = @ItmTrnKy";
                            cmdDel.Parameters.Add(new SqlParameter("@ItmTrnKy", it.ItemTrnKy));
                            await cmdDel.ExecuteNonQueryAsync();
                        }
                        // deleted rows do not contribute to totals
                        continue;
                    }

                    // Non-deleted rows contribute to totals
                    totalInvAmt += it.Quantity * it.TranPrice;
                    totalDisAmt += disAmt;

                    if (it.ItemTrnKy == 0)
                    {
                        // INSERT new ItmTrn
                        using var cmdIns = conn.CreateCommand();
                        cmdIns.Transaction = tx;
                        cmdIns.CommandText = @"
                            INSERT INTO ItmTrn
                                (TrnKy, LiNo, CKy, ItmKy, Qty, CosPri, SlsPri, TrnPri, LocKy, DisPer, DisAmt, EntUsrKy)
                            VALUES
                                (@TrnKy, @LiNo, @CKy, @ItmKy, @Qty, @CosPri, @SlsPri, @TrnPri, @LocKy, @DisPer, @DisAmt, @EntUsrKy)";
                        cmdIns.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                        cmdIns.Parameters.Add(new SqlParameter("@LiNo", lineNo++));
                        cmdIns.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));
                        cmdIns.Parameters.Add(new SqlParameter("@ItmKy", it.ItemKey));
                        cmdIns.Parameters.Add(new SqlParameter("@Qty", it.Quantity));
                        cmdIns.Parameters.Add(new SqlParameter("@CosPri", it.CostPrice));
                        cmdIns.Parameters.Add(new SqlParameter("@SlsPri", it.SalesPrice));
                        cmdIns.Parameters.Add(new SqlParameter("@TrnPri", it.TranPrice));
                        cmdIns.Parameters.Add(new SqlParameter("@LocKy", locKy));
                        // DisPer we can derive from discount amount if needed; frontend provided DisAmt already
                        decimal disPer = it.TranPrice != 0 ? (it.DiscountAmount / (it.Quantity * it.TranPrice) * 100) : 0m;
                        cmdIns.Parameters.Add(new SqlParameter("@DisPer", disPer));
                        cmdIns.Parameters.Add(new SqlParameter("@DisAmt", disAmt));
                        cmdIns.Parameters.Add(new SqlParameter("@EntUsrKy", userKey));

                        await cmdIns.ExecuteNonQueryAsync();
                    }
                    else if (it.IsUpdated)
                    {
                        // UPDATE existing ItmTrn
                        using var cmdUpd = conn.CreateCommand();
                        cmdUpd.Transaction = tx;
                        cmdUpd.CommandText = @"
                            UPDATE ItmTrn
                            SET ItmKy = @ItmKy,
                                Qty = @Qty,
                                CosPri = @CosPri,
                                SlsPri = @SlsPri,
                                TrnPri = @TrnPri,
                                DisPer = @DisPer,
                                DisAmt = @DisAmt,
                                LocKy = @LocKy
                            WHERE ItmTrnKy = @ItmTrnKy";
                        cmdUpd.Parameters.Add(new SqlParameter("@ItmKy", it.ItemKey));
                        cmdUpd.Parameters.Add(new SqlParameter("@Qty", it.Quantity));
                        cmdUpd.Parameters.Add(new SqlParameter("@CosPri", it.CostPrice));
                        cmdUpd.Parameters.Add(new SqlParameter("@SlsPri", it.SalesPrice));
                        cmdUpd.Parameters.Add(new SqlParameter("@TrnPri", it.TranPrice));
                        decimal disPer = it.TranPrice != 0 ? (it.DiscountAmount / (it.Quantity * it.TranPrice) * 100) : 0m;
                        cmdUpd.Parameters.Add(new SqlParameter("@DisPer", disPer));
                        cmdUpd.Parameters.Add(new SqlParameter("@DisAmt", disAmt));
                        cmdUpd.Parameters.Add(new SqlParameter("@LocKy", locKy));
                        cmdUpd.Parameters.Add(new SqlParameter("@ItmTrnKy", it.ItemTrnKy));

                        await cmdUpd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // No change for this line (frontend said IsUpdated = false and IsDeleted = false)
                        // but we still increment line counter for new LiNo assignment consistency.
                        lineNo++;
                    }
                } // end foreach items

                decimal grossTotal = (totalInvAmt - totalDisAmt);

                // 5) Upsert AccTrn lines (LiNo 1 = customer, LiNo 2 = sales)
                // Find if AccTrn exists for LiNo=1 and LiNo=2
                int? accTrn1 = null;
                int? accTrn2 = null;
                using (var cmdCheck1 = conn.CreateCommand())
                {
                    cmdCheck1.Transaction = tx;
                    cmdCheck1.CommandText = "SELECT AccTrnKy FROM vewAccTrnKy WHERE TrnKy = @TrnKy AND LiNo = 1";
                    cmdCheck1.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                    var r1 = await cmdCheck1.ExecuteScalarAsync();
                    if (r1 != null && r1 != DBNull.Value) accTrn1 = Convert.ToInt32(r1);
                }
                using (var cmdCheck2 = conn.CreateCommand())
                {
                    cmdCheck2.Transaction = tx;
                    cmdCheck2.CommandText = "SELECT AccTrnKy FROM vewAccTrnKy WHERE TrnKy = @TrnKy AND LiNo = 2";
                    cmdCheck2.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                    var r2 = await cmdCheck2.ExecuteScalarAsync();
                    if (r2 != null && r2 != DBNull.Value) accTrn2 = Convert.ToInt32(r2);
                }

                // Sales account key (positive)
                int salesAccKy = await _lookup.GetDefaultSalesAccountKeyAsync((short)_userContext.CompanyKey);

                // 1) Customer debit (line 1) -> negative gross total
                if (accTrn1.HasValue)
                {
                    using var cmdUpdate = conn.CreateCommand();
                    cmdUpdate.Transaction = tx;
                    cmdUpdate.CommandText = @"
                        UPDATE AccTrn
                        SET Amt = @Amt, AccKy = @AccKy, Status = 'U', PmtModeKy = @PmtModeKy
                        WHERE AccTrnKy = @AccTrnKy";
                    cmdUpdate.Parameters.Add(new SqlParameter("@Amt", -1 * grossTotal));
                    cmdUpdate.Parameters.Add(new SqlParameter("@AccKy", dto.AccKy));
                    cmdUpdate.Parameters.Add(new SqlParameter("@PmtModeKy", pmtModeKy));
                    cmdUpdate.Parameters.Add(new SqlParameter("@AccTrnKy", accTrn1.Value));
                    await cmdUpdate.ExecuteNonQueryAsync();
                }
                else
                {
                    using var cmdInsert = conn.CreateCommand();
                    cmdInsert.Transaction = tx;
                    cmdInsert.CommandText = @"
                        INSERT INTO AccTrn (TrnKy, AccKy, LiNo, Amt, PmtModeKy, EntUsrKy)
                        VALUES (@TrnKy, @AccKy, 1, @Amt, @PmtModeKy, @EntUsrKy)";
                    cmdInsert.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                    cmdInsert.Parameters.Add(new SqlParameter("@AccKy", dto.AccKy));
                    cmdInsert.Parameters.Add(new SqlParameter("@Amt", -1 * grossTotal));
                    cmdInsert.Parameters.Add(new SqlParameter("@PmtModeKy", pmtModeKy));
                    cmdInsert.Parameters.Add(new SqlParameter("@EntUsrKy", userKey));
                    await cmdInsert.ExecuteNonQueryAsync();
                }

                // 2) Sales credit (line 2)
                if (accTrn2.HasValue)
                {
                    using var cmdUpdate2 = conn.CreateCommand();
                    cmdUpdate2.Transaction = tx;
                    cmdUpdate2.CommandText = @"
                        UPDATE AccTrn
                        SET Amt = @Amt, AccKy = @AccKy, Status = 'U', PmtModeKy = @PmtModeKy
                        WHERE AccTrnKy = @AccTrnKy";
                    cmdUpdate2.Parameters.Add(new SqlParameter("@Amt", (totalInvAmt - totalDisAmt)));
                    cmdUpdate2.Parameters.Add(new SqlParameter("@AccKy", salesAccKy));
                    cmdUpdate2.Parameters.Add(new SqlParameter("@PmtModeKy", pmtModeKy));
                    cmdUpdate2.Parameters.Add(new SqlParameter("@AccTrnKy", accTrn2.Value));
                    await cmdUpdate2.ExecuteNonQueryAsync();
                }
                else
                {
                    using var cmdInsert2 = conn.CreateCommand();
                    cmdInsert2.Transaction = tx;
                    cmdInsert2.CommandText = @"
                        INSERT INTO AccTrn (TrnKy, AccKy, LiNo, Amt, PmtModeKy, EntUsrKy)
                        VALUES (@TrnKy, @AccKy, 2, @Amt, @PmtModeKy, @EntUsrKy)";
                    cmdInsert2.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                    cmdInsert2.Parameters.Add(new SqlParameter("@AccKy", salesAccKy));
                    cmdInsert2.Parameters.Add(new SqlParameter("@Amt", (totalInvAmt - totalDisAmt)));
                    cmdInsert2.Parameters.Add(new SqlParameter("@PmtModeKy", pmtModeKy));
                    cmdInsert2.Parameters.Add(new SqlParameter("@EntUsrKy", userKey));
                    await cmdInsert2.ExecuteNonQueryAsync();
                }

                // 6) Commit
                await _lookup.IncrementTranNumberLastAsync(_userContext.CompanyKey, "SLSRTN");
                await tx.CommitAsync();

                return trnKy;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}
