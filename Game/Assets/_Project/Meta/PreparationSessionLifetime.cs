using UnityEngine;

namespace HumanLastRampart.Meta
{
    // ScreenView deactivates its root synchronously on exit, invalidating outstanding quotes.
    public sealed class PreparationSessionLifetime : MonoBehaviour
    {
        internal PreparationTransactions Session;
        void OnDisable() { Session?.Close(); }
        void OnDestroy() { Session?.Close(); }
    }
}
