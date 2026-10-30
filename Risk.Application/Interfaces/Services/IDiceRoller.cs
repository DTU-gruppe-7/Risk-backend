namespace Risk.Application.Interfaces.Services;

public interface IDiceRoller
{
    IReadOnlyList<int> Roll(int count);
}
