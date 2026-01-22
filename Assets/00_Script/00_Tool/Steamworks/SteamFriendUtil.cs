using Steamworks;
using System.Collections.Generic;

public static class SteamFriendUtil
{
    public static List<CSteamID> GetOnlineFriends()
    {
        var list = new List<CSteamID>();

        int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
        for (int i = 0; i < count; i++)
        {
            var id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            var state = SteamFriends.GetFriendPersonaState(id);

            // ƒIƒ“ƒ‰ƒCƒ“‚¾‚¯•\Ž¦‚µ‚½‚¢‚È‚çi‚é
            if (state == EPersonaState.k_EPersonaStateOnline ||
                state == EPersonaState.k_EPersonaStateAway ||
                state == EPersonaState.k_EPersonaStateBusy)
            {
                list.Add(id);
            }
        }
        return list;
    }
}
