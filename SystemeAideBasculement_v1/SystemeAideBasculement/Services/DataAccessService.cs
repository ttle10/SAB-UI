using Microsoft.EntityFrameworkCore;
using SystemeAideBasculement.Context;
using SystemeAideBasculement.Models;

namespace SystemeAideBasculement.Services
{
    public class DataAccessService
    {
        private readonly AideMemoireDbContext _db;

        public DataAccessService(AideMemoireDbContext db)
        {
            _db = db;
        }

        public async Task<List<AideMemoireVm>> GetAllAsync()
        {

            return await _db.AideMemoires
                    .Where(e => !e.IsDeleted)
                    .OrderBy(e => e.Id)
                    .Select(e => e.ToVm())
                    .ToListAsync();
        }

        public async Task UpdateAsync(AideMemoireVm vmodel)
        {
            var entity = await _db.AideMemoires.FindAsync(vmodel.Id);
            if (entity == null) return;

            entity.Step = vmodel.Step;
            entity.Initials = vmodel.Initials;
            entity.IsCompleted = vmodel.IsCompleted;

            await _db.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(int id)
        {
            var entity = await _db.AideMemoires.FindAsync(id);
            if (entity == null) return;

            entity.IsDeleted = true;
            await _db.SaveChangesAsync();
        }

        public async Task AddAsync(string step)
        {
            var clean = step?.Trim();

            if (string.IsNullOrWhiteSpace(clean) || clean.Length > 500)
                throw new ArgumentException("Étape invalide");

            _db.AideMemoires.Add(new AideMemoireModel
            {
                Step = clean
            });

            await _db.SaveChangesAsync();
        }

        public async Task ResetAllAsync()
        {
            await _db.AideMemoires
                .Where(e => !e.IsDeleted)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Initials, string.Empty)
                    .SetProperty(e => e.IsCompleted, false)
                );
        }
    }

}
