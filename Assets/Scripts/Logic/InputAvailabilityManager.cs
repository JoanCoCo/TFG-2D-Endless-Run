using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputAvailabilityManager : MonoBehaviour
{
    private bool userIsTyping = false;

    public bool UserIsTyping
    {
        get
        {
            return userIsTyping;
        }
    }

    public void UserStartedTyping(string s) { if(!s.Equals("")) userIsTyping = true; }
    public void UserFinishedTyping(string s) { userIsTyping = false; }
}
