using UnityEngine;

public static class GameplayDebug
{
    public static void Log(
        bool enabled,
        object message,
        Object context = null)
    {
        if (!enabled)
        {
            return;
        }

        if (context != null)
        {
            Debug.Log(message, context);
            return;
        }

        Debug.Log(message);
    }
}
