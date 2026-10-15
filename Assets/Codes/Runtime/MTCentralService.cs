using System;
using UnityEngine;

namespace MTCentral
{
    public class MTCentralService
    {
        internal static Guid userId;
        public void Initialize(Guid userId, string apiUrl)
        {
            if (NetworkUtility.Instance == null)
            {
                MTCentralService.userId = userId;
                var nugo = new GameObject("MTCentral");
                var nu = nugo.AddComponent<NetworkUtility>();
                nu.BaseUrl = apiUrl;
            }
        }

        public static void Log(string msg, UnityEngine.Object o = null) => Debug.Log($"[MTCentral] {msg}", o);
        public static void LogError(string msg, UnityEngine.Object o = null) => Debug.LogError($"[MTCentral] {msg}", o);
    }
}
