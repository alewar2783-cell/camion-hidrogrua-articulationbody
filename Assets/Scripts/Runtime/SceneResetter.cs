using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Truck
{
    public sealed class SceneResetter : MonoBehaviour
    {
        public static void Reload()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard[Key.Backspace].wasPressedThisFrame || keyboard[Key.P].wasPressedThisFrame)
            {
                Reload();
            }
        }
    }
}
