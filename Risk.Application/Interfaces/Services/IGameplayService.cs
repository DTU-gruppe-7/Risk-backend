using Risk.Application.DTOs.Gameplay;

namespace Risk.Application.Interfaces.Services;

public interface IGameplayService
{
    Task PlaceTroopsAsync(PlaceTroopsDto dto, CancellationToken cancellationToken = default);
    Task FortifyTroopsAsync(FortifyTroopsDto dto, CancellationToken cancellationToken = default);
    Task<AttackResultDto> AttackAsync(AttackDto dto, CancellationToken cancellationToken = default);
}