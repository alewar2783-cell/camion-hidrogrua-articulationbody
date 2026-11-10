#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Truck
{
    public sealed class SceneResetter : MonoBehaviour
    {
        public static void Reload()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("The active scene is not saved, so it cannot be reloaded.");
                return;
            }

#if UNITY_EDITOR
            if (scene.buildIndex < 0)
            {
                // A scene outside the Build Settings can only be reloaded this way in the Editor.
                EditorSceneManager.LoadSceneInPlayMode(scene.path,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scene.path);
        }

        /// <summary>Instance entry point: a persistent button call needs a target object.</summary>
        public void ReloadScene()
        {
            Reload();
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
