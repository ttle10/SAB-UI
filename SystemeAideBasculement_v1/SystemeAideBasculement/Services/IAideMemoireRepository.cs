using SystemeAideBasculement.Models;

namespace SystemeAideBasculement.Services
{
    public interface IAideMemoireRepository
    {
        Task<List<AideMemoireVm>> GetAllAsync();
        Task UpdateAsync(AideMemoireVm vm, Guid initiator);

        Task SoftDeleteAsync(int id, Guid initiator);

        Task AddAsync(string step, Guid initiator);

        Task ResetAllAsync(Guid initiator);

        Task UpdateOrderAsync(List<AideMemoireVm> list, Guid initiator);
    }
}
