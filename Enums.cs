public enum Suits { Spades, Hearts, Clubs, Diamonds }
public enum Ranks
{
    Ace = 1, Two, Three, Four, Five, Six,
    Seven, Eight, Nine, Ten, Jack, Queen, King
}
public enum GamePhases { PreFlop, Flop, Turn, River, Showdown }
public enum BettingActions { Check, Bet, Call, Raise, Fold, None }
public enum Combinations { HighCard = 1, Pair, TwoPair, ThreeOfAKind, Straight, Flush, FullHouse, FourOfAKind, StraightFlush }