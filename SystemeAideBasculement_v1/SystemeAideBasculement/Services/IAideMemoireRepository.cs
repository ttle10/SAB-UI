using SystemeAideBasculement.Models;

namespace SystemeAideBasculement.Services
{
    public interface IAideMemoireRepository
    {
        Task<List<AideMemoireVm>> GetAllAsync();
        Task UpdateAsync(AideMemoireVm vm);

        Task SoftDeleteAsync(int id);

        Task AddAsync(string step);

        Task ResetAllAsync();
    }
}
