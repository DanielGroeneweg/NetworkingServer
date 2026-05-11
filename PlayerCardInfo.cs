using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json;
[Serializable]
public class PlayerCardCombo
{
    public int player;
    public List<Card> cards;
}
[Serializable]
public class PlayerCardInfo
{
    public List<PlayerCardCombo> players = new();
    public string GetJSON(Dictionary<int, Player> playerDic)
    {
        // Run through all players, make a PlayerCardCombo that stores a player's cards and their ID.
        // Add this PlayerCardCombo to a list se we can serialize it to json so the server can send it.
        foreach (int playerID in playerDic.Keys)
        {
            players.Add(new PlayerCardCombo() { player = playerID, cards = playerDic[playerID].cards.ToList() });
        }

        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}