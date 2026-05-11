using UnityEngine;
public class MoveData
{
    public BettingActions actionTaken;
    public int moneyBet { get; private set; }
    public int player { get; private set; }
    public MoveData(BettingActions actionTaken, int moneyBet, int player)
    {
        this.actionTaken = actionTaken;
        this.moneyBet = moneyBet;
        this.player = player;
    }
}