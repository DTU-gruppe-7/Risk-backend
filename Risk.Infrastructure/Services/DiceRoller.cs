using System.Security.Cryptography;
using Risk.Application.Interfaces.Services;

namespace Risk.Infrastructure.Services;

public class DiceRoller : IDiceRoller
{
    public IReadOnlyList<int> Roll(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Must roll at least 1 die.");

        var results = new int[count];
        for (int i = 0; i < count; i++)
        {
            // Returnerer heltal fra 1 til 6 uden bias (7 er eksklusiv)
            results[i] = RandomNumberGenerator.GetInt32(1, 7);
        }

        return results;
    }
}
