using Risk.Application.DTOs;

namespace Risk.Application.Interfaces.Services;

public interface IGameplayService
{
    Task<AttackResultDto> AttackAsync(AttackDto dto, CancellationToken cancellationToken = default);
    Task<BlitzResultDto> BlitzAttackAsync(BlitzAttackDto dto, CancellationToken cancellationToken = default);
}
