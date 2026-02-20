using Application.DTOs.GRN;
using Application.Interfaces;
using Infrastructure.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Infrastructure.Services
{
    public class GRNService
    {
        private readonly IDynamicDbContextFactory _factory;
        private readonly IUserRequestContext _userContext;
        private readonly IUserKeyService _userKeyService;
        private readonly CommonLookupService _lookup;

        public GRNService(IDynamicDbContextFactory factory, IUserRequestContext userContext, IUserKeyService userKeyService, CommonLookupService lookup)
        {
            _factory = factory;
            _userContext = userContext;
            _userKeyService = userKeyService;
            _lookup = lookup;
        }

        //--------------------------------------------------
        // GET GRN DETAILS
        //--------------------------------------------------
        public async Task<(bool success, string message, GRNResponseDTO? data)> GetGRNAsync(int trnNo)
        {
            using var db = await _factory.CreateDbContextAsync();

            int cKy = _userContext.CompanyKey;

            // ----------------------------------------
            // 1. Fetch TrnKy from vewTrnNo
            // ----------------------------------------
            var trnRecord = await db.vewTrnNo
                .Where(x => x.TrnNo == trnNo && x.OurCd == "GRN" && x.CKy == cKy)
                .FirstOrDefaultAsync();

            if (trnRecord == null)
                return (false, "No transaction found for this GRN number", null);

            int trnKy = trnRecord.TrnKy;

            // ----------------------------------------
            // 2. Fetch Header from vewGRNHdr
            // ----------------------------------------
            var hdr = await db.vewGRNHdr
                .Where(x => x.TrnKy == trnKy)
                .FirstOrDefaultAsync();

            if (hdr == null)
                return (false, "GRN header not found", null);

            var headerDto = new GRNHeaderDTO
            {
                TrnKy = hdr.TrnKy,
                TrnDt = hdr.TrnDt,
                PurAccKy = hdr.PurAccKy,
                PurAccCd = hdr.PurAccCd,
                PurAccNm = hdr.PurAccNm,
                AccNm = hdr.AccNm,
                AccTyp = hdr.AccTyp,
                AccKy = hdr.AccKy,
                AdrKy = hdr.Adrky,
                Code = hdr.Code,
                YurRef = hdr.YurRef,
                Des = hdr.Des,
                PmtTrmKy = hdr.PmtTrmKy
            };

            // ----------------------------------------
            // 3. Fetch GRN Details
            // ----------------------------------------
            var details = await db.vewGRNDtls
                .Where(x => x.TrnKy == trnKy)
                .Select(x => new GRNDetailDTO
                {
                    ItmKy = x.ItmKy,
                    ItmCd = x.ItmCd,
                    ItmNm = x.ItmNm,
                    Unit = x.Unit,
                    CosPri = x.CosPri,
                    TrnPri = x.TrnPri,
                    SlsPri = x.SlsPri,
                    Qty = x.Qty,
                    Amt = (x.TrnPri ?? 0) * (decimal)x.Qty,
                    ItmTrnKy = x.ItmTrnKy,
                    ExpirDt = x.ExpirDt,
                    BatchNo = x.BatchNo
                })
                .ToListAsync();

            // ----------------------------------------
            // 4. Calculate Total Amount
            // ----------------------------------------
            var response = new GRNResponseDTO
            {
                Header = headerDto,
                Details = details,
            };

            return (true, "Success", response);
        }


        //--------------------------------------------------
        // CREATE NEW GRN
        //--------------------------------------------------
        public async Task<(bool success, string message, int statusCode, int trnNo, int trnKy)>CreateGRNAsync(GRNCreateDTO dto)
        {
            const string ourCd = "GRN";

            using var db = await _factory.CreateDbContextAsync();
            using var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                var userKey = await _userKeyService.GetUserKeyAsync(
                    _userContext.UserId,
                    _userContext.CompanyKey
                );

                if (userKey == null)
                    throw new Exception("User key not found");

                short trnTypKy = await _lookup.GetTranTypeKeyAsync(ourCd);
                if (trnTypKy == 0)
                    return (false, "Transaction type not found", 400, 0, 0);

                if (!await IsValidTranDate(dto.trnDate))
                    return (false, "Invalid transaction date", 400, 0, 0);

                int trnNo = await GetNextTrnNoAsync(conn, tx, ourCd);

                // ---------------- Insert TrnMas ----------------
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                            INSERT INTO TrnMas
                            (TrnDt, TrnNo, TrnTypKy, OurCd, YurRef, AdrKy,
                             fApr, LocKy, Des, AccKy, PmtTrmKy, EntUsrKy, EntDtm)
                            VALUES
                            (@TrnDt, @TrnNo, @TrnTypKy, @OurCd, @YurRef, @AdrKy,
                             1, @LocKy, @Des, @AccKy, @PmtTrmKy, @UsrKy, GETDATE())
                        ";

                    cmd.Parameters.Add(new SqlParameter("@TrnDt", dto.trnDate));
                    cmd.Parameters.Add(new SqlParameter("@TrnNo", trnNo));
                    cmd.Parameters.Add(new SqlParameter("@TrnTypKy", trnTypKy));
                    cmd.Parameters.Add(new SqlParameter("@OurCd", ourCd));
                    cmd.Parameters.Add(new SqlParameter("@YurRef", (object?)dto.yurRef ?? DBNull.Value));
                    cmd.Parameters.Add(new SqlParameter("@AdrKy", dto.adrKy));
                    cmd.Parameters.Add(new SqlParameter("@LocKy", dto.locKy));
                    cmd.Parameters.Add(new SqlParameter("@Des", (object?)dto.des ?? DBNull.Value));
                    cmd.Parameters.Add(new SqlParameter("@AccKy", dto.accKy));
                    cmd.Parameters.Add(new SqlParameter("@PmtTrmKy", dto.pmtTrmKy));
                    cmd.Parameters.Add(new SqlParameter("@UsrKy", userKey));

                    await cmd.ExecuteNonQueryAsync();
                }

                // ---------------- Get TrnKy ----------------
                int trnKy;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                            SELECT TrnKy
                            FROM vewTrnNo
                            WHERE OurCd = @OurCd AND TrnNo = @TrnNo AND CKy = @CKy
                        ";

                    cmd.Parameters.Add(new SqlParameter("@OurCd", ourCd));
                    cmd.Parameters.Add(new SqlParameter("@TrnNo", trnNo));
                    cmd.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));

                    trnKy = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                decimal totalAmt = 0m;
                int liNo = 1;

                foreach (var itm in dto.items)
                {
                    if (itm.qty <= 0) continue;

                    int itmTrnKy;

                    // ---------------- Insert ItmTrn (NO OUTPUT) ----------------
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
                            INSERT INTO ItmTrn
                            (TrnKy, ItmKy, Qty, SlsPri, TrnPri, CosPri, LocKy, LiNo)
                            VALUES
                            (@TrnKy, @ItmKy, @Qty, @SlsPri, @TrnPri, @CosPri, @LocKy, @LiNo)
                        ";

                        cmd.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                        cmd.Parameters.Add(new SqlParameter("@ItmKy", itm.itemKey));
                        cmd.Parameters.Add(new SqlParameter("@Qty", itm.qty));
                        cmd.Parameters.Add(new SqlParameter("@SlsPri", itm.slsPri));
                        cmd.Parameters.Add(new SqlParameter("@TrnPri", itm.trnPri));
                        cmd.Parameters.Add(new SqlParameter("@CosPri", itm.cosPri));
                        cmd.Parameters.Add(new SqlParameter("@LocKy", dto.locKy));
                        cmd.Parameters.Add(new SqlParameter("@LiNo", liNo));

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // ---------------- Get ItmTrnKy safely ----------------
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
                            SELECT MAX(ItmTrnKy)
                            FROM ItmTrn
                            WHERE TrnKy = @TrnKy AND LiNo = @LiNo
                        ";

                        cmd.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                        cmd.Parameters.Add(new SqlParameter("@LiNo", liNo));

                        itmTrnKy = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    // ---------------- Insert ItmTrnCd ----------------
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
                            INSERT INTO ItmTrnCd (ItmTrnKy, ITCChar, ITCNo, ItcDt)
                            VALUES (@ItmTrnKy, @BatchNo, @Qty, @ExpDt)
                        ";

                        cmd.Parameters.Add(new SqlParameter("@ItmTrnKy", itmTrnKy));
                        cmd.Parameters.Add(new SqlParameter("@BatchNo", (object?)itm.batchNo ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@Qty", itm.qty));
                        cmd.Parameters.Add(new SqlParameter("@ExpDt", (object?)itm.expirDt ?? DBNull.Value));

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // ---------------- Upsert ItmBatch (SalePri SAFE) ----------------
                    await UpsertItmBatchAsync(
                        conn,
                        tx,
                        itm.itemKey,
                        itm.expirDt,
                        itm.cosPri,
                        (decimal)itm.qty,
                        itm.batchNo,
                        itm.slsPri
                    );

                    totalAmt += itm.trnPri * (decimal)itm.qty;
                    liNo++;
                }

                // ---------------- Accounting ----------------
                await PostAccTrnAsync(conn, tx, trnKy, 1, dto.accKy, -totalAmt, 1, userKey);

                if (dto.purAccKy.HasValue && dto.purAccKy.Value > 0)
                {
                    await PostAccTrnAsync(
                        conn, tx, trnKy, 2, dto.purAccKy.Value, totalAmt, 2, userKey
                    );
                }

                await tx.CommitAsync();
                return (true, "GRN created successfully", 201, trnNo, trnKy);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, ex.Message, 500, 0, 0);
            }
        }


        // Helper: get next TrnNo for OurCd and CKy
        private async Task<int> GetNextTrnNoAsync(System.Data.Common.DbConnection conn, System.Data.Common.DbTransaction tx, string ourCd)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                SELECT ISNULL(MAX(TrnNo), 0) + 1 FROM TrnMas
                WHERE OurCd = @OurCd AND CKy = @CKy;
            ";
            cmd.Parameters.Add(new SqlParameter("@OurCd", ourCd));
            cmd.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));
            var obj = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(obj);
        }

        // Helper: insert or update ItmBatch
        private async Task UpsertItmBatchAsync(DbConnection conn,DbTransaction tx,int itemKey,DateTime? expirDt,decimal cosPri,decimal qty,string? batchNo,decimal slsPri)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            cmd.CommandText = @"
                            IF EXISTS (
                                SELECT 1 FROM ItmBatch
                                WHERE ItmKy = @ItmKy
                                  AND ISNULL(BatchNo,'') = ISNULL(@BatchNo,'')
                                  AND ISNULL(ExpirDt,'1900-01-01') = ISNULL(@ExpirDt,'1900-01-01')
                            )
                            BEGIN
                                UPDATE ItmBatch
                                SET
                                    Qty = Qty + @Qty,
                                    CosPri = @CosPri,
                                    SalePri = @SalePri
                                WHERE ItmKy = @ItmKy
                                  AND ISNULL(BatchNo,'') = ISNULL(@BatchNo,'')
                                  AND ISNULL(ExpirDt,'1900-01-01') = ISNULL(@ExpirDt,'1900-01-01')
                            END
                            ELSE
                            BEGIN
                                INSERT INTO ItmBatch
                                (ItmKy, BatchNo, ExpirDt, Qty, CosPri, SalePri)
                                VALUES
                                (@ItmKy, @BatchNo, @ExpirDt, @Qty, @CosPri, @SalePri)
                            END
                        ";
            cmd.Parameters.Add(new SqlParameter("@ItmKy", itemKey));
            cmd.Parameters.Add(new SqlParameter("@BatchNo", (object?)batchNo ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@ExpirDt", (object?)expirDt ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@Qty", qty));
            cmd.Parameters.Add(new SqlParameter("@CosPri", cosPri));
            cmd.Parameters.Add(new SqlParameter("@SalePri", slsPri > 0 ? slsPri : cosPri));

            await cmd.ExecuteNonQueryAsync();
        }

        // Helper: post minimal accounting transaction lines
        // liNo is used to order lines. pmtModeKy intentionally left as 0.
        private async Task PostAccTrnAsync(System.Data.Common.DbConnection conn, System.Data.Common.DbTransaction tx, int trnKy, int liNo, int accKy, decimal amt, int pmtModeKy, int? userKey)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO AccTrn (TrnKy, LiNo, AccKy, PmtModeKy, Amt, EntUsrKy)
                VALUES (@TrnKy, @LiNo, @AccKy, @PmtModeKy, @Amt, @EntUsrKy);
            ";
            cmd.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
            cmd.Parameters.Add(new SqlParameter("@LiNo", liNo));
            cmd.Parameters.Add(new SqlParameter("@AccKy", accKy));
            cmd.Parameters.Add(new SqlParameter("@PmtModeKy", pmtModeKy));
            cmd.Parameters.Add(new SqlParameter("@Amt", amt));
            cmd.Parameters.Add(new SqlParameter("@EntUsrKy", userKey));
            await cmd.ExecuteNonQueryAsync();
        }

        // Transaction date rule: allow transactions within last 60 days up to today
        private Task<bool> IsValidTranDate(DateTime trnDt)
        {
            var today = DateTime.Today;
            var ok = trnDt.Date >= today.AddDays(-60) && trnDt.Date <= today;
            return Task.FromResult(ok);
        }


        //--------------------------------------------------
        // UPDATE EXISTING GRN
        //--------------------------------------------------
        public async Task<(bool success, string message, int statusCode)> UpdateGRNAsync(GRNUpdateDTO dto)
        {
            const string ourCd = "GRN";

            using var db = await _factory.CreateDbContextAsync();
            using var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                int trnKy;
                var userKey = await _userKeyService.GetUserKeyAsync(
                    _userContext.UserId,
                    _userContext.CompanyKey
                );

                if (userKey == null)
                    throw new Exception("User key not found.");

                // 1) Get TrnKy
                using (var cmdFind = conn.CreateCommand())
                {
                    cmdFind.Transaction = tx;
                    cmdFind.CommandText = @"
                SELECT TrnKy
                FROM vewTrnNo
                WHERE TrnNo = @TrnNo AND OurCd = @OurCd AND CKy = @CKy
            ";
                    cmdFind.Parameters.Add(new SqlParameter("@TrnNo", dto.trnNo));
                    cmdFind.Parameters.Add(new SqlParameter("@OurCd", ourCd));
                    cmdFind.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));

                    var trnKyObj = await cmdFind.ExecuteScalarAsync();
                    if (trnKyObj == null)
                        return (false, "GRN not found", 404);

                    trnKy = Convert.ToInt32(trnKyObj);
                }

                // 2) Validate date
                if (!await IsValidTranDate(dto.trnDate))
                    return (false, "You cannot alter transactions for this date", 400);

                // 3) Update TrnMas
                using (var cmdUpdHdr = conn.CreateCommand())
                {
                    cmdUpdHdr.Transaction = tx;
                    cmdUpdHdr.CommandText = @"
                UPDATE TrnMas
                SET TrnDt = @TrnDt,
                    AdrKy = @AdrKy,
                    LocKy = @LocKy,
                    Des = @Des,
                    PmtTrmKy = @PmtTrmKy,
                    AccKy = @AccKy,
                    YurRef = @YurRef,
                    EntUsrKy = @UsrKy,
                    EntDtm = GETDATE(),
                    fApr = 1,
                    Status = 'U'
                WHERE TrnKy = @TrnKy AND fInAct = 0
            ";

                    cmdUpdHdr.Parameters.Add(new SqlParameter("@TrnDt", dto.trnDate));
                    cmdUpdHdr.Parameters.Add(new SqlParameter("@AdrKy", dto.adrKy));
                    cmdUpdHdr.Parameters.Add(new SqlParameter("@LocKy", dto.locKy));
                    cmdUpdHdr.Parameters.Add(new SqlParameter("@Des", (object?)dto.des ?? DBNull.Value));
                    cmdUpdHdr.Parameters.Add(new SqlParameter("@PmtTrmKy", dto.pmtTrmKy));
                    cmdUpdHdr.Parameters.Add(new SqlParameter("@AccKy", dto.accKy));
                    cmdUpdHdr.Parameters.Add(new SqlParameter("@YurRef", (object?)dto.yurRef ?? DBNull.Value));
                    cmdUpdHdr.Parameters.Add(new SqlParameter("@UsrKy", userKey));
                    cmdUpdHdr.Parameters.Add(new SqlParameter("@TrnKy", trnKy));

                    await cmdUpdHdr.ExecuteNonQueryAsync();
                }

                // 4) Remove old items
                using (var cmdDel = conn.CreateCommand())
                {
                    cmdDel.Transaction = tx;
                    cmdDel.CommandText = @"
                DELETE FROM ItmTrnCd
                WHERE ItmTrnKy IN (SELECT ItmTrnKy FROM ItmTrn WHERE TrnKy = @TrnKy);

                DELETE FROM ItmTrn WHERE TrnKy = @TrnKy;
            ";
                    cmdDel.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                    await cmdDel.ExecuteNonQueryAsync();
                }

                // 5) Reinsert items
                int liNo = 1;
                decimal totalAmt = 0m;

                foreach (var itm in dto.items)
                {
                    if (itm.qty <= 0) continue;

                    // Insert ItmTrn (NO OUTPUT)
                    using (var cmdIns = conn.CreateCommand())
                    {
                        cmdIns.Transaction = tx;
                        cmdIns.CommandText = @"
                    INSERT INTO ItmTrn
                    (TrnKy, ItmKy, Qty, SlsPri, TrnPri, CosPri, LocKy, LiNo)
                    VALUES
                    (@TrnKy, @ItmKy, @Qty, @SlsPri, @TrnPri, @CosPri, @LocKy, @LiNo)
                ";

                        cmdIns.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                        cmdIns.Parameters.Add(new SqlParameter("@ItmKy", itm.itemKey));
                        cmdIns.Parameters.Add(new SqlParameter("@Qty", (decimal)itm.qty));
                        cmdIns.Parameters.Add(new SqlParameter("@SlsPri", itm.slsPri));
                        cmdIns.Parameters.Add(new SqlParameter("@TrnPri", itm.trnPri));
                        cmdIns.Parameters.Add(new SqlParameter("@CosPri", itm.cosPri));
                        cmdIns.Parameters.Add(new SqlParameter("@LocKy", dto.locKy));
                        cmdIns.Parameters.Add(new SqlParameter("@LiNo", liNo));

                        await cmdIns.ExecuteNonQueryAsync();
                    }

                    // Read ItmTrnKy safely
                    int itmTrnKy;
                    using (var cmdGet = conn.CreateCommand())
                    {
                        cmdGet.Transaction = tx;
                        cmdGet.CommandText = @"
                    SELECT ItmTrnKy
                    FROM ItmTrn
                    WHERE TrnKy = @TrnKy AND LiNo = @LiNo
                ";
                        cmdGet.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                        cmdGet.Parameters.Add(new SqlParameter("@LiNo", liNo));

                        itmTrnKy = Convert.ToInt32(await cmdGet.ExecuteScalarAsync());
                    }

                    // Insert ItmTrnCd
                    using (var cmdCd = conn.CreateCommand())
                    {
                        cmdCd.Transaction = tx;
                        cmdCd.CommandText = @"
                    INSERT INTO ItmTrnCd (ItmTrnKy, ITCChar, ITCNo, ItcDt)
                    VALUES (@ItmTrnKy, @BatchNo, @Qty, @ExpDt)
                ";

                        cmdCd.Parameters.Add(new SqlParameter("@ItmTrnKy", itmTrnKy));
                        cmdCd.Parameters.Add(new SqlParameter("@BatchNo", (object?)itm.batchNo ?? DBNull.Value));
                        cmdCd.Parameters.Add(new SqlParameter("@Qty", (decimal)itm.qty));
                        cmdCd.Parameters.Add(new SqlParameter("@ExpDt", (object?)itm.expirDt ?? DBNull.Value));

                        await cmdCd.ExecuteNonQueryAsync();
                    }

                    // Upsert batch
                    await UpsertItmBatchAsync(
                        conn,
                        tx,
                        itm.itemKey,
                        itm.expirDt,
                        itm.cosPri,
                        (decimal)itm.qty,
                        itm.batchNo,
                        itm.slsPri
                    );

                    totalAmt += itm.trnPri * (decimal)itm.qty;
                    liNo++;
                }

                // 6) Repost accounting
                using (var cmdDelAcc = conn.CreateCommand())
                {
                    cmdDelAcc.Transaction = tx;
                    cmdDelAcc.CommandText = "DELETE FROM AccTrn WHERE TrnKy = @TrnKy";
                    cmdDelAcc.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                    await cmdDelAcc.ExecuteNonQueryAsync();
                }

                await PostAccTrnAsync(conn, tx, trnKy, 1, dto.accKy, -totalAmt, 1, userKey);

                if (dto.purAccKy.HasValue && dto.purAccKy.Value > 0)
                {
                    await PostAccTrnAsync(
                        conn, tx, trnKy, 2, dto.purAccKy.Value, totalAmt, 2, userKey
                    );
                }

                await tx.CommitAsync();
                return (true, "GRN updated successfully", 200);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, "Error: " + ex.Message, 500);
            }
            finally
            {
                if (conn.State == System.Data.ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }


        //--------------------------------------------------
        // DELETE EXISTING GRN
        //--------------------------------------------------
        public async Task<(bool success, string message, int statusCode)> DeleteGRNAsync(int trnNo)
        {
            const string ourCd = "GRN";

            using var db = await _factory.CreateDbContextAsync();
            using var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                // 1) Find TrnKy for this GRN
                var cmdFind = conn.CreateCommand();
                cmdFind.Transaction = tx;
                cmdFind.CommandText = @"
                            SELECT TrnKy 
                            FROM vewTrnNo 
                            WHERE TrnNo = @TrnNo AND OurCd = @OurCd AND CKy = @CKy;
                        ";
                cmdFind.Parameters.Add(new SqlParameter("@TrnNo", trnNo));
                cmdFind.Parameters.Add(new SqlParameter("@OurCd", ourCd));
                cmdFind.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));

                var trnKyObj = await cmdFind.ExecuteScalarAsync();
                if (trnKyObj == null)
                    return (false, "GRN not found", 404);

                int trnKy = Convert.ToInt32(trnKyObj);

                // 2) Load item details from vewGRNDtls for qty rollback
                var cmdLoadItems = conn.CreateCommand();
                cmdLoadItems.Transaction = tx;
                cmdLoadItems.CommandText = @"
                            SELECT ItmKy, Qty, ExpirDt, CosPri 
                            FROM vewGRNDtls 
                            WHERE TrnKy = @TrnKy;
                        ";
                cmdLoadItems.Parameters.Add(new SqlParameter("@TrnKy", trnKy));

                var items = new List<(int itemKey, double qty, DateTime? expir, double cosPri)>();

                using (var reader = await cmdLoadItems.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        items.Add((
                            reader.GetInt32(0),
                            reader.GetDouble(1),
                            reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                            reader.IsDBNull(3) ? 0 : reader.GetDouble(3)
                        ));
                    }
                }

                // 3) Roll back quantities in ItmBatch (Qty = Qty - itemQty)
                foreach (var itm in items)
                {
                    var cmdUpdBatch = conn.CreateCommand();
                    cmdUpdBatch.Transaction = tx;
                    cmdUpdBatch.CommandText = @"
                            UPDATE ItmBatch 
                            SET Qty = Qty - @Qty
                            WHERE ItmKy = @ItmKy
                              AND ExpirDt = @ExpDt
                              AND CosPri = @CosPri;
                        ";

                    cmdUpdBatch.Parameters.Add(new SqlParameter("@Qty", itm.qty));
                    cmdUpdBatch.Parameters.Add(new SqlParameter("@ItmKy", itm.itemKey));
                    cmdUpdBatch.Parameters.Add(new SqlParameter("@ExpDt", (object?)itm.expir ?? DBNull.Value));
                    cmdUpdBatch.Parameters.Add(new SqlParameter("@CosPri", itm.cosPri));

                    await cmdUpdBatch.ExecuteNonQueryAsync();
                }

                // 4) Mark transaction inactive
                var cmdUpdMas = conn.CreateCommand();
                cmdUpdMas.Transaction = tx;
                cmdUpdMas.CommandText = @"
                            UPDATE TrnMas SET fInAct = 1
                            WHERE TrnKy = @TrnKy;
                        ";
                cmdUpdMas.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                await cmdUpdMas.ExecuteNonQueryAsync();

                // 5) Delete ItmTrn rows
                var cmdDelItm = conn.CreateCommand();
                cmdDelItm.Transaction = tx;
                cmdDelItm.CommandText = @"
                            DELETE FROM ItmTrn WHERE TrnKy = @TrnKy;
                        ";
                cmdDelItm.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                await cmdDelItm.ExecuteNonQueryAsync();

                // 6) Delete AccTrn rows
                var cmdDelAcc = conn.CreateCommand();
                cmdDelAcc.Transaction = tx;
                cmdDelAcc.CommandText = @"
                            DELETE FROM AccTrn WHERE TrnKy = @TrnKy;
                        ";
                cmdDelAcc.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                await cmdDelAcc.ExecuteNonQueryAsync();

                await tx.CommitAsync();

                return (true, "GRN deleted successfully", 200);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, "Error deleting GRN: " + ex.Message, 500);
            }
            finally
            {
                if (conn.State == System.Data.ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }
    }
}
