using Application.DTOs.Customers;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Context;
using Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Net;
using System.Security.Cryptography;

public class CustomerService
{
    private readonly IDynamicDbContextFactory _factory;
    private readonly IUserRequestContext _userContext;
    private readonly CommonLookupService _lookup;
    private readonly IUserKeyService _userKeyService;
    private readonly IValidationService _validator;

    public CustomerService(
        IDynamicDbContextFactory factory, 
        IUserRequestContext userContext, 
        CommonLookupService lookup,
        IUserKeyService userKeyService,
        IValidationService validator)
    {
        _factory = factory;
        _userContext = userContext;
        _lookup = lookup;
        _userKeyService = userKeyService;
        _validator = validator;
    }

    // Get all active customers
    public async Task<List<CustomerDto>> GetCustomersAsync()
    {
        using var db = await _factory.CreateDbContextAsync();

        var result = await (
            from c in db.Customers
            join a in db.Addresses on c.AdrKy equals a.AdrKy into adr
            from a in adr.DefaultIfEmpty() // LEFT JOIN
            where c.fInAct == false
            select new CustomerDto
            {
                AccTyp = c.AccTyp,
                AccNm = c.AccNm,
                AccKy = c.AccKy,
                AdrNm = c.AdrNm,
                AdrCd = c.AdrCd,
                AdrKy = c.AdrKy,
                NIC = c.NIC,
                Address = c.Address,
                Town = c.Town,
                City = c.City,
                TP1 = c.TP1,

                GPSLoc = a.GPSLoc
            }
        ).ToListAsync();

        return result;
    }



    // Add new customer address
    public async Task<int> AddCustomerAsync(AddCustomerAddressDto dto)
    {
        Console.WriteLine("Function started for adding customer address..............");

        string ourCd = dto.ourCd ?? "CUS";

        // Email validation
        if (!string.IsNullOrWhiteSpace(dto.EMail))
        {
            var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(dto.EMail, pattern))
                throw new Exception("Invalid email format");
        }

        // Mobile validation helper
        bool IsValidMobile(string number)
        {
            if (string.IsNullOrWhiteSpace(number))
                return true;
            return number.Length == 10 && number.All(char.IsDigit);
        }

        if (!IsValidMobile(dto.TP1))
            throw new Exception("TP1 must contain exactly 10 digits");

        if (!IsValidMobile(dto.TP2))
            throw new Exception("TP2 must contain exactly 10 digits");

        if (await _validator.IsExistAdrNm(dto.AdrNm))
            throw new Exception("Address name already exists");

