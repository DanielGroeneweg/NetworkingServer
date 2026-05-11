using System.Collections.Generic;
using System.Linq;
public class HandEvaluator
{
    class EvaluatedHand
    {
        public Combinations handRank;
        public List<int> combinationValues;
        public List<int> tieBrakerValues;
    }
    /// <summary>
    /// Returns a list of player IDs for each player who won/tied.
    /// </summary>
    /// <param name="players"></param>
    /// <param name="board"></param>
    /// <returns></returns>
    public static List<int> GetWinners(List<Player> players, Card[] board)
    {
        // Create a list of hands & player ID's, excluding players who folded/bankrupted.
        Dictionary<int, EvaluatedHand> potentialWinners = new();
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].isInHand) potentialWinners.Add(i + 1, GetHand(board, players[i].cards));
        }

        return CompareHands(potentialWinners);
    }
    /// <summary>
    /// Compares all hands and returns a List of winners
    /// </summary>
    /// <param name="hands"></param>
    /// <returns></returns>
    static List<int> CompareHands(Dictionary<int, EvaluatedHand> potentialWinners)
    {
        // Find the best combination out of all hands
        Combinations winningCombination = Combinations.HighCard;
        foreach (var player in potentialWinners.Keys)
            if (potentialWinners[player].handRank >= winningCombination) winningCombination = potentialWinners[player].handRank;

        // Add all people with the best combination to a list
        List<int> winners = new();
        foreach (int player in potentialWinners.Keys)
        {
            if (potentialWinners[player].handRank == winningCombination) winners.Add(player);
        }

        // If multiple people have the best combination, try and apply tiebreaker methods
        if (winners.Count > 1)
        {
            // Create a list to start comparing hands
            List<int> best = new();
            EvaluatedHand bestHand = null;

            // Add the first player
            best.Add(winners[0]);
            bestHand = potentialWinners[winners[0]];

            // Start at 1 to skip the first player who was already added
            for(int i = 1; i < winners.Count; i++)
            {
                int playerID = winners[i];
                EvaluatedHand currentHand = potentialWinners[playerID];

                // Compare hands
                int betterHand = CompareEvaluatedHands(currentHand, bestHand);

                // -1 is currentHand, 0 is a tie, 1 is bestHand
                switch(betterHand)
                {
                    // New hand is better, remove everyone who was tied (or leading) and set this new hand to the best.
                    case -1:
                        best.Clear();
                        best.Add(playerID);
                        bestHand = currentHand;
                        break;
                    
                    // Tie! Simply add this new player to the list of winner.
                    case 0:
                        best.Add(playerID);
                        break;

                    // Standing hand is better, no need to do anything.
                    case 1:
                        break;
                }
            }

            return best;
        }

        // Return the list of 1 winner if the size of winners is 1
        return winners;
    }
    /// <summary>
    /// Returns an EvaluatedHand with the strongest combination for said hand.
    /// </summary>
    /// <param name="board"></param>
    /// <param name="hand"></param>
    /// <returns></returns>
    static EvaluatedHand GetHand(Card[] board, Card[] hand)
    {
        #region SetUp
        // Combine board and player cards into a list of 7 cards
        List<Card> cards = new();
        cards.AddRange(hand);
        cards.AddRange(board);

        // Create a list of all values in the hand from high to low
        List<int> values = GetValues(cards);
        values = values.OrderByDescending(value => value).ToList();

        // Create groups of cards with the same value (I.E. 2x king, 1x 8)
        Dictionary<int, int> valueCounts = GetValueCounts(values);
        List<KeyValuePair<int, int>> groups = valueCounts.ToList();

        groups = groups
            // Group with most amount of cards goes first
            .OrderByDescending(keyValue => keyValue.Value)
            // Within that order, groups of higher card values come first
            .ThenByDescending(keyValue => keyValue.Key)
            .ToList();

        // Get a flush reference
        List<Card> cardsInFlush = GetFlush(cards);

        // Get a straight reference
        List<List<int>> possibleStraights = GetStraight(values);
        #endregion

        // Check for each combination if the player has it, Descending from best combination to worst.
        // This way we can skip a few checks if the player has a good hand.
        #region HandRankChecks
        // Check For Straight Flush
        if (cardsInFlush != null && possibleStraights != null)
        {
            // For each possible straight, check if it's a straight flush
            // If so, it is also the highest straight flush the player can make
            foreach (List<int> straight in possibleStraights)
            {
                bool hasStraightFlush = true;
                foreach (int value in straight)
                {
                    if (!CardsContainsCardWithValue(value, cardsInFlush))
                    {
                        hasStraightFlush = false;
                        break;
                    }
                }

                // Return the straight flush as best possible combination
                if (hasStraightFlush)
                {
                    EvaluatedHand bestHand = new EvaluatedHand();
                    bestHand.handRank = Combinations.StraightFlush;
                    bestHand.combinationValues = straight;

                    // 5 cards are in the combination, no tie breaker cards
                    bestHand.tieBrakerValues = null;
                    return bestHand;
                }
            }
        }

        // Check for Four of a kind
        if (groups[0].Value == 4)
        {
            EvaluatedHand bestHand = new EvaluatedHand();
            bestHand.handRank = Combinations.FourOfAKind;
            bestHand.combinationValues = new List<int> { groups[0].Key };

            // 5th card should be the highest value card, not including the ones in the four of a kind.
            while (values.Contains(groups[0].Key)) values.Remove(groups[0].Key);
            bestHand.tieBrakerValues = new List<int>() { values[0] };
            return bestHand;
        }

        // Check for Full House
        if (groups[0].Value == 3 && groups[1].Value == 2)
        {
            EvaluatedHand bestHand = new EvaluatedHand();
            bestHand.handRank = Combinations.FullHouse;
            bestHand.combinationValues = new List<int>() { groups[0].Key, groups[1].Key };

            // 5 cards are in the combination, no tie breaker cards
            bestHand.tieBrakerValues = null;
            return bestHand;
        }

        // Check for Flush
        if (cardsInFlush != null)
        {
            EvaluatedHand bestHand = new EvaluatedHand();
            bestHand.handRank = Combinations.Flush;
            bestHand.combinationValues = new List<int>();
            for (int i = 0; i < 5; i++)
            {
                bestHand.combinationValues.Add(GetCardValue(cardsInFlush[i]));
            }

            // 5 cards are in the combination, no tie breaker cards
            bestHand.tieBrakerValues = null;
            return bestHand;
        }

        // Check for Straight
        if (possibleStraights != null)
        {
            EvaluatedHand bestHand = new EvaluatedHand();
            bestHand.handRank = Combinations.Straight;

            // First straight is the one with the highest value
            bestHand.combinationValues = possibleStraights[0];

            // 5 cards are in the combination, no tie breaker cards
            bestHand.tieBrakerValues = null;
            return bestHand;
        }

        // Check for Three of a kind
        if (groups[0].Value == 3)
        {
            EvaluatedHand bestHand = new EvaluatedHand();
            bestHand.handRank = Combinations.ThreeOfAKind;
            bestHand.combinationValues = new List<int> { groups[0].Key };

            // 4th and 5th card should be the highest value cards, not including the ones in the three of a kind.
            while (values.Contains(groups[0].Value)) values.Remove(groups[0].Value);
            bestHand.tieBrakerValues = new List<int>() { values[0], values[1] };
            return bestHand;
        }

        // Check for Two Pair
        if (groups[0].Value == 2 && groups[1].Value == 2)
        {
            EvaluatedHand bestHand = new EvaluatedHand();
            bestHand.handRank = Combinations.TwoPair;
            bestHand.combinationValues = new List<int> { groups[0].Key, groups[1].Key };

            //5th card should be the highest value cards, not including the ones in the pairs.
            while (values.Contains(groups[0].Value)) values.Remove(groups[0].Value);
            while (values.Contains(groups[1].Value)) values.Remove(groups[1].Value);
            bestHand.tieBrakerValues = new List<int>() { values[0] };
            return bestHand;
        }

        // Check for Pair
        if (groups[0].Value == 2)
        {
            EvaluatedHand bestHand = new EvaluatedHand();
            bestHand.handRank = Combinations.Pair;
            bestHand.combinationValues = new List<int> { groups[0].Key };

            //3rd, 4th and 5th card should be the highest value cards, not including the ones in the pair.
            while (values.Contains(groups[0].Value)) values.Remove(groups[0].Value);
            bestHand.tieBrakerValues = new List<int>() { values[0], values[1], values[2] };
            return bestHand;
        }

        // Check for High Card
        {
            EvaluatedHand bestHand = new EvaluatedHand();
            bestHand.handRank = Combinations.HighCard;
            bestHand.combinationValues = new List<int>() { values[0] };
            // Add the other cards as tie breakers
            bestHand.tieBrakerValues = new List<int>() { values[1], values[2], values[3], values[4] };
            return bestHand;
        }
        #endregion
    }

    #region EvaluationHelperMethods
    /// <summary>
    /// Checks two EvaluatedHands and returns the better one
    /// 0 on a tie
    /// -1 on hand a
    /// 1 on hand b
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    static int CompareEvaluatedHands(EvaluatedHand a, EvaluatedHand b)
    {
        // Compare combination values
        int result = CompareLists(a.combinationValues, b.combinationValues);
        if (result != 0) return result;

        // If no winner is found, check tiebrakers
        return CompareLists(a.tieBrakerValues, b.tieBrakerValues);
    }
    /// <summary>
    /// Checks for two lists of integers and returns which one is greater
    /// 0 on a tie
    /// -1 on list a
    /// 1 on list b
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    static int CompareLists(List<int> a, List<int> b)
    {
        // no cards to check, it's a tie
        if (a == null && b == null) return 0;

        // If somehow player a does not have this list, make player b win
        if (a == null) return 1;

        // If somehow player b does not have this list, make player a win
        if (b == null) return -1;

        // Lists have to be the same size because players have the same combination
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] > b[i]) return -1;
            if (a[i] < b[i]) return 1;
        }

        // same values, it's a tie!
        return 0;
    }
    static bool CardsContainsCardWithValue(int value, List<Card> cards)
    {
        foreach (Card card in cards)
        {
            // Check for normal cards
            if ((int)card.rank == value) return true;

            // Check for Ace
            if (card.rank == Ranks.Ace && (value == 1 || value == 14)) return true;
        }

        // Return false if no cards have the right value
        return false;
    }
    /// <summary>
    /// Returns a list of all possible straights to be made, null if none.
    /// </summary>
    /// <param name="cards"></param>
    /// <returns></returns>
    static List<List<int>> GetStraight(List<int> cardValues)
    {
        // Make an Ace (14) count as BOTH 1 and 14, then remove all duplicates
        if (cardValues.Contains(14)) cardValues.Add(1);

        cardValues = cardValues.Distinct().ToList();

        List<List<int>> possibleStraights = new();

        // We start at each card and check if we can make a straight with that card as highest
        for (int card = 0; card < cardValues.Count; card++)
        {
            List<int> cardsInStraight = new();

            // Add the first value so that we have a starting point
            cardsInStraight.Add(cardValues[card]);

            // Skip if there are not enough cards to make a straight with
            if (cardValues.Count - card < 5)
            {
                break;
            }

            // Loop through all values in the potential hand, if 5 cards are 'descendant' of the previous, there is a straight.
            for (int i = card + 1; i < cardValues.Count; i++)
            {
                // Check if the value is 1 less than the last found value.
                if (cardValues[i] == cardsInStraight.Last() - 1)
                {
                    cardsInStraight.Add(cardValues[i]);

                    // If there are 5 consecutive cards, return them.
                    if (cardsInStraight.Count == 5)
                    {
                        possibleStraights.Add(cardsInStraight);
                        break;
                    }
                }

                // If the value is not 1 less than the last found value,
                else
                {
                    break;
                }
            }
        }

        // Return all possible straights
        if (possibleStraights.Count > 0) return possibleStraights;

        // If there is no straight found, return null
        return null;
    }
    /// <summary>
    /// Returns a list of cards in a flush, or null if there is no flush to be made.
    /// </summary>
    /// <param name="cards"></param>
    /// <returns></returns>
    static List<Card> GetFlush(List<Card> cards)
    {
        Dictionary<Suits, List<Card>> sameSuitCards = new();

        // Create a list of cards for each suit, containing each card of that suit
        foreach (Card card in cards)
        {
            if (!sameSuitCards.ContainsKey(card.suit))
            {
                sameSuitCards.Add(card.suit, new());
                sameSuitCards[card.suit].Add(card);
            }

            else sameSuitCards[card.suit].Add(card);
        }

        // Check for all suits if they have 5 or more cards, if so, return them, sorted so that the highest value comes first
        foreach (Suits suit in sameSuitCards.Keys)
        {
            if (sameSuitCards[suit].Count >= 5)
            {
                return sameSuitCards[suit].OrderByDescending(card => GetCardValue(card)).ToList();
            }
        }

        // Return null if there is no flush
        return null;
    }
    /// <summary>
    /// Returns a list of all values in integer form in a collection of cards
    /// </summary>
    /// <param name="cards"></param>
    /// <returns></returns>
    static List<int> GetValues(List<Card> cards)
    {
        List<int> values = new();
        foreach (Card card in cards) values.Add(GetCardValue(card));

        return values;
    }
    /// <summary>
    /// Returns the value of a card in integer form
    /// </summary>
    /// <param name="card"></param>
    /// <returns></returns>
    static int GetCardValue(Card card)
    {
        int value = (int)card.rank;

        // Ace should be high (14)
        if (value == 1)
            return 14;

        return value;
    }
    /// <summary>
    /// Returns a dictionary with as key an int (the card value) and as value an int (the amount of cards in the hand of that value)
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    static Dictionary<int, int> GetValueCounts(List<int> values)
    {
        Dictionary<int, int> valueCounts = new();

        foreach (int value in values)
        {
            if (valueCounts.ContainsKey(value)) valueCounts[value]++;
            else valueCounts.Add(value, 1);
        }

        return valueCounts;
    }
    #endregion
}