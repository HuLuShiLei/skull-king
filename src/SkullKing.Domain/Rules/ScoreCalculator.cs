namespace SkullKing.Domain.Rules;

/// <summary>一名玩家在某一轮的结算明细。</summary>
public sealed record PlayerRoundScore(
    int Seat,
    int Bid,
    int TricksWon,
    int BaseScore,
    int Bonus,
    int Total)
{
    public bool BidMet => Bid == TricksWon;
}

public static class ScoreCalculator
{
    public const int PerTrickWhenBidMet = 20;
    public const int PerTrickMissedPenalty = 10;
    public const int ZeroBidPerRound = 10;

    /// <summary>
    /// 叫 0 命中得「轮次 ×10」，落空则同额倒扣；叫 N 命中得「N ×20」外加奖励分，
    /// 落空按叫牌与实得的差额每墩扣 10 且没有奖励分。
    /// </summary>
    public static PlayerRoundScore Score(int seat, int roundNumber, int bid, int tricksWon, int accruedBonus)
    {
        if (bid == 0)
        {
            var zeroScore = roundNumber * ZeroBidPerRound;
            var met = tricksWon == 0;
            var baseScore = met ? zeroScore : -zeroScore;

            return new PlayerRoundScore(seat, bid, tricksWon, baseScore, 0, baseScore);
        }

        if (bid == tricksWon)
        {
            var baseScore = bid * PerTrickWhenBidMet;
            return new PlayerRoundScore(seat, bid, tricksWon, baseScore, accruedBonus, baseScore + accruedBonus);
        }

        var penalty = -Math.Abs(bid - tricksWon) * PerTrickMissedPenalty;
        return new PlayerRoundScore(seat, bid, tricksWon, penalty, 0, penalty);
    }
}
