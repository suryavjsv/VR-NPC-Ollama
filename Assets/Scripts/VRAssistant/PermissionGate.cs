using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace VRAssistant
{
    /// <summary>
    /// Blocks all NPC systems until required Android permissions are granted.
    /// Attach to a GameObject that loads BEFORE your NPC (or use execution order).
    ///
    /// SETUP:
    /// 1. Add this to a top-level GameObject in your scene (e.g., "PermissionManager").
    /// 2. It will automatically request permissions on start.
    /// 3. Other scripts can check PermissionGate.Instance.AllPermissionsGranted
    ///    or yield on WaitForPermissions().
    ///
    /// On non-Android platforms (Editor), permissions are auto-granted.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Run before everything else
    public class PermissionGate : MonoBehaviour
    {
        [Header("Required Permissions")]
        [SerializeField] private bool requireMicrophone = true;
        [SerializeField] private bool requireInternet = true;

        [Header("UI (optional)")]
        [Tooltip("A GameObject to show while waiting for permissions (e.g. a 'Please allow' panel).")]
        [SerializeField] private GameObject permissionPromptUI;

        /// <summary>
        /// True when ALL required permissions have been granted.
        /// </summary>
        public bool AllPermissionsGranted { get; private set; } = false;

        /// <summary>
        /// Fired once when all permissions are granted.
        /// </summary>
        public event Action OnPermissionsGranted;

        /// <summary>
        /// Singleton for easy access from any script.
        /// </summary>
        public static PermissionGate Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            StartCoroutine(RequestPermissionsCoroutine());
        }

        private IEnumerator RequestPermissionsCoroutine()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Show prompt UI if available
            if (permissionPromptUI != null)
                permissionPromptUI.SetActive(true);

            // Build list of permissions we need
            List<string> needed = new List<string>();

            if (requireMicrophone)
                needed.Add(Permission.Microphone);

            // Note: INTERNET doesn't require runtime permission on Android (auto-granted),
            // but we still check microphone which IS a dangerous permission.

            // Request each permission that hasn't been granted yet
            foreach (string perm in needed)
            {
                if (!Permission.HasUserAuthorizedPermission(perm))
                {
                    Debug.Log($"[PermissionGate] Requesting: {perm}");

                    var callbacks = new PermissionCallbacks();
                    bool responded = false;
                    bool granted = false;

                    callbacks.PermissionGranted += (_) =>
                    {
                        granted = true;
                        responded = true;
                    };
                    callbacks.PermissionDenied += (_) =>
                    {
                        granted = false;
                        responded = true;
                    };
                    callbacks.PermissionDeniedAndDontAskAgain += (_) =>
                    {
                        granted = false;
                        responded = true;
                    };

                    Permission.RequestUserPermission(perm, callbacks);

                    // Wait for the user to respond to the dialog
                    float timeout = 120f; // generous timeout
                    float waited = 0f;
                    while (!responded && waited < timeout)
                    {
                        yield return null;
                        waited += Time.unscaledDeltaTime;
                    }

                    if (!granted)
                    {
                        Debug.LogWarning($"[PermissionGate] Permission denied: {perm}. Retrying...");

                        // Keep retrying every few seconds until the user grants it
                        while (!Permission.HasUserAuthorizedPermission(perm))
                        {
                            yield return new WaitForSecondsRealtime(3f);

                            // Re-request (shows the dialog again)
                            responded = false;
                            granted = false;
                            Permission.RequestUserPermission(perm, callbacks);

                            float retryWait = 0f;
                            while (!responded && retryWait < timeout)
                            {
                                yield return null;
                                retryWait += Time.unscaledDeltaTime;
                            }
                        }
                    }

                    Debug.Log($"[PermissionGate] Granted: {perm}");
                }
                else
                {
                    Debug.Log($"[PermissionGate] Already granted: {perm}");
                }
            }

            // Hide prompt UI
            if (permissionPromptUI != null)
                permissionPromptUI.SetActive(false);

            Debug.Log("[PermissionGate] All permissions granted!");
#else
            // In Editor / non-Android: auto-pass
            Debug.Log("[PermissionGate] Non-Android platform — permissions auto-granted.");
            yield return null;
#endif

            AllPermissionsGranted = true;
            OnPermissionsGranted?.Invoke();
        }

        /// <summary>
        /// Coroutine-friendly wait. Use: yield return PermissionGate.Instance.WaitForPermissions();
        /// </summary>
        public IEnumerator WaitForPermissions()
        {
            while (!AllPermissionsGranted)
            {
                yield return null;
            }
        }
    }
}
