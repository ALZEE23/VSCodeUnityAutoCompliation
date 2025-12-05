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
		private static bool isEditorFocused = true;
		private static bool hasChangesWaitingForFocus = false;

		static AutoCompilation()
		{
			if (!SessionState.GetBool("DisableAutoComplation", false))
			{
				needUpdate = false;
				CompilationPipeline.compilationFinished += OnCompilationFinished;
				EditorApplication.quitting += _closeListener;
				EditorApplication.update += onUpdate;
				_createListener();
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

				// Jika editor tidak fokus, tandai ada perubahan tapi jangan compile
				if (!isEditorFocused)
				{
					hasChangesWaitingForFocus = true;
				}
				else if (!EditorApplication.isCompiling)
				{
					// Jika fokus dan tidak sedang compile, compile sekarang
					needUpdate = true;
				}

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
			// Check focus change
			if (InternalEditorUtility.isApplicationActive != isEditorFocused)
			{
				isEditorFocused = !isEditorFocused;

				// Ketika editor dapat fokus kembali
				if (isEditorFocused)
				{
					// Jika ada perubahan yang menunggu, compile sekarang
					if (hasChangesWaitingForFocus && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
					{
						hasChangesWaitingForFocus = false;
						AssetDatabase.Refresh();
						Debug.Log("Auto Compilation triggered on focus");
					}
				}
			}

			// Compile jika ada update dan editor fokus
			if (isEditorFocused && !EditorApplication.isCompiling && !EditorApplication.isUpdating && needUpdate)
			{
				needUpdate = false;
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
			// Listener tetap jalan terus untuk detect perubahan
			if (listener == null)
			{
				_createListener();
			}
		}
	}
}
