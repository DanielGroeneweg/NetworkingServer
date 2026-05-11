public class Card
{
    public Suits suit { get; private set; }
    public Ranks rank { get; private set; }
    public Card(Suits suit, Ranks rank)
    {
        this.suit = suit;
        this.rank = rank;
    }
    public override string ToString()
    {
        return $"{rank} of {suit}";
    }
}