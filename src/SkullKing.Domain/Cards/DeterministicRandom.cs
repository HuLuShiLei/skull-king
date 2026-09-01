namespace SkullKing.Domain.Cards;

/// <summary>
/// splitmix64。自带实现而不用 <see cref="System.Random"/>，是为了让同一个种子在任何运行时版本上
/// 都能洗出同一副牌，事件回放才能重现历史对局。
/// </summary>
public struct DeterministicRandom(ulong seed)
{
    private ulong _state = seed;

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>返回 [0, exclusiveUpperBound) 区间内的值。</summary>
    public uint NextBelow(uint exclusiveUpperBound)
    {
        if (exclusiveUpperBound == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
        }

        // Lemire 无偏取余
        var product = (ulong)(uint)NextUInt64() * exclusiveUpperBound;
        var low = (uint)product;

        if (low < exclusiveUpperBound)
        {
            var threshold = (uint)(-(int)exclusiveUpperBound) % exclusiveUpperBound;

            while (low < threshold)
            {
                product = (ulong)(uint)NextUInt64() * exclusiveUpperBound;
                low = (uint)product;
            }
        }

        return (uint)(product >> 32);
    }

    /// <summary>从一个基准种子派生出子种子，用于每一轮独立洗牌。</summary>
    public static ulong Derive(ulong seed, int salt)
    {
        var rng = new DeterministicRandom(seed ^ ((ulong)salt * 0xD1342543DE82EF95UL));
        return rng.NextUInt64();
    }
}
