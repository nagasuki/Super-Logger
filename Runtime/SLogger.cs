using UnityEngine;

namespace PugDev.SuperLogger
{
    public class SLogger
    {
        /// <summary>
        /// Logs a <b>debug</b> message to the Unity console with an optional category.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="category">The category of the message. Defaults to "General".</param>
        public static void LogDebug(string message, string category = "General:FFFFFF")
        {
            var groupMessage = category.Split(":")[0];
            var groupColor = category.Split(":")[1];
            Debug.Log($"[SLogger]<color=#{groupColor}>[{groupMessage}]</color> {message}");
        }

        /// <summary>
        /// Logs a <b>warning</b> message to the Unity console with an optional category.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="category">The category of the message. Defaults to "General".</param>
        public static void LogWarning(string message, string category = "General:FFFFFF")
        {
            var groupMessage = category.Split(":")[0];
            var groupColor = category.Split(":")[1];
            Debug.LogWarning($"[SLogger]<color=#{groupColor}>[{groupMessage}]</color> {message}");
        }

        /// <summary>
        /// Logs a <b>error</b> message to the Unity console with an optional category.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="category">The category of the message. Defaults to "General".</param>
        public static void LogError(string message, string category = "General:FFFFFF")
        {
            var groupMessage = category.Split(":")[0];
            var groupColor = category.Split(":")[1];
            Debug.LogError($"[SLogger]<color=#{groupColor}>[{groupMessage}]</color> {message}");
        }
    }
}