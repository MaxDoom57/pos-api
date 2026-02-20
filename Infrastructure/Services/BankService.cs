using Application.DTOs.Bank;
using Application.DTOs.Invoice;
using Application.DTOs.Lookups;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services
{
    public class BankService
    {
        private readonly IDynamicDbContextFactory _factory;
        private readonly IUserRequestContext _userContext;
        private readonly IUserKeyService _userKeyService;

        public BankService(IDynamicDbContextFactory factory, IUserRequestContext userContext, IUserKeyService userKeyService)
        {
            _factory = factory;
            _userContext = userContext;
            _userKeyService = userKeyService;
        }

        // -----------------------------
        // GET : Get Banks
        // -----------------------------
        public async Task<List<BankDto>> GetBanksAsync()
        {
            using var db = await _factory.CreateDbContextAsync();

            return await db.BnkMas
                .AsNoTracking()
                .Where(x => !x.fInAct)
                .OrderBy(x => x.BnkNm)
                .Select(x => new BankDto
                {
                    BnkKy = x.BnkKy,
                    BnkCd = x.BnkCd,
                    BnkNm = x.BnkNm
                })
                .ToListAsync();
        }


        // -----------------------------
        // POST : Create Bank
        // -----------------------------
        public async Task CreateBankAsync(CreateBankDto request)
        {
            using var db = await _factory.CreateDbContextAsync();
            var userKey = await _userKeyService.GetUserKeyAsync(_userContext.UserId, _userContext.CompanyKey);
            if (userKey == null)
                throw new Exception("User key not found for this user");

            if (string.IsNullOrWhiteSpace(request.BnkCd))
                throw new Exception("Bank code is required");

            if (string.IsNullOrWhiteSpace(request.BnkNm))
                throw new Exception("Bank name is required");

            bool codeExists = await db.BnkMas
                .AnyAsync(x => x.BnkCd == request.BnkCd && !x.fInAct);

            if (codeExists)
                throw new Exception("Bank code already exists");

            bool nameExists = await db.BnkMas
                .AnyAsync(x => x.BnkNm == request.BnkNm && !x.fInAct);

            if (nameExists)
                throw new Exception("Bank name already exists");

            var bank = new BnkMas
            {
                BnkCd = request.BnkCd,
                BnkNm = request.BnkNm,
                fInAct = false,
                fApr = 1,
                SKy = 1,
                EntUsrKy = userKey,
                EntDtm = DateTime.Now
            };

            db.BnkMas.Add(bank);
            await db.SaveChangesAsync();
        }


        // -----------------------------
        // PUT : Update Bank
        // -----------------------------
        public async Task UpdateBankAsync(int bankKey, UpdateBankDto request)
        {
            using var db = await _factory.CreateDbContextAsync();

            var bank = await db.BnkMas
                .FirstOrDefaultAsync(x => x.BnkKy == bankKey);

            if (bank == null)
                throw new Exception("Bank not found");

            if (string.IsNullOrWhiteSpace(request.BnkCd))
                throw new Exception("Bank code is required");

            if (string.IsNullOrWhiteSpace(request.BnkNm))
                throw new Exception("Bank name is required");

            bool codeExists = await db.BnkMas.AnyAsync(x =>
                x.BnkCd == request.BnkCd &&
                x.BnkKy != bankKey &&
                !x.fInAct);

            if (codeExists)
                throw new Exception("Bank code already exists");

            bool nameExists = await db.BnkMas.AnyAsync(x =>
                x.BnkNm == request.BnkNm &&
                x.BnkKy != bankKey &&
                !x.fInAct);

            if (nameExists)
                throw new Exception("Bank name already exists");

            bank.BnkCd = request.BnkCd;
            bank.BnkNm = request.BnkNm;
            bank.fInAct = false;

            await db.SaveChangesAsync();
        }


        // -----------------------------
        // DELETE : Delete Bank
        // -----------------------------
        public async Task DeleteBankAsync(int bankKey)
        {
            using var db = await _factory.CreateDbContextAsync();

            var bank = await db.BnkMas
                .FirstOrDefaultAsync(x => x.BnkKy == bankKey);

            if (bank == null)
                throw new Exception("Bank not found");

            if (bank.fInAct)
                throw new Exception("Bank already deleted");

            bank.fInAct = true;
            bank.Status = "D";

            await db.SaveChangesAsync();
        }

    }
}