        using var db = await _factory.CreateDbContextAsync();
        using var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        using var tx = conn.BeginTransaction();
        Console.WriteLine("Transaction started for adding customer address..............");
        try
        {
            string adrCd = await GenerateUniqueAdrCdAsync(conn, tx, dto.AdrNm);

            var userKey = await _userKeyService.GetUserKeyAsync(_userContext.UserId, _userContext.CompanyKey);
            if (userKey == null)
                throw new Exception("User key not found for this user");

            // -------------------------------------------------------------
            // 1) Insert into AdrMas
            // -------------------------------------------------------------
            using var cmd1 = conn.CreateCommand();
            cmd1.Transaction = tx;

            cmd1.CommandText = @"
            INSERT INTO Address
            (
                CKy, AdrCd, fInAct, AdrNm, FstNm, MidNm, LstNm,
                Address, TP1, TP2, EMail,
                EntUsrKy, EntDtm, GPSLoc
            )
            OUTPUT INSERTED.AdrKy
            VALUES
            (
                @CKy, @AdrCd, 0, @AdrNm, @FstNm, @MidNm, @LstNm,
                @Address, @TP1, @TP2, @EMail,
                @EntUsrKy, @EntDtm, @GPSLoc
            );";

            cmd1.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));
            cmd1.Parameters.Add(new SqlParameter("@AdrCd", adrCd));
            cmd1.Parameters.Add(new SqlParameter("@AdrNm", dto.AdrNm ?? (object)DBNull.Value));
            cmd1.Parameters.Add(new SqlParameter("@FstNm", dto.FstNm ?? (object)DBNull.Value));
            cmd1.Parameters.Add(new SqlParameter("@MidNm", dto.MidNm ?? (object)DBNull.Value));
            cmd1.Parameters.Add(new SqlParameter("@LstNm", dto.LstNm ?? (object)DBNull.Value));
            cmd1.Parameters.Add(new SqlParameter("@Address", dto.Address ?? (object)DBNull.Value));
            cmd1.Parameters.Add(new SqlParameter("@TP1", dto.TP1 ?? (object)DBNull.Value));
            cmd1.Parameters.Add(new SqlParameter("@TP2", dto.TP2 ?? (object)DBNull.Value));
            cmd1.Parameters.Add(new SqlParameter("@EMail", dto.EMail ?? (object)DBNull.Value));
            cmd1.Parameters.Add(new SqlParameter("@EntUsrKy", userKey));
            cmd1.Parameters.Add(new SqlParameter("@EntDtm", DateTime.Now));
            cmd1.Parameters.Add(new SqlParameter("@GPSLoc", dto.GPSLoc));

            var adrKyObj = await cmd1.ExecuteScalarAsync();
            int adrKy = Convert.ToInt32(adrKyObj);


            // -------------------------------------------------------------
            // 2) Insert into AddressCdRel
            // -------------------------------------------------------------
            short cdKey = await _lookup.GetAccountTypeKeyAsync(ourCd);

            using var cmd2 = conn.CreateCommand();
            cmd2.Transaction = tx;

            cmd2.CommandText = @"
            INSERT INTO AddressCdRel
            (AdrKy, CdKy, Lino, fApr, EntUsrKy)
            VALUES
            (@AdrKy, @CdKy, 0, 1, @EntUsrKy);";

            cmd2.Parameters.Add(new SqlParameter("@AdrKy", adrKy));
            cmd2.Parameters.Add(new SqlParameter("@CdKy", cdKey));
            cmd2.Parameters.Add(new SqlParameter("@EntUsrKy", userKey));

            await cmd2.ExecuteNonQueryAsync();

            tx.Commit();

            return adrKy;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private async Task<string> GenerateUniqueAdrCdAsync(DbConnection conn, DbTransaction tx, string adrNm)
    {
        if (string.IsNullOrWhiteSpace(adrNm))
            adrNm = "ADR";

        adrNm = adrNm.Trim();

        const int totalMax = 14;
        const int randomDigits = 2;

        int maxBaseLength = totalMax - randomDigits;

        if (adrNm.Length > maxBaseLength)
            adrNm = adrNm.Substring(0, maxBaseLength);

        while (true)
        {
            int random = RandomNumberGenerator.GetInt32(10, 99);
            string adrCd = $"{adrNm}{random}";

            // Check existence
            using var checkCmd = conn.CreateCommand();
            checkCmd.Transaction = tx;
            checkCmd.CommandText = "SELECT COUNT(*) FROM Address WHERE AdrCd = @AdrCd AND CKy = @CKy";

            checkCmd.Parameters.Add(new SqlParameter("@AdrCd", adrCd));
            checkCmd.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));

            var countObj = await checkCmd.ExecuteScalarAsync();
            int count = Convert.ToInt32(countObj);

            if (count == 0)
                return adrCd;
        }
    }

    public async Task UpdateCustomerAsync(UpdateCustomerAddressDto dto)
    {
        Console.WriteLine("Function started for updating customer address..............");

        // Email validation
        if (!string.IsNullOrWhiteSpace(dto.EMail))
        {
            var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(dto.EMail, pattern))
                throw new Exception("Invalid email format");
        }

        bool IsValidMobile(string number)
        {
            if (string.IsNullOrWhiteSpace(number))
                return true;
            return number.Length == 10 && number.All(char.IsDigit);
        }

        if (!IsValidMobile(dto.TP1))
            throw new Exception("TP1 must contain exactly 10 digits");

        if (!IsValidMobile(dto.TP2))
            throw new Exception("TP2 must contain exactly 10 digits");

        using var db = await _factory.CreateDbContextAsync();
        using var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        using var tx = conn.BeginTransaction();
        Console.WriteLine("Transaction started for updating customer address..............");

        try
        {
            var userKey = await _userKeyService.GetUserKeyAsync(
                _userContext.UserId,
                _userContext.CompanyKey
            );

            if (userKey == null)
                throw new Exception("User key not found for this user");

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            cmd.CommandText = @"
                        UPDATE Address
                        SET
                            AdrNm   = @AdrNm,
                            FstNm   = @FstNm,
                            MidNm   = @MidNm,
                            LstNm   = @LstNm,
                            Address = @Address,
                            TP1     = @TP1,
                            TP2     = @TP2,
                            EMail   = @EMail,
                            GPSLoc  = @GPSLoc,
                            EntUsrKy = @EntUsrKy,
                            EntDtm   = @EntDtm
                        WHERE AdrKy = @AdrKy AND CKy = @CKy;";

            cmd.Parameters.Add(new SqlParameter("@AdrKy", dto.AdrKy));
            cmd.Parameters.Add(new SqlParameter("@CKy", _userContext.CompanyKey));
            cmd.Parameters.Add(new SqlParameter("@AdrNm", dto.AdrNm ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@FstNm", dto.FstNm ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@MidNm", dto.MidNm ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@LstNm", dto.LstNm ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@Address", dto.Address ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@TP1", dto.TP1 ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@TP2", dto.TP2 ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@EMail", dto.EMail ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@GPSLoc", dto.GPSLoc ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@EntUsrKy", userKey));
            cmd.Parameters.Add(new SqlParameter("@EntDtm", DateTime.Now));

            int rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0)
                throw new Exception("Customer address not found");

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

}
