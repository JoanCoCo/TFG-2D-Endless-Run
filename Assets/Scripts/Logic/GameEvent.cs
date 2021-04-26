using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameEvent
{
    public const string PLAYER_HEALTH_CHANGED = "PLAYER_HEALTH_CHANGED";
    public const string TIME_PASSED = "TIME_PASSED";
    public const string PLAYER_DIED = "PLAYER_DIED";
    public const string PAUSE = "PAUSE";
    public const string RESUME = "RESUME";
    public const string NEW_HIGHSCORE_REACHED = "NEW_HIGHSCORE_REACHED";
    public const string DISTANCE_INCREASED = "DISTANCE_INCREASED";
    public const string PLAYER_MOVED = "PLAYER_MOVED";
    public const string PLAYER_STARTS = "PLAYER_STARTS";
    public const string LAST_PLAYER_POSITION_CHANGED = "LAST_PLAYER_POSITION_CHANGED";
    public const string FIRST_PLAYER_POSITION_CHANGED = "FIRST_PLAYER_POSITION_CHANGED";
    public const string GAME_FINISHED = "GAME_FINISHED";
}
