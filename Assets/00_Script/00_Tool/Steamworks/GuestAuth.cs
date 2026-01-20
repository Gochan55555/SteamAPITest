using System;
using UnityEngine;

public static class GuestAuth
{
    private const string KEY_GUEST_ID = "guest_id";
    private const string KEY_GUEST_NAME = "guest_name";

    public static string GetOrCreateGuestId()
    {
        var id = PlayerPrefs.GetString(KEY_GUEST_ID, "");
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("N"); // 32ï∂éö
            PlayerPrefs.SetString(KEY_GUEST_ID, id);
            PlayerPrefs.Save();
        }
        return id; 
    }

    public static string GetOrCreateGuestName()
    {
        var name = PlayerPrefs.GetString(KEY_GUEST_NAME, "");
        if (string.IsNullOrEmpty(name))
        {
            // éGÇ…î‘çÜÇÇ¬ÇØÇÈ
            var id = GetOrCreateGuestId();
            name = "Guest" + id.Substring(0, 4);
            PlayerPrefs.SetString(KEY_GUEST_NAME, name);
            PlayerPrefs.Save();
        }
        return name;
    }

    public static void SetGuestName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        PlayerPrefs.SetString(KEY_GUEST_NAME, name.Trim());
        PlayerPrefs.Save();
    }

    public static void ResetGuest()
    {
        PlayerPrefs.DeleteKey(KEY_GUEST_ID);
        PlayerPrefs.DeleteKey(KEY_GUEST_NAME);
        PlayerPrefs.Save();
    }
}
