using UnityEngine;

namespace SushiParty.Core
{
    public sealed class SushiPartyRoot : MonoBehaviour
    {
        private const string ServiceHostName = "~SushiParty Services";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureServices()
        {
            if (MinigameFlow.Instance != null)
            {
                return;
            }

            GameObject host = new GameObject(ServiceHostName);
            DontDestroyOnLoad(host);
            MinigameFlow.CreateOn(host);
        }

        private void Start()
        {
            if (MinigameFlow.Instance == null)
            {
                EnsureServices();
            }

            MinigameFlow.Instance.ReturnToMenu();
        }
    }
}
