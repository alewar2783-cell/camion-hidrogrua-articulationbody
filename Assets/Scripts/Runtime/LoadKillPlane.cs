using UnityEngine;

namespace Truck
{
    /// <summary>
    /// Returns a load to its spawn point when it leaves the world (fell off the ground plane),
    /// so the task can always be repeated without resetting the scene.
    /// </summary>
    public sealed class LoadKillPlane : MonoBehaviour
    {
        [SerializeField] private Rigidbody _body;
        [SerializeField] private Vector3 _spawn;

        /// <summary>A NaN or infinite height also counts as out of the world.</summary>
        public static bool IsOutOfWorld(float height)
        {
            return !(height >= TruckSpec.Loads.KillPlaneY);
        }

        public void Initialize(Rigidbody body, Vector3 spawn)
        {
            _body = body;
            _spawn = spawn;
        }

        private void FixedUpdate()
        {
            if (_body == null || !IsOutOfWorld(_body.position.y))
            {
                return;
            }

            transform.SetPositionAndRotation(_spawn, Quaternion.identity);
            _body.position = _spawn;
            _body.rotation = Quaternion.identity;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }
    }
}
