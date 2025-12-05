using System;
using UnityEditor;
using UnityEngine;
using UnityEditor.Compilation;
using System.Net;
using UnityEditorInternal;
using System.Net.Sockets;

namespace PostcyberPunk.AutoCompilation
{
	[InitializeOnLoad]
	public static class AutoCompilation
	{
		private static HttpListener listener;
		private static bool needUpdate;
		private static string port = "10245";
		private static IAsyncResult _result;
		private static double lastChangeTime = 0;
		private static float debounceDelay = 2.0f; // Tunggu 2 detik setelah perubahan terakhir
		private static int changeCount = 0;

		static AutoCompilation()
		{
			if (!SessionState.GetBool("DisableAutoComplation", false))
			{
				needUpdate = false;
				CompilationPipeline.compilationFinished += OnCompilationFinished;
				EditorApplication.quitting += _closeListener;
				EditorApplication.update += onUpdate;
				_createListener();
				Debug.Log("[AutoCompilation] Initialized");
			}
		}

		private static void _createListener()
		{
			if (listener != null)
			{
				return;
			}
			try
			{
				listener = new HttpListener();
				listener.Prefixes.Add("http://127.0.0.1:" + port + "/refresh/");
				listener.Start();
				_result = listener.BeginGetContext(new AsyncCallback(OnRequest), listener);
				Debug.Log($"[AutoCompilation] Listener started on port {port}");
			}
			catch (Exception e)
			{
				Debug.LogError("Auto Compilation starting failed:" + e);
			}
		}

		private static void OnRequest(IAsyncResult result)
		{
			if (listener.IsListening)
			{
				listener.EndGetContext(result);

				// Update waktu perubahan terakhir, tapi jangan compile dulu
				lastChangeTime = EditorApplication.timeSinceStartup;
				needUpdate = true;
				changeCount++;

				Debug.Log($"[AutoCompilation] File saved (#{changeCount}) - Waiting {debounceDelay}s before compile...");

				_result = listener.BeginGetContext(new AsyncCallback(OnRequest), listener);
			}
		}

		private static void _closeListener()
		{
			if (listener == null)
			{
				return;
			}
			listener.Stop();
			listener.Close();
			listener = null;
			Debug.Log("[AutoCompilation] Listener closed");
		}

		private static void onUpdate()
		{
			// Compile hanya jika:
			// 1. Ada perubahan (needUpdate = true)
			// 2. Sudah lewat X detik sejak perubahan terakhir (debounce)
			// 3. Tidak sedang compile
			if (needUpdate &&
				!EditorApplication.isCompiling &&
				!EditorApplication.isUpdating &&
				(EditorApplication.timeSinceStartup - lastChangeTime) >= debounceDelay)
			{
				needUpdate = false;
				Debug.Log($"[AutoCompilation] Starting compilation after {changeCount} file change(s)...");
				changeCount = 0;
				AssetDatabase.Refresh();
			}
		}

		[MenuItem("Tools/AutoCompilation/Toggle Auto-Completion")]
		public static void ToggleAutoCompilation()
		{
			bool toggle = SessionState.GetBool("DisableAutoComplation", false);
			if (toggle)
			{
				_createListener();
			}
			else
			{
				_closeListener();
			}
			SessionState.SetBool("DisableAutoComplation", !toggle);
			Debug.Log("Auto Completion is " + (!toggle ? "On" : "Off"));
		}

		private static void OnCompilationFinished(object _)
		{
			Debug.Log("[AutoCompilation] Compilation finished");

			if (listener == null)
			{
				_createListener();
			}
		}
	}
}
