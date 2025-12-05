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
		private static string port = "10245";
		private static IAsyncResult _result;
		private static bool hasChangesWaitingForFocus = false;
		private static double lastFocusTime = 0;

		static AutoCompilation()
		{
			if (!SessionState.GetBool("DisableAutoComplation", false))
			{
				CompilationPipeline.compilationFinished += OnCompilationFinished;
				EditorApplication.quitting += _closeListener;
				EditorApplication.update += onUpdate;
				// Detect saat Unity window dapat fokus
				EditorApplication.focusChanged += OnFocusChanged;
				_createListener();
			}
		}

		private static void OnFocusChanged(bool hasFocus)
		{
			if (hasFocus)
			{
				lastFocusTime = EditorApplication.timeSinceStartup;
				Debug.Log("Unity Editor got focus");

				// Jika ada perubahan yang menunggu, compile sekarang
				if (hasChangesWaitingForFocus && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
				{
					hasChangesWaitingForFocus = false;
					AssetDatabase.Refresh();
					Debug.Log("Auto Compilation triggered on focus");
				}
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

				// Tandai ada perubahan, tapi jangan compile langsung
				hasChangesWaitingForFocus = true;
				Debug.Log("File change detected, waiting for focus...");

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
		}

		private static void onUpdate()
		{
			// Cek setiap beberapa saat jika ada perubahan yang menunggu
			// Dan sudah ada fokus dalam 0.5 detik terakhir
			if (hasChangesWaitingForFocus &&
				!EditorApplication.isCompiling &&
				!EditorApplication.isUpdating &&
				(EditorApplication.timeSinceStartup - lastFocusTime) < 0.5)
			{
				hasChangesWaitingForFocus = false;
				AssetDatabase.Refresh();
				Debug.Log("Auto Compilation triggered");
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
			if (listener == null)
			{
				_createListener();
			}
		}
	}
}
