public class Card
{
    public Suits suit;
    public Ranks rank;
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