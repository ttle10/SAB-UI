using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SystemeAideBasculement.Context;
using SystemeAideBasculement.Hubs;
using SystemeAideBasculement.Models;

namespace SystemeAideBasculement.Services
{
    public class DataAccessService : IAideMemoireRepository
    {
        private readonly AideMemoireDbContext _db;

        private readonly IHubContext<NotificationHub> _hubContext;

        public DataAccessService(AideMemoireDbContext db,
                                IHubContext<NotificationHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }

        public async Task<List<AideMemoireVm>> GetAllAsync()
        {

            return await _db.AideMemoires
                    .AsNoTracking()  //
                    .Where(e => !e.IsDeleted)
                    .OrderBy(e => e.DisplayOrder)
                    .Select(e => e.ToVm())
                    .ToListAsync();
        }

        public async Task UpdateAsync(AideMemoireVm vmodel, Guid initiator)
        {
            var entity = await _db.AideMemoires.FindAsync(vmodel.Id);
            if (entity == null) return;

            entity.Step = vmodel.Step;
            entity.Initials = vmodel.Initials;
            entity.IsCompleted = vmodel.IsCompleted;

            await _db.SaveChangesAsync();

            // Notify all OTHER clients to refresh
            await _hubContext.Clients.All.SendAsync(NotificationService.ReminderChangedMethod, initiator);
        }

        public async Task SoftDeleteAsync(int id, Guid initiator)
        {
            var entity = await _db.AideMemoires.FindAsync(id);
            if (entity == null) return;

            entity.IsDeleted = true;
            await _db.SaveChangesAsync();

            // Notify all OTHER clients to refresh
            await _hubContext.Clients.All.SendAsync(NotificationService.ReminderChangedMethod, initiator);
        }

        public async Task AddAsync(string step, Guid initiator)
        {
            var clean = step?.Trim();

            if (string.IsNullOrWhiteSpace(clean) || clean.Length > AideMemoireModel.StepMaxLength)
                throw new ArgumentException("Étape invalide");

            var maxOrder = await _db.AideMemoires
                .Where(e => !e.IsDeleted)
                .MaxAsync(e => (int?)e.DisplayOrder) ?? 0;

            _db.AideMemoires.Add(new AideMemoireModel
            {
                Step = step,
                DisplayOrder = maxOrder + 1
            });

            await _db.SaveChangesAsync();

            // Notify all OTHER clients to refresh
            await _hubContext.Clients.All.SendAsync(NotificationService.ReminderChangedMethod, initiator);
        }

        public async Task ResetAllAsync(Guid initiator)
        {
            await _db.AideMemoires
                .Where(e => !e.IsDeleted)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Initials, string.Empty)
                    .SetProperty(e => e.IsCompleted, false)
                );

            // Notify all OTHER clients to refresh
            await _hubContext.Clients.All.SendAsync(NotificationService.ReminderChangedMethod, initiator);
        }


        public async Task UpdateDisplayOrderAsync(List<AideMemoireVm> list, Guid initiator)
        {
            foreach (var item in list)
            {
                var entity = await _db.AideMemoires.FindAsync(item.Id);

                if (entity != null)
                {
                    entity.DisplayOrder = item.DisplayOrder;
                }
            }

            await _db.SaveChangesAsync();

            // Notify all OTHER clients to refresh
            await _hubContext.Clients.All.SendAsync(NotificationService.ReminderChangedMethod, initiator);
        }
    }

}
