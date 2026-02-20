using Application.DTOs.GRE;
using Application.Interfaces;
using Infrastructure.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services
{
    public class GREService
    {
        private readonly IDynamicDbContextFactory _factory;
        private readonly IUserRequestContext _userContext;
        private readonly CommonLookupService _lookup;
        private readonly IUserKeyService _userKeyService;

        public GREService(IDynamicDbContextFactory factory, IUserRequestContext userContext, CommonLookupService lookup, IUserKeyService userKeyService)
        {
            _factory = factory;
            _userContext = userContext;
            _lookup = lookup;
            _userKeyService = userKeyService;
        }

        //--------------------------------------------------
        // GET GRE DETAILS
        //--------------------------------------------------
        public async Task<(bool success, string message, int statusCode, GREResponseDTO data)> GetGREAsync(int trnNo)
        {
            using var db = await _factory.CreateDbContextAsync();
            using var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            try
            {
                // First get TrnKy from vewTrnNo for this specific OurCd
                var cmdFind = conn.CreateCommand();
                cmdFind.CommandText = @"
                    SELECT TrnKy FROM vewTrnNo
                    WHERE TrnNo = @TrnNo AND OurCd = 'PURRTN' AND CKy = @CKy;
                ";
                cmdFind.Parameters.Add(new SqlParameter("@TrnNo", trnNo));
                cmdFind.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));

                var trnKyObj = await cmdFind.ExecuteScalarAsync();
                if (trnKyObj == null)
                {
                    return (false, "Transaction not found", 404, null);
                }

                int trnKy = Convert.ToInt32(trnKyObj);

                // Load header from vewPURRTNHdr
                GREHeaderDTO header = null;
                var cmdHdr = conn.CreateCommand();
                cmdHdr.CommandText = @"
                    SELECT TrnKy, TrnDt, Code, YurRef, AdrNm, AdrCd, LocKy, AdrKy, AccKy,
                           PurAccKy, PurAccCd, AccTrnKy, AccCd, AccNm, PmtTrmKy, DocNo
                    FROM vewPURRTNHdr
                    WHERE TrnKy = @TrnKy;
                ";
                cmdHdr.Parameters.Add(new SqlParameter("@TrnKy", trnKy));

                using (var rdr = await cmdHdr.ExecuteReaderAsync())
                {
                    if (await rdr.ReadAsync())
                    {
                        header = new GREHeaderDTO
                        {
                            TrnKy = rdr.GetInt32(0),
                            TrnDt = rdr.GetDateTime(1),
                            Code = rdr.GetString(2),
                            YurRef = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                            AdrNm = rdr.IsDBNull(4) ? null : rdr.GetString(4),
                            AdrCd = rdr.IsDBNull(5) ? null : rdr.GetString(5),
                            LocKy = rdr.GetInt16(6),
                            AdrKy = rdr.GetInt32(7),
                            AccKy = rdr.GetInt32(8),
                            PurAccKy = rdr.GetInt32(9),
                            PurAccCd = rdr.IsDBNull(10) ? null : rdr.GetString(10),
                            AccTrnKy = rdr.GetInt32(11),
                            AccCd = rdr.IsDBNull(12) ? null : rdr.GetString(12),
                            AccNm = rdr.IsDBNull(13) ? null : rdr.GetString(13),
                            PmtTrmKy = rdr.GetInt16(14),
                            DocNo = rdr.IsDBNull(15) ? null : rdr.GetString(15)
                        };
                    }
                }

                if (header == null)
                {
                    return (false, "Header not found", 404, null);
                }

                // Load detail rows
                var details = new List<GREDetailDTO>();
                var cmdDtl = conn.CreateCommand();
                cmdDtl.CommandText = @"
                    SELECT ItmKy, ItmCd, ItmNm, Unit, CosPri, TrnPri, SlsPri, Amt1, Amt2, ItmTrnKy, Qty
                    FROM vewPURRTNDtls
                    WHERE TrnKy = @TrnKy
                    ORDER BY ItmTrnKy;
                ";
                cmdDtl.Parameters.Add(new SqlParameter("@TrnKy", trnKy));

                using (var rdr = await cmdDtl.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        details.Add(new GREDetailDTO
                        {
                            ItmKy = rdr.GetInt32(0),
                            ItmCd = rdr.GetString(1),
                            ItmNm = rdr.IsDBNull(2) ? null : rdr.GetString(2),
                            Unit = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                            CosPri = rdr.IsDBNull(4) ? (decimal?)null : rdr.GetDecimal(4),
                            TrnPri = rdr.IsDBNull(5) ? (decimal?)null : rdr.GetDecimal(5),
                            SlsPri = rdr.GetDecimal(6),
                            Amt1 = rdr.GetDecimal(7),
                            Amt2 = rdr.GetDecimal(8),
                            ItmTrnKy = rdr.GetInt32(9),
                            Qty = rdr.GetDouble(10)
                        });
                    }
                }

                var response = new GREResponseDTO
                {
                    Header = header,
                    Details = details,
                };

                return (true, "Success", 200, response);
            }
            catch (Exception ex)
            {
                return (false, "Error " + ex.Message, 500, null);
            }
            finally
            {
                if (conn.State == System.Data.ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }

        //--------------------------------------------------
        // ADD NEW GRE
        //--------------------------------------------------
        public async Task<(bool success, string message, int statusCode)> AddNewGREAsync(AddGRERequestDTO dto)
        {
            using var db = await _factory.CreateDbContextAsync();
            using var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // 1. Generate new TrnNo
                int trnTypKy = await _lookup.GetTrnTypKyAsync("GRN2");
                var userKey = await _userKeyService.GetUserKeyAsync(_userContext.UserId, _userContext.CompanyKey);
                if (userKey == null)
                    throw new Exception("User key not found");
                short pmtTrmKy = await _lookup.GetPaymentTermKeyAsync(dto.PmtTrm);


                var cmdTrnNo = conn.CreateCommand();
                cmdTrnNo.Transaction = transaction;
                cmdTrnNo.CommandText = @"
                            SELECT MAX(TrnNo) + 1 FROM TrnMas WHERE OurCd = 'PURRTN' AND CKy = @CKy
                        ";
                cmdTrnNo.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));

                var trnNoObj = await cmdTrnNo.ExecuteScalarAsync();
                int trnNo = trnNoObj == DBNull.Value || trnNoObj == null ? 1 : Convert.ToInt32(trnNoObj);

                // 2. Insert into TrnMas
                var cmdInsertTrn = conn.CreateCommand();
                cmdInsertTrn.Transaction = transaction;

                string yurRefPart = string.IsNullOrWhiteSpace(dto.YurRef) ? "NULL" : $"@YurRef";

                cmdInsertTrn.CommandText = $@"
                            INSERT INTO TrnMas
                            (TrnDt, TrnNo, TrnTypKy, OurCd, DocNo, YurRef, AdrKy, AccKy,
                             fApr, LocKy, PmtTrmKy, EntUsrKy, EntDtm)
                            VALUES
                            (@TrnDt, @TrnNo, @TrnTypKy, 'PURRTN', @DocNo, {yurRefPart},
                             @AdrKy, @AccKy, 1, @LocKy, @PmtTrmKy, @UsrKy, GETDATE())
                        ";

                cmdInsertTrn.Parameters.Add(new SqlParameter("@TrnDt", dto.TrnDt));
                cmdInsertTrn.Parameters.Add(new SqlParameter("@TrnNo", trnNo));
                cmdInsertTrn.Parameters.Add(new SqlParameter("@TrnTypKy", trnTypKy)); // You may compute this separately
                cmdInsertTrn.Parameters.Add(new SqlParameter("@DocNo", dto.DocNo));

                if (!string.IsNullOrWhiteSpace(dto.YurRef))
                    cmdInsertTrn.Parameters.Add(new SqlParameter("@YurRef", dto.YurRef));

                cmdInsertTrn.Parameters.Add(new SqlParameter("@AdrKy", dto.AdrKy));
                cmdInsertTrn.Parameters.Add(new SqlParameter("@AccKy", dto.AccKy));
                cmdInsertTrn.Parameters.Add(new SqlParameter("@LocKy", dto.LocKy));
                cmdInsertTrn.Parameters.Add(new SqlParameter("@PmtTrmKy", pmtTrmKy));
                cmdInsertTrn.Parameters.Add(new SqlParameter("@UsrKy", userKey));

                await cmdInsertTrn.ExecuteNonQueryAsync();

                // 3. Get new TrnKy
                var cmdGetTrnKy = conn.CreateCommand();
                cmdGetTrnKy.Transaction = transaction;
                cmdGetTrnKy.CommandText = @"
                            SELECT TrnKy FROM vewTrnNo 
                            WHERE TrnNo = @TrnNo AND OurCd = 'PURRTN' AND CKy = @CKy
                        ";
                cmdGetTrnKy.Parameters.Add(new SqlParameter("@TrnNo", trnNo));
                cmdGetTrnKy.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));

                var trnKyObj = await cmdGetTrnKy.ExecuteScalarAsync();
                if (trnKyObj == null)
                {
                    await transaction.RollbackAsync();
                    return (false, "Failed to retrieve TrnKy", 500);
                }

                int trnKy = Convert.ToInt32(trnKyObj);

                // 4. Insert detail items
                foreach (var item in dto.Items)
                {
                    var cmdItm = conn.CreateCommand();
                    cmdItm.Transaction = transaction;

                    cmdItm.CommandText = @"
                            INSERT INTO ItmTrn
                            (TrnKy, LocKy, ItmKy, Qty, SlsPri, CosPri, TrnPri, Amt1, Amt2, LiNo)
                            VALUES
                            (@TrnKy, @LocKy, @ItmKy, @Qty, @SlsPri, @CosPri, @TrnPri, @Amt1, @Amt2, @LiNo)
                        ";

                    cmdItm.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                    cmdItm.Parameters.Add(new SqlParameter("@LocKy", dto.LocKy));
                    cmdItm.Parameters.Add(new SqlParameter("@ItmKy", item.ItemKey));
                    cmdItm.Parameters.Add(new SqlParameter("@Qty", -1 * item.Qty));
                    cmdItm.Parameters.Add(new SqlParameter("@SlsPri", item.SlsPri));
                    cmdItm.Parameters.Add(new SqlParameter("@CosPri", item.CosPri));
                    cmdItm.Parameters.Add(new SqlParameter("@TrnPri", item.TrnPri));
                    cmdItm.Parameters.Add(new SqlParameter("@Amt1", item.GSTAmt));
                    cmdItm.Parameters.Add(new SqlParameter("@Amt2", item.NSLAmt));
                    cmdItm.Parameters.Add(new SqlParameter("@LiNo", dto.Items.IndexOf(item) + 1));

                    await cmdItm.ExecuteNonQueryAsync();
                }

                // 5. Commit
                await transaction.CommitAsync();

                return (true, "GRE Added Successfully", 201);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "Error: " + ex.Message, 500);
            }
        }


        //--------------------------------------------------
        // UPDATE EXIST GRE
        //--------------------------------------------------
        public async Task<(bool success, string message, int statusCode)> UpdateGREAsync(UpdateGRERequestDTO dto)
        {
            using var db = await _factory.CreateDbContextAsync();
            using var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            using var tx = await conn.BeginTransactionAsync();

            try
            {
                int companyKey = _userContext.CompanyKey;
                var userKey = await _userKeyService.GetUserKeyAsync(_userContext.UserId, _userContext.CompanyKey);
                if (userKey == null)
                    throw new Exception("User key not found");
                short pmtTrmKy = await _lookup.GetPaymentTermKeyAsync(dto.PmtTrm);

                // 1) Find TrnKy
                var cmdFind = conn.CreateCommand();
                cmdFind.Transaction = tx;
                cmdFind.CommandText = @"
                            SELECT TrnKy 
                            FROM vewTrnNo 
                            WHERE TrnNo = @TrnNo 
                              AND OurCd = 'PURRTN'
                              AND CKy = @CKy";
                cmdFind.Parameters.Add(new SqlParameter("@TrnNo", dto.TrnNo));
                cmdFind.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));

                var trnKyObj = await cmdFind.ExecuteScalarAsync();
                if (trnKyObj == null)
                {
                    await tx.RollbackAsync();
                    return (false, "Invalid GRE number", 404);
                }

                int trnKy = Convert.ToInt32(trnKyObj);

                // 2) Update TrnMas
                var cmdUpdateMas = conn.CreateCommand();
                cmdUpdateMas.Transaction = tx;

                string yurRefPart = string.IsNullOrWhiteSpace(dto.YurRef)
                    ? "NULL"
                    : "@YurRef";

                cmdUpdateMas.CommandText = $@"
                            UPDATE TrnMas
                            SET TrnDt     = @TrnDt,
                                DocNo     = @DocNo,
                                {(string.IsNullOrWhiteSpace(dto.YurRef) ? "" : "YurRef = @YurRef,")}
                                AdrKy     = @AdrKy,
                                AccKy     = @AccKy,
                                PmtTrmKy  = @PmtTrmKy,
                                Status    = 'U',
                                EntUsrKy  = @UsrKy,
                                EntDtm    = GETDATE()
                            WHERE TrnKy = @TrnKy";

                cmdUpdateMas.Parameters.Add(new SqlParameter("@TrnDt", dto.TrnDt));
                cmdUpdateMas.Parameters.Add(new SqlParameter("@DocNo", dto.DocNo));
                if (!string.IsNullOrWhiteSpace(dto.YurRef))
                    cmdUpdateMas.Parameters.Add(new SqlParameter("@YurRef", dto.YurRef));
                cmdUpdateMas.Parameters.Add(new SqlParameter("@AdrKy", dto.AdrKy));
                cmdUpdateMas.Parameters.Add(new SqlParameter("@AccKy", dto.AccKy));
                cmdUpdateMas.Parameters.Add(new SqlParameter("@PmtTrmKy", pmtTrmKy));
                cmdUpdateMas.Parameters.Add(new SqlParameter("@UsrKy", userKey));
                cmdUpdateMas.Parameters.Add(new SqlParameter("@TrnKy", trnKy));

                await cmdUpdateMas.ExecuteNonQueryAsync();

                // 3) Loop through item rows
                int liNo = 1;
                foreach (var item in dto.Items)
                {
                    if (!item.IsValid)
                        continue;

                    if (item.ToDelete && item.ItmTrnKy.HasValue)
                    {
                        var cmdDel = conn.CreateCommand();
                        cmdDel.Transaction = tx;
                        cmdDel.CommandText = "DELETE FROM ItmTrn WHERE ItmTrnKy=@Ky";
                        cmdDel.Parameters.Add(new SqlParameter("@Ky", item.ItmTrnKy.Value));
                        await cmdDel.ExecuteNonQueryAsync();
                        continue;
                    }

                    // Load item prices (from vewItmMas)
                    decimal costPri = 0;
                    decimal salePri = 0;

                    var cmdLoadItem = conn.CreateCommand();
                    cmdLoadItem.Transaction = tx;
                    cmdLoadItem.CommandText = @"
                                SELECT SlsPri, CosPri 
                                FROM vewItmMas
                                WHERE ItmKy = @ItmKy";
                    cmdLoadItem.Parameters.Add(new SqlParameter("@ItmKy", item.ItemKey));

                    using (var reader = await cmdLoadItem.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            salePri = reader.GetDecimal(0);
                            costPri = reader.GetDecimal(1);
                        }
                    }

                    if (!item.ItmTrnKy.HasValue)
                    {
                        // Insert new item line
                        var cmdInsert = conn.CreateCommand();
                        cmdInsert.Transaction = tx;

                        cmdInsert.CommandText = @"
                                INSERT INTO ItmTrn
                                (TrnKy, LocKy, ItmKy, Qty, SlsPri, CosPri, TrnPri, Amt1, Amt2, LiNo)
                                VALUES
                                (@TrnKy, @LocKy, @ItmKy, @Qty, @SlsPri, @CosPri, @TrnPri, @GST, @NSL, @LiNo)";

                        cmdInsert.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                        cmdInsert.Parameters.Add(new SqlParameter("@LocKy", dto.LocKy));
                        cmdInsert.Parameters.Add(new SqlParameter("@ItmKy", item.ItemKey));
                        cmdInsert.Parameters.Add(new SqlParameter("@Qty", -1 * item.Qty));
                        cmdInsert.Parameters.Add(new SqlParameter("@SlsPri", salePri));
                        cmdInsert.Parameters.Add(new SqlParameter("@CosPri", costPri));
                        cmdInsert.Parameters.Add(new SqlParameter("@TrnPri", item.TrnPri));
                        cmdInsert.Parameters.Add(new SqlParameter("@GST", item.GSTAmt));
                        cmdInsert.Parameters.Add(new SqlParameter("@NSL", item.NSLAmt));
                        cmdInsert.Parameters.Add(new SqlParameter("@LiNo", liNo));

                        await cmdInsert.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // Update existing item line
                        var cmdUpdate = conn.CreateCommand();
                        cmdUpdate.Transaction = tx;

                        cmdUpdate.CommandText = @"
                                UPDATE ItmTrn
                                SET ItmKy=@ItmKy,
                                    Qty=@Qty,
                                    SlsPri=@SlsPri,
                                    CosPri=@CosPri,
                                    TrnPri=@TrnPri,
                                    Amt1=@GST,
                                    Amt2=@NSL,
                                    LiNo=@LiNo
                                WHERE ItmTrnKy=@Ky";

                        cmdUpdate.Parameters.Add(new SqlParameter("@Ky", item.ItmTrnKy.Value));
                        cmdUpdate.Parameters.Add(new SqlParameter("@ItmKy", item.ItemKey));
                        cmdUpdate.Parameters.Add(new SqlParameter("@Qty", -1 * item.Qty));
                        cmdUpdate.Parameters.Add(new SqlParameter("@SlsPri", salePri));
                        cmdUpdate.Parameters.Add(new SqlParameter("@CosPri", costPri));
                        cmdUpdate.Parameters.Add(new SqlParameter("@TrnPri", item.TrnPri));
                        cmdUpdate.Parameters.Add(new SqlParameter("@GST", item.GSTAmt));
                        cmdUpdate.Parameters.Add(new SqlParameter("@NSL", item.NSLAmt));
                        cmdUpdate.Parameters.Add(new SqlParameter("@LiNo", liNo));

                        await cmdUpdate.ExecuteNonQueryAsync();
                    }

                    liNo++;
                }

                // 4) Recompute totals and update accounting entries
                decimal totalAmt = 0;
                decimal totalGST = 0;
                decimal totalNSL = 0;

                var cmdTotals = conn.CreateCommand();
                cmdTotals.Transaction = tx;

                cmdTotals.CommandText = @"
                        SELECT Qty, TrnPri, Amt1, Amt2 
                        FROM vewPURRTNDtls 
                        WHERE TrnKy=@TrnKy";

                cmdTotals.Parameters.Add(new SqlParameter("@TrnKy", trnKy));

                using (var reader = await cmdTotals.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        double qty = reader.GetDouble(0);
                        decimal pri = reader.GetDecimal(1);
                        decimal amt1 = reader.GetDecimal(2);
                        decimal amt2 = reader.GetDecimal(3);

                        totalAmt += (pri * (decimal)(-qty)) + amt1 + amt2;
                        totalGST += amt1;
                        totalNSL += amt2;
                    }
                }

                // Update AccTrn Line 1 (Supplier)
                var cmdAcc1 = conn.CreateCommand();
                cmdAcc1.Transaction = tx;
                cmdAcc1.CommandText = @"
                        UPDATE AccTrn
                        SET Amt=@Amt, Status='U', AccKy=@AccKy
                        WHERE TrnKy=@TrnKy AND LiNo=1";

                cmdAcc1.Parameters.Add(new SqlParameter("@Amt", totalAmt));
                cmdAcc1.Parameters.Add(new SqlParameter("@AccKy", dto.AccKy));
                cmdAcc1.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                await cmdAcc1.ExecuteNonQueryAsync();

                // Update AccTrn Line 2 (Purchase Return Account)
                var cmdAcc2 = conn.CreateCommand();
                cmdAcc2.Transaction = tx;

                decimal purAmt = totalAmt - totalGST - totalNSL;

                cmdAcc2.CommandText = @"
                        UPDATE AccTrn
                        SET Amt=@Amt, Status='U', AccKy=@AccKy
                        WHERE TrnKy=@TrnKy AND LiNo=2";

                cmdAcc2.Parameters.Add(new SqlParameter("@Amt", -purAmt));
                cmdAcc2.Parameters.Add(new SqlParameter("@AccKy", dto.AccKy));
                cmdAcc2.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                await cmdAcc2.ExecuteNonQueryAsync();

                // Update AccTrn Line 3 (GST account)
                int gstAccKy = await _lookup.GetAmt1AccKyAsync("PURRTN", companyKey);

                var cmdAcc3 = conn.CreateCommand();
                cmdAcc3.Transaction = tx;

                cmdAcc3.CommandText = @"
                        UPDATE AccTrn
                        SET Amt=@Amt, Status='U', AccKy=@AccKy
                        WHERE TrnKy=@TrnKy AND LiNo=3";

                cmdAcc3.Parameters.Add(new SqlParameter("@Amt", -totalGST));
                cmdAcc3.Parameters.Add(new SqlParameter("@AccKy", gstAccKy));
                cmdAcc3.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                await cmdAcc3.ExecuteNonQueryAsync();

                await tx.CommitAsync();
                return (true, "GRE updated successfully", 200);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, "Error: " + ex.Message, 500);
            }
        }


        //--------------------------------------------------
        // DELETE EXIST GRE
        //--------------------------------------------------
        public async Task<(bool success, string message, int statusCode)> DeleteGreAsync(int trnNo)
        {
            using var db = await _factory.CreateDbContextAsync();
            using var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                // Step 1: Resolve TrnKy from vewTrnNo
                int trnKy = await _lookup.GetTrnKyByTrnNoAsync(trnNo);

                if (trnKy == 0)
                    return (false, "Invalid GRE transaction number", 404);

                // Step 2: Delete logic from VB
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;

                    // Mark TrnMas as inactive
                    cmd.CommandText = "UPDATE TrnMas SET fInAct = 1 WHERE TrnKy = @TrnKy";
                    cmd.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                    await cmd.ExecuteNonQueryAsync();
                }

                // Delete ItmTrn
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM ItmTrn WHERE TrnKy = @TrnKy";
                    cmd.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                    await cmd.ExecuteNonQueryAsync();
                }

                // Delete AccTrn
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM AccTrn WHERE TrnKy = @TrnKy";
                    cmd.Parameters.Add(new SqlParameter("@TrnKy", trnKy));
                    await cmd.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
                return (true, "GRE deleted", 200);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, "Error deleting GRE: " + ex.Message, 500);
            }
        }
    }
}
