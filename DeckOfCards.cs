using System.Collections.Generic;
using System;
using System.Linq;
public class DeckOfCards
{
    private Stack<Card> deck = new();
    Random random = new Random();
    public DeckOfCards(bool randomizedOrder = false)
    {
        CreateDeck(randomizedOrder);
    }
    public Card DrawCard()
    {
        return deck.Pop();
    }
    void CreateDeck(bool randomizedOrder)
    {
        // Delete the old deck
        deck.Clear();

        // For each suit, create a card of that suit for each rank
        foreach (Suits suit in System.Enum.GetValues(typeof(Suits)))
        {
            foreach (Ranks rank in System.Enum.GetValues(typeof(Ranks)))
            {
                deck.Push(new Card(suit, rank));
            }
        }

        // Give deck a random card order
        if (randomizedOrder) Shuffle();
    }
    void Shuffle()
    {
        // Turn the current deck stack into a list
        List<Card> list = deck.ToList();

        // Apply Fisher-Yates shuffle (start at end of list, swap last element with a random one before it (or itself))
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);

            // swap
            Card temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }

        list.Reverse();
        deck = new Stack<Card>(list);
    }
}