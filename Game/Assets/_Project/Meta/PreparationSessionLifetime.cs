using UnityEngine;

namespace HumanLastRampart.Meta
{
    // ScreenView deactivates its root synchronously on exit, invalidating outstanding quotes.
    public sealed class PreparationSessionLifetime : MonoBehaviour
    {
        internal PreparationTransactions Session;
        internal TrapPreparationTransactions Traps;
        void OnDisable() { Session?.Close(); Traps?.Close(); }
        void OnDestroy() { Session?.Close(); Traps?.Close(); }
    }
}
